using System;
using System.Collections.Generic;
using AMS_Shopee.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace AMS_Shopee.Data;

public partial class AmsDbContext : DbContext
{
    public AmsDbContext()
    {
    }

    public AmsDbContext(DbContextOptions<AmsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AppCseAllocation> AppCseAllocations { get; set; }

    public virtual DbSet<AppCseUpload> AppCseUploads { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    public virtual DbSet<ItemCategory> ItemCategories { get; set; }

    public virtual DbSet<LoginAttempt> LoginAttempts { get; set; }

    public virtual DbSet<Office> Offices { get; set; }

    public virtual DbSet<PropertyDocument> PropertyDocuments { get; set; }

    public virtual DbSet<PropertyType> PropertyTypes { get; set; }

    public virtual DbSet<RisItem> RisItems { get; set; }

    public virtual DbSet<RisNumberSequence> RisNumberSequences { get; set; }

    public virtual DbSet<RisStatusHistory> RisStatusHistories { get; set; }

    public virtual DbSet<RisTransaction> RisTransactions { get; set; }

    public virtual DbSet<RoPersonnel> RoPersonnel { get; set; }

    public virtual DbSet<SchemaMigration> SchemaMigrations { get; set; }

    public virtual DbSet<StockMovement> StockMovements { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SupplyItem> SupplyItems { get; set; }

    public virtual DbSet<SupplySuggestion> SupplySuggestions { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserActivity> UserActivities { get; set; }

    public virtual DbSet<VOfficeItemBalance> VOfficeItemBalances { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseMySql("name=ConnectionStrings:Default", Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.46-mysql"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<AppCseAllocation>(entity =>
        {
            entity.HasKey(e => e.AllocationId).HasName("PRIMARY");

            entity
                .ToTable("app_cse_allocations")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.UploadId, "fk_alloc_upload");

            entity.HasIndex(e => new { e.ItemId, e.FiscalYear }, "idx_alloc_item_year");

            entity.HasIndex(e => new { e.OfficeId, e.ItemId, e.FiscalYear }, "uq_alloc").IsUnique();

            entity.Property(e => e.AllocationId).HasColumnName("allocation_id");
            entity.Property(e => e.AllocatedQty).HasColumnName("allocated_qty");
            entity.Property(e => e.FiscalYear).HasColumnName("fiscal_year");
            entity.Property(e => e.IssuedQty).HasColumnName("issued_qty");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.OfficeId).HasColumnName("office_id");
            entity.Property(e => e.Q1Qty).HasColumnName("q1_qty");
            entity.Property(e => e.Q2Qty).HasColumnName("q2_qty");
            entity.Property(e => e.Q3Qty).HasColumnName("q3_qty");
            entity.Property(e => e.Q4Qty).HasColumnName("q4_qty");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UploadId).HasColumnName("upload_id");

            entity.HasOne(d => d.Item).WithMany(p => p.AppCseAllocations)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_alloc_item");

            entity.HasOne(d => d.Office).WithMany(p => p.AppCseAllocations)
                .HasForeignKey(d => d.OfficeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_alloc_office");

            entity.HasOne(d => d.Upload).WithMany(p => p.AppCseAllocations)
                .HasForeignKey(d => d.UploadId)
                .HasConstraintName("fk_alloc_upload");
        });

        modelBuilder.Entity<AppCseUpload>(entity =>
        {
            entity.HasKey(e => e.UploadId).HasName("PRIMARY");

            entity
                .ToTable("app_cse_uploads")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.UploadedBy, "fk_upload_user");

            entity.HasIndex(e => e.FiscalYear, "idx_upload_year");

            entity.Property(e => e.UploadId).HasColumnName("upload_id");
            entity.Property(e => e.FilePath)
                .HasMaxLength(500)
                .HasColumnName("file_path");
            entity.Property(e => e.FiscalYear).HasColumnName("fiscal_year");
            entity.Property(e => e.Notes)
                .HasMaxLength(500)
                .HasColumnName("notes");
            entity.Property(e => e.OriginalFileName)
                .HasMaxLength(255)
                .HasColumnName("original_file_name");
            entity.Property(e => e.RowCount).HasColumnName("row_count");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("uploaded_at");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.AppCseUploads)
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_upload_user");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.CartItemId).HasName("PRIMARY");

            entity
                .ToTable("cart_items")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.ItemId, "fk_cart_item");

            entity.HasIndex(e => new { e.UserId, e.ItemId }, "uq_cart_user_item").IsUnique();

            entity.Property(e => e.CartItemId).HasColumnName("cart_item_id");
            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("added_at");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Remarks)
                .HasMaxLength(255)
                .HasColumnName("remarks");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Item).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cart_item");

