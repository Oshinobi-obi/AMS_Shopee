// OfficeQueries.cs — read models for the signed-in office: the cart (with the
// data the RIS preview needs) and the office's full APP-CSE for verification.

using AMS_Shopee.Data;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Requisition;

public sealed record PersonnelOption(uint Id, string FullName, string Position);

public sealed record CartLineView(
    uint ItemId, string StockNo, string Name, string? Specifications, string Unit,
    string? CategoryIcon, string? ImagePath, decimal UnitPrice, int Quantity,
    int StockOnHand, uint ReorderLevel, QuotaBalance? Balance)
{
    public StockLevel Level => RequisitionService.LevelOf(StockOnHand, ReorderLevel);
    public decimal LineTotal => UnitPrice * Quantity;
    /// Same wording SubmitAsync stores on the RIS line.
    public string Description => string.IsNullOrWhiteSpace(Specifications) ? Name : $"{Name}, {Specifications}";
}

public sealed record CartView(
    string EntityName, string FundCluster, string Division, string Office, string OfficeAcronym,
    string? ResponsibilityCenterCode, IReadOnlyList<CartLineView> Lines, IReadOnlyList<PersonnelOption> Personnel,
    PersonnelOption? ApprovedBy)
{
    public decimal EstimatedTotal => Lines.Sum(l => l.LineTotal);
    public int TotalUnits => Lines.Sum(l => l.Quantity);
}

public sealed record AppCseRow(
    uint ItemId, string StockNo, string Name, string? Specifications, string Unit, decimal UnitPrice,
    uint? Q1, uint? Q2, uint? Q3, uint? Q4, int Allocated, int Issued, int Pending)
{
    public int Remaining => Math.Max(0, Allocated - Issued - Pending);
    public decimal EstimatedCost => UnitPrice * Allocated;
}

public sealed record AppCseReport(
    ushort FiscalYear, IReadOnlyList<ushort> AvailableYears,
    string OfficeName, string OfficeAcronym, IReadOnlyList<AppCseRow> Rows)
{
    public decimal EstimatedTotal => Rows.Sum(r => r.EstimatedCost);
}

public sealed class OfficeQueries(IDbContextFactory<AmsDbContext> dbFactory, RequisitionService requisitions)
{
    public async Task<CartView?> GetCartAsync(CurrentUser? u, CancellationToken ct = default)
    {
        if (u is null || !u.IsOffice) return null;
        var officeId = u.OfficeId!.Value;
        var balances = await requisitions.GetBalancesAsync(u, ct);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var office = await db.Offices.AsNoTracking().Include(o => o.ParentOffice)
            .FirstAsync(o => o.OfficeId == officeId, ct);
        var settings = await db.SystemSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);

        var lines = await db.CartItems.AsNoTracking()
            .Where(c => c.UserId == u.UserId)
            .OrderBy(c => c.AddedAt).ThenBy(c => c.CartItemId)
            .Select(c => new
            {
                c.ItemId, c.Item.StockNo, c.Item.ItemName, c.Item.Specifications, c.Item.UnitOfMeasure,
                Icon = c.Item.Category != null ? c.Item.Category.Icon : null,
                c.Item.ImagePath, c.Item.UnitPrice, c.Quantity, c.Item.StockOnHand, c.Item.ReorderLevel,
            })
            .ToListAsync(ct);

        var personnel = await db.RoPersonnel.AsNoTracking()
            .Where(p => p.OfficeId == officeId)
            .OrderBy(p => p.FullName)
            .Select(p => new PersonnelOption(p.PersonnelId, p.FullName, p.Position))
            .ToListAsync(ct);

        // Default "Approved by" signatory (system_settings 'ris.approved_by_personnel_id')
        PersonnelOption? approvedBy = null;
        if (uint.TryParse(settings.GetValueOrDefault("ris.approved_by_personnel_id"), out var approverId))
            approvedBy = await db.RoPersonnel.AsNoTracking()
                .Where(p => p.PersonnelId == approverId)
                .Select(p => new PersonnelOption(p.PersonnelId, p.FullName, p.Position))
                .FirstOrDefaultAsync(ct);

        return new CartView(
            settings.GetValueOrDefault("ris.entity_name", "Entity name not set"),
            settings.GetValueOrDefault("ris.default_fund_cluster", ""),
            (office.ParentOffice ?? office).OfficeName,
            office.OfficeName,
            office.OfficeAcronym,
            office.ResponsibilityCenterCode,
            lines.Select(l => new CartLineView(
                l.ItemId, l.StockNo, l.ItemName, l.Specifications, l.UnitOfMeasure, l.Icon, l.ImagePath,
                l.UnitPrice, (int)l.Quantity, l.StockOnHand, l.ReorderLevel,
                balances.GetValueOrDefault(l.ItemId))).ToList(),
            personnel,
            approvedBy);
    }

    public async Task<AppCseReport?> GetAppCseAsync(CurrentUser? u, ushort? year, CancellationToken ct = default)
    {
        if (u is null || !u.IsOffice) return null;
        var officeId = u.OfficeId!.Value;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var office = await db.Offices.AsNoTracking().FirstAsync(o => o.OfficeId == officeId, ct);

        var years = await db.AppCseAllocations.AsNoTracking()
            .Where(a => a.OfficeId == officeId)
            .Select(a => a.FiscalYear).Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(ct);

        var current = requisitions.FiscalYear;
        var fy = year is ushort y && years.Contains(y) ? y
               : years.Contains(current) ? current
               : years.Count > 0 ? years[0] : current;

        var pending = await db.RisItems
            .Where(i => i.Ris.OfficeId == officeId && i.Ris.FiscalYear == fy && i.Ris.Status == "PendingApproval")
            .GroupBy(i => i.ItemId)
            .Select(g => new { ItemId = g.Key, Qty = g.Sum(i => (long)i.RequestedQty) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Qty, ct);

        var rows = await db.AppCseAllocations.AsNoTracking()
            .Where(a => a.OfficeId == officeId && a.FiscalYear == fy)
            .OrderBy(a => a.Item.Category == null ? 999 : a.Item.Category.SortOrder)
            .ThenBy(a => a.Item.ItemName)
            .Select(a => new
            {
                a.ItemId, a.Item.StockNo, a.Item.ItemName, a.Item.Specifications, a.Item.UnitOfMeasure,
                a.Item.UnitPrice, a.Q1Qty, a.Q2Qty, a.Q3Qty, a.Q4Qty, a.AllocatedQty, a.IssuedQty,
            })
            .ToListAsync(ct);

        return new AppCseReport(fy, years, office.OfficeName, office.OfficeAcronym,
            rows.Select(r => new AppCseRow(
                r.ItemId, r.StockNo, r.ItemName, r.Specifications, r.UnitOfMeasure, r.UnitPrice,
                r.Q1Qty, r.Q2Qty, r.Q3Qty, r.Q4Qty,
                (int)r.AllocatedQty, (int)r.IssuedQty, (int)pending.GetValueOrDefault(r.ItemId))).ToList());
    }
}
