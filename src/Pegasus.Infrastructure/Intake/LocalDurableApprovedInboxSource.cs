using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Intake;

public sealed class LocalApprovedInboxOptions : Pegasus.Infrastructure.Email.IApprovedInboxSourceSettings
{
    public const string RequiredRuntimeProfile = "DevelopmentOffline";

    /// <param name="maximumContentLength">
    /// The mailbox envelope bound this adapter materializes up to. It must be
    /// the bound Core enforces, because a message this adapter streams past is
    /// one Core has to recognize as a valid oversize rejection. It is a
    /// parameter only so that a test can exercise both sides of the boundary
    /// without writing the production number to disk.
    /// </param>
    public LocalApprovedInboxOptions(
        string runtimeProfile,
        string mailboxId,
        string mailboxAddress,
        string rootPath,
        string inboxFolderIdentity = "inbox",
        long maximumContentLength = IntakeEnvelopeLimits.MaximumMailboxContentLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumContentLength);
        if (!string.Equals(runtimeProfile, RequiredRuntimeProfile, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The immutable local approved-inbox adapter is disabled outside DevelopmentOffline.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (mailboxId.Length > 100)
        {
            throw new ArgumentException("The approved mailbox identity must be 100 characters or fewer.", nameof(mailboxId));
        }

        var normalizedAddress = mailboxAddress.Trim();
        if (normalizedAddress.Length > 320
            || !MailAddress.TryCreate(normalizedAddress, out var parsedAddress)
            || !string.Equals(parsedAddress.Address, normalizedAddress, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The approved mailbox address is invalid.", nameof(mailboxAddress));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(inboxFolderIdentity);
        var normalizedFolder = inboxFolderIdentity.Trim();
        if (normalizedFolder.Length > 200
            || !string.Equals(
                Path.GetFileName(normalizedFolder),
                normalizedFolder,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The approved Inbox folder identity must be a single path segment.",
                nameof(inboxFolderIdentity));
        }

        MailboxId = mailboxId.Trim();
        MailboxAddress = normalizedAddress;
        RootPath = Path.GetFullPath(rootPath);
        InboxFolderIdentity = normalizedFolder;
        MaximumContentLength = maximumContentLength;
    }

    public string MailboxId { get; }

    public string MailboxAddress { get; }

    /// <summary>
    /// The root of the local mailbox estate. Each mailbox reads one folder directly
    /// beneath it, named by the folder identity its lease carries, so the offline
    /// profile can hold more than one approved mailbox.
    /// </summary>
    public string RootPath { get; }

    public string InboxFolderIdentity { get; }

    public long MaximumContentLength { get; }
}

internal sealed class LocalDurableApprovedInboxSource(
    LocalApprovedInboxOptions options,
    IIntakeQuarantineArtifactStore quarantineArtifactStore)
    : IApprovedInboxSource
{
    private const int CursorVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApprovedInboxPage> ReadAsync(
        ApprovedInboxPollLease lease,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessages);

        // Which mailbox may be read is settled by the approved estate, not by this
        // adapter. What it still owns is refusing to leave its own root: the folder
        // identity must be one plain segment that resolves directly beneath it.
        var folder = lease.InboxFolderIdentity;
        if (string.IsNullOrWhiteSpace(folder)
            || !string.Equals(Path.GetFileName(folder), folder, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The approved Inbox folder identity is not a single local folder segment.");
        }

        var rootPath = Path.GetFullPath(options.RootPath);
        var directory = Path.GetFullPath(Path.Combine(rootPath, folder));
        if (!string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(directory) ?? string.Empty),
                Path.TrimEndingDirectorySeparator(rootPath),
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The approved Inbox folder resolved outside the immutable local root.");
        }

        var root = new DirectoryInfo(directory);
        root.Refresh();
        if (!root.Exists)
        {
            throw new DirectoryNotFoundException(
                "The configured immutable local approved-inbox folder does not exist.");
        }

        if ((root.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "The immutable local approved-inbox root cannot be a reparse point.");
        }

        var cursor = ParseCursor(lease.Cursor);
        var files = root
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(file => string.Equals(file.Extension, ".eml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .ToArray();
        var filesByName = files.ToDictionary(file => file.Name, StringComparer.Ordinal);
        var nextCursor = new Dictionary<string, string>(cursor, StringComparer.Ordinal);
        var serializedNextCursor = SerializeCursor(nextCursor);
        var messageCapacity = (int)Math.Min(
            maximumMessages,
            (long)files.Length + cursor.Count);
        var messages = new List<ApprovedInboxMessage>(messageCapacity);

        foreach (var removedItem in cursor
                     .Where(item => !filesByName.ContainsKey(item.Key))
                     .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (messages.Count == maximumMessages)
            {
                break;
            }

            nextCursor.Remove(removedItem.Key);
            serializedNextCursor = SerializeCursor(nextCursor);
            messages.Add(new(
                CreateImmutableMessageId(removedItem.Key, removedItem.Value),
                removedItem.Key,
                ReadOnlyMemory<byte>.Empty,
                DateTimeOffset.UnixEpoch,
                serializedNextCursor)
            {
                SourceRejection = new(
                    "immutable_source_missing",
                    null,
                    null,
                    null,
                    removedItem.Value,
                    "missing")
            });
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wasObserved = cursor.TryGetValue(file.Name, out var expectedHash);
            if (!wasObserved && messages.Count >= maximumMessages)
            {
                continue;
            }

            ImmutableFileRead read;
            try
            {
                read = await ReadImmutableFileAsync(
                    file,
                    retainContent: !wasObserved,
                    cancellationToken);
            }
            catch (ApprovedInboxSourceMissingException)
            {
                if (!wasObserved || messages.Count >= maximumMessages)
                {
                    continue;
                }

                nextCursor.Remove(file.Name);
                serializedNextCursor = SerializeCursor(nextCursor);
                messages.Add(new(
                    CreateImmutableMessageId(file.Name, expectedHash!),
                    file.Name,
                    ReadOnlyMemory<byte>.Empty,
                    DateTimeOffset.UnixEpoch,
                    serializedNextCursor)
                {
                    SourceRejection = new(
                        "immutable_source_missing",
                        null,
                        null,
                        null,
                        expectedHash,
                        "missing")
                });
                continue;
            }

            if (wasObserved)
            {
                if (string.Equals(read.Hash, expectedHash, StringComparison.Ordinal))
                {
                    continue;
                }

                if (messages.Count >= maximumMessages)
                {
                    continue;
                }

                var retained = await RetainChangedSourceAsync(
                    file,
                    read.SourceLength,
                    read.Hash,
                    cancellationToken);
                nextCursor[file.Name] = retained.ContentHash;
                serializedNextCursor = SerializeCursor(nextCursor);
                messages.Add(new(
                    CreateImmutableMessageId(file.Name, expectedHash!),
                    file.Name,
                    ReadOnlyMemory<byte>.Empty,
                    new DateTimeOffset(file.LastWriteTimeUtc),
                    serializedNextCursor)
                {
                    SourceRejection = new(
                        "immutable_source_changed",
                        retained.ContentLength,
                        retained.ContentHash,
                        retained.StorageKey,
                        expectedHash,
                        "changed")
                });
                continue;
            }

            nextCursor.Add(file.Name, read.Hash);
            serializedNextCursor = SerializeCursor(nextCursor);
            messages.Add(new(
                CreateImmutableMessageId(file.Name, read.Hash),
                file.Name,
                new ReadOnlyMemory<byte>(read.Content ?? Array.Empty<byte>()),
                new DateTimeOffset(file.LastWriteTimeUtc),
                serializedNextCursor)
            {
                SourceRejection = read.RetentionKey is null
                    ? null
                    : new(
                        "message_too_large",
                        read.SourceLength,
                        read.Hash,
                        read.RetentionKey),
                // Only the branch that materialised the content can read it. A
                // message already observed is re-hashed without being retained, and
                // one the adapter rejected has no content to read, so neither
                // carries display metadata.
                RetainedMetadata = read.Content is null || read.RetentionKey is not null
                    ? null
                    : await ReadRetainedMetadataAsync(
                        read.Content,
                        folder,
                        cancellationToken)
            });
        }

        return new(messages, serializedNextCursor);
    }

    /// <summary>
    /// The display facts of a newly observed local message, read from the bytes the
    /// poll already holds.
    /// </summary>
    /// <remarks>
    /// Local files have no provider identities: the folder is the lease's own
    /// folder, the conversation is whatever the MIME References chain says, and read
    /// state is false because a file on disk has never been read by anybody.
    /// </remarks>
    private static async Task<RetainedMailboxMessageMetadata?> ReadRetainedMetadataAsync(
        byte[] content,
        string folderIdentity,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream(content, writable: false);
            var display = await LocalEmailDisplayReader.ReadAsync(stream, cancellationToken);
            return new(
                folderIdentity,
                display.ThreadIdentity,
                display.MessageIdentity,
                display.SenderAddress,
                display.SenderDisplayName,
                display.ToAddresses ?? [],
                display.CcAddresses ?? [],
                display.ReplyToAddresses,
                string.IsNullOrWhiteSpace(display.Subject) ? null : display.Subject,
                string.IsNullOrWhiteSpace(display.Body) ? null : display.Body,
                display.Attachments ?? [],
                IsRead: false);
        }
        catch (FormatException)
        {
            // An unparseable .eml still becomes an intake source, and the reader
            // behind that records what it could not read. Refusing the whole poll
            // over a display view nobody has asked for yet would be worse.
            return null;
        }
    }

    private async Task<ImmutableFileRead> ReadImmutableFileAsync(
        FileInfo file,
        bool retainContent,
        CancellationToken cancellationToken)
    {
        file.Refresh();
        if (!file.Exists)
        {
            throw new ApprovedInboxSourceMissingException();
        }

        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Approved-inbox items must be regular immutable files.");
        }

        await using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var sourceLength = stream.Length;
        if (!retainContent)
        {
            var streamedHash = await SHA256.HashDataAsync(stream, cancellationToken);
            return new(Convert.ToHexString(streamedHash), null, sourceLength, null);
        }

        if (sourceLength > options.MaximumContentLength)
        {
            var retained = await RetainStreamAsync(stream, sourceLength, cancellationToken);
            return new(
                retained.ContentHash,
                null,
                retained.ContentLength,
                retained.StorageKey);
        }

        var content = GC.AllocateUninitializedArray<byte>(checked((int)sourceLength));
        await stream.ReadExactlyAsync(content, cancellationToken);
        return new(
            Convert.ToHexString(SHA256.HashData(content)),
            content,
            sourceLength,
            null);
    }

    private async Task<IntakeQuarantineArtifact> RetainChangedSourceAsync(
        FileInfo file,
        long expectedLength,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        file.Refresh();
        if (!file.Exists)
        {
            throw new ApprovedInboxSourceMissingException();
        }

        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Approved-inbox items must be regular immutable files.");
        }

        await using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var retained = await RetainStreamAsync(stream, stream.Length, cancellationToken);
        if (retained.ContentLength != expectedLength
            || !string.Equals(retained.ContentHash, expectedHash, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return retained;
    }

    private async Task<IntakeQuarantineArtifact> RetainStreamAsync(
        Stream stream,
        long sourceLength,
        CancellationToken cancellationToken)
    {
        try
        {
            var retained = await quarantineArtifactStore.StoreStreamAsync(
                stream,
                sourceLength,
                cancellationToken);
            await quarantineArtifactStore.VerifyAsync(retained, cancellationToken);
            return retained;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            throw new IntakeArtifactRetentionException(exception);
        }
    }

    private static Dictionary<string, string> ParseCursor(string? value)
    {
        if (value is null)
        {
            return new(StringComparer.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("The approved-inbox cursor is invalid.");
        }

        CursorEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<CursorEnvelope>(value, JsonOptions)
                ?? throw new InvalidDataException("The approved-inbox cursor is missing.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The approved-inbox cursor is malformed.", exception);
        }

        if (envelope.Version != CursorVersion || envelope.Items is null)
        {
            throw new InvalidDataException(
                $"Unsupported approved-inbox cursor version '{envelope.Version}'.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in envelope.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Path)
                || !string.Equals(Path.GetFileName(item.Path), item.Path, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(item.Hash)
                || item.Hash.Length != 64
                || item.Hash.Any(character => !char.IsAsciiHexDigit(character))
                || !result.TryAdd(item.Path, item.Hash.ToUpperInvariant()))
            {
                throw new InvalidDataException("The approved-inbox cursor contains an invalid item.");
            }
        }

        return result;
    }

    private static string SerializeCursor(IReadOnlyDictionary<string, string> items) =>
        JsonSerializer.Serialize(
            new CursorEnvelope(
                CursorVersion,
                items
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new CursorItem(item.Key, item.Value))
                    .ToArray()),
            JsonOptions);

    private static string CreateImmutableMessageId(string fileName, string contentHash)
    {
        var identity = $"{fileName.Length}:{fileName}{contentHash}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private sealed record CursorEnvelope(int Version, IReadOnlyList<CursorItem> Items);

    private sealed record CursorItem(string Path, string Hash);

    private sealed record ImmutableFileRead(
        string Hash,
        byte[]? Content,
        long SourceLength,
        string? RetentionKey);

    private sealed class ApprovedInboxSourceMissingException : IOException
    {
    }
}
