using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class Supplier
{
    public uint SupplierId { get; set; }

    public string SupplierName { get; set; } = null!;

    public string? ContactPerson { get; set; }

    public string? ContactNo { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<SupplyItem> SupplyItems { get; set; } = new List<SupplyItem>();
}
