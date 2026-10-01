using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class RisStatusHistory
{
    public ulong HistoryId { get; set; }

    public uint RisId { get; set; }

    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = null!;

    public uint ChangedBy { get; set; }

    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }

    public virtual User ChangedByNavigation { get; set; } = null!;

    public virtual RisTransaction Ris { get; set; } = null!;
}
