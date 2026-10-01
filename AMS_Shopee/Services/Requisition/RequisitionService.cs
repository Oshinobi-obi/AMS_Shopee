// RequisitionService.cs
// Assumes EF Core entities scaffolded from the migrated ams_stockwatch schema
// (INT UNSIGNED columns mapped to int for readability — adjust if yours are uint).
// Register as:  builder.Services.AddScoped<RequisitionService>();
//               builder.Services.AddDbContextFactory<AmsDbContext>(...);
// A DbContext FACTORY is used on purpose: in Blazor Server a scoped DbContext lives
// for the whole circuit and is not safe for concurrent or long-lived use.

using System.Data;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Requisition;

// ───────────────────────────── Result types ─────────────────────────────

public enum StockLevel { InStock, LowStock, OutOfStock }

public sealed record QuotaBalance(int Allocated, int Issued, int Pending, int InCart)
{
    public int RequestedToDate => Issued + Pending;               // banner: "Requested to Date"
    public int Remaining       => Math.Max(0, Allocated - Issued - Pending);
    public int CanStillAdd     => Math.Max(0, Remaining - InCart);
}

public enum IssueSeverity { Error, Warning }
public sealed record CartIssue(IssueSeverity Severity, string Code, string Title, string Message);

public sealed record CartCheck(bool Allowed, IReadOnlyList<CartIssue> Issues, QuotaBalance? Balance)
{
    public static CartCheck Block(string code, string title, string msg, QuotaBalance? b = null) =>
        new(false, [new CartIssue(IssueSeverity.Error, code, title, msg)], b);
}

public sealed record OpResult(bool Ok, string Message, IReadOnlyList<string>? Details = null, int? RisId = null);

/// Built once per request from the auth cookie's claims.
public sealed record CurrentUser(int UserId, string Role, int? OfficeId, bool CanRequisition)
{
    public bool IsAmsAdmin => Role is "SuperAdmin" or "Admin";
    public bool IsOffice   => Role == "Office" && OfficeId is not null && CanRequisition;
}

// ───────────────────────────── Service ─────────────────────────────

