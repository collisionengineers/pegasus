using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using CollisionDocNet.Core;
using CollisionDocNet.Model;

namespace CollisionDocNet.Email;

/// <summary>A bounded, passive RFC 5322 and MIME evidence extractor.</summary>
internal sealed class EmlExtractor
{
    public const string ExtractorVersion = "collisiondocnet-eml/0.1";
    public const string SpecificationIdentity = "RFC5322-RFC2045-RFC2046-RFC2047-RFC2183-RFC2231-RFC6532/2026-07-23";

    public static ExtractionResult Extract(
        ReadOnlyMemory<byte> source,
        ResourceLimits limits,
        EmlExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(limits);
        options ??= EmlExtractionOptions.Strict;
        var state = new ParseState(source, limits, options, cancellationToken);
        return state.ParseRoot();
    }

    private sealed class ParseState
    {
        private readonly ReadOnlyMemory<byte> _source;
        private readonly ResourceBudget _budget;
        private readonly ExtractionControl _control;
        private readonly EmlExtractionOptions _options;
        private readonly bool _chargeInput;
        private readonly List<ContentSegment> _content = [];
        private readonly List<MetadataEntry> _metadata = [];
        private readonly List<Participant> _participants = [];
        private readonly List<EvidenceRelationship> _relationships = [];
        private readonly List<ReviewAsset> _assets = [];
        private readonly List<ExtractionIssue> _issues = [];
        private readonly List<ExtractionResult> _nested = [];
        private readonly HashSet<string> _reportedIssueKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _reportedRelationshipKeys = new(StringComparer.Ordinal);
        private ExtractionOutcome _outcome = ExtractionOutcome.Complete;
        private int _order;

        public ParseState(
            ReadOnlyMemory<byte> source,
            ResourceLimits limits,
            EmlExtractionOptions options,
            CancellationToken cancellationToken)
        {
            _source = source;
            _budget = new ResourceBudget(limits);
            _control = new ExtractionControl(limits.MaxElapsed, cancellationToken: cancellationToken);
            _options = options;
            _chargeInput = true;
        }

        private ParseState(
            ReadOnlyMemory<byte> source,
            ResourceBudget budget,
            ExtractionControl control,
            EmlExtractionOptions options)
        {
            _source = source;
            _budget = budget;
            _control = control;
            _options = options;
            _chargeInput = false;
        }

        public ExtractionResult ParseRoot(int depth = 0)
        {
            if (_chargeInput && !_budget.TryCharge(ResourceKind.InputBytes, _source.Length))
            {
                SetOutcome(ExtractionOutcome.ResourceLimitExceeded);
                AddIssue("EML_LIMIT_INPUT", "The source exceeds the configured input-byte limit.", "1", 0, _source.Length, true);
            }
            else
            {
                ParseEntity(_source, "1", depth, isRoot: true, absoluteOffset: 0);
            }

            return CreateResult(_source, _outcome, _content, _metadata, _participants, _relationships, _assets, _issues, _nested);
        }

        private void ParseEntity(ReadOnlyMemory<byte> entity, string partPath, int depth, bool isRoot, int absoluteOffset)
        {
            if (!CheckControl() || !_budget.TryObserveNestingDepth(depth))
            {
                if (_outcome == ExtractionOutcome.Complete)
                {
                    SetOutcome(ExtractionOutcome.ResourceLimitExceeded);
                    AddIssue("EML_LIMIT_DEPTH", "The MIME nesting-depth limit was exceeded.", partPath, absoluteOffset, entity.Length, true);
                }

                return;
            }

            if (!_budget.TryCharge(ResourceKind.Objects, 1))
            {
                SetLimited("EML_LIMIT_OBJECTS", "The MIME object-count limit was exceeded.", partPath, absoluteOffset, entity.Length);
                return;
            }

            HeaderBlock headerBlock = ParseHeaders(entity, partPath, isRoot, absoluteOffset);
            if (!headerBlock.IsValid)
            {
                SetOutcome(ExtractionOutcome.Corrupt);
                return;
            }

            string contentTypeValue = LastHeader(headerBlock.Headers, "content-type") ?? (isRoot ? "text/plain" : "text/plain");
            MediaTypeValue contentType = ParseMediaType(contentTypeValue, partPath, absoluteOffset);
            string transferEncoding = (LastHeader(headerBlock.Headers, "content-transfer-encoding") ?? "7bit").Trim().ToLowerInvariant();
            string dispositionValue = LastHeader(headerBlock.Headers, "content-disposition") ?? string.Empty;
            MediaTypeValue disposition = ParseMediaType(dispositionValue, partPath, absoluteOffset);
            ReadOnlyMemory<byte> body = entity[headerBlock.BodyOffset..];
            int bodyAbsoluteOffset = checked(absoluteOffset + headerBlock.BodyOffset);

            if (IsEncrypted(contentType.Type))
            {
                AddAsset(body.Span, contentType.Type, GetFileName(contentType, disposition), partPath, bodyAbsoluteOffset, body.Length);
                SetOutcome(ExtractionOutcome.Encrypted);
                AddIssue("EML_PROTECTED", "An encrypted MIME payload was retained without decryption.", partPath, bodyAbsoluteOffset, body.Length, false);
                return;
            }

            if (contentType.Type.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
            {
                if (contentType.Type.Equals("multipart/signed", StringComparison.OrdinalIgnoreCase))
                {
                    SetPartial("EML_SIGNED_UNVERIFIED", "The multipart signature was retained but was not cryptographically verified.", partPath, absoluteOffset, entity.Length);
                }

                ParseMultipart(body, contentType, partPath, depth, bodyAbsoluteOffset);
                return;
            }

            if (contentType.Type.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase) ||
                contentType.Type.Equals("message/global", StringComparison.OrdinalIgnoreCase))
            {
                byte[]? decodedNested = DecodeTransfer(body.Span, transferEncoding, partPath, bodyAbsoluteOffset);
                if (decodedNested is null || IsTerminal(_outcome))
                {
                    return;
                }

                ParseNested(decodedNested, partPath, depth + 1, bodyAbsoluteOffset, body.Length);
                return;
            }

            if (contentType.Type.Equals("message/partial", StringComparison.OrdinalIgnoreCase) ||
                contentType.Type.Equals("message/external-body", StringComparison.OrdinalIgnoreCase))
            {
                AddAsset(body.Span, contentType.Type, GetFileName(contentType, disposition), partPath, bodyAbsoluteOffset, body.Length);
                SetPartial("EML_MESSAGE_SPECIAL", $"The {contentType.Type} entity was retained without external assembly or retrieval.", partPath, bodyAbsoluteOffset, body.Length);
                return;
            }

            if (IsUnsupportedMessageSubtype(contentType.Type) || contentType.Type.Equals("application/ms-tnef", StringComparison.OrdinalIgnoreCase))
            {
                AddAsset(body.Span, contentType.Type, GetFileName(contentType, disposition), partPath, bodyAbsoluteOffset, body.Length);
                SetPartial("EML_SUBTYPE_UNSUPPORTED", $"The {SanitizeToken(contentType.Type)} entity was retained without semantic interpretation.", partPath, bodyAbsoluteOffset, body.Length);
                return;
            }

            byte[]? decoded = DecodeTransfer(body.Span, transferEncoding, partPath, bodyAbsoluteOffset);
            if (decoded is null || IsTerminal(_outcome))
            {
                return;
            }

            bool attachment = disposition.Type.Equals("attachment", StringComparison.OrdinalIgnoreCase) ||
                !contentType.Type.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
            if (attachment)
            {
                AddAsset(decoded, contentType.Type, GetFileName(contentType, disposition), partPath, bodyAbsoluteOffset, body.Length);
                AddContentIdRelationship(headerBlock.Headers, partPath);
                return;
            }

            if (contentType.Type.Equals("text/plain", StringComparison.OrdinalIgnoreCase) &&
                contentType.Parameters.TryGetValue("format", out string? format) &&
                format.Equals("flowed", StringComparison.OrdinalIgnoreCase))
            {
                SetPartial("EML_FLOWED_UNSUPPORTED", "RFC 3676 flowed text was extracted without flowed-line semantic reconstruction.", partPath, bodyAbsoluteOffset, body.Length);
            }

            string text = DecodeText(decoded, contentType.Parameters.GetValueOrDefault("charset"), partPath, bodyAbsoluteOffset, body.Length);
            if (contentType.Type.Equals("text/html", StringComparison.OrdinalIgnoreCase))
            {
                text = ExtractInertHtmlText(text, partPath, bodyAbsoluteOffset, body.Length);
                if (IsTerminal(_outcome))
                {
                    return;
                }
            }

            if (!_budget.TryCharge(ResourceKind.TextCharacters, text.Length))
            {
                SetLimited("EML_LIMIT_TEXT", "The extracted-text limit was exceeded.", partPath, bodyAbsoluteOffset, body.Length);
                return;
            }

            _content.Add(new ContentSegment(
                _order++,
                contentType.Type.Equals("text/html", StringComparison.OrdinalIgnoreCase) ? "email-html-text" : "email-text",
                DeterministicText.Normalize(text),
                Location(partPath, bodyAbsoluteOffset, body.Length)));
        }