            entity.HasOne(d => d.User).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_cart_user");
        });

        modelBuilder.Entity<DataProtectionKey>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("data_protection_keys")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.FriendlyName)
                .HasMaxLength(255)
                .HasColumnName("friendly_name");
            entity.Property(e => e.XmlData)
                .HasColumnType("text")
                .HasColumnName("xml_data");
        });

        modelBuilder.Entity<ItemCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PRIMARY");

            entity
                .ToTable("item_categories")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.CategoryName, "uq_category_name").IsUnique();

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(100)
                .HasColumnName("category_name");
            entity.Property(e => e.Icon)
                .HasMaxLength(50)
                .HasColumnName("icon");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.HasKey(e => e.AttemptId).HasName("PRIMARY");

            entity
                .ToTable("login_attempts")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => new { e.Username, e.AttemptedAt }, "idx_username_time");

            entity.Property(e => e.AttemptId).HasColumnName("attempt_id");
            entity.Property(e => e.AttemptedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("attempted_at");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");
            entity.Property(e => e.WasSuccessful).HasColumnName("was_successful");
        });

        modelBuilder.Entity<Office>(entity =>
        {
            entity.HasKey(e => e.OfficeId).HasName("PRIMARY");

            entity
                .ToTable("offices")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.ParentOfficeId, "idx_offices_parent");

            entity.HasIndex(e => e.OfficeAcronym, "office_acronym").IsUnique();

            entity.Property(e => e.OfficeId).HasColumnName("office_id");
            entity.Property(e => e.CanRequisition)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("can_requisition");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.OfficeAcronym)
                .HasMaxLength(20)
                .HasColumnName("office_acronym");
            entity.Property(e => e.OfficeName)
                .HasMaxLength(150)
                .HasColumnName("office_name");
            entity.Property(e => e.OfficeType)
                .HasDefaultValueSql("'Unit'")
                .HasColumnType("enum('Office','Division','Section','Unit','External')")
                .HasColumnName("office_type");
            entity.Property(e => e.ParentOfficeId).HasColumnName("parent_office_id");
            entity.Property(e => e.ResponsibilityCenterCode)
                .HasMaxLength(30)
                .HasColumnName("responsibility_center_code");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");

            entity.HasOne(d => d.ParentOffice).WithMany(p => p.InverseParentOffice)
                .HasForeignKey(d => d.ParentOfficeId)
                .HasConstraintName("fk_offices_parent");
        });

        modelBuilder.Entity<PropertyDocument>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("PRIMARY");

            entity
                .ToTable("property_documents")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.OfficeId, "idx_office");

            entity.HasIndex(e => e.PersonnelId, "idx_personnel");

            entity.HasIndex(e => e.PropertyTypeId, "property_type_id");

            entity.HasIndex(e => e.UploadedBy, "uploaded_by");

            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.DocumentType)
                .HasMaxLength(20)
                .HasColumnName("document_type");
            entity.Property(e => e.FilePath)
                .HasMaxLength(255)
                .HasColumnName("file_path");
            entity.Property(e => e.OfficeId).HasColumnName("office_id");
            entity.Property(e => e.OriginalFileName)
                .HasMaxLength(255)
                .HasColumnName("original_file_name");
            entity.Property(e => e.PersonnelId).HasColumnName("personnel_id");
            entity.Property(e => e.PropertyTypeId).HasColumnName("property_type_id");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("uploaded_at");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");

            entity.HasOne(d => d.Office).WithMany(p => p.PropertyDocuments)
                .HasForeignKey(d => d.OfficeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("property_documents_ibfk_1");

            entity.HasOne(d => d.Personnel).WithMany(p => p.PropertyDocuments)
                .HasForeignKey(d => d.PersonnelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("property_documents_ibfk_3");

            entity.HasOne(d => d.PropertyType).WithMany(p => p.PropertyDocuments)
                .HasForeignKey(d => d.PropertyTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("property_documents_ibfk_2");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.PropertyDocuments)
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("property_documents_ibfk_4");
        });

        modelBuilder.Entity<PropertyType>(entity =>
        {
            entity.HasKey(e => e.PropertyTypeId).HasName("PRIMARY");

            entity
                .ToTable("property_types")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.TypeName, "type_name").IsUnique();

            entity.Property(e => e.PropertyTypeId).HasColumnName("property_type_id");
            entity.Property(e => e.TypeName)
                .HasMaxLength(100)
                .HasColumnName("type_name");
        });

        modelBuilder.Entity<RisItem>(entity =>
        {
            entity.HasKey(e => e.RisItemId).HasName("PRIMARY");

            entity
                .ToTable("ris_items")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.ItemId, "idx_ris_items_item");

            entity.HasIndex(e => new { e.RisId, e.ItemId }, "uq_ris_item").IsUnique();

            entity.Property(e => e.RisItemId).HasColumnName("ris_item_id");
            entity.Property(e => e.IssuedQty).HasColumnName("issued_qty");
            entity.Property(e => e.ItemDescription)
                .HasMaxLength(700)
                .HasColumnName("item_description");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.LineNo).HasColumnName("line_no");
            entity.Property(e => e.Remarks)
                .HasMaxLength(255)
                .HasColumnName("remarks");
            entity.Property(e => e.RequestedQty).HasColumnName("requested_qty");
            entity.Property(e => e.RisId).HasColumnName("ris_id");
            entity.Property(e => e.StockAvailable).HasColumnName("stock_available");
            entity.Property(e => e.StockNo)
                .HasMaxLength(30)
                .HasColumnName("stock_no");
            entity.Property(e => e.UnitCost)
                .HasPrecision(12, 2)
                .HasColumnName("unit_cost");
            entity.Property(e => e.UnitOfMeasure)
                .HasMaxLength(30)
                .HasColumnName("unit_of_measure");

            entity.HasOne(d => d.Item).WithMany(p => p.RisItems)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_risitem_item");

            entity.HasOne(d => d.Ris).WithMany(p => p.RisItems)
                .HasForeignKey(d => d.RisId)
                .HasConstraintName("fk_risitem_ris");
        });

        modelBuilder.Entity<RisNumberSequence>(entity =>
        {
            entity.HasKey(e => new { e.FiscalYear, e.SeqMonth })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("ris_number_sequences")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.FiscalYear).HasColumnName("fiscal_year");
            entity.Property(e => e.SeqMonth).HasColumnName("seq_month");
            entity.Property(e => e.LastSeq).HasColumnName("last_seq");
        });

        modelBuilder.Entity<RisStatusHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PRIMARY");

            entity
                .ToTable("ris_status_history")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.ChangedBy, "fk_hist_user");

            entity.HasIndex(e => new { e.RisId, e.ChangedAt }, "idx_hist_ris");

            entity.Property(e => e.HistoryId).HasColumnName("history_id");
            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("changed_at");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.FromStatus)
                .HasMaxLength(30)
                .HasColumnName("from_status");
            entity.Property(e => e.Note)
                .HasMaxLength(1000)
                .HasColumnName("note");
            entity.Property(e => e.RisId).HasColumnName("ris_id");
            entity.Property(e => e.ToStatus)
                .HasMaxLength(30)
                .HasColumnName("to_status");

            entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.RisStatusHistories)
                .HasForeignKey(d => d.ChangedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_hist_user");

            entity.HasOne(d => d.Ris).WithMany(p => p.RisStatusHistories)
                .HasForeignKey(d => d.RisId)
                .HasConstraintName("fk_hist_ris");
        });

        modelBuilder.Entity<RisTransaction>(entity =>
        {
            entity.HasKey(e => e.RisId).HasName("PRIMARY");

            entity
                .ToTable("ris_transactions")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.ApprovedByPersonnelId, "fk_ris_appr_pers");

            entity.HasIndex(e => e.ApprovedByUserId, "fk_ris_appr_user");

            entity.HasIndex(e => e.IssuedByPersonnelId, "fk_ris_iss_pers");

            entity.HasIndex(e => e.ReceivedByPersonnelId, "fk_ris_rcv_pers");

            entity.HasIndex(e => e.RejectedByUserId, "fk_ris_rej_user");

            entity.HasIndex(e => e.RequestedByPersonnelId, "fk_ris_req_pers");

            entity.HasIndex(e => e.RequestedByUserId, "fk_ris_req_user");

            entity.HasIndex(e => new { e.OfficeId, e.FiscalYear, e.Status }, "idx_ris_office_year");

            entity.HasIndex(e => new { e.Status, e.RequestedAt }, "idx_ris_status");

            entity.HasIndex(e => e.RisNo, "uq_ris_no").IsUnique();

            entity.Property(e => e.RisId).HasColumnName("ris_id");
            entity.Property(e => e.ApprovedAt)
                .HasColumnType("datetime")
                .HasColumnName("approved_at");
            entity.Property(e => e.ApprovedByPersonnelId).HasColumnName("approved_by_personnel_id");
            entity.Property(e => e.ApprovedByUserId).HasColumnName("approved_by_user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DivisionName)
                .HasMaxLength(150)
                .HasColumnName("division_name");
            entity.Property(e => e.EntityName)
                .HasMaxLength(200)
                .HasColumnName("entity_name");
            entity.Property(e => e.FiscalYear).HasColumnName("fiscal_year");
            entity.Property(e => e.FundCluster)
                .HasMaxLength(10)
                .HasColumnName("fund_cluster");
            entity.Property(e => e.IssuedAt)
                .HasColumnType("datetime")
                .HasColumnName("issued_at");
            entity.Property(e => e.IssuedByPersonnelId).HasColumnName("issued_by_personnel_id");
            entity.Property(e => e.OfficeId).HasColumnName("office_id");
            entity.Property(e => e.OfficeName)
                .HasMaxLength(150)
                .HasColumnName("office_name");
            entity.Property(e => e.PdfPath)
                .HasMaxLength(500)
                .HasColumnName("pdf_path");
            entity.Property(e => e.Purpose)
                .HasMaxLength(500)
                .HasColumnName("purpose");
            entity.Property(e => e.ReceivedByPersonnelId).HasColumnName("received_by_personnel_id");
            entity.Property(e => e.RejectedAt)
                .HasColumnType("datetime")
                .HasColumnName("rejected_at");
            entity.Property(e => e.RejectedByUserId).HasColumnName("rejected_by_user_id");
            entity.Property(e => e.RejectionReason)
                .HasMaxLength(1000)
                .HasColumnName("rejection_reason");
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("requested_at");
            entity.Property(e => e.RequestedByPersonnelId).HasColumnName("requested_by_personnel_id");
            entity.Property(e => e.RequestedByUserId).HasColumnName("requested_by_user_id");
            entity.Property(e => e.ResponsibilityCenterCode)
                .HasMaxLength(30)
                .HasColumnName("responsibility_center_code");
            entity.Property(e => e.RisNo)
                .HasMaxLength(30)
                .HasColumnName("ris_no");
            entity.Property(e => e.RowVersion).HasColumnName("row_version");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'PendingApproval'")
                .HasColumnType("enum('PendingApproval','ApprovedForIssuance','Issued','Rejected','Cancelled')")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.ApprovedByPersonnel).WithMany(p => p.RisTransactionApprovedByPersonnel)
                .HasForeignKey(d => d.ApprovedByPersonnelId)
                .HasConstraintName("fk_ris_appr_pers");

            entity.HasOne(d => d.ApprovedByUser).WithMany(p => p.RisTransactionApprovedByUsers)
                .HasForeignKey(d => d.ApprovedByUserId)
                .HasConstraintName("fk_ris_appr_user");

            entity.HasOne(d => d.IssuedByPersonnel).WithMany(p => p.RisTransactionIssuedByPersonnel)
                .HasForeignKey(d => d.IssuedByPersonnelId)
                .HasConstraintName("fk_ris_iss_pers");

            entity.HasOne(d => d.Office).WithMany(p => p.RisTransactions)
                .HasForeignKey(d => d.OfficeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ris_office");

            entity.HasOne(d => d.ReceivedByPersonnel).WithMany(p => p.RisTransactionReceivedByPersonnel)
                .HasForeignKey(d => d.ReceivedByPersonnelId)
                .HasConstraintName("fk_ris_rcv_pers");

            entity.HasOne(d => d.RejectedByUser).WithMany(p => p.RisTransactionRejectedByUsers)
                .HasForeignKey(d => d.RejectedByUserId)
                .HasConstraintName("fk_ris_rej_user");

            entity.HasOne(d => d.RequestedByPersonnel).WithMany(p => p.RisTransactionRequestedByPersonnel)
                .HasForeignKey(d => d.RequestedByPersonnelId)
                .HasConstraintName("fk_ris_req_pers");

            entity.HasOne(d => d.RequestedByUser).WithMany(p => p.RisTransactionRequestedByUsers)
                .HasForeignKey(d => d.RequestedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ris_req_user");
        });

        modelBuilder.Entity<RoPersonnel>(entity =>
        {
            entity.HasKey(e => e.PersonnelId).HasName("PRIMARY");

            entity
                .ToTable("ro_personnel")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.OfficeId, "idx_office");

            entity.Property(e => e.PersonnelId).HasColumnName("personnel_id");
            entity.Property(e => e.FullName)
                .HasMaxLength(150)
                .HasColumnName("full_name");
            entity.Property(e => e.OfficeId).HasColumnName("office_id");
            entity.Property(e => e.Position)
                .HasMaxLength(100)
                .HasColumnName("position");

            entity.HasOne(d => d.Office).WithMany(p => p.RoPersonnel)
                .HasForeignKey(d => d.OfficeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ro_personnel_ibfk_1");
        });

        modelBuilder.Entity<SchemaMigration>(entity =>
        {
            entity.HasKey(e => e.Version).HasName("PRIMARY");

            entity
                .ToTable("schema_migrations")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .HasColumnName("version");
            entity.Property(e => e.AppliedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("applied_at");
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.MovementId).HasName("PRIMARY");

            entity
                .ToTable("stock_movements")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.OfficeId, "fk_mov_office");

            entity.HasIndex(e => e.RisId, "fk_mov_ris");

            entity.HasIndex(e => e.PerformedBy, "fk_mov_user");

            entity.HasIndex(e => new { e.ItemId, e.CreatedAt }, "idx_mov_item_date");

            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceNo }, "idx_mov_ref");

            entity.Property(e => e.MovementId).HasColumnName("movement_id");
            entity.Property(e => e.BalanceAfter).HasColumnName("balance_after");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.MovementType)
                .HasColumnType("enum('OPENING','RECEIPT','ISSUE','RETURN','ADJUSTMENT_IN','ADJUSTMENT_OUT')")
                .HasColumnName("movement_type");
            entity.Property(e => e.OfficeId).HasColumnName("office_id");
            entity.Property(e => e.PerformedBy).HasColumnName("performed_by");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.ReferenceNo)
                .HasMaxLength(50)
                .HasColumnName("reference_no");
            entity.Property(e => e.ReferenceType)
                .HasMaxLength(20)
                .HasColumnName("reference_type");
            entity.Property(e => e.Remarks)
                .HasMaxLength(500)
                .HasColumnName("remarks");
            entity.Property(e => e.RisId).HasColumnName("ris_id");
            entity.Property(e => e.UnitCost)
                .HasPrecision(12, 2)
                .HasColumnName("unit_cost");

            entity.HasOne(d => d.Item).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_mov_item");

            entity.HasOne(d => d.Office).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.OfficeId)
                .HasConstraintName("fk_mov_office");

            entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.PerformedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_mov_user");

            entity.HasOne(d => d.Ris).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.RisId)
                .HasConstraintName("fk_mov_ris");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PRIMARY");

            entity
                .ToTable("suppliers")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.SupplierName, "uq_supplier_name").IsUnique();

            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.ContactNo)
                .HasMaxLength(50)
                .HasColumnName("contact_no");
            entity.Property(e => e.ContactPerson)
                .HasMaxLength(150)
                .HasColumnName("contact_person");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.SupplierName)
                .HasMaxLength(200)
                .HasColumnName("supplier_name");
        });

        modelBuilder.Entity<SupplyItem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PRIMARY");

            entity
                .ToTable("supply_items")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.PropertyTypeId, "fk_items_ptype");

            entity.HasIndex(e => e.SupplierId, "fk_items_supplier");

            entity.HasIndex(e => new { e.ItemName, e.Specifications }, "ft_items").HasAnnotation("MySql:FullTextIndex", true);

            entity.HasIndex(e => new { e.IsActive, e.ItemName }, "idx_items_active");

            entity.HasIndex(e => e.CategoryId, "idx_items_category");

            entity.HasIndex(e => e.StockNo, "uq_stock_no").IsUnique();

            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.ImagePath)
                .HasMaxLength(500)
                .HasColumnName("image_path");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.ItemName)
                .HasMaxLength(200)
                .HasColumnName("item_name");
            entity.Property(e => e.PropertyTypeId).HasColumnName("property_type_id");
            entity.Property(e => e.ReorderLevel)
                .HasDefaultValueSql("'10'")
                .HasColumnName("reorder_level");
            entity.Property(e => e.RowVersion).HasColumnName("row_version");
            entity.Property(e => e.Specifications)
                .HasColumnType("text")
                .HasColumnName("specifications");
            entity.Property(e => e.StockNo)
                .HasMaxLength(30)
                .HasColumnName("stock_no");
            entity.Property(e => e.StockOnHand).HasColumnName("stock_on_hand");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.UnitOfMeasure)
                .HasMaxLength(30)
                .HasColumnName("unit_of_measure");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(12, 2)
                .HasColumnName("unit_price");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Category).WithMany(p => p.SupplyItems)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("fk_items_category");

            entity.HasOne(d => d.PropertyType).WithMany(p => p.SupplyItems)
                .HasForeignKey(d => d.PropertyTypeId)
                .HasConstraintName("fk_items_ptype");

            entity.HasOne(d => d.Supplier).WithMany(p => p.SupplyItems)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("fk_items_supplier");
        });

        modelBuilder.Entity<SupplySuggestion>(entity =>
        {
            entity.HasKey(e => e.SuggestionId).HasName("PRIMARY");

            entity
                .ToTable("supply_suggestions")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.OfficeId, "fk_sugg_office");

            entity.HasIndex(e => e.ReviewedBy, "fk_sugg_reviewer");

            entity.HasIndex(e => e.UserId, "fk_sugg_user");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "idx_sugg_status");

            entity.Property(e => e.SuggestionId).HasColumnName("suggestion_id");
            entity.Property(e => e.AdminResponse)
                .HasMaxLength(1000)
                .HasColumnName("admin_response");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.EstimatedAnnualQty).HasColumnName("estimated_annual_qty");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.ItemName)
                .HasMaxLength(200)
                .HasColumnName("item_name");
            entity.Property(e => e.Justification)
                .HasColumnType("text")
                .HasColumnName("justification");
            entity.Property(e => e.OfficeId).HasColumnName("office_id");
            entity.Property(e => e.ReviewedAt)
                .HasColumnType("datetime")
                .HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'New'")
                .HasColumnType("enum('New','UnderReview','AddedToCatalog','ForNextAPP','Declined')")
                .HasColumnName("status");
            entity.Property(e => e.SubmitterEmail)
                .HasMaxLength(100)
                .HasColumnName("submitter_email");
            entity.Property(e => e.SubmitterName)
                .HasMaxLength(150)
                .HasColumnName("submitter_name");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Office).WithMany(p => p.SupplySuggestions)
                .HasForeignKey(d => d.OfficeId)
                .HasConstraintName("fk_sugg_office");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.SupplySuggestionReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("fk_sugg_reviewer");

            entity.HasOne(d => d.User).WithMany(p => p.SupplySuggestionUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_sugg_user");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingKey).HasName("PRIMARY");

            entity
                .ToTable("system_settings")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.SettingKey)
                .HasMaxLength(100)
                .HasColumnName("setting_key");
            entity.Property(e => e.SettingValue)
                .HasMaxLength(500)
                .HasColumnName("setting_value");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity
                .ToTable("users")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Email, "email").IsUnique();

            entity.HasIndex(e => e.Role, "idx_role");

            entity.HasIndex(e => e.Username, "idx_username").IsUnique();

            entity.HasIndex(e => e.OfficeId, "idx_users_office");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("display_name");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.FailedLoginAttempts).HasColumnName("failed_login_attempts");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.LastLoginAt)
                .HasColumnType("datetime")
                .HasColumnName("last_login_at");
            entity.Property(e => e.LockedUntil)
                .HasColumnType("datetime")
                .HasColumnName("locked_until");
            entity.Property(e => e.OfficeId).HasColumnName("office_id");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.ProfilePicturePath)
                .HasMaxLength(500)
                .HasColumnName("profile_picture_path");
            entity.Property(e => e.RequirePasswordChange).HasColumnName("require_password_change");
            entity.Property(e => e.Role)
                .HasDefaultValueSql("'Office'")
                .HasColumnType("enum('SuperAdmin','Admin','Office')")
                .HasColumnName("role");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");

            entity.HasOne(d => d.Office).WithMany(p => p.Users)
                .HasForeignKey(d => d.OfficeId)
                .HasConstraintName("fk_users_office");
        });

        modelBuilder.Entity<UserActivity>(entity =>
        {
            entity.HasKey(e => e.ActivityId).HasName("PRIMARY");

            entity
                .ToTable("user_activities")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.CreatedAt, "idx_user_activities_created_at");

            entity.HasIndex(e => e.UserId, "idx_user_activities_user_id");

            entity.Property(e => e.ActivityId).HasColumnName("activity_id");
            entity.Property(e => e.ActivityType)
                .HasMaxLength(50)
                .HasColumnName("activity_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserActivities)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_activities_ibfk_1");
        });

        modelBuilder.Entity<VOfficeItemBalance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_office_item_balance");

            entity.Property(e => e.AllocatedQty).HasColumnName("allocated_qty");
            entity.Property(e => e.FiscalYear).HasColumnName("fiscal_year");
            entity.Property(e => e.IssuedQty).HasColumnName("issued_qty");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.OfficeId).HasColumnName("office_id");
            entity.Property(e => e.PendingQty)
                .HasPrecision(32)
                .HasColumnName("pending_qty");
            entity.Property(e => e.RemainingQty)
                .HasPrecision(33)
                .HasColumnName("remaining_qty");
            entity.Property(e => e.RequestedToDate)
                .HasPrecision(33)
                .HasColumnName("requested_to_date");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
