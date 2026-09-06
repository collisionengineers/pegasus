using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Email;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfSentEvidencePollStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : ISentEvidencePollStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CompletedPollDelay = TimeSpan.FromMinutes(1);

    public Task<ApprovedSentPollLease?> ClaimAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken) =>
        ClaimCoreAsync(null, nowUtc, leaseDuration, cancellationToken);

    public Task<ApprovedSentPollLease?> ClaimAsync(
        Guid approvedMailboxId, DateTimeOffset nowUtc, TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (approvedMailboxId == Guid.Empty)
        {
            throw new ArgumentException("An approved mailbox is required.", nameof(approvedMailboxId));
        }
        return ClaimCoreAsync(approvedMailboxId, nowUtc, leaseDuration, cancellationToken);
    }

    private async Task<ApprovedSentPollLease?> ClaimCoreAsync(
        Guid? approvedMailboxId, DateTimeOffset nowUtc, TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var approvedState = Pegasus.Core.Identity.ApprovedMailboxState.Approved.ToString();
        var mailboxes = await context.ApprovedMailboxes
            .Where(item => item.State == approvedState
                && item.AllowSentEvidence
                && (approvedMailboxId == null || item.Id == approvedMailboxId)
                && item.MailboxIdentity != null
                && item.SentFolderIdentity != null
                && item.ActivatedAtUtc != null
                && item.MailboxGeneration > 0)
            .OrderBy(item => item.MailboxIdentity)
            .ToArrayAsync(cancellationToken);
        if (mailboxes.Length == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        var mailboxIds = mailboxes.Select(value => value.MailboxIdentity!).ToArray();
        var states = await context.ApprovedSentPollStates
            .Where(item => mailboxIds.Contains(item.MailboxId))
            .ToDictionaryAsync(item => item.MailboxId, StringComparer.Ordinal, cancellationToken);
        foreach (var mailbox in mailboxes)
        {
            var mailboxId = mailbox.MailboxIdentity!;
            var sentFolderIdentity = mailbox.SentFolderIdentity!;
            var activatedAtUtc = mailbox.ActivatedAtUtc!.Value;
            var fingerprint = ScopeFingerprint(mailboxId, sentFolderIdentity);
            if (!states.TryGetValue(mailboxId, out var existing))
            {
                existing = new()
                {
                    MailboxId = mailboxId,
                    MailboxAddress = mailbox.Address,
                    SentFolderIdentity = sentFolderIdentity,
                    ScopeFingerprint = fingerprint,
                    Generation = mailbox.MailboxGeneration,
                    StartBoundaryUtc = activatedAtUtc,
                    DueAtUtc = nowUtc
                };
                states.Add(mailboxId, existing);
                context.ApprovedSentPollStates.Add(existing);
            }
            else if (!string.Equals(existing.ScopeFingerprint, fingerprint, StringComparison.Ordinal)
                || existing.Generation != mailbox.MailboxGeneration)
            {
                existing.MailboxAddress = mailbox.Address;
                existing.SentFolderIdentity = sentFolderIdentity;
                existing.ScopeFingerprint = fingerprint;
                existing.Generation = mailbox.MailboxGeneration;
                existing.StartBoundaryUtc = activatedAtUtc;
                existing.Cursor = null;
                existing.DueAtUtc = nowUtc;
                existing.LeaseToken = null;
                existing.LeaseExpiresAtUtc = null;
                existing.LastCompletedAtUtc = null;
                existing.LastFailureCode = null;
            }
        }

        var state = states.Values
            .Where(value => (approvedMailboxId is not null || value.DueAtUtc <= nowUtc)
                && (value.LeaseExpiresAtUtc is null || value.LeaseExpiresAtUtc <= nowUtc))
            .OrderBy(value => value.DueAtUtc)
            .ThenBy(value => value.MailboxId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (state is null)
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        ValidateLeaseState(state);
        state.LeaseToken = Guid.NewGuid().ToString("N");
        state.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            state.MailboxId,
            state.MailboxAddress,
            state.SentFolderIdentity,
            state.Cursor,
            state.LeaseToken,
            mailboxes.Single(value => value.MailboxIdentity == state.MailboxId).Id,
            state.Generation,
            state.StartBoundaryUtc);
    }

    public async Task RecordOutcomeAsync(
        string mailboxId,
        string leaseToken,
        SentEvidencePollOutcome outcome,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(mailboxId, leaseToken);
        ValidateOutcome(outcome);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var state = await GetOwnedStateAsync(
            context,
            mailboxId,
            leaseToken,
            "recording an item outcome",
            cancellationToken);
        var existing = await context.ApprovedSentPollOutcomes.SingleOrDefaultAsync(
            item => item.Id == outcome.Id || item.OperationKey == outcome.OperationKey,
            cancellationToken);
        if (existing is null)
        {
            context.ApprovedSentPollOutcomes.Add(MapEntity(state, outcome));
        }
        else
        {
            VerifyReplay(existing, state, outcome);
        }

        state.Cursor = outcome.Item.NextCursor;
        state.DueAtUtc = outcome.RecordedAtUtc;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task CompleteAsync(
        string mailboxId,
        string leaseToken,
        string nextCursor,
        DateTimeOffset completedAtUtc,
        bool hasRemainingItems,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(mailboxId, leaseToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(nextCursor);
        RequireUtc(completedAtUtc, nameof(completedAtUtc));
        return UpdateOwnedStateAsync(
            mailboxId,
            leaseToken,
            "completing the poll",
            state =>
            {
                state.Cursor = nextCursor;
                state.DueAtUtc = completedAtUtc.Add(CompletedPollDelay);
                state.LeaseToken = null;
                state.LeaseExpiresAtUtc = null;
                state.LastCompletedAtUtc = completedAtUtc;
                state.LastFailureCode = hasRemainingItems ? "sent_poll_backlog_remaining" : null;
            },
            cancellationToken);
    }

    public Task ReleaseAsync(
        string mailboxId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        string failureCode,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(mailboxId, leaseToken);
        RequireUtc(dueAtUtc, nameof(dueAtUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Trim().Length > 100)
        {
            throw new ArgumentException("The approved-Sent failure code cannot exceed 100 characters.", nameof(failureCode));
        }

        return UpdateOwnedStateAsync(
            mailboxId,
            leaseToken,
            "releasing the poll",
            state =>
            {
                state.DueAtUtc = dueAtUtc;
                state.LeaseToken = null;
                state.LeaseExpiresAtUtc = null;
                state.LastFailureCode = failureCode.Trim();
            },
            cancellationToken);
    }

    private async Task UpdateOwnedStateAsync(
        string mailboxId,
        string leaseToken,
        string operation,
        Action<ApprovedSentPollStateEntity> update,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var state = await GetOwnedStateAsync(
            context,
            mailboxId,
            leaseToken,
            operation,
            cancellationToken);
        update(state);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<ApprovedSentPollStateEntity> GetOwnedStateAsync(
        PegasusDbContext context,
        string mailboxId,
        string leaseToken,
        string operation,
        CancellationToken cancellationToken) =>
        await context.ApprovedSentPollStates.SingleOrDefaultAsync(
            item => item.MailboxId == mailboxId && item.LeaseToken == leaseToken,
            cancellationToken)
        ?? throw new InvalidOperationException(
            $"The approved-Sent lease was lost before {operation}.");

    private static ApprovedSentPollOutcomeEntity MapEntity(
        ApprovedSentPollStateEntity state,
        SentEvidencePollOutcome outcome)
    {
        var provenance = outcome.Item.Provenance;
        return new()
        {
            Id = outcome.Id,
            MailboxId = state.MailboxId,
            MailboxAddress = state.MailboxAddress,
            SourceOccurrenceIdentity = outcome.Item.SourceOccurrenceIdentity,
            SourceSha256 = outcome.Item.SourceSha256.ToUpperInvariant(),
            OriginalSourceSha256 = outcome.Item.OriginalSourceSha256?.ToUpperInvariant(),
            ObservedSourceSha256 = outcome.Item.ObservedSourceSha256?.ToUpperInvariant(),
            EvidenceMarker = outcome.Item.EvidenceMarker,
            CurrentLocationIdentity = outcome.Item.CurrentLocationIdentity,
            ObservationKind = outcome.Item.ObservationKind.ToString(),
            SentFolderIdentity = provenance?.SentFolderIdentity,
            ImmutableItemIdentity = provenance?.ImmutableItemIdentity,
            InternetMessageIdentity = provenance?.InternetMessageIdentity,
            ConversationIdentity = provenance?.ConversationIdentity,
            ReplyChainIdentity = provenance?.ReplyChainIdentity,
            InReplyToIdentitiesJson = provenance is null
                ? null
                : JsonSerializer.Serialize(provenance.InReplyToIdentities, JsonOptions),
            AuthoritativeCaseIdentitiesJson = provenance is null
                ? null
                : JsonSerializer.Serialize(provenance.AuthoritativeCaseIdentities, JsonOptions),
            SentAtUtc = provenance?.SentAtUtc,
            MimeSha256 = provenance?.MimeSha256.ToUpperInvariant(),
            OutcomeKind = outcome.Kind.ToString(),
            RelatedEvidenceId = outcome.RelatedEvidenceId,
            FailureCode = outcome.FailureCode,
            RecordedAtUtc = outcome.RecordedAtUtc,
            CursorAfterItem = outcome.Item.NextCursor,
            OperationKey = outcome.OperationKey
        };
    }

    private static void VerifyReplay(
        ApprovedSentPollOutcomeEntity existing,
        ApprovedSentPollStateEntity state,
        SentEvidencePollOutcome outcome)
    {
        var expected = MapEntity(state, outcome);
        if (existing.Id != expected.Id
            || !string.Equals(existing.MailboxId, expected.MailboxId, StringComparison.Ordinal)
            || !string.Equals(existing.MailboxAddress, expected.MailboxAddress, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.SourceOccurrenceIdentity, expected.SourceOccurrenceIdentity, StringComparison.Ordinal)
            || !string.Equals(existing.SourceSha256, expected.SourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.OriginalSourceSha256, expected.OriginalSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.ObservedSourceSha256, expected.ObservedSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.EvidenceMarker, expected.EvidenceMarker, StringComparison.Ordinal)
            || !string.Equals(existing.CurrentLocationIdentity, expected.CurrentLocationIdentity, StringComparison.Ordinal)
            || !string.Equals(existing.ObservationKind, expected.ObservationKind, StringComparison.Ordinal)
            || !string.Equals(existing.SentFolderIdentity, expected.SentFolderIdentity, StringComparison.Ordinal)
            || !string.Equals(existing.ImmutableItemIdentity, expected.ImmutableItemIdentity, StringComparison.Ordinal)
            || !string.Equals(existing.InternetMessageIdentity, expected.InternetMessageIdentity, StringComparison.Ordinal)
            || !string.Equals(existing.ConversationIdentity, expected.ConversationIdentity, StringComparison.Ordinal)
            || !string.Equals(existing.ReplyChainIdentity, expected.ReplyChainIdentity, StringComparison.Ordinal)
            || !string.Equals(existing.InReplyToIdentitiesJson, expected.InReplyToIdentitiesJson, StringComparison.Ordinal)
            || !string.Equals(existing.AuthoritativeCaseIdentitiesJson, expected.AuthoritativeCaseIdentitiesJson, StringComparison.Ordinal)
            || existing.SentAtUtc != expected.SentAtUtc
            || !string.Equals(existing.MimeSha256, expected.MimeSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.OutcomeKind, expected.OutcomeKind, StringComparison.Ordinal)
            || existing.RelatedEvidenceId != expected.RelatedEvidenceId
            || !string.Equals(existing.FailureCode, expected.FailureCode, StringComparison.Ordinal)
            || !string.Equals(existing.CursorAfterItem, expected.CursorAfterItem, StringComparison.Ordinal)
            || !string.Equals(existing.OperationKey, expected.OperationKey, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The approved-Sent occurrence or operation key is already bound to another durable outcome.");
        }
    }

    private static void ValidateLeaseState(ApprovedSentPollStateEntity state)
    {
        if ((state.LeaseToken is null) != (state.LeaseExpiresAtUtc is null))
        {
            throw new InvalidDataException("The approved-Sent lease state is inconsistent.");
        }
    }

    private static void ValidateIdentity(string mailboxId, string leaseToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
    }

    private static void ValidateOutcome(SentEvidencePollOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(outcome.Item);
        if (outcome.Id == Guid.Empty || !Enum.IsDefined(outcome.Kind))
        {
            throw new ArgumentException("The approved-Sent outcome identity or kind is invalid.", nameof(outcome));
        }
        ValidateSha256(outcome.Item.SourceSha256, nameof(outcome));
        if (outcome.Item.EvidenceMarker is null)
        {
            if (outcome.Item.OriginalSourceSha256 is not null
                || outcome.Item.ObservedSourceSha256 is not null)
            {
                throw new ArgumentException(
                    "Approved-Sent source-integrity hashes require an evidence marker.",
                    nameof(outcome));
            }
        }
        else
        {
            ValidateSha256(outcome.Item.OriginalSourceSha256, nameof(outcome));
            if (outcome.Item.ObservedSourceSha256 is { } observedSourceSha256)
            {
                ValidateSha256(observedSourceSha256, nameof(outcome));
            }

            if (outcome.Item.EvidenceMarker is not ("changed" or "reused" or "missing")
                || !string.Equals(
                    outcome.Item.MalformedReasonCode,
                    outcome.Item.EvidenceMarker switch
                    {
                        "changed" => "immutable_sent_source_changed",
                        "reused" => "immutable_sent_source_reused",
                        "missing" => "immutable_sent_source_missing",
                        _ => null
                    },
                    StringComparison.Ordinal)
                || (outcome.Item.EvidenceMarker == "missing")
                    != (outcome.Item.ObservedSourceSha256 is null)
                || (outcome.Item.EvidenceMarker == "changed")
                    != (outcome.Item.ObservationKind == ApprovedSentItemObservationKind.Changed)
                || (outcome.Item.EvidenceMarker is "reused" or "missing"
                    && outcome.Item.ObservationKind != ApprovedSentItemObservationKind.Deleted))
            {
                throw new ArgumentException(
                    "The approved-Sent source-integrity evidence is invalid.",
                    nameof(outcome));
            }
        }

        if (outcome.Item.MalformedReasonCode is { } malformedReasonCode
            && (outcome.Kind != SentEvidencePollOutcomeKind.MalformedQuarantined
                || !string.Equals(
                    outcome.FailureCode,
                    malformedReasonCode,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "A malformed approved-Sent item requires its exact quarantine outcome.",
                nameof(outcome));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(outcome.OperationKey);
        if (outcome.OperationKey.Length > 100)
        {
            throw new ArgumentException("The approved-Sent operation key cannot exceed 100 characters.", nameof(outcome));
        }

        if (outcome.FailureCode is { Length: > 100 })
        {
            throw new ArgumentException("The approved-Sent outcome failure code cannot exceed 100 characters.", nameof(outcome));
        }

        RequireUtc(outcome.RecordedAtUtc, nameof(outcome));
    }

    private static void ValidateSha256(string? value, string parameterName)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "Approved-Sent SHA-256 values must contain 64 hexadecimal characters.",
                parameterName);
        }
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Approved-Sent persistence instants must be UTC.", parameterName);
        }
    }

    private static string ScopeFingerprint(string mailboxId, string sentFolderIdentity) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{mailboxId}\n{sentFolderIdentity}")));
}
