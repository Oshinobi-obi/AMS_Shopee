// This is where I read the data for the public My accountability page: offices with personnel,
// the personnel of an office, and the documents (PAR, ICS, Return Slips) and register items
// under one person's name. It only reads, it never changes anything.

using AMS_Shopee.Data;
using AMS_Shopee.Services.Requisition;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Accountability;

public sealed record AccountabilityOffice(uint OfficeId, string Acronym, string Name, int People);

public sealed class AccountabilityDocument
{
    public uint DocumentId { get; set; }
    public string DocumentType { get; set; } = "";       // PAR, ICS or Return Slip
    public string PropertyType { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime? DateSigned { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ReturnedOn { get; set; }
    public uint? ReturnSlipId { get; set; }
    public List<AccountabilityItem> Items { get; set; } = [];
    /// Laptop, Printer and so on, as set in e-PINS when the document was uploaded.
    public List<string> Equipment { get; set; } = [];

    public bool IsReturned => ReturnedOn is not null;
    public bool IsReturnSlip => DocumentType == "Return Slip";
    /// The value of the property on it that the person still has (not "Returned").
    public decimal Total => Items.Where(i => !i.IsReturned).Sum(i => i.Total);
    public int Units => Items.Where(i => !i.IsReturned).Sum(i => i.Quantity);
}

public sealed class AccountabilityItem
{
    public string PropertyNo { get; set; } = "";
    public string Description { get; set; } = "";
    public string Classification { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime? AcquiredOn { get; set; }
    public string Status { get; set; } = "";
    public uint? DocumentId { get; set; }
    public decimal Total => Quantity * UnitCost;
    public bool IsReturned => Status == "Returned";
}

public sealed record AccountabilityFile(uint DocumentId, uint PersonnelId, string StoredName, string OriginalName);

public sealed class AccountabilityQueries(IDbContextFactory<AmsDbContext> dbFactory, ILogger<AccountabilityQueries> log)
{
    /// Offices that have at least one RO personnel.
    public async Task<List<AccountabilityOffice>> OfficesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.Database.SqlQuery<OfficeRow>($@"
            SELECT o.office_id AS OfficeId, o.office_acronym AS Acronym, o.office_name AS Name, COUNT(p.personnel_id) AS People
              FROM offices o JOIN ro_personnel p ON p.office_id = o.office_id
             WHERE o.is_active = 1
             GROUP BY o.office_id, o.office_acronym, o.office_name
             ORDER BY o.office_acronym").ToListAsync();
        return rows.Select(r => new AccountabilityOffice(r.OfficeId, r.Acronym, r.Name, (int)r.People)).ToList();
    }

    public async Task<List<PersonnelOption>> PeopleAsync(uint officeId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.RoPersonnel.AsNoTracking()
            .Where(p => p.OfficeId == officeId)
            .OrderBy(p => p.FullName)
            .Select(p => new PersonnelOption(p.PersonnelId, p.FullName, p.Position))
            .ToListAsync();
    }

    /// The office of a person (so the page can load again from a link with ?person=).
    public async Task<uint?> OfficeOfAsync(uint personnelId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.RoPersonnel.AsNoTracking().Where(p => p.PersonnelId == personnelId)
            .Select(p => (uint?)p.OfficeId).FirstOrDefaultAsync();
    }

    /// Every PAR, ICS and Return Slip of one person, newest first, each with the register items on it,
    /// plus register items under their name that aren't on any document.
    /// Items marked "Returned" (their PAR / ICS was returned) don't count in the totals.
    public async Task<(List<AccountabilityDocument> Documents, List<AccountabilityItem> Unfiled)> ForPersonAsync(uint personnelId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        List<DocRow> rows;
        try
        {
            rows = await db.Database.SqlQuery<DocRow>($@"
                SELECT d.document_id AS DocumentId, d.document_type AS DocumentType, t.type_name AS PropertyType,
                       d.original_file_name AS FileName, d.date_signed AS DateSigned, d.uploaded_at AS UploadedAt,
                       d.returned_on AS ReturnedOn, d.return_slip_id AS ReturnSlipId, d.equipment_type AS EquipmentType
                  FROM property_documents d JOIN property_types t ON t.property_type_id = d.property_type_id
                 WHERE d.personnel_id = {personnelId}
                 ORDER BY COALESCE(d.date_signed, d.uploaded_at) DESC, d.document_id DESC").ToListAsync();
        }
        catch (Exception ex) when (ex.GetBaseException().Message.Contains("Unknown column") && ex.GetBaseException().Message.Contains("equipment_type"))
        {
            // Database update 018 (equipment type) isn't run yet
            rows = await db.Database.SqlQuery<DocRow>($@"
                SELECT d.document_id AS DocumentId, d.document_type AS DocumentType, t.type_name AS PropertyType,
                       d.original_file_name AS FileName, d.date_signed AS DateSigned, d.uploaded_at AS UploadedAt,
                       d.returned_on AS ReturnedOn, d.return_slip_id AS ReturnSlipId, NULL AS EquipmentType
                  FROM property_documents d JOIN property_types t ON t.property_type_id = d.property_type_id
                 WHERE d.personnel_id = {personnelId}
                 ORDER BY COALESCE(d.date_signed, d.uploaded_at) DESC, d.document_id DESC").ToListAsync();
        }
        catch (Exception ex) when (ex.GetBaseException().Message.Contains("Unknown column"))
        {
            // Database updates 013/014 (date signed, returns) aren't run yet
            rows = await db.Database.SqlQuery<DocRow>($@"
                SELECT d.document_id AS DocumentId, d.document_type AS DocumentType, t.type_name AS PropertyType,
                       d.original_file_name AS FileName, NULL AS DateSigned, d.uploaded_at AS UploadedAt,
                       NULL AS ReturnedOn, NULL AS ReturnSlipId, NULL AS EquipmentType
                  FROM property_documents d JOIN property_types t ON t.property_type_id = d.property_type_id
                 WHERE d.personnel_id = {personnelId}
                 ORDER BY d.uploaded_at DESC, d.document_id DESC").ToListAsync();
        }

        var docs = rows.Select(r => new AccountabilityDocument
        {
            DocumentId = r.DocumentId, DocumentType = r.DocumentType, PropertyType = r.PropertyType, FileName = r.FileName,
            DateSigned = r.DateSigned, UploadedAt = r.UploadedAt, ReturnedOn = r.ReturnedOn, ReturnSlipId = r.ReturnSlipId,
            Equipment = (r.EquipmentType ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        }).ToList();

        List<AccountabilityItem> items = [];
        try
        {
            items = await db.Database.SqlQuery<AccountabilityItem>($@"
                SELECT property_no AS PropertyNo, description AS Description, classification AS Classification,
                       CAST(quantity AS SIGNED) AS Quantity, unit_cost AS UnitCost, acquired_on AS AcquiredOn,
                       status AS Status, document_id AS DocumentId
                  FROM property_items
                 WHERE personnel_id = {personnelId}
                    OR document_id IN (SELECT document_id FROM property_documents WHERE personnel_id = {personnelId})
                 ORDER BY property_no").ToListAsync();
        }
        catch (Exception ex) when (ex.GetBaseException().Message.Contains("doesn't exist"))
        {
            // The property register (update 007) isn't in this database yet
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Property items for personnel {Id} could not be read", personnelId);
        }

        var byDoc = docs.ToDictionary(d => d.DocumentId);
        var unfiled = new List<AccountabilityItem>();
        foreach (var i in items)
        {
            if (i.DocumentId is { } id && byDoc.TryGetValue(id, out var d)) d.Items.Add(i);
            else if (!i.IsReturned) unfiled.Add(i);   // returned property isn't theirs anymore
        }
        return (docs, unfiled);
    }

    public async Task<AccountabilityFile?> FileAsync(uint documentId) =>
        (await FilesAsync([documentId])).FirstOrDefault();

    public async Task<List<AccountabilityFile>> FilesAsync(IReadOnlyCollection<uint> documentIds)
    {
        if (documentIds.Count == 0) return [];
        var ids = string.Join(",", documentIds.Distinct());   // numbers only, so it's safe to put in the query
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.Database.SqlQueryRaw<FileRow>($@"
            SELECT document_id AS DocumentId, personnel_id AS PersonnelId, file_path AS StoredName, original_file_name AS OriginalName
              FROM property_documents WHERE document_id IN ({ids})").ToListAsync();
        return rows.Select(r => new AccountabilityFile(r.DocumentId, r.PersonnelId, r.StoredName, r.OriginalName)).ToList();
    }

    private sealed class OfficeRow
    {
        public uint OfficeId { get; set; }
        public string Acronym { get; set; } = "";
        public string Name { get; set; } = "";
        public long People { get; set; }
    }

    private sealed class DocRow
    {
        public uint DocumentId { get; set; }
        public string DocumentType { get; set; } = "";
        public string PropertyType { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime? DateSigned { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? ReturnedOn { get; set; }
        public uint? ReturnSlipId { get; set; }
        public string? EquipmentType { get; set; }
    }

    private sealed class FileRow
    {
        public uint DocumentId { get; set; }
        public uint PersonnelId { get; set; }
        public string StoredName { get; set; } = "";
        public string OriginalName { get; set; } = "";
    }
}
