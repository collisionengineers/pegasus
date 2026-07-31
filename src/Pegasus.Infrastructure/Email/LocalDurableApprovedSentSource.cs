using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Email;

public sealed class LocalApprovedSentOptions
{
    public const string RequiredRuntimeProfile = "DevelopmentOffline";

    public LocalApprovedSentOptions(
        string runtimeProfile,
        string mailboxId,
        string mailboxAddress,
        string sentFolderIdentity,
        string rootPath)
    {
        if (!string.Equals(runtimeProfile, RequiredRuntimeProfile, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The immutable local approved-Sent adapter is disabled outside DevelopmentOffline.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sentFolderIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (mailboxId.Trim().Length > 100)
        {
            throw new ArgumentException("The mailbox transport identity cannot exceed 100 characters.", nameof(mailboxId));
        }

        if (sentFolderIdentity.Trim().Length > 200)
        {
            throw new ArgumentException("The Sent-folder identity cannot exceed 200 characters.", nameof(sentFolderIdentity));
        }

        MailboxId = mailboxId.Trim();
        MailboxAddress = ApprovedMailboxAddress.Normalize(mailboxAddress);
        SentFolderIdentity = sentFolderIdentity.Trim();
        RootPath = Path.GetFullPath(rootPath);
    }

    public string MailboxId { get; }

    public string MailboxAddress { get; }

    public string SentFolderIdentity { get; }

    public string RootPath { get; }
}

internal sealed class LocalDurableApprovedSentSource(LocalApprovedSentOptions options)
    : IApprovedSentSource
{
    private const int CursorVersion = 1;
    private const int CopyVersion = 1;
    private const int MaximumCopyLength = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApprovedSentPage> ReadAsync(
        ApprovedSentPollLease lease,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        if (!string.Equals(lease.MailboxId, options.MailboxId, StringComparison.Ordinal)
            || !string.Equals(lease.MailboxAddress, options.MailboxAddress, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                lease.SentFolderIdentity,
                options.SentFolderIdentity,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The claimed mailbox or Sent folder is not the configured immutable local approved-Sent source.");
        }

        var root = new DirectoryInfo(options.RootPath);
        root.Refresh();
        if (!root.Exists)
        {
            throw new DirectoryNotFoundException(
                "The configured immutable local approved-Sent root does not exist.");
        }

        if ((root.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "The immutable local approved-Sent root cannot be a reparse point.");
        }

        var cursor = ParseCursor(lease.Cursor);
        var snapshots = await ReadCurrentSnapshotsAsync(root, cancellationToken);
        ValidateCurrentSnapshots(snapshots);
        var changes = CreateChanges(cursor, snapshots);
        var nextCursor = cursor.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var items = new List<ApprovedSentItem>(Math.Min(maximumItems, changes.Count));

        foreach (var change in changes.Take(maximumItems))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cursorItem = ApplyChange(nextCursor, change);
            var serializedCursor = SerializeCursor(nextCursor.Values);
            items.Add(new(
                cursorItem.SourceOccurrenceIdentity,
                cursorItem.SourceSha256,
                cursorItem.CurrentLocationIdentity,
                change.ObservationKind,
                cursorItem.Provenance,
                cursorItem.MalformedReasonCode,
                serializedCursor,
                cursorItem.OriginalSourceSha256,
                cursorItem.ObservedSourceSha256,
                cursorItem.EvidenceMarker));
        }

        var pageCursor = items.Count == 0
            ? SerializeCursor(nextCursor.Values)
            : items[^1].NextCursor;
        return new(items, pageCursor, changes.Count > items.Count);
    }

    private static async Task<IReadOnlyList<CursorItem>> ReadCurrentSnapshotsAsync(
        DirectoryInfo root,
        CancellationToken cancellationToken)
    {
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseSensitive,
            MatchType = MatchType.Simple
        };
        var files = root
            .EnumerateFiles("*.sent.json", enumerationOptions)
            .OrderBy(file => Path.GetRelativePath(root.FullName, file.FullName), StringComparer.Ordinal)
            .ToArray();
        var snapshots = new List<CursorItem>(files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            snapshots.Add(await ReadSnapshotAsync(root, file, cancellationToken));
        }

        return snapshots;
    }

    private static async Task<CursorItem> ReadSnapshotAsync(
        DirectoryInfo root,
        FileInfo file,
        CancellationToken cancellationToken)
    {
        file.Refresh();
        if (!file.Exists || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Approved-Sent copies must be regular immutable files.");
        }

        var relativePath = Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/');
        if (relativePath.StartsWith("../", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("An approved-Sent copy escaped its configured root.");
        }

        await using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumCopyLength)
        {
            var oversizeSourceSha256 = Convert.ToHexString(
                await SHA256.HashDataAsync(stream, cancellationToken));
            return Malformed(relativePath, oversizeSourceSha256, "sent_copy_too_large");
        }

        var content = GC.AllocateUninitializedArray<byte>(checked((int)stream.Length));
        await stream.ReadExactlyAsync(content, cancellationToken);
        var sourceSha256 = Convert.ToHexString(SHA256.HashData(content));
        SentCopyEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SentCopyEnvelope>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return Malformed(relativePath, sourceSha256, "sent_copy_malformed_json");
        }

        if (envelope is null || envelope.Version != CopyVersion)
        {
            return Malformed(relativePath, sourceSha256, "sent_copy_unsupported_version");
        }

        ApprovedSentItemProvenance provenance;
        try
        {
            provenance = new(
                RequireText(envelope.MailboxId, 100),
                ApprovedMailboxAddress.Normalize(RequireText(envelope.MailboxAddress, 320)),
                RequireText(envelope.SentFolderIdentity, 200),
                RequireText(envelope.ImmutableItemIdentity, 500),
                RequireText(envelope.InternetMessageIdentity, 500),
                RequireText(envelope.ConversationIdentity, 500),
                RequireText(envelope.ReplyChainIdentity, 500),
                envelope.InReplyToIdentities?.Select(identity => RequireText(identity, 500)).ToArray()
                    ?? throw new InvalidDataException("The Sent copy is missing reply-chain references."),
                envelope.AuthoritativeCaseIdentities?.ToArray()
                    ?? throw new InvalidDataException("The Sent copy is missing its authoritative Case-identity collection."),
                envelope.SentDateTimeUtc,
                RequireSha256(envelope.MimeSha256));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            return Malformed(relativePath, sourceSha256, "sent_copy_invalid_provenance");
        }

        return new(
            Hash($"{provenance.MailboxId}\n{provenance.ImmutableItemIdentity}"),
            sourceSha256,
            relativePath,
            Deleted: false,
            provenance,
            MalformedReasonCode: null);
    }

