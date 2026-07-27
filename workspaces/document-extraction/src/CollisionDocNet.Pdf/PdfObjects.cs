using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace CollisionDocNet.Pdf;

public readonly record struct PdfSpan(int Offset, int Length)
{
    public int End => checked(Offset + Length);
}

public abstract record PdfValue(PdfSpan Span);
public sealed record PdfNull(PdfSpan Span) : PdfValue(Span);
public sealed record PdfBoolean(bool Value, PdfSpan Span) : PdfValue(Span);
public sealed record PdfNumber(double Value, bool IsInteger, string Raw, PdfSpan Span) : PdfValue(Span);
public sealed record PdfName(string Value, PdfSpan Span) : PdfValue(Span);
public sealed record PdfString(byte[] Bytes, bool IsHex, PdfSpan Span) : PdfValue(Span);
public sealed record PdfArray(IReadOnlyList<PdfValue> Values, PdfSpan Span) : PdfValue(Span);
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Dictionary is the ISO 32000 object type name.")]
public sealed record PdfDictionary(IReadOnlyDictionary<string, PdfValue> Values, IReadOnlyList<string> DuplicateKeys, PdfSpan Span) : PdfValue(Span)
{
    public bool TryGet(string name, out PdfValue value) => Values.TryGetValue(name, out value!);
}
public sealed record PdfReference(int ObjectNumber, int Generation, PdfSpan Span) : PdfValue(Span);
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Stream is the ISO 32000 object type name.")]
public sealed record PdfStream(PdfDictionary Dictionary, byte[] EncodedBytes, PdfSpan Span) : PdfValue(Span);

public readonly record struct PdfObjectId(int Number, int Generation);
public sealed record PdfIndirectObject(PdfObjectId Id, PdfValue Value, PdfSpan Span);

public enum PdfIssueSeverity { Information, Warning, Error }
public sealed record PdfIssue(string Code, PdfIssueSeverity Severity, int Offset, string Message);

public sealed record PdfLimits
{
    public int MaxInputBytes { get; init; } = 10 * 1024 * 1024;
    public int MaxTokens { get; init; } = 1_000_000;
    public int MaxObjects { get; init; } = 100_000;
    public int MaxDepth { get; init; } = 128;
    public int MaxRevisions { get; init; } = 64;
    public int MaxDecodedStreamBytes { get; init; } = 64 * 1024 * 1024;
    public int MaxExpansionRatio { get; init; } = 200;
    public int MaxPages { get; init; } = 10_000;
    public int MaxOperators { get; init; } = 2_000_000;
    public int MaxTextCharacters { get; init; } = 64 * 1024 * 1024;
    public int MaxInlineImageBytes { get; init; } = 16 * 1024 * 1024;
    public int MaxRecoveryObjects { get; init; } = 10_000;
    public int MaxEvidenceItems { get; init; } = 100_000;
    public int MaxAssetBytes { get; init; } = 64 * 1024 * 1024;
}

public enum PdfParseOutcome { Complete, Partial, Encrypted, Corrupt, ResourceLimitExceeded, UnsupportedFormat, Cancelled, TechnicalFailure }

public sealed record PdfTextRun(int PageIndex, string Text, double X, double Y, int ContentOffset, string MappingSource);

public sealed record PdfEvidenceItem(
    string Kind,
    string Subtype,
    PdfObjectId? ObjectId,
    int Offset,
    IReadOnlyDictionary<string, string> Properties);

public sealed record PdfPassiveAsset(
    string StableId,
    string Kind,
    string? Name,
    string? MediaType,
    PdfObjectId ObjectId,
    byte[] Bytes,
    IReadOnlyDictionary<string, string> Properties);

public sealed record PdfSignatureEvidence(
    PdfObjectId ObjectId,
    IReadOnlyList<long> ByteRange,
    bool ByteRangeStructurallyValid,
    bool CoversWholeInput,
    string? SubFilter,
    int SignatureByteCount);

public sealed record PdfEncryptionEvidence(
    string Handler,
    int? Version,
    int? Revision,
    string? SubFilter,
    bool IsPublicKeyHandler);

public sealed record PdfPassiveEvidence(
    IReadOnlyList<PdfEvidenceItem> Items,
    IReadOnlyList<PdfPassiveAsset> Assets,
    IReadOnlyList<PdfSignatureEvidence> Signatures,
    PdfEncryptionEvidence? Encryption)
{
    public static PdfPassiveEvidence Empty { get; } = new([], [], [], null);
}

public sealed record PdfParseResult(
    PdfParseOutcome Outcome,
    string? HeaderVersion,
    string? CatalogVersion,
    IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> Objects,
    IReadOnlyList<PdfTextRun> TextRuns,
    IReadOnlyList<PdfIssue> Issues,
    bool UsedRecovery,
    PdfPassiveEvidence Evidence)
{
    internal static IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> Freeze(Dictionary<PdfObjectId, PdfIndirectObject> source) =>
        new ReadOnlyDictionary<PdfObjectId, PdfIndirectObject>(source);
}

public sealed class PdfParseException(string code, int offset, string message) : Exception(message)
{
    public string Code { get; } = code;
    public int Offset { get; } = offset;
}
