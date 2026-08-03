using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class MailboxModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ApprovedInboxPollStateEntity>(entity =>
        {
            entity.ToTable("ApprovedInboxPollStates");
            entity.HasKey(item => item.MailboxId);
            entity.Property(item => item.MailboxId).HasMaxLength(100);
            entity.Property(item => item.MailboxAddress).HasMaxLength(320).IsRequired();
            entity.Property(item => item.LeaseToken).HasMaxLength(64);
            entity.Property(item => item.LastFailureCode).HasMaxLength(100);
            entity.HasIndex(item => item.MailboxAddress).IsUnique();
            entity.HasIndex(item => new { item.DueAtUtc, item.MailboxId }).IsDescending(true, false);
        });

        builder.Entity<ApprovedInboxPoisonMessageEntity>(entity =>
        {
            entity.ToTable("ApprovedInboxPoisonMessages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MailboxId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.OccurrenceKey).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ImmutableMessageId).IsRequired();
            entity.Property(item => item.FileName).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64);
            entity.Property(item => item.OriginalSourceHash).HasMaxLength(64);
            entity.Property(item => item.EvidenceMarker).HasMaxLength(50);
            entity.Property(item => item.StorageKey).HasMaxLength(200);
            entity.Property(item => item.FailureCode).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CursorAfterMessage).IsRequired();
            entity.HasIndex(item => new { item.MailboxId, item.OccurrenceKey }).IsUnique();
            entity.HasIndex(item => new { item.QuarantinedAtUtc, item.Id }).IsDescending(true, false);
            entity.HasOne<ApprovedInboxPollStateEntity>()
                .WithMany()
                .HasForeignKey(item => item.MailboxId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApprovedSentPollStateEntity>(entity =>
        {
            entity.ToTable("ApprovedSentPollStates");
            entity.HasKey(item => item.MailboxId);
            entity.Property(item => item.MailboxId).HasMaxLength(100);
            entity.Property(item => item.MailboxAddress).HasMaxLength(320).IsRequired();
            entity.Property(item => item.SentFolderIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.LeaseToken).HasMaxLength(64);
            entity.Property(item => item.LastFailureCode).HasMaxLength(100);
            entity.HasIndex(item => item.MailboxAddress).IsUnique();
            entity.HasIndex(item => new { item.DueAtUtc, item.MailboxId }).IsDescending(true, false);
        });

        builder.Entity<ApprovedSentPollOutcomeEntity>(entity =>
        {
            entity.ToTable("ApprovedSentPollOutcomes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MailboxId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.MailboxAddress).HasMaxLength(320).IsRequired();
            entity.Property(item => item.SourceOccurrenceIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.OriginalSourceSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.ObservedSourceSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.EvidenceMarker).HasMaxLength(40);
            entity.Property(item => item.CurrentLocationIdentity).HasMaxLength(500);
            entity.Property(item => item.ObservationKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SentFolderIdentity).HasMaxLength(200);
            entity.Property(item => item.ImmutableItemIdentity).HasMaxLength(500);
            entity.Property(item => item.InternetMessageIdentity).HasMaxLength(500);
            entity.Property(item => item.ConversationIdentity).HasMaxLength(500);
            entity.Property(item => item.ReplyChainIdentity).HasMaxLength(500);
            entity.Property(item => item.MimeSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.OutcomeKind).HasMaxLength(80).IsRequired();
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.MailboxId, item.RecordedAtUtc, item.Id }).IsDescending(false, true, false);
            entity.HasIndex(item => new { item.OutcomeKind, item.RecordedAtUtc, item.Id }).IsDescending(false, true, false);
            entity.HasIndex(item => new { item.RecordedAtUtc, item.Id }).IsDescending(true, false);
            entity.HasIndex(item => item.RelatedEvidenceId);
            entity.HasOne<ApprovedSentPollStateEntity>()
                .WithMany()
                .HasForeignKey(item => item.MailboxId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeMailClassificationDecisionEntity>(entity =>
        {
            entity.ToTable("IntakeMailClassificationDecisions");
            entity.HasKey(item => item.IntakeReceiptId);
            entity.Property(item => item.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Direction).HasMaxLength(20);
            entity.Property(item => item.Family).HasMaxLength(100);
            entity.Property(item => item.Subtype).HasMaxLength(100);
            entity.Property(item => item.OtherName).HasMaxLength(200);
            entity.Property(item => item.OtherReasoning).HasMaxLength(1000);
            entity.Property(item => item.AmbiguousCandidatesJson).IsRequired();
            entity.Property(item => item.PredicatesJson).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.PolicyKey).HasMaxLength(100).IsRequired();
            entity.HasOne(item => item.IntakeReceipt)
                .WithOne(item => item.MailClassificationDecision)
                .HasForeignKey<IntakeMailClassificationDecisionEntity>(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IntakeMailRouteDecisionEntity>(entity =>
        {
            entity.ToTable("IntakeMailRouteDecisions");
            entity.HasKey(item => item.IntakeReceiptId);
            entity.Property(item => item.Disposition).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RouteOwnerCode).HasMaxLength(100);
            entity.Property(item => item.RouteKind).HasMaxLength(40);
            entity.Property(item => item.WorkProviderCode).HasMaxLength(100);
            entity.Property(item => item.PredicatesJson).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.PolicyKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.TransportIdentitiesJson).IsRequired();
            entity.Property(item => item.OriginalIdentitiesJson).IsRequired();
            entity.Property(item => item.EffectiveSenderAddress).HasMaxLength(320);
            entity.Property(item => item.EffectiveSenderSourceLabel).HasMaxLength(500);
            entity.HasOne(item => item.IntakeReceipt)
                .WithOne(item => item.MailRouteDecision)
                .HasForeignKey<IntakeMailRouteDecisionEntity>(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
