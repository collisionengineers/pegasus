using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class CaseWorkflowModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<CaseWorkflowEntity>(entity =>
        {
            entity.ToTable("CaseWorkflows", table => table.HasCheckConstraint("CK_CaseWorkflows_Version", "[Version] >= 0"));
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ClosureOutcome).HasMaxLength(40);
            entity.Property(item => item.EditLeaseTokenHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.EditLeaseHolder).HasMaxLength(200);
            entity.Property(item => item.EditLeaseOperationKey).HasMaxLength(100);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            entity.HasIndex(item => item.ReportApprovalId).IsUnique();
            entity.HasIndex(item => item.ReportSentEvidenceId).IsUnique();
            entity.HasOne(item => item.Case).WithOne().HasForeignKey<CaseWorkflowEntity>(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReplacementCase).WithMany().HasForeignKey(item => item.ReplacementCaseId).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(item => item.MailboxIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SentFolderIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ImmutableItemIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ConversationIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ReplyChainIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.LinkedByKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.LinkedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.LinkedByRolesJson).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => new { item.CaseId, item.ImmutableItemIdentity }).IsUnique();
            entity.HasOne<CaseEntity>().WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseDueWorkEntity>(entity =>
        {
            entity.ToTable("CaseDueWork", table => table.HasCheckConstraint("CK_CaseDueWork_Version", "[Version] >= 0"));
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.MissingMaterialReason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.MostRecentChannel).HasMaxLength(100);
            entity.Property(item => item.MostRecentOutcome).HasMaxLength(500);
            entity.Property(item => item.MostRecentNote).HasMaxLength(1000);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            entity.HasIndex(item => new { item.State, item.NextChaseAtUtc });
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
    }
}
