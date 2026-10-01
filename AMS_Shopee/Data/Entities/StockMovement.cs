using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class StockMovement
{
    public ulong MovementId { get; set; }

    public uint ItemId { get; set; }

    public string MovementType { get; set; } = null!;

    public uint Quantity { get; set; }

    public int BalanceAfter { get; set; }

    public decimal? UnitCost { get; set; }

    public string? ReferenceType { get; set; }

    public string? ReferenceNo { get; set; }

    public uint? RisId { get; set; }

    public uint? OfficeId { get; set; }

    public string? Remarks { get; set; }

    public uint PerformedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual SupplyItem Item { get; set; } = null!;

    public virtual Office? Office { get; set; }

    public virtual User PerformedByNavigation { get; set; } = null!;

    public virtual RisTransaction? Ris { get; set; }
}
