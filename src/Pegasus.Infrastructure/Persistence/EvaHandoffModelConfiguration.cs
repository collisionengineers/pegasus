using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class EvaHandoffModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<EvaFirstHandoffProxyEntity>(entity =>
        {
            // The proxy proves that Pegasus produced the package. Neither
            // check may be relaxed: EVA owns receipt and named-Engineer
            // assignment, and the row must never be able to claim either.
            entity.ToTable("EvaFirstHandoffProxies", table =>
            {
                table.HasCheckConstraint(
                    "CK_EvaFirstHandoffProxies_NoDeliveryClaim",
                    "[ClaimsExternalDelivery] = 0");
                table.HasCheckConstraint(
                    "CK_EvaFirstHandoffProxies_NoAssignmentClaim",
                    "[ClaimsEngineerAssignment] = 0");
                table.HasCheckConstraint(
                    "CK_EvaFirstHandoffProxies_ExportVersion",
                    "[LatestExportedWorkflowVersion] IS NULL OR [LatestExportedWorkflowVersion] >= 0");
            });
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.AdapterKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.AdapterVersion).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.HasOne<CaseEntity>()
                .WithOne()
                .HasForeignKey<EvaFirstHandoffProxyEntity>(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
