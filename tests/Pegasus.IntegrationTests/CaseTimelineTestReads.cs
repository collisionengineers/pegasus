using Pegasus.Core.Cases;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The first page of both Notes section tabs as one list, for a test that
/// looks for an entry the Case's timeline shows.
/// </summary>
internal static class CaseTimelineTestReads
{
    public static async Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryAsync(
        this ICaseQueryStore store,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var timeline = await store.ListTimelineAsync(caseId, 1, 1, cancellationToken);
        return [.. timeline.History.Items, .. timeline.Notes.Items];
    }
}
