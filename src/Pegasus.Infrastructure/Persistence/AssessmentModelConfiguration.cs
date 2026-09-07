using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.AiWork;

namespace Pegasus.Infrastructure.Persistence;

internal static class AssessmentModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<CaseAssessmentFieldEntity>(entity =>
        {
            entity.ToTable("CaseAssessmentFields", table =>
            {
                table.HasCheckConstraint(
                    "CK_CaseAssessmentFields_FieldPath",
                    "[FieldPath] <> '' AND LEN([FieldPath]) <= 60");
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
                table.HasCheckConstraint(
                    "CK_CaseEstimateLines_Quantity",
                    "[Quantity] IS NULL OR [Quantity] > 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.LineType).HasMaxLength(20).IsRequired();
            entity.Property(item => item.GuideCode).HasMaxLength(50);
            entity.Property(item => item.Description).HasMaxLength(300);
            entity.Property(item => item.WorkUnits).HasPrecision(18, 6);
            entity.Property(item => item.PaintWorkUnits).HasPrecision(18, 6);
            entity.Property(item => item.Materials).HasPrecision(18, 2);
            entity.Property(item => item.Price).HasPrecision(18, 2);
            entity.Property(item => item.PartNumber).HasMaxLength(100);
            entity.Property(item => item.Betterment).HasMaxLength(100);
            entity.Property(item => item.Status).HasMaxLength(20);
            entity.Property(item => item.EvidenceLabel).HasMaxLength(20);
            entity.Property(item => item.Justification).HasMaxLength(500);
            entity.Property(item => item.Operation).HasMaxLength(200);
            entity.Property(item => item.SourceDocumentIdentity).HasMaxLength(200);
            entity.Property(item => item.SourceDocumentSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.SourceRowIdentity).HasMaxLength(200);
            entity.Property(item => item.AmendedBy).HasMaxLength(200);
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
                    + "([State] = 'Draft' AND [AcceptedBy] IS NULL AND [AcceptedAtUtc] IS NULL) OR "
                    + "([State] = 'Discarded' AND [DiscardedBy] IS NOT NULL AND [DiscardedAtUtc] IS NOT NULL AND [DiscardReason] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_CaseRepairSpecifications_Current",
                    "[IsCurrent] = 0 OR [State] = 'Accepted'");
                table.HasCheckConstraint(
                    "CK_CaseRepairSpecifications_VatPercent",
                    "[VatPercent] BETWEEN 0 AND 100");
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
            entity.Property(item => item.Name).HasMaxLength(EstimatePolicy.MaximumNameLength).IsRequired();
            entity.Property(item => item.LabourRate).HasPrecision(18, 2);
            entity.Property(item => item.PaintMaterials).HasPrecision(18, 2);
            entity.Property(item => item.OtherCosts).HasPrecision(18, 2);
            entity.Property(item => item.PartsDiscountPercent).HasPrecision(7, 4);
            entity.Property(item => item.MaterialsDiscountPercent).HasPrecision(7, 4);
            entity.Property(item => item.SpecialistDiscountPercent).HasPrecision(7, 4);
            entity.Property(item => item.OverallDiscountPercent).HasPrecision(7, 4);
            entity.Property(item => item.RepairerVatStatus).HasMaxLength(20);
            entity.Property(item => item.VatOverrideReason).HasMaxLength(500);
            entity.Property(item => item.VatPercent).HasPrecision(5, 2);
            entity.Property(item => item.Notes).HasMaxLength(EstimatePolicy.MaximumNotesLength);
            entity.Property(item => item.DiscardedBy).HasMaxLength(200);
            entity.Property(item => item.DiscardReason).HasMaxLength(500);
            entity.Property(item => item.LastOperationKey).HasMaxLength(100);
            entity.HasIndex(item => new { item.CaseId, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.CreationOperationKey }).IsUnique();
            entity.HasIndex(item => item.CaseId)
                .IsUnique()
                .HasFilter("[IsCurrent] = 1");
            entity.HasIndex(item => item.AiJobId);
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseValuationEntity>(entity =>
        {
            var sources = string.Join(
                ", ",
                Enum.GetValues<ValuationSource>().Select(item => SqlLiteral(item.ToString())));
            entity.ToTable("CaseValuations", table =>
            {
                table.HasCheckConstraint(
                    "CK_CaseValuations_Source",
                    $"[Source] IN ({sources})");
                table.HasCheckConstraint(
                    "CK_CaseValuations_Mileage",
                    "[Mileage] >= 0");
                table.HasCheckConstraint(
                    "CK_CaseValuations_RetailValue",
                    "[RetailValue] >= 0");
                table.HasCheckConstraint(
                    "CK_CaseValuations_TradeValue",
                    "[TradeValue] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Source).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Date).HasColumnType("date");
            entity.Property(item => item.Time).HasColumnType("time");
            entity.Property(item => item.GuideMonth).HasColumnType("date");
            entity.Property(item => item.RetailValue).HasPrecision(18, 2);
            entity.Property(item => item.TradeValue).HasPrecision(18, 2);
            entity.Property(item => item.RecordedBy).HasMaxLength(200).IsRequired();
            entity.Property(item => item.LastEditedBy).HasMaxLength(200);
            entity.HasIndex(item => new { item.CaseId, item.Date, item.Time });
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

        builder.Entity<AiJobEntity>(entity =>
        {
            var states = string.Join(", ", Enum.GetNames<AiJobState>().Select(SqlLiteral));
            var kinds = string.Join(", ", Enum.GetNames<AiJobKind>().Select(SqlLiteral));
            var subjects = string.Join(", ", Enum.GetNames<AiJobSubjectKind>().Select(SqlLiteral));
            var results = string.Join(", ", Enum.GetNames<AiJobResultKind>().Select(SqlLiteral));
            entity.ToTable("AiJobs", table =>
            {
                table.HasCheckConstraint("CK_AiJobs_State", $"[State] IN ({states})");
                table.HasCheckConstraint("CK_AiJobs_Kind", $"[Kind] IN ({kinds})");
                table.HasCheckConstraint("CK_AiJobs_SubjectKind", $"[SubjectKind] IN ({subjects})");
                table.HasCheckConstraint(
                    "CK_AiJobs_ResultKind",
                    $"[ResultKind] IS NULL OR [ResultKind] IN ({results})");
                table.HasCheckConstraint(
                    "CK_AiJobs_TargetPercent",
                    "[TargetPercentOfEngineerValue] IS NULL OR [TargetPercentOfEngineerValue] BETWEEN 1 AND 100");
                table.HasCheckConstraint(
                    "CK_AiJobs_MarketResearchResult",
                    "([ResultKind] = 'MarketResearch' "
                    + "AND [MarketResearchDocumentOccurrenceId] IS NOT NULL "
                    + "AND [MarketResearchDocumentVersionId] IS NOT NULL "
                    + "AND [MarketResearchValuationId] IS NOT NULL "
                    + "AND [MarketResearchRecordedDate] IS NOT NULL "
                    + "AND [MarketResearchRecordedTime] IS NOT NULL "
                    + "AND [MarketResearchMileage] IS NOT NULL AND [MarketResearchMileage] >= 0 "
                    + "AND [MarketResearchRetailValue] IS NOT NULL AND [MarketResearchRetailValue] >= 0 "
                    + "AND [MarketResearchTradeValue] IS NOT NULL AND [MarketResearchTradeValue] >= 0 "
                    + "AND [MarketResearchCompletionHash] IS NOT NULL) OR "
                    + "(([ResultKind] IS NULL OR [ResultKind] <> 'MarketResearch') "
                    + "AND [MarketResearchDocumentOccurrenceId] IS NULL "
                    + "AND [MarketResearchDocumentVersionId] IS NULL "
                    + "AND [MarketResearchValuationId] IS NULL "
                    + "AND [MarketResearchRecordedDate] IS NULL "
                    + "AND [MarketResearchRecordedTime] IS NULL "
                    + "AND [MarketResearchMileage] IS NULL "
                    + "AND [MarketResearchRetailValue] IS NULL "
                    + "AND [MarketResearchTradeValue] IS NULL "
                    + "AND [MarketResearchCompletionHash] IS NULL)");
            });
            entity.HasKey(item => item.JobId);
            entity.Property(item => item.JobId).ValueGeneratedNever();
            entity.Property(item => item.Kind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SubjectKind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.SubjectReference).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Instruction).HasMaxLength(500).IsRequired();
            entity.Property(item => item.EngineerValueAtSend).HasPrecision(18, 2);
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.CreatedByKind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CreatedBy).HasMaxLength(200).IsRequired();
            entity.Property(item => item.TakenBy).HasMaxLength(200);
            entity.Property(item => item.ProgressNote).HasMaxLength(500);
            entity.Property(item => item.ResultKind).HasMaxLength(40);
            entity.Property(item => item.ResultReference).HasMaxLength(200);
            entity.Property(item => item.ResultText).HasMaxLength(4000);
            entity.Property(item => item.MarketResearchRecordedDate).HasColumnType("date");
            entity.Property(item => item.MarketResearchRecordedTime).HasColumnType("time");
            entity.Property(item => item.MarketResearchRetailValue).HasPrecision(18, 2);
            entity.Property(item => item.MarketResearchTradeValue).HasPrecision(18, 2);
            entity.Property(item => item.MarketResearchCompletionHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.ClosureReason).HasMaxLength(500);
            entity.Property(item => item.LastOperationKey).HasMaxLength(100);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.State, item.LeaseExpiresAtUtc });
            entity.HasIndex(item => item.SubjectId);
            entity.HasIndex(item => item.CreatedAtUtc);
            entity.HasIndex(item => item.MarketResearchDocumentOccurrenceId);
            entity.HasIndex(item => item.MarketResearchValuationId);
        });

        builder.Entity<SendToAiControlEntity>(entity =>
        {
            entity.ToTable("SendToAiControl");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(40);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ChannelBaseUrl).HasMaxLength(200);
            entity.Property(item => item.ChannelTokenProtected).HasMaxLength(2000);
        });
    }

    private static string SqlLiteral(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
