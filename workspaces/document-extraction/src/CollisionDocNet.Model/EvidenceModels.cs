using System.Collections.Immutable;
using System.Text.Json.Serialization;
using CollisionDocNet.Core;

namespace CollisionDocNet.Model;

public enum DetectedContainer
{
    Unknown = 0,
    FlatBinary,
    CompoundFile,
    ZipPackage,
    InternetMessage,
}

public enum DetectedFormat
{
    Unknown = 0,
    Pdf,
    WordBinary,
    WordprocessingMl,
    OutlookItem,
    InternetMessage,
}

public enum ExtractionOutcome
{
    Complete = 0,
    Partial,
    Encrypted,
    Corrupt,
    UnsupportedFormat,
    UnsupportedFeature,
    ResourceLimitExceeded,
    Cancelled,
    TimedOut,
    TechnicalFailure,
}

public enum SourceLocationKind
{
    Unknown = 0,
    ByteRange,
    ContainerEntry,
    LogicalRange,
}

public sealed record SourceLocation
{
    public SourceLocation(
        SourceLocationKind kind,
        string domain,
        string path,
        long offset,
        long length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _ = checked(offset + length);

        Kind = kind;
        Domain = domain;
        Path = path;
        Offset = offset;
        Length = length;
    }

    public SourceLocationKind Kind { get; }
    public string Domain { get; }
    public string Path { get; }
    public long Offset { get; }
    public long Length { get; }
    public long End => checked(Offset + Length);
}

public sealed record ContentSegment
{
    public ContentSegment(int order, string kind, string text, SourceLocation? sourceLocation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(text);
        Order = order;
        Kind = kind;
        Text = text;
        SourceLocation = sourceLocation;
    }

    public int Order { get; }
    public string Kind { get; }
    public string Text { get; }
    public SourceLocation? SourceLocation { get; }
}

public sealed record MetadataEntry
{
    public MetadataEntry(int order, string name, string value, SourceLocation? sourceLocation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        Order = order;
        Name = name;
        Value = value;
        SourceLocation = sourceLocation;
    }

    public int Order { get; }
    public string Name { get; }
    public string Value { get; }
    public SourceLocation? SourceLocation { get; }
}

public sealed record Participant
{
    public Participant(
        int order,
        string role,
        string? displayName,
        string? address,
        SourceLocation? sourceLocation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        if (displayName is not null && string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A supplied display name cannot be blank.", nameof(displayName));
        }

        if (address is not null && string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A supplied address cannot be blank.", nameof(address));
        }

        if (displayName is null && address is null)
        {
            throw new ArgumentException("A participant requires a display name or address.");
        }

        Order = order;
        Role = role;
        DisplayName = displayName;
        Address = address;
        SourceLocation = sourceLocation;
    }

    public int Order { get; }
    public string Role { get; }
    public string? DisplayName { get; }
    public string? Address { get; }
    public SourceLocation? SourceLocation { get; }
}

public sealed record EvidenceRelationship
{
    public EvidenceRelationship(
        int order,
        string kind,
        string sourceIdentity,
        string targetIdentity,
        SourceLocation? sourceLocation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetIdentity);
        Order = order;
        Kind = kind;
        SourceIdentity = sourceIdentity;
        TargetIdentity = targetIdentity;
        SourceLocation = sourceLocation;
    }

    public int Order { get; }
    public string Kind { get; }
    public string SourceIdentity { get; }
    public string TargetIdentity { get; }
    public SourceLocation? SourceLocation { get; }
}

public sealed record ReviewAsset
{
    public ReviewAsset(
        string stableId,
        string kind,
        string? mediaType,
        string? originalName,
        ReadOnlyMemory<byte> content,
        SourceLocation? sourceLocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (IsWindowsReservedDeviceName(stableId)
            || stableId.Length > 97
            || stableId.Any(static character =>
                character is not (>= 'a' and <= 'z')
                && character is not (>= '0' and <= '9')
                && character != '-'))
        {
            throw new ArgumentException(
                "The stable identity must be a filename-safe lowercase ASCII token no longer than 97 characters.",
                nameof(stableId));
        }

        if (mediaType is not null && string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException("A supplied media type cannot be blank.", nameof(mediaType));
        }

        if (originalName is not null && string.IsNullOrWhiteSpace(originalName))
        {
            throw new ArgumentException("A supplied original name cannot be blank.", nameof(originalName));
        }

        StableId = stableId;
        Kind = kind;
        MediaType = mediaType;
        OriginalName = originalName;
        Content = ImmutableArray.Create(content.ToArray());
        ContentHash = Sha256Digest.Compute(content.Span);
        Length = content.Length;
        SourceLocation = sourceLocation;
    }

