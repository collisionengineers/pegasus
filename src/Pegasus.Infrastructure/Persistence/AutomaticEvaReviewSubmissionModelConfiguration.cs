using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class AutomaticEvaReviewSubmissionModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<AutomaticEvaReviewSubmissionEntity>(entity =>
        {
            entity.ToTable("AutomaticEvaReviewSubmissions", table =>
                table.HasCheckConstraint(
                    "CK_AutomaticEvaReviewSubmissions_State",
                    "[State] IN ('Pending', 'Dispatching', 'Completed', 'ReconciliationRequired')"));
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.CaseId).IsUnique();
            entity.HasIndex(item => new { item.State, item.DueAtUtc });
            entity.Property(item => item.OperationKey).HasMaxLength(32);
            entity.Property(item => item.State).HasMaxLength(32);
        });
    }
}
