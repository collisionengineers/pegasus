using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads the Case list (MI-04). The filtered Cases are read once; every read
/// beyond them is limited to those Cases' ids, sent as one parameter, and runs
/// only when a chosen column needs it.
/// </summary>
internal sealed class EfCaseListQueries(IDbContextFactory<PegasusDbContext> contextFactory) : ICaseListQueries
{
    private const string RepairCostPath = "$.costs.totals.printed.gross";
    private const string SignatoryPath = "$.signatoryStaffId";

    public async Task<IReadOnlyList<CaseListRecord>> GetAsync(CaseListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await (
            from @case in Cases(db, query)
            join snapshot in db.CaseDataSnapshots.AsNoTracking() on @case.Id equals snapshot.WorkId into snapshots
            from snapshot in snapshots.DefaultIfEmpty()
            join workflow in db.CaseWorkflows.AsNoTracking() on @case.Id equals workflow.CaseId into workflows
            from workflow in workflows.DefaultIfEmpty()
            join triage in db.Triage.AsNoTracking() on @case.Id equals triage.CaseId into triages
            from triage in triages.DefaultIfEmpty()
            select new CaseRow(
                @case.Id,
                @case.Reference,
                @case.AuditReference,
                @case.Principal.Code,
                @case.Type,
                snapshot == null ? null : snapshot.OriginReceivedAtUtc,
                @case.CreatedAtUtc,
                workflow == null ? null : workflow.State,
                triage == null ? null : triage.State,
                workflow == null ? (triage == null ? null : triage.AssigneeId) : workflow.AssignedEngineerId,
                workflow == null ? null : workflow.SignOffEngineerId,
                workflow != null && workflow.HasOpenQuery))
            .ToListAsync(cancellationToken);
        // Each read below is limited to these ids, passed as one parameter
        // (EF.Parameter) so the count of Cases never meets SQL Server's cap on
        // parameters in one command.
        var caseIds = rows.Select(row => row.CaseId).ToHashSet();
        var auditWorks = await db.CaseWorks.AsNoTracking()
            .Where(work => work.Kind == CaseWorkKinds.Audit && EF.Parameter(caseIds).Contains(work.CaseId))
            .ToDictionaryAsync(work => work.CaseId, work => work.Id, cancellationToken);

        var claimData = query.Needs.HasFlag(CaseListFacts.ClaimData) && query.ClaimFields.Count > 0
            ? await ClaimDataAsync(db, caseIds, query.ClaimFields, cancellationToken)
            : [];
        var assessment = query.Needs.HasFlag(CaseListFacts.Assessment) && query.AssessmentPaths.Count > 0
            ? await AssessmentAsync(db, caseIds, query.AssessmentPaths, cancellationToken)
            : [];
        var reports = query.Needs.HasFlag(CaseListFacts.ReportFigures)
            ? await ReportFiguresAsync(db, caseIds, cancellationToken)
            : [];
        var sends = query.Needs.HasFlag(CaseListFacts.Sends)
            ? await FirstSendsAsync(db, caseIds, cancellationToken)
            : [];
        var activity = query.Needs.HasFlag(CaseListFacts.Activity)
            ? await ActivityAsync(db, caseIds, cancellationToken)
            : [];

        return [.. rows.Select(row =>
        {
            var type = CaseTypeCodes.Parse(row.Type);
            return new CaseListRecord(
                row.CaseId,
                row.Reference,
                row.AuditReference,
                row.PrincipalCode,
                type,
                // CaseDataPolicy.ReceivedDate: the origin receipt, else the Case's creation.
                LondonCalendar.DateAt(row.OriginReceivedAtUtc ?? row.CreatedAtUtc),
                row.WorkflowState is null ? null : Enum.Parse<CaseLifecycleState>(row.WorkflowState),
                row.TriageState is null ? null : EfTriageStore.ParseState(row.TriageState),
                row.AssignedEngineerId,
                row.SignOffEngineerId,
                claimData.GetValueOrDefault(row.CaseId) ?? new Dictionary<string, string>(),
                Work(row.CaseId, assessment, reports, sends),
                auditWorks.TryGetValue(row.CaseId, out var auditWorkId) ? Work(auditWorkId, assessment, reports, sends) : null,
                activity.GetValueOrDefault(row.CaseId) ?? CaseListActivity.None)
            {
                HasOpenQuery = row.HasOpenQuery
            };
        })];
    }

