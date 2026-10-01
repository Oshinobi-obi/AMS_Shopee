using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class VOfficeItemBalance
{
    public uint OfficeId { get; set; }

    public uint ItemId { get; set; }

    public ushort FiscalYear { get; set; }

    public uint AllocatedQty { get; set; }

    public uint IssuedQty { get; set; }

    public decimal PendingQty { get; set; }

    public decimal RequestedToDate { get; set; }

    public decimal RemainingQty { get; set; }
}
