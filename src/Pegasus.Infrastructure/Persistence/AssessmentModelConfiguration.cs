using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.AiWork;

namespace Pegasus.Infrastructure.Persistence;

internal static class AssessmentModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        var fieldPaths = string.Join(
            ", ",
            AssessmentVocabulary.Definitions.Keys.OrderBy(path => path, StringComparer.Ordinal)
                .Select(SqlLiteral));
        builder.Entity<CaseAssessmentFieldEntity>(entity =>
        {
            entity.ToTable("CaseAssessmentFields", table =>
            {
                table.HasCheckConstraint(
                    "CK_CaseAssessmentFields_FieldPath",
                    $"[FieldPath] IN ({fieldPaths})");
                table.HasCheckConstraint(
                    "CK_CaseAssessmentFields_RecordedByKind",
                    "[RecordedByKind] IN ('Staff', 'Automation')");
                table.HasCheckConstraint(
                    "CK_CaseAssessmentFields_Confirmation",
                    "([ConfirmedBy] IS NULL AND [ConfirmedAtUtc] IS NULL) OR "
                    + "([ConfirmedBy] IS NOT NULL AND [ConfirmedAtUtc] IS NOT NULL)");
            });
            entity.HasKey(item => new { item.CaseId, item.FieldPath });
            entity.Property(item => item.FieldPath).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Value).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.RecordedByKind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RecordedBy).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ConfirmedBy).HasMaxLength(200);
            entity.HasIndex(item => item.FieldPath);
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseEstimateLineEntity>(entity =>
        {
            var lineTypes = string.Join(", ", EstimateLineCodes.Types.Select(SqlLiteral));
            var statuses = string.Join(", ", EstimateLineCodes.Statuses.Select(SqlLiteral));
            var evidenceLabels = string.Join(
                ", ",
                EstimateLineCodes.EvidenceLabels.Select(SqlLiteral));
            entity.ToTable("CaseEstimateLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_CaseEstimateLines_LineType",
                    $"[LineType] IN ({lineTypes})");
                table.HasCheckConstraint(
                    "CK_CaseEstimateLines_Status",
                    $"[Status] IS NULL OR [Status] IN ({statuses})");
                table.HasCheckConstraint(
                    "CK_CaseEstimateLines_EvidenceLabel",
                    $"[EvidenceLabel] IS NULL OR [EvidenceLabel] IN ({evidenceLabels})");
                table.HasCheckConstraint(
                    "CK_CaseEstimateLines_Position",
                    "[Position] > 0");
                table.HasCheckConstraint(
                    "CK_CaseEstimateLines_Unpriced",
                    "[Unpriced] = 0 OR [Price] IS NULL");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.LineType).HasMaxLength(20).IsRequired();
            entity.Property(item => item.GuideCode).HasMaxLength(50);
            entity.Property(item => item.Description).HasMaxLength(300);
            entity.Property(item => item.WorkUnits).HasPrecision(9, 1);
            entity.Property(item => item.Price).HasPrecision(18, 2);
            entity.Property(item => item.PartNumber).HasMaxLength(100);
            entity.Property(item => item.Betterment).HasMaxLength(100);
            entity.Property(item => item.Status).HasMaxLength(20);
            entity.Property(item => item.EvidenceLabel).HasMaxLength(20);
            entity.Property(item => item.Justification).HasMaxLength(500);
            entity.Property(item => item.RecordedByKind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RecordedBy).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ConfirmedBy).HasMaxLength(200);
            entity.HasIndex(item => new { item.RepairSpecificationId, item.Position })
                .IsUnique()
                .HasFilter("[RepairSpecificationId] IS NOT NULL");
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RepairSpecification)
                .WithMany(item => item.Lines)
                .HasForeignKey(item => item.RepairSpecificationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseRepairSpecificationEntity>(entity =>
        {
            var states = string.Join(", ", Enum.GetNames<RepairSpecificationState>().Select(SqlLiteral));
            var routes = string.Join(", ", Enum.GetNames<RepairSpecificationSourceRoute>().Select(SqlLiteral));
            entity.ToTable("CaseRepairSpecifications", table =>
            {
                table.HasCheckConstraint("CK_CaseRepairSpecifications_State", $"[State] IN ({states})");
                table.HasCheckConstraint("CK_CaseRepairSpecifications_SourceRoute", $"[SourceRoute] IN ({routes})");
                table.HasCheckConstraint("CK_CaseRepairSpecifications_Version", "[Version] > 0");
                table.HasCheckConstraint(
                    "CK_CaseRepairSpecifications_Acceptance",
                    "([State] IN ('Accepted', 'Superseded') AND [AcceptedBy] IS NOT NULL AND [AcceptedAtUtc] IS NOT NULL) OR "
                    + "([State] = 'Draft' AND [AcceptedBy] IS NULL AND [AcceptedAtUtc] IS NULL)");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.SourceRoute).HasMaxLength(30).IsRequired();
            entity.Property(item => item.SourceArtifactReference).HasMaxLength(500);
            entity.Property(item => item.SourceVersion).HasMaxLength(100);
            entity.Property(item => item.SourceSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.CalculationLabour).HasPrecision(18, 2);
            entity.Property(item => item.CalculationParts).HasPrecision(18, 2);
            entity.Property(item => item.CalculationPaintMaterials).HasPrecision(18, 2);
            entity.Property(item => item.CalculationSpecialistOther).HasPrecision(18, 2);
            entity.Property(item => item.CalculationVat).HasPrecision(18, 2);
            entity.Property(item => item.CalculationTotal).HasPrecision(18, 2);
            entity.Property(item => item.CalculationPolicyVersion).HasMaxLength(100);
            entity.Property(item => item.CreatedBy).HasMaxLength(200).IsRequired();
            entity.Property(item => item.CreationOperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.AcceptedBy).HasMaxLength(200);
            entity.Property(item => item.SupersessionReason).HasMaxLength(500);
            entity.HasIndex(item => new { item.CaseId, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.CreationOperationKey }).IsUnique();
            entity.HasIndex(item => item.CaseId)
                .IsUnique()
                .HasFilter("[State] = 'Accepted'");
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AiWorkRequestEntity>(entity =>
        {
            var states = string.Join(
                ", ",
                Enum.GetNames<AiWorkRequestState>().Select(SqlLiteral));
            entity.ToTable("AiWorkRequests", table =>
            {
                table.HasCheckConstraint("CK_AiWorkRequests_State", $"[State] IN ({states})");
                table.HasCheckConstraint(
                    "CK_AiWorkRequests_CaseVersion",
                    "[CaseVersionAtSend] >= 0");
            });
            entity.HasKey(item => item.RequestId);
            entity.Property(item => item.RequestId).ValueGeneratedNever();
            entity.Property(item => item.CaseReference).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CapabilityScope).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Instruction).HasMaxLength(500).IsRequired();
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.CreatedBy).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ClosureReason).HasMaxLength(500);
            entity.Property(item => item.ReplyStatus).HasMaxLength(40);
            entity.Property(item => item.ReplyMessage).HasMaxLength(2000);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.CaseId, item.OperationKey }).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.CreatedAtUtc });
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SendToAiControlEntity>(entity =>
        {
            entity.ToTable("SendToAiControl");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(40);
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
    }

    private static string SqlLiteral(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