    private static IQueryable<CaseEntity> Cases(PegasusDbContext db, CaseListQuery query)
    {
        var cases = db.Cases.AsNoTracking();
        if (!query.IncludeTriage)
        {
            cases = cases.Where(@case => @case.Type != CaseTypeCodes.Triage);
        }
        if (query.ReceivedFrom is { } from && query.ReceivedTo is { } to)
        {
            var start = LondonCalendar.StartOfDay(from);
            var end = LondonCalendar.StartOfDay(to.AddDays(1));
            cases =
                from @case in cases
                let received = db.CaseDataSnapshots
                    .Where(snapshot => snapshot.WorkId == @case.Id)
                    .Select(snapshot => snapshot.OriginReceivedAtUtc)
                    .FirstOrDefault() ?? @case.CreatedAtUtc
                where received >= start && received < end
                select @case;
        }

        return cases;
    }

    /// <summary>The accepted value of each asked-for field on the Case's own work (<see cref="CaseDataFieldValues.Accepted"/>).</summary>
    private static async Task<Dictionary<Guid, IReadOnlyDictionary<string, string>>> ClaimDataAsync(
        PegasusDbContext db,
        HashSet<Guid> caseIds,
        IReadOnlySet<string> fieldNames,
        CancellationToken cancellationToken)
    {
        var names = fieldNames.ToArray();
        var fields = await db.CaseDataFields.AsNoTracking()
            .Where(field => names.Contains(field.FieldName)
                && (field.ValueKind == CaseDataCodes.Confirmed || field.ValueKind == CaseDataCodes.Fact)
                && EF.Parameter(caseIds).Contains(field.WorkId))
            .ToListAsync(cancellationToken);
        return fields
            .GroupBy(field => field.WorkId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var list = group.ToList();
                    return (IReadOnlyDictionary<string, string>)list
                        .Select(field => field.FieldName)
                        .Distinct(StringComparer.Ordinal)
                        .Select(name => (Name: name, Value: CaseDataFieldValues.Accepted(list, name)))
                        .Where(item => item.Value is not null)
                        .ToDictionary(item => item.Name, item => item.Value!, StringComparer.Ordinal);
                });
    }

    private static async Task<Dictionary<Guid, Dictionary<string, string>>> AssessmentAsync(
        PegasusDbContext db,
        HashSet<Guid> caseIds,
        IReadOnlySet<string> assessmentPaths,
        CancellationToken cancellationToken)
    {
        var paths = assessmentPaths.ToArray();
        var fields = await db.CaseAssessmentFields.AsNoTracking()
            .Where(field => paths.Contains(field.FieldPath)
                && db.CaseWorks.Any(work => work.Id == field.WorkId && EF.Parameter(caseIds).Contains(work.CaseId)))
            .Select(field => new { field.WorkId, field.FieldPath, field.Value })
            .ToListAsync(cancellationToken);
        return fields
            .GroupBy(field => field.WorkId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(field => field.FieldPath, field => field.Value, StringComparer.Ordinal));
    }

    /// <summary>
    /// Each work's confirmed assessment reports, read the way MI-02 confirms
    /// them (the artifact's hash matches its custody-confirmed document
    /// version). The fee comes from the first, the repair cost and signatory
    /// from the latest, each read from the frozen snapshot in the database.
    /// </summary>
    private static async Task<Dictionary<Guid, ReportFigures>> ReportFiguresAsync(
        PegasusDbContext db,
        HashSet<Guid> caseIds,
        CancellationToken cancellationToken)
    {
        var reports = await (
            from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
            join artifact in db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                on generation.Id equals artifact.GenerationId
            join version in db.Set<DocumentVersionEntity>().AsNoTracking()
                on artifact.VersionId equals (Guid?)version.Id
            where EF.Parameter(caseIds).Contains(generation.CaseId)
                && artifact.Kind == nameof(CaseReportArtifactKind.AssessmentReport)
                && artifact.Sha256 != null
                && artifact.Sha256 == version.Sha256
                && version.CustodyStatus == DocumentCustodyStatus.Confirmed
            select new ReportRow(
                generation.WorkId,
                generation.Id,
                generation.GeneratedAtUtc,
                SqlJson.Value(generation.SnapshotJson, FrozenReportSnapshot.AgreedFeePath),
                SqlJson.Value(generation.SnapshotJson, RepairCostPath),
                SqlJson.Value(generation.SnapshotJson, SignatoryPath)))
            .ToListAsync(cancellationToken);
        return reports
            .GroupBy(report => report.WorkId)
            .ToDictionary(group => group.Key, group =>
            {
                var ordered = group.OrderBy(report => report.GeneratedAtUtc).ThenBy(report => report.GenerationId).ToList();
                var first = ordered[0];
                var latest = ordered[^1];
                return new ReportFigures(
                    FrozenReportSnapshot.AgreedFee(first.GenerationId, first.AgreedFee),
                    FrozenReportSnapshot.Number(latest.RepairCost),
                    Guid.TryParse(latest.SignatoryId, out var signatory) ? signatory : null);
            });
    }

    /// <summary>
    /// Each work's first observed report send and who made it (MI-01's send
    /// record); a send that carried both works' reports is each work's send.
    /// </summary>
    private static async Task<Dictionary<Guid, SendRow>> FirstSendsAsync(
        PegasusDbContext db,
        HashSet<Guid> caseIds,
        CancellationToken cancellationToken)
    {
        var sends = await (
            from operation in db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
            where (generation.Id == operation.ContextId || generation.Id == operation.SecondContextId)
                && operation.Purpose == StaffMailPurpose.CaseReport
                && operation.State == StaffMailState.Sent
                && operation.ObservedSentAtUtc != null
                && EF.Parameter(caseIds).Contains(generation.CaseId)
            select new SendRow(generation.WorkId, operation.ObservedSentAtUtc!.Value, operation.ActorKind, operation.ActorSubjectId))
            .ToListAsync(cancellationToken);
        return sends
            .GroupBy(send => send.WorkId)
            .ToDictionary(group => group.Key, group => group.OrderBy(send => send.SentAtUtc).First());
    }

    private static async Task<Dictionary<Guid, CaseListActivity>> ActivityAsync(
        PegasusDbContext db,
        HashSet<Guid> caseIds,
        CancellationToken cancellationToken)
    {
        // One statement: each figure is a stream of one tick per counted row,
        // and the database counts the ticks per figure and Case. A report send
        // names the generation it delivered, so it joins to the Case; other
        // staff mail names its Case directly.
        var occurrences =
            from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join version in db.Set<DocumentVersionEntity>().AsNoTracking() on occurrence.VersionId equals version.Id
            where !version.IsLogicallyRemoved && EF.Parameter(caseIds).Contains(occurrence.CaseId)
            select occurrence;
        var openTask = nameof(CaseTaskState.Open);
        var sent = db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .Where(item => item.State == StaffMailState.Sent);
        // One pass over the occurrences: each row ticks Images or Documents
        // (mutually exclusive by role), and an in-report image also carries
        // InReport 1, so the Images group's sum is the in-report figure.
        var ticks = occurrences
            .Select(item => new ActivityTick
            {
                Figure = item.SemanticRole == DocumentSemanticRole.Image ? ActivityFigure.Images : ActivityFigure.Documents,
                CaseId = item.CaseId,
                InReport = item.SemanticRole == DocumentSemanticRole.Image && item.InReport ? 1 : 0
            })
            .Concat(db.CaseWorkflowEvents.AsNoTracking()
                .Where(item => item.EventType == AddCaseNote.EventType && EF.Parameter(caseIds).Contains(item.CaseId))
                .Select(item => new ActivityTick { Figure = ActivityFigure.Notes, CaseId = item.CaseId, InReport = 0 }))
            .Concat(db.CaseManualChases.AsNoTracking()
                .Where(item => EF.Parameter(caseIds).Contains(item.CaseId))
                .Select(item => new ActivityTick { Figure = ActivityFigure.Chases, CaseId = item.CaseId, InReport = 0 }))
            .Concat(db.CaseTasks.AsNoTracking()
                .Where(item => item.State == openTask && EF.Parameter(caseIds).Contains(item.CaseId))
                .Select(item => new ActivityTick { Figure = ActivityFigure.OpenTasks, CaseId = item.CaseId, InReport = 0 }))
            .Concat(sent
                .Where(item => item.Purpose != StaffMailPurpose.CaseReport && EF.Parameter(caseIds).Contains(item.ContextId))
                .Select(item => new ActivityTick { Figure = ActivityFigure.Correspondence, CaseId = item.ContextId, InReport = 0 }))
            .Concat(
                from operation in sent
                join generation in db.Set<CaseReportGenerationEntity>().AsNoTracking() on operation.ContextId equals generation.Id
                where operation.Purpose == StaffMailPurpose.CaseReport && EF.Parameter(caseIds).Contains(generation.CaseId)
                select new ActivityTick { Figure = ActivityFigure.Correspondence, CaseId = generation.CaseId, InReport = 0 });
        var counts = (await ticks
                .GroupBy(tick => new { tick.Figure, tick.CaseId })
                .Select(group => new { group.Key.Figure, group.Key.CaseId, Count = group.Count(), InReport = group.Sum(tick => tick.InReport) })
                .ToListAsync(cancellationToken))
            .ToDictionary(item => (item.Figure, item.CaseId), item => item);

        // MI-01's queries: CaseQueries rows that are open or Done, not withdrawn
        // (operator, 27 October 2026).
        var withdrawn = nameof(CaseQueryClosure.Withdrawn);
        var queries = await db.CaseQueries.AsNoTracking()
            .Where(item => EF.Parameter(caseIds).Contains(item.CaseId) && item.ClosedAs != withdrawn)
            .GroupBy(item => item.CaseId)
            .Select(group => new { CaseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CaseId, item => item.Count, cancellationToken);

        return caseIds.ToDictionary(caseId => caseId, caseId =>
        {
            int Of(int figure) => counts.TryGetValue((figure, caseId), out var tally) ? tally.Count : 0;
            return new CaseListActivity(
                Of(ActivityFigure.Images),
                counts.TryGetValue((ActivityFigure.Images, caseId), out var images) ? images.InReport : 0,
                Of(ActivityFigure.Documents),
                queries.GetValueOrDefault(caseId),
                Of(ActivityFigure.Correspondence),
                Of(ActivityFigure.Chases),
                Of(ActivityFigure.OpenTasks),
                Of(ActivityFigure.Notes));
        });
    }

    /// <summary>The figures of a Case's Activity that one set query counts together.</summary>
    private static class ActivityFigure
    {
        public const int Images = 0;
        public const int Documents = 2;
        public const int Notes = 3;
        public const int Chases = 4;
        public const int OpenTasks = 5;
        public const int Correspondence = 6;
    }

    private sealed class ActivityTick
    {
        public int Figure { get; init; }

        public Guid CaseId { get; init; }

        /// <summary>1 on an in-report image's tick, 0 on every other tick.</summary>
        public int InReport { get; init; }
    }

    private static CaseListWorkFacts Work(
        Guid workId,
        Dictionary<Guid, Dictionary<string, string>> assessment,
        Dictionary<Guid, ReportFigures> reports,
        Dictionary<Guid, SendRow> sends)
    {
        var report = reports.GetValueOrDefault(workId);
        var send = sends.GetValueOrDefault(workId);
        return new(
            assessment.GetValueOrDefault(workId) ?? new Dictionary<string, string>(),
            report?.AgreedFee,
            report?.RepairCost,
            report?.SignatoryId,
            send?.SentAtUtc,
            send?.ActorKind,
            send?.ActorSubjectId);
    }

    private sealed record CaseRow(
        Guid CaseId,
        string Reference,
        string? AuditReference,
        string PrincipalCode,
        string Type,
        DateTimeOffset? OriginReceivedAtUtc,
        DateTimeOffset CreatedAtUtc,
        string? WorkflowState,
        string? TriageState,
        Guid? AssignedEngineerId,
        Guid? SignOffEngineerId,
        bool HasOpenQuery);

    private sealed record ReportRow(
        Guid WorkId,
        Guid GenerationId,
        DateTimeOffset GeneratedAtUtc,
        string? AgreedFee,
        string? RepairCost,
        string? SignatoryId);

    private sealed record ReportFigures(decimal AgreedFee, decimal? RepairCost, Guid? SignatoryId);

    private sealed record SendRow(
        Guid WorkId,
        DateTimeOffset SentAtUtc,
        Pegasus.Core.Identity.ActorKind ActorKind,
        string ActorSubjectId);
}
