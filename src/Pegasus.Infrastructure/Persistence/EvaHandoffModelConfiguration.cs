using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class EvaHandoffModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<EvaHandoffRevisionEntity>(entity =>
        {
            entity.ToTable("EvaHandoffRevisions", table =>
            {
                table.HasCheckConstraint("CK_EvaHandoffRevisions_Revision", "[Revision] > 0");
                table.HasCheckConstraint(
                    "CK_EvaHandoffRevisions_AcceptedCaseVersion",
                    "[AcceptedCaseVersion] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SchemaVersion).HasMaxLength(50).IsRequired();
            entity.Property(item => item.InputFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.FileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.BundleSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.JsonSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.ProvenanceSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.GeneratedBy).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.CaseId, item.Revision }).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.InputFingerprint }).IsUnique();
            entity.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EvaHandoffOperationEntity>(entity =>
        {
            entity.ToTable("EvaHandoffOperations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.RecordedAtUtc });
            entity.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaHandoffRevisionEntity>()
                .WithMany()
                .HasForeignKey(item => item.RevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EvaFirstHandoffProxyEntity>(entity =>
        {
            entity.ToTable("EvaFirstHandoffProxies", table =>
            {
                table.HasCheckConstraint(
                    "CK_EvaFirstHandoffProxies_NoDeliveryClaim",
                    "[ClaimsExternalDelivery] = 0");
                table.HasCheckConstraint(
                    "CK_EvaFirstHandoffProxies_NoAssignmentClaim",
                    "[ClaimsEngineerAssignment] = 0");
            });
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.AdapterKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.AdapterVersion).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.RevisionId).IsUnique();
            entity.HasOne<CaseEntity>()
                .WithOne()
                .HasForeignKey<EvaFirstHandoffProxyEntity>(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaHandoffRevisionEntity>()
                .WithOne()
                .HasForeignKey<EvaFirstHandoffProxyEntity>(item => item.RevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EvaHandoffDownloadOperationEntity>(entity =>
        {
            entity.ToTable("EvaHandoffDownloadOperations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.PreparedAtUtc });
            entity.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaHandoffRevisionEntity>()
                .WithMany()
                .HasForeignKey(item => item.RevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
