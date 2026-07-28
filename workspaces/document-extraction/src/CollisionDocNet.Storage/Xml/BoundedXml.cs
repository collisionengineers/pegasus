using System.Collections.Immutable;
using System.Xml;

namespace CollisionDocNet.Storage.Xml;

public sealed record BoundedXmlLimits
{
    public static BoundedXmlLimits Default { get; } = new();

    public int MaximumInputBytes { get; init; } = 16 * 1024 * 1024;

    public int MaximumDepth { get; init; } = 128;

    public int MaximumNodes { get; init; } = 1_000_000;

    public int MaximumAttributesPerElement { get; init; } = 1_024;

    public long MaximumTextCharacters { get; init; } = 64 * 1024 * 1024;
}

public enum BoundedXmlReadError
{
    None = 0,
    InputLimitExceeded,
    DepthLimitExceeded,
    NodeLimitExceeded,
    AttributeLimitExceeded,
    TextLimitExceeded,
    DtdProhibited,
    InvalidXml,
    Cancelled,
}

public enum BoundedXmlNodeKind
{
    ElementStart,
    ElementEnd,
    Text,
    CData,
    ProcessingInstruction,
    Comment,
}

public readonly record struct XmlSourceSpan(int LineNumber, int LinePosition);

public sealed record BoundedXmlAttributeValue(
    string Prefix,
    string LocalName,
    string NamespaceUri,
    string Value);

public sealed record BoundedXmlNode(
    BoundedXmlNodeKind Kind,
    int Depth,
    string Prefix,
    string LocalName,
    string NamespaceUri,
    string Value,
    ImmutableArray<BoundedXmlAttributeValue> Attributes,
    XmlSourceSpan Source);

public sealed record BoundedXmlDocument(ImmutableArray<BoundedXmlNode> Nodes);

public readonly record struct BoundedXmlReadResult(
    BoundedXmlDocument? Document,
    BoundedXmlReadError Error,
    string? Diagnostic)
{
    public bool IsSuccess => Error == BoundedXmlReadError.None && Document is not null;
}

