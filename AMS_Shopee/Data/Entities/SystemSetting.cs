using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class SystemSetting
{
    public string SettingKey { get; set; } = null!;

    public string SettingValue { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }
}
