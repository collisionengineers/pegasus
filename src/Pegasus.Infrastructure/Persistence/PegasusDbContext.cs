using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

public sealed class PegasusDbContext(DbContextOptions<PegasusDbContext> options)
    : IdentityDbContext<StaffAccount, StaffRoleEntity, Guid>(options)
{
    internal DbSet<IntakeReceiptEntity> IntakeReceipts => Set<IntakeReceiptEntity>();

    internal DbSet<IntakeAssetEntity> IntakeAssets => Set<IntakeAssetEntity>();

    internal DbSet<InstructionDraftEntity> InstructionDrafts => Set<InstructionDraftEntity>();

    internal DbSet<IntakeReceiptEventEntity> IntakeReceiptEvents => Set<IntakeReceiptEventEntity>();

    internal DbSet<ProviderDomainPackageEntity> ProviderDomainPackages => Set<ProviderDomainPackageEntity>();

    internal DbSet<ProviderReferenceEntity> ProviderReferences => Set<ProviderReferenceEntity>();

    internal DbSet<ProviderDomainEvidenceEntity> ProviderDomainEvidence => Set<ProviderDomainEvidenceEntity>();
    internal DbSet<TriageEntity> Triages => Set<TriageEntity>();
    internal DbSet<TriageFindingEntity> TriageFindings => Set<TriageFindingEntity>();
    internal DbSet<TriageEvidenceEntity> TriageEvidence => Set<TriageEvidenceEntity>();
    internal DbSet<TriageCaseLinkEntity> TriageCaseLinks => Set<TriageCaseLinkEntity>();
    internal DbSet<CaseEntity> Cases => Set<CaseEntity>();
    internal DbSet<CaseSequenceEntity> CaseSequences => Set<CaseSequenceEntity>();
    internal DbSet<CaseLeaseEntity> CaseLeases => Set<CaseLeaseEntity>();
    internal DbSet<BusinessActionEntity> BusinessActions => Set<BusinessActionEntity>();

#pragma warning disable CA1725
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<StaffAccount>(entity =>
        {
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<TriageEntity>(entity =>
        {
            entity.ToTable("Triages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Registration).HasMaxLength(20).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Ignore(item => item.CurrentFinding);
        });
        modelBuilder.Entity<TriageFindingEntity>(entity =>
        {
            entity.ToTable("TriageFindings");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Roadworthiness).HasMaxLength(40);
            entity.Property(item => item.Assessment).HasMaxLength(40);
            entity.Property(item => item.Reason).HasMaxLength(1000);
            entity.HasOne(item => item.Triage).WithMany(item => item.Findings).HasForeignKey(item => item.TriageId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TriageEvidenceEntity>(entity =>
        {
            entity.ToTable("TriageReplyEvidence");
            entity.HasKey(item => item.TriageId);
            entity.Property(item => item.ExternalMessageId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ConversationId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ApprovedMailbox).HasMaxLength(320).IsRequired();
            entity.Property(item => item.ReplyHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(item => new { item.ExternalMessageId, item.ConversationId }).IsUnique();
        });
        modelBuilder.Entity<TriageCaseLinkEntity>(entity =>
        {
            entity.ToTable("TriageCaseLinks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Reason).HasMaxLength(1000);
            entity.HasIndex(item => new { item.TriageId, item.UnlinkedAtUtc });
            entity.HasOne(item => item.Triage).WithMany(item => item.CaseLinks).HasForeignKey(item => item.TriageId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CaseEntity>(entity =>
        {
            entity.ToTable("Cases");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PrincipalCode).HasMaxLength(20).IsRequired();
            entity.Property(item => item.BaseReference).HasMaxLength(40).IsRequired();
            entity.Property(item => item.DisplayReference).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Type).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Registration).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Claimant).HasMaxLength(300);
            entity.Property(item => item.ClaimNumber).HasMaxLength(100);
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.TerminalOutcome).HasMaxLength(60);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.DisplayReference).IsUnique();
            entity.HasIndex(item => new { item.State, item.IsHeld, item.NextDueAtUtc });
            entity.HasIndex(item => new { item.Registration, item.PrincipalCode });
        });
        modelBuilder.Entity<CaseSequenceEntity>(entity =>
        {
            entity.ToTable("CaseSequences");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PrincipalCode).HasMaxLength(20).IsRequired();
            entity.HasIndex(item => new { item.PrincipalCode, item.Year }).IsUnique();
            entity.ToTable("CaseSequences", table => table.HasCheckConstraint("CK_CaseSequences_LastSequence", "[LastSequence] BETWEEN 0 AND 999"));
        });
        modelBuilder.Entity<CaseLeaseEntity>(entity =>
        {
            entity.ToTable("CaseLeases");
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(item => item.HolderName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<BusinessActionEntity>(entity =>
        {
            entity.ToTable("BusinessActions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Caller).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(1000);
            entity.HasIndex(item => new { item.CaseId, item.OccurredAtUtc });
            entity.HasIndex(item => new { item.TriageId, item.OccurredAtUtc });
        });
        modelBuilder.Entity<IntakeReceiptEntity>(entity =>
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

        modelBuilder.Entity<IntakeAssetEntity>(entity =>
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

        modelBuilder.Entity<InstructionDraftEntity>(entity =>
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

        modelBuilder.Entity<IntakeReceiptEventEntity>(entity =>
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

        modelBuilder.Entity<ProviderDomainPackageEntity>(entity =>
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

        modelBuilder.Entity<ProviderReferenceEntity>(entity =>
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

        modelBuilder.Entity<ProviderDomainEvidenceEntity>(entity =>
        {
            entity.ToTable("ProviderDomainEvidence");
            entity.HasKey(item => new { item.Version, item.Code, item.DomainSuffix });
            entity.Property(item => item.Version).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DomainSuffix).HasMaxLength(254).IsRequired();
            entity.HasIndex(item => new { item.Version, item.DomainSuffix });
        });
    }
#pragma warning restore CA1725
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