    public string StableId { get; }
    public string Kind { get; }
    public string? MediaType { get; }
    public string? OriginalName { get; }
    public Sha256Digest ContentHash { get; }
    public long Length { get; }
    public SourceLocation? SourceLocation { get; }

    [JsonIgnore]
    public ImmutableArray<byte> Content { get; }

    private static bool IsWindowsReservedDeviceName(string stableId)
    {
        int extensionSeparator = stableId.IndexOf('.', StringComparison.Ordinal);
        ReadOnlySpan<char> baseName = extensionSeparator < 0
            ? stableId.AsSpan()
            : stableId.AsSpan(0, extensionSeparator);

        if (baseName.Equals("con", StringComparison.OrdinalIgnoreCase)
            || baseName.Equals("prn", StringComparison.OrdinalIgnoreCase)
            || baseName.Equals("aux", StringComparison.OrdinalIgnoreCase)
            || baseName.Equals("nul", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return baseName.Length == 4
            && (baseName[..3].Equals("com", StringComparison.OrdinalIgnoreCase)
                || baseName[..3].Equals("lpt", StringComparison.OrdinalIgnoreCase))
            && baseName[3] is >= '1' and <= '9';
    }
}

public enum ExtractionIssueSeverity
{
    Information = 0,
    Warning,
    Error,
}

public sealed record ExtractionIssue
{
    public ExtractionIssue(
        int order,
        ExtractionIssueSeverity severity,
        string code,
        string message,
        SourceLocation? sourceLocation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Order = order;
        Severity = severity;
        Code = code;
        Message = message;
        SourceLocation = sourceLocation;
    }

    public int Order { get; }
    public ExtractionIssueSeverity Severity { get; }
    public string Code { get; }
    public string Message { get; }
    public SourceLocation? SourceLocation { get; }
}

public sealed record ResourceMeasurements
{
    public ResourceMeasurements(
        long inputBytes,
        long decodedBytes,
        int objects,
        int textCharacters,
        int assets,
        long assetBytes,
        int maximumNestingDepth,
        long elapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(decodedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(objects);
        ArgumentOutOfRangeException.ThrowIfNegative(textCharacters);
        ArgumentOutOfRangeException.ThrowIfNegative(assets);
        ArgumentOutOfRangeException.ThrowIfNegative(assetBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumNestingDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        InputBytes = inputBytes;
        DecodedBytes = decodedBytes;
        Objects = objects;
        TextCharacters = textCharacters;
        Assets = assets;
        AssetBytes = assetBytes;
        MaximumNestingDepth = maximumNestingDepth;
        ElapsedMilliseconds = elapsedMilliseconds;
    }

    public long InputBytes { get; }
    public long DecodedBytes { get; }
    public int Objects { get; }
    public int TextCharacters { get; }
    public int Assets { get; }
    public long AssetBytes { get; }
    public int MaximumNestingDepth { get; }

    /// <summary>
    /// Volatile diagnostic telemetry retained in memory but excluded from canonical semantic JSON.
    /// </summary>
    [JsonIgnore]
    public long ElapsedMilliseconds { get; }

    public static ResourceMeasurements FromSnapshot(
        ResourceBudgetSnapshot snapshot,
        TimeSpan elapsed) =>
        new(
            snapshot.InputBytes,
            snapshot.DecodedBytes,
            snapshot.Objects,
            snapshot.TextCharacters,
            snapshot.Assets,
            snapshot.AssetBytes,
            snapshot.MaximumNestingDepth,
            checked((long)elapsed.TotalMilliseconds));
}
