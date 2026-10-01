using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class SupplySuggestion
{
    public uint SuggestionId { get; set; }

    public uint? OfficeId { get; set; }

    public uint? UserId { get; set; }

    public string? SubmitterName { get; set; }

    public string? SubmitterEmail { get; set; }

    public string ItemName { get; set; } = null!;

    public string? Description { get; set; }

    public string? Justification { get; set; }

    public uint? EstimatedAnnualQty { get; set; }

    public string Status { get; set; } = null!;

    public string? AdminResponse { get; set; }

    public uint? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Office? Office { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual User? User { get; set; }
}
