using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class PropertyDocument
{
    public uint DocumentId { get; set; }

    public uint OfficeId { get; set; }

    public string DocumentType { get; set; } = null!;

    public uint PropertyTypeId { get; set; }

    public uint PersonnelId { get; set; }

    public string FilePath { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public uint UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual Office Office { get; set; } = null!;

    public virtual RoPersonnel Personnel { get; set; } = null!;

    public virtual PropertyType PropertyType { get; set; } = null!;

    public virtual User UploadedByNavigation { get; set; } = null!;
}
