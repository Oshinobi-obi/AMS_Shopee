// This decides whose APP-CSE an office uses.
// Most offices use their own. A unit set to share its division's (offices.appcse_office_id,
// like LRPO under ESSD) requests on the division's APP-CSE, so its requisitions use the
// division's balance together with the division's own requisitions.

using AMS_Shopee.Data;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Requisition;

public readonly record struct AppCseScope(uint BudgetOfficeId, IReadOnlyList<uint> SharingOfficeIds)
{
    public static async Task<AppCseScope> ForAsync(AmsDbContext db, uint officeId, CancellationToken ct = default)
    {
        try
        {
            var budget = await db.Database
                .SqlQuery<uint>($"SELECT COALESCE(appcse_office_id, office_id) AS Value FROM offices WHERE office_id = {officeId}")
                .SingleAsync(ct);
            var sharing = await db.Database
                .SqlQuery<uint>($"SELECT office_id AS Value FROM offices WHERE COALESCE(appcse_office_id, office_id) = {budget}")
                .ToListAsync(ct);
            return new AppCseScope(budget, sharing.Count > 0 ? sharing : [officeId]);
        }
        catch (Exception ex) when (ex.GetBaseException().Message.Contains("Unknown column"))
        {
            // Database update 016 isn't run yet, so every office uses its own APP-CSE.
            return new AppCseScope(officeId, [officeId]);
        }
    }
}
