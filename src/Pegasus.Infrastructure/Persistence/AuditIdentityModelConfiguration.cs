using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class AuditIdentityModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<InstructionDraftEntity>(entity =>
            entity.Property(item => item.InspectionDate).HasColumnType("date"));

        builder.Entity<CaseEntity>(entity =>
        {
            entity.Property(item => item.AcceptedInspectionDeadline).HasColumnType("date");
            entity.Property(item => item.AuditCustodyRemoteId).HasMaxLength(200);
            entity.HasOne<StandaloneAuditEvidenceEntity>()
                .WithMany()
                .HasForeignKey(item => item.StandaloneAuditEvidenceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StandaloneAuditEvidenceEntity>(entity =>
        {
            entity.ToTable("StandaloneAuditEvidence");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Assessment).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ConfirmedByKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ConfirmedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ConfirmedByRolesJson).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(item => item.IntakeReceiptId).IsUnique();
            entity.HasIndex(item => item.OriginalReportAssetId).IsUnique();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasOne(item => item.IntakeReceipt)
                .WithOne()
                .HasForeignKey<StandaloneAuditEvidenceEntity>(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OriginalReportAsset)
                .WithOne()
                .HasForeignKey<StandaloneAuditEvidenceEntity>(item => item.OriginalReportAssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseEngineerFindingEntity>(entity =>
        {
            entity.ToTable("CaseEngineerFindings");
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.Assessment).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RecordedByKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RecordedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.RecordedByRolesJson).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => item.CustodyWorkId).IsUnique();
            entity.HasOne(item => item.Case)
                .WithOne(item => item.EngineerFinding)
                .HasForeignKey<CaseEngineerFindingEntity>(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CustodyWork)
                .WithOne()
                .HasForeignKey<CaseEngineerFindingEntity>(item => item.CustodyWorkId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class StandaloneAuditEvidenceEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public Guid OriginalReportAssetId { get; set; }
    public IntakeAssetEntity OriginalReportAsset { get; set; } = null!;
    public required string Assessment { get; set; }
    public required string ConfirmedByKind { get; set; }
    public required string ConfirmedBySubjectId { get; set; }
    public required string ConfirmedByRolesJson { get; set; }
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public required string OperationKey { get; set; }
    public required string Reason { get; set; }
    public required string RequestHash { get; set; }
    public long ResultingReceiptVersion { get; set; }
}

internal sealed class CaseEngineerFindingEntity
{
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string Assessment { get; set; }
    public required string RecordedByKind { get; set; }
    public required string RecordedBySubjectId { get; set; }
    public required string RecordedByRolesJson { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public Guid CustodyWorkId { get; set; }
    public ExternalWorkItemEntity CustodyWork { get; set; } = null!;
}
