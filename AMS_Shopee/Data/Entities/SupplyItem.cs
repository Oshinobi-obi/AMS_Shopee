using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class SupplyItem
{
    public uint ItemId { get; set; }

    public string StockNo { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public string? Specifications { get; set; }

    public string UnitOfMeasure { get; set; } = null!;

    public uint? CategoryId { get; set; }

    public uint? SupplierId { get; set; }

    public uint? PropertyTypeId { get; set; }

    public decimal UnitPrice { get; set; }

    public string? ImagePath { get; set; }

    public int StockOnHand { get; set; }

    public uint ReorderLevel { get; set; }

    public bool? IsActive { get; set; }

    /// The item expires. AMS sets the EXP, the MFD is optional.
    public bool IsPerishable { get; set; }

    public DateTime? ManufacturedOn { get; set; }

    public DateTime? ExpiresOn { get; set; }

    /// MFD / EXP shown as month and year only (database update 020).
    public bool MfdMonthOnly { get; set; }

    public bool ExpMonthOnly { get; set; }

    /// Colors the offices can choose, like "Black|#1f2937;Blue|#1d4ed8" (see ItemColors).
    public string? ColorOptions { get; set; }

    public uint RowVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AppCseAllocation> AppCseAllocations { get; set; } = new List<AppCseAllocation>();

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual ItemCategory? Category { get; set; }

    public virtual PropertyType? PropertyType { get; set; }

    public virtual ICollection<RisItem> RisItems { get; set; } = new List<RisItem>();

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public virtual Supplier? Supplier { get; set; }
}
