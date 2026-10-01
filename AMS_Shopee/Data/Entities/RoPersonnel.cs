using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class RoPersonnel
{
    public uint PersonnelId { get; set; }

    public uint OfficeId { get; set; }

    public string FullName { get; set; } = null!;

    public string Position { get; set; } = null!;

    public virtual Office Office { get; set; } = null!;

    public virtual ICollection<PropertyDocument> PropertyDocuments { get; set; } = new List<PropertyDocument>();

    public virtual ICollection<RisTransaction> RisTransactionApprovedByPersonnel { get; set; } = new List<RisTransaction>();

    public virtual ICollection<RisTransaction> RisTransactionIssuedByPersonnel { get; set; } = new List<RisTransaction>();

    public virtual ICollection<RisTransaction> RisTransactionReceivedByPersonnel { get; set; } = new List<RisTransaction>();

    public virtual ICollection<RisTransaction> RisTransactionRequestedByPersonnel { get; set; } = new List<RisTransaction>();
}