    private static CursorItem Malformed(
        string relativePath,
        string sourceSha256,
        string reasonCode) =>
        new(
            Hash($"malformed\n{relativePath}\n{sourceSha256}"),
            sourceSha256,
            relativePath,
            Deleted: false,
            Provenance: null,
            reasonCode);

    private static void ValidateCurrentSnapshots(
        IReadOnlyList<CursorItem> snapshots)
    {
        var byIdentity = new HashSet<string>(StringComparer.Ordinal);
        var byPath = new HashSet<string>(StringComparer.Ordinal);
        foreach (var snapshot in snapshots)
        {
            if (!byIdentity.Add(snapshot.SourceOccurrenceIdentity)
                || snapshot.CurrentLocationIdentity is null
                || !byPath.Add(snapshot.CurrentLocationIdentity))
            {
                throw new InvalidDataException(
                    "The immutable approved-Sent source contains a duplicate item or location identity.");
            }
        }
    }

    private static List<PendingChange> CreateChanges(
        IReadOnlyDictionary<string, CursorItem> cursor,
        IReadOnlyList<CursorItem> snapshots)
    {
        var byIdentity = snapshots.ToDictionary(
            item => item.SourceOccurrenceIdentity,
            StringComparer.Ordinal);
        var byPath = snapshots.ToDictionary(
            item => item.CurrentLocationIdentity!,
            StringComparer.Ordinal);
        var handledCurrentIdentities = new HashSet<string>(StringComparer.Ordinal);
        var changes = new List<PendingChange>();

        foreach (var observed in cursor.Values
                     .Where(item => !item.Deleted)
                     .OrderBy(item => item.CurrentLocationIdentity, StringComparer.Ordinal)
                     .ThenBy(item => item.SourceOccurrenceIdentity, StringComparer.Ordinal))
        {
            if (byIdentity.TryGetValue(observed.SourceOccurrenceIdentity, out var current))
            {
                handledCurrentIdentities.Add(current.SourceOccurrenceIdentity);
                if (!string.Equals(observed.SourceSha256, current.SourceSha256, StringComparison.Ordinal))
                {
                    changes.Add(CreateIntegrityTerminal(
                        ApprovedSentItemObservationKind.Changed,
                        observed,
                        current,
                        "changed",
                        "immutable_sent_source_changed"));
                }
                else if (!string.Equals(
                             observed.CurrentLocationIdentity,
                             current.CurrentLocationIdentity,
                             StringComparison.Ordinal))
                {
                    changes.Add(new(
                        ApprovedSentItemObservationKind.Moved,
                        current with { Provenance = observed.Provenance }));
                }

                continue;
            }

            var replacement = observed.CurrentLocationIdentity is { } location
                && byPath.TryGetValue(location, out var currentAtLocation)
                    ? currentAtLocation
                    : null;
            changes.Add(CreateIntegrityTerminal(
                ApprovedSentItemObservationKind.Deleted,
                observed,
                replacement,
                replacement is null ? "missing" : "reused",
                replacement is null
                    ? "immutable_sent_source_missing"
                    : "immutable_sent_source_reused"));
        }

        foreach (var current in snapshots)
        {
            if (handledCurrentIdentities.Contains(current.SourceOccurrenceIdentity))
            {
                continue;
            }

            if (!cursor.TryGetValue(current.SourceOccurrenceIdentity, out var observed))
            {
                changes.Add(new(ApprovedSentItemObservationKind.Discovered, current));
            }
            else if (!string.Equals(observed.SourceSha256, current.SourceSha256, StringComparison.Ordinal))
            {
                changes.Add(CreateIntegrityTerminal(
                    ApprovedSentItemObservationKind.Changed,
                    observed,
                    current,
                    "changed",
                    "immutable_sent_source_changed"));
            }
            else if (observed.Deleted
                     || !string.Equals(
                         observed.CurrentLocationIdentity,
                         current.CurrentLocationIdentity,
                         StringComparison.Ordinal))
            {
                changes.Add(new(
                    ApprovedSentItemObservationKind.Moved,
                    current with { Provenance = observed.Provenance }));
            }
        }

        changes.Sort(static (left, right) =>
        {
            var leftPriority = left.ObservationKind == ApprovedSentItemObservationKind.Discovered ? 1 : 0;
            var rightPriority = right.ObservationKind == ApprovedSentItemObservationKind.Discovered ? 1 : 0;
            var priorityComparison = leftPriority.CompareTo(rightPriority);
            return priorityComparison != 0
                ? priorityComparison
                : string.Compare(
                    left.Item.SourceOccurrenceIdentity,
                    right.Item.SourceOccurrenceIdentity,
                    StringComparison.Ordinal);
        });

        return changes;
    }

