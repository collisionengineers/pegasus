using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfOperationsStore(
    IDbContextFactory<PegasusDbContext> contextFactory) :
    IEmailOperationsProjectionStore,
    IRequestOperationsProjectionStore,
    IMailboxProcessingRetryStore,
    IExternalWorkRetryStore
{
    private const string SentBacklogRemainingFailureCode = "sent_poll_backlog_remaining";

    private readonly IDbContextFactory<PegasusDbContext> contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    async Task<EmailOperationsProjection> IEmailOperationsProjectionStore.GetAsync(
        int maximumItemsPerDirection,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItemsPerDirection);
        var sourceLimit = checked(maximumItemsPerDirection + 1);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var mailboxRows = await context.ApprovedInboxPollStates
            .AsNoTracking()
            .OrderByDescending(item => item.DueAtUtc)
            .ThenBy(item => item.ApprovedMailboxId)
            .Take(sourceLimit)
            .Select(item => new InboxStateRow(
                item.ApprovedMailboxId,
                item.MailboxAddress,
                item.DueAtUtc,
                item.LeaseToken,
                item.LeaseExpiresAtUtc,
                item.LastCompletedAtUtc,
                item.LastFailureCode))
            .ToListAsync(cancellationToken);
        var poisonRows = await (
                from poison in context.ApprovedInboxPoisonMessages.AsNoTracking()
                join mailbox in context.ApprovedInboxPollStates.AsNoTracking()
                    on poison.ApprovedMailboxId equals mailbox.ApprovedMailboxId
                orderby poison.QuarantinedAtUtc descending, poison.Id
                select new PoisonMessageRow(
                    poison.Id,
                    poison.ApprovedMailboxId,
                    mailbox.MailboxAddress,
                    poison.QuarantinedAtUtc,
                    poison.FailureCode,
                    poison.SourceLength))
            .Take(sourceLimit)
            .ToListAsync(cancellationToken);
        var intakeRows = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox")
            .OrderByDescending(item => item.ProcessedAtUtc)
            .ThenBy(item => item.Id)
            .Take(sourceLimit)
            .Select(item => new IntakeEmailRow(
                item.Id,
                item.ProcessedAtUtc,
                item.Decision))
            .ToListAsync(cancellationToken);
        var responseRows = await context.EmailResponseEvidence
            .AsNoTracking()
            .OrderByDescending(item => item.DiscoveredAtUtc)
            .ThenBy(item => item.Id)
            .Take(sourceLimit)
            .Select(item => new ResponseEmailRow(
                item.Id,
                item.SentEvidence.TriageCaseId,
                item.DiscoveredAtUtc))
            .ToListAsync(cancellationToken);
        var sentStateRows = await context.ApprovedSentPollStates
            .AsNoTracking()
            .OrderByDescending(item => item.DueAtUtc)
            .ThenBy(item => item.MailboxId)
            .Take(sourceLimit)
            .Select(item => new SentStateRow(
                item.MailboxId,
                item.MailboxAddress,
                item.DueAtUtc,
                item.LeaseToken,
                item.LeaseExpiresAtUtc,
                item.LastCompletedAtUtc,
                item.LastFailureCode))
            .ToListAsync(cancellationToken);
        var sentOutcomeRows = await context.ApprovedSentPollOutcomes
            .AsNoTracking()
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenBy(item => item.Id)
            .Take(sourceLimit)
            .Select(item => new SentOutcomeRow(
                item.Id,
                item.MailboxAddress,
                item.RecordedAtUtc,
                item.OutcomeKind,
                item.FailureCode))
            .ToListAsync(cancellationToken);
        var sentRows = await context.SentEmailEvidence
            .AsNoTracking()
            .OrderByDescending(item => item.SentAtUtc)
            .ThenBy(item => item.Id)
            .Take(sourceLimit)
            .Select(item => new SentEmailRow(
                item.Id,
                item.TriageCaseId,
                item.SentAtUtc))
            .ToListAsync(cancellationToken);
        var reportRows = await (
                from evidence in context.CaseReportSentEvidence.AsNoTracking()
                join caseRecord in context.Cases.AsNoTracking()
                    on evidence.CaseId equals (Guid?)caseRecord.Id into cases
                from caseRecord in cases.DefaultIfEmpty()
                orderby evidence.DiscoveredAtUtc descending, evidence.Id
                select new ReportSentEmailRow(
                    evidence.Id,
                    evidence.MailboxIdentity,
                    evidence.DiscoveredAtUtc,
                    evidence.CaseId,
                    caseRecord == null ? null : caseRecord.Reference,
                    caseRecord == null ? null : caseRecord.Principal.Code))
            .Take(sourceLimit)
            .ToListAsync(cancellationToken);

        var receivedCandidates = new List<EmailOperationProjection>(
            mailboxRows.Count + poisonRows.Count + intakeRows.Count + responseRows.Count);
        receivedCandidates.AddRange(mailboxRows.Select(item => MapInboxState(item, nowUtc)));
        receivedCandidates.AddRange(poisonRows.Select(item => new EmailOperationProjection(
            $"received-poison:{item.Id:D}",
            EmailOperationDirection.Received,
            EmailOperationState.Failed,
            item.MailboxAddress,
            item.QuarantinedAtUtc,
            IntakeId: null,
            TriageCaseId: null,
            CaseId: null,
            CaseReference: null,
            PrincipalCode: null,
            item.FailureCode,
            RetryMailboxId: null,
            RetryExpectedDueAtUtc: null,
            item.SourceLength)));
        receivedCandidates.AddRange(intakeRows.Select(item => new EmailOperationProjection(
            $"received-intake:{item.Id:D}",
            EmailOperationDirection.Received,
            MapIntakeState(item.Decision),
            MailboxIdentity: null,
            item.ProcessedAtUtc,
            item.Id,
            TriageCaseId: null,
            CaseId: null,
            CaseReference: null,
            PrincipalCode: null,
            FailureCode: string.Equals(item.Decision, "technical_failure", StringComparison.Ordinal)
                ? "technical_failure"
                : null,
            RetryMailboxId: null,
            RetryExpectedDueAtUtc: null)));
        receivedCandidates.AddRange(responseRows.Select(item => new EmailOperationProjection(
            $"received-response:{item.Id:D}",
            EmailOperationDirection.Received,
            EmailOperationState.Succeeded,
            MailboxIdentity: null,
            item.DiscoveredAtUtc,
            IntakeId: null,
            item.TriageCaseId,
            CaseId: null,
            CaseReference: null,
            PrincipalCode: null,
            FailureCode: null,
            RetryMailboxId: null,
            RetryExpectedDueAtUtc: null)));

        var sentCandidates = new List<EmailOperationProjection>(
            sentStateRows.Count + sentOutcomeRows.Count + sentRows.Count + reportRows.Count);
        sentCandidates.AddRange(sentRows.Select(item => new EmailOperationProjection(
            $"sent-triage:{item.Id:D}",
            EmailOperationDirection.Sent,
            EmailOperationState.Succeeded,
            MailboxIdentity: null,
            item.SentAtUtc,
            IntakeId: null,
            item.TriageCaseId,
            CaseId: null,
            CaseReference: null,
            PrincipalCode: null,
            FailureCode: null,
            RetryMailboxId: null,
            RetryExpectedDueAtUtc: null)));
        sentCandidates.AddRange(reportRows.Select(item => new EmailOperationProjection(
            $"sent-report:{item.Id:D}",
            EmailOperationDirection.Sent,
            EmailOperationState.Succeeded,
            item.MailboxIdentity,
            item.DiscoveredAtUtc,
            IntakeId: null,
            TriageCaseId: null,
            item.CaseId,
            item.CaseReference,
            item.PrincipalCode,
            FailureCode: null,
            RetryMailboxId: null,
            RetryExpectedDueAtUtc: null)));
        sentCandidates.AddRange(sentStateRows.Select(item => MapSentState(item, nowUtc)));
        sentCandidates.AddRange(sentOutcomeRows.Select(item => new EmailOperationProjection(
            $"sent-outcome:{item.Id:D}",
            EmailOperationDirection.Sent,
            MapSentOutcomeState(item.OutcomeKind),
            item.MailboxAddress,
            item.RecordedAtUtc,
            IntakeId: null,
            TriageCaseId: null,
            CaseId: null,
            CaseReference: null,
            PrincipalCode: null,
            item.FailureCode,
            RetryMailboxId: null,
            RetryExpectedDueAtUtc: null)));

        var received = OrderEmailOperations(receivedCandidates);
        var sent = OrderEmailOperations(sentCandidates);
        return new(
            received.Take(maximumItemsPerDirection).ToImmutableArray(),
            sent.Take(maximumItemsPerDirection).ToImmutableArray(),
            received.Length > maximumItemsPerDirection,
            sent.Length > maximumItemsPerDirection);
    }

    async Task<RequestOperationsProjection> IRequestOperationsProjectionStore.GetAsync(
        int maximumItems,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var sourceLimit = checked(maximumItems + 1);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Every Case's work, a Triage Case's included: it has no workflow row
        // but keeps standard Case custody, so its failed job is retried here too.
        var workRows = await (
                from item in context.ExternalWorkItems.AsNoTracking()
                where item.CaseId != null
                    && item.State == "failed"
                    && ((item.LeaseToken == null && item.LeaseExpiresAtUtc == null)
                        || (item.LeaseToken != null && item.LeaseExpiresAtUtc <= nowUtc))
                orderby item.DueAtUtc descending, item.Id
                select new ExternalWorkRow(
                    item.Id,
                    item.State,
                    item.CaseId!.Value,
                    item.Case!.Reference,
                    item.Case!.Principal.Code,
                    item.Kind,
                    item.AttemptCount,
                    item.DueAtUtc,
                    item.LeaseExpiresAtUtc,
                    item.LeaseToken != null,
                    item.CompletedAtUtc,
                    item.FailureCode,
                    item.FailureReason))
            .Take(sourceLimit)
            .ToListAsync(cancellationToken);

        var ordered = workRows.Select(item => MapExternalWork(item, nowUtc))
            .OrderByDescending(item => item.LastActivityAtUtc)
            .ThenBy(item => item.Id)
            .ToArray();
        return new(
            ordered.Take(maximumItems).ToImmutableArray(),
            ordered.Length > maximumItems);
    }

    Task<int> IRequestOperationsProjectionStore.CountRetryableExternalFailuresAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) => CountRetryableExternalFailuresAsync(nowUtc, cancellationToken);

    private async Task<int> CountRetryableExternalFailuresAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExternalWorkItems
            .AsNoTracking()
            .CountAsync(item => item.State == "failed"
                && ((item.LeaseToken == null && item.LeaseExpiresAtUtc == null)
                    || (item.LeaseToken != null && item.LeaseExpiresAtUtc <= nowUtc)),
                cancellationToken);
    }

    async Task<OperationsRetryResult> IMailboxProcessingRetryStore.RetryAsync(
        RetryMailboxProcessingCommand command,
        DateTimeOffset retryAtUtc,
        CancellationToken cancellationToken) => command.Direction switch
    {
        EmailOperationDirection.Received => await RetryReceivedMailboxAsync(
            command,
            retryAtUtc,
            cancellationToken),
        EmailOperationDirection.Sent => await RetrySentMailboxAsync(
            command,
            retryAtUtc,
            cancellationToken),
        _ => throw new InvalidOperationException(
            "The requested mailbox processing failure is unavailable.")
    };

    private async Task<OperationsRetryResult> RetryReceivedMailboxAsync(
        RetryMailboxProcessingCommand command,
        DateTimeOffset retryAtUtc,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.MailboxId, out var mailboxId) || mailboxId == Guid.Empty)
        {
            throw new ArgumentException("A valid approved mailbox identity is required.", nameof(command));
        }
        var expectedFailureCode = command.ExpectedFailureCode.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await context.ApprovedInboxPollStates
            .Where(item => item.ApprovedMailboxId == mailboxId
                && item.LastFailureCode == expectedFailureCode
                && item.DueAtUtc == command.ExpectedDueAtUtc
                && ((item.LeaseToken == null && item.LeaseExpiresAtUtc == null)
                    || (item.LeaseToken != null && item.LeaseExpiresAtUtc <= retryAtUtc)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DueAtUtc, retryAtUtc)
                .SetProperty(item => item.LeaseToken, (string?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.LastFailureCode, (string?)null),
                cancellationToken);
        if (updated == 1)
        {
            return new(IsReplay: false);
        }

        var current = await context.ApprovedInboxPollStates
            .AsNoTracking()
            .Where(item => item.ApprovedMailboxId == mailboxId)
            .Select(item => new
            {
                item.LeaseToken,
                item.LeaseExpiresAtUtc,
                item.LastFailureCode
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The mailbox processing failure is unavailable.");
        if (current.LastFailureCode is null)
        {
            return new(IsReplay: true);
        }
        if (current.LeaseToken is not null && current.LeaseExpiresAtUtc > retryAtUtc)
        {
            throw new InvalidOperationException("Mailbox processing is already leased.");
        }

        throw new InvalidOperationException("The mailbox processing failure changed before retry.");
    }

    private async Task<OperationsRetryResult> RetrySentMailboxAsync(
        RetryMailboxProcessingCommand command,
        DateTimeOffset retryAtUtc,
        CancellationToken cancellationToken)
    {
        var mailboxId = command.MailboxId.Trim();
        var expectedFailureCode = command.ExpectedFailureCode.Trim();
        if (string.Equals(
                expectedFailureCode,
                SentBacklogRemainingFailureCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The sent mailbox backlog is pending work, not a retryable failure.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await context.ApprovedSentPollStates
            .Where(item => item.MailboxId == mailboxId
                && item.LastFailureCode == expectedFailureCode
                && item.DueAtUtc == command.ExpectedDueAtUtc
                && ((item.LeaseToken == null && item.LeaseExpiresAtUtc == null)
                    || (item.LeaseToken != null && item.LeaseExpiresAtUtc <= retryAtUtc)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DueAtUtc, retryAtUtc)
                .SetProperty(item => item.LeaseToken, (string?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.LastFailureCode, (string?)null),
                cancellationToken);
        if (updated == 1)
        {
            return new(IsReplay: false);
        }

        var current = await context.ApprovedSentPollStates
            .AsNoTracking()
            .Where(item => item.MailboxId == mailboxId)
            .Select(item => new
            {
                item.LeaseToken,
                item.LeaseExpiresAtUtc,
                item.LastFailureCode
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The mailbox processing failure is unavailable.");
        if (current.LastFailureCode is null)
        {
            return new(IsReplay: true);
        }
        if (current.LeaseToken is not null && current.LeaseExpiresAtUtc > retryAtUtc)
        {
            throw new InvalidOperationException("Mailbox processing is already leased.");
        }

        throw new InvalidOperationException("The mailbox processing failure changed before retry.");
    }

    async Task<OperationsRetryResult> IExternalWorkRetryStore.RetryAsync(
        RetryExternalWorkCommand command,
        DateTimeOffset retryAtUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await context.ExternalWorkItems
            .Where(item => item.Id == command.WorkItemId
                && item.State == "failed"
                && item.AttemptCount == command.ExpectedAttemptCount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, "pending")
                .SetProperty(item => item.DueAtUtc, retryAtUtc)
                .SetProperty(item => item.LeaseToken, (string?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, (string?)null)
                .SetProperty(item => item.FailureReason, (string?)null),
                cancellationToken);
        if (updated == 1)
        {
            return new(IsReplay: false);
        }

        var current = await context.ExternalWorkItems
            .AsNoTracking()
            .Where(item => item.Id == command.WorkItemId)
            .Select(item => new { item.State, item.AttemptCount })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The external work failure is unavailable.");
        if (current.AttemptCount >= command.ExpectedAttemptCount
            && !string.Equals(current.State, "failed", StringComparison.Ordinal))
        {
            return new(IsReplay: true);
        }
        if (current.AttemptCount > command.ExpectedAttemptCount)
        {
            return new(IsReplay: true);
        }

        throw new InvalidOperationException("The external work failure changed before retry.");
    }

    private static EmailOperationProjection MapInboxState(
        InboxStateRow item,
        DateTimeOffset nowUtc)
    {
        var inconsistentLease = (item.LeaseToken is null) != (item.LeaseExpiresAtUtc is null);
        var activelyLeased = item.LeaseToken is not null && item.LeaseExpiresAtUtc > nowUtc;
        var state = inconsistentLease
            ? EmailOperationState.Unknown
            : item.LastFailureCode is not null
                ? EmailOperationState.Failed
                : activelyLeased || item.LastCompletedAtUtc is null
                    ? EmailOperationState.Pending
                    : EmailOperationState.Succeeded;
        var canRetry = state == EmailOperationState.Failed && !activelyLeased;
        return new(
            $"received-mailbox:{item.MailboxId}",
            EmailOperationDirection.Received,
            state,
            item.MailboxAddress,
            item.DueAtUtc,
            IntakeId: null,
            TriageCaseId: null,
            CaseId: null,
            CaseReference: null,
            PrincipalCode: null,
            item.LastFailureCode,
            canRetry ? item.MailboxId.ToString("D") : null,
            canRetry ? item.DueAtUtc : null);
    }

    private static EmailOperationProjection MapSentState(
        SentStateRow item,
        DateTimeOffset nowUtc)
    {
        var inconsistentLease = (item.LeaseToken is null) != (item.LeaseExpiresAtUtc is null);
        var activelyLeased = item.LeaseToken is not null && item.LeaseExpiresAtUtc > nowUtc;
        var state = inconsistentLease
            ? EmailOperationState.Unknown
            : string.Equals(item.LastFailureCode, SentBacklogRemainingFailureCode, StringComparison.Ordinal)
                ? EmailOperationState.Pending
                : item.LastFailureCode is not null
                    ? EmailOperationState.Failed
                    : activelyLeased || item.LastCompletedAtUtc is null
                        ? EmailOperationState.Pending
                        : EmailOperationState.Succeeded;
        var canRetry = state == EmailOperationState.Failed && !activelyLeased;
        return new(
            $"sent-mailbox:{item.MailboxId}",
            EmailOperationDirection.Sent,
            state,
            item.MailboxAddress,
            item.DueAtUtc,
            IntakeId: null,
            TriageCaseId: null,
            CaseId: null,
            CaseReference: null,
            PrincipalCode: null,
            item.LastFailureCode,
            canRetry ? item.MailboxId : null,
            canRetry ? item.DueAtUtc : null);
    }

    private static EmailOperationState MapSentOutcomeState(string outcomeKind) => outcomeKind switch
    {
        nameof(SentEvidencePollOutcomeKind.TriageResponseRecorded) or
        nameof(SentEvidencePollOutcomeKind.ReportEvidenceRetainedUnlinked) or
        nameof(SentEvidencePollOutcomeKind.MoveObserved) or
        nameof(SentEvidencePollOutcomeKind.DeleteObserved) => EmailOperationState.Succeeded,
        nameof(SentEvidencePollOutcomeKind.MalformedQuarantined) => EmailOperationState.Failed,
        nameof(SentEvidencePollOutcomeKind.Unmatched) or
        nameof(SentEvidencePollOutcomeKind.Ambiguous) => EmailOperationState.Unknown,
        _ => EmailOperationState.Unknown
    };

    private static EmailOperationState MapIntakeState(string decision) => decision switch
    {
        "case_created" or "needs_sorting" or "unsupported" or "ocr_required" =>
            EmailOperationState.Succeeded,
        "technical_failure" => EmailOperationState.Failed,
        _ => EmailOperationState.Unknown
    };

    private static DateTimeOffset LatestActivity(
        DateTimeOffset createdAtUtc,
        DateTimeOffset? revokedAtUtc,
        DateTimeOffset? lastReceiptAtUtc)
    {
        var latest = revokedAtUtc is { } revoked && revoked > createdAtUtc
            ? revoked
            : createdAtUtc;
        return lastReceiptAtUtc is { } receipt && receipt > latest
            ? receipt
            : latest;
    }

    private static RequestOperationProjection MapExternalWork(
        ExternalWorkRow item,
        DateTimeOffset nowUtc)
    {
        var leaseIsConsistent = item.HasWorkLease == item.LeaseExpiresAtUtc.HasValue;
        var activelyLeased = item.HasWorkLease && item.LeaseExpiresAtUtc > nowUtc;
        var state = !leaseIsConsistent
            ? RequestOperationState.UnknownExternal
            : item.State switch
            {
                "pending" or "dispatching" or "queued" or "processing" => RequestOperationState.Pending,
                "completed" => RequestOperationState.Completed,
                "failed" => RequestOperationState.Failed,
                _ => RequestOperationState.UnknownExternal
            };
        return new(
            item.Id,
            state,
            item.CaseId,
            item.CaseReference,
            item.PrincipalCode,
            LatestActivity(item.DueAtUtc, item.LeaseExpiresAtUtc, item.CompletedAtUtc),
            item.Kind,
            item.AttemptCount,
            item.FailureCode,
            item.FailureReason,
            CanRetry: state == RequestOperationState.Failed && !activelyLeased);
    }

    private static EmailOperationProjection[] OrderEmailOperations(
        IEnumerable<EmailOperationProjection> candidates) => candidates
        .OrderByDescending(item => item.LastActivityAtUtc)
        .ThenBy(item => item.OperationId, StringComparer.Ordinal)
        .ToArray();

    private sealed record InboxStateRow(
        Guid MailboxId,
        string MailboxAddress,
        DateTimeOffset DueAtUtc,
        string? LeaseToken,
        DateTimeOffset? LeaseExpiresAtUtc,
        DateTimeOffset? LastCompletedAtUtc,
        string? LastFailureCode);

    private sealed record SentStateRow(
        string MailboxId,
        string MailboxAddress,
        DateTimeOffset DueAtUtc,
        string? LeaseToken,
        DateTimeOffset? LeaseExpiresAtUtc,
        DateTimeOffset? LastCompletedAtUtc,
        string? LastFailureCode);

    private sealed record SentOutcomeRow(
        Guid Id,
        string MailboxAddress,
        DateTimeOffset RecordedAtUtc,
        string OutcomeKind,
        string? FailureCode);

    private sealed record PoisonMessageRow(
        Guid Id,
        Guid MailboxId,
        string MailboxAddress,
        DateTimeOffset QuarantinedAtUtc,
        string FailureCode,
        long? SourceLength);

    private sealed record IntakeEmailRow(
        Guid Id,
        DateTimeOffset ProcessedAtUtc,
        string Decision);

    private sealed record ResponseEmailRow(
        Guid Id,
        Guid TriageCaseId,
        DateTimeOffset DiscoveredAtUtc);

    private sealed record SentEmailRow(
        Guid Id,
        Guid TriageCaseId,
        DateTimeOffset SentAtUtc);

    private sealed record ReportSentEmailRow(
        Guid Id,
        string MailboxIdentity,
        DateTimeOffset DiscoveredAtUtc,
        Guid? CaseId,
        string? CaseReference,
        string? PrincipalCode);

    private sealed record ExternalWorkRow(
        Guid Id,
        string State,
        Guid CaseId,
        string CaseReference,
        string PrincipalCode,
        string Kind,
        int AttemptCount,
        DateTimeOffset DueAtUtc,
        DateTimeOffset? LeaseExpiresAtUtc,
        bool HasWorkLease,
        DateTimeOffset? CompletedAtUtc,
        string? FailureCode,
        string? FailureReason);
}
