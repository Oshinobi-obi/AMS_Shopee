using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class AppCseUpload
{
    public uint UploadId { get; set; }

    public ushort FiscalYear { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public uint RowCount { get; set; }

    public string? Notes { get; set; }

    public uint UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual ICollection<AppCseAllocation> AppCseAllocations { get; set; } = new List<AppCseAllocation>();

    public virtual User UploadedByNavigation { get; set; } = null!;
}