    private static PendingChange CreateIntegrityTerminal(
        ApprovedSentItemObservationKind observationKind,
        CursorItem observed,
        CursorItem? current,
        string evidenceMarker,
        string failureCode)
    {
        var originalSourceSha256 = observed.OriginalSourceSha256 ?? observed.SourceSha256;
        var terminal = observed with
        {
            SourceSha256 = current?.SourceSha256 ?? observed.SourceSha256,
            CurrentLocationIdentity = observationKind == ApprovedSentItemObservationKind.Deleted
                ? null
                : current?.CurrentLocationIdentity,
            Deleted = observationKind == ApprovedSentItemObservationKind.Deleted,
            MalformedReasonCode = failureCode,
            OriginalSourceSha256 = originalSourceSha256,
            ObservedSourceSha256 = current?.SourceSha256,
            EvidenceMarker = evidenceMarker
        };
        return new(observationKind, terminal);
    }

    private static CursorItem ApplyChange(
        IDictionary<string, CursorItem> cursor,
        PendingChange change)
    {
        var updated = change.Item with
        {
            Deleted = change.ObservationKind == ApprovedSentItemObservationKind.Deleted
        };
        cursor[updated.SourceOccurrenceIdentity] = updated;
        return updated;
    }

    private static Dictionary<string, CursorItem> ParseCursor(string? value)
    {
        if (value is null)
        {
            return new(StringComparer.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("The approved-Sent cursor is invalid.");
        }

        CursorEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<CursorEnvelope>(value, JsonOptions)
                ?? throw new InvalidDataException("The approved-Sent cursor is missing.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The approved-Sent cursor is malformed.", exception);
        }

        if (envelope.Version != CursorVersion || envelope.Items is null)
        {
            throw new InvalidDataException(
                $"Unsupported approved-Sent cursor version '{envelope.Version}'.");
        }

        var result = new Dictionary<string, CursorItem>(StringComparer.Ordinal);
        foreach (var item in envelope.Items)
        {
            if (item is null
                || string.IsNullOrWhiteSpace(item.SourceOccurrenceIdentity)
                || item.SourceOccurrenceIdentity.Length > 200
                || item.SourceOccurrenceIdentity.Any(char.IsControl)
                || item.SourceSha256 is null
                || item.SourceSha256.Length != 64
                || item.SourceSha256.Any(character => !char.IsAsciiHexDigit(character))
                || item.Deleted != (item.CurrentLocationIdentity is null)
                || item.CurrentLocationIdentity is { } location
                    && (string.IsNullOrWhiteSpace(location)
                        || location.Length > 500
                        || location.Any(char.IsControl)
                        || Path.IsPathRooted(location)
                        || location.StartsWith("../", StringComparison.Ordinal))
                || item.MalformedReasonCode is { Length: > 100 }
                || HasInvalidIntegrityEvidence(item)
                || !result.TryAdd(item.SourceOccurrenceIdentity, item))
            {
                throw new InvalidDataException("The approved-Sent cursor contains an invalid item.");
            }
        }

        return result;
    }

