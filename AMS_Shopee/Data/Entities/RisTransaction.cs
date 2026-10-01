using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class RisTransaction
{
    public uint RisId { get; set; }

    public string RisNo { get; set; } = null!;

    public uint OfficeId { get; set; }

    public ushort FiscalYear { get; set; }

    public string EntityName { get; set; } = null!;

    public string FundCluster { get; set; } = null!;

    public string DivisionName { get; set; } = null!;

    public string OfficeName { get; set; } = null!;

    public string? ResponsibilityCenterCode { get; set; }

    public string Purpose { get; set; } = null!;

    public string Status { get; set; } = null!;

    public uint RequestedByUserId { get; set; }

    public uint? RequestedByPersonnelId { get; set; }

    public DateTime RequestedAt { get; set; }

    public uint? ApprovedByUserId { get; set; }

    public uint? ApprovedByPersonnelId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public uint? IssuedByPersonnelId { get; set; }

    public uint? ReceivedByPersonnelId { get; set; }

    public DateTime? IssuedAt { get; set; }

    public uint? RejectedByUserId { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string? RejectionReason { get; set; }

    public string? PdfPath { get; set; }

    public uint RowVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual RoPersonnel? ApprovedByPersonnel { get; set; }

    public virtual User? ApprovedByUser { get; set; }

    public virtual RoPersonnel? IssuedByPersonnel { get; set; }

    public virtual Office Office { get; set; } = null!;

    public virtual RoPersonnel? ReceivedByPersonnel { get; set; }

    public virtual User? RejectedByUser { get; set; }

    public virtual RoPersonnel? RequestedByPersonnel { get; set; }

    public virtual User RequestedByUser { get; set; } = null!;

    public virtual ICollection<RisItem> RisItems { get; set; } = new List<RisItem>();

    public virtual ICollection<RisStatusHistory> RisStatusHistories { get; set; } = new List<RisStatusHistory>();

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
