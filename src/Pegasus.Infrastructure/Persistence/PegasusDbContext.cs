using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

public sealed class PegasusDbContext(DbContextOptions<PegasusDbContext> options)
    : IdentityDbContext<PegasusIdentityUser, IdentityRole<Guid>, Guid>(options)
{
    internal DbSet<ApplicationInitializationEntity> ApplicationInitializations =>
        Set<ApplicationInitializationEntity>();

    internal DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    internal DbSet<OrganizationRoleEntity> OrganizationRoles => Set<OrganizationRoleEntity>();
    internal DbSet<PrincipalSequenceLineageEntity> PrincipalSequenceLineages =>
        Set<PrincipalSequenceLineageEntity>();
    internal DbSet<PrincipalEntity> Principals => Set<PrincipalEntity>();
    internal DbSet<CaseSequenceEntity> CaseSequences => Set<CaseSequenceEntity>();
    internal DbSet<CaseEntity> Cases => Set<CaseEntity>();
    internal DbSet<CaseIntakeLinkEntity> CaseIntakeLinks => Set<CaseIntakeLinkEntity>();
    internal DbSet<CaseHistoryEntity> CaseHistory => Set<CaseHistoryEntity>();
    internal DbSet<ExternalWorkItemEntity> ExternalWorkItems => Set<ExternalWorkItemEntity>();

    internal DbSet<IntakeReceiptEntity> IntakeReceipts => Set<IntakeReceiptEntity>();

    internal DbSet<IntakeAssetEntity> IntakeAssets => Set<IntakeAssetEntity>();

    internal DbSet<InstructionDraftEntity> InstructionDrafts => Set<InstructionDraftEntity>();

    internal DbSet<IntakeReceiptEventEntity> IntakeReceiptEvents => Set<IntakeReceiptEventEntity>();
    internal DbSet<IntakeStagedReceiptEntity> IntakeStagedReceipts => Set<IntakeStagedReceiptEntity>();

    internal DbSet<IntakeWorkItemEntity> IntakeWorkItems => Set<IntakeWorkItemEntity>();
    internal DbSet<IntakeEvaluationEntity> IntakeEvaluations => Set<IntakeEvaluationEntity>();



    internal DbSet<ProviderDomainPackageEntity> ProviderDomainPackages => Set<ProviderDomainPackageEntity>();

    internal DbSet<ProviderReferenceEntity> ProviderReferences => Set<ProviderReferenceEntity>();

    internal DbSet<ProviderDomainEvidenceEntity> ProviderDomainEvidence => Set<ProviderDomainEvidenceEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        CustodyModelConfiguration.Configure(builder);

        builder.Entity<PegasusIdentityUser>(entity =>
        {
            entity.Property(item => item.IsEnabled).HasDefaultValue(true);
            entity.Property(item => item.MustChangePassword).HasDefaultValue(true);
        });

        builder.Entity<ApplicationInitializationEntity>(entity =>
        {
            entity.ToTable("ApplicationInitializations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(32);
            entity.Property(item => item.ManifestSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.MigrationId).HasMaxLength(150).IsRequired();
        });

        builder.Entity<IntakeReceiptEntity>(entity =>
        {
            entity.ToTable("IntakeReceipts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourceChannel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ExternalReceiptToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceReaderKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.SourceReaderVersion).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ExtractionPolicyKey).HasMaxLength(100);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.Decision).HasMaxLength(40).IsRequired();
            entity.Property(item => item.DecisionReason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.Property(item => item.FailureReason).HasMaxLength(500);
            entity.Property(item => item.EvidenceJson).IsRequired();
            entity.Property(item => item.FieldsJson).IsRequired();
            entity.Property(item => item.OcrCandidatesJson).IsRequired();
            entity.HasIndex(item => item.SourceHash);
            entity.HasIndex(item => new { item.SourceChannel, item.ExternalReceiptToken }).IsUnique();
        });

        builder.Entity<IntakeAssetEntity>(entity =>
        {
            entity.ToTable("IntakeAssets");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceLabel).HasMaxLength(500).IsRequired();
            entity.Property(item => item.FileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Disposition).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ContentHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.StorageKey).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.IntakeReceiptId, item.ContentHash });
            entity.HasOne(item => item.IntakeReceipt)
                .WithMany(item => item.Assets)
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InstructionDraftEntity>(entity =>
        {
            entity.ToTable("InstructionDrafts");
            entity.HasKey(item => item.IntakeReceiptId);
            entity.Property(item => item.SuggestedPrincipalCode).HasMaxLength(20);
            entity.Property(item => item.ClaimantName).HasMaxLength(300);
            entity.Property(item => item.ClaimNumber).HasMaxLength(100);
            entity.Property(item => item.VehicleRegistration).HasMaxLength(20);
            entity.Property(item => item.VehicleMake).HasMaxLength(100);
            entity.Property(item => item.VehicleModel).HasMaxLength(100);
            entity.Property(item => item.AccidentCircumstances).HasMaxLength(2000);
            entity.Property(item => item.DateOfIncident).HasColumnType("date");
            entity.Property(item => item.InstructionDate).HasColumnType("date");
            entity.Property(item => item.InspectionAddress).HasMaxLength(1000);
            entity.HasOne(item => item.IntakeReceipt)
                .WithOne(item => item.InstructionDraft)
                .HasForeignKey<InstructionDraftEntity>(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IntakeReceiptEventEntity>(entity =>
        {
            entity.ToTable("IntakeReceiptEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.DetailsJson).IsRequired();
            entity.HasOne<IntakeReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeStagedReceiptEntity>(entity =>
        {
            entity.ToTable("IntakeStagedReceipts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourceChannel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ExternalReceiptToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.StorageKey).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.SourceChannel, item.ExternalReceiptToken }).IsUnique();
            entity.HasIndex(item => item.SourceHash);
        });

        builder.Entity<IntakeWorkItemEntity>(entity =>
        {
            entity.ToTable("IntakeWorkItems", table =>
                table.HasCheckConstraint("CK_IntakeWorkItems_AttemptCount", "[AttemptCount] >= 0"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.LeaseToken).HasMaxLength(64);
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => item.StagedReceiptId).IsUnique();
            entity.HasIndex(item => new { item.State, item.DueAtUtc });
            entity.HasOne(item => item.StagedReceipt)
                .WithOne(item => item.WorkItem)
                .HasForeignKey<IntakeWorkItemEntity>(item => item.StagedReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeEvaluationEntity>(entity =>
        {
            entity.ToTable("IntakeEvaluations");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.StagedReceiptId, item.Revision }).IsUnique();
            entity.HasOne<IntakeStagedReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.StagedReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("Organizations", table =>
            {
                table.HasCheckConstraint("CK_Organizations_Name", "[Name] <> ''");
                table.HasCheckConstraint("CK_Organizations_Version", "[Version] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(300).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.Name);
        });

        builder.Entity<OrganizationRoleEntity>(entity =>
        {
            entity.ToTable("OrganizationRoles", table =>
                table.HasCheckConstraint(
                    "CK_OrganizationRoles_Role",
                    "[Role] IN ('work_provider', 'instruction_intermediary')"));
            entity.HasKey(item => new { item.OrganizationId, item.Role });
            entity.Property(item => item.Role).HasMaxLength(40).IsRequired();
            entity.HasOne(item => item.Organization)
                .WithMany(item => item.Roles)
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PrincipalSequenceLineageEntity>(entity =>
        {
            entity.ToTable("PrincipalSequenceLineages");
            entity.HasKey(item => item.Id);
        });

        builder.Entity<PrincipalEntity>(entity =>
        {
            entity.ToTable("Principals", table =>
            {
                table.HasCheckConstraint("CK_Principals_Code", "[Code] <> ''");
                table.HasCheckConstraint("CK_Principals_Version", "[Version] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.Code).IsUnique();
            entity.HasIndex(item => item.PredecessorId).IsUnique();
            entity.HasIndex(item => item.SuccessorId).IsUnique();
            entity.HasOne(item => item.Organization)
                .WithMany(item => item.Principals)
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SequenceLineage)
                .WithMany(item => item.Principals)
                .HasForeignKey(item => item.SequenceLineageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Predecessor)
                .WithMany()
                .HasForeignKey(item => item.PredecessorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Successor)
                .WithMany()
                .HasForeignKey(item => item.SuccessorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseSequenceEntity>(entity =>
        {
            entity.ToTable("CaseSequences", table =>
            {
                table.HasCheckConstraint("CK_CaseSequences_Year", "[Year] >= 2000 AND [Year] <= 9999");
                table.HasCheckConstraint(
                    "CK_CaseSequences_LastAllocatedSequence",
                    "[LastAllocatedSequence] >= 0 AND [LastAllocatedSequence] <= 999");
            });
            entity.HasKey(item => new { item.SequenceLineageId, item.Year });
            entity.HasOne(item => item.SequenceLineage)
                .WithMany(item => item.Sequences)
                .HasForeignKey(item => item.SequenceLineageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseEntity>(entity =>
        {
            entity.ToTable("Cases", table =>
            {
                table.HasCheckConstraint("CK_Cases_Sequence", "[Sequence] >= 1 AND [Sequence] <= 999");
                table.HasCheckConstraint("CK_Cases_Version", "[Version] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Reference).HasMaxLength(40).IsRequired();
            entity.Property(item => item.AuditReference).HasMaxLength(43);
            entity.Property(item => item.Type).HasMaxLength(40).IsRequired();
            entity.Property(item => item.InitialState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CustodyState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.StandaloneAuditAssessment).HasMaxLength(40);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.Property(item => item.CustodyRootRemoteId).HasMaxLength(200);
            entity.Property(item => item.CustodySourceRemoteId).HasMaxLength(200);
            entity.Property(item => item.CustodySourceContentHash).HasMaxLength(64);
            entity.Property(item => item.CustodySourceETag).HasMaxLength(200);
            entity.HasIndex(item => item.Reference).IsUnique();
            entity.HasIndex(item => item.AuditReference).IsUnique();
            entity.HasIndex(item => item.OriginIntakeReceiptId).IsUnique();
            entity.HasIndex(item => new { item.SequenceLineageId, item.Year, item.Sequence }).IsUnique();
            entity.HasOne(item => item.Principal)
                .WithMany(item => item.Cases)
                .HasForeignKey(item => item.PrincipalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IntakeReceiptEntity>()
                .WithOne()
                .HasForeignKey<CaseEntity>(item => item.OriginIntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExternalWorkItemEntity>(entity =>
        {
            entity.ToTable("ExternalWorkItems", table =>
                table.HasCheckConstraint("CK_ExternalWorkItems_AttemptCount", "[AttemptCount] >= 0"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(100).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.LeaseToken).HasMaxLength(64);
            entity.Property(item => item.ExternalReceipt).HasMaxLength(500);
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.Property(item => item.FailureReason).HasMaxLength(500);
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.State, item.DueAtUtc });
            entity.HasOne(item => item.Case)
                .WithMany(item => item.ExternalWork)
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseIntakeLinkEntity>(entity =>
        {
            entity.ToTable("CaseIntakeLinks");
            entity.HasKey(item => item.IntakeReceiptId);
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.CustodyWorkId).IsUnique();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasOne(item => item.Case)
                .WithMany(item => item.IntakeLinks)
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IntakeReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CustodyWork)
                .WithOne()
                .HasForeignKey<CaseIntakeLinkEntity>(item => item.CustodyWorkId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseHistoryEntity>(entity =>
        {
            entity.ToTable("CaseHistory");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.OccurredAtUtc });
            entity.HasOne(item => item.Case)
                .WithMany(item => item.History)
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProviderDomainPackageEntity>(entity =>
        {
            entity.ToTable("ProviderDomainPackages", table =>
            {
                table.HasCheckConstraint("CK_ProviderDomainPackages_SchemaVersion", "[SchemaVersion] > 0");
                table.HasCheckConstraint("CK_ProviderDomainPackages_SourceRowCount", "[SourceRowCount] > 0");
            });
            entity.HasKey(item => item.Version);
            entity.Property(item => item.Version).HasMaxLength(64).IsRequired();
            entity.Property(item => item.PackageSha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourcePath).HasMaxLength(512).IsRequired();
            entity.Property(item => item.SourceContentSha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourceSheet).HasMaxLength(31).IsRequired();
            entity.HasMany(item => item.Providers)
                .WithOne(item => item.Package)
                .HasForeignKey(item => item.Version)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProviderReferenceEntity>(entity =>
        {
            entity.ToTable("ProviderReferences", table =>
                table.HasCheckConstraint("CK_ProviderReferences_SourceRow", "[SourceRow] > 0"));
            entity.HasKey(item => new { item.Version, item.Code });
            entity.Property(item => item.Version).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(20).IsRequired();
            entity.HasMany(item => item.DomainEvidence)
                .WithOne(item => item.Provider)
                .HasForeignKey(item => new { item.Version, item.Code })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProviderDomainEvidenceEntity>(entity =>
        {
            entity.ToTable("ProviderDomainEvidence");
            entity.HasKey(item => new { item.Version, item.Code, item.DomainSuffix });
            entity.Property(item => item.Version).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DomainSuffix).HasMaxLength(254).IsRequired();
            entity.HasIndex(item => new { item.Version, item.DomainSuffix });
        });
    }
}

public sealed class PegasusIdentityUser : IdentityUser<Guid>
{
    public bool IsEnabled { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;
}

internal sealed class ApplicationInitializationEntity
{
    public required string Id { get; set; }

    public required string ManifestSha256 { get; set; }

    public required string MigrationId { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }
}
internal sealed class OrganizationEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public long Version { get; set; }
    public List<OrganizationRoleEntity> Roles { get; set; } = [];
    public List<PrincipalEntity> Principals { get; set; } = [];
}

internal sealed class OrganizationRoleEntity
{
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = null!;
    public required string Role { get; set; }
}

internal sealed class PrincipalSequenceLineageEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<PrincipalEntity> Principals { get; set; } = [];
    public List<CaseSequenceEntity> Sequences { get; set; } = [];
}

internal sealed class PrincipalEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = null!;
    public required string Code { get; set; }
    public Guid SequenceLineageId { get; set; }
    public PrincipalSequenceLineageEntity SequenceLineage { get; set; } = null!;
    public Guid? PredecessorId { get; set; }
    public PrincipalEntity? Predecessor { get; set; }
    public Guid? SuccessorId { get; set; }
    public PrincipalEntity? Successor { get; set; }
    public bool IsActive { get; set; }
    public long Version { get; set; }
    public List<CaseEntity> Cases { get; set; } = [];
}

internal sealed class CaseSequenceEntity
{
    public Guid SequenceLineageId { get; set; }
    public PrincipalSequenceLineageEntity SequenceLineage { get; set; } = null!;
    public int Year { get; set; }
    public int LastAllocatedSequence { get; set; }
}

internal sealed class CaseEntity
{
    public Guid Id { get; set; }
    public Guid PrincipalId { get; set; }
    public PrincipalEntity Principal { get; set; } = null!;
    public Guid SequenceLineageId { get; set; }
    public int Year { get; set; }
    public int Sequence { get; set; }
    public required string Reference { get; set; }
    public string? AuditReference { get; set; }
    public required string Type { get; set; }
    public required string InitialState { get; set; }
    public required string CustodyState { get; set; }
    public Guid OriginIntakeReceiptId { get; set; }
    public string? StandaloneAuditAssessment { get; set; }
    public bool InstructionComplete { get; set; }
    public bool ImagesComplete { get; set; }
    public bool InstructionConfirmedByStaff { get; set; }
    public bool ImagesConfirmedByStaff { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long Version { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public string? CustodyRootRemoteId { get; set; }
    public string? CustodySourceRemoteId { get; set; }
    public string? CustodySourceContentHash { get; set; }
    public string? CustodySourceETag { get; set; }
    public DateTimeOffset? CustodyConfirmedAtUtc { get; set; }
    public List<CaseIntakeLinkEntity> IntakeLinks { get; set; } = [];
    public List<CaseHistoryEntity> History { get; set; } = [];
    public List<ExternalWorkItemEntity> ExternalWork { get; set; } = [];
}

internal sealed class CaseIntakeLinkEntity
{
    public Guid IntakeReceiptId { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public Guid CustodyWorkId { get; set; }
    public ExternalWorkItemEntity CustodyWork { get; set; } = null!;
    public DateTimeOffset LinkedAtUtc { get; set; }
    public required string Actor { get; set; }
    public required string OperationKey { get; set; }
}

internal sealed class CaseHistoryEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string EventType { get; set; }
    public required string Actor { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public required string OperationKey { get; set; }
    public long? BeforeVersion { get; set; }
    public long AfterVersion { get; set; }
}

internal sealed class ExternalWorkItemEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string Kind { get; set; }
    public required string OperationKey { get; set; }
    public required string State { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public string? ExternalReceipt { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}


internal sealed class IntakeReceiptEntity
{
    public Guid Id { get; set; }
    public required string SourceFileName { get; set; }
    public required string MediaType { get; set; }
    public long SourceLength { get; set; }
    public required string SourceHash { get; set; }
    public required string SourceChannel { get; set; }
    public required string ExternalReceiptToken { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset ProcessedAtUtc { get; set; }
    public required string SourceReaderKey { get; set; }
    public required string SourceReaderVersion { get; set; }
    public string? ExtractionPolicyKey { get; set; }
    public long Version { get; set; }
    public int? ExtractionPolicyVersion { get; set; }
    public required string Decision { get; set; }
    public required string DecisionReason { get; set; }
    public required string EvidenceJson { get; set; }
    public required string FieldsJson { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public required string OcrCandidatesJson { get; set; }
    public InstructionDraftEntity? InstructionDraft { get; set; }
    public List<IntakeAssetEntity> Assets { get; set; } = [];
}

internal sealed class InstructionDraftEntity
{
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public string? SuggestedPrincipalCode { get; set; }
    public string? ClaimantName { get; set; }
    public string? ClaimNumber { get; set; }
    public string? VehicleRegistration { get; set; }
    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public long? VehicleMileage { get; set; }
    public string? AccidentCircumstances { get; set; }
    public DateOnly? DateOfIncident { get; set; }
    public DateOnly? InstructionDate { get; set; }
    public string? InspectionAddress { get; set; }
}

internal sealed class IntakeAssetEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public required string SourceLabel { get; set; }
    public required string FileName { get; set; }
    public required string MediaType { get; set; }
    public required string Kind { get; set; }
    public required string Disposition { get; set; }
    public long ContentLength { get; set; }
    public required string ContentHash { get; set; }
    public required string StorageKey { get; set; }
    public int? PageNumber { get; set; }
    public string? BoundsJson { get; set; }
    public int? WidthPixels { get; set; }
    public int? HeightPixels { get; set; }
}

internal sealed class IntakeReceiptEventEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public required string EventType { get; set; }
    public required string Actor { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public required string DetailsJson { get; set; }
}

internal sealed class IntakeStagedReceiptEntity
{
    public Guid Id { get; set; }
    public required string SourceFileName { get; set; }
    public required string MediaType { get; set; }
    public long SourceLength { get; set; }
    public required string SourceHash { get; set; }
    public required string SourceChannel { get; set; }
    public required string ExternalReceiptToken { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public required string Actor { get; set; }
    public required string StorageKey { get; set; }
    public DateTimeOffset StagedAtUtc { get; set; }
    public IntakeWorkItemEntity? WorkItem { get; set; }
}

internal sealed class IntakeWorkItemEntity
{
    public Guid Id { get; set; }
    public Guid StagedReceiptId { get; set; }
    public IntakeStagedReceiptEntity StagedReceipt { get; set; } = null!;
    public required string OperationKey { get; set; }
    public required string State { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public Guid? ProcessedReceiptId { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

internal sealed class IntakeEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid StagedReceiptId { get; set; }
    public Guid ProcessedReceiptId { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset EvaluatedAtUtc { get; set; }
}

internal sealed class ProviderDomainPackageEntity
{
    public required string Version { get; set; }
    public int SchemaVersion { get; set; }
    public required string PackageSha256 { get; set; }
    public required string SourcePath { get; set; }
    public required string SourceContentSha256 { get; set; }
    public required string SourceSheet { get; set; }
    public int SourceRowCount { get; set; }
    public List<ProviderReferenceEntity> Providers { get; set; } = [];
}

internal sealed class ProviderReferenceEntity
{
    public required string Version { get; set; }
    public required string Code { get; set; }
    public int SourceRow { get; set; }
    public ProviderDomainPackageEntity Package { get; set; } = null!;
    public List<ProviderDomainEvidenceEntity> DomainEvidence { get; set; } = [];
}

internal sealed class ProviderDomainEvidenceEntity
{
    public required string Version { get; set; }
    public required string Code { get; set; }
    public required string DomainSuffix { get; set; }
    public ProviderReferenceEntity Provider { get; set; } = null!;
}
