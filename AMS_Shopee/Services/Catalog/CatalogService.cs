// This is where I get the catalog for the public home page,
// and where "Suggest a supply" is saved. It's registered as Scoped in Program.cs.

using AMS_Shopee.Data;
using AMS_Shopee.Data.Entities;
using AMS_Shopee.Services.Requisition;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Catalog;

public enum StockFilter { All, InStock, LowStock, OutOfStock }

public sealed record CatalogCategory(uint Id, string Name, string? Icon);

public sealed record CatalogItem(
    uint ItemId,
    string StockNo,
    string Name,
    string? Specifications,
    string Unit,
    string? Supplier,
    string? Category,
    string? CategoryIcon,
    decimal UnitPrice,
    string? ImagePath,
    int StockOnHand,
    uint ReorderLevel,
    bool IsPerishable = false,
    DateTime? ManufacturedOn = null,
    DateTime? ExpiresOn = null,
    bool MfdMonthOnly = false,
    bool ExpMonthOnly = false,
    string? ColorOptions = null)
{
    /// Colors the offices can choose from (most items have none).
    public IReadOnlyList<ItemColor> Colors => _colors ??= ItemColors.Parse(ColorOptions);
    private IReadOnlyList<ItemColor>? _colors;

    /// "Mar 15, 2027", or "Mar 2027" when the pack only shows the month and year.
    public static string FormatDate(DateTime d, bool monthOnly, bool longMonth = false) =>
        d.ToString(monthOnly ? (longMonth ? "MMMM yyyy" : "MMM yyyy") : (longMonth ? "MMMM d, yyyy" : "MMM d, yyyy"));

    public StockLevel Level => RequisitionService.LevelOf(StockOnHand, ReorderLevel);

    /// Days until the EXP (negative means expired), null if the item doesn't expire.
    public int? DaysToExpiry => IsPerishable && ExpiresOn is { } e
        ? (e.Date - TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AMS_Shopee.Services.PhZone.Manila).Date).Days
        : null;

    /// Items expiring within this many days are shown in amber.
    public const int ExpirySoonDays = 90;
    public string ExpiryCss => DaysToExpiry switch { < 0 => "is-expired", <= ExpirySoonDays => "is-soon", _ => "" };
}

public sealed class SuggestionInput
{
    public string ItemName { get; set; } = "";
    public string? Description { get; set; }
    public string? Justification { get; set; }
    public int? EstimatedAnnualQty { get; set; }
    public string? SubmitterName { get; set; }    // guests only
    public string? SubmitterEmail { get; set; }   // guests only
}

public sealed class CatalogService(IDbContextFactory<AmsDbContext> dbFactory)
{
    public async Task<List<CatalogCategory>> GetCategoriesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ItemCategories.AsNoTracking()
            .Where(c => c.SupplyItems.Any(i => i.IsActive != false))     // hide categories with no items
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CategoryName)
            .Select(c => new CatalogCategory(c.CategoryId, c.CategoryName, c.Icon))
            .ToListAsync(ct);
    }

    public async Task<List<CatalogItem>> GetItemsAsync(
        string? search, uint? categoryId, StockFilter filter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.SupplyItems.AsNoTracking().Where(i => i.IsActive != false);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(i => i.ItemName.Contains(s)
                          || (i.Specifications != null && i.Specifications.Contains(s))
                          || i.StockNo.Contains(s));
        }
        if (categoryId is uint cid)
            q = q.Where(i => i.CategoryId == cid);

        q = filter switch
        {
            StockFilter.InStock    => q.Where(i => i.StockOnHand > i.ReorderLevel),
            StockFilter.LowStock   => q.Where(i => i.StockOnHand > 0 && i.StockOnHand <= i.ReorderLevel),
            StockFilter.OutOfStock => q.Where(i => i.StockOnHand <= 0),
            _                      => q,
        };

        return await q
            .OrderBy(i => i.Category == null ? 999 : i.Category.SortOrder)
            .ThenBy(i => i.ItemName)
            .Select(Project)
            .ToListAsync(ct);
    }

    public async Task<CatalogItem?> GetItemAsync(uint itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.SupplyItems.AsNoTracking()
            .Where(i => i.ItemId == itemId && i.IsActive != false)
            .Select(Project)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<OpResult> SubmitSuggestionAsync(SuggestionInput input, CurrentUser? user, CancellationToken ct = default)
    {
        var name = input.ItemName?.Trim() ?? "";
        if (name.Length < 3)
            return new(false, "Enter the item name (at least 3 characters).");
        if (input.EstimatedAnnualQty is < 1 or > 100_000)
            return new(false, "Estimated quantity must be between 1 and 100,000.");
        var email = input.SubmitterEmail?.Trim();
        if (!string.IsNullOrEmpty(email) && (email.Length > 100 || !email.Contains('@')))
            return new(false, "Check the email address, or leave it blank.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.SupplySuggestions.Add(new SupplySuggestion
        {
            ItemName = Clip(name, 200)!,
            Description = Clip(input.Description, 2000),
            Justification = Clip(input.Justification, 2000),
            EstimatedAnnualQty = input.EstimatedAnnualQty is int q ? (uint)q : null,
            OfficeId = user?.OfficeId,
            UserId = user?.UserId,
            SubmitterName = user is null ? Clip(input.SubmitterName, 150) : null,
            SubmitterEmail = user is null ? (string.IsNullOrEmpty(email) ? null : email) : null,
            Status = "New",
        });
        await db.SaveChangesAsync(ct);
        return new(true, "Thanks! AMS will review your suggestion.");
    }

    // Shared projection (I kept it as an expression so EF can turn it into SQL)
    private static readonly System.Linq.Expressions.Expression<Func<SupplyItem, CatalogItem>> Project =
        i => new CatalogItem(
            i.ItemId, i.StockNo, i.ItemName, i.Specifications, i.UnitOfMeasure,
            i.Supplier != null ? i.Supplier.SupplierName : null,
            i.Category != null ? i.Category.CategoryName : null,
            i.Category != null ? i.Category.Icon : null,
            i.UnitPrice, i.ImagePath, i.StockOnHand, i.ReorderLevel,
            i.IsPerishable, i.ManufacturedOn, i.ExpiresOn, i.MfdMonthOnly, i.ExpMonthOnly, i.ColorOptions);

    private static string? Clip(string? s, int max)
    {
        s = s?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        return s.Length <= max ? s : s[..max];
    }
}
