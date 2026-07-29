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
            entity.HasIndex(item => item.DueAtUtc);
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
