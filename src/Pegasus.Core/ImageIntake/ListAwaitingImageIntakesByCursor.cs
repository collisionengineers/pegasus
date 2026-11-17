using System.Globalization;
using Pegasus.Core.Identity;

namespace Pegasus.Core.ImageIntake;

/// <summary>
/// The Image Intake records awaiting instruction with no Case, newest first,
/// as the Cases page lists them, one cursor page at a time. The sort key is
/// the offset after the page; a page resumes after the last record it returned,
/// wherever that record now stands, and one that has left the list resumes at
/// that offset. The scope's query name and filters are the MCP tool's, which
/// issued the cursors before this class did, so a cursor issued earlier still
/// decodes (R1042).
/// </summary>
public sealed class ListAwaitingImageIntakesByCursor(
    IImageIntakeQueries queries,
    ICursorProtector protector)
{
    private const string QueryName = "pegasus_image_intake_list";

    private readonly IImageIntakeQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    private readonly ICursorProtector protector =
        protector ?? throw new ArgumentNullException(nameof(protector));

    public async Task<CursorPage<ImageIntakeSummary>> ExecuteAsync(
        ActionActor actor,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var effectiveLimit = CursorPaging.NormalizeLimit(limit);
        var scope = CursorPaging.CreateScope(
            QueryName, actor, "unassociated", "awaiting-instruction", "newest-first");
        var skip = 0;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var position = protector.Unprotect(cursor, scope);
            skip = await queries.ListPositionAsync(
                    associated: false,
                    ImageInitiatedCaseState.AwaitingInstruction,
                    position.Id,
                    cancellationToken) is { } place
                ? place + 1
                : int.TryParse(position.SortKey, NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                    ? offset
                    : throw new CursorRejectedException();
        }

        var window = await queries.ListWindowAsync(
            associated: false,
            ImageInitiatedCaseState.AwaitingInstruction,
            skip,
            effectiveLimit + 1,
            cancellationToken);
        var items = window.Take(effectiveLimit).ToArray();
        var next = window.Count > effectiveLimit
            ? protector.Protect(
                scope,
                (skip + items.Length).ToString(CultureInfo.InvariantCulture),
                items[^1].Id)
            : null;
        return new(items, next);
    }
}
