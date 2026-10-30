using System.Text.Json;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The old-system folder search port over Box (R561): a name search below
/// the configured old-system folder, returning names and identities only.
/// Without that folder configured, or when Box refuses, throttles or fails,
/// the answer is Unavailable rather than empty. Nothing is written.
/// </summary>
internal sealed class BoxOldSystemCaseFolderSearch(
    BoxContentClient client,
    BoxCustodyOptions options) : IOldSystemCaseFolderSearch
{
    public async Task<OldSystemCaseFolderSearchResult> SearchAsync(
        string term,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        if (options.OldSystemRootFolderId is not { } root)
        {
            return new([], false, OldSystemLookupState.Unavailable);
        }
        try
        {
            var (hits, truncated) = await client.SearchFoldersUnderAsync(root, term, maximumItems, cancellationToken);
            return new(
                hits.Select(hit => new OldSystemCaseFolder(hit.Id, hit.Name, hit.ModifiedAtUtc)).ToArray(),
                truncated);
        }
        catch (Exception exception) when (
            exception is BoxThrottledException
                or HttpRequestException
                or InvalidDataException
                or JsonException
                or UnauthorizedAccessException
                or InvalidOperationException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new([], false, OldSystemLookupState.Unavailable);
        }
    }
}
