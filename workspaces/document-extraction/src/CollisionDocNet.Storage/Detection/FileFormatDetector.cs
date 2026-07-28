using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using CollisionDocNet.Storage.CompoundFile;
using CollisionDocNet.Storage.Opc;
using CollisionDocNet.Storage.Zip;

namespace CollisionDocNet.Storage.Detection;

public enum DetectedContainer
{
    Unknown = 0,
    Pdf,
    CompoundFile,
    Zip,
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
    EncryptedOpenXml,
}

public enum DetectionEvidenceKind
{
    Signature,
    Structure,
    RequiredPart,
    RequiredStream,
    HeaderGrammar,
}

public sealed record DetectionEvidence(DetectionEvidenceKind Kind, string Code, long Offset);

public sealed record FormatCandidate(
    DetectedContainer Container,
    DetectedFormat Format,
    int Confidence,
    ImmutableArray<DetectionEvidence> Evidence);

public sealed record FormatDetectionResult(
    ImmutableArray<FormatCandidate> Candidates,
    bool IsAmbiguous,
    bool FilenameHintMismatch,
    bool MediaTypeHintMismatch,
    string? DiagnosticCode)
{
    public DetectedFormat Format => Candidates.Length == 1 ? Candidates[0].Format : DetectedFormat.Unknown;

    public uint? DiagnosticLocation { get; init; }
}

public sealed record FileFormatDetectionLimits
{
    public static FileFormatDetectionLimits Default { get; } = new();

    public int MaximumInputBytes { get; init; } = 16 * 1024 * 1024;

    public CompoundFileReadLimits CompoundFile { get; init; } = CompoundFileReadLimits.Default;

    public BoundedZipLimits Zip { get; init; } = BoundedZipLimits.Default;

    public OpcLimits Opc { get; init; } = OpcLimits.Default;

    public int MaximumEmailHeaderBytes { get; init; } = 256 * 1024;

    public int MaximumEmailHeaderLines { get; init; } = 10_000;
}

