using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Intake;

public sealed class LocalApprovedInboxOptions
{
    public const string RequiredRuntimeProfile = "DevelopmentOffline";

    public LocalApprovedInboxOptions(
        string runtimeProfile,
        string mailboxId,
        string mailboxAddress,
        string rootPath)
    {
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

        MailboxId = mailboxId.Trim();
        MailboxAddress = normalizedAddress;
        RootPath = Path.GetFullPath(rootPath);
    }

    public string MailboxId { get; }

    public string MailboxAddress { get; }

    public string RootPath { get; }
}

internal sealed class LocalDurableApprovedInboxSource(LocalApprovedInboxOptions options)
    : IApprovedInboxSource
{
    private const int CursorVersion = 1;
    private const int MaximumMessageLength = 10 * 1024 * 1024;
    private const int MaximumFileNameLength = 260;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApprovedInboxPage> ReadAsync(
        ApprovedInboxPollLease lease,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessages);
        if (!string.Equals(lease.MailboxId, options.MailboxId, StringComparison.Ordinal)
            || !string.Equals(
                lease.MailboxAddress,
                options.MailboxAddress,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "The claimed mailbox is not the configured immutable local approved inbox.");
        }

        var root = new DirectoryInfo(options.RootPath);
        root.Refresh();
        if (!root.Exists)
        {
            throw new DirectoryNotFoundException(
                "The configured immutable local approved-inbox root does not exist.");
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
        var removedItem = cursor.Keys.FirstOrDefault(path => !filesByName.ContainsKey(path));
        if (removedItem is not null)
        {
            throw new InvalidDataException(
                "A previously observed immutable approved-inbox item is no longer present.");
        }

        var nextCursor = new Dictionary<string, string>(cursor, StringComparer.Ordinal);
        var messages = new List<ApprovedInboxMessage>(Math.Min(maximumMessages, files.Length));
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wasObserved = cursor.TryGetValue(file.Name, out var expectedHash);
            if (!wasObserved && messages.Count >= maximumMessages)
            {
                continue;
            }

            var read = await ReadImmutableFileAsync(file, retainContent: !wasObserved, cancellationToken);
            if (wasObserved)
            {
                if (!string.Equals(read.Hash, expectedHash, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "A previously observed immutable approved-inbox item has changed.");
                }

                continue;
            }

            if (read.Content is null)
            {
                continue;
            }

            nextCursor.Add(file.Name, read.Hash);
            messages.Add(new(
                CreateImmutableMessageId(file.Name, read.Hash),
                file.Name,
                read.Content,
                new DateTimeOffset(file.LastWriteTimeUtc)));
        }

        return new(messages, SerializeCursor(nextCursor));
    }

    private static async Task<ImmutableFileRead> ReadImmutableFileAsync(
        FileInfo file,
        bool retainContent,
        CancellationToken cancellationToken)
    {
        file.Refresh();
        if (!file.Exists || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Approved-inbox items must be regular immutable files.");
        }

        if (file.Name.Length is 0 or > MaximumFileNameLength)
        {
            throw new InvalidDataException(
                "An approved-inbox item has an invalid file name.");
        }

        await using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > MaximumMessageLength)
        {
            throw new InvalidDataException(
                "An approved-inbox item is empty or exceeds the 10 MB intake limit.");
        }

        if (!retainContent)
        {
            var streamedHash = await SHA256.HashDataAsync(stream, cancellationToken);
            return new(Convert.ToHexString(streamedHash), null);
        }

        var content = GC.AllocateUninitializedArray<byte>(checked((int)stream.Length));
        await stream.ReadExactlyAsync(content, cancellationToken);
        return new(Convert.ToHexString(SHA256.HashData(content)), content);
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

    private sealed record ImmutableFileRead(string Hash, byte[]? Content);
}
