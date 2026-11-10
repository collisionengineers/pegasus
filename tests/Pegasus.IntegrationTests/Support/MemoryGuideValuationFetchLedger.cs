using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// The guide valuation ask ledger's contract in memory, for a host whose Case
/// is a recording fake with no Cases row for the ledger's key (R900).
/// </summary>
internal sealed class MemoryGuideValuationFetchLedger : IGuideValuationFetchLedger
{
    private readonly Dictionary<GuideValuationFetchKey, (string Token, bool Answered, DateTimeOffset Expires)> rows = [];
    private readonly Lock gate = new();

    public Task<string?> TryClaimAsync(
        GuideValuationFetchKey key,
        ActionActor actor,
        DateTimeOffset nowUtc,
        DateTimeOffset claimExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (rows.TryGetValue(key, out var row) && (row.Answered || row.Expires > nowUtc))
            {
                return Task.FromResult<string?>(null);
            }

            var token = Guid.NewGuid().ToString("N");
            rows[key] = (token, false, claimExpiresAtUtc);
            return Task.FromResult<string?>(token);
        }
    }

    public Task MarkAnsweredAsync(
        GuideValuationFetchKey key, string claimToken, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (rows.TryGetValue(key, out var row) && row.Token == claimToken)
            {
                rows[key] = row with { Answered = true };
            }
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(GuideValuationFetchKey key, string claimToken, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (rows.TryGetValue(key, out var row) && row.Token == claimToken && !row.Answered)
            {
                rows.Remove(key);
            }
        }

        return Task.CompletedTask;
    }
}
