using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Triage;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfEmailEvidenceStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null)
    : IRecordSentEmailEvidence, IRecordEmailResponseEvidence, IEmailEvidenceChaseReadModel
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
        EnsureLiveLease(triage, request.Actor, request.EditLeaseToken);


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
        AppendHistory(
            context,
            triage,
            "sent_email_evidence_recorded",
            entity.Actor,
            entity.OperationKey,
            "Recorded exact Sent email evidence",
            requestHash);

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
        var mimeSha256 = request.MimeSha256.ToLowerInvariant();
        var requestHash = Hash(
            $"response|{request.SentEvidenceId:N}|{request.ExpectedSentEvidenceVersion}|{messageIdentity}|{mimeSha256}|{request.ReceivedAtUtc:O}|{request.Actor.Trim()}");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.EmailResponseEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == request.OperationKey.Trim(), cancellationToken);
        if (replay is not null)
        {
            EnsureSameResponse(replay, requestHash);
            return;
        }
        var conflictingHistory = await context.TriageHistory.AsNoTracking().SingleOrDefaultAsync(
            item => item.OperationKey == request.OperationKey.Trim(),
            cancellationToken);
        if (conflictingHistory is not null)
        {
            throw new TriageOperationConflictException(
                conflictingHistory.TriageId,
                request.OperationKey.Trim());
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
        EnsureLiveLease(sentEvidence.Triage, request.Actor, request.EditLeaseToken);

        if (sentEvidence.Response is not null)
        {
            throw new InvalidOperationException("A response is already recorded for the Sent evidence.");
        }

        if (request.ReceivedAtUtc < sentEvidence.SentAtUtc)
        {
            throw new ArgumentException("Response evidence cannot predate the Sent evidence.", nameof(request));
        }

        if (await context.EmailResponseEvidence.AnyAsync(
                item => item.MessageIdentity == messageIdentity,
                cancellationToken))
        {
            throw new InvalidOperationException("The response message identity is already recorded by another operation.");
        }

        var actor = request.Actor.Trim();
        var operationKey = request.OperationKey.Trim();
        context.EmailResponseEvidence.Add(new()
        {
            Id = Guid.NewGuid(),
            SentEvidenceId = sentEvidence.Id,
            SentEvidence = sentEvidence,
            MessageIdentity = messageIdentity,
            MimeSha256 = mimeSha256,
            ReceivedAtUtc = request.ReceivedAtUtc,
            Actor = actor,
            OperationKey = operationKey,
            RequestHash = requestHash
        });
        sentEvidence.Version++;
        AppendHistory(
            context,
            sentEvidence.Triage,
            "email_response_evidence_recorded",
            actor,
            operationKey,
            "Recorded exact reply-chain response evidence",
            requestHash);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailEvidenceChaseProjection>> GetDueAsync(
        DateTimeOffset asOfUtc,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        if (maximumResults is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults), "Maximum results must be between one and 1000.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.SentEmailEvidence
            .AsNoTracking()
            .Where(item => item.ChaseDueAtUtc <= asOfUtc && item.Response == null)
            .OrderBy(item => item.ChaseDueAtUtc)
            .ThenBy(item => item.Id)
            .Take(maximumResults)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new EmailEvidenceChaseProjection(
            item.Id,
            item.TriageId,
            item.MessageIdentity,
            item.Subject,
            DeserializeRecipients(item.RecipientsJson),
            item.SentAtUtc,
            item.ChaseDueAtUtc)).ToArray();
    }

    private static void Validate(RecordSentEmailEvidenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Subject);
        ArgumentNullException.ThrowIfNull(request.Recipients);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MimeSha256);
        ValidateActorAndOperation(request.Actor, request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EditLeaseToken);
        if (request.EditLeaseToken.Length > 64)
        {
            throw new ArgumentException("The edit lease token cannot exceed 64 characters.", nameof(request));
        }

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
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MimeSha256);
        ValidateActorAndOperation(request.Actor, request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EditLeaseToken);
        if (request.ExpectedSentEvidenceVersion < 0 || request.EditLeaseToken.Length > 64)
        {
            throw new ArgumentException("A valid expected evidence version and edit lease token are required.", nameof(request));
        }
        if (request.SentEvidenceId == Guid.Empty || request.MessageIdentity.Trim().Length > 200)
        {
            throw new ArgumentException("A valid Sent evidence and response message identity are required.", nameof(request));
        }

        ValidateSha256(request.MimeSha256, nameof(request));
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

    private void AppendHistory(
        PegasusDbContext context,
        TriageEntity triage,
        string eventType,
        string actor,
        string operationKey,
        string reason,
        string requestHash)
    {
        var beforeVersion = triage.Version;
        triage.Version++;
        context.TriageHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            TriageId = triage.Id,
            Triage = triage,
            EventType = eventType,
            Actor = actor,
            Reason = reason,
            OperationKey = operationKey,
            RequestHash = requestHash,
            OccurredAtUtc = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow(),
            BeforeVersion = beforeVersion,
            AfterVersion = triage.Version,
            AfterState = triage.State,
            AfterAssigneeId = triage.AssigneeId,
            AfterLinkedCaseId = triage.LinkedCaseId
        });
    }

    private void EnsureLiveLease(TriageEntity triage, string actor, string leaseToken)
    {
        var now = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();
        if (triage.EditLeaseExpiresAtUtc is null
            || triage.EditLeaseExpiresAtUtc <= now
            || string.IsNullOrEmpty(triage.EditLeaseTokenHash)
            || string.IsNullOrEmpty(triage.EditLeaseHolder))
        {
            throw new TriageEditLeaseExpiredException(triage.Id);
        }

        var suppliedHash = Hash(leaseToken);
        if (!string.Equals(triage.EditLeaseHolder, actor.Trim(), StringComparison.Ordinal)
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(triage.EditLeaseTokenHash),
                Convert.FromHexString(suppliedHash)))
        {
            throw new TriageEditLeaseConflictException(triage.Id);
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
