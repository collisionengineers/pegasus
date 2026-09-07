using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// Where a keyset page stopped: the row's sort timestamp and its id. The id is
/// the tie-breaker, and it is what makes the continuation stable — two rows
/// sharing a timestamp would otherwise be skipped or repeated depending on how
/// the database happened to order them that day.
///
/// This is the DECODED position the store works in. The opaque, tamper-evident
/// cursor a caller holds is minted from it by <see cref="ICursorProtector"/> at
/// the Core boundary, so no store ever parses a caller's string.
/// </summary>
public sealed record KeysetPosition(DateTimeOffset SortKey, Guid Id);

/// <summary>One store-level keyset page: the rows, and where to resume.</summary>
/// <param name="Next">
/// Null when this page is the last: a caller that reaches it stops rather than
/// asking again and receiving the same rows.
/// </param>
public sealed record KeysetPage<T>(IReadOnlyList<T> Items, KeysetPosition? Next);

/// <summary>
/// Everything a connector may know about one retained intake file BEFORE any
/// byte of it is served: exactly what it is, how big it is, what it hashes to,
/// and which receipt and version it belongs to.
///
/// It deliberately carries no storage key. A connector that received one could
/// read the artifact store directly, outside every authorization and integrity
/// check on this boundary, and could keep reading it after the receipt moved
/// on. Identity plus hash is enough to fetch the bytes through the boundary and
/// to verify what came back.
/// </summary>
/// <param name="Occurrence">
/// Which of the receipt's retained files this is, in the receipt's own recorded
/// order. Stable for a receipt version, so a connector can page a multi-part
/// receipt without holding ids it has not been given.
/// </param>
public sealed record IntakeFileMetadata(
    Guid ReceiptId,
    long ReceiptVersion,
    Guid AssetId,
    int Occurrence,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256);

public sealed record IntakeSourceMetadataQuery(Guid ReceiptId, ActionActor Actor);

public interface IGetIntakeSourceMetadata
{
    Task<IntakeFileMetadata?> ExecuteAsync(
        IntakeSourceMetadataQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The one owner of "which of a receipt's retained files is which", so the
/// metadata boundary and the download boundary can never disagree about an
/// occurrence number or about which asset is the source.
/// </summary>
public static class IntakeFileIdentity
{
    /// <summary>
    /// The receipt's recorded assets in one stable order. Recorded order is
    /// preserved and the id breaks any tie, so the same receipt version always
    /// numbers its files the same way.
    /// </summary>
    public static IReadOnlyList<IntakeAssetRecord> Ordered(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return receipt.AssetRecords
            .Select((asset, index) => (asset, index))
            .OrderBy(entry => entry.index)
            .ThenBy(entry => entry.asset.Id)
            .Select(entry => entry.asset)
            .ToArray();
    }

    public static int OccurrenceOf(IntakeReceipt receipt, Guid assetId)
    {
        var ordered = Ordered(receipt);
        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Id == assetId)
            {
                return index;
            }
        }

        return -1;
    }

    public static IntakeFileMetadata Describe(IntakeReceipt receipt, IntakeAssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(asset);
        return new(
            receipt.Id,
            receipt.Version,
            asset.Id,
            OccurrenceOf(receipt, asset.Id),
            asset.FileName,
            asset.MediaType,
            asset.ContentLength,
            asset.ContentHash);
    }

    /// <summary>
    /// The receipt's own retained source asset: exactly one, or none. Two would
    /// mean the retention is inconsistent, and choosing between them is not
    /// this boundary's decision to make.
    /// </summary>
    public static IntakeAssetRecord? SourceAsset(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var sources = receipt.AssetRecords
            .Where(asset => asset.Kind == IntakeAssetKind.Source
                && asset.Disposition == IntakeAssetDisposition.Source)
            .Take(2)
            .ToArray();
        return sources.Length == 1 ? sources[0] : null;
    }
}

