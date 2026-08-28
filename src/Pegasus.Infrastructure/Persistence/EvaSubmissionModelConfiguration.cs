using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class EvaSubmissionModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<EvaSubmissionEntity>(entity =>
        {
            entity.ToTable("EvaSubmissions", table =>
            {
                // The four outcomes FRD-07 requires stay distinct, named in
                // the database so a row can never carry a fifth.
                table.HasCheckConstraint(
                    "CK_EvaSubmissions_Outcome",
                    "[Outcome] IN ('Succeeded', 'Rejected', 'Partial', 'Unknown')");

                // IsDelivered exists to drive the unique index, so it must
                // agree with Outcome or the index guards the wrong rows. It
                // means the instruction reached EVA, which a Partial did: EVA
                // accepted it and returned no identifier, and a second send
                // would create a second claim no API call can withdraw.
                table.HasCheckConstraint(
                    "CK_EvaSubmissions_DeliveredAgreesWithOutcome",
                    "([IsDelivered] = 1 AND [Outcome] IN ('Succeeded', 'Partial')) "
                    + "OR ([IsDelivered] = 0 AND [Outcome] NOT IN ('Succeeded', 'Partial'))");

                table.HasCheckConstraint(
                    "CK_EvaSubmissions_Counts",
                    "[ImagesSent] >= 0 AND [AttemptCount] >= 1 AND [WorkflowVersion] >= 0");
            });

            entity.HasKey(item => item.Id);
            entity.Property(item => item.ExternalRef).HasMaxLength(100).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Outcome).HasMaxLength(20).IsRequired();
            entity.Property(item => item.EvaId).HasMaxLength(100);
            entity.Property(item => item.FileReference).HasMaxLength(100);
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.Property(item => item.FailureDetail).HasMaxLength(500);
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();

            // The once-per-case rule, enforced by the database rather than by
            // the code path alone. EVA has no idempotency of its own: a second
            // accepted instruction for the same case creates a second claim
            // with its own File Reference, and no API call can undo it. A
            // filtered index is what makes "at most one success, any number of
            // failures" expressible — the failures are exactly what a caller
            // needs to see to decide whether to try again.
            entity.HasIndex(item => item.CaseId)
                .IsUnique()
                .HasFilter("[IsDelivered] = 1")
                .HasDatabaseName("UX_EvaSubmissions_CaseDelivered");

            entity.HasIndex(item => new { item.CaseId, item.SubmittedAtUtc })
                .HasDatabaseName("IX_EvaSubmissions_CaseSubmittedAt");

            // The replay lookup's own index: a case's attempts under one key.
            entity.HasIndex(item => new { item.CaseId, item.OperationKey })
                .HasDatabaseName("IX_EvaSubmissions_CaseOperationKey");

            entity.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
