using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class SchemaMigration
{
    public string Version { get; set; } = null!;

    public DateTime AppliedAt { get; set; }
}