        private HeaderBlock ParseHeaders(ReadOnlyMemory<byte> entity, string partPath, bool projectMetadata, int absoluteOffset)
        {
            ReadOnlySpan<byte> bytes = entity.Span;
            var headers = new List<Header>();
            int cursor = 0;
            int nextControlCheck = 0;
            Header? current = null;
            while (cursor < bytes.Length)
            {
                if (cursor >= nextControlCheck && !CheckControl(partPath, checked(absoluteOffset + cursor)))
                {
                    return new HeaderBlock(headers, cursor, false);
                }

                if (cursor >= nextControlCheck)
                {
                    nextControlCheck = checked(cursor + 4096);
                }

                int lineStart = cursor;
                int lineEnd = FindLineEnd(bytes, cursor, out int terminatorLength);
                int lineLength = lineEnd - lineStart;
                if (lineLength > _options.MaximumHeaderLineBytes)
                {
                    AddIssue("EML_HEADER_LINE_LIMIT", "A header line exceeds the configured limit.", partPath, checked(absoluteOffset + lineStart), lineLength, true);
                    SetOutcome(ExtractionOutcome.ResourceLimitExceeded);
                    return new HeaderBlock(headers, lineEnd + terminatorLength, false);
                }

                ReadOnlySpan<byte> line = bytes[lineStart..lineEnd];
                cursor = lineEnd + terminatorLength;
                if (line.IsEmpty)
                {
                    FlushHeader(current, headers, partPath, projectMetadata);
                    return new HeaderBlock(headers, cursor, true);
                }

                if (terminatorLength == 1)
                {
                    bool lfOnly = lineEnd < bytes.Length && bytes[lineEnd] == '\n';
                    string ending = lfOnly ? "LF" : "CR";
                    if (!_options.AllowLfOnlyLines)
                    {
                        AddIssue("EML_LINE_ENDING", $"A bare {ending} line ending is not enabled.", partPath, checked(absoluteOffset + lineStart), checked(lineLength + 1), true);
                        SetOutcome(ExtractionOutcome.Corrupt);
                        return new HeaderBlock(headers, cursor, false);
                    }

                    SetPartialOnce("EML_LINE_ENDING_COMPAT", $"A bare {ending} line ending was accepted in compatibility mode.", partPath, checked(absoluteOffset + lineStart), checked(lineLength + 1));
                }

                bool continuation = line[0] is (byte)' ' or (byte)'\t';
                if (continuation)
                {
                    if (current is null)
                    {
                        AddIssue("EML_ORPHAN_FOLD", "A folded line has no preceding header field.", partPath, checked(absoluteOffset + lineStart), lineLength, false);
                        SetPartialOutcome();
                        continue;
                    }

                    current.AppendContinuation(Encoding.UTF8.GetString(line).Trim(), checked(absoluteOffset + lineEnd + terminatorLength - current.Offset));
                    continue;
                }

                FlushHeader(current, headers, partPath, projectMetadata);
                int colon = line.IndexOf((byte)':');
                if (colon <= 0 || !IsFieldName(line[..colon]))
                {
                    AddIssue("EML_INVALID_HEADER", "A header field has invalid field-name syntax.", partPath, checked(absoluteOffset + lineStart), lineLength, true);
                    SetOutcome(ExtractionOutcome.Corrupt);
                    return new HeaderBlock(headers, cursor, false);
                }

                if (headers.Count >= _options.MaximumHeaderCount)
                {
                    SetLimited("EML_LIMIT_HEADERS", "The header-count limit was exceeded.", partPath, checked(absoluteOffset + lineStart), lineLength);
                    return new HeaderBlock(headers, cursor, false);
                }

                string name = Encoding.ASCII.GetString(line[..colon]);
                string value = Encoding.UTF8.GetString(line[(colon + 1)..]).Trim();
                current = new Header(name, value, checked(absoluteOffset + lineStart), lineEnd + terminatorLength - lineStart);
            }

            FlushHeader(current, headers, partPath, projectMetadata);
            AddIssue("EML_MISSING_SEPARATOR", "The message has no header/body separator.", partPath, checked(absoluteOffset + bytes.Length), 0, true);
            SetOutcome(ExtractionOutcome.Corrupt);
            return new HeaderBlock(headers, bytes.Length, false);
        }

