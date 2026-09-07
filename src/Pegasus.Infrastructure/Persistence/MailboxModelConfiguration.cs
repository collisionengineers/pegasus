using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class MailboxModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ApprovedInboxPollStateEntity>(entity =>
        {
            entity.ToTable("ApprovedInboxPollStates", t => t.HasCheckConstraint("CK_ApprovedInboxPollStates_Generation", "[Generation] >= 0"));
            entity.HasKey(item => item.ApprovedMailboxId);
            entity.Property(item => item.MailboxAddress).HasMaxLength(320).IsRequired();
            entity.Property(item => item.ScopeFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.LeaseToken).HasMaxLength(64);
            entity.Property(item => item.LastFailureCode).HasMaxLength(100);
            entity.HasIndex(item => item.MailboxAddress).IsUnique();
            entity.HasIndex(item => new { item.DueAtUtc, item.ApprovedMailboxId }).IsDescending(true, false);
            entity.HasOne(item => item.ApprovedMailbox)
                .WithOne()
                .HasForeignKey<ApprovedInboxPollStateEntity>(item => item.ApprovedMailboxId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApprovedInboxPoisonMessageEntity>(entity =>
        {
            entity.ToTable("ApprovedInboxPoisonMessages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OccurrenceKey).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ImmutableMessageId).IsRequired();
            entity.Property(item => item.FileName).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64);
            entity.Property(item => item.OriginalSourceHash).HasMaxLength(64);
            entity.Property(item => item.EvidenceMarker).HasMaxLength(50);
            entity.Property(item => item.StorageKey).HasMaxLength(200);
            entity.Property(item => item.FailureCode).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CursorAfterMessage).IsRequired();
            entity.HasIndex(item => new { item.ApprovedMailboxId, item.OccurrenceKey }).IsUnique();
            entity.HasIndex(item => new { item.QuarantinedAtUtc, item.Id }).IsDescending(true, false);
            entity.HasOne<ApprovedInboxPollStateEntity>()
                .WithMany()
                .HasForeignKey(item => item.ApprovedMailboxId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RetainedMailboxMessageEntity>(entity =>
        {
            entity.ToTable("RetainedMailboxMessages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MailboxAddress).HasMaxLength(320).IsRequired();
            entity.Property(item => item.FolderScope).HasMaxLength(40).IsRequired();
            entity.Property(item => item.FolderIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ImmutableMessageId).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ConversationIdentity).HasMaxLength(500);
            entity.Property(item => item.InternetMessageIdentity).HasMaxLength(500);
            entity.Property(item => item.CanonicalInternetMessageIdentity)
                .HasMaxLength(500)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(item => item.ExternalReceiptToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SenderAddress).HasMaxLength(320);
            entity.Property(item => item.SenderDisplayName).HasMaxLength(320);
            entity.Property(item => item.ToAddressesJson).IsRequired();
            entity.Property(item => item.CcAddressesJson).IsRequired();
            entity.Property(item => item.ReplyToAddressesJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired(false);
            entity.Property(item => item.Subject).HasMaxLength(1000);
            entity.Property(item => item.BodyExcerpt).HasMaxLength(400);
            entity.Property(item => item.SourceSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            // One row per message per mailbox: the poll inserts if absent and a
            // redelivery is refused here, not judged in application code.
            entity.HasIndex(item => new { item.MailboxId, item.ImmutableMessageId }).IsUnique();
            entity.HasIndex(item => new { item.MailboxId, item.CanonicalInternetMessageIdentity })
                .IsUnique()
                .HasFilter("[CanonicalInternetMessageIdentity] IS NOT NULL");
            entity.HasIndex(item => new { item.ReceivedAtUtc, item.Id }).IsDescending(true, false);
            entity.HasIndex(item => new
            {
                item.MailboxId,
                item.FolderScope,
                item.ReceivedAtUtc,
                item.Id
            }).IsDescending(false, false, true, false);
            entity.HasIndex(item => item.ConversationIdentity);
            entity.HasIndex(item => item.ExternalReceiptToken);
            entity.HasOne<ApprovedInboxPollStateEntity>()
                .WithMany()
                .HasForeignKey(item => item.MailboxId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RetainedMailboxAttachmentEntity>(entity =>
        {
            entity.ToTable("RetainedMailboxAttachments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.RetainedMailboxMessageId, item.Ordinal }).IsUnique();
            entity.HasOne(item => item.RetainedMailboxMessage)
                .WithMany(item => item.Attachments)
                .HasForeignKey(item => item.RetainedMailboxMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RetainedMailFolderMoveEntity>(entity =>
        {
            entity.ToTable("RetainedMailFolderMoves");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OperationKey).HasMaxLength(36).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.ExpectedRecommendationPolicyKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.MailboxId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ImmutableMessageId).HasMaxLength(500).IsRequired();
            entity.Property(item => item.SourceFolderId).HasMaxLength(500).IsRequired();
            entity.Property(item => item.DestinationFolderId).HasMaxLength(500).IsRequired();
            entity.Property(item => item.FolderType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ActorRolesJson).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(item => item.FailureReason).HasMaxLength(1000);
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => item.RetainedMailboxMessageId)
                .IsUnique()
                .HasFilter("[Outcome] IN ('pending', 'uncertain')");
            entity.HasIndex(item => new { item.RetainedMailboxMessageId, item.RecordedAtUtc });
            entity.HasOne(item => item.RetainedMailboxMessage)
                .WithMany()
                .HasForeignKey(item => item.RetainedMailboxMessageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApprovedSentPollStateEntity>(entity =>
        {
            entity.ToTable("ApprovedSentPollStates", t => t.HasCheckConstraint("CK_ApprovedSentPollStates_Generation", "[Generation] >= 0"));
            entity.HasKey(item => item.MailboxId);
            entity.Property(item => item.MailboxId).HasMaxLength(100);
            entity.Property(item => item.MailboxAddress).HasMaxLength(320).IsRequired();
            entity.Property(item => item.SentFolderIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ScopeFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
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
            entity.Property(item => item.CaseType).HasMaxLength(40);
            entity.Property(item => item.OtherName).HasMaxLength(200);
            entity.Property(item => item.OtherReasoning).HasMaxLength(1000);
            entity.Property(item => item.AmbiguousCandidatesJson).IsRequired();
            entity.Property(item => item.PredicatesJson).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.PolicyKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.StandaloneAuditReportAssetSourceLabel).HasMaxLength(500);
            entity.Property(item => item.StandaloneAuditReportAssessment).HasMaxLength(40);
            entity.Property(item => item.DecidedByActor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            entity.HasOne(item => item.IntakeReceipt)
                .WithOne(item => item.MailClassificationDecision)
                .HasForeignKey<IntakeMailClassificationDecisionEntity>(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IntakeMailClassificationHistoryEntity>(entity =>
        {
            entity.ToTable("IntakeMailClassificationHistory");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.BeforeJson).IsRequired();
            entity.Property(item => item.AfterJson).IsRequired();
            entity.HasIndex(item => new { item.IntakeReceiptId, item.Version }).IsUnique();
            entity.HasOne(item => item.ClassificationDecision)
                .WithMany(item => item.History)
                .HasForeignKey(item => item.IntakeReceiptId)
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
