using Microsoft.EntityFrameworkCore;

namespace CollisionSpike.Infrastructure.Persistence;

public sealed class CollisionSpikeDbContext(DbContextOptions<CollisionSpikeDbContext> options) : DbContext(options)
{
    internal DbSet<QdosIntakeReceiptEntity> QdosIntakeReceipts => Set<QdosIntakeReceiptEntity>();

    internal DbSet<QdosIntakeAssetEntity> QdosIntakeAssets => Set<QdosIntakeAssetEntity>();

    internal DbSet<QdosTypedDraftEntity> QdosTypedDrafts => Set<QdosTypedDraftEntity>();

    internal DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QdosIntakeReceiptEntity>(entity =>
        {
            entity.ToTable("QdosIntakeReceipts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourceChannel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ExternalReceiptToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Decision).HasMaxLength(40).IsRequired();
            entity.Property(item => item.DecisionReason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.Property(item => item.FailureReason).HasMaxLength(500);
            entity.Property(item => item.OcrCandidatesJson).IsRequired();
            entity.HasIndex(item => item.SourceHash);
            entity.HasIndex(item => new { item.SourceChannel, item.ExternalReceiptToken }).IsUnique();
        });

        modelBuilder.Entity<QdosIntakeAssetEntity>(entity =>
        {
            entity.ToTable("QdosIntakeAssets");
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

        modelBuilder.Entity<QdosTypedDraftEntity>(entity =>
        {
            entity.ToTable("QdosTypedDrafts");
            entity.HasKey(item => item.IntakeReceiptId);
            entity.Property(item => item.PrincipalCode).HasMaxLength(20).IsRequired();
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
                .WithOne(item => item.TypedDraft)
                .HasForeignKey<QdosTypedDraftEntity>(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditEventEntity>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.HasOne<QdosIntakeReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class QdosIntakeReceiptEntity
{
    public Guid Id { get; set; }

    public required string SourceFileName { get; set; }

    public required string MediaType { get; set; }

    public long SourceLength { get; set; }

    public required string SourceHash { get; set; }

    public required string SourceChannel { get; set; }

    public required string ExternalReceiptToken { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public required string Decision { get; set; }

    public required string DecisionReason { get; set; }

    public required string EvidenceJson { get; set; }

    public required string FieldsJson { get; set; }

    public string? FailureCode { get; set; }

    public string? FailureReason { get; set; }

    public required string OcrCandidatesJson { get; set; }

    public QdosTypedDraftEntity? TypedDraft { get; set; }

    public List<QdosIntakeAssetEntity> Assets { get; set; } = [];
}

internal sealed class QdosTypedDraftEntity
{
    public Guid IntakeReceiptId { get; set; }

    public QdosIntakeReceiptEntity IntakeReceipt { get; set; } = null!;

    public required string PrincipalCode { get; set; }

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

internal sealed class QdosIntakeAssetEntity
{
    public Guid Id { get; set; }

    public Guid IntakeReceiptId { get; set; }

    public QdosIntakeReceiptEntity IntakeReceipt { get; set; } = null!;

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

internal sealed class AuditEventEntity
{
    public Guid Id { get; set; }

    public Guid IntakeReceiptId { get; set; }

    public required string EventType { get; set; }

    public required string Actor { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public required string DetailsJson { get; set; }
}
