using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Workflow;

public enum ApprovedSentItemObservationKind
{
    Discovered,
    Changed,
    Moved,
    Deleted
}

public enum SentEvidencePollOutcomeKind
{
    TriageResponseRecorded,
    ReportEvidenceRetainedUnlinked,
    ReportEvidenceAutoLinked,
    Unmatched,
    Ambiguous,
    MalformedQuarantined,
    MoveObserved,
    DeleteObserved
}

public sealed record ApprovedSentPollLease(
    string MailboxId,
    string MailboxAddress,
    string SentFolderIdentity,
    string? Cursor,
    string LeaseToken,
    Guid ApprovedMailboxId = default,
    long Generation = 0,
    DateTimeOffset StartBoundaryUtc = default);

public sealed record ApprovedSentItemProvenance(
    string MailboxId,
    string MailboxAddress,
    string SentFolderIdentity,
    string ImmutableItemIdentity,
    string InternetMessageIdentity,
    string ConversationIdentity,
    string ReplyChainIdentity,
    IReadOnlyList<string> InReplyToIdentities,
    IReadOnlyList<Guid> AuthoritativeCaseIdentities,
    DateTimeOffset SentAtUtc,
    string MimeSha256,
    Guid? StaffMailOperationId = null,
    IReadOnlyList<string>? AttachmentSha256 = null,
    Guid? StaffMailMailboxId = null,
    long? StaffMailMailboxGeneration = null,
    string? StaffMailPayloadHash = null);

public sealed record ApprovedSentItem(
    string SourceOccurrenceIdentity,
    string SourceSha256,
    string? CurrentLocationIdentity,
    ApprovedSentItemObservationKind ObservationKind,
    ApprovedSentItemProvenance? Provenance,
    string? MalformedReasonCode,
    string NextCursor,
    string? OriginalSourceSha256 = null,
    string? ObservedSourceSha256 = null,
    string? EvidenceMarker = null);

public sealed record ApprovedSentPage(
    IReadOnlyList<ApprovedSentItem> Items,
    string NextCursor,
    bool HasMore);

public sealed record SentEvidencePollOutcome(
    Guid Id,
    SentEvidencePollOutcomeKind Kind,
    ApprovedSentItem Item,
    Guid? RelatedEvidenceId,
    string? FailureCode,
    DateTimeOffset RecordedAtUtc,
    string OperationKey);

public sealed record PollSentEvidenceResult(
    int PagesRead,
    int ItemsHandled,
    int TriageResponsesRecorded,
    int ReportEvidenceRetained,
    int UnlinkedItems,
    int QuarantinedItems)
{
    public static PollSentEvidenceResult Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

public sealed record PollSentEvidenceBatchResult(
    int MailboxesAttempted, int MailboxesFailed, int PagesRead, int ItemsHandled,
    int ReportEvidenceRetained, string? FirstFailure);

public sealed record UnlinkedSentEvidenceCandidate(
    Guid PollOutcomeId,
    SentEvidencePollOutcomeKind OutcomeKind,
    string MailboxAddress,
    string SentFolderIdentity,
    string ImmutableItemIdentity,
    string InternetMessageIdentity,
    string ConversationIdentity,
    string ReplyChainIdentity,
    IReadOnlyList<string> InReplyToIdentities,
    string SourceOccurrenceIdentity,
    string SourceSha256,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset DiscoveredAtUtc);

public interface ISentEvidencePollOutcomeQueries
{
    Task<IReadOnlyList<UnlinkedSentEvidenceCandidate>> ListUnlinkedReplyCandidatesAsync(
        IReadOnlyList<string> exactReplyChainIdentities,
        int maximumResults,
        CancellationToken cancellationToken);
}

public interface IApprovedSentSource
{
    Task<ApprovedSentPage> ReadAsync(
        ApprovedSentPollLease lease,
        int maximumItems,
        CancellationToken cancellationToken);
}

public interface ISentEvidencePollStore
{
    Task<ApprovedSentPollLease?> ClaimAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<ApprovedSentPollLease?> ClaimAsync(
        Guid approvedMailboxId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken) =>
        ClaimAsync(nowUtc, leaseDuration, cancellationToken);

    Task RecordOutcomeAsync(
        string mailboxId,
        string leaseToken,
        SentEvidencePollOutcome outcome,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        string mailboxId,
        string leaseToken,
        string nextCursor,
        DateTimeOffset completedAtUtc,
        bool hasRemainingItems,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        string mailboxId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        string failureCode,
        CancellationToken cancellationToken);
}

public sealed class ApprovedSentSourceThrottledException : Exception
{
    public ApprovedSentSourceThrottledException(TimeSpan retryAfter)
        : base("The approved Sent source throttled the bounded poll.")
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryAfter, TimeSpan.Zero);
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}

