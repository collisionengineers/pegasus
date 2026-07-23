using Microsoft.EntityFrameworkCore;

namespace CollisionSpike.Infrastructure.Persistence;

public sealed class CollisionSpikeDbContext(DbContextOptions<CollisionSpikeDbContext> options) : DbContext(options)
{
    internal DbSet<CaseEntity> Cases => Set<CaseEntity>();

    internal DbSet<QdosIntakeReceiptEntity> QdosIntakeReceipts => Set<QdosIntakeReceiptEntity>();

    internal DbSet<PrincipalYearCounterEntity> PrincipalYearCounters => Set<PrincipalYearCounterEntity>();

    internal DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseEntity>(entity =>
        {
            entity.ToTable("Cases");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PrincipalCode).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CaseReference).HasMaxLength(32).IsRequired();
            entity.HasIndex(item => item.CaseReference).IsUnique();
        });

        modelBuilder.Entity<QdosIntakeReceiptEntity>(entity =>
        {
            entity.ToTable("QdosIntakeReceipts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Decision).HasMaxLength(40).IsRequired();
            entity.Property(item => item.DecisionReason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.Property(item => item.FailureReason).HasMaxLength(500);
            entity.HasIndex(item => item.SourceHash).IsUnique();
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PrincipalYearCounterEntity>(entity =>
        {
            entity.ToTable("PrincipalYearCounters");
            entity.HasKey(item => new { item.PrincipalCode, item.Year });
            entity.Property(item => item.PrincipalCode).HasMaxLength(20);
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
            entity.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class CaseEntity
{
    public Guid Id { get; set; }

    public required string PrincipalCode { get; set; }

    public required string CaseReference { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class QdosIntakeReceiptEntity
{
    public Guid Id { get; set; }

    public required string SourceFileName { get; set; }

    public required string MediaType { get; set; }

    public long SourceLength { get; set; }

    public required string SourceHash { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public required string Decision { get; set; }

    public required string DecisionReason { get; set; }

    public required string EvidenceJson { get; set; }

    public required string FieldsJson { get; set; }

    public string? FailureCode { get; set; }

    public string? FailureReason { get; set; }

    public Guid? CaseId { get; set; }

    public CaseEntity? Case { get; set; }
}

internal sealed class PrincipalYearCounterEntity
{
    public required string PrincipalCode { get; set; }

    public int Year { get; set; }

    public int CurrentSequence { get; set; }
}

internal sealed class AuditEventEntity
{
    public Guid Id { get; set; }

    public Guid IntakeReceiptId { get; set; }

    public Guid? CaseId { get; set; }

    public required string EventType { get; set; }

    public required string Actor { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public required string DetailsJson { get; set; }
}
