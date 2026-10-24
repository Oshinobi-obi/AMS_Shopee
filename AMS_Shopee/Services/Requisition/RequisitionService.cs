// This is the main requisition logic: checking the cart, submitting the RIS, approving and rejecting.
// It follows the scaffolded entities in Data/Entities (uint ids, bool? flags,
// RisItems / RisStatusHistories names).
// It's registered as Scoped in Program.cs and uses IDbContextFactory so every operation
// gets its own short-lived DbContext (Blazor Server needs this).

using System.Data;
using System.Security.Claims;
using AMS_Shopee.Data;
using AMS_Shopee.Services.Catalog;
using AMS_Shopee.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Requisition;

// Result types

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

/// The logged-in account, read from the claims in the login cookie (see AccountEndpoints).
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

// Service

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

    // This is the admin toggle "Require APP-CSE for Requisitions" (system_settings).
    // When on (the default), items must be in the office's APP-CSE and within its balance.
    // When off, offices can request any catalog item in any quantity (AMS still checks warehouse stock).
    public const string RequireAppCseKey = "requisition.require_appcse";

    // This reads the toggle. Missing or anything other than "0" means on, so old databases keep the strict rule.
    private static async Task<bool> RequireAppCseAsync(AmsDbContext db, CancellationToken ct)
    {
        var v = await db.SystemSettings.AsNoTracking()
            .Where(x => x.SettingKey == RequireAppCseKey).Select(x => x.SettingValue).FirstOrDefaultAsync(ct);
        return v != "0";
    }

    // This lets the catalog and cart pages know whether to show and enforce the APP-CSE limit.
    public async Task<bool> AppCseRequiredAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await RequireAppCseAsync(db, ct);
    }

    public static StockLevel LevelOf(int onHand, uint reorderLevel) =>
        onHand <= 0 ? StockLevel.OutOfStock
      : onHand <= reorderLevel ? StockLevel.LowStock
      : StockLevel.InStock;

    // Balance (used by the item banner AND every check)
    private static async Task<QuotaBalance?> BalanceAsync(
        AmsDbContext db, uint officeId, uint itemId, ushort year, uint? cartUserId, CancellationToken ct)
    {
        // An office can use its division's APP-CSE (like LRPO uses ESSD's), see AppCseScope.
        var scope = await AppCseScope.ForAsync(db, officeId, ct);
        var sharing = scope.SharingOfficeIds.ToList();
        var alloc = await db.AppCseAllocations.AsNoTracking()
            .Where(a => a.OfficeId == scope.BudgetOfficeId && a.ItemId == itemId && a.FiscalYear == year)
            .Select(a => new { a.AllocatedQty, a.IssuedQty })
            .FirstOrDefaultAsync(ct);
        if (alloc is null) return null;                     // item isn't in this office's APP-CSE

        // Pending requests RESERVE the quota, so two pending RIS can't use the same balance.
        var pending = await db.RisItems
            .Where(i => i.ItemId == itemId
                     && sharing.Contains(i.Ris.OfficeId)
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

    /// All balances of an office in 3 queries (the catalog uses this instead of one query per card).
    public async Task<Dictionary<uint, QuotaBalance>> GetBalancesAsync(CurrentUser? u, CancellationToken ct = default)
    {
        var result = new Dictionary<uint, QuotaBalance>();
        if (u is null || !u.IsOffice) return result;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var officeId = u.OfficeId!.Value;
        var year = FiscalYear;

        var scope = await AppCseScope.ForAsync(db, officeId, ct);
        var sharing = scope.SharingOfficeIds.ToList();
        var allocations = await db.AppCseAllocations.AsNoTracking()
            .Where(a => a.OfficeId == scope.BudgetOfficeId && a.FiscalYear == year)
            .Select(a => new { a.ItemId, a.AllocatedQty, a.IssuedQty })
            .ToListAsync(ct);

        var pending = await db.RisItems
            .Where(i => sharing.Contains(i.Ris.OfficeId) && i.Ris.FiscalYear == year && i.Ris.Status == Pending)
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

    /// Number of different items in the cart (for the badge in the top bar).
    public async Task<int> GetCartLineCountAsync(CurrentUser? u, CancellationToken ct = default)
    {
        if (u is null || !u.IsOffice) return 0;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CartItems.CountAsync(c => c.UserId == u.UserId, ct);
    }

    // 1. ADD TO CART CHECKS
    //   BLOCKS: not logged in, not a requesting office, item turned off,
    //           item not in the APP-CSE, cart qty more than the APP-CSE balance left.
    //   WARNS:  cart qty more than the warehouse stock (the RIS has "Stock Available? Yes/No"
    //           and AMS decides when approving).
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
        var requireAppCse = await RequireAppCseAsync(db, ct);
        var bal = await BalanceAsync(db, u.OfficeId!.Value, itemId, year, u.UserId, ct);
        if (requireAppCse && bal is null)
            return CartCheck.Block("NOT_IN_APP_CSE", "Not in your APP-CSE",
                $"{item.ItemName} isn't in your office's {year} APP-CSE, so it can't be requested yet. " +
                "Coordinate with AMS if your APP needs to be updated.");

        // When APP-CSE is off, an item may have no allocation row, so I read the cart quantity on its own.
        var inCart = bal?.InCart ?? (int)(await db.CartItems
            .Where(c => c.UserId == u.UserId && c.ItemId == itemId).Select(c => (uint?)c.Quantity).FirstOrDefaultAsync(ct) ?? 0u);
        var newCartQty = inCart + qtyToAdd;
        if (requireAppCse && bal is not null && newCartQty > bal.Remaining)
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

        return new CartCheck(true, issues, bal is null ? null : bal with { InCart = newCartQty });
    }

    /// Adds to the cart. Items with colors need a color, and the line keeps how many of each color.
    public async Task<CartCheck> AddToCartAsync(CurrentUser? u, uint itemId, int qty, string? color = null, CancellationToken ct = default)
    {
        var check = await ValidateAddToCartAsync(u, itemId, qty, ct);
        if (!check.Allowed) return check;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var options = ItemColors.Parse(await db.SupplyItems.Where(i => i.ItemId == itemId).Select(i => i.ColorOptions).FirstOrDefaultAsync(ct));
        string? chosen = null;
        if (options.Count > 0)
        {
            chosen = options.FirstOrDefault(c => c.Name.Equals(ItemColors.Clean(color), StringComparison.OrdinalIgnoreCase))?.Name;
            if (chosen is null)
                return CartCheck.Block("CHOOSE_COLOR", "Choose a color", "This item comes in different colors. Open it and choose the color you need.");
        }

        var line = await db.CartItems.FirstOrDefaultAsync(c => c.UserId == u!.UserId && c.ItemId == itemId, ct);
        if (line is null)
        {
            line = new CartItem { UserId = u!.UserId, ItemId = itemId, Quantity = (uint)qty };
            if (chosen is not null) line.Colors = ItemColors.FormatQty([(chosen, qty)]);
            db.CartItems.Add(line);
        }
        else if (chosen is not null)
        {
            // Units added before the item had colors stay as "any color"
            var colors = ItemColors.ParseQty(line.Colors);
            var untagged = Math.Max(0, (int)line.Quantity - colors.Sum(c => c.Qty));
            colors.Add((chosen, qty));
            colors = ItemColors.ParseQty(ItemColors.FormatQty(colors));   // merges the same color
            line.Colors = ItemColors.FormatQty(colors);
            line.Quantity = (uint)(colors.Sum(c => c.Qty) + untagged);
        }
        else
            line.Quantity += (uint)qty;
        await db.SaveChangesAsync(ct);
        return check;
    }

    /// Changes how many of one color are in the cart (0 removes the color, nothing left removes the line).
    public async Task<CartCheck> SetCartColorQuantityAsync(CurrentUser? u, uint itemId, string color, int qty, CancellationToken ct = default)
    {
        if (u is null || !u.IsOffice)
            return CartCheck.Block("NOT_REQUESTING_OFFICE", "This account can't request supplies", "Only office accounts can file requisitions.");
        if (qty is < 0 or > 10_000)
            return CartCheck.Block("INVALID_QTY", "Check the quantity", "Enter a whole number.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var line = await db.CartItems.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == u.UserId && c.ItemId == itemId, ct);
        if (line is null)
            return CartCheck.Block("NOT_IN_CART", "Item not in your cart", "Refresh the page; this item was already removed.");

        var colors = ItemColors.ParseQty(line.Colors);
        var untagged = Math.Max(0, (int)line.Quantity - colors.Sum(c => c.Qty));
        var at = colors.FindIndex(c => c.Name.Equals(color, StringComparison.OrdinalIgnoreCase));
        if (at >= 0) colors[at] = (colors[at].Name, qty); else if (qty > 0) colors.Add((ItemColors.Clean(color), qty));
        var total = colors.Sum(c => c.Qty) + untagged;
        if (total == 0)
        {
            await RemoveFromCartAsync(u, itemId, ct);
            return new CartCheck(true, [], null);
        }
        return await SetCartQuantityAsync(u, itemId, total, ItemColors.FormatQty(colors) ?? "", ct);
    }

    /// Sets a cart line to an exact quantity (the plus/minus on the cart page). Same rules as adding.
    public Task<CartCheck> SetCartQuantityAsync(CurrentUser? u, uint itemId, int qty, CancellationToken ct = default) =>
        SetCartQuantityAsync(u, itemId, qty, null, ct);

    /// <param name="colors">The new color breakdown ("" means none), or null to keep it.</param>
    private async Task<CartCheck> SetCartQuantityAsync(CurrentUser? u, uint itemId, int qty, string? colors, CancellationToken ct)
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

        // Balance WITHOUT the cart (null user), because the new quantity replaces what's in the cart.
        var requireAppCse = await RequireAppCseAsync(db, ct);
        var bal = await BalanceAsync(db, u.OfficeId!.Value, itemId, FiscalYear, null, ct);
        if (requireAppCse && bal is null)
            return CartCheck.Block("NOT_IN_APP_CSE", "Not in your APP-CSE",
                $"{line.Item.ItemName} is no longer in your office's APP-CSE. Remove it from your cart.");
        if (requireAppCse && bal is not null && qty > bal.Remaining)
            return CartCheck.Block("EXCEEDS_APP_CSE", "Exceeds APP-CSE balance",
                bal.Remaining == 0
                    ? $"Your office has no remaining APP-CSE balance for {line.Item.ItemName}. Remove it from your cart."
                    : $"You can request up to {bal.Remaining} {line.Item.UnitOfMeasure} of {line.Item.ItemName}. " +
                      $"(APP-CSE total: {bal.Allocated} · Requested to date: {bal.RequestedToDate})", bal);

        line.Quantity = (uint)qty;
        if (colors is not null) line.Colors = colors.Length == 0 ? null : colors;
        await db.SaveChangesAsync(ct);

        var issues = new List<CartIssue>();
        if (qty > line.Item.StockOnHand)
            issues.Add(new(IssueSeverity.Warning, "LIMITED_STOCK", "Limited warehouse stock",
                $"AMS currently has {line.Item.StockOnHand} {line.Item.UnitOfMeasure}. Your request may be partially issued."));
        return new CartCheck(true, issues, bal is null ? null : bal with { InCart = qty });
    }

    public async Task RemoveFromCartAsync(CurrentUser? u, uint itemId, CancellationToken ct = default)
    {
        if (u is null) return;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.CartItems.Where(c => c.UserId == u.UserId && c.ItemId == itemId).ExecuteDeleteAsync(ct);
    }

    // 2. SUBMIT REQUISITION (cart becomes an RIS with status PendingApproval)
    public async Task<OpResult> SubmitAsync(
        CurrentUser u, string purpose, uint? requestedByPersonnelId, uint? receivedByPersonnelId, CancellationToken ct = default)
    {
        if (!u.IsOffice) return new(false, "Only office accounts can submit requisitions.");
        if (string.IsNullOrWhiteSpace(purpose)) return new(false, "Please state the purpose of this requisition.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var officeId = u.OfficeId!.Value;
        var year = FiscalYear;

        // Lock per office so two browser tabs can't both pass the quota check.
        await LockOfficeAsync(db, officeId, ct);

        var cart = await db.CartItems.Include(c => c.Item)
            .Where(c => c.UserId == u.UserId)
            .OrderBy(c => c.Item.StockNo)
            .ToListAsync(ct);
        if (cart.Count == 0) return new(false, "Your cart is empty.");

        var requireAppCse = await RequireAppCseAsync(db, ct);
        var problems = new List<string>();
        foreach (var line in cart)   // check again, things can change after adding to cart
        {
            if (line.Item.IsActive == false) { problems.Add($"{line.Item.ItemName} was removed from the catalog."); continue; }
            if (!requireAppCse) continue;   // APP-CSE validation is turned off in Settings
            var bal = await BalanceAsync(db, officeId, line.ItemId, year, null, ct);
            if (bal is null) problems.Add($"{line.Item.ItemName} is no longer in your APP-CSE.");
            else if ((int)line.Quantity > bal.Remaining)
                problems.Add($"{line.Item.ItemName}: {line.Quantity} requested, only {bal.Remaining} remaining.");
        }
        if (problems.Count > 0)
            return new(false, "Some items need your attention before submitting.", problems);   // rolls back on dispose

        // The signatories the office picks have to be its own personnel.
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
            DivisionName = (office.ParentOffice ?? office).OfficeName,   // a section shows its division
            OfficeName = office.OfficeName,
            ResponsibilityCenterCode = office.ResponsibilityCenterCode,
            Purpose = purpose.Trim(),
            Status = Pending,
            RequestedByUserId = u.UserId,
            RequestedByPersonnelId = requestedByPersonnelId,
            RequestedAt = now,
            // Default approving official printed on the slip. AMS can change it when approving.
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
                ItemDescription = Describe(line.Item, line.Colors, (int)line.Quantity),
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

    // 3. APPROVE (deducts warehouse stock and APP-CSE, all or nothing)
    // <param name="issueQty">Optional quantity per line (ris_item_id to qty) for a partial issue.</param>
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
        var budgetOfficeId = (await AppCseScope.ForAsync(db, ris.OfficeId, ct)).BudgetOfficeId;
        var now = NowPh;
        var shortfalls = new List<string>();

        foreach (var line in ris.RisItems.OrderBy(i => i.ItemId))   // always lock in the same order so there are no deadlocks
        {
            var requested = (int)line.RequestedQty;
            var qty = issueQty is not null && issueQty.TryGetValue(line.RisItemId, out var o) ? o : requested;
            if (qty < 0 || qty > requested)
                return new(false, $"Line {line.LineNo}: issue quantity must be between 0 and {requested}.");

            line.IssuedQty = (uint)qty;
            line.StockAvailable = qty > 0;
            if (qty == 0) { line.Remarks ??= "Not available"; continue; }

            // The deduction and the check happen in one step, the WHERE clause is the check.
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
                 WHERE office_id = {budgetOfficeId} AND item_id = {line.ItemId}
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
            await tx.RollbackAsync(ct);   // nothing is deducted unless everything works
            return new(false, "Approval was not saved. Adjust the issue quantities and try again.", shortfalls);
        }
        if (ris.RisItems.All(i => i.IssuedQty == 0))
            return new(false, "Every line is set to 0. Reject the request instead.");

        ris.Status = Approved;
        ris.ApprovedByUserId = admin.UserId;
        ris.ApprovedByPersonnelId = approvedByPersonnelId ?? ris.ApprovedByPersonnelId;   // keep the default if nobody was picked
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

        // I make the PDF AFTER the commit so the slow file work doesn't hold the row locks.
        log.LogInformation("RIS {RisNo} approved by user {UserId}", ris.RisNo, admin.UserId);
        return new(true, $"{ris.RisNo} is approved for issuance.", RisId: ris.RisId);
    }

    // 4. REJECT (a reason is required, the reserved quantity is released automatically)
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

    // 7. REQUEST AGAIN (copy an old RIS into the cart)
    public sealed record RequestAgainResult(int Added, IReadOnlyList<string> Reduced, IReadOnlyList<string> Skipped);

    /// Adds every line of an old requisition of the office to the cart. Each line goes through
    /// the normal cart checks. A line over the APP-CSE balance is added with what's still allowed,
    /// and lines that can't be added at all are listed with the reason.
    public async Task<RequestAgainResult> RequestAgainAsync(CurrentUser u, uint risId, CancellationToken ct = default)
    {
        List<(uint ItemId, int Qty, string Name)> lines;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            lines = (await db.RisTransactions.AsNoTracking()
                    .Where(r => r.RisId == risId && r.OfficeId == u.OfficeId)
                    .SelectMany(r => r.RisItems)
                    .OrderBy(i => i.LineNo)
                    .Select(i => new { i.ItemId, i.RequestedQty, i.ItemDescription })
                    .ToListAsync(ct))
                .Select(i => (i.ItemId, (int)i.RequestedQty, i.ItemDescription)).ToList();
        }

        var added = 0;
        var reduced = new List<string>();
        var skipped = new List<string>();
        foreach (var (itemId, qty, name) in lines)
        {
            // A line with colors ("... (Colors: Black 5, Blue 3)") is added one color at a time
            if (ColorsOf(name) is { Count: > 0 } colors)
            {
                var ok = 0;
                foreach (var (color, n) in colors)
                    if ((await AddToCartAsync(u, itemId, n, color, ct)).Allowed) ok++;
                if (ok == colors.Count) added++;
                else if (ok > 0) { added++; reduced.Add($"{name}: some colors couldn't be added"); }
                else skipped.Add($"{name}: can't be added");
                continue;
            }

            var check = await AddToCartAsync(u, itemId, qty, ct: ct);
            if (check.Allowed) { added++; continue; }

            var canAdd = check.Balance?.CanStillAdd ?? 0;
            if (check.Issues.Any(i => i.Code == "EXCEEDS_APP_CSE") && canAdd > 0 &&
                (await AddToCartAsync(u, itemId, canAdd, ct: ct)).Allowed)
            {
                added++;
                reduced.Add($"{name}: {canAdd} instead of {qty} (APP-CSE balance)");
                continue;
            }
            skipped.Add($"{name}: {check.Issues.FirstOrDefault()?.Title ?? "can't be added"}");
        }
        return new RequestAgainResult(added, reduced, skipped);
    }

    // 6. RECEIVED BY (who from the office will pick up the items)
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

    // 5. CANCEL (the office takes back its own pending request)
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

    // Helpers

    // Locks the office whose APP-CSE is used (shared by a division and its units), so two
    // requests can't both pass the balance check.
    private static async Task LockOfficeAsync(AmsDbContext db, uint officeId, CancellationToken ct)
    {
        var budget = (await AppCseScope.ForAsync(db, officeId, ct)).BudgetOfficeId;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT office_id FROM offices WHERE office_id = {budget} FOR UPDATE", ct);
    }

    /// Monthly number with no gaps, safe even with many users at once (LAST_INSERT_ID is per connection).
    private static async Task<string> NextRisNumberAsync(AmsDbContext db, DateTime now, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ris_number_sequences (fiscal_year, seq_month, last_seq)
            VALUES ({now.Year}, {now.Month}, LAST_INSERT_ID(1))
            ON DUPLICATE KEY UPDATE last_seq = LAST_INSERT_ID(last_seq + 1)", ct);
        var seq = await db.Database.SqlQuery<ulong>($"SELECT LAST_INSERT_ID() AS Value").SingleAsync(ct);
        return $"{now:yyyy-MM}-{seq:0000}";
    }

    private static string Describe(SupplyItem i, string? colors, int quantity)
    {
        var text = DescribeLine(i.ItemName, i.Specifications, colors, quantity);
        return text.Length <= 700 ? text : text[..697] + "...";   // ris_items.item_description is VARCHAR(700)
    }

    /// The colors that DescribeLine wrote on an RIS line ("any color" is skipped).
    private static List<(string Name, int Qty)> ColorsOf(string description)
    {
        var m = System.Text.RegularExpressions.Regex.Match(description, @"\(Colors: (.+)\)$");
        if (!m.Success) return [];
        return m.Groups[1].Value.Split(", ")
            .Select(part => (Part: part, Space: part.LastIndexOf(' ')))
            .Where(x => x.Space > 0 && int.TryParse(x.Part[(x.Space + 1)..], out _))
            .Select(x => (Name: x.Part[..x.Space], Qty: int.Parse(x.Part[(x.Space + 1)..])))
            .Where(x => x.Name != "any color" && x.Qty > 0)
            .ToList();
    }

    /// The RIS line, like "Ballpen, 0.5 mm (Colors: Black 5, Blue 3)". The slip preview in the cart uses it too.
    public static string DescribeLine(string name, string? specifications, string? colors, int quantity)
    {
        var text = string.IsNullOrWhiteSpace(specifications) ? name : $"{name}, {specifications}";
        var list = ItemColors.ParseQty(colors);
        if (list.Count == 0) return text;
        var any = quantity - list.Sum(c => c.Qty);
        if (any > 0) list.Add(("any color", any));
        return $"{text} (Colors: {ItemColors.Text(list)})";
    }

    private static string Friendly(string status) => status switch
    {
        Approved => "approved for issuance",
        Pending  => "pending approval",
        _        => status.ToLowerInvariant(),
    };
}
