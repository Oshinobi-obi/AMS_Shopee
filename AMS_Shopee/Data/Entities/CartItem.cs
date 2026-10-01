using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class CartItem
{
    public uint CartItemId { get; set; }

    public uint UserId { get; set; }

    public uint ItemId { get; set; }

    public uint Quantity { get; set; }

    public string? Remarks { get; set; }

    public DateTime AddedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual SupplyItem Item { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
