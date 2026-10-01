using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class Office
{
    public uint OfficeId { get; set; }

    public string OfficeName { get; set; } = null!;

    public string OfficeAcronym { get; set; } = null!;

    public uint? ParentOfficeId { get; set; }

    public string OfficeType { get; set; } = null!;

    public string? ResponsibilityCenterCode { get; set; }

    public bool? CanRequisition { get; set; }

    public bool? IsActive { get; set; }

    public ushort SortOrder { get; set; }

    public virtual ICollection<AppCseAllocation> AppCseAllocations { get; set; } = new List<AppCseAllocation>();

    public virtual ICollection<Office> InverseParentOffice { get; set; } = new List<Office>();

    public virtual Office? ParentOffice { get; set; }

    public virtual ICollection<PropertyDocument> PropertyDocuments { get; set; } = new List<PropertyDocument>();

    public virtual ICollection<RisTransaction> RisTransactions { get; set; } = new List<RisTransaction>();

    public virtual ICollection<RoPersonnel> RoPersonnel { get; set; } = new List<RoPersonnel>();

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public virtual ICollection<SupplySuggestion> SupplySuggestions { get; set; } = new List<SupplySuggestion>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
