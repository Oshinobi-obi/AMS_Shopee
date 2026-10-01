using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class ItemCategory
{
    public uint CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Icon { get; set; }

    public ushort SortOrder { get; set; }

    public virtual ICollection<SupplyItem> SupplyItems { get; set; } = new List<SupplyItem>();
}
