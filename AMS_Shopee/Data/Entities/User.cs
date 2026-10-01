using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class User
{
    public uint UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? ProfilePicturePath { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public uint? OfficeId { get; set; }

    public bool? IsActive { get; set; }

    public bool RequirePasswordChange { get; set; }

    public uint FailedLoginAttempts { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AppCseUpload> AppCseUploads { get; set; } = new List<AppCseUpload>();

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Office? Office { get; set; }

    public virtual ICollection<PropertyDocument> PropertyDocuments { get; set; } = new List<PropertyDocument>();

    public virtual ICollection<RisStatusHistory> RisStatusHistories { get; set; } = new List<RisStatusHistory>();

    public virtual ICollection<RisTransaction> RisTransactionApprovedByUsers { get; set; } = new List<RisTransaction>();

    public virtual ICollection<RisTransaction> RisTransactionRejectedByUsers { get; set; } = new List<RisTransaction>();

    public virtual ICollection<RisTransaction> RisTransactionRequestedByUsers { get; set; } = new List<RisTransaction>();

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public virtual ICollection<SupplySuggestion> SupplySuggestionReviewedByNavigations { get; set; } = new List<SupplySuggestion>();

    public virtual ICollection<SupplySuggestion> SupplySuggestionUsers { get; set; } = new List<SupplySuggestion>();

    public virtual ICollection<UserActivity> UserActivities { get; set; } = new List<UserActivity>();
}
