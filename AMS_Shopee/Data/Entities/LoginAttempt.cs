using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class LoginAttempt
{
    public uint AttemptId { get; set; }

    public string Username { get; set; } = null!;

    public string? IpAddress { get; set; }

    public bool WasSuccessful { get; set; }

    public DateTime AttemptedAt { get; set; }
}
