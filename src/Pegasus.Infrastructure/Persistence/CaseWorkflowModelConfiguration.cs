using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class CaseWorkflowModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<CaseWorkflowEntity>(entity =>
        {
            entity.ToTable("CaseWorkflows", table =>
            {
                table.HasCheckConstraint("CK_CaseWorkflows_Version", "[Version] >= 0");
                table.HasCheckConstraint("CK_CaseWorkflows_EditLeaseGeneration", "[EditLeaseGeneration] >= 0");
                table.HasCheckConstraint(
                    "CK_CaseWorkflows_ReplacementNotSelf",
                    "[ReplacementCaseId] IS NULL OR [ReplacementCaseId] <> [CaseId]");
                table.HasCheckConstraint(
                    "CK_CaseWorkflows_OriginalNotSelf",
                    "[OriginalCaseId] IS NULL OR [OriginalCaseId] <> [CaseId]");
                table.HasCheckConstraint(
                    "CK_CaseWorkflows_ArchiveMetadata",
                    "([ArchivedAtUtc] IS NULL AND [ArchivedByKind] IS NULL AND [ArchivedBySubjectId] IS NULL AND [ArchivedByRolesJson] IS NULL AND [ArchiveReason] IS NULL) OR ([ArchivedAtUtc] IS NOT NULL AND [ArchivedByKind] IS NOT NULL AND [ArchivedBySubjectId] IS NOT NULL AND [ArchivedByRolesJson] IS NOT NULL AND [ArchiveReason] IS NOT NULL AND [ArchiveReason] <> '')");
            });
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.PreHoldState).HasMaxLength(40);
            entity.Property(item => item.ClosureOutcome).HasMaxLength(40);
            entity.Property(item => item.ArchivedByKind).HasMaxLength(40);
            entity.Property(item => item.ArchivedBySubjectId).HasMaxLength(200);
            entity.Property(item => item.ArchivedByRolesJson).HasMaxLength(500);
            entity.Property(item => item.ArchiveReason).HasMaxLength(500);
            entity.Property(item => item.EditLeaseToken).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.EditLeaseTokenHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.EditLeaseRequestHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.EditLeaseHolder).HasMaxLength(200);
            entity.Property(item => item.EditLeaseHolderKind).HasMaxLength(40);
            entity.Property(item => item.EditLeaseOperationKey).HasMaxLength(100);
            entity.Property(item => item.EditLeaseGeneration).HasDefaultValue(0L);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            entity.HasIndex(item => item.ReportApprovalId).IsUnique();
            entity.HasIndex(item => item.ReportSentEvidenceId).IsUnique();
            entity.HasIndex(item => item.ReplacementCaseId).IsUnique();
            entity.HasIndex(item => item.OriginalCaseId).IsUnique();
            entity.HasOne(item => item.Case).WithOne().HasForeignKey<CaseWorkflowEntity>(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReplacementCase).WithMany().HasForeignKey(item => item.ReplacementCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OriginalCase).WithMany().HasForeignKey(item => item.OriginalCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReportApproval).WithMany().HasForeignKey(item => item.ReportApprovalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReportSentEvidence).WithMany().HasForeignKey(item => item.ReportSentEvidenceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseWorkflowEventEntity>(entity =>
        {
            entity.ToTable("CaseWorkflowEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => new { item.CaseId, item.OperationKey }).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.AfterVersion }).IsUnique();
            entity.HasOne(item => item.Workflow).WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseEditLeaseOperationEntity>(entity =>
        {
            entity.ToTable(
                "CaseEditLeaseOperations",
                table => table.HasCheckConstraint(
                    "CK_CaseEditLeaseOperations_ResultVersion",
                    "[ResultVersion] >= 0"));
            entity.HasKey(item => new { item.CaseId, item.OperationKey });
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.OperationKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ResultTokenHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).HasMaxLength(500).IsRequired();
            entity.HasOne(item => item.Workflow)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseReportApprovalEntity>(entity =>
        {
            entity.ToTable("CaseReportApprovals");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ArtifactIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ArtifactSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.ApprovedByKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ApprovedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ApprovedByRolesJson).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => new { item.CaseId, item.ArtifactIdentity, item.ArtifactSha256 }).IsUnique();
            entity.HasOne<CaseEntity>().WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseReportSentEvidenceEntity>(entity =>
        {
            entity.ToTable("CaseReportSentEvidence");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MailboxIdentity).HasMaxLength(320).IsRequired();
            entity.Property(item => item.SentFolderIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ImmutableItemIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.InternetMessageIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ConversationIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ReplyChainIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.SourceOccurrenceIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.MimeSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.DiscoveredByKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.DiscoveredBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.RetentionOperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RetentionRequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.LinkedByKind).HasMaxLength(40);
            entity.Property(item => item.LinkedBySubjectId).HasMaxLength(200);
            entity.Property(item => item.LinkedByRolesJson).HasMaxLength(500);
            entity.HasIndex(item => item.RetentionOperationKey).IsUnique();
            entity.HasIndex(item => new { item.MailboxIdentity, item.ImmutableItemIdentity }).IsUnique();
            entity.HasIndex(item => new { item.DiscoveredAtUtc, item.Id }).IsDescending(true, false);
            entity.HasOne<CaseEntity>().WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseDueWorkEntity>(entity =>
        {
            entity.ToTable("CaseDueWork", table =>
            {
                table.HasCheckConstraint("CK_CaseDueWork_Version", "[Version] >= 0");
                table.HasCheckConstraint(
                    "CK_CaseDueWork_NextChaseOrdering",
                    "([NextChaseAtUtc] IS NULL AND [NextChaseAtUtcTicks] IS NULL) OR ([NextChaseAtUtc] IS NOT NULL AND [NextChaseAtUtcTicks] IS NOT NULL)");
            });
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.MissingMaterialReason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.MostRecentChannel).HasMaxLength(100);
            entity.Property(item => item.MostRecentOutcome).HasMaxLength(500);
            entity.Property(item => item.MostRecentNote).HasMaxLength(1000);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            entity.HasIndex(item => new { item.State, item.NextChaseAtUtcTicks });
            entity.HasOne(item => item.Workflow).WithOne(item => item.DueWork).HasForeignKey<CaseDueWorkEntity>(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseManualChaseEntity>(entity =>
        {
            entity.ToTable("CaseManualChases");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Channel).HasMaxLength(100).IsRequired();
            entity.Property(item => item.TargetPartyOrAddress).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Outcome).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Note).HasMaxLength(1000);
            entity.HasIndex(item => new { item.CaseId, item.OperationKey }).IsUnique();
            entity.HasOne(item => item.DueWork).WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseTaskEntity>(entity =>
        {
            entity.ToTable("CaseTasks", table =>
            {
                table.HasCheckConstraint(
                    "CK_CaseTasks_State",
                    "[State] IN ('Open', 'Completed', 'Cancelled')");
                table.HasCheckConstraint("CK_CaseTasks_Version", "[Version] >= 0");
                table.HasCheckConstraint("CK_CaseTasks_Description", "[Description] <> ''");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            entity.HasIndex(item => new { item.CaseId, item.State });
            entity.HasIndex(item => new { item.AssigneeId, item.State });
            entity.HasOne(item => item.Workflow)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PegasusIdentityUser>()
                .WithMany()
                .HasForeignKey(item => item.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
