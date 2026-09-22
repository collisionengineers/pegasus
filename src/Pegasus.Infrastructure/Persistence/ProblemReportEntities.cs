using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One problem report (FRD-12 Report a problem; ADR-0055): the person's words,
/// the state captured with them as JSON, and where the report went. Kept so
/// nothing a person wrote is lost when the outward post fails; an
/// Administrator retries from the list.
/// </summary>
internal sealed class ProblemReportEntity
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public required string Description { get; set; }
    public required string SnapshotJson { get; set; }
    public string? Route { get; set; }
    public string? CaseReference { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public required string Status { get; set; }
    public int? IssueNumber { get; set; }
    public string? IssueUrl { get; set; }
    public string? Failure { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public DateTimeOffset? DispatchClaimExpiresAtUtc { get; set; }
    public string? DispatchClaimToken { get; set; }
}

internal static class ProblemReportModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ProblemReportEntity>(entity =>
        {
            entity.ToTable("ProblemReports", table => table.HasCheckConstraint(
                "CK_ProblemReports_Status",
                "[Status] IN ('Sent', 'NotSent', 'Unknown')"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Description).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.SnapshotJson).IsRequired();
            entity.Property(item => item.Route).HasMaxLength(400);
            entity.Property(item => item.CaseReference).HasMaxLength(40);
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(32).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.IssueUrl).HasMaxLength(400);
            entity.Property(item => item.Failure).HasMaxLength(400);
            entity.Property(item => item.DispatchClaimToken).HasMaxLength(64);
            // The Administrator's list reads newest first.
            entity.HasIndex(item => item.CreatedAtUtc).IsDescending();
            entity.HasIndex(item => item.OperationKey).IsUnique();
        });
    }
}