public sealed class PollSentEvidence(
    ISentEvidencePollStore pollStore,
    IApprovedSentSource sentSource,
    IApprovedMailboxPolicy approvedMailboxPolicy,
    IExactEmailResponseEvidenceQueries responseEvidenceQueries,
    IRecordEmailResponseEvidence recordEmailResponseEvidence,
    IRetainApprovedMailboxReportSentEvidence retainReportEvidence,
    IAutoLinkReportEvidence autoLinkReportEvidence,
    TimeProvider timeProvider,
    IStaffMailSendStore? staffMailSendStore = null) : IStaffMailEvidenceReconciler
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromSeconds(30);
    private const string MailboxNotApprovedFailureCode = "sent_mailbox_not_approved";
    private readonly record struct HandledItem(
        SentEvidencePollOutcomeKind Kind,
        bool ReportEvidenceRetained);

    public async Task<PollSentEvidenceBatchResult> ExecuteBatchAsync(
        int maximumMailboxes, int maximumPages, int maximumItemsPerPage,
        ActionActor actor, CancellationToken cancellationToken = default)
    {
        if (maximumMailboxes is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMailboxes));
        }
        var attempted = 0;
        var failed = 0;
        var pages = 0;
        var items = 0;
        var reportEvidence = 0;
        string? firstFailure = null;
        for (var index = 0; index < maximumMailboxes; index++)
        {
            try
            {
                var result = await ExecuteAsync(
                    maximumPages, maximumItemsPerPage, actor, cancellationToken);
                if (result == PollSentEvidenceResult.Empty)
                {
                    break;
                }
                attempted++;
                pages += result.PagesRead;
                items += result.ItemsHandled;
                reportEvidence += result.ReportEvidenceRetained;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                attempted++;
                failed++;
                firstFailure ??= $"{exception.GetType().Name}: {exception.Message}";
            }
        }
        return new(attempted, failed, pages, items, reportEvidence, firstFailure);
    }

    public Task<PollSentEvidenceResult> ExecuteAsync(
        int maximumPages,
        int maximumItemsPerPage,
        ActionActor actor,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(null, maximumPages, maximumItemsPerPage, actor, cancellationToken);

    public async Task ReconcileAsync(
        Guid approvedMailboxId, CancellationToken cancellationToken)
    {
        if (approvedMailboxId == Guid.Empty)
        {
            throw new ArgumentException("An approved mailbox is required.", nameof(approvedMailboxId));
        }
        _ = await ExecuteCoreAsync(
            approvedMailboxId, 5, 50,
            ActionActor.SystemWorker("staff-mail-sent-reconcile"), cancellationToken);
    }

    private async Task<PollSentEvidenceResult> ExecuteCoreAsync(
        Guid? approvedMailboxId,
        int maximumPages,
        int maximumItemsPerPage,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (maximumPages is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPages),
                "A Sent-evidence poll must read between one and ten pages.");
        }

        if (maximumItemsPerPage is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumItemsPerPage),
                "A Sent-evidence page must contain between one and 100 items.");
        }

        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ExecuteSystemWork);
        if (actor.Kind != ActorKind.SystemWorker)
        {
            throw new UnauthorizedAccessException("Sent-evidence polling requires a system-worker actor.");
        }

        var lease = approvedMailboxId is { } targetMailboxId
            ? await pollStore.ClaimAsync(
                targetMailboxId, timeProvider.GetUtcNow(), LeaseDuration, cancellationToken)
            : await pollStore.ClaimAsync(
                timeProvider.GetUtcNow(), LeaseDuration, cancellationToken);
        if (lease is null)
        {
            return PollSentEvidenceResult.Empty;
        }

        ValidateLease(lease);
        try
        {
            if (!await approvedMailboxPolicy.IsApprovedAsync(
                    lease.MailboxAddress,
                    ApprovedMailboxRouteScope.SentEvidence,
                    cancellationToken))
            {
                // An administrator can disable Sent-evidence for this mailbox at any time
                // (or not yet have approved it) — that is an expected state, not a fault.
                // Release the lease for the normal failure-retry backoff and report an
                // empty tick, the same idiom already used above for "not due yet", instead
                // of throwing an unhandled exception every poll.
                await pollStore.ReleaseAsync(
                    lease.MailboxId,
                    lease.LeaseToken,
                    timeProvider.GetUtcNow().Add(FailureRetryDelay),
                    MailboxNotApprovedFailureCode,
                    cancellationToken);
                return PollSentEvidenceResult.Empty;
            }

            var pagesRead = 0;
            var itemsHandled = 0;
            var triageResponses = 0;
            var reportEvidence = 0;
            var unlinkedItems = 0;
            var quarantinedItems = 0;
            var cursor = lease.Cursor;

            for (var pageNumber = 0; pageNumber < maximumPages; pageNumber++)
            {
                var page = await sentSource.ReadAsync(
                    lease with { Cursor = cursor },
                    maximumItemsPerPage,
                    cancellationToken);
                ValidatePage(page, maximumItemsPerPage);
                pagesRead++;

                foreach (var item in page.Items)
                {
                    var handled = await HandleItemAsync(lease, item, actor, cancellationToken);
                    itemsHandled++;
                    cursor = item.NextCursor;
                    if (handled.ReportEvidenceRetained)
                    {
                        reportEvidence++;
                    }

                    switch (handled.Kind)
                    {
                        case SentEvidencePollOutcomeKind.TriageResponseRecorded:
                            triageResponses++;
                            break;
                        case SentEvidencePollOutcomeKind.ReportEvidenceRetainedUnlinked:
                            unlinkedItems++;
                            break;
                        case SentEvidencePollOutcomeKind.Unmatched:
                        case SentEvidencePollOutcomeKind.Ambiguous:
                            unlinkedItems++;
                            break;
                        case SentEvidencePollOutcomeKind.MalformedQuarantined:
                            quarantinedItems++;
                            break;
                    }
                }

                cursor = page.NextCursor;
                if (!page.HasMore)
                {
                    await pollStore.CompleteAsync(
                        lease.MailboxId,
                        lease.LeaseToken,
                        cursor,
                        timeProvider.GetUtcNow(),
                        hasRemainingItems: false,
                        cancellationToken);
                    return new(
                        pagesRead,
                        itemsHandled,
                        triageResponses,
                        reportEvidence,
                        unlinkedItems,
                        quarantinedItems);
                }
            }

            await pollStore.CompleteAsync(
                lease.MailboxId,
                lease.LeaseToken,
                cursor ?? throw new InvalidDataException("The Sent source did not provide a durable cursor."),
                timeProvider.GetUtcNow(),
                hasRemainingItems: true,
                cancellationToken);
            return new(
                pagesRead,
                itemsHandled,
                triageResponses,
                reportEvidence,
                unlinkedItems,
                quarantinedItems);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var retryDelay = exception is ApprovedSentSourceThrottledException throttled
                ? throttled.RetryAfter
                : FailureRetryDelay;
            await pollStore.ReleaseAsync(
                lease.MailboxId,
                lease.LeaseToken,
                timeProvider.GetUtcNow().Add(retryDelay),
                FailureCode(exception),
                cancellationToken);
            throw;
        }
    }

    private async Task<HandledItem> HandleItemAsync(
        ApprovedSentPollLease lease,
        ApprovedSentItem item,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ValidateItemEnvelope(item);
        var operationKey = CreateOperationKey(lease.MailboxId, item);
        var outcomeId = CreateStableId(operationKey);
        var nowUtc = timeProvider.GetUtcNow();

        if (!string.IsNullOrWhiteSpace(item.MalformedReasonCode))
        {
            await RecordOutcomeAsync(
                lease,
                item,
                outcomeId,
                SentEvidencePollOutcomeKind.MalformedQuarantined,
                relatedEvidenceId: null,
                item.MalformedReasonCode,
                nowUtc,
                operationKey,
                cancellationToken);
            return new(SentEvidencePollOutcomeKind.MalformedQuarantined, ReportEvidenceRetained: false);
        }

        if (item.Provenance is not { } provenance)
        {
            await RecordOutcomeAsync(
                lease,
                item,
                outcomeId,
                SentEvidencePollOutcomeKind.MalformedQuarantined,
                relatedEvidenceId: null,
                "missing_sent_provenance",
                nowUtc,
                operationKey,
                cancellationToken);
            return new(SentEvidencePollOutcomeKind.MalformedQuarantined, ReportEvidenceRetained: false);
        }

        try
        {
            ValidateProvenance(lease, provenance, nowUtc);
        }
        catch (ArgumentException exception)
        {
            await RecordOutcomeAsync(
                lease,
                item,
                outcomeId,
                SentEvidencePollOutcomeKind.MalformedQuarantined,
                relatedEvidenceId: null,
                FailureCode(exception),
                nowUtc,
                operationKey,
                cancellationToken);
            return new(SentEvidencePollOutcomeKind.MalformedQuarantined, ReportEvidenceRetained: false);
        }

        if (item.ObservationKind is ApprovedSentItemObservationKind.Moved
            or ApprovedSentItemObservationKind.Deleted)
        {
            var kind = item.ObservationKind == ApprovedSentItemObservationKind.Moved
                ? SentEvidencePollOutcomeKind.MoveObserved
                : SentEvidencePollOutcomeKind.DeleteObserved;
            await RecordOutcomeAsync(
                lease,
                item,
                outcomeId,
                kind,
                relatedEvidenceId: null,
                failureCode: null,
                nowUtc,
                operationKey,
                cancellationToken);
            return new(kind, ReportEvidenceRetained: false);
        }

        IReadOnlyList<ExactEmailResponseEvidenceCandidate> candidates =
            provenance.InReplyToIdentities.Count == 0
                ? Array.Empty<ExactEmailResponseEvidenceCandidate>()
                : await responseEvidenceQueries.FindExactCandidatesAsync(
                    provenance.InReplyToIdentities,
                    cancellationToken);
        var exactCandidates = candidates
            .Where(candidate => provenance.InReplyToIdentities.Contains(
                candidate.ReplyChainIdentity,
                StringComparer.Ordinal))
            .GroupBy(candidate => candidate.SentEvidenceId)
            .Select(group => group.Single())
            .ToArray();
        var caseIdentities = provenance.AuthoritativeCaseIdentities.Distinct().ToArray();
        StaffMailExecution? staffExecution = null;
        if (provenance.StaffMailOperationId is { } staffOperationId)
        {
            staffExecution = staffMailSendStore is null ? null : await staffMailSendStore.GetExecutionForObservationAsync(
                actor, staffOperationId, cancellationToken);
            if (staffExecution is null
                || staffExecution.Operation.ApprovedMailboxId != lease.ApprovedMailboxId
                || staffExecution.Operation.MailboxGeneration != lease.Generation
                || provenance.StaffMailMailboxId != staffExecution.Operation.ApprovedMailboxId
                || provenance.StaffMailMailboxGeneration != staffExecution.Operation.MailboxGeneration
                || !string.Equals(provenance.StaffMailPayloadHash,
                    staffExecution.Operation.PayloadHash, StringComparison.Ordinal)
                || staffExecution.Operation.State is not (StaffMailState.Sending or StaffMailState.Submitted or StaffMailState.Unknown or StaffMailState.Sent)
                || staffExecution.ContextId == Guid.Empty
                || staffExecution.Purpose == StaffMailPurpose.CaseReport && staffExecution.CaseId is null
                || (staffExecution.CaseId is { } resolvedCaseId
                    && caseIdentities.Length > 0
                    && (caseIdentities.Length != 1 || caseIdentities[0] != resolvedCaseId))
                || !staffExecution.Attachments.Select(value => value.Sha256).Order(StringComparer.Ordinal)
                    .SequenceEqual((provenance.AttachmentSha256 ?? []).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                await RecordOutcomeAsync(
                    lease, item, outcomeId, SentEvidencePollOutcomeKind.Ambiguous,
                    null, "staff_mail_sent_correlation_mismatch", nowUtc, operationKey,
                    cancellationToken);
                return new(SentEvidencePollOutcomeKind.Ambiguous, ReportEvidenceRetained: false);
            }
            if (staffExecution.CaseId is { } staffCaseId)
            {
                caseIdentities = [staffCaseId];
            }
        }

        Guid? relatedEvidenceId = null;
        SentEvidencePollOutcomeKind outcomeKind;
        string? failureCode = null;
        var reportEvidenceRetained = false;
        if (exactCandidates.Length == 1
            && caseIdentities.Length == 0
            && (exactCandidates[0].RecordedResponseMessageIdentity is null
                || string.Equals(
                    exactCandidates[0].RecordedResponseMessageIdentity,
                    provenance.InternetMessageIdentity,
                    StringComparison.Ordinal)))
        {
            var candidate = exactCandidates[0];
            if (candidate.RecordedResponseMessageIdentity is not null)
            {
                relatedEvidenceId = candidate.SentEvidenceId;
                outcomeKind = SentEvidencePollOutcomeKind.TriageResponseRecorded;
            }
            else
            {
                try
                {
                    await recordEmailResponseEvidence.ExecuteAsync(
                        new(
                            candidate.SentEvidenceId,
                            candidate.ExpectedSentEvidenceVersion,
                            outcomeId,
                            lease.LeaseToken,
                            provenance.MailboxId,
                            provenance.MailboxAddress,
                            provenance.SentFolderIdentity,
                            provenance.ImmutableItemIdentity,
                            provenance.InternetMessageIdentity,
                            provenance.ConversationIdentity,
                            provenance.ReplyChainIdentity,
                            provenance.InReplyToIdentities,
                            item.SourceOccurrenceIdentity,
                            item.SourceSha256,
                            item.CurrentLocationIdentity!,
                            provenance.MimeSha256,
                            provenance.SentAtUtc,
                            nowUtc,
                            actor,
                            CreateResponseOperationKey(provenance),
                            operationKey,
                            item.NextCursor,
                            "Exact approved-mailbox reply-chain Sent evidence"),
                        cancellationToken);
                    relatedEvidenceId = candidate.SentEvidenceId;
                    outcomeKind = SentEvidencePollOutcomeKind.TriageResponseRecorded;
                }
                catch (TriageResponseEvidenceAlreadyLinkedException)
                {
                    outcomeKind = SentEvidencePollOutcomeKind.Ambiguous;
                }
            }
        }
        else if (exactCandidates.Length == 0 && caseIdentities.Length > 0)
        {
            var retained = await retainReportEvidence.ExecuteAsync(
                new(
                    CreateStableId($"report-evidence:{provenance.MailboxId}:{provenance.ImmutableItemIdentity}"),
                    provenance.MailboxAddress,
                    provenance.SentFolderIdentity,
                    provenance.ImmutableItemIdentity,
                    provenance.InternetMessageIdentity,
                    provenance.ConversationIdentity,
                    provenance.ReplyChainIdentity,
                    item.SourceOccurrenceIdentity,
                    item.SourceSha256,
                    provenance.MimeSha256,
                    provenance.SentAtUtc,
                    nowUtc,
                    actor,
                    CreateReportOperationKey(provenance)),
                cancellationToken);
            reportEvidenceRetained = true;
            relatedEvidenceId = retained.EvidenceId;
            if (caseIdentities.Length == 1)
            {
                var autoLink = await autoLinkReportEvidence.ExecuteAsync(
                    new(
                        caseIdentities[0],
                        retained.EvidenceId,
                        actor,
                        CreateAutoLinkOperationKey(caseIdentities[0], retained.EvidenceId, provenance),
                        "Exact approved-mailbox Sent evidence and one authoritative Case identity"),
                    cancellationToken);
                if (autoLink.Disposition == AutoLinkReportEvidenceDisposition.Linked)
                {
                    outcomeKind = SentEvidencePollOutcomeKind.ReportEvidenceAutoLinked;
                }
                else
                {
                    outcomeKind = SentEvidencePollOutcomeKind.ReportEvidenceRetainedUnlinked;
                    failureCode = autoLink.NotLinkedReasonCode;
                }
            }
            else
            {
                outcomeKind = SentEvidencePollOutcomeKind.Ambiguous;
            }
        }
        else
        {
            outcomeKind = exactCandidates.Length == 0 && caseIdentities.Length == 0
                ? SentEvidencePollOutcomeKind.Unmatched
                : SentEvidencePollOutcomeKind.Ambiguous;
        }

        await RecordOutcomeAsync(
            lease,
            item,
            outcomeId,
            outcomeKind,
            relatedEvidenceId,
            failureCode,
            nowUtc,
            operationKey,
            cancellationToken);
        if (staffExecution is not null && staffExecution.Operation.State is not StaffMailState.Sent)
        {
            await staffMailSendStore!.TransitionObservedSentAsync(
                actor,
                staffExecution.Operation.Id,
                staffExecution.Operation.Version,
                provenance.ImmutableItemIdentity,
                provenance.SentAtUtc,
                nowUtc,
                cancellationToken);
        }
        return new(outcomeKind, reportEvidenceRetained);
    }

    private Task RecordOutcomeAsync(
        ApprovedSentPollLease lease,
        ApprovedSentItem item,
        Guid outcomeId,
        SentEvidencePollOutcomeKind kind,
        Guid? relatedEvidenceId,
        string? failureCode,
        DateTimeOffset recordedAtUtc,
        string operationKey,
        CancellationToken cancellationToken) =>
        pollStore.RecordOutcomeAsync(
            lease.MailboxId,
            lease.LeaseToken,
            new(
                outcomeId,
                kind,
                item,
                relatedEvidenceId,
                failureCode,
                recordedAtUtc,
                operationKey),
            cancellationToken);

    private static void ValidateLease(ApprovedSentPollLease lease)
    {
        if (lease.ApprovedMailboxId == Guid.Empty || lease.Generation <= 0
            || lease.StartBoundaryUtc == default || lease.StartBoundaryUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The approved Sent lease boundary is invalid.", nameof(lease));
        }
        RequireText(lease.MailboxId, 100, nameof(lease));
        RequireText(lease.SentFolderIdentity, 200, nameof(lease));
        RequireText(lease.MailboxAddress, 320, nameof(lease));
        RequireText(lease.LeaseToken, 64, nameof(lease));
    }

    private static void ValidatePage(ApprovedSentPage page, int maximumItems)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(page.Items);
        RequireText(page.NextCursor, int.MaxValue, nameof(page));
        if (page.Items.Count > maximumItems)
        {
            throw new InvalidDataException("The approved Sent source returned more items than requested.");
        }

        var cursors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in page.Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.NextCursor) || !cursors.Add(item.NextCursor))
            {
                throw new InvalidDataException("The approved Sent source returned an invalid item cursor.");
            }
        }

        if (page.Items.Count > 0
            && !string.Equals(page.Items[^1].NextCursor, page.NextCursor, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The approved Sent page cursor is not the cursor after its final item.");
        }

        if (page.HasMore && page.Items.Count == 0)
        {
            throw new InvalidDataException("The approved Sent source reported a backlog without returning an item.");
        }
    }

    private static void ValidateItemEnvelope(ApprovedSentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        RequireText(item.SourceOccurrenceIdentity, 200, nameof(item));
        ValidateSha256(item.SourceSha256, nameof(item));
        RequireText(item.NextCursor, int.MaxValue, nameof(item));
        if (item.CurrentLocationIdentity is { } location)
        {
            RequireText(location, 500, nameof(item));
        }

        if (!Enum.IsDefined(item.ObservationKind)
            || (item.ObservationKind == ApprovedSentItemObservationKind.Deleted
                ? item.CurrentLocationIdentity is not null
                : item.CurrentLocationIdentity is null))
        {
            throw new ArgumentException(
                "The Sent-item observation does not have a valid immutable-copy location.",
                nameof(item));
        }

        if (item.MalformedReasonCode is not null)
        {
            RequireText(item.MalformedReasonCode, 100, nameof(item));
        }

        if (item.EvidenceMarker is null)
        {
            if (item.ObservationKind == ApprovedSentItemObservationKind.Changed
                || item.OriginalSourceSha256 is not null
                || item.ObservedSourceSha256 is not null)
            {
                throw new ArgumentException(
                    "Sent source-integrity hashes require a terminal evidence marker.",
                    nameof(item));
            }

            return;
        }

        RequireText(item.EvidenceMarker, 40, nameof(item));
        ValidateSha256(item.OriginalSourceSha256!, nameof(item));
        if (item.ObservedSourceSha256 is { } observedSourceSha256)
        {
            ValidateSha256(observedSourceSha256, nameof(item));
        }

        if (item.MalformedReasonCode is null
            || item.EvidenceMarker is not ("changed" or "reused" or "missing")
            || !string.Equals(
                item.MalformedReasonCode,
                item.EvidenceMarker switch
                {
                    "changed" => "immutable_sent_source_changed",
                    "reused" => "immutable_sent_source_reused",
                    "missing" => "immutable_sent_source_missing",
                    _ => null
                },
                StringComparison.Ordinal)
            || (item.EvidenceMarker == "missing") != (item.ObservedSourceSha256 is null)
            || (item.EvidenceMarker == "changed")
                != (item.ObservationKind == ApprovedSentItemObservationKind.Changed)
            || (item.EvidenceMarker is "reused" or "missing"
                && item.ObservationKind != ApprovedSentItemObservationKind.Deleted))
        {
            throw new ArgumentException(
                "The Sent source-integrity terminal observation is invalid.",
                nameof(item));
        }
    }

    private static void ValidateProvenance(
        ApprovedSentPollLease lease,
        ApprovedSentItemProvenance provenance,
        DateTimeOffset observedAtUtc)
    {
        if (!string.Equals(provenance.MailboxId, lease.MailboxId, StringComparison.Ordinal)
            || !string.Equals(
                provenance.MailboxAddress,
                lease.MailboxAddress,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                provenance.SentFolderIdentity,
                lease.SentFolderIdentity,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The Sent item does not belong to the claimed approved mailbox.");
        }

        RequireText(provenance.SentFolderIdentity, 200, nameof(provenance));
        RequireText(provenance.ImmutableItemIdentity, 500, nameof(provenance));
        RequireText(provenance.InternetMessageIdentity, 500, nameof(provenance));
        RequireText(provenance.ConversationIdentity, 500, nameof(provenance));
        RequireText(provenance.ReplyChainIdentity, 500, nameof(provenance));
        ValidateSha256(provenance.MimeSha256, nameof(provenance));
        if (provenance.SentAtUtc == default
            || provenance.SentAtUtc.Offset != TimeSpan.Zero
            || provenance.SentAtUtc > observedAtUtc)
        {
            throw new ArgumentException(
                "The authoritative Sent time must be a UTC instant no later than discovery.",
                nameof(provenance));
        }

        ArgumentNullException.ThrowIfNull(provenance.InReplyToIdentities);
        if (provenance.InReplyToIdentities.Count > 100
            || provenance.InReplyToIdentities.Any(identity => string.IsNullOrWhiteSpace(identity)
                || identity.Length != identity.Trim().Length
                || identity.Length > 500
                || identity.Any(char.IsControl))
            || provenance.InReplyToIdentities.Distinct(StringComparer.Ordinal).Count()
                != provenance.InReplyToIdentities.Count)
        {
            throw new ArgumentException("The exact reply-chain identities are invalid.", nameof(provenance));
        }

        ArgumentNullException.ThrowIfNull(provenance.AuthoritativeCaseIdentities);
        if (provenance.AuthoritativeCaseIdentities.Count > 100
            || provenance.AuthoritativeCaseIdentities.Any(identity => identity == Guid.Empty)
            || provenance.AuthoritativeCaseIdentities.Distinct().Count()
                != provenance.AuthoritativeCaseIdentities.Count)
        {
            throw new ArgumentException("The authoritative Case identities are invalid.", nameof(provenance));
        }
        if ((provenance.AttachmentSha256?.Count ?? 0) > 100
            || (provenance.AttachmentSha256?.Any(value => value is null || value.Length != 64
                || !value.All(Uri.IsHexDigit)))
                == true)
        {
            throw new ArgumentException("The Sent attachment hashes are invalid.", nameof(provenance));
        }
    }

    private static string FailureCode(Exception exception) => exception switch
    {
        ApprovedSentSourceThrottledException => "sent_source_throttled",
        UnauthorizedAccessException => MailboxNotApprovedFailureCode,
        InvalidDataException or ArgumentException => "invalid_sent_source_item",
        _ => "sent_evidence_poll_failure"
    };

    private static string CreateOperationKey(string mailboxId, ApprovedSentItem item) =>
        $"sent-poll:{Hash($"{mailboxId}\n{item.SourceOccurrenceIdentity}\n{item.SourceSha256}\n{item.ObservationKind}\n{item.CurrentLocationIdentity}\n{item.OriginalSourceSha256}\n{item.ObservedSourceSha256}\n{item.EvidenceMarker}")}";

    private static string CreateResponseOperationKey(ApprovedSentItemProvenance provenance) =>
        $"sent-response:{Hash($"{provenance.MailboxId}\n{provenance.ImmutableItemIdentity}")}";

    private static string CreateReportOperationKey(ApprovedSentItemProvenance provenance) =>
        $"sent-report:{Hash($"{provenance.MailboxId}\n{provenance.ImmutableItemIdentity}")}";

    private static Guid CreateStableId(string material)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(material), hash);
        return new Guid(hash[..16]);
    }

    private static string CreateAutoLinkOperationKey(
        Guid caseId,
        Guid evidenceId,
        ApprovedSentItemProvenance provenance) =>
        $"report-auto-link:{Hash($"{caseId:D}\n{evidenceId:D}\n{provenance.MailboxId}\n{provenance.ImmutableItemIdentity}")}";

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void ValidateSha256(string value, string parameterName)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("A SHA-256 value must contain 64 hexadecimal characters.", parameterName);
        }
    }

    private static void RequireText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            throw new ArgumentException("A required Sent-evidence identity is invalid.", parameterName);
        }
    }
}
