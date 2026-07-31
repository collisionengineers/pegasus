using System.Diagnostics;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseReportSentEvidenceStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IApprovedMailboxPolicy approvedMailboxPolicy)
    : IApprovedMailboxReportSentEvidenceStore
{
    public async Task<RetainedApprovedMailboxReportSentEvidence?> GetAsync(
        Guid evidenceId,
        CancellationToken cancellationToken)
    {
        if (evidenceId == Guid.Empty)
        {
            throw new ArgumentException("A retained Sent-evidence identifier is required.", nameof(evidenceId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CaseReportSentEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == evidenceId
                    && item.DiscoveredByKind == nameof(ActorKind.SystemWorker),
                cancellationToken);
        return entity is null ? null : MapRetained(entity);
    }

    public async Task<IReadOnlyList<RetainedApprovedMailboxReportSentEvidence>> ListUnlinkedAsync(
        int maximumResults,
        CancellationToken cancellationToken)
    {
        if (maximumResults is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.CaseReportSentEvidence
            .AsNoTracking()
            .Where(item => item.CaseId == null
                && item.DiscoveredByKind == nameof(ActorKind.SystemWorker))
            .OrderBy(item => item.Id)
            .Take(maximumResults)
            .ToArrayAsync(cancellationToken);
        return entities.Select(MapRetained).ToArray();
    }

    public async Task<RetainedApprovedMailboxReportSentEvidence> RetainAsync(
        RetainApprovedMailboxReportSentEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = Normalize(request);
        if (!await approvedMailboxPolicy.IsApprovedAsync(
                normalized.MailboxIdentity,
                ApprovedMailboxRouteScope.SentEvidence,
                cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "The retained Sent item does not belong to a mailbox approved for Sent evidence.");
        }

        var requestHash = Hash(normalized);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await RetainOnceAsync(normalized, requestHash, cancellationToken);
            }
            catch (Exception exception) when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                var replay = await FindReplayAsync(normalized, requestHash, cancellationToken);
                if (replay is not null)
                {
                    return replay;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new UnreachableException();
    }

    private async Task<RetainedApprovedMailboxReportSentEvidence> RetainOnceAsync(
        RetainApprovedMailboxReportSentEvidenceRequest request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await context.CaseReportSentEvidence
            .SingleOrDefaultAsync(
                item => item.Id == request.EvidenceId
                    || item.RetentionOperationKey == request.OperationKey,
                cancellationToken);
        if (existing is not null)
        {
            EnsureExactReplay(existing, request, requestHash);
            return MapRetained(existing);
        }


        var duplicateItem = await context.CaseReportSentEvidence
            .AsNoTracking()
            .AnyAsync(
                item => item.MailboxIdentity == request.MailboxIdentity
                    && item.ImmutableItemIdentity == request.ImmutableItemIdentity,
                cancellationToken);
        if (duplicateItem)
        {
            throw new InvalidOperationException(
                "The immutable approved-mailbox Sent item already has another retained evidence identity.");
        }

        var entity = new CaseReportSentEvidenceEntity
        {
            Id = request.EvidenceId,
            MailboxIdentity = request.MailboxIdentity,
            SentFolderIdentity = request.SentFolderIdentity,
            ImmutableItemIdentity = request.ImmutableItemIdentity,
            InternetMessageIdentity = request.InternetMessageIdentity,
            ConversationIdentity = request.ConversationIdentity,
            ReplyChainIdentity = request.ReplyChainIdentity,
            SourceOccurrenceIdentity = request.SourceOccurrenceIdentity,
            SourceSha256 = request.SourceSha256,
            MimeSha256 = request.MimeSha256,
            SentAtUtc = request.SentAtUtc,
            DiscoveredAtUtc = request.DiscoveredAtUtc,
            DiscoveredByKind = request.DiscoveredBy.Kind.ToString(),
            DiscoveredBySubjectId = request.DiscoveredBy.SubjectId,
            RetentionOperationKey = request.OperationKey,
            RetentionRequestHash = requestHash
        };
        context.CaseReportSentEvidence.Add(entity);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "report_sent_evidence",
            entity.Id.ToString("D"),
            "report_sent_evidence_retained",
            request.DiscoveredBy,
            request.DiscoveredAtUtc,
            request.OperationKey,
            reason: "Retained exact approved-mailbox Sent evidence",
            afterJson: DocumentActionHistory.Serialize(new
            {
                EvidenceId = entity.Id,
                entity.MailboxIdentity,
                entity.SentFolderIdentity,
                entity.ImmutableItemIdentity,
                entity.InternetMessageIdentity,
                entity.ConversationIdentity,
                entity.ReplyChainIdentity,
                entity.SourceOccurrenceIdentity,
                entity.SourceSha256,
                entity.MimeSha256,
                entity.SentAtUtc,
                entity.DiscoveredAtUtc
            })));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapRetained(entity);
    }

    private async Task<RetainedApprovedMailboxReportSentEvidence?> FindReplayAsync(
        RetainApprovedMailboxReportSentEvidenceRequest request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CaseReportSentEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == request.EvidenceId
                    || item.RetentionOperationKey == request.OperationKey,
                cancellationToken);
        if (entity is null)
        {
            return null;
        }

        EnsureExactReplay(entity, request, requestHash);
        return MapRetained(entity);
    }

    private static void EnsureExactReplay(
        CaseReportSentEvidenceEntity entity,
        RetainApprovedMailboxReportSentEvidenceRequest request,
        string requestHash)
    {
        if (entity.Id != request.EvidenceId
            || !string.Equals(entity.RetentionOperationKey, request.OperationKey, StringComparison.Ordinal)
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(entity.RetentionRequestHash),
                Convert.FromHexString(requestHash)))
        {
            throw new InvalidOperationException(
                "The retained Sent-evidence identity or operation key was already used with different authoritative evidence.");
        }
    }

    private static RetainApprovedMailboxReportSentEvidenceRequest Normalize(
        RetainApprovedMailboxReportSentEvidenceRequest request) => request with
    {
        MailboxIdentity = ApprovedMailboxAddress.Normalize(request.MailboxIdentity),
        SentFolderIdentity = request.SentFolderIdentity.Trim(),
        ImmutableItemIdentity = request.ImmutableItemIdentity.Trim(),
        InternetMessageIdentity = request.InternetMessageIdentity.Trim(),
        ConversationIdentity = request.ConversationIdentity.Trim(),
        ReplyChainIdentity = request.ReplyChainIdentity.Trim(),
        SourceOccurrenceIdentity = request.SourceOccurrenceIdentity.Trim(),
        SourceSha256 = request.SourceSha256.ToUpperInvariant(),
        MimeSha256 = request.MimeSha256.ToUpperInvariant(),
        OperationKey = request.OperationKey.Trim()
    };

    private static string Hash(RetainApprovedMailboxReportSentEvidenceRequest request)
    {
        var material = string.Join(
            '\n',
            request.EvidenceId.ToString("N"),
            request.MailboxIdentity,
            request.SentFolderIdentity,
            request.ImmutableItemIdentity,
            request.InternetMessageIdentity,
            request.ConversationIdentity,
            request.ReplyChainIdentity,
            request.SourceOccurrenceIdentity,
            request.SourceSha256,
            request.MimeSha256,
            request.SentAtUtc.ToString("O"),
            request.DiscoveredBy.Kind.ToString(),
            request.DiscoveredBy.SubjectId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static RetainedApprovedMailboxReportSentEvidence MapRetained(
        CaseReportSentEvidenceEntity entity) => new(
        entity.Id,
        entity.MailboxIdentity,
        entity.SentFolderIdentity,
        entity.ImmutableItemIdentity,
        entity.InternetMessageIdentity,
        entity.ConversationIdentity,
        entity.ReplyChainIdentity,
        entity.SourceOccurrenceIdentity,
        entity.SourceSha256,
        entity.MimeSha256,
        entity.SentAtUtc,
        entity.DiscoveredAtUtc,
        ParseDiscoveryActor(entity.DiscoveredByKind, entity.DiscoveredBySubjectId));

    private static ActionActor ParseDiscoveryActor(string kind, string subjectId) => kind switch
    {
        nameof(ActorKind.SystemWorker) => ActionActor.SystemWorker(subjectId),
        _ => throw new InvalidDataException(
            "Retained approved-mailbox evidence contains an unsupported discovery actor.")
    };

    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        DbUpdateException { InnerException: { } innerException } =>
            IsRetryableConcurrencyFailure(innerException),
        _ => false
    };
}
