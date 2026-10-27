using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Work Centre's System Actions (v42 Design A): what Pegasus itself did on a
/// Case in the window, each row read from the record the action wrote. The four
/// sources are read together and reduced once; a further read attaches the
/// Cases' confirmed vehicle fields. A row whose record was dismissed at or after
/// the action (<see cref="WorkCentreDismissalPolicy"/>) is not listed.
/// </summary>
internal sealed class EfSystemActionQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : ISystemActionQueries
{
    private const string AutoLinkEvent = "intake_case_linked_automatic";
    private const string MergeEvent = "image_initiated_case_merged";
    private const string QueryReceivedEvent = "case_query_received";
    private const string QueryLoggedEvent = "case_query_logged";
    private static readonly string SystemWorkerActorKind = nameof(ActorKind.SystemWorker);
    private static readonly string AutomationActorKind = nameof(ActorKind.Automation);
    private static readonly string CancellationCategory = MailCategories.Code(MailCategory.Cancellation);
    private static readonly string MailboxCode = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
    private static readonly string ManualUploadCode = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.ManualUpload);

    public async Task<SystemActionPage> ListAsync(
        DateTimeOffset sinceUtc,
        SystemActionFilter filter,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        // The four sources share nothing, so they read together.
        var mailTask = ReadMailAsync(sinceUtc, cancellationToken);
        var imagesTask = ReadImagesAsync(sinceUtc, cancellationToken);
        var queriesTask = ReadQueriesAsync(sinceUtc, cancellationToken);
        var cancellationsTask = ReadCancellationsAsync(sinceUtc, cancellationToken);
        await Task.WhenAll(mailTask, imagesTask, queriesTask, cancellationsTask);

        var rows = mailTask.Result
            .Concat(imagesTask.Result)
            .Concat(queriesTask.Result)
            .Concat(cancellationsTask.Result)
            .ToArray();
        var vehicles = await ReadVehiclesAsync(
            rows.Select(row => row.CaseId).Distinct().ToArray(),
            cancellationToken);
        var window = rows.Select(row => WithVehicle(row, vehicles)).ToArray();
        var counts = Enum.GetValues<SystemActionKind>().ToDictionary(
            kind => kind,
            kind => window.Count(row => row.Kind == kind));
        var shown = window
            .Where(row => SystemActionsPolicy.Matches(filter, row.Kind) && SystemActionsPolicy.MatchesSearch(row, search))
            .OrderByDescending(row => row.OccurredAtUtc)
            .ThenBy(row => row.RecordId)
            .ToArray();
        var skipped = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        return new SystemActionPage(
            shown.Skip(skipped).Take(pageSize).ToArray(),
            page,
            pageSize,
            shown.Length,
            counts);
    }

    /// <summary>
    /// An e-mail the matcher linked by itself. The signal is the association
    /// reason (it names the agreeing signals) or, when none was recorded, the
    /// match policy key. A receipt whose current category is cancellation is the
    /// cancellation row, not also this one: both would dismiss the same receipt.
    /// </summary>
    private async Task<Raw[]> ReadMailAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var dismissals = context.Set<WorkCentreDismissalEntity>();
        return await (
            from history in context.IntakeMutationHistory.AsNoTracking()
            where history.EventType == AutoLinkEvent
                && history.OccurredAtUtc >= sinceUtc
                && history.CaseId != null
            join association in context.IntakeManualAssociations.AsNoTracking()
                on history.IntakeReceiptId equals association.IntakeReceiptId
            where association.MatchPolicyKey != null
            join caseEntity in context.Cases.AsNoTracking()
                on history.CaseId equals (Guid?)caseEntity.Id
            join receipt in context.IntakeReceipts.AsNoTracking()
                on history.IntakeReceiptId equals receipt.Id
            join decisionCandidate in context.IntakeMailClassificationDecisions.AsNoTracking()
                on receipt.Id equals decisionCandidate.IntakeReceiptId into decisions
            from decision in decisions.DefaultIfEmpty()
            join messageCandidate in context.RetainedMailboxMessages.AsNoTracking()
                on receipt.ExternalReceiptToken equals messageCandidate.ExternalReceiptToken into messages
            from message in messages.DefaultIfEmpty()
            where (decision == null || decision.Category != CancellationCategory)
                && !dismissals.Any(dismissal => dismissal.RecordId == receipt.Id
                    && dismissal.DismissedAtUtc >= history.OccurredAtUtc)
            select new Raw(
                SystemActionKind.AutoMatchedMail,
                receipt.Id,
                caseEntity.Id,
                caseEntity.Reference,
                history.OccurredAtUtc,
                message == null || message.Subject == null ? association.Reason ?? association.MatchPolicyKey! : message.Subject,
                association.Reason ?? association.MatchPolicyKey!,
                null,
                false))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// An image-initiated Case merged into the instruction Case. The event is
    /// <c>image_initiated_case_merged</c> on the Case workflow. The store writes
    /// the image intake id into the after payload (<see cref="EfImageIntakeStore"/>);
    /// that payload is kept on the action history, not on the workflow event's
    /// <c>ResultJson</c>, which this writer leaves empty.
    /// </summary>
    private async Task<Raw[]> ReadImagesAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var events = await (
            from merged in context.CaseWorkflowEvents.AsNoTracking()
            where merged.EventType == MergeEvent && merged.OccurredAtUtc >= sinceUtc
            join caseEntity in context.Cases.AsNoTracking() on merged.CaseId equals caseEntity.Id
            join history in context.ActionHistory.AsNoTracking()
                on merged.OperationKey equals history.CorrelationId
            where history.EventKind == MergeEvent && history.AggregateType == "case"
            select new ImageEvent(
                caseEntity.Id,
                caseEntity.Reference,
                merged.OccurredAtUtc,
                merged.Reason,
                history.AggregateId,
                history.AfterJson))
            .ToArrayAsync(cancellationToken);
        // The payload is stored against the Case id in "D" form. The comparison
        // stays here: a Guid's string form does not translate.
        var matched = events
            .Where(item => string.Equals(item.AggregateId, item.CaseId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matched.Length == 0)
        {
            return [];
        }

        var parsed = matched
            .Select(item => (item, intake: ReadImageIntake(item.AfterJson)))
            .Where(item => item.intake is not null)
            .Select(item => item.item with { IntakeId = item.intake!.Value.Id, IntakeReference = item.intake.Value.Reference })
            .ToArray();
        var recordIds = parsed.Select(item => item.IntakeId).Distinct().ToArray();
        await using var dismissalContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        var dismissed = await dismissalContext.Set<WorkCentreDismissalEntity>().AsNoTracking()
            .Where(item => recordIds.Contains(item.RecordId))
            .Select(item => new { item.RecordId, item.DismissedAtUtc })
            .ToArrayAsync(cancellationToken);
        return parsed
            .Where(item => !dismissed.Any(dismissal => dismissal.RecordId == item.IntakeId
                && dismissal.DismissedAtUtc >= item.OccurredAtUtc))
            .Select(item => new Raw(
                SystemActionKind.VehicleImagesPaired,
                item.IntakeId,
                item.CaseId,
                item.Reference,
                item.OccurredAtUtc,
                item.IntakeReference ?? item.Reason ?? string.Empty,
                item.Reason ?? string.Empty,
                null,
                false))
            .ToArray();
    }

    /// <summary>A query opened by the Worker or the Automation actor. Take is offered only while it is unassigned.</summary>
    private async Task<Raw[]> ReadQueriesAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var dismissals = context.Set<WorkCentreDismissalEntity>();
        return await (
            from query in context.CaseQueries.AsNoTracking()
            where query.RaisedAtUtc >= sinceUtc
                && (query.RaisedByKind == SystemWorkerActorKind || query.RaisedByKind == AutomationActorKind)
            join caseEntity in context.Cases.AsNoTracking() on query.CaseId equals caseEntity.Id
            join receiptCandidate in context.IntakeReceipts.AsNoTracking()
                on query.IntakeReceiptId equals (Guid?)receiptCandidate.Id into receipts
            from receipt in receipts.DefaultIfEmpty()
            join messageCandidate in context.RetainedMailboxMessages.AsNoTracking()
                on receipt.ExternalReceiptToken equals messageCandidate.ExternalReceiptToken into messages
            from message in messages.DefaultIfEmpty()
            join eventCandidate in context.CaseWorkflowEvents.AsNoTracking()
                    .Where(item => item.EventType == QueryReceivedEvent || item.EventType == QueryLoggedEvent)
                on query.RaisedOperationKey equals eventCandidate.OperationKey into events
            from raised in events.DefaultIfEmpty()
            where !dismissals.Any(dismissal => dismissal.RecordId == query.Id
                && dismissal.DismissedAtUtc >= query.RaisedAtUtc)
            select new Raw(
                SystemActionKind.QueryOpenedAutomatically,
                query.Id,
                caseEntity.Id,
                caseEntity.Reference,
                query.RaisedAtUtc,
                message != null && message.Subject != null
                    ? message.Subject
                    : query.Details ?? string.Empty,
                raised != null && raised.Reason != null ? raised.Reason : string.Empty,
                query.Id,
                query.AssigneeStaffId == null))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// The newest receipt currently linked to each Case whose category is
    /// cancellation, by the same link and category rule as
    /// <c>EfCaseQueryStore.ReadLinkedCancellationMessageIdAsync</c>. The row is
    /// the receipt. It is in the window when the later of the link and the
    /// classification falls in the window: that is when both halves of the rule
    /// were true.
    /// </summary>
    private async Task<Raw[]> ReadCancellationsAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Two reads, not one conditional join: an active manual association is the
        // link, and an accepted link counts only when no association row exists.
        // A reversed association must not fall through to the older accepted link.
        var associated = await (
            from association in context.IntakeManualAssociations.AsNoTracking()
            where association.IsActive
            join receipt in context.IntakeReceipts.AsNoTracking()
                on association.IntakeReceiptId equals receipt.Id
            where (receipt.SourceChannel == MailboxCode || receipt.SourceChannel == ManualUploadCode)
                && receipt.MailClassificationDecision != null
                && receipt.MailClassificationDecision.Category == CancellationCategory
            join message in context.RetainedMailboxMessages.AsNoTracking()
                on receipt.ExternalReceiptToken equals message.ExternalReceiptToken
            select new CancellationCandidate(
                receipt.Id,
                association.CaseId,
                message.Id,
                message.ReceivedAtUtc,
                message.Subject,
                association.LinkedAtUtc,
                receipt.MailClassificationDecision!.DecidedAtUtc,
                association.Reason ?? receipt.MailClassificationDecision.Reason))
            .ToArrayAsync(cancellationToken);
        var accepted = await (
            from link in context.CaseIntakeLinks.AsNoTracking()
            join receipt in context.IntakeReceipts.AsNoTracking()
                on link.IntakeReceiptId equals receipt.Id
            where receipt.ManualAssociation == null
                && (receipt.SourceChannel == MailboxCode || receipt.SourceChannel == ManualUploadCode)
                && receipt.MailClassificationDecision != null
                && receipt.MailClassificationDecision.Category == CancellationCategory
            join message in context.RetainedMailboxMessages.AsNoTracking()
                on receipt.ExternalReceiptToken equals message.ExternalReceiptToken
            select new CancellationCandidate(
                receipt.Id,
                link.CaseId,
                message.Id,
                message.ReceivedAtUtc,
                message.Subject,
                link.LinkedAtUtc,
                receipt.MailClassificationDecision!.DecidedAtUtc,
                receipt.MailClassificationDecision.Reason))
            .ToArrayAsync(cancellationToken);
        var candidates = associated.Concat(accepted).ToArray();

        var newest = candidates
            .GroupBy(item => item.CaseId)
            .Select(group => group
                .OrderByDescending(item => item.ReceivedAtUtc)
                .ThenBy(item => item.MessageId)
                .First())
            .Where(item => MatchAt(item) >= sinceUtc)
            .ToArray();
        if (newest.Length == 0)
        {
            return [];
        }

        var recordIds = newest.Select(item => item.ReceiptId).ToArray();
        var caseIds = newest.Select(item => item.CaseId).Distinct().ToArray();
        await using var lookup = await contextFactory.CreateDbContextAsync(cancellationToken);
        var references = await lookup.Cases.AsNoTracking()
            .Where(item => caseIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Reference })
            .ToDictionaryAsync(item => item.Id, item => item.Reference, cancellationToken);
        var dismissed = await lookup.Set<WorkCentreDismissalEntity>().AsNoTracking()
            .Where(item => recordIds.Contains(item.RecordId))
            .Select(item => new { item.RecordId, item.DismissedAtUtc })
            .ToArrayAsync(cancellationToken);
        return newest
            .Where(item => references.ContainsKey(item.CaseId)
                && !dismissed.Any(dismissal => dismissal.RecordId == item.ReceiptId
                    && dismissal.DismissedAtUtc >= MatchAt(item)))
            .Select(item => new Raw(
                SystemActionKind.CancellationMatched,
                item.ReceiptId,
                item.CaseId,
                references[item.CaseId],
                MatchAt(item),
                item.Subject ?? string.Empty,
                item.Signal,
                null,
                false))
            .ToArray();
    }

    private static DateTimeOffset MatchAt(CancellationCandidate item) =>
        item.LinkedAtUtc >= item.DecidedAtUtc ? item.LinkedAtUtc : item.DecidedAtUtc;

    private async Task<Dictionary<Guid, VehicleFields>> ReadVehiclesAsync(
        Guid[] caseIds,
        CancellationToken cancellationToken)
    {
        if (caseIds.Length == 0)
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var fields = await context.CaseDataFields.AsNoTracking()
            .Where(field => caseIds.Contains(field.WorkId)
                && field.ValueKind == CaseDataCodes.Confirmed
                && (field.FieldName == CaseDataFieldNames.VehicleRegistration
                    || field.FieldName == CaseDataFieldNames.VehicleMake
                    || field.FieldName == CaseDataFieldNames.VehicleModel))
            .Select(field => new { field.WorkId, field.FieldName, field.Value })
            .ToArrayAsync(cancellationToken);
        return fields
            .GroupBy(field => field.WorkId)
            .ToDictionary(
                group => group.Key,
                group => new VehicleFields(
                    group.FirstOrDefault(field => field.FieldName == CaseDataFieldNames.VehicleRegistration)?.Value,
                    group.FirstOrDefault(field => field.FieldName == CaseDataFieldNames.VehicleMake)?.Value,
                    group.FirstOrDefault(field => field.FieldName == CaseDataFieldNames.VehicleModel)?.Value));
    }

    private static SystemActionRow WithVehicle(Raw row, IReadOnlyDictionary<Guid, VehicleFields> vehicles)
    {
        vehicles.TryGetValue(row.CaseId, out var vehicle);
        return new SystemActionRow(
            row.RecordId,
            row.Kind,
            row.CaseId,
            row.Reference,
            vehicle?.Registration,
            vehicle?.Make,
            vehicle?.Model,
            row.Summary,
            row.Signal,
            row.OccurredAtUtc,
            row.QueryId,
            row.QueryUnassigned);
    }

    /// <summary>The after payload <see cref="EfImageIntakeStore"/> writes: image intake id and reference.</summary>
    private static (Guid Id, string? Reference)? ReadImageIntake(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("ImageIntakeId", out var id)
                || !Guid.TryParse(id.GetString(), out var parsed))
            {
                return null;
            }

            var reference = document.RootElement.TryGetProperty("ImageIntakeReference", out var named)
                ? named.GetString()
                : null;
            return (parsed, reference);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Raw(
        SystemActionKind Kind,
        Guid RecordId,
        Guid CaseId,
        string Reference,
        DateTimeOffset OccurredAtUtc,
        string Summary,
        string Signal,
        Guid? QueryId,
        bool QueryUnassigned);

    private sealed record ImageEvent(
        Guid CaseId,
        string Reference,
        DateTimeOffset OccurredAtUtc,
        string? Reason,
        string AggregateId,
        string? AfterJson)
    {
        public Guid IntakeId { get; init; }

        public string? IntakeReference { get; init; }
    }

    private sealed record CancellationCandidate(
        Guid ReceiptId,
        Guid CaseId,
        Guid MessageId,
        DateTimeOffset ReceivedAtUtc,
        string? Subject,
        DateTimeOffset LinkedAtUtc,
        DateTimeOffset DecidedAtUtc,
        string Signal);

    private sealed record VehicleFields(string? Registration, string? Make, string? Model);
}
