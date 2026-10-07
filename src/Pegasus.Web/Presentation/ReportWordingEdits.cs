using Pegasus.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Cases;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The report's wording blocks as the Case save reads them (v28 P30), for
/// both callers of the Report section's wording: the Case page's Save and the
/// Automation actor's wording tools (ADR-0064). The blocks compose from the
/// work's report snapshot as it would generate today; a heading, wording or
/// order a caller submits that reads the same as the composed one is no
/// change, so the block keeps tracking its fields. The composed sentence is
/// read here rather than posted back, so a caller cannot claim one it never
/// saw.
/// </summary>
public static class ReportWordingEdits
{
    public const string Unavailable = "The report wording is unavailable. Refresh the Case and retry.";

    /// <summary>
    /// The work's wording state: the snapshot the blocks compose from and
    /// every block the Report section offers. A work whose report cannot yet
    /// be projected has neither.
    /// </summary>
    public static (AssessmentReportSnapshot? Snapshot, IReadOnlyList<ReportWordingBlock> Offered) Offered(
        AssessmentReportProjectionInput projection,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var projected = AssessmentReportProjection.Project(projection with
        {
            ReportDate = LondonCalendar.DateAt(now),
        });
        return projected.Snapshot is { } snapshot
            ? (snapshot, ReportWordingComposition.Offered(snapshot, projection.Wording ?? []))
            : (null, []);
    }

    /// <summary>
    /// Reads the work's wording state for a save, refusing when the report
    /// cannot be projected, since there is then no composed sentence to
    /// compare a submitted one with.
    /// </summary>
    public static async Task<ReportWordingState> ReadAsync(
        ICaseReportSnapshotSource source,
        Guid caseId,
        ActionActor actor,
        CaseWorkSelector work,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var inputs = await source.GetAsync(caseId, actor, work, reuse: null, cancellationToken)
            ?? throw new InvalidOperationException(Unavailable);
        var (snapshot, offered) = Offered(inputs.Projection, now);
        return snapshot is null
            ? throw new InvalidOperationException(Unavailable)
            : new(snapshot, snapshot.Presentation(), offered);
    }

    /// <summary>
    /// One submitted block as the Case save stores it: a heading or wording
    /// matching the composed one, and a standard block's standard place, are
    /// no change. Line breaks are stored as the composed sentences write them.
    /// </summary>
    public static CaseReportWording ToRecord(
        string key,
        string? title,
        string? text,
        int? order,
        bool included,
        bool manual,
        AssessmentReportSnapshot? snapshot,
        AssessmentReportPresentation? presentation)
    {
        ArgumentNullException.ThrowIfNull(key);
        var storedTitle = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        var storedText = string.IsNullOrWhiteSpace(text)
            ? null
            : ReportWordingComposition.LineBreaks(text.Trim());
        if (!manual && snapshot is not null && presentation is not null)
        {
            if (storedTitle is not null
                && string.Equals(storedTitle, ReportWordingComposition.StandardTitle(key, presentation), StringComparison.Ordinal))
            {
                storedTitle = null;
            }
            if (storedText is not null && string.Equals(
                storedText,
                ReportWordingComposition.ComposedText(key, snapshot, presentation).Trim(),
                StringComparison.Ordinal))
            {
                storedText = null;
            }
        }
        var storedOrder = !manual && order == ReportWordingComposition.StandardIndex(key) ? null : order;
        return new(key, storedTitle, storedText, storedOrder, included, manual);
    }
}

/// <summary>The snapshot a work's wording composes from, its presentation, and the blocks offered.</summary>
public sealed record ReportWordingState(
    AssessmentReportSnapshot Snapshot,
    AssessmentReportPresentation Presentation,
    IReadOnlyList<ReportWordingBlock> Offered);
