using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class RisItem
{
    public uint RisItemId { get; set; }

    public uint RisId { get; set; }

    public uint ItemId { get; set; }

    public ushort LineNo { get; set; }

    public string StockNo { get; set; } = null!;

    public string UnitOfMeasure { get; set; } = null!;

    public string ItemDescription { get; set; } = null!;

    public decimal UnitCost { get; set; }

    public uint RequestedQty { get; set; }

    public bool? StockAvailable { get; set; }

    public uint? IssuedQty { get; set; }

    public string? Remarks { get; set; }

    public virtual SupplyItem Item { get; set; } = null!;

    public virtual RisTransaction Ris { get; set; } = null!;
}
