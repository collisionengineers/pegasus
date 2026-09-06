using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class CaseDataModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<CaseDataSnapshotEntity>(entity =>
        {
            entity.ToTable("CaseDataSnapshots", table =>
            {
                table.HasCheckConstraint(
                    "CK_CaseDataSnapshots_CompletenessPolicyVersion",
                    "[CompletenessPolicyVersion] > 0");
                table.HasCheckConstraint(
                    "CK_CaseDataSnapshots_ExtractionPolicyVersion",
                    "[ExtractionPolicyVersion] IS NULL OR [ExtractionPolicyVersion] > 0");
            });
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.OriginSourceChannel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.OriginExternalReceiptToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OriginSourceHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.SourceReaderKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.SourceReaderVersion).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ExtractionPolicyKey).HasMaxLength(100);
            entity.Property(item => item.CompletenessPolicyKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.OriginIntakeReceiptId);
            entity.HasOne(item => item.Case)
                .WithOne()
                .HasForeignKey<CaseDataSnapshotEntity>(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IntakeReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.OriginIntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseDataFieldEntity>(entity =>
        {
            entity.ToTable("CaseDataFields", table =>
            {
                table.HasCheckConstraint(
                    "CK_CaseDataFields_FieldName",
                    "[FieldName] <> '' AND LEN([FieldName]) <= 60");
                table.HasCheckConstraint(
                    "CK_CaseDataFields_ValueKind",
                    "[ValueKind] IN ('fact', 'suggestion', 'confirmed')");
                table.HasCheckConstraint(
                    "CK_CaseDataFields_ValueType",
                    "[ValueType] IN ('text', 'integer', 'date', 'inspection_mode')");
                table.HasCheckConstraint(
                    "CK_CaseDataFields_SourceKind",
                    "[SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup', 'provider_setting', 'provider_api')");
                table.HasCheckConstraint(
                    "CK_CaseDataFields_Confirmation",
                    "([ValueKind] = 'confirmed' AND [ConfirmedByActor] IS NOT NULL AND [ConfirmedAtUtc] IS NOT NULL) OR "
                    + "([ValueKind] <> 'confirmed' AND [ConfirmedByActor] IS NULL AND [ConfirmedAtUtc] IS NULL)");
                table.HasCheckConstraint(
                    "CK_CaseDataFields_PolicyVersion",
                    "[PolicyVersion] > 0");
            });
            entity.HasKey(item => new { item.CaseId, item.FieldName, item.ValueKind });
            entity.Property(item => item.FieldName).HasMaxLength(60).IsRequired();
            entity.Property(item => item.ValueKind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.ValueType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Value).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.SourceKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SourceIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.SourceLabel).HasMaxLength(500).IsRequired();
            entity.Property(item => item.PolicyKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ConfirmedByActor).HasMaxLength(200);
            entity.HasIndex(item => new { item.FieldName, item.ValueKind });
            entity.HasOne(item => item.Snapshot)
                .WithMany(item => item.Fields)
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static string SqlLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
