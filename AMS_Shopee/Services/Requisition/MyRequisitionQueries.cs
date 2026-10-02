// MyRequisitionQueries.cs — read models for an office following up on its own RIS.

using AMS_Shopee.Components.Requisition;
using AMS_Shopee.Data;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Requisition;

public static class RisStatusText
{
    public static string Label(string status) => status switch
    {
        "PendingApproval" => "Pending approval",
        "ApprovedForIssuance" => "Approved, ready for release",
        "Issued" => "Issued",
        "Rejected" => "Rejected",
        "Cancelled" => "Cancelled",
        _ => status,
    };

    public static string Css(string status) => status switch
    {
        "PendingApproval" => "is-pending",
        "ApprovedForIssuance" => "is-approved",
        "Issued" => "is-issued",
        "Rejected" => "is-rejected",
        _ => "is-cancelled",
    };
}

public sealed record MyRisRow(uint RisId, string RisNo, string Purpose, string Status, DateTime RequestedAt,
                              int ItemCount, int Units, decimal Amount);

public sealed record MyRisEvent(string ToStatus, DateTime At, string? Note);

public sealed record MyRisDetail(uint RisId, string RisNo, string Status, DateTime RequestedAt,
                                 DateTime? ApprovedAt, DateTime? IssuedAt, string? RejectionReason,
                                 RisDocumentModel Document, IReadOnlyList<MyRisEvent> Events, decimal Amount,
                                 uint? ReceivedById, IReadOnlyList<PersonnelOption> OfficePersonnel, bool HasSignedCopy);

public sealed class MyRequisitionQueries(IDbContextFactory<AmsDbContext> dbFactory)
{
    public async Task<List<MyRisRow>> ListAsync(CurrentUser? u, CancellationToken ct = default)
    {
        if (u is null || !u.IsOffice) return [];
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.RisTransactions.AsNoTracking()
            .Where(r => r.OfficeId == u.OfficeId)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new MyRisRow(
                r.RisId, r.RisNo, r.Purpose, r.Status, r.RequestedAt,
                r.RisItems.Count(),
                r.RisItems.Sum(i => (int)(i.IssuedQty ?? i.RequestedQty)),
                r.RisItems.Sum(i => i.UnitCost * (i.IssuedQty ?? i.RequestedQty))))
            .ToListAsync(ct);
    }

    /// Only returns a RIS that belongs to the signed-in office.
    public async Task<MyRisDetail?> GetAsync(CurrentUser? u, uint risId, CancellationToken ct = default)
    {
        if (u is null || !u.IsOffice) return null;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var r = await db.RisTransactions.AsNoTracking()
            .Include(x => x.RisItems)
            .Include(x => x.RisStatusHistories)
            .Include(x => x.RequestedByPersonnel)
            .Include(x => x.ApprovedByPersonnel)
            .Include(x => x.IssuedByPersonnel)
            .Include(x => x.ReceivedByPersonnel)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.RisId == risId && x.OfficeId == u.OfficeId, ct);
        if (r is null) return null;

        static RisSignatory? Sign(Data.Entities.RoPersonnel? p, DateTime? at) =>
            p is null && at is null ? null : new RisSignatory(p?.FullName, p?.Position, at);

        var doc = new RisDocumentModel
        {
            EntityName = r.EntityName,
            FundCluster = r.FundCluster,
            Division = r.DivisionName,
            Office = r.OfficeName,
            ResponsibilityCenterCode = r.ResponsibilityCenterCode,
            RisNo = r.RisNo,
            Purpose = r.Purpose,
            Status = r.Status,
            Lines = r.RisItems.OrderBy(i => i.LineNo)
                .Select(i => new RisLine((int)i.ItemId, i.StockNo, i.UnitOfMeasure, i.ItemDescription, (int)i.RequestedQty,
                                         i.StockAvailable, (int?)i.IssuedQty, i.Remarks)).ToList(),
            RequestedBy = Sign(r.RequestedByPersonnel, r.RequestedAt),
            ApprovedBy = Sign(r.ApprovedByPersonnel, r.ApprovedAt),
            IssuedBy = Sign(r.IssuedByPersonnel, r.IssuedAt),
            ReceivedBy = Sign(r.ReceivedByPersonnel, r.IssuedAt),
        };

        var people = await db.RoPersonnel.AsNoTracking()
            .Where(p => p.OfficeId == r.OfficeId).OrderBy(p => p.FullName)
            .Select(p => new PersonnelOption(p.PersonnelId, p.FullName, p.Position)).ToListAsync(ct);
        var hasSigned = r.Status == "Issued" && await db.Database
            .SqlQuery<long>($"SELECT COUNT(*) AS Value FROM ris_signed_copies WHERE ris_id = {r.RisId}")
            .SingleAsync(ct) > 0;

        return new MyRisDetail(
            r.RisId, r.RisNo, r.Status, r.RequestedAt, r.ApprovedAt, r.IssuedAt, r.RejectionReason, doc,
            r.RisStatusHistories.OrderBy(h => h.ChangedAt).Select(h => new MyRisEvent(h.ToStatus, h.ChangedAt, h.Note)).ToList(),
            r.RisItems.Sum(i => i.UnitCost * (i.IssuedQty ?? i.RequestedQty)),
            r.ReceivedByPersonnelId, people, hasSigned);
    }
}
