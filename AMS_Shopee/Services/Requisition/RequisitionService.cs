// RequisitionService.cs — cart validation, RIS submission, approval, rejection.
// Matches the scaffolded entities in Data/Entities (uint ids, bool? flags,
// RisItems / RisStatusHistories navigation names).
// Registered in Program.cs as Scoped; uses IDbContextFactory so every operation
// gets its own short-lived DbContext (required for Blazor Server).

using System.Data;
using System.Security.Claims;
using AMS_Shopee.Data;
using AMS_Shopee.Data.Entities;
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

public sealed record OpResult(bool Ok, string Message, IReadOnlyList<string>? Details = null, uint? RisId = null);

/// The signed-in account, read from the auth cookie's claims (see AccountEndpoints).
public sealed record CurrentUser(uint UserId, string Role, uint? OfficeId, bool CanRequisition)
{
    public bool IsAmsAdmin => Role is "SuperAdmin" or "Admin";
    public bool IsOffice   => Role == "Office" && OfficeId is not null && CanRequisition;

    public static CurrentUser? From(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) return null;
        if (!uint.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        uint? officeId = uint.TryParse(principal.FindFirstValue("office_id"), out var o) ? o : null;
        return new CurrentUser(userId,
                               principal.FindFirstValue(ClaimTypes.Role) ?? "",
                               officeId,
                               principal.FindFirstValue("can_requisition") == "1");
    }
}

// ───────────────────────────── Service ─────────────────────────────

