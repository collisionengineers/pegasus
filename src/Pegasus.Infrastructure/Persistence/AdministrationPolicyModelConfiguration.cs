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
                RequireCompleteInstructionsBeforeEngineerAssignment = true,
                RequireCompleteImagesBeforeEngineerAssignment = true,
                RequireStaffInstructionReviewBeforeEngineerAssignment = true,
                RequireStaffImageReviewBeforeEngineerAssignment = true,
                Version = 1
            });
        });

        builder.Entity<ApprovedMailboxEntity>(entity =>
        {
            entity.ToTable("ApprovedMailboxes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Address).HasMaxLength(320).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.MailboxIdentity).HasMaxLength(100);
            entity.Property(item => item.InboxFolderIdentity).HasMaxLength(200);
            entity.Property(item => item.SentFolderIdentity).HasMaxLength(200);
            entity.HasIndex(item => item.Address).IsUnique();
            // Two rows may await their identities, but a supplied identity is exclusive:
            // it becomes the ApprovedInboxPollStates key, so an alias would share a cursor.
            entity.HasIndex(item => item.MailboxIdentity)
                .IsUnique()
                .HasFilter("[MailboxIdentity] IS NOT NULL");
            // The seeded production row keeps NULL identities: the real Graph identities
            // are deployment configuration, not repository content. The read-only
            // configuration fallback supplies them until an administrator saves them.
            entity.HasData(new ApprovedMailboxEntity
            {
                Id = InitialInstructionsMailboxId,
                Address = "instructions@collisionengineers.co.uk",
                AllowInboundIntake = true,
                AllowSentEvidence = false,
                State = ApprovedMailboxState.Approved.ToString(),
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
