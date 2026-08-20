using System.Collections.Immutable;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Msg;

internal enum MsgItemKind
{
    Generic,
    Mail,
    Report,
    Meeting,
    Calendar,
    Contact,
    DistributionList,
    Task,
    Note,
    Journal,
}

internal enum MsgReadOutcome
{
    Complete,
    Partial,
    Encrypted,
    Corrupt,
    ResourceLimitExceeded,
    Cancelled,
}

internal enum MapiValueKind
{
    Raw,
    Integer16,
    Integer32,
    Integer64,
    Real32,
    Real64,
    Currency,
    FloatingTime,
    Boolean,
    Error,
    FileTime,
    Identifier,
    Text,
    Binary,
    OpaqueObject,
}

internal sealed record NamedPropertyIdentity(Guid PropertySet, uint? NumericName, string? StringName);

internal sealed record MapiValue(
    int Index,
    MapiValueKind Kind,
    object? Decoded,
    ImmutableArray<byte> RawBytes);

internal sealed record MapiProperty(
    uint OwnerStorageId,
    ushort PropertyId,
    ushort PropertyType,
    uint Flags,
    NamedPropertyIdentity? NamedIdentity,
    ImmutableArray<MapiValue> Values);

internal sealed record MsgRecipient(
    uint StorageId,
    uint SourceOrder,
    string Role,
    string? DisplayName,
    string? EmailAddress,
    ImmutableArray<MapiProperty> Properties);

internal sealed record MsgBodySet(
    string? PlainText,
    string? HtmlText,
    ImmutableArray<byte> HtmlBytes,
    string? RtfText,
    ImmutableArray<byte> RtfBytes,
    string? CanonicalText,
    string CanonicalSource);

internal sealed record MsgAttachment(
    uint StorageId,
    uint SourceOrder,
    int Method,
    string? FileName,
    string? DisplayName,
    string? MediaType,
    string? ContentId,
    string? ContentLocation,
    bool IsInline,
    ImmutableArray<byte> Content,
    string? PassiveReference,
    MsgDocument? EmbeddedMessage,
    ImmutableArray<MsgPassiveStorage> PassiveStorages,
    ImmutableArray<MapiProperty> Properties);

internal sealed record MsgPassiveStorage(
    uint StorageId,
    string StorageName,
    int ChildObjectCount);

internal sealed record MsgSemanticProjection(
    MsgItemKind Kind,
    string MessageClass,
    ImmutableDictionary<string, string> Fields);

internal sealed record MsgIssue(string Code, string Message, uint? StorageId = null, ushort? PropertyId = null);

internal sealed record MsgDocument(
    MsgReadOutcome Outcome,
    MsgSemanticProjection Projection,
    ImmutableArray<MapiProperty> Properties,
    ImmutableArray<MsgRecipient> Recipients,
    MsgBodySet Bodies,
    ImmutableArray<MsgAttachment> Attachments,
    ImmutableArray<MsgIssue> Issues);

internal sealed record MsgReadLimits(
    int MaximumProperties,
    int MaximumRecipients,
    int MaximumAttachments,
    int MaximumNestingDepth,
    long MaximumDecodedBytes,
    int MaximumRtfBytes,
    int MaximumValues = 200_000,
    int MaximumModelItems = 50_000,
    int MaximumChildStorages = 50_000)
{
    public static MsgReadLimits Default { get; } = new(
        100_000,
        10_000,
        10_000,
        8,
        100 * 1024 * 1024,
        20 * 1024 * 1024,
        200_000,
        50_000,
        50_000);
}