public sealed class RequisitionService(
    IDbContextFactory<AmsDbContext> dbFactory,
    TimeProvider clock,
    ILogger<RequisitionService> log)
{
    private const string Pending  = "PendingApproval";
    private const string Approved = "ApprovedForIssuance";
    private const string Rejected = "Rejected";

    private static readonly TimeZoneInfo Manila = AMS_Shopee.Services.PhZone.Manila;
    private DateTime NowPh => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Manila).DateTime;
    public ushort FiscalYear => (ushort)NowPh.Year;

    public static StockLevel LevelOf(int onHand, uint reorderLevel) =>
        onHand <= 0 ? StockLevel.OutOfStock
      : onHand <= reorderLevel ? StockLevel.LowStock
      : StockLevel.InStock;

    // ── Balance (used by the item banner AND every validation) ──────────
    private static async Task<QuotaBalance?> BalanceAsync(
        AmsDbContext db, uint officeId, uint itemId, ushort year, uint? cartUserId, CancellationToken ct)
    {
        var alloc = await db.AppCseAllocations.AsNoTracking()
            .Where(a => a.OfficeId == officeId && a.ItemId == itemId && a.FiscalYear == year)
            .Select(a => new { a.AllocatedQty, a.IssuedQty })
            .FirstOrDefaultAsync(ct);
        if (alloc is null) return null;                     // item not in this office's APP-CSE

        // Pending requests RESERVE quota, so several pending RIS can't double-book it.
        var pending = await db.RisItems
            .Where(i => i.ItemId == itemId
                     && i.Ris.OfficeId == officeId
                     && i.Ris.FiscalYear == year
                     && i.Ris.Status == Pending)
            .SumAsync(i => (long)i.RequestedQty, ct);

        var inCart = cartUserId is null ? 0u : await db.CartItems
            .Where(c => c.UserId == cartUserId && c.ItemId == itemId)
            .Select(c => (uint?)c.Quantity)
            .FirstOrDefaultAsync(ct) ?? 0u;

        return new QuotaBalance((int)alloc.AllocatedQty, (int)alloc.IssuedQty, (int)pending, (int)inCart);
    }

    public async Task<QuotaBalance?> GetBalanceAsync(CurrentUser? u, uint itemId, CancellationToken ct = default)
    {
        if (u is null || !u.IsOffice) return null;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await BalanceAsync(db, u.OfficeId!.Value, itemId, FiscalYear, u.UserId, ct);
    }

    /// All of an office's balances in 3 queries (used by the catalog grid instead of one query per card).
    public async Task<Dictionary<uint, QuotaBalance>> GetBalancesAsync(CurrentUser? u, CancellationToken ct = default)
    {
        var result = new Dictionary<uint, QuotaBalance>();
        if (u is null || !u.IsOffice) return result;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var officeId = u.OfficeId!.Value;
        var year = FiscalYear;

        var allocations = await db.AppCseAllocations.AsNoTracking()
            .Where(a => a.OfficeId == officeId && a.FiscalYear == year)
            .Select(a => new { a.ItemId, a.AllocatedQty, a.IssuedQty })
            .ToListAsync(ct);

        var pending = await db.RisItems
            .Where(i => i.Ris.OfficeId == officeId && i.Ris.FiscalYear == year && i.Ris.Status == Pending)
            .GroupBy(i => i.ItemId)
            .Select(g => new { ItemId = g.Key, Qty = g.Sum(i => (long)i.RequestedQty) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Qty, ct);

        var inCart = await db.CartItems
            .Where(c => c.UserId == u.UserId)
            .ToDictionaryAsync(c => c.ItemId, c => c.Quantity, ct);

        foreach (var a in allocations)
            result[a.ItemId] = new QuotaBalance(
                (int)a.AllocatedQty,
                (int)a.IssuedQty,
                (int)pending.GetValueOrDefault(a.ItemId),
                (int)inCart.GetValueOrDefault(a.ItemId));
        return result;
    }

    /// Number of different items in the user's cart (for the top-bar badge).
    public async Task<int> GetCartLineCountAsync(CurrentUser? u, CancellationToken ct = default)
    {
        if (u is null || !u.IsOffice) return 0;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CartItems.CountAsync(c => c.UserId == u.UserId, ct);
    }

    // ── 1. ADD-TO-CART VALIDATION ───────────────────────────────────────
    //   HARD (blocks): not logged in, not a requesting office, inactive item,
    //                  item not in APP-CSE, cart qty > remaining APP-CSE balance.
    //   SOFT (warns):  cart qty > warehouse stock (RIS has "Stock Available? Yes/No";
    //                  AMS decides at approval).
    public async Task<CartCheck> ValidateAddToCartAsync(
        CurrentUser? u, uint itemId, int qtyToAdd, CancellationToken ct = default)
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
            .Select(i => new { i.ItemName, i.UnitOfMeasure, i.StockOnHand, Active = i.IsActive ?? true })
            .FirstOrDefaultAsync(ct);
        if (item is null || !item.Active)
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

    public async Task<CartCheck> AddToCartAsync(CurrentUser? u, uint itemId, int qty, CancellationToken ct = default)
    {
        var check = await ValidateAddToCartAsync(u, itemId, qty, ct);
        if (!check.Allowed) return check;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var line = await db.CartItems.FirstOrDefaultAsync(c => c.UserId == u!.UserId && c.ItemId == itemId, ct);
        if (line is null)
            db.CartItems.Add(new CartItem { UserId = u!.UserId, ItemId = itemId, Quantity = (uint)qty });
        else
            line.Quantity += (uint)qty;
        await db.SaveChangesAsync(ct);
        return check;
    }

    /// Sets a cart line to an exact quantity (the cart page's stepper). Same rules as adding.
    public async Task<CartCheck> SetCartQuantityAsync(CurrentUser? u, uint itemId, int qty, CancellationToken ct = default)
    {
        if (u is null || !u.IsOffice)
            return CartCheck.Block("NOT_REQUESTING_OFFICE", "This account can't request supplies",
                "Only office accounts can file requisitions.");
        if (qty is < 1 or > 10_000)
            return CartCheck.Block("INVALID_QTY", "Check the quantity", "Enter a whole number of at least 1.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var line = await db.CartItems.Include(c => c.Item)
            .FirstOrDefaultAsync(c => c.UserId == u.UserId && c.ItemId == itemId, ct);
        if (line is null)
            return CartCheck.Block("NOT_IN_CART", "Item not in your cart", "Refresh the page; this item was already removed.");

        // Balance WITHOUT the cart (null user) — the new quantity replaces the cart amount.
        var bal = await BalanceAsync(db, u.OfficeId!.Value, itemId, FiscalYear, null, ct);
        if (bal is null)
            return CartCheck.Block("NOT_IN_APP_CSE", "Not in your APP-CSE",
                $"{line.Item.ItemName} is no longer in your office's APP-CSE. Remove it from your cart.");
        if (qty > bal.Remaining)
            return CartCheck.Block("EXCEEDS_APP_CSE", "Exceeds APP-CSE balance",
                bal.Remaining == 0
                    ? $"Your office has no remaining APP-CSE balance for {line.Item.ItemName}. Remove it from your cart."
                    : $"You can request up to {bal.Remaining} {line.Item.UnitOfMeasure} of {line.Item.ItemName}. " +
                      $"(APP-CSE total: {bal.Allocated} · Requested to date: {bal.RequestedToDate})", bal);

        line.Quantity = (uint)qty;
        await db.SaveChangesAsync(ct);

        var issues = new List<CartIssue>();
        if (qty > line.Item.StockOnHand)
            issues.Add(new(IssueSeverity.Warning, "LIMITED_STOCK", "Limited warehouse stock",
                $"AMS currently has {line.Item.StockOnHand} {line.Item.UnitOfMeasure}. Your request may be partially issued."));
        return new CartCheck(true, issues, bal with { InCart = qty });
    }

    public async Task RemoveFromCartAsync(CurrentUser? u, uint itemId, CancellationToken ct = default)
    {
        if (u is null) return;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.CartItems.Where(c => c.UserId == u.UserId && c.ItemId == itemId).ExecuteDeleteAsync(ct);
    }

    // ── 2. SUBMIT REQUISITION (cart → RIS, status PendingApproval) ──────
    public async Task<OpResult> SubmitAsync(
        CurrentUser u, string purpose, uint? requestedByPersonnelId, uint? receivedByPersonnelId, CancellationToken ct = default)
    {
        if (!u.IsOffice) return new(false, "Only office accounts can submit requisitions.");
        if (string.IsNullOrWhiteSpace(purpose)) return new(false, "Please state the purpose of this requisition.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var officeId = u.OfficeId!.Value;
        var year = FiscalYear;

        // Per-office mutex: two browser tabs can't both pass the quota check.
        await LockOfficeAsync(db, officeId, ct);

        var cart = await db.CartItems.Include(c => c.Item)
            .Where(c => c.UserId == u.UserId)
            .OrderBy(c => c.Item.StockNo)
            .ToListAsync(ct);
        if (cart.Count == 0) return new(false, "Your cart is empty.");

        var problems = new List<string>();
        foreach (var line in cart)   // re-validate: things change after add-to-cart
        {
            if (line.Item.IsActive == false) { problems.Add($"{line.Item.ItemName} was removed from the catalog."); continue; }
            var bal = await BalanceAsync(db, officeId, line.ItemId, year, null, ct);
            if (bal is null) problems.Add($"{line.Item.ItemName} is no longer in your APP-CSE.");
            else if ((int)line.Quantity > bal.Remaining)
                problems.Add($"{line.Item.ItemName}: {line.Quantity} requested, only {bal.Remaining} remaining.");
        }
        if (problems.Count > 0)
            return new(false, "Some items need your attention before submitting.", problems);   // rollback on dispose

        // Signatories chosen by the office must be its own personnel.
        foreach (var pid in new[] { requestedByPersonnelId, receivedByPersonnelId }.OfType<uint>())
            if (!await db.RoPersonnel.AnyAsync(p => p.PersonnelId == pid && p.OfficeId == officeId, ct))
                return new(false, "Choose people from your own office for Requested by and Received by.");

        var office   = await db.Offices.Include(o => o.ParentOffice).FirstAsync(o => o.OfficeId == officeId, ct);
        var settings = await db.SystemSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);
        var now = NowPh;
        uint? defaultApprover = uint.TryParse(settings.GetValueOrDefault("ris.approved_by_personnel_id"), out var approverId)
            ? approverId : null;
        uint? defaultIssuer = uint.TryParse(settings.GetValueOrDefault("ris.issued_by_personnel_id"), out var issuerId)
            ? issuerId : null;

        var ris = new RisTransaction
        {
            RisNo = await NextRisNumberAsync(db, now, ct),
            OfficeId = officeId,
            FiscalYear = year,
            EntityName = settings.GetValueOrDefault("ris.entity_name", "Entity name not set"),
            FundCluster = settings.GetValueOrDefault("ris.default_fund_cluster", ""),
            DivisionName = (office.ParentOffice ?? office).OfficeName,   // section → its division
            OfficeName = office.OfficeName,
            ResponsibilityCenterCode = office.ResponsibilityCenterCode,
            Purpose = purpose.Trim(),
            Status = Pending,
            RequestedByUserId = u.UserId,
            RequestedByPersonnelId = requestedByPersonnelId,
            RequestedAt = now,
            // Default approving official printed on the slip; AMS can change it when approving.
            ApprovedByPersonnelId = defaultApprover,
            IssuedByPersonnelId = defaultIssuer,
            ReceivedByPersonnelId = receivedByPersonnelId,
        };
        ushort lineNo = 1;
        foreach (var line in cart)
            ris.RisItems.Add(new RisItem
            {
                ItemId = line.ItemId,
                LineNo = lineNo++,
                StockNo = line.Item.StockNo,
                UnitOfMeasure = line.Item.UnitOfMeasure,
                ItemDescription = Describe(line.Item),
                UnitCost = line.Item.UnitPrice,
                RequestedQty = line.Quantity,
                Remarks = line.Remarks,
            });
        ris.RisStatusHistories.Add(new RisStatusHistory { ToStatus = Pending, ChangedBy = u.UserId, ChangedAt = now });

        db.RisTransactions.Add(ris);
        db.CartItems.RemoveRange(cart);
        db.UserActivities.Add(new UserActivity
        {
            UserId = u.UserId, ActivityType = "RIS_SUBMITTED", Description = $"Submitted RIS {ris.RisNo}",
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(true, $"Requisition {ris.RisNo} was submitted to AMS.", RisId: ris.RisId);
    }

    // ── 3. APPROVE (deduct warehouse stock + APP-CSE, all-or-nothing) ───
    /// <param name="issueQty">Optional per-line override (ris_item_id → qty) for partial issue.</param>
    public async Task<OpResult> ApproveAsync(
        CurrentUser admin, uint risId, IReadOnlyDictionary<uint, int>? issueQty,
        uint? approvedByPersonnelId, uint? issuedByPersonnelId, CancellationToken ct = default)
    {
        if (!admin.IsAmsAdmin) return new(false, "Only AMS can approve requisitions.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT ris_id FROM ris_transactions WHERE ris_id = {risId} FOR UPDATE", ct);
        var ris = await db.RisTransactions.Include(r => r.RisItems)
            .SingleOrDefaultAsync(r => r.RisId == risId, ct);
        if (ris is null) return new(false, "Requisition not found.");
        if (ris.Status != Pending)
            return new(false, $"{ris.RisNo} is already {Friendly(ris.Status)}. Refresh the queue.");

        await LockOfficeAsync(db, ris.OfficeId, ct);
        var now = NowPh;
        var shortfalls = new List<string>();

        foreach (var line in ris.RisItems.OrderBy(i => i.ItemId))   // fixed lock order → no deadlocks
        {
            var requested = (int)line.RequestedQty;
            var qty = issueQty is not null && issueQty.TryGetValue(line.RisItemId, out var o) ? o : requested;
            if (qty < 0 || qty > requested)
                return new(false, $"Line {line.LineNo}: issue quantity must be between 0 and {requested}.");

            line.IssuedQty = (uint)qty;
            line.StockAvailable = qty > 0;
            if (qty == 0) { line.Remarks ??= "Not available"; continue; }

            // Atomic, conditional deductions — the WHERE clause IS the validation.
            var stockOk = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE supply_items SET stock_on_hand = stock_on_hand - {qty}
                 WHERE item_id = {line.ItemId} AND stock_on_hand >= {qty}", ct);
            if (stockOk == 0)
            {
                shortfalls.Add($"{line.StockNo} {line.ItemDescription}: not enough warehouse stock for {qty}.");
                continue;
            }

            var quotaOk = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE app_cse_allocations SET issued_qty = issued_qty + {qty}
                 WHERE office_id = {ris.OfficeId} AND item_id = {line.ItemId}
                   AND fiscal_year = {ris.FiscalYear} AND issued_qty + {qty} <= allocated_qty", ct);
            if (quotaOk == 0)
            {
                shortfalls.Add($"{line.StockNo} {line.ItemDescription}: exceeds the office's APP-CSE balance.");
                continue;
            }

            var balanceAfter = await db.Database
                .SqlQuery<int>($"SELECT stock_on_hand AS Value FROM supply_items WHERE item_id = {line.ItemId}")
                .SingleAsync(ct);

            db.StockMovements.Add(new StockMovement
            {
                ItemId = line.ItemId,
                MovementType = "ISSUE",
                Quantity = (uint)qty,
                BalanceAfter = balanceAfter,
                UnitCost = line.UnitCost,
                ReferenceType = "RIS",
                ReferenceNo = ris.RisNo,
                RisId = ris.RisId,
                OfficeId = ris.OfficeId,
                PerformedBy = admin.UserId,
                CreatedAt = now,
            });
        }

        if (shortfalls.Count > 0)
        {
            await tx.RollbackAsync(ct);   // nothing is deducted unless everything succeeds
            return new(false, "Approval was not saved. Adjust the issue quantities and try again.", shortfalls);
        }
        if (ris.RisItems.All(i => i.IssuedQty == 0))
            return new(false, "Every line is set to 0. Reject the request instead.");

        ris.Status = Approved;
        ris.ApprovedByUserId = admin.UserId;
        ris.ApprovedByPersonnelId = approvedByPersonnelId ?? ris.ApprovedByPersonnelId;   // keep the default if none chosen
        ris.IssuedByPersonnelId = issuedByPersonnelId;
        ris.ApprovedAt = now;
        ris.RisStatusHistories.Add(new RisStatusHistory
        {
            FromStatus = Pending, ToStatus = Approved, ChangedBy = admin.UserId, ChangedAt = now,
        });
        db.UserActivities.Add(new UserActivity
        {
            UserId = admin.UserId, ActivityType = "RIS_APPROVED", Description = $"Approved RIS {ris.RisNo}",
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Generate the PDF AFTER commit (slow I/O must not hold row locks).
        log.LogInformation("RIS {RisNo} approved by user {UserId}", ris.RisNo, admin.UserId);
        return new(true, $"{ris.RisNo} is approved for issuance.", RisId: ris.RisId);
    }

    // ── 4. REJECT (reason required; the pending reservation is released automatically) ──
    public async Task<OpResult> RejectAsync(CurrentUser admin, uint risId, string reason, CancellationToken ct = default)
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
        if (ris.Status != Pending)
            return new(false, $"{ris.RisNo} is already {Friendly(ris.Status)}.");

        var now = NowPh;
        ris.Status = Rejected;
        ris.RejectionReason = reason;
        ris.RejectedByUserId = admin.UserId;
        ris.RejectedAt = now;
        db.RisStatusHistories.Add(new RisStatusHistory
        {
            RisId = risId, FromStatus = Pending, ToStatus = Rejected,
            ChangedBy = admin.UserId, Note = reason, ChangedAt = now,
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(true, $"{ris.RisNo} was rejected. The office can see your reason.");
    }

    // ── 6. RECEIVED BY (office names who will pick up the items) ────────
    public async Task<OpResult> SetReceiverAsync(CurrentUser u, uint risId, uint? personnelId, CancellationToken ct = default)
    {
        if (!u.IsOffice) return new(false, "Only office accounts can do this.");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ris = await db.RisTransactions.FirstOrDefaultAsync(r => r.RisId == risId && r.OfficeId == u.OfficeId, ct);
        if (ris is null) return new(false, "Requisition not found.");
        if (ris.Status is not (Pending or Approved))
            return new(false, $"{ris.RisNo} is already {Friendly(ris.Status)}, so the receiver can't be changed here.");
        if (personnelId is uint pid && !await db.RoPersonnel.AnyAsync(p => p.PersonnelId == pid && p.OfficeId == u.OfficeId, ct))
            return new(false, "Choose someone from your own office.");

        ris.ReceivedByPersonnelId = personnelId;
        await db.SaveChangesAsync(ct);
        return new(true, "Saved. This name will be printed under Received by.", RisId: risId);
    }

    // ── 5. CANCEL (office withdraws its own pending request) ───────────
    public async Task<OpResult> CancelAsync(CurrentUser u, uint risId, CancellationToken ct = default)
    {
        if (!u.IsOffice) return new(false, "Only office accounts can cancel requisitions.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT ris_id FROM ris_transactions WHERE ris_id = {risId} FOR UPDATE", ct);

        var ris = await db.RisTransactions.SingleOrDefaultAsync(r => r.RisId == risId && r.OfficeId == u.OfficeId, ct);
        if (ris is null) return new(false, "Requisition not found.");
        if (ris.Status != Pending)
            return new(false, $"{ris.RisNo} can no longer be cancelled because it's already {Friendly(ris.Status)}.");

        var now = NowPh;
        ris.Status = "Cancelled";
        db.RisStatusHistories.Add(new RisStatusHistory
        {
            RisId = risId, FromStatus = Pending, ToStatus = "Cancelled", ChangedBy = u.UserId,
            Note = "Cancelled by the office.", ChangedAt = now,
        });
        db.UserActivities.Add(new UserActivity { UserId = u.UserId, ActivityType = "RIS_CANCELLED", Description = $"Cancelled RIS {ris.RisNo}" });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(true, $"{ris.RisNo} was cancelled. Its quantities are back in your APP-CSE balance.", RisId: risId);
    }

    // ───────────────────────────── helpers ─────────────────────────────

    private static Task LockOfficeAsync(AmsDbContext db, uint officeId, CancellationToken ct) =>
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

    private static string Describe(SupplyItem i)
    {
        var text = string.IsNullOrWhiteSpace(i.Specifications) ? i.ItemName : $"{i.ItemName}, {i.Specifications}";
        return text.Length <= 700 ? text : text[..697] + "...";   // ris_items.item_description is VARCHAR(700)
    }

    private static string Friendly(string status) => status switch
    {
        Approved => "approved for issuance",
        Pending  => "pending approval",
        _        => status.ToLowerInvariant(),
    };
}
