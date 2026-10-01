using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class UserActivity
{
    public int ActivityId { get; set; }

    public uint UserId { get; set; }

    public string ActivityType { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
