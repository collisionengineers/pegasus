using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Pegasus.Core;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads report activity from its durable facts. A generated report is an
/// artifact whose immutable document version is confirmed and hash-matched;
/// a mail count is the staff mail operation observed in Sent state.
/// </summary>
internal sealed class EfV1ActivityReportQueries(
    IDbContextFactory<PegasusDbContext> factory) : IV1ActivityReportQueries
{
    public async Task<IReadOnlyList<PrincipalReportActivity>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifacts = await (
            from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
            join artifact in db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                on generation.Id equals artifact.GenerationId
            join documentVersion in db.Set<DocumentVersionEntity>().AsNoTracking()
                on artifact.VersionId equals (Guid?)documentVersion.Id into documentVersions
            from documentVersion in documentVersions.DefaultIfEmpty()
            join @case in db.Cases.AsNoTracking() on generation.CaseId equals @case.Id
            join work in db.CaseWorks.AsNoTracking() on generation.WorkId equals work.Id
            where generation.GeneratedAtUtc >= fromUtc && generation.GeneratedAtUtc < toUtc
            select new ArtifactRow(
                @case.PrincipalId,
                @case.Principal.Code,
                @case.Id,
                @case.OriginIntakeReceiptId,
                generation.Id,
                generation.GeneratedAtUtc,
                artifact.Kind,
                generation.SnapshotJson,
                artifact.VersionId,
                artifact.Sha256,
                documentVersion == null ? null : documentVersion.Sha256,
                documentVersion == null ? null : documentVersion.CustodyStatus,
                generation.WorkId,
                @case.Type,
                work.Kind))
            .ToListAsync(cancellationToken);

        var readyEvents = (await db.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateType == "case"
                    && item.EventKind == "case_report_generation_ready"
                    && item.Outcome == "Succeeded"
                    && item.OccurredAtUtc >= fromUtc
                    && item.OccurredAtUtc < toUtc)
                .Select(item => new ReadyEventRow(
                    item.AggregateId,
                    item.OccurredAtUtc,
                    item.AfterJson))
                .ToListAsync(cancellationToken))
            .Select(TryReadReadyAction)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
        var readyGenerationIds = readyEvents
            .Select(item => item.GenerationId)
            .Distinct()
            .ToArray();
        List<ReadyGenerationRow> readyGenerations = readyGenerationIds.Length == 0
            ? []
            : await (
                from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
                join @case in db.Cases.AsNoTracking() on generation.CaseId equals @case.Id
                where readyGenerationIds.Contains(generation.Id)
                select new ReadyGenerationRow(
                    generation.Id,
                    @case.Id,
                    @case.PrincipalId,
                    @case.Principal.Code,
                    @case.OriginIntakeReceiptId))
            .ToListAsync(cancellationToken);
        var readyByGeneration = readyGenerations.ToDictionary(item => item.GenerationId);
        var readyTransitions = readyEvents
            .Where(item => readyByGeneration.TryGetValue(item.GenerationId, out var generation)
                && generation.CaseId == item.CaseId)
            .Select(item =>
            {
                var generation = readyByGeneration[item.GenerationId];
                return new ReadyTransition(
                    generation.PrincipalId,
                    generation.Code,
                    generation.OriginIntakeReceiptId,
                    item.OccurredAtUtc);
            })
            .ToList();

        var sent = await db.Set<StaffMailSendOperationEntity>()
            .AsNoTracking()
            .Where(x => x.Purpose == StaffMailPurpose.CaseReport
                && x.State == StaffMailState.Sent
                && x.ObservedSentAtUtc >= fromUtc
                && x.ObservedSentAtUtc < toUtc)
            .Join(db.Set<CaseReportGenerationEntity>().AsNoTracking(),
                operation => operation.ContextId,
                generation => generation.Id,
                (operation, generation) => new { operation, generation })
            .Join(db.CaseWorks.AsNoTracking(), x => x.generation.WorkId, work => work.Id,
                (x, work) => new { x.operation, x.generation, WorkKind = work.Kind })
            .Join(db.Cases.AsNoTracking(), x => x.generation.CaseId, @case => @case.Id,
                (x, @case) => new SentRow(
                    @case.PrincipalId,
                    @case.Principal.Code,
                    @case.OriginIntakeReceiptId,
                    x.operation.ObservedSentAtUtc!.Value,
                    x.operation.ActorSubjectId,
                    @case.Type,
                    x.WorkKind))
            .ToListAsync(cancellationToken);

        // MI-02: the agreed fee is frozen in each work's first confirmed
        // AssessmentReport generation globally (an Inspection + Audit Case's
        // Audit carries its own fee), and attributed only to the exact
        // selected period containing that first report.
        var confirmedWorkIds = artifacts
            .Where(IsConfirmed)
            .Select(x => x.WorkId)
            .Distinct()
            .ToArray();
        var firstReports = confirmedWorkIds.Length == 0
            ? []
            : await (
                from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
                join artifact in db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                    on generation.Id equals artifact.GenerationId
                join documentVersion in db.Set<DocumentVersionEntity>().AsNoTracking()
                    on artifact.VersionId equals (Guid?)documentVersion.Id
                join @case in db.Cases.AsNoTracking() on generation.CaseId equals @case.Id
                join work in db.CaseWorks.AsNoTracking() on generation.WorkId equals work.Id
                where confirmedWorkIds.Contains(generation.WorkId)
                    && artifact.Kind == nameof(CaseReportArtifactKind.AssessmentReport)
                    && artifact.Sha256 != null
                    && artifact.Sha256 == documentVersion.Sha256
                    && documentVersion.CustodyStatus == DocumentCustodyStatus.Confirmed
                select new ArtifactRow(
                    @case.PrincipalId,
                    @case.Principal.Code,
                    @case.Id,
                    @case.OriginIntakeReceiptId,
                    generation.Id,
                    generation.GeneratedAtUtc,
                    artifact.Kind,
                    generation.SnapshotJson,
                    artifact.VersionId,
                    artifact.Sha256,
                    documentVersion.Sha256,
                    documentVersion.CustodyStatus,
                    generation.WorkId,
                    @case.Type,
                    work.Kind))
            .ToListAsync(cancellationToken);
        var agreedFees = firstReports
            .GroupBy(x => x.WorkId)
            .Select(group => group
                .OrderBy(x => x.GeneratedAtUtc)
                .ThenBy(x => x.GenerationId)
                .First())
            .Where(first => FirstReportFeeAttribution.InPeriod(first.GeneratedAtUtc, fromUtc, toUtc))
            .ToDictionary(first => first.WorkId, first => FrozenFeeOf(first));

        var receiptIds = artifacts.Select(x => x.OriginIntakeReceiptId)
            .Concat(readyTransitions.Select(x => x.OriginIntakeReceiptId))
            .Concat(sent.Select(x => x.OriginIntakeReceiptId))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var received = receiptIds.Length == 0
            ? new Dictionary<Guid, DateTimeOffset>()
            : await db.IntakeReceipts.AsNoTracking()
                .Where(x => receiptIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.ReceivedAtUtc, cancellationToken);

        // Triage's three non-terminal persisted states are its current queue;
        // CreatedAtUtc is the durable age anchor, never inferred from history.
        // A Triage Case's Principal is its Case's.
        var triage = await db.Set<TriageEntity>().AsNoTracking()
            .Where(x => x.State != "completed" && x.State != "cancelled")
            .Select(x => new TriageRow(x.Case.PrincipalId, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var held = await (
            from workflow in db.CaseWorkflows.AsNoTracking()
            join @case in db.Cases.AsNoTracking() on workflow.CaseId equals @case.Id
            where workflow.State == nameof(CaseLifecycleState.Held)
            select new HeldRow(
                @case.PrincipalId,
                db.CaseWorkflowEvents.AsNoTracking()
                    .Where(eventRow => eventRow.CaseId == workflow.CaseId
                        && eventRow.EventType == "case_held")
                    .OrderByDescending(eventRow => eventRow.OccurredAtUtc)
                    .Select(eventRow => (DateTimeOffset?)eventRow.OccurredAtUtc)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
        var operationalPrincipalIds = triage.Select(x => x.PrincipalId)
            .Concat(held.Select(x => x.PrincipalId))
            .Distinct()
            .ToArray();
        var operationalPrincipalCodes = operationalPrincipalIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.Principals.AsNoTracking()
                .Where(principal => operationalPrincipalIds.Contains(principal.Id))
                .ToDictionaryAsync(principal => principal.Id, principal => principal.Code, cancellationToken);

        return artifacts.Select(x => new PrincipalKey(x.PrincipalId, x.Code))
            .Concat(readyTransitions.Select(x => new PrincipalKey(x.PrincipalId, x.Code)))
            .Concat(sent.Select(x => new PrincipalKey(x.PrincipalId, x.Code)))
            .Concat(triage.Select(row => new PrincipalKey(
                row.PrincipalId,
                operationalPrincipalCodes[row.PrincipalId])))
            .Concat(held.Select(row => new PrincipalKey(
                row.PrincipalId,
                operationalPrincipalCodes[row.PrincipalId])))
            .Distinct()
            .Select(key => BuildRow(
                key,
                artifacts.Where(x => x.PrincipalId == key.PrincipalId).ToList(),
                readyTransitions,
                sent.Where(x => x.PrincipalId == key.PrincipalId).ToList(),
                triage.Where(x => x.PrincipalId == key.PrincipalId).ToList(),
                held.Where(x => x.PrincipalId == key.PrincipalId).ToList(),
                received,
                agreedFees))
            .OrderBy(x => x.PrincipalCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PrincipalId)
            .ToList();
    }

    private static PrincipalReportActivity BuildRow(
        PrincipalKey key,
        List<ArtifactRow> artifacts,
        List<ReadyTransition> allReadyTransitions,
        List<SentRow> sent,
        List<TriageRow> triage,
        List<HeldRow> held,
        Dictionary<Guid, DateTimeOffset> received,
        Dictionary<Guid, decimal> agreedFees)
    {
        var confirmed = artifacts.Where(IsConfirmed).ToList();
        var generatedArtifactDurations = confirmed
            .Where(x => x.OriginIntakeReceiptId is { } receiptId && received.ContainsKey(receiptId))
            .Select(x => x.GeneratedAtUtc - received[x.OriginIntakeReceiptId!.Value])
            .ToList();
        var generationDurations = confirmed
            .GroupBy(x => x.GenerationId)
            .Select(group => group.First())
            .Where(x => x.OriginIntakeReceiptId is { } receiptId && received.ContainsKey(receiptId))
            .Select(x => x.GeneratedAtUtc - received[x.OriginIntakeReceiptId!.Value])
            .ToList();
        var readyTransitions = allReadyTransitions
            .Where(transition => transition.PrincipalId == key.PrincipalId)
            .ToList();
        var readyDurations = readyTransitions
            .Where(transition => transition.OriginIntakeReceiptId is { } receiptId && received.ContainsKey(receiptId))
            .Select(transition => transition.OccurredAtUtc
                - received[transition.OriginIntakeReceiptId!.Value])
            .ToList();
        var sentDurations = sent
            .Where(x => x.OriginIntakeReceiptId is { } receiptId && received.ContainsKey(receiptId))
            .Select(x => x.ObservedSentAtUtc - received[x.OriginIntakeReceiptId!.Value])
            .ToList();
        var types = artifacts.GroupBy(x => x.Kind, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new PrincipalReportArtifactTypeActivity(
                x.Key,
                x.Count(IsConfirmed),
                x.Count() - x.Count(IsConfirmed)))
            .ToArray();
        // Each work with a confirmed report whose first report falls in the
        // period contributes its frozen fee once.
        var feeWorks = confirmed
            .GroupBy(x => x.WorkId)
            .Select(group => group.First())
            .Where(x => agreedFees.ContainsKey(x.WorkId))
            .ToList();
        return new(
            key.PrincipalId,
            key.Code,
            confirmed.Select(x => x.GenerationId).Distinct().Count(),
            confirmed.Count,
            sent.Count,
            readyTransitions.Count,
            confirmed.Count - generatedArtifactDurations.Count,
            readyTransitions.Count - readyDurations.Count,
            sent.Count - sentDurations.Count,
            sent.Count(x => string.IsNullOrWhiteSpace(x.ActorSubjectId)),
            Average(generationDurations),
            Average(generatedArtifactDurations),
            Average(readyDurations),
            Average(sentDurations),
            triage.Count,
            triage.Select(x => (DateTimeOffset?)x.CreatedAtUtc).Min(),
            held.Count,
            held.Select(x => x.HeldAtUtc).Min(),
            held.Count(x => x.HeldAtUtc is null),
            types,
            feeWorks.Sum(x => agreedFees[x.WorkId]),
            confirmed.Count(x => x.Kind == nameof(CaseReportArtifactKind.AssessmentReport) && x.IsAudit),
            sent.Count(x => x.IsAudit),
            feeWorks.Where(x => x.IsAudit).Sum(x => agreedFees[x.WorkId]));
    }

    private static decimal FrozenFeeOf(ArtifactRow report)
    {
        try
        {
            using var document = JsonDocument.Parse(report.SnapshotJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("agreedFee", out var fee)
                || fee.ValueKind != JsonValueKind.Number
                || !fee.TryGetDecimal(out _))
            {
                throw new InvalidDataException(
                    $"The frozen snapshot of report generation '{report.GenerationId}' has no valid agreed fee.");
            }

            return fee.GetDecimal();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The frozen snapshot of report generation '{report.GenerationId}' is unreadable.",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException(
                $"The frozen snapshot of report generation '{report.GenerationId}' is unreadable.",
                exception);
        }
    }

    private static bool IsConfirmed(ArtifactRow artifact) =>
        artifact.VersionId is not null
        && artifact.ArtifactSha256 is not null
        && artifact.ArtifactSha256 == artifact.VersionSha256
        && artifact.CustodyStatus == DocumentCustodyStatus.Confirmed;

    private static TimeSpan? Average(List<TimeSpan> durations) => durations.Count == 0
        ? null
        : TimeSpan.FromTicks((long)durations.Average(x => x.Ticks));

    private static ReadyAction? TryReadReadyAction(ReadyEventRow row)
    {
        if (!Guid.TryParse(row.AggregateId, out var caseId)
            || string.IsNullOrWhiteSpace(row.AfterJson))
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(row.AfterJson);
            if (!payload.RootElement.TryGetProperty("generationId", out var generation)
                || !Guid.TryParse(generation.GetString(), out var generationId))
            {
                return null;
            }

            return new(caseId, generationId, row.OccurredAtUtc);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record PrincipalKey(Guid PrincipalId, string Code);
    private sealed record ArtifactRow(
        Guid PrincipalId,
        string Code,
        Guid CaseId,
        Guid? OriginIntakeReceiptId,
        Guid GenerationId,
        DateTimeOffset GeneratedAtUtc,
        string Kind,
        string SnapshotJson,
        Guid? VersionId,
        string? ArtifactSha256,
        string? VersionSha256,
        DocumentCustodyStatus? CustodyStatus,
        Guid WorkId,
        string CaseType,
        string WorkKind)
    {
        /// <summary>MI-02's split: an Audit report (<see cref="CaseWorkKinds.IsAuditReport"/>) or an Inspection report.</summary>
        public bool IsAudit => CaseWorkKinds.IsAuditReport(CaseType, WorkKind);
    }
    private sealed record ReadyEventRow(
        string AggregateId,
        DateTimeOffset OccurredAtUtc,
        string? AfterJson);
    private sealed record ReadyGenerationRow(
        Guid GenerationId,
        Guid CaseId,
        Guid PrincipalId,
        string Code,
        Guid? OriginIntakeReceiptId);
    private sealed record ReadyAction(
        Guid CaseId,
        Guid GenerationId,
        DateTimeOffset OccurredAtUtc);
    private sealed record ReadyTransition(
        Guid PrincipalId,
        string Code,
        Guid? OriginIntakeReceiptId,
        DateTimeOffset OccurredAtUtc);
    private sealed record SentRow(
        Guid PrincipalId,
        string Code,
        Guid? OriginIntakeReceiptId,
        DateTimeOffset ObservedSentAtUtc,
        string ActorSubjectId,
        string CaseType,
        string WorkKind)
    {
        public bool IsAudit => CaseWorkKinds.IsAuditReport(CaseType, WorkKind);
    }
    private sealed record TriageRow(Guid PrincipalId, DateTimeOffset CreatedAtUtc);
    private sealed record HeldRow(Guid PrincipalId, DateTimeOffset? HeldAtUtc);
}