    private static bool HasInvalidIntegrityEvidence(CursorItem item)
    {
        if (item.EvidenceMarker is null)
        {
            return item.OriginalSourceSha256 is not null || item.ObservedSourceSha256 is not null;
        }

        return item.MalformedReasonCode is null
            || item.EvidenceMarker is not ("changed" or "reused" or "missing")
            || !string.Equals(
                item.MalformedReasonCode,
                item.EvidenceMarker switch
                {
                    "changed" => "immutable_sent_source_changed",
                    "reused" => "immutable_sent_source_reused",
                    "missing" => "immutable_sent_source_missing",
                    _ => null
                },
                StringComparison.Ordinal)
            || IsInvalidSha256(item.OriginalSourceSha256)
            || (item.EvidenceMarker == "missing"
                ? item.ObservedSourceSha256 is not null || !item.Deleted
                : IsInvalidSha256(item.ObservedSourceSha256))
            || (item.EvidenceMarker == "changed") == item.Deleted;
    }

    private static bool IsInvalidSha256(string? value) =>
        value is null
        || value.Length != 64
        || value.Any(character => !char.IsAsciiHexDigit(character));

    private static string SerializeCursor(IEnumerable<CursorItem> items) =>
        JsonSerializer.Serialize(
            new CursorEnvelope(
                CursorVersion,
                items.OrderBy(item => item.SourceOccurrenceIdentity, StringComparer.Ordinal).ToArray()),
            JsonOptions);

    private static string RequireText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Trim().Length > maximumLength
            || value.Any(char.IsControl))
        {
            throw new InvalidDataException("The Sent copy contains an invalid identity.");
        }

        return value.Trim();
    }

    private static string RequireSha256(string? value)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new InvalidDataException("The Sent copy contains an invalid MIME SHA-256.");
        }

        return value.ToUpperInvariant();
    }

    private static string Hash(string material) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));

    private sealed record SentCopyEnvelope(
        int Version,
        string? MailboxId,
        string? MailboxAddress,
        string? SentFolderIdentity,
        string? ImmutableItemIdentity,
        string? InternetMessageIdentity,
        string? ConversationIdentity,
        string? ReplyChainIdentity,
        IReadOnlyList<string>? InReplyToIdentities,
        IReadOnlyList<Guid>? AuthoritativeCaseIdentities,
        DateTimeOffset SentDateTimeUtc,
        string? MimeSha256);

    private sealed record CursorEnvelope(int Version, IReadOnlyList<CursorItem> Items);

    private sealed record CursorItem(
        string SourceOccurrenceIdentity,
        string SourceSha256,
        string? CurrentLocationIdentity,
        bool Deleted,
        ApprovedSentItemProvenance? Provenance,
        string? MalformedReasonCode,
        string? OriginalSourceSha256 = null,
        string? ObservedSourceSha256 = null,
        string? EvidenceMarker = null);

    private sealed record PendingChange(
        ApprovedSentItemObservationKind ObservationKind,
        CursorItem Item);
}
