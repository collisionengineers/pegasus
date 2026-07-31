using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfSentEvidencePollOutcomeQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : ISentEvidencePollOutcomeQueries
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<UnlinkedSentEvidenceCandidate>> ListUnlinkedReplyCandidatesAsync(
        IReadOnlyList<string> exactReplyChainIdentities,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exactReplyChainIdentities);
        if (maximumResults is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResults),
                "A Sent-evidence candidate query must return between one and 100 items.");
        }

        if (exactReplyChainIdentities.Count is < 1 or > 100
            || exactReplyChainIdentities.Any(identity => string.IsNullOrWhiteSpace(identity)
                || identity.Trim().Length > 500
                || identity.Any(char.IsControl)))
        {
            throw new ArgumentException(
                "Between one and 100 distinct exact reply-chain identities are required.",
                nameof(exactReplyChainIdentities));
        }

        var identities = exactReplyChainIdentities.Select(identity => identity.Trim()).ToArray();
        if (identities.Distinct(StringComparer.Ordinal).Count() != identities.Length)
        {
            throw new ArgumentException(
                "Between one and 100 distinct exact reply-chain identities are required.",
                nameof(exactReplyChainIdentities));
        }

        var identityJson = JsonSerializer.Serialize(identities, JsonOptions);
        var unmatched = nameof(SentEvidencePollOutcomeKind.Unmatched);
        var ambiguous = nameof(SentEvidencePollOutcomeKind.Ambiguous);
        var responseRecorded = nameof(SentEvidencePollOutcomeKind.TriageResponseRecorded);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.ApprovedSentPollOutcomes
            .FromSqlInterpolated(
                $"""
                SELECT TOP ({maximumResults}) o.*
                FROM [dbo].[ApprovedSentPollOutcomes] AS o
                WHERE (
                      (o.[RelatedEvidenceId] IS NULL
                       AND o.[OutcomeKind] IN ({unmatched}, {ambiguous}))
                   OR (o.[RelatedEvidenceId] IS NOT NULL
                       AND o.[OutcomeKind] = {responseRecorded}
                       AND EXISTS (
                           SELECT 1
                           FROM [dbo].[EmailResponseEvidence] AS retained
                           WHERE retained.[PollOutcomeId] = o.[Id]
                             AND retained.[SentEvidenceId] = o.[RelatedEvidenceId])
                       AND NOT EXISTS (
                           SELECT 1
                           FROM [dbo].[TriageResponseEvidenceLinks] AS currentLink
                           WHERE currentLink.[SentEvidenceId] = o.[RelatedEvidenceId]))
                  )
                  AND o.[SentFolderIdentity] IS NOT NULL
                  AND o.[ImmutableItemIdentity] IS NOT NULL
                  AND o.[InternetMessageIdentity] IS NOT NULL
                  AND o.[ConversationIdentity] IS NOT NULL
                  AND o.[ReplyChainIdentity] IS NOT NULL
                  AND o.[InReplyToIdentitiesJson] IS NOT NULL
                  AND o.[SentAtUtc] IS NOT NULL
                  AND o.[MimeSha256] IS NOT NULL
                  AND EXISTS (
                      SELECT 1
                      FROM OPENJSON(o.[InReplyToIdentitiesJson]) AS observed
                      INNER JOIN OPENJSON({identityJson}) AS requested
                          ON CONVERT(nvarchar(500), observed.[value]) COLLATE Latin1_General_100_BIN2
                           = CONVERT(nvarchar(500), requested.[value]) COLLATE Latin1_General_100_BIN2)
                ORDER BY o.[RecordedAtUtc] DESC, o.[Id]
                """)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var candidates = new List<UnlinkedSentEvidenceCandidate>(rows.Length);
        foreach (var row in rows)
        {
            var inReplyToIdentities = JsonSerializer.Deserialize<string[]>(
                row.InReplyToIdentitiesJson!,
                JsonOptions)
                ?? throw new InvalidDataException(
                    "A retained Sent-evidence outcome has no exact reply-chain identities.");
            if (!inReplyToIdentities.Any(identity => identities.Contains(identity, StringComparer.Ordinal)))
            {
                throw new InvalidDataException(
                    "A retained Sent-evidence candidate violated the exact reply-chain predicate.");
            }

            candidates.Add(new(
                row.Id,
                Enum.Parse<SentEvidencePollOutcomeKind>(row.OutcomeKind),
                row.MailboxAddress,
                row.SentFolderIdentity!,
                row.ImmutableItemIdentity!,
                row.InternetMessageIdentity!,
                row.ConversationIdentity!,
                row.ReplyChainIdentity!,
                inReplyToIdentities,
                row.SourceOccurrenceIdentity,
                row.SourceSha256,
                row.MimeSha256!,
                row.SentAtUtc!.Value,
                row.RecordedAtUtc));
        }

        return candidates;
    }
}