/// <summary>
/// Passive structural detector for the five product formats and encrypted
/// OOXML wrappers. Filename and media-type hints are used only to report
/// mismatches. Multiple structural matches are retained as ambiguity evidence.
/// </summary>
public static class FileFormatDetector
{
    private static readonly byte[] CompoundSignature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];

    public static FormatDetectionResult Detect(
        ReadOnlyMemory<byte> bytes,
        string? filename = null,
        string? declaredMediaType = null,
        FileFormatDetectionLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= FileFormatDetectionLimits.Default;
        if (limits.MaximumInputBytes < 0 || bytes.Length > limits.MaximumInputBytes)
        {
            return new([], false, false, false, "input-limit-exceeded");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = ImmutableArray.CreateBuilder<FormatCandidate>();
            ReadOnlySpan<byte> span = bytes.Span;
            DetectPdf(span, candidates);
            CompoundFileReadResult? compoundFailure = DetectCompound(
                bytes, limits.CompoundFile, candidates, cancellationToken);
            DetectZip(bytes, limits, candidates, cancellationToken);
            DetectEmail(span, limits, candidates, cancellationToken);

            FormatCandidate[] candidateArray = candidates.ToArray();
            Array.Sort(candidateArray, static (left, right) =>
            {
                int format = left.Format.CompareTo(right.Format);
                return format != 0 ? format : right.Confidence.CompareTo(left.Confidence);
            });
            ImmutableArray<FormatCandidate> ordered = [.. candidateArray];
            DetectedFormat filenameHint = FormatFromFilename(filename);
            DetectedFormat mediaHint = FormatFromMediaType(declaredMediaType);
            bool filenameMismatch = ordered.Length != 0 && filenameHint != DetectedFormat.Unknown &&
                !ContainsFormat(ordered, filenameHint);
            bool mediaMismatch = ordered.Length != 0 && mediaHint != DetectedFormat.Unknown &&
                !ContainsFormat(ordered, mediaHint);
            string? diagnosticCode = ordered.Length == 0
                ? compoundFailure is CompoundFileReadResult failure
                    ? CompoundDiagnosticCode(failure)
                    : "unsupported-format"
                : ordered.Length > 1 ? "ambiguous-polyglot" : null;
            return new(
                ordered,
                ordered.Length > 1,
                filenameMismatch,
                mediaMismatch,
                diagnosticCode)
            {
                DiagnosticLocation = compoundFailure?.Location,
            };
        }
        catch (OperationCanceledException)
        {
            return new([], false, false, false, "cancelled");
        }
    }

    private static void DetectPdf(ReadOnlySpan<byte> bytes, ImmutableArray<FormatCandidate>.Builder candidates)
    {
        int headerLimit = Math.Min(bytes.Length, 1024);
        int header = bytes[..headerLimit].IndexOf("%PDF-"u8);
        if (header < 0 || header + 8 > bytes.Length ||
            bytes[header + 5] is < (byte)'1' or > (byte)'2' || bytes[header + 6] != (byte)'.' ||
            bytes[header + 7] is < (byte)'0' or > (byte)'9')
        {
            return;
        }

        int tailStart = Math.Max(0, bytes.Length - 2048);
        int eofRelative = bytes[tailStart..].LastIndexOf("%%EOF"u8);
        if (eofRelative < 0)
        {
            return;
        }

        candidates.Add(new(
            DetectedContainer.Pdf,
            DetectedFormat.Pdf,
            100,
            [
                new(DetectionEvidenceKind.Signature, "pdf-header", header),
                new(DetectionEvidenceKind.Structure, "pdf-eof", tailStart + eofRelative),
            ]));
    }

    private static CompoundFileReadResult? DetectCompound(
        ReadOnlyMemory<byte> bytes,
        CompoundFileReadLimits configuredLimits,
        ImmutableArray<FormatCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        if (!bytes.Span.StartsWith(CompoundSignature))
        {
            return null;
        }

        CompoundFileReadLimits limits = configuredLimits with
        {
            MaximumInputBytes = Math.Max(configuredLimits.MaximumInputBytes, bytes.Length),
        };
        CompoundFileReadResult result = CompoundFileReader.Read(bytes, limits, cancellationToken);
        if (!result.IsSuccess)
        {
            return result;
        }

        var rootStreams = new Dictionary<string, CompoundFileDirectoryEntry>(StringComparer.Ordinal);
        foreach (CompoundFileDirectoryEntry entry in result.File!.DirectoryEntries)
        {
            if (entry.ObjectType == CompoundFileObjectType.Stream && entry.ParentStreamId == 0)
            {
                rootStreams.Add(entry.Name, entry);
            }
        }
        bool encryptedPackage = rootStreams.ContainsKey("EncryptionInfo") &&
            rootStreams.ContainsKey("EncryptedPackage");
        bool word = rootStreams.TryGetValue("WordDocument", out CompoundFileDirectoryEntry? wordDocument) &&
            (rootStreams.ContainsKey("0Table") || rootStreams.ContainsKey("1Table")) &&
            HasWordFib(wordDocument.Content.AsSpan());
        bool outlook = rootStreams.TryGetValue(
            "__properties_version1.0", out CompoundFileDirectoryEntry? messageProperties) &&
            HasOutlookMessageProfile(result.File.DirectoryEntries, messageProperties.Content.AsSpan());

        if (encryptedPackage)
        {
            candidates.Add(CfbCandidate(DetectedFormat.EncryptedOpenXml, "encrypted-ooxml-streams"));
        }

        if (word)
        {
            candidates.Add(CfbCandidate(DetectedFormat.WordBinary, "word-binary-streams"));
        }

        if (outlook)
        {
            candidates.Add(CfbCandidate(DetectedFormat.OutlookItem, "outlook-property-streams"));
        }

        return null;
    }

    private static string CompoundDiagnosticCode(CompoundFileReadResult failure) =>
        failure.Error == CompoundFileReadError.HeaderInvalid
            ? $"cfb-header-{KebabCase(failure.HeaderError.ToString())}"
            : $"cfb-{KebabCase(failure.Error.ToString())}";

    private static string KebabCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (index != 0 && char.IsUpper(character))
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static bool HasWordFib(ReadOnlySpan<byte> wordDocument) =>
        wordDocument.Length >= 32 &&
        BinaryPrimitives.ReadUInt16LittleEndian(wordDocument) == 0xA5EC &&
        BinaryPrimitives.ReadUInt16LittleEndian(wordDocument[2..]) is >= 0x0065 and <= 0x0112;

    private static bool HasOutlookMessageProfile(
        ImmutableArray<CompoundFileDirectoryEntry> entries,
        ReadOnlySpan<byte> properties)
    {
        if (properties.Length < 32 || ((properties.Length - 32) & 15) != 0 ||
            ContainsNonZero(properties[..8]) || ContainsNonZero(properties.Slice(24, 8)))
        {
            return false;
        }

        uint declaredRecipients = BinaryPrimitives.ReadUInt32LittleEndian(properties[16..]);
        uint declaredAttachments = BinaryPrimitives.ReadUInt32LittleEndian(properties[20..]);
        uint actualRecipients = 0;
        uint actualAttachments = 0;
        foreach (CompoundFileDirectoryEntry entry in entries)
        {
            if (entry.ParentStreamId != 0 || entry.ObjectType != CompoundFileObjectType.Storage)
            {
                continue;
            }

            if (IsOutlookChildStorage(entry.Name, "__recip_version1.0_#"))
            {
                actualRecipients++;
            }
            else if (IsOutlookChildStorage(entry.Name, "__attach_version1.0_#"))
            {
                actualAttachments++;
            }
        }

        return declaredRecipients == actualRecipients && declaredAttachments == actualAttachments;
    }

    private static bool IsOutlookChildStorage(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length != prefix.Length + 8)
        {
            return false;
        }

        foreach (char value in name.AsSpan(prefix.Length))
        {
            if (!Uri.IsHexDigit(value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsNonZero(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static FormatCandidate CfbCandidate(DetectedFormat format, string code) =>
        new(
            DetectedContainer.CompoundFile,
            format,
            100,
            [
                new(DetectionEvidenceKind.Signature, "cfb-signature", 0),
                new(DetectionEvidenceKind.RequiredStream, code, 0),
            ]);

    private static void DetectZip(
        ReadOnlyMemory<byte> bytes,
        FileFormatDetectionLimits limits,
        ImmutableArray<FormatCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        BoundedZipLimits zipLimits = limits.Zip with
        {
            MaximumInputBytes = Math.Max(limits.Zip.MaximumInputBytes, bytes.Length),
        };
        BoundedZipReadResult zip = BoundedZipReader.Read(bytes, zipLimits, cancellationToken);
        if (!zip.IsSuccess)
        {
            return;
        }

        bool contentTypes = false;
        bool wordDocument = false;
        foreach (BoundedZipEntry entry in zip.Archive!.Entries)
        {
            contentTypes |= entry.Name == "[Content_Types].xml";
            wordDocument |= entry.Name == "word/document.xml";
        }
        if (!contentTypes || !wordDocument)
        {
            return;
        }

        OpcLimits opcLimits = limits.Opc with { Zip = zipLimits };
        OpcReadResult opc = OpcPackageReader.Read(bytes, opcLimits, cancellationToken);
        if (!opc.IsSuccess)
        {
            return;
        }

        bool typedWordDocument = false;
        foreach (OpcPart part in opc.Package!.Parts)
        {
            if (part.Name == "/word/document.xml" &&
                (part.ContentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase) ||
                 part.ContentType == "application/xml"))
            {
                typedWordDocument = true;
                break;
            }
        }

        if (!typedWordDocument)
        {
            return;
        }

        candidates.Add(new(
            DetectedContainer.Zip,
            DetectedFormat.WordprocessingMl,
            100,
            [
                new(DetectionEvidenceKind.Structure, "zip-central-directory", 0),
                new(DetectionEvidenceKind.RequiredPart, "opc-content-types", 0),
                new(DetectionEvidenceKind.RequiredPart, "word-document-part", 0),
            ]));
    }

    private static void DetectEmail(
        ReadOnlySpan<byte> bytes,
        FileFormatDetectionLimits limits,
        ImmutableArray<FormatCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        int scanLength = Math.Min(bytes.Length, limits.MaximumEmailHeaderBytes);
        ReadOnlySpan<byte> header = bytes[..scanLength];
        int delimiter = header.IndexOf("\r\n\r\n"u8);
        int delimiterLength = 4;
        if (delimiter < 0)
        {
            delimiter = header.IndexOf("\n\n"u8);
            delimiterLength = 2;
        }

        if (delimiter < 0)
        {
            return;
        }

        int recognised = 0;
        int lineCount = 0;
        int cursor = 0;
        bool previousHeader = false;
        while (cursor < delimiter)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++lineCount > limits.MaximumEmailHeaderLines)
            {
                return;
            }

            int newline = header[cursor..delimiter].IndexOf((byte)'\n');
            int end = newline < 0 ? delimiter : cursor + newline;
            ReadOnlySpan<byte> line = header[cursor..end];
            if (line.EndsWith("\r"u8))
            {
                line = line[..^1];
            }

            if (line.Length > 0 && (line[0] == (byte)' ' || line[0] == (byte)'\t'))
            {
                if (!previousHeader)
                {
                    return;
                }
            }
            else
            {
                int colon = line.IndexOf((byte)':');
                if (colon <= 0 || !IsHeaderName(line[..colon]))
                {
                    return;
                }

                previousHeader = true;
                ReadOnlySpan<byte> name = line[..colon];
                if (AsciiEquals(name, "from") || AsciiEquals(name, "to") ||
                    AsciiEquals(name, "subject") || AsciiEquals(name, "date") ||
                    AsciiEquals(name, "message-id") || AsciiEquals(name, "mime-version") ||
                    AsciiEquals(name, "content-type"))
                {
                    recognised++;
                }
            }

            cursor = end + 1;
        }

        if (recognised < 2)
        {
            return;
        }

        candidates.Add(new(
            DetectedContainer.InternetMessage,
            DetectedFormat.InternetMessage,
            90,
            [
                new(DetectionEvidenceKind.HeaderGrammar, "rfc5322-header-block", 0),
                new(DetectionEvidenceKind.Structure, "header-body-delimiter", delimiter + delimiterLength),
            ]));
    }

    private static bool IsHeaderName(ReadOnlySpan<byte> name)
    {
        foreach (byte value in name)
        {
            if (value is < 33 or > 126 || value == (byte)':')
            {
                return false;
            }
        }

        return true;
    }

    private static bool AsciiEquals(ReadOnlySpan<byte> bytes, string expected)
    {
        if (bytes.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = bytes[index];
            if (value is >= (byte)'A' and <= (byte)'Z')
            {
                value += (byte)('a' - 'A');
            }

            if (value != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static DetectedFormat FormatFromFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return DetectedFormat.Unknown;
        }

        string extension = Path.GetExtension(filename);
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.Pdf;
        if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.WordBinary;
        if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.WordprocessingMl;
        if (extension.Equals(".msg", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.OutlookItem;
        if (extension.Equals(".eml", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.InternetMessage;
        return DetectedFormat.Unknown;
    }

    private static DetectedFormat FormatFromMediaType(string? mediaType)
    {
        ReadOnlySpan<char> value = mediaType.AsSpan();
        int separator = value.IndexOf(';');
        if (separator >= 0) value = value[..separator];
        value = value.Trim();
        if (value.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.Pdf;
        if (value.Equals("application/msword", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.WordBinary;
        if (value.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.WordprocessingMl;
        if (value.Equals("application/vnd.ms-outlook", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.OutlookItem;
        if (value.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase)) return DetectedFormat.InternetMessage;
        return DetectedFormat.Unknown;
    }

    private static bool ContainsFormat(ImmutableArray<FormatCandidate> candidates, DetectedFormat format)
    {
        foreach (FormatCandidate candidate in candidates)
        {
            if (candidate.Format == format)
            {
                return true;
            }
        }

        return false;
    }
}