        private void FlushHeader(Header? header, List<Header> headers, string partPath, bool project)
        {
            if (header is null)
            {
                return;
            }

            Header value = header;
            if (IsSingletonHeader(value.Name))
            {
                foreach (Header existing in headers)
                {
                    if (existing.Name.Equals(value.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        SetPartial("EML_HEADER_DUPLICATE", "A singleton header field occurs more than once; every occurrence was retained.", partPath, value.Offset, value.Length);
                        break;
                    }
                }
            }

            headers.Add(value);
            if (!_budget.TryCharge(ResourceKind.Objects, 1))
            {
                SetLimited("EML_LIMIT_OBJECTS", "The header object-count limit was exceeded.", partPath, value.Offset, value.Length);
                return;
            }

            if (!project)
            {
                return;
            }

            string decoded = DecodeEncodedWords(value.Value, partPath, value.Offset, value.Length);
            _metadata.Add(new MetadataEntry(_order++, $"header:{value.Name.ToLowerInvariant()}", decoded, Location(partPath, value.Offset, value.Length)));
            if (value.Name.Equals("from", StringComparison.OrdinalIgnoreCase) ||
                value.Name.Equals("to", StringComparison.OrdinalIgnoreCase) ||
                value.Name.Equals("cc", StringComparison.OrdinalIgnoreCase) ||
                value.Name.Equals("bcc", StringComparison.OrdinalIgnoreCase) ||
                value.Name.Equals("reply-to", StringComparison.OrdinalIgnoreCase) ||
                value.Name.Equals("sender", StringComparison.OrdinalIgnoreCase))
            {
                AddParticipants(value.Name, decoded, partPath, value.Offset, value.Length);
            }
        }

        private void ParseMultipart(ReadOnlyMemory<byte> body, MediaTypeValue contentType, string partPath, int depth, int bodyAbsoluteOffset)
        {
            if (!contentType.Parameters.TryGetValue("boundary", out string? boundary) || string.IsNullOrEmpty(boundary))
            {
                AddAsset(body.Span, contentType.Type, null, partPath, bodyAbsoluteOffset, body.Length);
                SetPartial("EML_BOUNDARY_MISSING", "A multipart entity has no usable boundary parameter.", partPath, bodyAbsoluteOffset, body.Length);
                return;
            }

            byte[] marker = Encoding.ASCII.GetBytes("--" + boundary);
            ReadOnlySpan<byte> bytes = body.Span;
            var starts = new List<(int LineStart, int BodyStart, bool Closing)>();
            int lineStart = 0;
            int nextControlCheck = 0;
            while (lineStart <= bytes.Length)
            {
                if (lineStart >= nextControlCheck && !CheckControl(partPath, checked(bodyAbsoluteOffset + lineStart)))
                {
                    return;
                }

                if (lineStart >= nextControlCheck)
                {
                    nextControlCheck = checked(lineStart + 4096);
                }

                int lineEnd = FindLineEnd(bytes, lineStart, out int terminator);
                ReadOnlySpan<byte> line = bytes[lineStart..lineEnd];
                bool closing = line.Length >= marker.Length + 2 && line[..marker.Length].SequenceEqual(marker) && line.Slice(marker.Length, 2).SequenceEqual("--"u8);
                int syntaxLength = marker.Length + (closing ? 2 : 0);
                bool onlyLinearWhitespaceAfter = line.Length >= syntaxLength && IsLinearWhitespace(line[syntaxLength..]);
                if (line.Length >= marker.Length && line[..marker.Length].SequenceEqual(marker) && onlyLinearWhitespaceAfter)
                {
                    starts.Add((lineStart, lineEnd + terminator, closing));
                }

                if (lineEnd == bytes.Length)
                {
                    break;
                }

                lineStart = lineEnd + terminator;
            }

            int partNumber = 0;
            bool closed = false;
            for (int index = 0; index < starts.Count; index++)
            {
                if (starts[index].Closing)
                {
                    closed = true;
                    break;
                }

                int end = index + 1 < starts.Count ? starts[index + 1].LineStart : bytes.Length;
                int start = starts[index].BodyStart;
                while (end > start && bytes[end - 1] is (byte)'\r' or (byte)'\n')
                {
                    end--;
                }

                ParseEntity(body[start..end], $"{partPath}.{++partNumber}", depth + 1, isRoot: false, checked(bodyAbsoluteOffset + start));
                if (IsTerminal(_outcome))
                {
                    return;
                }
            }

            if (partNumber == 0)
            {
                SetPartial("EML_BOUNDARY_NOT_FOUND", "The declared multipart boundary was not found.", partPath, bodyAbsoluteOffset, body.Length);
            }
            else if (!closed)
            {
                SetPartial("EML_BOUNDARY_UNCLOSED", "The multipart closing boundary is missing.", partPath, bodyAbsoluteOffset, body.Length);
            }
        }

        private void ParseNested(byte[] bytes, string partPath, int depth, int rawOffset, int rawLength)
        {
            if (!_budget.TryObserveNestingDepth(depth))
            {
                SetLimited("EML_LIMIT_DEPTH", "The nested-message depth limit was exceeded.", partPath, rawOffset, rawLength);
                return;
            }

            var nestedState = new ParseState(bytes, _budget, _control, _options);
            ExtractionResult result = nestedState.ParseRoot(depth);
            _nested.Add(result);
            AddRelationship("nested-message", partPath, result.SourceHash.Hex, Location(partPath, rawOffset, rawLength));
            if (IsTerminal(result.Outcome))
            {
                SetOutcome(result.Outcome);
                AddIssue("EML_NESTED_TERMINAL", "A nested message reached a terminal extraction outcome.", partPath, rawOffset, rawLength, true);
            }
            else if (result.Outcome != ExtractionOutcome.Complete)
            {
                SetPartial("EML_NESTED_INCOMPLETE", "A nested message was not extracted completely.", partPath, rawOffset, rawLength);
            }
        }

        private byte[]? DecodeTransfer(ReadOnlySpan<byte> body, string encoding, string partPath, int rawOffset)
        {
            if (encoding is "7bit" or "8bit" or "binary" or "")
            {
                if (!CanDecode(body.Length) || !_budget.TryCharge(ResourceKind.DecodedBytes, body.Length))
                {
                    SetLimited("EML_LIMIT_DECODED", "The cumulative decoded-byte limit was exceeded.", partPath, rawOffset, body.Length);
                    return null;
                }

                return body.ToArray();
            }

            if (encoding == "base64")
            {
                return DecodeBase64(body, partPath, rawOffset);
            }

            if (encoding == "quoted-printable")
            {
                return DecodeQuotedPrintableBody(body, partPath, rawOffset);
            }

            SetPartial("EML_TRANSFER_UNSUPPORTED", $"Transfer encoding '{SanitizeToken(encoding)}' is unsupported; raw bytes were retained.", partPath, rawOffset, body.Length);
            AddAsset(body, "application/octet-stream", null, partPath, rawOffset, body.Length);
            return null;
        }

        private byte[]? DecodeBase64(ReadOnlySpan<byte> input, string partPath, int rawOffset)
        {
            int maximum = checked((input.Length / 4 + 1) * 3);
            int remaining = RemainingDecodedCapacity();
            using var output = new MemoryStream(Math.Min(maximum, remaining));
            Span<int> quartet = stackalloc int[4];
            int quartetCount = 0;
            int nextControlCheck = 0;
            bool sawPadding = false;
            for (int index = 0; index < input.Length; index++)
            {
                if (index >= nextControlCheck && !CheckControl(partPath, checked(rawOffset + index)))
                {
                    return null;
                }

                if (index >= nextControlCheck)
                {
                    nextControlCheck = checked(index + 4096);
                }

                byte value = input[index];
                if (value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                {
                    continue;
                }

                int decoded = Base64Value(value);
                if (decoded < 0 && value != '=')
                {
                    return RetainMalformedTransfer(input, partPath, rawOffset);
                }

                if (sawPadding && value != '=')
                {
                    return RetainMalformedTransfer(input, partPath, rawOffset);
                }

                quartet[quartetCount++] = value == '=' ? -2 : decoded;
                if (quartetCount != 4)
                {
                    continue;
                }

                if (quartet[0] < 0 || quartet[1] < 0 || quartet[2] == -2 && quartet[3] != -2)
                {
                    return RetainMalformedTransfer(input, partPath, rawOffset);
                }

                int outputCount = quartet[2] == -2 ? 1 : quartet[3] == -2 ? 2 : 3;
                if (output.Length > remaining - outputCount)
                {
                    SetLimited("EML_LIMIT_DECODED", "The cumulative decoded-byte limit was exceeded.", partPath, rawOffset, input.Length);
                    return null;
                }

                output.WriteByte((byte)((quartet[0] << 2) | (quartet[1] >> 4)));
                if (outputCount > 1)
                {
                    output.WriteByte((byte)((quartet[1] << 4) | (quartet[2] >> 2)));
                }

                if (outputCount > 2)
                {
                    output.WriteByte((byte)((quartet[2] << 6) | quartet[3]));
                }

                sawPadding = outputCount < 3;
                quartetCount = 0;
            }

            if (quartetCount != 0)
            {
                return RetainMalformedTransfer(input, partPath, rawOffset);
            }

            if (!_budget.TryCharge(ResourceKind.DecodedBytes, output.Length))
            {
                SetLimited("EML_LIMIT_DECODED", "The cumulative decoded-byte limit was exceeded.", partPath, rawOffset, input.Length);
                return null;
            }

            return output.ToArray();
        }

        private byte[]? DecodeQuotedPrintableBody(ReadOnlySpan<byte> input, string partPath, int rawOffset)
        {
            int remaining = RemainingDecodedCapacity();
            using var output = new MemoryStream(Math.Min(input.Length, remaining));
            bool malformed = false;
            int nextControlCheck = 0;
            for (int index = 0; index < input.Length; index++)
            {
                if (index >= nextControlCheck && !CheckControl(partPath, checked(rawOffset + index)))
                {
                    return null;
                }

                if (index >= nextControlCheck)
                {
                    nextControlCheck = checked(index + 4096);
                }

                if (input[index] == '=' && index + 1 < input.Length)
                {
                    if (input[index + 1] == '\r' && index + 2 < input.Length && input[index + 2] == '\n')
                    {
                        index += 2;
                        continue;
                    }

                    if (input[index + 1] == '\n')
                    {
                        SetPartial("EML_QP_LF_SOFT_BREAK", "A bare-LF quoted-printable soft break was accepted in compatibility mode.", partPath, checked(rawOffset + index), 2);
                        index++;
                        continue;
                    }

                    if (index + 2 < input.Length && TryHex(input[index + 1], out int high) && TryHex(input[index + 2], out int low))
                    {
                        if (!WriteDecodedByte(output, (byte)((high << 4) | low), remaining, partPath, rawOffset, input.Length))
                        {
                            return null;
                        }

                        index += 2;
                        continue;
                    }

                    malformed = true;
                }

                if (!WriteDecodedByte(output, input[index], remaining, partPath, rawOffset, input.Length))
                {
                    return null;
                }
            }

            if (malformed)
            {
                SetPartial("EML_TRANSFER_MALFORMED", "Malformed quoted-printable data was retained with undecodable octets unchanged.", partPath, rawOffset, input.Length);
            }

            if (!_budget.TryCharge(ResourceKind.DecodedBytes, output.Length))
            {
                SetLimited("EML_LIMIT_DECODED", "The cumulative decoded-byte limit was exceeded.", partPath, rawOffset, input.Length);
                return null;
            }

            return output.ToArray();
        }

        private byte[]? RetainMalformedTransfer(ReadOnlySpan<byte> input, string partPath, int rawOffset)
        {
            SetPartial("EML_TRANSFER_MALFORMED", "A transfer-encoded body is malformed and was retained as a raw asset.", partPath, rawOffset, input.Length);
            AddAsset(input, "application/octet-stream", null, partPath, rawOffset, input.Length);
            return null;
        }

        private bool WriteDecodedByte(MemoryStream output, byte value, int remaining, string partPath, int rawOffset, int rawLength)
        {
            if (output.Length >= remaining)
            {
                SetLimited("EML_LIMIT_DECODED", "The cumulative decoded-byte limit was exceeded.", partPath, rawOffset, rawLength);
                return false;
            }

            output.WriteByte(value);
            return true;
        }

        private bool CanDecode(int count) => count <= RemainingDecodedCapacity();

        private int RemainingDecodedCapacity()
        {
            long remaining = _budget.Limits.MaxDecodedBytes - _budget.GetSnapshot().DecodedBytes;
            return checked((int)Math.Min(remaining, int.MaxValue));
        }

        private string DecodeText(ReadOnlySpan<byte> bytes, string? charset, string partPath, int rawOffset, int rawLength)
        {
            string normalized = (charset ?? "us-ascii").Trim().Trim('"').ToLowerInvariant();
            try
            {
                return normalized switch
                {
                    "utf-8" or "utf8" => new UTF8Encoding(false, true).GetString(bytes),
                    "us-ascii" or "ascii" => DecodeAscii(bytes, partPath, rawOffset, rawLength),
                    "iso-8859-1" or "latin1" => Encoding.Latin1.GetString(bytes),
                    "windows-1252" or "cp1252" => DocumentTextDecoder.Decode(bytes, DocumentEncoding.Windows1252, InvalidTextPolicy.Replace).Text,
                    "utf-16" or "utf-16le" => new UnicodeEncoding(false, false, true).GetString(bytes),
                    "utf-16be" => new UnicodeEncoding(true, false, true).GetString(bytes),
                    _ => UnsupportedCharset(bytes, normalized, partPath, rawOffset, rawLength),
                };
            }
            catch (DecoderFallbackException)
            {
                SetPartial("EML_CHARSET_INVALID", "Invalid byte sequences were replaced while decoding text.", partPath, rawOffset, rawLength);
                return Encoding.UTF8.GetString(bytes);
            }
        }

        private string UnsupportedCharset(ReadOnlySpan<byte> bytes, string charset, string partPath, int rawOffset, int rawLength)
        {
            SetPartial("EML_CHARSET_UNSUPPORTED", $"Charset '{SanitizeToken(charset)}' is unsupported; bytes were retained as an asset.", partPath, rawOffset, rawLength);
            AddAsset(bytes, "application/octet-stream", null, partPath, rawOffset, rawLength);
            return string.Empty;
        }

        private string DecodeAscii(ReadOnlySpan<byte> bytes, string partPath, int rawOffset, int rawLength)
        {
            var chars = new char[bytes.Length];
            bool replaced = false;
            for (int index = 0; index < bytes.Length; index++)
            {
                chars[index] = bytes[index] <= 0x7F ? (char)bytes[index] : '\uFFFD';
                replaced |= bytes[index] > 0x7F;
            }

            if (replaced)
            {
                SetPartial("EML_ASCII_REPLACED", "Non-ASCII octets in an ASCII body were replaced.", partPath, rawOffset, rawLength);
            }

            return new string(chars);
        }

        private string ExtractInertHtmlText(string html, string partPath, int rawOffset, int rawLength)
        {
            int remaining = _budget.Limits.MaxTextCharacters - _budget.GetSnapshot().TextCharacters;
            var output = new StringBuilder(Math.Min(html.Length, remaining));
            bool suppress = false;
            bool suppressStyle = false;
            int index = 0;
            int nextControlCheck = 0;
            while (index < html.Length)
            {
                if (index >= nextControlCheck && !CheckControl(partPath, rawOffset))
                {
                    return string.Empty;
                }

                if (index >= nextControlCheck)
                {
                    nextControlCheck = checked(index + 4096);
                }

                if (html[index] == '<')
                {
                    int close = html.IndexOf('>', index + 1);
                    if (close < 0)
                    {
                        SetPartial("EML_HTML_MALFORMED", "An unterminated HTML tag was ignored.", partPath, rawOffset, rawLength);
                        break;
                    }

                    ReadOnlySpan<char> tag = html.AsSpan(index + 1, close - index - 1).Trim();
                    ReadOnlySpan<char> tagName = GetHtmlTagName(tag, out bool closingTag);
                    bool scriptTag = tagName.Equals("script", StringComparison.OrdinalIgnoreCase);
                    bool styleTag = tagName.Equals("style", StringComparison.OrdinalIgnoreCase);
                    if (!closingTag && (scriptTag || styleTag))
                    {
                        suppress = true;
                        suppressStyle = styleTag;
                        AddIssueOnce("EML_HTML_ACTIVE", "Active or styling content was suppressed.", partPath, rawOffset, rawLength, false);
                    }
                    else if (closingTag && (scriptTag || styleTag))
                    {
                        suppress = false;
                        suppressStyle = false;
                    }
                    else if (tagName.Equals("br", StringComparison.OrdinalIgnoreCase) ||
                        (closingTag && (tagName.Equals("p", StringComparison.OrdinalIgnoreCase) || tagName.Equals("div", StringComparison.OrdinalIgnoreCase))))
                    {
                        if (!AppendBounded(output, "\n", remaining, partPath, rawOffset, rawLength))
                        {
                            return string.Empty;
                        }
                    }

                    InspectPassiveHtmlReferences(tag, partPath);
                    if (IsActiveHtmlTag(tagName) || HasActiveHtmlAttribute(tag))
                    {
                        AddIssueOnce("EML_HTML_ACTIVE", "Active or styling content was suppressed.", partPath, rawOffset, rawLength, false);
                    }

                    index = close + 1;
                    continue;
                }

                int next = html.IndexOf('<', index);
                if (next < 0)
                {
                    next = html.Length;
                }

                if (!suppress)
                {
                    for (int chunkStart = index; chunkStart < next; chunkStart += 4096)
                    {
                        if (!CheckControl(partPath, rawOffset))
                        {
                            return string.Empty;
                        }

                        int chunkLength = Math.Min(4096, next - chunkStart);
                        string decoded = WebUtility.HtmlDecode(html.Substring(chunkStart, chunkLength));
                        if (!AppendBounded(output, decoded, remaining, partPath, rawOffset, rawLength))
                        {
                            return string.Empty;
                        }
                    }
                }
                else if (suppressStyle)
                {
                    InspectPassiveHtmlReferences(html.AsSpan(index, next - index), partPath);
                }

                index = next;
            }

            return output.ToString();
        }

        private void InspectPassiveHtmlReferences(ReadOnlySpan<char> value, string partPath)
        {
            if (value.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("//", StringComparison.Ordinal))
            {
                AddRelationshipOnce("passive-external-reference", partPath, "external-uri-redacted", null);
            }

            if (value.Contains("file:", StringComparison.OrdinalIgnoreCase) ||
                value.Contains(@"\\", StringComparison.Ordinal) ||
                ContainsDrivePath(value))
            {
                AddRelationshipOnce("passive-local-reference", partPath, "local-path-redacted", null);
            }
        }

        private static ReadOnlySpan<char> GetHtmlTagName(ReadOnlySpan<char> tag, out bool closing)
        {
            closing = !tag.IsEmpty && tag[0] == '/';
            int start = closing ? 1 : 0;
            while (start < tag.Length && char.IsWhiteSpace(tag[start]))
            {
                start++;
            }

            int end = start;
            while (end < tag.Length && (char.IsAsciiLetterOrDigit(tag[end]) || tag[end] is '-' or ':'))
            {
                end++;
            }

            return tag[start..end];
        }

        private static bool IsActiveHtmlTag(ReadOnlySpan<char> tagName) =>
            tagName.Equals("iframe", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("frame", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("frameset", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("object", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("embed", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("applet", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("form", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("input", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("button", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("link", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("meta", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("base", StringComparison.OrdinalIgnoreCase);

        private static bool HasActiveHtmlAttribute(ReadOnlySpan<char> tag)
        {
            for (int index = 0; index < tag.Length; index++)
            {
                if ((index == 0 || char.IsWhiteSpace(tag[index - 1])) &&
                    index + 2 < tag.Length &&
                    (tag[index] is 'o' or 'O') &&
                    (tag[index + 1] is 'n' or 'N') &&
                    char.IsAsciiLetter(tag[index + 2]))
                {
                    return true;
                }
            }

            return tag.Contains("javascript:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsDrivePath(ReadOnlySpan<char> value)
        {
            for (int index = 0; index + 2 < value.Length; index++)
            {
                if (char.IsAsciiLetter(value[index]) && value[index + 1] == ':' && value[index + 2] is '\\' or '/')
                {
                    return true;
                }
            }

            return false;
        }

        private bool AppendBounded(StringBuilder output, string value, int maximum, string partPath, int rawOffset, int rawLength)
        {
            if (value.Length > maximum - output.Length)
            {
                SetLimited("EML_LIMIT_TEXT", "The extracted-text limit was exceeded.", partPath, rawOffset, rawLength);
                return false;
            }

            output.Append(value);
            return true;
        }

        private void AddAsset(ReadOnlySpan<byte> bytes, string mediaType, string? name, string partPath, int rawOffset, int rawLength)
        {
            ResourceBudgetSnapshot snapshot = _budget.GetSnapshot();
            if (snapshot.Assets >= _budget.Limits.MaxAssets || bytes.Length > _budget.Limits.MaxAssetBytes - snapshot.AssetBytes)
            {
                SetLimited("EML_LIMIT_ASSET", "The asset count or byte limit was exceeded.", partPath, rawOffset, rawLength);
                return;
            }

            if (!_budget.TryCharge(ResourceKind.Assets, 1) || !_budget.TryCharge(ResourceKind.AssetBytes, bytes.Length))
            {
                SetLimited("EML_LIMIT_ASSET", "The asset count or byte limit was exceeded.", partPath, rawOffset, rawLength);
                return;
            }

            Sha256Digest hash = Sha256Digest.Compute(bytes);
            string stableId = StableIdentity.Create("eml-asset", partPath, hash.Hex);
            _assets.Add(new ReviewAsset(stableId, "mime-part", mediaType, name, bytes.ToArray(), Location(partPath, rawOffset, rawLength)));
        }

        private void AddContentIdRelationship(IReadOnlyList<Header> headers, string partPath)
        {
            string? contentId = LastHeader(headers, "content-id")?.Trim().Trim('<', '>');
            if (!string.IsNullOrEmpty(contentId))
            {
                AddRelationship("content-id", partPath, contentId, null);
            }
        }

        private void AddParticipants(string role, string value, string partPath, int offset, int length)
        {
            foreach (string token in SplitAddresses(value))
            {
                string candidate = token.Trim();
                int groupColon = IndexOutsideQuotes(candidate, ':');
                if (groupColon >= 0)
                {
                    candidate = candidate[(groupColon + 1)..].Trim();
                }

                candidate = candidate.TrimEnd(';').Trim();
                if (candidate.Length == 0)
                {
                    continue;
                }

                int left = candidate.LastIndexOf('<');
                int right = candidate.LastIndexOf('>');
                string? displayName = left > 0 ? UnquotePhrase(candidate[..left].Trim()) : null;
                string address = left >= 0 && right > left ? candidate[(left + 1)..right].Trim() : candidate;
                if (address.Length == 0)
                {
                    SetPartial("EML_ADDRESS_MALFORMED", "An address token had no usable mailbox.", partPath, offset, length);
                    continue;
                }

                _participants.Add(new Participant(_order++, role.ToLowerInvariant(), displayName, address, Location(partPath, offset, length)));
            }
        }

        private string DecodeEncodedWords(string input, string partPath, int rawOffset, int rawLength)
        {
            var output = new StringBuilder(input.Length);
            int cursor = 0;
            bool previousWasEncoded = false;
            while (cursor < input.Length)
            {
                int start = input.IndexOf("=?", cursor, StringComparison.Ordinal);
                if (start < 0)
                {
                    output.Append(input, cursor, input.Length - cursor);
                    break;
                }

                ReadOnlySpan<char> between = input.AsSpan(cursor, start - cursor);
                if (!previousWasEncoded || !IsLinearWhitespace(between))
                {
                    output.Append(between);
                }
                int charsetEnd = input.IndexOf('?', start + 2);
                int encodingEnd = charsetEnd < 0 ? -1 : input.IndexOf('?', charsetEnd + 1);
                int end = encodingEnd < 0 ? -1 : input.IndexOf("?=", encodingEnd + 1, StringComparison.Ordinal);
                if (charsetEnd < 0 || encodingEnd < 0 || end < 0)
                {
                    output.Append(input, start, input.Length - start);
                    SetPartial("EML_ENCODED_WORD_MALFORMED", "A malformed encoded word was retained raw.", partPath, rawOffset, rawLength);
                    break;
                }

                string charset = input[(start + 2)..charsetEnd];
                string mode = input[(charsetEnd + 1)..encodingEnd];
                string encoded = input[(encodingEnd + 1)..end];
                try
                {
                    byte[] bytes;
                    if (mode.Equals("B", StringComparison.OrdinalIgnoreCase))
                    {
                        bytes = Convert.FromBase64String(encoded);
                    }
                    else if (mode.Equals("Q", StringComparison.OrdinalIgnoreCase))
                    {
                        bytes = DecodeEncodedWordQ(encoded);
                    }
                    else
                    {
                        output.Append(input, start, end + 2 - start);
                        SetPartial("EML_ENCODED_WORD_MODE", "An encoded word used an unsupported encoding mode and was retained raw.", partPath, rawOffset, rawLength);
                        cursor = end + 2;
                        previousWasEncoded = false;
                        continue;
                    }

                    output.Append(DecodeText(bytes, charset, partPath, rawOffset, rawLength));
                    previousWasEncoded = true;
                }
                catch (FormatException)
                {
                    output.Append(input, start, end + 2 - start);
                    SetPartial("EML_ENCODED_WORD_MALFORMED", "A malformed encoded word was retained raw.", partPath, rawOffset, rawLength);
                    previousWasEncoded = false;
                }

                cursor = end + 2;
            }

            return output.ToString();
        }

        private static byte[] DecodeEncodedWordQ(string value) => DecodeQuotedPrintable(Encoding.ASCII.GetBytes(value.Replace('_', ' ')));

        private static byte[] DecodeQuotedPrintable(ReadOnlySpan<byte> input)
        {
            using var output = new MemoryStream(input.Length);
            for (int index = 0; index < input.Length; index++)
            {
                if (input[index] == '=' && index + 1 < input.Length)
                {
                    if (input[index + 1] == '\r' && index + 2 < input.Length && input[index + 2] == '\n')
                    {
                        index += 2;
                        continue;
                    }

                    if (input[index + 1] == '\n')
                    {
                        index++;
                        continue;
                    }

                    if (index + 2 < input.Length && TryHex(input[index + 1], out int high) && TryHex(input[index + 2], out int low))
                    {
                        output.WriteByte((byte)((high << 4) | low));
                        index += 2;
                        continue;
                    }
                }

                output.WriteByte(input[index]);
            }

            return output.ToArray();
        }

        private static bool TryHex(byte value, out int result)
        {
            result = value switch
            {
                >= (byte)'0' and <= (byte)'9' => value - '0',
                >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
                >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
                _ => -1,
            };
            return result >= 0;
        }

        private static string RemoveAsciiWhitespace(ReadOnlySpan<byte> bytes)
        {
            var builder = new StringBuilder(bytes.Length);
            foreach (byte value in bytes)
            {
                if (value is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                {
                    builder.Append((char)value);
                }
            }

            return builder.ToString();
        }

        private static int FindLineEnd(ReadOnlySpan<byte> bytes, int start, out int terminatorLength)
        {
            for (int index = start; index < bytes.Length; index++)
            {
                if (bytes[index] == '\n')
                {
                    terminatorLength = 1;
                    return index > start && bytes[index - 1] == '\r' ? index - 1 : index;
                }

                if (bytes[index] == '\r')
                {
                    terminatorLength = index + 1 < bytes.Length && bytes[index + 1] == '\n' ? 2 : 1;
                    return index;
                }
            }

            terminatorLength = 0;
            return bytes.Length;
        }

        private static bool IsFieldName(ReadOnlySpan<byte> bytes)
        {
            foreach (byte value in bytes)
            {
                if (value is < 33 or > 126 || value == ':')
                {
                    return false;
                }
            }

            return true;
        }

        private MediaTypeValue ParseMediaType(string value, string partPath, int rawOffset)
        {
            List<string> parts = SplitParameterFields(value);
            string type = parts.Count == 0 ? string.Empty : parts[0].Trim().ToLowerInvariant();
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var continuations = new Dictionary<string, SortedDictionary<int, ParameterSegment>>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < parts.Count; index++)
            {
                int equals = parts[index].IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                string name = parts[index][..equals].Trim();
                string parameterValue = UnquoteParameter(parts[index][(equals + 1)..].Trim());
                int star = name.IndexOf('*');
                ReadOnlySpan<char> suffix = star < 0 ? [] : name.AsSpan(star + 1);
                bool encoded = star >= 0 && (suffix.IsEmpty || suffix[^1] == '*');
                ReadOnlySpan<char> segmentText = encoded && !suffix.IsEmpty ? suffix[..^1] : suffix;
                if (star >= 0 && !segmentText.IsEmpty && int.TryParse(segmentText, NumberStyles.None, CultureInfo.InvariantCulture, out int segment))
                {
                    string baseName = name[..star];
                    if (!continuations.TryGetValue(baseName, out SortedDictionary<int, ParameterSegment>? values))
                    {
                        values = [];
                        continuations.Add(baseName, values);
                    }

                    values[segment] = new ParameterSegment(parameterValue, encoded);
                }
                else
                {
                    parameters[name.TrimEnd('*')] = encoded
                        ? DecodeExtendedParameter(parameterValue, partPath, rawOffset)
                        : parameterValue;
                }
            }

            foreach ((string name, SortedDictionary<int, ParameterSegment> segments) in continuations)
            {
                if (segments.Count == 0 || segments.Keys.First() != 0 || segments.Keys.Last() != segments.Count - 1)
                {
                    SetPartial("EML_PARAMETER_CONTINUATION", "An RFC 2231 parameter continuation was non-contiguous and was not joined.", partPath, rawOffset, 0);
                    continue;
                }

                var joined = new StringBuilder();
                foreach (ParameterSegment segment in segments.Values)
                {
                    joined.Append(segment.Value);
                }

                parameters[name] = segments.Values.Any(static segment => segment.Encoded)
                    ? DecodeExtendedParameter(joined.ToString(), partPath, rawOffset)
                    : joined.ToString();
            }

            return new MediaTypeValue(type, parameters);
        }

        private string DecodeExtendedParameter(string value, string partPath, int rawOffset)
        {
            int firstQuote = value.IndexOf('\'');
            int secondQuote = firstQuote < 0 ? -1 : value.IndexOf('\'', firstQuote + 1);
            string charset = firstQuote > 0 ? value[..firstQuote] : "utf-8";
            string encoded = secondQuote >= 0 ? value[(secondQuote + 1)..] : value;
            using var output = new MemoryStream(encoded.Length);
            for (int index = 0; index < encoded.Length; index++)
            {
                if (encoded[index] == '%' && index + 2 < encoded.Length &&
                    byte.TryParse(encoded.AsSpan(index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte decoded))
                {
                    output.WriteByte(decoded);
                    index += 2;
                }
                else
                {
                    if (encoded[index] > 0x7F)
                    {
                        SetPartial("EML_PARAMETER_ENCODING", "An extended parameter contained a non-ASCII literal and was retained with replacement.", partPath, rawOffset, 0);
                        output.WriteByte((byte)'?');
                    }
                    else
                    {
                        output.WriteByte((byte)encoded[index]);
                    }
                }
            }

            byte[] bytes = output.ToArray();
            try
            {
                return charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase)
                    ? new UTF8Encoding(false, true).GetString(bytes)
                    : charset.Equals("us-ascii", StringComparison.OrdinalIgnoreCase)
                        ? Encoding.ASCII.GetString(bytes)
                        : charset.Equals("iso-8859-1", StringComparison.OrdinalIgnoreCase)
                            ? Encoding.Latin1.GetString(bytes)
                            : UnsupportedParameterCharset(bytes, charset, partPath, rawOffset);
            }
            catch (DecoderFallbackException)
            {
                SetPartial("EML_PARAMETER_ENCODING", "An extended parameter contained invalid encoded bytes.", partPath, rawOffset, 0);
                return Encoding.UTF8.GetString(bytes);
            }
        }

        private string UnsupportedParameterCharset(byte[] bytes, string charset, string partPath, int rawOffset)
        {
            SetPartial("EML_PARAMETER_CHARSET", $"Extended parameter charset '{SanitizeToken(charset)}' is unsupported.", partPath, rawOffset, 0);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string? GetFileName(MediaTypeValue contentType, MediaTypeValue disposition) =>
            disposition.Parameters.GetValueOrDefault("filename") ?? contentType.Parameters.GetValueOrDefault("name");

        private static int Base64Value(byte value) => value switch
        {
            >= (byte)'A' and <= (byte)'Z' => value - 'A',
            >= (byte)'a' and <= (byte)'z' => value - 'a' + 26,
            >= (byte)'0' and <= (byte)'9' => value - '0' + 52,
            (byte)'+' => 62,
            (byte)'/' => 63,
            _ => -1,
        };

        private static bool IsLinearWhitespace(ReadOnlySpan<byte> value)
        {
            foreach (byte character in value)
            {
                if (character is not ((byte)' ' or (byte)'\t'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLinearWhitespace(ReadOnlySpan<char> value)
        {
            foreach (char character in value)
            {
                if (character is not (' ' or '\t'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsUnsupportedMessageSubtype(string mediaType) => mediaType.Equals("message/delivery-status", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("message/disposition-notification", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("message/feedback-report", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("message/global-delivery-status", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("message/global-disposition-notification", StringComparison.OrdinalIgnoreCase);

        private static List<string> SplitAddresses(string value)
        {
            var result = new List<string>();
            int start = 0;
            bool quoted = false;
            bool escaped = false;
            int angleDepth = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (quoted && character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    quoted = !quoted;
                }
                else if (!quoted && character == '<')
                {
                    angleDepth++;
                }
                else if (!quoted && character == '>' && angleDepth > 0)
                {
                    angleDepth--;
                }
                else if (!quoted && angleDepth == 0 && character is ',' or ';')
                {
                    result.Add(value[start..(index + (character == ';' ? 1 : 0))]);
                    start = index + 1;
                }
            }

            if (start < value.Length)
            {
                result.Add(value[start..]);
            }

            return result;
        }

        private static int IndexOutsideQuotes(string value, char sought)
        {
            bool quoted = false;
            bool escaped = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (escaped)
                {
                    escaped = false;
                }
                else if (quoted && character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    quoted = !quoted;
                }
                else if (!quoted && character == sought)
                {
                    return index;
                }
            }

            return -1;
        }

        private static string? UnquotePhrase(string value)
        {
            string trimmed = value.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            {
                trimmed = trimmed[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
            }

            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static List<string> SplitParameterFields(string value)
        {
            var result = new List<string>();
            int start = 0;
            bool quoted = false;
            bool escaped = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (escaped)
                {
                    escaped = false;
                }
                else if (quoted && character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    quoted = !quoted;
                }
                else if (!quoted && character == ';')
                {
                    result.Add(value[start..index]);
                    start = index + 1;
                }
            }

            result.Add(value[start..]);
            return result;
        }

        private static string UnquoteParameter(string value)
        {
            if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
            {
                return value;
            }

            var output = new StringBuilder(value.Length - 2);
            bool escaped = false;
            for (int index = 1; index < value.Length - 1; index++)
            {
                char character = value[index];
                if (escaped)
                {
                    output.Append(character);
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else
                {
                    output.Append(character);
                }
            }

            if (escaped)
            {
                output.Append('\\');
            }

            return output.ToString();
        }

        private static string? LastHeader(IReadOnlyList<Header> headers, string name)
        {
            for (int index = headers.Count - 1; index >= 0; index--)
            {
                if (headers[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return headers[index].Value;
                }
            }

            return null;
        }

        private static bool IsEncrypted(string mediaType) =>
            mediaType.Equals("multipart/encrypted", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("application/pkcs7-mime", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("application/x-pkcs7-mime", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("application/pgp-encrypted", StringComparison.OrdinalIgnoreCase);

        private static bool IsSingletonHeader(string name) =>
            name.Equals("subject", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("from", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("sender", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("date", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("message-id", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("mime-version", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("content-type", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("content-transfer-encoding", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("content-disposition", StringComparison.OrdinalIgnoreCase);

        private bool CheckControl() => CheckControl("1", 0);

        private bool CheckControl(string partPath, int absoluteOffset)
        {
            ExtractionControlState state = _control.Check();
            if (state == ExtractionControlState.Continue)
            {
                return true;
            }

            ExtractionOutcome outcome = state == ExtractionControlState.Cancelled ? ExtractionOutcome.Cancelled : ExtractionOutcome.TimedOut;
            if (!IsHardStop(_outcome))
            {
                SetOutcome(outcome);
                AddIssue(state == ExtractionControlState.Cancelled ? "EML_CANCELLED" : "EML_TIMED_OUT", "Extraction stopped at the caller control boundary.", partPath, absoluteOffset, 0, true);
            }

            return false;
        }

        private void SetLimited(string code, string message)
        {
            SetLimited(code, message, "1", 0, 0);
        }

        private void SetLimited(string code, string message, string partPath, int offset, int length)
        {
            if (!CheckControl(partPath, offset))
            {
                return;
            }

            if (IsHardStop(_outcome))
            {
                return;
            }

            SetOutcome(ExtractionOutcome.ResourceLimitExceeded);
            AddIssue(code, message, partPath, offset, length, true);
        }

        private void SetPartial(string code, string message)
        {
            SetPartialOutcome();
            AddIssue(code, message, "1", 0, 0, false);
        }

        private void SetPartial(string code, string message, string partPath, int offset, int length)
        {
            SetPartialOutcome();
            AddIssue(code, message, partPath, offset, length, false);
        }

        private void SetPartialOnce(string code, string message, string partPath, int offset, int length)
        {
            SetPartialOutcome();
            AddIssueOnce(code, message, partPath, offset, length, false);
        }

        private void SetPartialOutcome()
        {
            if (_outcome == ExtractionOutcome.Complete)
            {
                _outcome = ExtractionOutcome.Partial;
            }
        }

        private void SetOutcome(ExtractionOutcome candidate)
        {
            if (IsHardStop(_outcome))
            {
                return;
            }

            if (IsHardStop(candidate) || OutcomeRank(candidate) > OutcomeRank(_outcome))
            {
                _outcome = candidate;
            }
        }

        private static int OutcomeRank(ExtractionOutcome outcome) => outcome switch
        {
            ExtractionOutcome.Complete => 0,
            ExtractionOutcome.Partial => 10,
            ExtractionOutcome.UnsupportedFormat or ExtractionOutcome.UnsupportedFeature => 20,
            ExtractionOutcome.Corrupt => 30,
            ExtractionOutcome.Encrypted => 40,
            ExtractionOutcome.TechnicalFailure => 50,
            ExtractionOutcome.ResourceLimitExceeded or ExtractionOutcome.Cancelled or ExtractionOutcome.TimedOut => 100,
            _ => 0,
        };

        private static bool IsHardStop(ExtractionOutcome outcome) => outcome is
            ExtractionOutcome.ResourceLimitExceeded or
            ExtractionOutcome.Cancelled or
            ExtractionOutcome.TimedOut;

        private static bool IsTerminal(ExtractionOutcome outcome) => outcome is not
            (ExtractionOutcome.Complete or ExtractionOutcome.Partial);

        private void AddIssue(string code, string message, int offset, int length, bool error) =>
            AddIssue(code, message, "1", offset, length, error);

        private void AddIssueOnce(string code, string message, string partPath, int offset, int length, bool error)
        {
            if (_reportedIssueKeys.Add($"{partPath}\0{code}"))
            {
                AddIssue(code, message, partPath, offset, length, error);
            }
        }

        private void AddIssue(string code, string message, string partPath, int offset, int length, bool error) =>
            _issues.Add(new ExtractionIssue(_order++, error ? ExtractionIssueSeverity.Error : ExtractionIssueSeverity.Warning, code, message, Location(partPath, offset, length)));

        private void AddRelationship(string kind, string sourceIdentity, string targetIdentity, SourceLocation? location)
        {
            if (!_budget.TryCharge(ResourceKind.Objects, 1))
            {
                SetLimited("EML_LIMIT_OBJECTS", "The evidence relationship limit was exceeded.", location?.Path ?? "1", checked((int)(location?.Offset ?? 0)), checked((int)(location?.Length ?? 0)));
                return;
            }

            _relationships.Add(new EvidenceRelationship(_order++, kind, sourceIdentity, targetIdentity, location));
        }

        private void AddRelationshipOnce(string kind, string sourceIdentity, string targetIdentity, SourceLocation? location)
        {
            if (_reportedRelationshipKeys.Add($"{sourceIdentity}\0{kind}\0{targetIdentity}"))
            {
                AddRelationship(kind, sourceIdentity, targetIdentity, location);
            }
        }

        private static SourceLocation Location(string path, int offset, int length) =>
            new(SourceLocationKind.ByteRange, "eml", path, offset, length);

        private static string SanitizeToken(string value)
        {
            var builder = new StringBuilder(Math.Min(value.Length, 64));
            foreach (char character in value)
            {
                if (builder.Length == 64)
                {
                    break;
                }

                builder.Append(character is >= '!' and <= '~' ? character : '?');
            }

            return builder.ToString();
        }

        private ExtractionResult CreateResult(
            ReadOnlyMemory<byte> source,
            ExtractionOutcome outcome,
            IEnumerable<ContentSegment> content,
            IEnumerable<MetadataEntry> metadata,
            IEnumerable<Participant> participants,
            IEnumerable<EvidenceRelationship> relationships,
            IEnumerable<ReviewAsset> assets,
            IEnumerable<ExtractionIssue> issues,
            IEnumerable<ExtractionResult> nested) =>
            new(
                DetectedContainer.InternetMessage,
                DetectedFormat.InternetMessage,
                outcome,
                Sha256Digest.Compute(source.Span),
                ExtractorVersion,
                SpecificationIdentity,
                _budget.Limits.PolicyId,
                ResourceMeasurements.FromSnapshot(_budget.GetSnapshot(), TimeSpan.Zero),
                content,
                metadata,
                participants,
                relationships,
                assets,
                issues,
                nested);

        private readonly record struct HeaderBlock(IReadOnlyList<Header> Headers, int BodyOffset, bool IsValid);

        private readonly record struct ParameterSegment(string Value, bool Encoded);

        private sealed class Header
        {
            private readonly StringBuilder _value;

            public Header(string name, string value, int offset, int length)
            {
                Name = name;
                _value = new StringBuilder(value);
                Offset = offset;
                Length = length;
            }

            public string Name { get; }
            public string Value => _value.ToString();
            public int Offset { get; }
            public int Length { get; private set; }

            public void AppendContinuation(string value, int length)
            {
                _value.Append(' ').Append(value);
                Length = length;
            }
        }

        private sealed record MediaTypeValue(string Type, Dictionary<string, string> Parameters);
    }
}
