using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class DataProtectionKey
{
    public uint Id { get; set; }

    public string? FriendlyName { get; set; }

    public string XmlData { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
