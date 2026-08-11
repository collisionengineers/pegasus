using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class IntakeAllocationModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<IntakeAllocationAttemptEntity>(entity =>
        {
            entity.ToTable("IntakeAllocationAttempts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CaseType).HasMaxLength(40);
            entity.Property(item => item.PrincipalCode).HasMaxLength(20).IsRequired();
            entity.Property(item => item.AcceptedInspectionDeadline).HasColumnType("date");
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CommandHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.FailureKind).HasMaxLength(40);
            entity.Property(item => item.RecoveryDisposition).HasMaxLength(40);
            entity.Property(item => item.SafeReason).HasMaxLength(500);
            entity.Property(item => item.CaseReference).HasMaxLength(40);
            entity.Property(item => item.AuditReference).HasMaxLength(50);
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.IntakeReceiptId, item.AttemptNumber }).IsUnique();
            entity.HasOne(item => item.IntakeReceipt)
                .WithMany()
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