/// <summary>
/// The exact metadata of a receipt's retained source, authorized at the same
/// boundary the bytes are. A connector reads this first and verifies what it
/// later downloads against the hash and length it was given here.
/// </summary>
public sealed class GetIntakeSourceMetadata(IIntakeReceiptQueries queries)
    : IGetIntakeSourceMetadata
{
    public async Task<IntakeFileMetadata?> ExecuteAsync(
        IntakeSourceMetadataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        // The same right the download boundary requires: staff casework, or the
        // Automation Actor, which ADR-0011 grants exactly the ordinary
        // operational casework surface and nothing else.
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.ReceiptId == Guid.Empty)
        {
            return null;
        }

        var receipt = await queries.GetAsync(query.ReceiptId, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var source = IntakeFileIdentity.SourceAsset(receipt);
        return source is null ? null : IntakeFileIdentity.Describe(receipt, source);
    }
}

public sealed class ListIntake(IIntakeReceiptQueries queries) : IListIntake
{
    private readonly IIntakeReceiptQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<IntakeListPage> ExecuteAsync(
        ListIntakeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.Page is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The requested page is outside the supported range.");
        }
        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The requested page size is outside the supported range.");
        }
        if (query.Decision is { } decision && !Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The intake decision is not recognized.");
        }

        return await queries.ListAsync(
            query.Decision,
            query.Page,
            query.PageSize,
            cancellationToken);
    }
}

public sealed record ListIntakeByCursorQuery(
    ActionActor Actor,
    IntakeDecision? Decision,
    string? Cursor,
    int? Limit);

public interface IListIntakeByCursor
{
    Task<CursorPage<IntakeReceiptSummary>> ExecuteAsync(
        ListIntakeByCursorQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stable continuation over the received-items list. The opaque cursor is
/// minted and read by the shared <see cref="ICursorProtector"/>, and it is
/// bound to a scope built from the query name, the actor and the filters and
/// order this page was produced under. A cursor minted for a different actor,
/// a different decision filter or a different query is rejected rather than
/// silently paging someone else's list.
/// </summary>
public sealed class ListIntakeByCursor(
    IIntakeReceiptQueries queries,
    ICursorProtector cursorProtector) : IListIntakeByCursor
{
    /// <summary>
    /// The query name that scopes this list's cursors. It is part of the scope
    /// string, so a cursor from the Unidentified queue can never be replayed
    /// here even for the same actor.
    /// </summary>
    public const string QueryName = "intake.received.list";

    /// <summary>
    /// The order the cursor encodes, named in the scope so a later change to it
    /// invalidates every outstanding cursor instead of resuming into an order
    /// that no longer holds.
    /// </summary>
    public const string OrderName = "received-desc,id-desc";

    public async Task<CursorPage<IntakeReceiptSummary>> ExecuteAsync(
        ListIntakeByCursorQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.Decision is { } decision && !Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The intake decision is not recognized.");
        }

        var limit = CursorPaging.NormalizeLimit(query.Limit);
        var scope = Scope(query.Actor, query.Decision);
        var after = Decode(query.Cursor, scope);
        var page = await queries.ListByCursorAsync(query.Decision, after, limit, cancellationToken);
        return new(
            page.Items,
            page.Next is { } next
                ? cursorProtector.Protect(
                    scope,
                    CursorPaging.EncodeUtcTimestamp(next.SortKey),
                    next.Id)
                : null);
    }

    private static string Scope(ActionActor actor, IntakeDecision? decision) =>
        CursorPaging.CreateScope(
            QueryName,
            actor,
            decision?.ToString(),
            OrderName);

    private KeysetPosition? Decode(string? cursor, string scope)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        var (sortKey, id) = cursorProtector.Unprotect(cursor, scope);
        return new(CursorPaging.DecodeUtcTimestamp(sortKey), id);
    }
}

public sealed class GetIntake(IIntakeReceiptQueries queries) : IGetIntake
{
    private readonly IIntakeReceiptQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public Task<IntakeReceipt?> ExecuteAsync(
        GetIntakeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.ReceiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "An intake receipt identifier is required.",
                nameof(query));
        }

        return queries.GetAsync(query.ReceiptId, cancellationToken);
    }
}
