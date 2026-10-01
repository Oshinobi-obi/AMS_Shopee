using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class PropertyType
{
    public uint PropertyTypeId { get; set; }

    public string TypeName { get; set; } = null!;

    public virtual ICollection<PropertyDocument> PropertyDocuments { get; set; } = new List<PropertyDocument>();

    public virtual ICollection<SupplyItem> SupplyItems { get; set; } = new List<SupplyItem>();
}
