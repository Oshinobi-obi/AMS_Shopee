using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class AppCseAllocation
{
    public uint AllocationId { get; set; }

    public uint OfficeId { get; set; }

    public uint ItemId { get; set; }

    public ushort FiscalYear { get; set; }

    public uint AllocatedQty { get; set; }

    public uint? Q1Qty { get; set; }

    public uint? Q2Qty { get; set; }

    public uint? Q3Qty { get; set; }

    public uint? Q4Qty { get; set; }

    public uint IssuedQty { get; set; }

    public uint? UploadId { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual SupplyItem Item { get; set; } = null!;

    public virtual Office Office { get; set; } = null!;

    public virtual AppCseUpload? Upload { get; set; }
}
