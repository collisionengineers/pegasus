using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Triage;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfEmailEvidenceStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null)
    : IRecordSentEmailEvidence,
      IRecordEmailResponseEvidence,
      IExactEmailResponseEvidenceQueries
{

    public async Task<SentEmailEvidence> ExecuteAsync(
        RecordSentEmailEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var recipients = request.Recipients.Select(item => item.Trim()).ToArray();
        var messageIdentity = request.MessageIdentity.Trim();
        var subject = request.Subject.Trim();
        var mimeSha256 = request.MimeSha256.ToLowerInvariant();
        var requestHash = Hash(
            $"sent|{request.TriageId:N}|{request.ExpectedTriageVersion}|{messageIdentity}|{subject}|{mimeSha256}|{request.SentAtUtc:O}|{request.ChaseDueAtUtc:O}|{request.Actor.Trim()}|{string.Join('\n', recipients)}");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.SentEmailEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == request.OperationKey.Trim(), cancellationToken);
        if (replay is not null)
        {
            EnsureSameSentEvidence(replay, requestHash);
            return Map(replay);
        }
        if (await context.TriageHistory.AsNoTracking().AnyAsync(
                item => item.OperationKey == request.OperationKey.Trim(),
                cancellationToken))
        {
            throw new TriageOperationConflictException(request.TriageId, request.OperationKey.Trim());
        }


        var triage = await context.Triage
            .SingleOrDefaultAsync(item => item.Id == request.TriageId, cancellationToken)
            ?? throw new InvalidOperationException($"Triage '{request.TriageId}' does not exist.");
        if (triage.Version != request.ExpectedTriageVersion)
        {
            throw new TriageVersionConflictException(triage.Id, request.ExpectedTriageVersion, triage.Version);
        }


        var duplicateMessage = await context.SentEmailEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.MessageIdentity == messageIdentity, cancellationToken);
        if (duplicateMessage is not null)
        {
            throw new InvalidOperationException("The Sent message identity is already recorded by another operation.");
        }

        var entity = new SentEmailEvidenceEntity
        {
            Id = Guid.NewGuid(),
            TriageId = triage.Id,
            Triage = triage,
            MessageIdentity = messageIdentity,
            Subject = subject,
            RecipientsJson = JsonSerializer.Serialize(recipients),
            MimeSha256 = mimeSha256,
            SentAtUtc = request.SentAtUtc,
            ChaseDueAtUtc = request.ChaseDueAtUtc,
            Actor = request.Actor.Trim(),
            OperationKey = request.OperationKey.Trim(),
            RequestHash = requestHash,
            Version = 0
        };
        context.SentEmailEvidence.Add(entity);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task ExecuteAsync(
        RecordEmailResponseEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var messageIdentity = request.MessageIdentity.Trim();
        var mailboxAddress = ApprovedMailboxAddress.Normalize(request.MailboxAddress);
        var inReplyToIdentities = request.InReplyToIdentities
            .Select(identity => identity.Trim())
            .ToArray();
        var actor = $"system-worker:{request.Actor.SubjectId}";
        var operationKey = request.OperationKey.Trim();
        var mimeSha256 = request.MimeSha256.ToUpperInvariant();
        var sourceSha256 = request.SourceSha256.ToUpperInvariant();
        var mailboxId = request.MailboxId.Trim();
        var sentFolderIdentity = request.SentFolderIdentity.Trim();
        var currentLocationIdentity = request.CurrentLocationIdentity.Trim();
        var cursorAfterItem = request.CursorAfterItem.Trim();
        var pollLeaseToken = request.PollLeaseToken.Trim();
        var pollOutcomeOperationKey = request.PollOutcomeOperationKey.Trim();
        var requestHash = Hash(string.Join(
            '\n',
            [
                "response",
                request.SentEvidenceId.ToString("N"),
                request.PollOutcomeId.ToString("N"),
                mailboxId,
                mailboxAddress,
                sentFolderIdentity,
                currentLocationIdentity,
                request.ImmutableItemIdentity.Trim(),
                messageIdentity,
                request.ConversationIdentity.Trim(),
                request.ReplyChainIdentity.Trim(),
                string.Join('\n', inReplyToIdentities),
                request.SourceOccurrenceIdentity.Trim(),
                sourceSha256,
                mimeSha256,
                request.SentAtUtc.ToString("O"),
                request.DiscoveredAtUtc.ToString("O"),
                cursorAfterItem,
                pollOutcomeOperationKey,
                actor,
                request.Reason.Trim()
            ]));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.EmailResponseEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureSameResponse(replay, requestHash);
            return;
        }

        var conflictingHistory = await context.TriageHistory.AsNoTracking().SingleOrDefaultAsync(
            item => item.OperationKey == operationKey,
            cancellationToken);
        if (conflictingHistory is not null)
        {
            throw new TriageOperationConflictException(
                conflictingHistory.TriageId,
                operationKey);
        }

        var pollState = await context.ApprovedSentPollStates.SingleOrDefaultAsync(
            item => item.MailboxId == mailboxId,
            cancellationToken);
        if (pollState is null
            || !string.Equals(pollState.MailboxAddress, mailboxAddress, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(pollState.SentFolderIdentity, sentFolderIdentity, StringComparison.Ordinal)
            || !string.Equals(pollState.LeaseToken, pollLeaseToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The approved-Sent poll lease was lost before its exact response could be retained.");
        }

        if (await context.ApprovedSentPollOutcomes.AnyAsync(
                item => item.Id == request.PollOutcomeId
                    || item.OperationKey == pollOutcomeOperationKey,
                cancellationToken))
        {
            throw new InvalidDataException(
                "The approved-Sent outcome is already retained without its atomic response evidence.");
        }

        var sentEvidence = await context.SentEmailEvidence
            .Include(item => item.Triage)
            .Include(item => item.Response)
            .SingleOrDefaultAsync(item => item.Id == request.SentEvidenceId, cancellationToken)
            ?? throw new InvalidOperationException($"Sent email evidence '{request.SentEvidenceId}' does not exist.");
        if (sentEvidence.Version != request.ExpectedSentEvidenceVersion)
        {
            throw new DbUpdateConcurrencyException(
                $"Sent email evidence '{sentEvidence.Id}' changed before its response could be recorded.");
        }

        if (!inReplyToIdentities.Contains(sentEvidence.MessageIdentity, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The approved-mailbox Sent item is not an exact reply to the selected Triage evidence.");
        }

        if (await context.TriageResponseEvidenceLinks.AnyAsync(
                item => item.TriageId == sentEvidence.TriageId,
                cancellationToken))
        {
            throw new TriageResponseEvidenceAlreadyLinkedException(sentEvidence.TriageId);
        }
        if (sentEvidence.Response is not null)
        {
            throw new InvalidOperationException("A response is already recorded for the Sent evidence.");
        }


        if (request.SentAtUtc < sentEvidence.SentAtUtc)
        {
            throw new ArgumentException("Response evidence cannot predate the Sent evidence.", nameof(request));
        }

        if (await context.EmailResponseEvidence.AnyAsync(
                item => item.MessageIdentity == messageIdentity,
                cancellationToken))
        {
            throw new InvalidOperationException("The response message identity is already recorded by another operation.");
        }

        context.ApprovedSentPollOutcomes.Add(new()
        {
            Id = request.PollOutcomeId,
            MailboxId = mailboxId,
            MailboxAddress = mailboxAddress,
            SourceOccurrenceIdentity = request.SourceOccurrenceIdentity.Trim(),
            SourceSha256 = sourceSha256,
            CurrentLocationIdentity = currentLocationIdentity,
            ObservationKind = ApprovedSentItemObservationKind.Discovered.ToString(),
            SentFolderIdentity = sentFolderIdentity,
            ImmutableItemIdentity = request.ImmutableItemIdentity.Trim(),
            InternetMessageIdentity = messageIdentity,
            ConversationIdentity = request.ConversationIdentity.Trim(),
            ReplyChainIdentity = request.ReplyChainIdentity.Trim(),
            InReplyToIdentitiesJson = JsonSerializer.Serialize(inReplyToIdentities),
            AuthoritativeCaseIdentitiesJson = JsonSerializer.Serialize(Array.Empty<Guid>()),
            SentAtUtc = request.SentAtUtc,
            MimeSha256 = mimeSha256,
            OutcomeKind = SentEvidencePollOutcomeKind.TriageResponseRecorded.ToString(),
            RelatedEvidenceId = sentEvidence.Id,
            RecordedAtUtc = request.DiscoveredAtUtc,
            CursorAfterItem = cursorAfterItem,
            OperationKey = pollOutcomeOperationKey
        });

        context.EmailResponseEvidence.Add(new()
        {
            Id = request.PollOutcomeId,
            SentEvidenceId = sentEvidence.Id,
            SentEvidence = sentEvidence,
            PollOutcomeId = request.PollOutcomeId,
            MailboxId = mailboxId,
            MailboxAddress = mailboxAddress,
            SentFolderIdentity = sentFolderIdentity,
            ImmutableItemIdentity = request.ImmutableItemIdentity.Trim(),
            MessageIdentity = messageIdentity,
            ConversationIdentity = request.ConversationIdentity.Trim(),
            ReplyChainIdentity = request.ReplyChainIdentity.Trim(),
            InReplyToIdentitiesJson = JsonSerializer.Serialize(inReplyToIdentities),
            SourceOccurrenceIdentity = request.SourceOccurrenceIdentity.Trim(),
            SourceSha256 = sourceSha256,
            MimeSha256 = mimeSha256,
            SentAtUtc = request.SentAtUtc,
            DiscoveredAtUtc = request.DiscoveredAtUtc,
            Actor = actor,
            OperationKey = operationKey,
            RequestHash = requestHash
        });
        sentEvidence.Version++;
        var linkedAtUtc = UtcNow();
        context.TriageResponseEvidenceLinks.Add(new()
        {
            TriageId = sentEvidence.TriageId,
            Triage = sentEvidence.Triage,
            SentEvidenceId = sentEvidence.Id,
            SentEvidence = sentEvidence,
            Actor = actor,
            OperationKey = operationKey,
            Reason = request.Reason.Trim(),
            LinkedAtUtc = linkedAtUtc
        });
        AppendResponseHistory(
            context,
            sentEvidence.Triage,
            request.Actor,
            operationKey,
            request.Reason.Trim(),
            requestHash,
            linkedAtUtc);


        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await using var verification =
                await contextFactory.CreateDbContextAsync(CancellationToken.None);
            var committedReplay = await verification.EmailResponseEvidence
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.OperationKey == operationKey,
                    CancellationToken.None);
            if (committedReplay is not null)
            {
                EnsureSameResponse(committedReplay, requestHash);
                return;
            }
            if (await verification.TriageHistory.AsNoTracking().AnyAsync(
                    item => item.OperationKey == operationKey,
                    CancellationToken.None))
            {
                throw new TriageOperationConflictException(
                    sentEvidence.TriageId,
                    operationKey);
            }


            if (await verification.TriageResponseEvidenceLinks.AsNoTracking().AnyAsync(
                    item => item.TriageId == sentEvidence.TriageId,
                    CancellationToken.None))
            {
                throw new TriageResponseEvidenceAlreadyLinkedException(
                    sentEvidence.TriageId,
                    exception);
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<ExactEmailResponseEvidenceCandidate>> FindExactCandidatesAsync(
        IReadOnlyList<string> replyChainIdentities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replyChainIdentities);
        if (replyChainIdentities.Count is < 1 or > 100
            || replyChainIdentities.Any(identity => string.IsNullOrWhiteSpace(identity)
                || identity.Trim().Length > 500)
            || replyChainIdentities.Distinct(StringComparer.Ordinal).Count()
                != replyChainIdentities.Count)
        {
            throw new ArgumentException(
                "Between one and 100 distinct exact reply-chain identities are required.",
                nameof(replyChainIdentities));
        }

        var identities = replyChainIdentities.Select(identity => identity.Trim()).ToArray();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.SentEmailEvidence
            .AsNoTracking()
            .Include(item => item.Response)
            .Where(item => identities.Contains(item.MessageIdentity))
            .ToArrayAsync(cancellationToken);
        return rows
            .Where(item => identities.Contains(item.MessageIdentity, StringComparer.Ordinal))
            .Select(item => new ExactEmailResponseEvidenceCandidate(
                item.Id,
                item.Response is null
                    ? item.Version
                    : checked(item.Version - 1),
                item.MessageIdentity,
                item.Response?.MessageIdentity))
            .ToArray();
    }


    private static void Validate(RecordSentEmailEvidenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Subject);
        ArgumentNullException.ThrowIfNull(request.Recipients);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MimeSha256);
        ValidateActorAndOperation(request.Actor, request.OperationKey);

        if (request.TriageId == Guid.Empty || request.ExpectedTriageVersion < 0)
        {
            throw new ArgumentException("A valid Triage identity and expected version are required.", nameof(request));
        }

        if (request.MessageIdentity.Trim().Length > 200 || request.Subject.Trim().Length > 500)
        {
            throw new ArgumentException("Email evidence identity or subject exceeds its storage limit.", nameof(request));
        }

        if (request.Recipients.Count is < 1 or > 100
            || request.Recipients.Any(item => string.IsNullOrWhiteSpace(item)
                || item.Trim().Length > 320
                || item.Any(char.IsControl)))
        {
            throw new ArgumentException("Email evidence requires between one and 100 valid recipients.", nameof(request));
        }

        ValidateSha256(request.MimeSha256, nameof(request));
        if (request.ChaseDueAtUtc <= request.SentAtUtc)
        {
            throw new ArgumentException("The chase due time must be after the Sent time.", nameof(request));
        }
    }

    private static void Validate(RecordEmailResponseEvidenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ExecuteSystemWork);
        if (request.Actor.Kind != ActorKind.SystemWorker)
        {
            throw new UnauthorizedAccessException(
                "Automatic response evidence requires a system-worker actor.");
        }

        if (request.SentEvidenceId == Guid.Empty
            || request.PollOutcomeId == Guid.Empty
            || request.ExpectedSentEvidenceVersion < 0)
        {
            throw new ArgumentException(
                "Valid Sent-evidence, poll-outcome and expected-version identities are required.",
                nameof(request));
        }

        ValidateText(request.MailboxId, 100, nameof(request));
        ValidateText(request.PollLeaseToken, 64, nameof(request));
        _ = ApprovedMailboxAddress.Normalize(request.MailboxAddress);
        ValidateText(request.SentFolderIdentity, 200, nameof(request));
        ValidateText(request.ImmutableItemIdentity, 500, nameof(request));
        ValidateText(request.MessageIdentity, 500, nameof(request));
        ValidateText(request.ConversationIdentity, 500, nameof(request));
        ValidateText(request.ReplyChainIdentity, 500, nameof(request));
        ValidateText(request.SourceOccurrenceIdentity, 200, nameof(request));
        ValidateText(request.CurrentLocationIdentity, 500, nameof(request));
        ValidateText(request.OperationKey, 100, nameof(request));
        ValidateText(request.PollOutcomeOperationKey, 100, nameof(request));
        ValidateText(request.CursorAfterItem, int.MaxValue, nameof(request));
        ValidateText(request.Reason, 500, nameof(request));
        ValidateSha256(request.SourceSha256, nameof(request));
        ValidateSha256(request.MimeSha256, nameof(request));
        if (request.SentAtUtc == default
            || request.DiscoveredAtUtc == default
            || request.SentAtUtc.Offset != TimeSpan.Zero
            || request.DiscoveredAtUtc.Offset != TimeSpan.Zero
            || request.DiscoveredAtUtc < request.SentAtUtc)
        {
            throw new ArgumentException(
                "Authoritative response Sent and discovery times must be ordered UTC instants.",
                nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.InReplyToIdentities);
        if (request.InReplyToIdentities.Count is < 1 or > 100
            || request.InReplyToIdentities.Any(identity => string.IsNullOrWhiteSpace(identity)
                || identity.Trim().Length > 500)
            || request.InReplyToIdentities.Distinct(StringComparer.Ordinal).Count()
                != request.InReplyToIdentities.Count)
        {
            throw new ArgumentException(
                "Automatic response evidence requires distinct exact reply-chain identities.",
                nameof(request));
        }

        if ($"system-worker:{request.Actor.SubjectId}".Length > 200)
        {
            throw new ArgumentException("The system-worker identity exceeds its storage limit.", nameof(request));
        }
    }

    private static void ValidateActorAndOperation(string actor, string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (actor.Trim().Length > 200 || operationKey.Trim().Length > 100)
        {
            throw new ArgumentException("Actor or operation key exceeds its storage limit.");
        }
    }

    private static void ValidateText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Trim().Length > maximumLength
            || value.Any(char.IsControl))
        {
            throw new ArgumentException("An exact response-evidence value is invalid.", parameterName);
        }
    }

    private static void ValidateSha256(string value, string parameterName)
    {
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("The MIME evidence SHA-256 must contain 64 hexadecimal characters.", parameterName);
        }
    }

    private static void EnsureSameSentEvidence(SentEmailEvidenceEntity entity, string requestHash)
    {
        if (entity.RequestHash != requestHash)
        {
            throw new InvalidOperationException("The operation key was already used for different Sent email evidence.");
        }
    }

    private static void EnsureSameResponse(EmailResponseEvidenceEntity entity, string requestHash)
    {
        if (entity.RequestHash != requestHash)
        {
            throw new InvalidOperationException("The operation key was already used for different response evidence.");
        }
    }

    private static SentEmailEvidence Map(SentEmailEvidenceEntity entity) => new(
        entity.Id,
        entity.TriageId,
        entity.MessageIdentity,
        entity.Subject,
        DeserializeRecipients(entity.RecipientsJson),
        entity.MimeSha256,
        entity.SentAtUtc,
        entity.ChaseDueAtUtc,
        entity.Version);



    private static string[] DeserializeRecipients(string json) =>
        JsonSerializer.Deserialize<string[]>(json)
        ?? throw new InvalidDataException("Persisted email recipients are invalid.");

    private static void AppendResponseHistory(
        PegasusDbContext context,
        TriageEntity triage,
        ActionActor actor,
        string operationKey,
        string reason,
        string requestHash,
        DateTimeOffset occurredAtUtc)
    {
        var beforeVersion = triage.Version;
        triage.Version++;
        context.TriageHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            TriageId = triage.Id,
            Triage = triage,
            EventType = "triage_response_linked",
            Actor = actor.SubjectId,
            ActorKind = actor.Kind.ToString(),
            Reason = reason,
            OperationKey = operationKey,
            RequestHash = requestHash,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = triage.Version,
            AfterState = triage.State,
            AfterAssigneeId = triage.AssigneeId,
            AfterLinkedCaseId = triage.LinkedCaseId
        });
    }

    private DateTimeOffset UtcNow() =>
        timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();



    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