/// <summary>
/// Reads XML as a namespace-aware, non-resolving event stream. DTDs and all
/// external resources are prohibited. Source positions are XML line/character
/// positions, not byte offsets.
/// </summary>
public static class BoundedXmlReader
{
    public static BoundedXmlReadResult Read(
        ReadOnlyMemory<byte> bytes,
        BoundedXmlLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= BoundedXmlLimits.Default;
        if (!AreValid(limits) || bytes.Length > limits.MaximumInputBytes)
        {
            return Failure(BoundedXmlReadError.InputLimitExceeded);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ContainsDocumentType(bytes.Span))
            {
                return Failure(BoundedXmlReadError.DtdProhibited);
            }

            using var stream = new CancellationAwareReadStream(bytes, cancellationToken);
            using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = false,
                CloseInput = false,
                ConformanceLevel = ConformanceLevel.Document,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,
                IgnoreWhitespace = false,
                MaxCharactersFromEntities = 0,
                // Input bytes are bounded separately and DTDs are rejected before parsing.
                // Track extracted text ourselves so markup does not consume the text budget.
                MaxCharactersInDocument = 0,
                XmlResolver = null,
            });

            var nodes = ImmutableArray.CreateBuilder<BoundedXmlNode>();
            long textCharacters = 0;
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.Depth > limits.MaximumDepth)
                {
                    return Failure(BoundedXmlReadError.DepthLimitExceeded);
                }

                if (nodes.Count >= limits.MaximumNodes)
                {
                    return Failure(BoundedXmlReadError.NodeLimitExceeded);
                }

                BoundedXmlNodeKind? kind = reader.NodeType switch
                {
                    XmlNodeType.Element => BoundedXmlNodeKind.ElementStart,
                    XmlNodeType.EndElement => BoundedXmlNodeKind.ElementEnd,
                    XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace => BoundedXmlNodeKind.Text,
                    XmlNodeType.CDATA => BoundedXmlNodeKind.CData,
                    XmlNodeType.ProcessingInstruction => BoundedXmlNodeKind.ProcessingInstruction,
                    XmlNodeType.Comment => BoundedXmlNodeKind.Comment,
                    _ => null,
                };
                if (kind is null)
                {
                    continue;
                }

                ImmutableArray<BoundedXmlAttributeValue> attributes = [];
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.AttributeCount > limits.MaximumAttributesPerElement)
                    {
                        return Failure(BoundedXmlReadError.AttributeLimitExceeded);
                    }

                    var attributeBuilder = ImmutableArray.CreateBuilder<BoundedXmlAttributeValue>(reader.AttributeCount);
                    while (reader.MoveToNextAttribute())
                    {
                        attributeBuilder.Add(new(reader.Prefix, reader.LocalName, reader.NamespaceURI, reader.Value));
                    }

                    reader.MoveToElement();
                    attributes = attributeBuilder.MoveToImmutable();
                }

                string value = kind is BoundedXmlNodeKind.Text or BoundedXmlNodeKind.CData or
                    BoundedXmlNodeKind.Comment or BoundedXmlNodeKind.ProcessingInstruction
                    ? reader.Value
                    : string.Empty;
                if (value.Length != 0)
                {
                    textCharacters = checked(textCharacters + value.Length);
                    if (textCharacters > limits.MaximumTextCharacters)
                    {
                        return Failure(BoundedXmlReadError.TextLimitExceeded);
                    }
                }

                var lineInfo = (IXmlLineInfo)reader;
                nodes.Add(new(
                    kind.Value,
                    reader.Depth,
                    reader.Prefix,
                    reader.LocalName,
                    reader.NamespaceURI,
                    value,
                    attributes,
                    new(lineInfo.LineNumber, lineInfo.LinePosition)));

                if (reader.NodeType == XmlNodeType.Element && reader.IsEmptyElement)
                {
                    if (nodes.Count >= limits.MaximumNodes)
                    {
                        return Failure(BoundedXmlReadError.NodeLimitExceeded);
                    }

                    nodes.Add(new(
                        BoundedXmlNodeKind.ElementEnd,
                        reader.Depth,
                        reader.Prefix,
                        reader.LocalName,
                        reader.NamespaceURI,
                        string.Empty,
                        [],
                        new(lineInfo.LineNumber, lineInfo.LinePosition)));
                }
            }

            return new(new(nodes.ToImmutable()), BoundedXmlReadError.None, null);
        }
        catch (OperationCanceledException)
        {
            return Failure(BoundedXmlReadError.Cancelled);
        }
        catch (XmlException exception)
        {
            return Failure(BoundedXmlReadError.InvalidXml,
                $"XML error at line {exception.LineNumber}, position {exception.LinePosition}.");
        }
        catch (OverflowException)
        {
            return Failure(BoundedXmlReadError.TextLimitExceeded);
        }
    }

    private static bool AreValid(BoundedXmlLimits limits) =>
        limits.MaximumInputBytes >= 0 && limits.MaximumDepth >= 0 &&
        limits.MaximumNodes > 0 && limits.MaximumAttributesPerElement >= 0 &&
        limits.MaximumTextCharacters >= 0;

    private static bool ContainsDocumentType(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> token = "<!DOCTYPE"u8;
        for (int index = 0; index <= bytes.Length - token.Length; index++)
        {
            int tokenIndex = 0;
            while (tokenIndex < token.Length &&
                ToUpperAscii(bytes[index + tokenIndex]) == token[tokenIndex])
            {
                tokenIndex++;
            }

            if (tokenIndex == token.Length)
            {
                return true;
            }
        }

        return ContainsUtf16DocumentType(bytes, littleEndian: true) ||
            ContainsUtf16DocumentType(bytes, littleEndian: false) ||
            ContainsUtf32DocumentType(bytes, littleEndian: true) ||
            ContainsUtf32DocumentType(bytes, littleEndian: false);
    }

    private static bool ContainsUtf16DocumentType(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        ReadOnlySpan<byte> token = "<!DOCTYPE"u8;
        int byteLength = token.Length * 2;
        for (int index = 0; index <= bytes.Length - byteLength; index++)
        {
            bool matches = true;
            for (int tokenIndex = 0; tokenIndex < token.Length; tokenIndex++)
            {
                int offset = index + (tokenIndex * 2);
                byte ascii = littleEndian ? bytes[offset] : bytes[offset + 1];
                byte zero = littleEndian ? bytes[offset + 1] : bytes[offset];
                if (zero != 0 || ToUpperAscii(ascii) != token[tokenIndex])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsUtf32DocumentType(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        ReadOnlySpan<byte> token = "<!DOCTYPE"u8;
        int byteLength = token.Length * 4;
        for (int index = 0; index <= bytes.Length - byteLength; index++)
        {
            bool matches = true;
            for (int tokenIndex = 0; tokenIndex < token.Length; tokenIndex++)
            {
                int offset = index + (tokenIndex * 4);
                int asciiOffset = littleEndian ? offset : offset + 3;
                if (ToUpperAscii(bytes[asciiOffset]) != token[tokenIndex] ||
                    bytes[offset + (littleEndian ? 1 : 0)] != 0 ||
                    bytes[offset + (littleEndian ? 2 : 1)] != 0 ||
                    bytes[offset + (littleEndian ? 3 : 2)] != 0)
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class CancellationAwareReadStream(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(Math.Min(buffer.Length, 4096), bytes.Length - _position);
            if (count == 0)
            {
                return 0;
            }

            bytes.Span.Slice(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static byte ToUpperAscii(byte value) =>
        value is >= (byte)'a' and <= (byte)'z'
            ? (byte)(value - ('a' - 'A'))
            : value;

    private static BoundedXmlReadResult Failure(BoundedXmlReadError error, string? diagnostic = null) =>
        new(null, error, diagnostic);
}