public sealed class RequisitionService(
    IDbContextFactory<AmsDbContext> dbFactory,
    TimeProvider clock,
    ILogger<RequisitionService> log)
{
    private static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
    private DateTime NowPh => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Manila).DateTime;
    public int FiscalYear => NowPh.Year;

    public static StockLevel LevelOf(int onHand, int reorderLevel) =>
        onHand <= 0 ? StockLevel.OutOfStock
      : onHand <= reorderLevel ? StockLevel.LowStock
      : StockLevel.InStock;

    // ── Balance (used by the item banner AND every validation) ──────────
    private static async Task<QuotaBalance?> BalanceAsync(
        AmsDbContext db, int officeId, int itemId, int year, int? cartUserId, CancellationToken ct)
    {
        var alloc = await db.AppCseAllocations.AsNoTracking()
            .Where(a => a.OfficeId == officeId && a.ItemId == itemId && a.FiscalYear == year)
            .Select(a => new { a.AllocatedQty, a.IssuedQty })
            .FirstOrDefaultAsync(ct);
        if (alloc is null) return null;                     // item not in this office's APP-CSE

        // Pending requests RESERVE quota. Without this, an office could file three
        // pending RIS for the same 30 remaining pcs and all three would look valid.
        var pending = await db.RisItems
            .Where(i => i.ItemId == itemId
                     && i.Ris.OfficeId == officeId
                     && i.Ris.FiscalYear == year
                     && i.Ris.Status == "PendingApproval")
            .SumAsync(i => (int?)i.RequestedQty, ct) ?? 0;

        var inCart = cartUserId is null ? 0 : await db.CartItems
            .Where(c => c.UserId == cartUserId && c.ItemId == itemId)
            .Select(c => (int?)c.Quantity).FirstOrDefaultAsync(ct) ?? 0;

        return new QuotaBalance(alloc.AllocatedQty, alloc.IssuedQty, pending, inCart);
    }

    public async Task<QuotaBalance?> GetBalanceAsync(CurrentUser u, int itemId, CancellationToken ct = default)
    {
        if (!u.IsOffice) return null;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await BalanceAsync(db, u.OfficeId!.Value, itemId, FiscalYear, u.UserId, ct);
    }

    // ── 1. ADD-TO-CART VALIDATION ───────────────────────────────────────
    // Rule set:
    //   HARD (blocks): not logged in, not a requesting office, inactive item,
    //                  item not in APP-CSE, cart qty > remaining APP-CSE balance.
    //   SOFT (warns):  cart qty > warehouse stock. The RIS itself has a
    //                  "Stock Available? Yes/No" column, so an office may still
    //                  request; AMS decides at approval (partial issue / wait).
    public async Task<CartCheck> ValidateAddToCartAsync(
        CurrentUser? u, int itemId, int qtyToAdd, CancellationToken ct = default)
    {
        if (u is null)
            return CartCheck.Block("LOGIN_REQUIRED", "Please log in",
                "Log in with your office account to add items to your requisition.");
        if (!u.IsOffice)
            return CartCheck.Block("NOT_REQUESTING_OFFICE", "This account can't request supplies",
                "Only office accounts can file requisitions. AMS accounts manage the catalog and approvals.");
        if (qtyToAdd is < 1 or > 10_000)
            return CartCheck.Block("INVALID_QTY", "Check the quantity", "Enter a whole number of at least 1.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var item = await db.SupplyItems.AsNoTracking()
            .Where(i => i.ItemId == itemId)
            .Select(i => new { i.ItemName, i.UnitOfMeasure, i.StockOnHand, i.IsActive })
            .FirstOrDefaultAsync(ct);
        if (item is null || !item.IsActive)
            return CartCheck.Block("ITEM_UNAVAILABLE", "Item unavailable",
                "This item is no longer in the catalog.");

        var year = FiscalYear;
        var bal = await BalanceAsync(db, u.OfficeId!.Value, itemId, year, u.UserId, ct);
        if (bal is null)
            return CartCheck.Block("NOT_IN_APP_CSE", "Not in your APP-CSE",
                $"{item.ItemName} isn't in your office's {year} APP-CSE, so it can't be requested yet. " +
                "Coordinate with AMS if your APP needs to be updated.");

        var newCartQty = bal.InCart + qtyToAdd;
        if (newCartQty > bal.Remaining)
        {
            var msg = bal.CanStillAdd == 0
                ? $"Your office has no remaining APP-CSE balance for {item.ItemName} this year."
                : $"You can add up to {bal.CanStillAdd} more {item.UnitOfMeasure} of {item.ItemName}.";
            return CartCheck.Block("EXCEEDS_APP_CSE", "Exceeds APP-CSE balance",
                $"{msg} (APP-CSE total: {bal.Allocated} · Requested to date: {bal.RequestedToDate} · " +
                $"Already in cart: {bal.InCart})", bal);
        }

        var issues = new List<CartIssue>();
        if (newCartQty > item.StockOnHand)
            issues.Add(new(IssueSeverity.Warning, "LIMITED_STOCK", "Limited warehouse stock",
                item.StockOnHand <= 0
                    ? "AMS has none in the warehouse right now. You can still request it; AMS will issue it once restocked."
                    : $"AMS currently has {item.StockOnHand} {item.UnitOfMeasure}. Your request may be partially issued."));

        return new CartCheck(true, issues, bal with { InCart = newCartQty });
    }

    public async Task<CartCheck> AddToCartAsync(CurrentUser? u, int itemId, int qty, CancellationToken ct = default)
    {
        var check = await ValidateAddToCartAsync(u, itemId, qty, ct);
        if (!check.Allowed) return check;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var line = await db.CartItems.FirstOrDefaultAsync(c => c.UserId == u!.UserId && c.ItemId == itemId, ct);
        if (line is null) db.CartItems.Add(new CartItem { UserId = u!.UserId, ItemId = itemId, Quantity = qty });
        else line.Quantity += qty;
        await db.SaveChangesAsync(ct);
        return check;
    }

    // ── 2. SUBMIT REQUISITION (cart → RIS, status PendingApproval) ──────
    public async Task<OpResult> SubmitAsync(
        CurrentUser u, string purpose, int? requestedByPersonnelId, CancellationToken ct = default)
    {
        if (!u.IsOffice) return new(false, "Only office accounts can submit requisitions.");
        if (string.IsNullOrWhiteSpace(purpose)) return new(false, "Please state the purpose of this requisition.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var officeId = u.OfficeId!.Value;
        var year = FiscalYear;

        // Per-office mutex: serializes submit/approve for this office so two
        // browser tabs can't both pass the quota check.
        await LockOfficeAsync(db, officeId, ct);

        var cart = await db.CartItems.Include(c => c.Item)
            .Where(c => c.UserId == u.UserId)
            .OrderBy(c => c.Item.StockNo).ToListAsync(ct);
        if (cart.Count == 0) return new(false, "Your cart is empty.");

        var problems = new List<string>();
        foreach (var line in cart)                       // re-validate: things change after add-to-cart
        {
            if (!line.Item.IsActive) { problems.Add($"{line.Item.ItemName} was removed from the catalog."); continue; }
            var bal = await BalanceAsync(db, officeId, line.ItemId, year, null, ct);
            if (bal is null) problems.Add($"{line.Item.ItemName} is no longer in your APP-CSE.");
            else if (line.Quantity > bal.Remaining)
                problems.Add($"{line.Item.ItemName}: {line.Quantity} requested, only {bal.Remaining} remaining.");
        }
        if (problems.Count > 0)
            return new(false, "Some items need your attention before submitting.", problems);  // tx disposes → rollback

        var office   = await db.Offices.Include(o => o.ParentOffice).FirstAsync(o => o.OfficeId == officeId, ct);
        var settings = await db.SystemSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);
        var now = NowPh;

        var ris = new RisTransaction
        {
            RisNo = await NextRisNumberAsync(db, now, ct),
            OfficeId = officeId,
            FiscalYear = year,
            EntityName = settings["ris.entity_name"],
            FundCluster = settings["ris.default_fund_cluster"],
            DivisionName = (office.ParentOffice ?? office).OfficeName,   // section → its division
            OfficeName = office.OfficeName,
            ResponsibilityCenterCode = office.ResponsibilityCenterCode,
            Purpose = purpose.Trim(),
            Status = "PendingApproval",
            RequestedByUserId = u.UserId,
            RequestedByPersonnelId = requestedByPersonnelId,
            RequestedAt = now,
        };
        short n = 1;
        foreach (var line in cart)
            ris.Items.Add(new RisItem
            {
                ItemId = line.ItemId, LineNo = n++,
                StockNo = line.Item.StockNo,
                UnitOfMeasure = line.Item.UnitOfMeasure,
                ItemDescription = Describe(line.Item),
                UnitCost = line.Item.UnitPrice,
                RequestedQty = line.Quantity,
                Remarks = line.Remarks,
            });
        ris.StatusHistory.Add(new RisStatusHistory { ToStatus = "PendingApproval", ChangedBy = u.UserId, ChangedAt = now });

        db.RisTransactions.Add(ris);
        db.CartItems.RemoveRange(cart);
        db.UserActivities.Add(new UserActivity { UserId = u.UserId, ActivityType = "RIS_SUBMITTED", Description = $"Submitted RIS {ris.RisNo}" });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(true, $"Requisition {ris.RisNo} was submitted to AMS.", RisId: ris.RisId);
    }

    // ── 3. APPROVE (deduct warehouse stock + APP-CSE, all-or-nothing) ───
    /// <param name="issueQty">Optional per-line override (ris_item_id → qty) for partial issue.</param>
    public async Task<OpResult> ApproveAsync(
        CurrentUser admin, int risId, IReadOnlyDictionary<int, int>? issueQty,
        int? approvedByPersonnelId, int? issuedByPersonnelId, CancellationToken ct = default)
    {
        if (!admin.IsAmsAdmin) return new(false, "Only AMS can approve requisitions.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT ris_id FROM ris_transactions WHERE ris_id = {risId} FOR UPDATE", ct);
        var ris = await db.RisTransactions.Include(r => r.Items).SingleOrDefaultAsync(r => r.RisId == risId, ct);
        if (ris is null) return new(false, "Requisition not found.");
        if (ris.Status != "PendingApproval")
            return new(false, $"{ris.RisNo} is already {Friendly(ris.Status)}. Refresh the queue.");

        await LockOfficeAsync(db, ris.OfficeId, ct);
        var now = NowPh;
        var shortfalls = new List<string>();

        foreach (var line in ris.Items.OrderBy(i => i.ItemId))   // fixed lock order → no deadlocks
        {
            var qty = issueQty?.GetValueOrDefault(line.RisItemId, line.RequestedQty) ?? line.RequestedQty;
            if (qty < 0 || qty > line.RequestedQty)
                return new(false, $"Line {line.LineNo}: issue quantity must be between 0 and {line.RequestedQty}.");

            line.IssuedQty = qty;
            line.StockAvailable = qty > 0;
            if (qty == 0) { line.Remarks ??= "Not available"; continue; }

            // Atomic, conditional deductions — the WHERE clause IS the validation.
            var stockOk = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE supply_items SET stock_on_hand = stock_on_hand - {qty}
                 WHERE item_id = {line.ItemId} AND stock_on_hand >= {qty}", ct);
            if (stockOk == 0) { shortfalls.Add($"{line.StockNo} {line.ItemDescription}: not enough warehouse stock for {qty}."); continue; }

            var quotaOk = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE app_cse_allocations SET issued_qty = issued_qty + {qty}
                 WHERE office_id = {ris.OfficeId} AND item_id = {line.ItemId}
                   AND fiscal_year = {ris.FiscalYear} AND issued_qty + {qty} <= allocated_qty", ct);
            if (quotaOk == 0) { shortfalls.Add($"{line.StockNo} {line.ItemDescription}: exceeds the office's APP-CSE balance."); continue; }

            var balanceAfter = await db.Database
                .SqlQuery<int>($"SELECT stock_on_hand AS Value FROM supply_items WHERE item_id = {line.ItemId}")
                .SingleAsync(ct);

            db.StockMovements.Add(new StockMovement
            {
                ItemId = line.ItemId, MovementType = "ISSUE", Quantity = qty, BalanceAfter = balanceAfter,
                UnitCost = line.UnitCost, ReferenceType = "RIS", ReferenceNo = ris.RisNo,
                RisId = ris.RisId, OfficeId = ris.OfficeId, PerformedBy = admin.UserId, CreatedAt = now,
            });
        }

        if (shortfalls.Count > 0)
        {
            await tx.RollbackAsync(ct);   // nothing is deducted unless everything succeeds
            return new(false, "Approval was not saved. Adjust the issue quantities and try again.", shortfalls);
        }
        if (ris.Items.All(i => i.IssuedQty == 0))
            return new(false, "Every line is set to 0. Reject the request instead.");

        ris.Status = "ApprovedForIssuance";
        ris.ApprovedByUserId = admin.UserId;
        ris.ApprovedByPersonnelId = approvedByPersonnelId;
        ris.IssuedByPersonnelId = issuedByPersonnelId;
        ris.ApprovedAt = now;
        ris.StatusHistory.Add(new RisStatusHistory { FromStatus = "PendingApproval", ToStatus = ris.Status, ChangedBy = admin.UserId, ChangedAt = now });
        db.UserActivities.Add(new UserActivity { UserId = admin.UserId, ActivityType = "RIS_APPROVED", Description = $"Approved RIS {ris.RisNo}" });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Generate the PDF AFTER commit (slow I/O must not hold row locks).
        // e.g. _ = pdfQueue.EnqueueAsync(ris.RisId);
        log.LogInformation("RIS {RisNo} approved by {UserId}", ris.RisNo, admin.UserId);
        return new(true, $"{ris.RisNo} is approved for issuance.", RisId: ris.RisId);
    }

    // ── 4. REJECT (reason required; pending reservation is released automatically) ──
    public async Task<OpResult> RejectAsync(CurrentUser admin, int risId, string reason, CancellationToken ct = default)
    {
        if (!admin.IsAmsAdmin) return new(false, "Only AMS can reject requisitions.");
        reason = reason?.Trim() ?? "";
        if (reason.Length is < 10 or > 1000)
            return new(false, "Please give a reason of at least 10 characters so the office knows what to fix.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT ris_id FROM ris_transactions WHERE ris_id = {risId} FOR UPDATE", ct);

        var ris = await db.RisTransactions.SingleOrDefaultAsync(r => r.RisId == risId, ct);
        if (ris is null) return new(false, "Requisition not found.");
        if (ris.Status != "PendingApproval")
            return new(false, $"{ris.RisNo} is already {Friendly(ris.Status)}.");

        var now = NowPh;
        ris.Status = "Rejected";
        ris.RejectionReason = reason;
        ris.RejectedByUserId = admin.UserId;
        ris.RejectedAt = now;
        db.RisStatusHistory.Add(new RisStatusHistory { RisId = risId, FromStatus = "PendingApproval", ToStatus = "Rejected", ChangedBy = admin.UserId, Note = reason, ChangedAt = now });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(true, $"{ris.RisNo} was rejected. The office can see your reason.");
    }

    // ───────────────────────────── helpers ─────────────────────────────

    private static Task LockOfficeAsync(AmsDbContext db, int officeId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT office_id FROM offices WHERE office_id = {officeId} FOR UPDATE", ct);

    /// Gap-free monthly sequence, safe under concurrency (LAST_INSERT_ID is per-connection).
    private static async Task<string> NextRisNumberAsync(AmsDbContext db, DateTime now, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ris_number_sequences (fiscal_year, seq_month, last_seq)
            VALUES ({now.Year}, {now.Month}, LAST_INSERT_ID(1))
            ON DUPLICATE KEY UPDATE last_seq = LAST_INSERT_ID(last_seq + 1)", ct);
        var seq = await db.Database.SqlQuery<ulong>($"SELECT LAST_INSERT_ID() AS Value").SingleAsync(ct);
        return $"{now:yyyy-MM}-{seq:0000}";
    }

    private static string Describe(SupplyItem i) =>
        string.IsNullOrWhiteSpace(i.Specifications) ? i.ItemName : $"{i.ItemName}, {i.Specifications}";

    private static string Friendly(string status) => status switch
    {
        "ApprovedForIssuance" => "approved for issuance",
        "PendingApproval"     => "pending approval",
        _                     => status.ToLowerInvariant(),
    };
}
