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
            entity.HasIndex(item => item.Address).IsUnique();
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
    }
}
