using System.Globalization;
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
/// Reads the Case list (MI-04). One filtered set of Cases is reused as a
/// semi-join by every read, and each read beyond the Cases themselves runs
/// only when a chosen column needs it.
/// </summary>
internal sealed class EfCaseListQueries(IDbContextFactory<PegasusDbContext> contextFactory) : ICaseListQueries
{
    private const string AgreedFeePath = "$.agreedFee";
    private const string RepairCostPath = "$.costs.totals.printed.gross";
    private const string SignatoryPath = "$.signatoryStaffId";

    public async Task<IReadOnlyList<CaseListRecord>> GetAsync(CaseListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var cases = Cases(db, query);

        var rows = await (
            from @case in cases
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
                workflow == null ? null : workflow.SignOffEngineerId))
            .ToListAsync(cancellationToken);
        var auditWorks = await db.CaseWorks.AsNoTracking()
            .Where(work => work.Kind == CaseWorkKinds.Audit && cases.Any(@case => @case.Id == work.CaseId))
            .ToDictionaryAsync(work => work.CaseId, work => work.Id, cancellationToken);

        var claimData = query.Needs.HasFlag(CaseListFacts.ClaimData) && query.ClaimFields.Count > 0
            ? await ClaimDataAsync(db, cases, query.ClaimFields, cancellationToken)
            : [];
        var assessment = query.Needs.HasFlag(CaseListFacts.Assessment) && query.AssessmentPaths.Count > 0
            ? await AssessmentAsync(db, cases, query.AssessmentPaths, cancellationToken)
            : [];
        var reports = query.Needs.HasFlag(CaseListFacts.ReportFigures)
            ? await ReportFiguresAsync(db, cases, cancellationToken)
            : [];
        var sends = query.Needs.HasFlag(CaseListFacts.Sends)
            ? await FirstSendsAsync(db, cases, cancellationToken)
            : [];
        var activity = query.Needs.HasFlag(CaseListFacts.Activity)
            ? await ActivityAsync(db, cases, rows.Select(row => row.CaseId).ToHashSet(), cancellationToken)
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
                activity.GetValueOrDefault(row.CaseId) ?? CaseListActivity.None);
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
        IQueryable<CaseEntity> cases,
        IReadOnlySet<string> fieldNames,
        CancellationToken cancellationToken)
    {
        var names = fieldNames.ToArray();
        var fields = await db.CaseDataFields.AsNoTracking()
            .Where(field => names.Contains(field.FieldName)
                && (field.ValueKind == CaseDataCodes.Confirmed || field.ValueKind == CaseDataCodes.Fact)
                && cases.Any(@case => @case.Id == field.WorkId))
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
        IQueryable<CaseEntity> cases,
        IReadOnlySet<string> assessmentPaths,
        CancellationToken cancellationToken)
    {
        var paths = assessmentPaths.ToArray();
        var fields = await db.CaseAssessmentFields.AsNoTracking()
            .Where(field => paths.Contains(field.FieldPath)
                && db.CaseWorks.Any(work => work.Id == field.WorkId && cases.Any(@case => @case.Id == work.CaseId)))
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
        IQueryable<CaseEntity> cases,
        CancellationToken cancellationToken)
    {
        var reports = await (
            from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
            join artifact in db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                on generation.Id equals artifact.GenerationId
            join version in db.Set<DocumentVersionEntity>().AsNoTracking()
                on artifact.VersionId equals (Guid?)version.Id
            where cases.Any(@case => @case.Id == generation.CaseId)
                && artifact.Kind == nameof(CaseReportArtifactKind.AssessmentReport)
                && artifact.Sha256 != null
                && artifact.Sha256 == version.Sha256
                && version.CustodyStatus == DocumentCustodyStatus.Confirmed
            select new ReportRow(
                generation.WorkId,
                generation.Id,
                generation.GeneratedAtUtc,
                SqlJson.Value(generation.SnapshotJson, AgreedFeePath),
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
                    Number(first.AgreedFee)
                        ?? throw new InvalidDataException($"The frozen snapshot of report generation '{first.GenerationId}' has no valid agreed fee."),
                    Number(latest.RepairCost),
                    Guid.TryParse(latest.SignatoryId, out var signatory) ? signatory : null);
            });
    }

    /// <summary>Each work's first observed report send and who made it (MI-01's send record).</summary>
    private static async Task<Dictionary<Guid, SendRow>> FirstSendsAsync(
        PegasusDbContext db,
        IQueryable<CaseEntity> cases,
        CancellationToken cancellationToken)
    {
        var sends = await (
            from operation in db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            join generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
                on operation.ContextId equals generation.Id
            where operation.Purpose == StaffMailPurpose.CaseReport
                && operation.State == StaffMailState.Sent
                && operation.ObservedSentAtUtc != null
                && cases.Any(@case => @case.Id == generation.CaseId)
            select new SendRow(generation.WorkId, operation.ObservedSentAtUtc!.Value, operation.ActorKind, operation.ActorSubjectId))
            .ToListAsync(cancellationToken);
        return sends
            .GroupBy(send => send.WorkId)
            .ToDictionary(group => group.Key, group => group.OrderBy(send => send.SentAtUtc).First());
    }

    private static async Task<Dictionary<Guid, CaseListActivity>> ActivityAsync(
        PegasusDbContext db,
        IQueryable<CaseEntity> cases,
        HashSet<Guid> caseIds,
        CancellationToken cancellationToken)
    {
        var documents = await (
            from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join version in db.Set<DocumentVersionEntity>().AsNoTracking() on occurrence.VersionId equals version.Id
            where !version.IsLogicallyRemoved && cases.Any(@case => @case.Id == occurrence.CaseId)
            group occurrence by occurrence.CaseId into byCase
            select new
            {
                CaseId = byCase.Key,
                Images = byCase.Count(item => item.SemanticRole == DocumentSemanticRole.Image),
                InReport = byCase.Count(item => item.SemanticRole == DocumentSemanticRole.Image && item.InReport),
                Documents = byCase.Count(item => item.SemanticRole != DocumentSemanticRole.Image)
            })
            .ToDictionaryAsync(item => item.CaseId, cancellationToken);
        var notes = await CountsAsync(
            db.CaseWorkflowEvents.AsNoTracking()
                .Where(item => item.EventType == AddCaseNote.EventType && cases.Any(@case => @case.Id == item.CaseId))
                .Select(item => item.CaseId),
            cancellationToken);
        var chases = await CountsAsync(
            db.CaseManualChases.AsNoTracking()
                .Where(item => cases.Any(@case => @case.Id == item.CaseId))
                .Select(item => item.CaseId),
            cancellationToken);
        var openTask = nameof(CaseTaskState.Open);
        var openTasks = await CountsAsync(
            db.CaseTasks.AsNoTracking()
                .Where(item => item.State == openTask && cases.Any(@case => @case.Id == item.CaseId))
                .Select(item => item.CaseId),
            cancellationToken);

        // Staff mail names its Case directly, except a report send, which
        // names the generation it delivered.
        var sent = db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .Where(item => item.State == StaffMailState.Sent);
        var correspondence = await CountsAsync(
            sent.Where(item => item.Purpose != StaffMailPurpose.CaseReport && cases.Any(@case => @case.Id == item.ContextId))
                .Select(item => item.ContextId),
            cancellationToken);
        var reportMail = await CountsAsync(
            from operation in sent
            join generation in db.Set<CaseReportGenerationEntity>().AsNoTracking() on operation.ContextId equals generation.Id
            where operation.Purpose == StaffMailPurpose.CaseReport && cases.Any(@case => @case.Id == generation.CaseId)
            select generation.CaseId,
            cancellationToken);

        // MI-01's queries: post-report mailbox mail standing linked to the
        // Case by the Inbox's own association rule.
        var mailbox = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        var postReport = MailTaxonomy.CategoryName(ReceivedMailFamily.PostReportEmails);
        var receipts = await db.IntakeReceipts.AsNoTracking()
            .Where(item => item.SourceChannel == mailbox
                && item.MailClassificationDecision != null
                && item.MailClassificationDecision.Family == postReport
                && (db.IntakeManualAssociations.Any(link => link.IntakeReceiptId == item.Id
                        && cases.Any(@case => @case.Id == link.CaseId))
                    || db.CaseIntakeLinks.Any(link => link.IntakeReceiptId == item.Id
                        && cases.Any(@case => @case.Id == link.CaseId))))
            .Select(item => new { item.Id, item.MailClassificationDecision!.Subtype })
            .ToDictionaryAsync(item => item.Id, item => item.Subtype, cancellationToken);
        var associations = await CurrentIntakeAssociations.ReadAsync(db, receipts.Keys.ToList(), cancellationToken);
        var queries = associations.Current
            .Where(pair => caseIds.Contains(pair.Value.CaseId))
            .GroupBy(pair => pair.Value.CaseId)
            .ToDictionary(group => group.Key, group => group.Select(pair => receipts[pair.Key]).ToList());

        return caseIds.ToDictionary(caseId => caseId, caseId =>
        {
            var document = documents.GetValueOrDefault(caseId);
            var caseQueries = queries.GetValueOrDefault(caseId) ?? [];
            return new CaseListActivity(
                document?.Images ?? 0,
                document?.InReport ?? 0,
                document?.Documents ?? 0,
                caseQueries.Count,
                caseQueries.Count(subtype => subtype == MailCategory.DisputeSubtype),
                caseQueries.Count(subtype => subtype == MailCategory.AmendmentRequestSubtype),
                correspondence.GetValueOrDefault(caseId) + reportMail.GetValueOrDefault(caseId),
                chases.GetValueOrDefault(caseId),
                openTasks.GetValueOrDefault(caseId),
                notes.GetValueOrDefault(caseId));
        });
    }

    private static Task<Dictionary<Guid, int>> CountsAsync(IQueryable<Guid> caseIds, CancellationToken cancellationToken) =>
        caseIds
            .GroupBy(caseId => caseId)
            .Select(group => new { CaseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CaseId, item => item.Count, cancellationToken);

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

    private static decimal? Number(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

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
        Guid? SignOffEngineerId);

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
