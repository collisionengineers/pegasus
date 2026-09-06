using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

internal static class AdministrationPolicyModelConfiguration
{
    internal const string WorkflowPolicyKey = "case-workflow";
    internal static readonly Guid InitialInstructionsMailboxId =
        Guid.Parse("49f47eb9-c5b0-464f-b8f0-8c90ba061728");

    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<WorkflowConfigurationEntity>(entity =>
        {
            entity.ToTable("WorkflowConfigurations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(100);
            entity.HasData(new WorkflowConfigurationEntity
            {
                Id = WorkflowPolicyKey,
                Version = 1
            });
        });

        builder.Entity<ApprovedMailboxEntity>(entity =>
        {
            entity.ToTable("ApprovedMailboxes", table =>
            {
                table.HasCheckConstraint("CK_ApprovedMailboxes_SendLimit", "[VerifiedEncodedMessageSizeLimit] IS NULL OR [VerifiedEncodedMessageSizeLimit] > 0");
                table.HasCheckConstraint("CK_ApprovedMailboxes_MailboxGeneration", "[MailboxGeneration] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Address).HasMaxLength(320).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.MailboxIdentity).HasMaxLength(100);
            entity.Property(item => item.InboxFolderIdentity).HasMaxLength(200);
            entity.Property(item => item.SentFolderIdentity).HasMaxLength(200);
            entity.Property(item => item.SendLimitVerifiedBy).HasMaxLength(200);
            entity.HasIndex(item => item.Address).IsUnique();
            // A supplied Graph identity is exclusive to one approved mailbox.
            entity.HasIndex(item => item.MailboxIdentity)
                .IsUnique()
                .HasFilter("[MailboxIdentity] IS NOT NULL");
            // The seeded row remains inactive until its Graph identities are saved.
            entity.HasData(new ApprovedMailboxEntity
            {
                Id = InitialInstructionsMailboxId,
                Address = "instructions@collisionengineers.co.uk",
                AllowInboundIntake = true,
                AllowSentEvidence = false,
                State = ApprovedMailboxState.Approved.ToString(),
                MailboxGeneration = 1,
                Version = 1
            });
        });

        builder.Entity<ApprovedMailboxFolderBindingEntity>(entity =>
        {
            entity.ToTable("ApprovedMailboxFolderBindings");
            entity.HasKey(item => new { item.ApprovedMailboxId, item.FolderType });
            entity.Property(item => item.FolderType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.FolderIdentity).HasMaxLength(200).IsRequired();
            entity.HasOne(item => item.ApprovedMailbox)
                .WithMany(item => item.FolderBindings)
                .HasForeignKey(item => item.ApprovedMailboxId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApprovedMailboxSubscriptionEntity>(entity =>
        {
            entity.ToTable("ApprovedMailboxSubscriptions");
            entity.HasKey(item => item.ApprovedMailboxId);
            entity.Property(item => item.SubscriptionId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Resource).HasMaxLength(500).IsRequired();
            entity.Property(item => item.LifecycleState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.LastMaintenanceFailureCode).HasMaxLength(100);
            entity.Property(item => item.Generation).HasDefaultValue(0L);
            entity.HasIndex(item => item.SubscriptionId).IsUnique();
            entity.HasIndex(item => item.ExpiresAtUtc);
            entity.HasOne(item => item.ApprovedMailbox)
                .WithOne()
                .HasForeignKey<ApprovedMailboxSubscriptionEntity>(item => item.ApprovedMailboxId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApprovedOutlookCategoryEntity>(entity =>
        {
            entity.ToTable("ApprovedOutlookCategories");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DisplayName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.NormalizedDisplayName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.HasIndex(item => item.NormalizedDisplayName).IsUnique();
        });
    }
}
