using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One run of the mail classifier, as it records itself through
/// <c>pegasus_classifier_run_record</c> (FRD-08; R1318): the settings it
/// started with on its open, and how it ended on its close. A run with no
/// close is running or did not finish.
/// </summary>
internal sealed class ClassifierRunEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public bool DryRun { get; set; }
    public required string Model { get; set; }
    public int Batch { get; set; }
    public int ScanPages { get; set; }
    public decimal MaxBudgetUsd { get; set; }
    public required string AgentVersion { get; set; }
    public string? Outcome { get; set; }
    public string? OutcomeKind { get; set; }
    public int? Eligible { get; set; }
    public int? AwaitingDecision { get; set; }
    public bool CandidatesHidden { get; set; }
    public int HiddenRuns { get; set; }
    public int? Verdicts { get; set; }
    public int? Failed { get; set; }
    public int? Deferred { get; set; }
    public int? Acted { get; set; }
    public int? Turns { get; set; }
    public decimal? CostUsd { get; set; }
    public int? DurationMs { get; set; }
    public string? Error { get; set; }
    public string? Summary { get; set; }
    public required string RecordedBy { get; set; }
}

/// <summary>One message of a run's batch and what the run did with it, written once at the run's close.</summary>
internal sealed class ClassifierRunItemEntity
{
    public Guid RunId { get; set; }
    public int Ordinal { get; set; }
    public Guid MessageId { get; set; }
    public required string Action { get; set; }
    public string? Category { get; set; }
    public Guid? CaseId { get; set; }
    public string? CaseReference { get; set; }
    public string? Reason { get; set; }
    public string? Refusal { get; set; }
    public DateTimeOffset? OccurredAtUtc { get; set; }
}

internal static class ClassifierRunModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ClassifierRunEntity>(entity =>
        {
            entity.ToTable("ClassifierRuns");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Model).HasMaxLength(100).IsRequired();
            entity.Property(item => item.AgentVersion).HasMaxLength(40).IsRequired();
            entity.Property(item => item.MaxBudgetUsd).HasPrecision(9, 4);
            entity.Property(item => item.Outcome).HasMaxLength(100);
            entity.Property(item => item.OutcomeKind).HasMaxLength(40);
            entity.Property(item => item.CostUsd).HasPrecision(12, 6);
            entity.Property(item => item.Error).HasMaxLength(1000);
            entity.Property(item => item.Summary).HasMaxLength(4000);
            entity.Property(item => item.RecordedBy).HasMaxLength(200).IsRequired();
            // The list reads a period newest first, and the health row its newest runs.
            entity.HasIndex(item => item.StartedAtUtc);
        });

        builder.Entity<ClassifierRunItemEntity>(entity =>
        {
            entity.ToTable("ClassifierRunItems");
            entity.HasKey(item => new { item.RunId, item.Ordinal });
            entity.Property(item => item.Ordinal).ValueGeneratedNever();
            entity.Property(item => item.Action).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(60);
            entity.Property(item => item.CaseReference).HasMaxLength(40);
            entity.Property(item => item.Reason).HasMaxLength(500);
            entity.Property(item => item.Refusal).HasMaxLength(1000);
            entity.HasOne<ClassifierRunEntity>()
                .WithMany()
                .HasForeignKey(item => item.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
