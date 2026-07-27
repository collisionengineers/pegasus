using System.Globalization;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace CollisionDocNet.Pdf;

internal static class PdfParser
{
    public static PdfParseResult Parse(ReadOnlyMemory<byte> input, PdfLimits? limits = null, bool allowRecovery = false, CancellationToken cancellationToken = default)
    {
        limits ??= new PdfLimits();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ParseCore(input, limits, allowRecovery, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result(PdfParseOutcome.Cancelled, null, null, [], [], [Issue("PDF_CANCELLED", PdfIssueSeverity.Error, 0, "PDF extraction was cancelled.")], false, PdfPassiveEvidence.Empty);
        }
        catch (OverflowException)
        {
            return Result(PdfParseOutcome.ResourceLimitExceeded, null, null, [], [], [Issue("PDF_ARITHMETIC_LIMIT", PdfIssueSeverity.Error, 0, "Checked PDF size arithmetic exceeded its supported range.")], false, PdfPassiveEvidence.Empty);
        }
        catch (PdfParseException ex)
        {
            PdfParseOutcome outcome = ex.Code.Contains("LIMIT", StringComparison.Ordinal) ? PdfParseOutcome.ResourceLimitExceeded : PdfParseOutcome.Corrupt;
            return Result(outcome, null, null, [], [], [Issue(ex.Code, PdfIssueSeverity.Error, ex.Offset, ex.Message)], false, PdfPassiveEvidence.Empty);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return Result(PdfParseOutcome.Corrupt, null, null, [], [], [Issue("PDF_DECODER_FAILURE", PdfIssueSeverity.Error, 0, "A bounded PDF decoder rejected malformed input.")], false, PdfPassiveEvidence.Empty);
        }
    }

    private static PdfParseResult ParseCore(ReadOnlyMemory<byte> input, PdfLimits limits, bool allowRecovery, CancellationToken cancellationToken)
    {
        if (input.Length > limits.MaxInputBytes) return Result(PdfParseOutcome.ResourceLimitExceeded, null, null, [], [], [Issue("PDF_INPUT_LIMIT", PdfIssueSeverity.Error, 0, "Input byte limit exceeded.")], false, PdfPassiveEvidence.Empty);
        ReadOnlySpan<byte> data = input.Span;
        int header = FindHeader(data);
        if (header < 0) return Result(PdfParseOutcome.UnsupportedFormat, null, null, [], [], [Issue("PDF_HEADER_MISSING", PdfIssueSeverity.Error, 0, "No PDF header was found in the permitted leading region.")], false, PdfPassiveEvidence.Empty);
        string headerVersion = Encoding.ASCII.GetString(data.Slice(header + 5, 3));
        var objects = new Dictionary<PdfObjectId, PdfIndirectObject>(); var issues = new List<PdfIssue>(); bool recovered = false;
        RevisionState? revisionState = null;
        try
        {
            ScanIndirectObjects(input, objects, issues, limits, cancellationToken);
            revisionState = ValidateRevisionChain(input, objects, issues, limits, cancellationToken);
            ApplyAuthoritativeXref(objects, revisionState, issues);
            ParseObjectStreams(objects, revisionState, issues, limits, cancellationToken);
            ApplyAuthoritativeXref(objects, revisionState, issues);
        }
        catch (PdfParseException ex)
        {
            issues.Add(Issue(ex.Code, PdfIssueSeverity.Error, ex.Offset, ex.Message));
            if (!allowRecovery) return Result(ex.Code.Contains("LIMIT", StringComparison.Ordinal) ? PdfParseOutcome.ResourceLimitExceeded : PdfParseOutcome.Corrupt, headerVersion, null, objects, [], issues, false, PdfPassiveEvidence.Empty);
            recovered = true;
            issues.Add(Issue("PDF_BOUNDED_RECOVERY", PdfIssueSeverity.Warning, ex.Offset, "Bounded object scan recovery was used; the result cannot be complete."));
        }

        PdfIndirectObject? catalog = FindCatalog(objects);
        string? catalogVersion = catalog?.Value is PdfDictionary cd && cd.TryGet("Version", out PdfValue cv) && cv is PdfName vn ? vn.Value : null;
        PdfDictionary? trailer = revisionState?.Trailer;
        if (trailer is not null && trailer.TryGet("Encrypt", out _))
        {
            var encryptionOnly = new PdfPassiveEvidence([], [], [], PdfPassiveEvidenceExtractor.ClassifyEncryption(trailer, objects));
            return Result(PdfParseOutcome.Encrypted, headerVersion, catalogVersion, objects, [], Append(issues, Issue("PDF_ENCRYPTED", PdfIssueSeverity.Warning, 0, "Encryption dictionary present; encryption was classified but no encrypted content was interpreted.")), recovered, encryptionOnly);
        }
        PdfPassiveEvidence evidence;
        try { evidence = PdfPassiveEvidenceExtractor.Extract(input, objects, catalog?.Value as PdfDictionary, trailer, limits, issues, cancellationToken); }
        catch (PdfParseException ex)
        {
            issues.Add(Issue(ex.Code, PdfIssueSeverity.Error, ex.Offset, ex.Message));
            return Result(PdfParseOutcome.ResourceLimitExceeded, headerVersion, catalogVersion, objects, [], issues, recovered, PdfPassiveEvidence.Empty);
        }
        if (catalog is null) issues.Add(Issue("PDF_CATALOG_MISSING", PdfIssueSeverity.Error, 0, "Current Catalog could not be resolved."));

        var text = new List<PdfTextRun>();
        var markedContent = new List<PdfEvidenceItem>();
        var inlineAssets = new PdfInlineAssetCollector(evidence.Assets, evidence.Items.Count, limits);
        var contentBudget = new PdfContentBudget(limits);
        if (catalog?.Value is PdfDictionary catalogDictionary && catalogDictionary.TryGet("Pages", out PdfValue pagesValue))
        {
            var visited = new HashSet<PdfObjectId>(); int pageIndex = 0;
            try { WalkPages(pagesValue, objects, visited, ref pageIndex, text, markedContent, issues, limits, contentBudget, inlineAssets, 0, null, cancellationToken); }
            catch (PdfParseException ex) when (ex.Code.Contains("LIMIT", StringComparison.Ordinal))
            {
                issues.Add(Issue(ex.Code, PdfIssueSeverity.Error, ex.Offset, ex.Message));
                evidence = MergeContentEvidence(evidence, markedContent, inlineAssets);
                return Result(PdfParseOutcome.ResourceLimitExceeded, headerVersion, catalogVersion, objects, OrderedText(text), issues, recovered, evidence);
            }
        }
        evidence = MergeContentEvidence(evidence, markedContent, inlineAssets);
        PdfParseOutcome outcome = recovered || issues.Any(i => i.Severity != PdfIssueSeverity.Information) ? PdfParseOutcome.Partial : PdfParseOutcome.Complete;
        if (catalog is null) outcome = PdfParseOutcome.Corrupt;
        return Result(outcome, headerVersion, catalogVersion, objects, OrderedText(text), issues, recovered, evidence);
    }

    private static void ScanIndirectObjects(ReadOnlyMemory<byte> input, Dictionary<PdfObjectId, PdfIndirectObject> objects, List<PdfIssue> issues, PdfLimits limits, CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> data = input.Span; int offset = 0;
        while (offset < data.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadObjectHeader(data, offset, out int objectNumber, out int generation, out int bodyStart)) { offset++; continue; }
            if (objects.Count >= limits.MaxObjects) throw new PdfParseException("PDF_OBJECT_LIMIT", offset, "Object limit exceeded.");
            int cursor = bodyStart; var lexer = new PdfLexer(input, limits); PdfValue value;
            try { value = lexer.ReadValue(ref cursor); }
            catch (PdfParseException) { offset++; continue; }
            lexer.SkipTrivia(ref cursor);
            if (value is PdfDictionary dictionary && Match(data, cursor, "stream"u8))
            {
                cursor += 6;
                if (cursor < data.Length && data[cursor] == (byte)'\r') { cursor++; if (cursor < data.Length && data[cursor] == (byte)'\n') cursor++; }
                else if (cursor < data.Length && data[cursor] == (byte)'\n') cursor++;
                else throw new PdfParseException("PDF_STREAM_EOL", cursor, "Stream keyword is not followed by an end-of-line marker.");
                int streamStart = cursor; int streamLength = ResolveStreamLength(dictionary, input, objects, limits);
                int streamEnd;
                bool recoveredLength = false;
                if (streamLength >= 0 && streamStart <= data.Length - streamLength)
                {
                    streamEnd = streamStart + streamLength;
                    int terminator = SkipSingleEol(data, streamEnd);
                    if (!Match(data, terminator, "endstream"u8)) throw new PdfParseException("PDF_STREAM_LENGTH_MISMATCH", streamStart, "Declared stream Length does not end at an endstream token.");
                    cursor = terminator + 9;
                }
                else
                {
                    streamEnd = IndexOf(data, "endstream"u8, streamStart);
                    if (streamEnd < 0) throw new PdfParseException("PDF_STREAM_TRUNCATED", streamStart, "Stream terminator was not found.");
                    while (streamEnd > streamStart && data[streamEnd - 1] is (byte)'\r' or (byte)'\n') streamEnd--;
                    cursor = IndexOf(data, "endstream"u8, streamEnd) + 9;
                    recoveredLength = true;
                    issues.Add(Issue("PDF_STREAM_LENGTH_RECOVERED", PdfIssueSeverity.Warning, streamStart, "Stream length was indirect, missing or invalid; bounded terminator recovery was used."));
                }
                value = new PdfStream(dictionary, data[streamStart..streamEnd].ToArray(), new(dictionary.Span.Offset, streamEnd - dictionary.Span.Offset));
                lexer.SkipTrivia(ref cursor);
                if (!Match(data, cursor, "endobj"u8)) throw new PdfParseException(recoveredLength ? "PDF_STREAM_RECOVERY_ENDOBJ" : "PDF_OBJECT_TRUNCATED", cursor, "Stream is not followed by a structurally valid endobj token.");
            }
            else
            {
                lexer.SkipTrivia(ref cursor);
                if (!Match(data, cursor, "endobj"u8)) throw new PdfParseException("PDF_OBJECT_TRUNCATED", offset, "Indirect object is not immediately followed by endobj.");
            }
            cursor += 6;
            var id = new PdfObjectId(objectNumber, generation);
            if (objects.ContainsKey(id)) issues.Add(Issue("PDF_OBJECT_REDEFINED", PdfIssueSeverity.Information, offset, "An incremental revision redefined an object."));
            objects[id] = new(id, value, new(offset, cursor - offset));
            offset = cursor;
        }
    }

    private static void ParseObjectStreams(Dictionary<PdfObjectId, PdfIndirectObject> objects, RevisionState revisionState, List<PdfIssue> issues, PdfLimits limits, CancellationToken cancellationToken)
    {
        foreach (PdfIndirectObject container in objects.Values.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (container.Value is not PdfStream stream || !IsName(stream.Dictionary, "Type", "ObjStm")) continue;
            int count = DirectInteger(stream.Dictionary, "N", -1); int first = DirectInteger(stream.Dictionary, "First", -1);
            if (count < 0 || first < 0 || count > limits.MaxObjects) { issues.Add(Issue("PDF_OBJECT_STREAM_INVALID", PdfIssueSeverity.Error, stream.Span.Offset, "Object stream N/First is invalid.")); continue; }
            byte[] decoded;
            try { decoded = PdfStreamDecoder.Decode(stream, limits, cancellationToken); }
            catch (PdfParseException ex) { issues.Add(Issue(ex.Code, PdfIssueSeverity.Warning, ex.Offset, ex.Message)); continue; }
            if (first > decoded.Length) { issues.Add(Issue("PDF_OBJECT_STREAM_INVALID", PdfIssueSeverity.Error, stream.Span.Offset, "Object stream First exceeds decoded data.")); continue; }
            string header = Encoding.ASCII.GetString(decoded, 0, first); string[] parts = header.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < count * 2) { issues.Add(Issue("PDF_OBJECT_STREAM_INVALID", PdfIssueSeverity.Error, stream.Span.Offset, "Object stream header is truncated.")); continue; }
            for (int i = 0; i < count; i++)
            {
                if (!int.TryParse(parts[i * 2], NumberStyles.None, CultureInfo.InvariantCulture, out int number) || !int.TryParse(parts[i * 2 + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int relative)) continue;
                if (!revisionState.Entries.TryGetValue(number, out XrefEntry compressed) || compressed.Type != 2 || compressed.Field2 != container.Id.Number || compressed.Field3 != i) continue;
                int valueOffset = checked(first + relative); var lexer = new PdfLexer(decoded, limits);
                try
                {
                    PdfValue value = lexer.ReadValue(ref valueOffset); var id = new PdfObjectId(number, 0);
                    objects[id] = new(id, value, new(container.Span.Offset, container.Span.Length));
                }
                catch (PdfParseException ex) { issues.Add(Issue(ex.Code, PdfIssueSeverity.Warning, stream.Span.Offset, "Compressed object could not be parsed.")); }
            }
        }
    }

    private static RevisionState ValidateRevisionChain(ReadOnlyMemory<byte> input, Dictionary<PdfObjectId, PdfIndirectObject> objects, List<PdfIssue> issues, PdfLimits limits, CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> data = input.Span; int marker = LastIndexOf(data, "startxref"u8);
        if (marker < 0) throw new PdfParseException("PDF_STARTXREF_MISSING", data.Length, "startxref marker is missing.");
        int cursor = marker + 9; SkipWhite(data, ref cursor);
        if (!TryUnsigned(data, ref cursor, out int xrefOffset) || xrefOffset < 0 || xrefOffset >= data.Length) throw new PdfParseException("PDF_STARTXREF_INVALID", cursor, "startxref offset is invalid.");
        var visited = new HashSet<int>();
        var authoritative = new Dictionary<int, XrefEntry>();
        PdfDictionary? newestTrailer = null;
        for (int revision = 0; ; revision++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (revision >= limits.MaxRevisions) throw new PdfParseException("PDF_REVISION_LIMIT", xrefOffset, "Incremental revision limit exceeded.");
            if (!visited.Add(xrefOffset)) throw new PdfParseException("PDF_PREV_CYCLE", xrefOffset, "Incremental revision chain contains a cycle.");
            PdfDictionary? trailer;
            if (Match(data, xrefOffset, "xref"u8)) trailer = ParseClassicXref(input, xrefOffset, objects, issues, limits, authoritative);
            else
            {
                PdfIndirectObject? xref = FindXrefObject(objects, xrefOffset);
                if (xref?.Value is not PdfStream xs) throw new PdfParseException("PDF_XREF_INVALID", xrefOffset, "startxref does not address a recognised xref table or stream.");
                ReadXrefStream(xs, issues, limits, authoritative, hybridSupplement: false, cancellationToken); trailer = xs.Dictionary;
            }
            newestTrailer ??= trailer;
            int hybridOffset = trailer is null ? -1 : DirectInteger(trailer, "XRefStm", -1);
            if (hybridOffset >= 0)
            {
                PdfIndirectObject? hybrid = FindXrefObject(objects, hybridOffset);
                if (hybrid?.Value is not PdfStream hybridStream) throw new PdfParseException("PDF_HYBRID_XREF_INVALID", hybridOffset, "XRefStm does not address a recognised xref stream.");
                ReadXrefStream(hybridStream, issues, limits, authoritative, hybridSupplement: true, cancellationToken);
            }
            int previous = trailer is null ? -1 : DirectInteger(trailer, "Prev", -1);
            if (previous < 0) break;
            if (previous >= data.Length) throw new PdfParseException("PDF_PREV_INVALID", trailer!.Span.Offset, "Prev offset is outside the input.");
            xrefOffset = previous;
        }
        return new(newestTrailer, authoritative);
    }

    private static PdfDictionary ParseClassicXref(ReadOnlyMemory<byte> input, int offset, Dictionary<PdfObjectId, PdfIndirectObject> objects, List<PdfIssue> issues, PdfLimits limits, Dictionary<int, XrefEntry> authoritative)
    {
        ReadOnlySpan<byte> data = input.Span; int cursor = offset + 4;
        while (true)
        {
            SkipWhite(data, ref cursor);
            if (Match(data, cursor, "trailer"u8)) { cursor += 7; var lexer = new PdfLexer(input, limits); return (PdfDictionary)lexer.ReadValue(ref cursor); }
            if (!TryUnsigned(data, ref cursor, out int first) || !ConsumeWhite(data, ref cursor) || !TryUnsigned(data, ref cursor, out int count)) throw new PdfParseException("PDF_XREF_INVALID", cursor, "Invalid xref subsection header.");
            for (int i = 0; i < count; i++)
            {
                SkipWhite(data, ref cursor);
                if (!TryUnsigned(data, ref cursor, out int entryOffset) || !ConsumeWhite(data, ref cursor) || !TryUnsigned(data, ref cursor, out int generation)) throw new PdfParseException("PDF_XREF_INVALID", cursor, "Invalid xref entry.");
                SkipWhite(data, ref cursor); if (cursor >= data.Length) throw new PdfParseException("PDF_XREF_INVALID", cursor, "Truncated xref entry.");
                byte state = data[cursor++];
                int number = checked(first + i);
                if (state is not ((byte)'n') and not ((byte)'f')) throw new PdfParseException("PDF_XREF_INVALID", cursor - 1, "Xref entry state is invalid.");
                authoritative.TryAdd(number, new(state == (byte)'n' ? 1 : 0, entryOffset, generation));
                if (state == (byte)'n' && objects.TryGetValue(new(first + i, generation), out PdfIndirectObject? item) && item.Span.Offset != entryOffset)
                    issues.Add(Issue("PDF_XREF_OFFSET_MISMATCH", PdfIssueSeverity.Warning, cursor, "Xref entry does not match the parsed object offset."));
            }
        }
    }

    private static void ReadXrefStream(PdfStream stream, List<PdfIssue> issues, PdfLimits limits, Dictionary<int, XrefEntry> authoritative, bool hybridSupplement, CancellationToken cancellationToken)
    {
        if (!stream.Dictionary.TryGet("W", out PdfValue wv) || wv is not PdfArray { Values.Count: 3 } wa) throw new PdfParseException("PDF_XREF_STREAM_INVALID", stream.Span.Offset, "Xref stream W array is invalid.");
        var widths = new int[3]; int record = 0;
        for (int i = 0; i < widths.Length; i++)
        {
            widths[i] = wa.Values[i] is PdfNumber { IsInteger: true } n ? (int)n.Value : -1;
            if (widths[i] < 0 || widths[i] > 8) throw new PdfParseException("PDF_XREF_STREAM_INVALID", stream.Span.Offset, "Xref stream field widths are invalid.");
            record += widths[i];
        }
        if (record == 0) throw new PdfParseException("PDF_XREF_STREAM_INVALID", stream.Span.Offset, "Xref stream field widths are invalid.");
        try
        {
            byte[] decoded = PdfStreamDecoder.Decode(stream, limits, cancellationToken);
            if (decoded.Length % record != 0) throw new PdfParseException("PDF_XREF_STREAM_TRUNCATED", stream.Span.Offset, "Xref stream has a partial final record.");
            int[] index = ReadIndex(stream.Dictionary);
            int position = 0;
            for (int pair = 0; pair < index.Length; pair += 2)
            {
                int first = index[pair]; int count = index[pair + 1];
                for (int item = 0; item < count; item++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long type = widths[0] == 0 ? 1 : ReadBigEndian(decoded, ref position, widths[0]);
                    long field2 = ReadBigEndian(decoded, ref position, widths[1]);
                    long field3 = ReadBigEndian(decoded, ref position, widths[2]);
                    if (type is < 0 or > 2 || field2 > int.MaxValue || field3 > int.MaxValue) throw new PdfParseException("PDF_XREF_STREAM_INVALID", stream.Span.Offset, "Xref stream entry is outside supported bounds.");
                    int number = checked(first + item);
                    var entry = new XrefEntry((int)type, (int)field2, (int)field3);
                    if (hybridSupplement && type == 2) authoritative[number] = entry;
                    else authoritative.TryAdd(number, entry);
                }
            }
            if (position != decoded.Length) throw new PdfParseException("PDF_XREF_STREAM_SIZE", stream.Span.Offset, "Xref stream records do not match Index/Size.");
        }
        catch (PdfParseException ex) { throw new PdfParseException(ex.Code, stream.Span.Offset, ex.Message); }
    }

    private static void WalkPages(PdfValue value, Dictionary<PdfObjectId, PdfIndirectObject> objects, HashSet<PdfObjectId> visited, ref int pageIndex, List<PdfTextRun> text, List<PdfEvidenceItem> markedContent, List<PdfIssue> issues, PdfLimits limits, PdfContentBudget contentBudget, PdfInlineAssetCollector inlineAssets, int depth, PdfDictionary? inheritedResources, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (depth > limits.MaxDepth) throw new PdfParseException("PDF_PAGE_DEPTH_LIMIT", value.Span.Offset, "Page tree depth limit exceeded.");
        PdfDictionary? node = Resolve(value, objects) switch { PdfDictionary d => d, PdfStream s => s.Dictionary, _ => null };
        if (value is PdfReference reference && !visited.Add(new(reference.ObjectNumber, reference.Generation))) { issues.Add(Issue("PDF_PAGE_CYCLE", PdfIssueSeverity.Error, value.Span.Offset, "Page tree contains a cycle.")); return; }
        if (node is null) { issues.Add(Issue("PDF_PAGE_INVALID", PdfIssueSeverity.Error, value.Span.Offset, "Page tree node is not a dictionary.")); return; }
        PdfDictionary? resources = node.TryGet("Resources", out PdfValue resourceValue) ? Resolve(resourceValue, objects) as PdfDictionary : inheritedResources;
        if (IsName(node, "Type", "Page"))
        {
            if (pageIndex >= limits.MaxPages) throw new PdfParseException("PDF_PAGE_LIMIT", node.Span.Offset, "Page limit exceeded.");
            InterpretPage(node, resources, pageIndex++, objects, text, markedContent, issues, limits, contentBudget, inlineAssets, cancellationToken); return;
        }
        if (!node.TryGet("Kids", out PdfValue kidsValue) || Resolve(kidsValue, objects) is not PdfArray kids) { issues.Add(Issue("PDF_PAGE_KIDS_MISSING", PdfIssueSeverity.Error, node.Span.Offset, "Pages node has no valid Kids array.")); return; }
        foreach (PdfValue kid in kids.Values) WalkPages(kid, objects, visited, ref pageIndex, text, markedContent, issues, limits, contentBudget, inlineAssets, depth + 1, resources, cancellationToken);
    }

    private static void InterpretPage(PdfDictionary page, PdfDictionary? resources, int pageIndex, Dictionary<PdfObjectId, PdfIndirectObject> objects, List<PdfTextRun> text, List<PdfEvidenceItem> markedContent, List<PdfIssue> issues, PdfLimits limits, PdfContentBudget contentBudget, PdfInlineAssetCollector inlineAssets, CancellationToken cancellationToken)
    {
        if (!page.TryGet("Contents", out PdfValue contents)) return;
        IEnumerable<PdfValue> streams = Resolve(contents, objects) is PdfArray a ? a.Values : [contents];
        foreach (PdfValue item in streams)
        {
            if (Resolve(item, objects) is not PdfStream stream) { issues.Add(Issue("PDF_CONTENT_INVALID", PdfIssueSeverity.Warning, item.Span.Offset, "Page content is not a stream.")); continue; }
            try { InterpretContentStream(stream, resources, pageIndex, objects, text, markedContent, issues, limits, contentBudget, inlineAssets, 0, cancellationToken); }
            catch (PdfParseException ex)
            {
                if (ex.Code.Contains("LIMIT", StringComparison.Ordinal)) throw;
                issues.Add(Issue(ex.Code, PdfIssueSeverity.Warning, stream.Span.Offset, ex.Message));
            }
        }
    }

    private static void InterpretContentStream(PdfStream stream, PdfDictionary? resources, int pageIndex, Dictionary<PdfObjectId, PdfIndirectObject> objects, List<PdfTextRun> text, List<PdfEvidenceItem> markedContent, List<PdfIssue> issues, PdfLimits limits, PdfContentBudget contentBudget, PdfInlineAssetCollector inlineAssets, int depth, CancellationToken cancellationToken)
    {
        if (depth > limits.MaxDepth) throw new PdfParseException("PDF_FORM_DEPTH_LIMIT", stream.Span.Offset, "Form XObject nesting limit exceeded.");
        Dictionary<string, PdfFontMap> fontMaps = BuildFontMaps(resources, objects, issues, limits, cancellationToken);
        byte[] decoded = PdfStreamDecoder.Decode(stream, limits, cancellationToken);
        PdfContentInterpreter.Interpret(decoded, pageIndex, fontMaps, text, issues, limits, stream.Span.Offset, (name, offset) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (resources is null || !resources.TryGet("XObject", out PdfValue xObjectValue) || Resolve(xObjectValue, objects) is not PdfDictionary xObjects || !xObjects.TryGet(name, out PdfValue target) || Resolve(target, objects) is not PdfStream form) return false;
            if (!IsName(form.Dictionary, "Subtype", "Form"))
            {
                if (IsName(form.Dictionary, "Subtype", "Image")) inlineAssets.AddXObjectOccurrence(form.Dictionary, target as PdfReference, offset);
                else inlineAssets.AddXObject(form, target as PdfReference, offset);
                return true;
            }
            PdfObjectId? formId = target is PdfReference formReference ? new(formReference.ObjectNumber, formReference.Generation) : null;
            if (!contentBudget.TryEnterForm(formId)) { issues.Add(Issue("PDF_FORM_CYCLE", PdfIssueSeverity.Warning, offset, "A recursive Form XObject occurrence was not re-entered.")); return true; }
            PdfDictionary? formResources = form.Dictionary.TryGet("Resources", out PdfValue formResourceValue) ? Resolve(formResourceValue, objects) as PdfDictionary : resources;
            try { InterpretContentStream(form, formResources, pageIndex, objects, text, markedContent, issues, limits, contentBudget, inlineAssets, depth + 1, cancellationToken); }
            catch (PdfParseException ex)
            {
                if (ex.Code.Contains("LIMIT", StringComparison.Ordinal)) throw;
                issues.Add(Issue(ex.Code, PdfIssueSeverity.Warning, offset, ex.Message));
            }
            finally { contentBudget.ExitForm(formId); }
            inlineAssets.AddXObjectOccurrence(form.Dictionary, target as PdfReference, offset);
            return true;
        }, (tag, properties, offset) =>
        {
            if (markedContent.Count >= limits.MaxEvidenceItems) throw new PdfParseException("PDF_EVIDENCE_LIMIT", offset, "Marked-content evidence item limit exceeded.");
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (properties is not null)
            {
                if (properties.TryGet("ActualText", out PdfValue actual) && actual is PdfString actualText) values["ActualText"] = Encoding.Latin1.GetString(actualText.Bytes);
                if (properties.TryGet("Alt", out PdfValue alt) && alt is PdfString alternate) values["Alt"] = Encoding.Latin1.GetString(alternate.Bytes);
                if (properties.TryGet("MCID", out PdfValue mcid) && mcid is PdfNumber number) values["MCID"] = number.Raw;
            }
            markedContent.Add(new("marked-content", tag, null, offset, new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(values)));
        }, inlineAssets.Add, contentBudget, cancellationToken);
    }

    private static Dictionary<string, PdfFontMap> BuildFontMaps(PdfDictionary? resources, Dictionary<PdfObjectId, PdfIndirectObject> objects, List<PdfIssue> issues, PdfLimits limits, CancellationToken cancellationToken)
    {
        var maps = new Dictionary<string, PdfFontMap>(StringComparer.Ordinal);
        if (resources is null || !resources.TryGet("Font", out PdfValue fv) || Resolve(fv, objects) is not PdfDictionary fonts) return maps;
        foreach ((string key, PdfValue value) in fonts.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Resolve(value, objects) is not PdfDictionary font) continue;
            Dictionary<int, string>? unicode = null;
            if (font.TryGet("ToUnicode", out PdfValue tuv) && Resolve(tuv, objects) is PdfStream cmap)
            {
                try { unicode = PdfFontMap.ParseToUnicode(PdfStreamDecoder.Decode(cmap, limits, cancellationToken), limits); }
                catch (PdfParseException ex) { issues.Add(Issue(ex.Code, PdfIssueSeverity.Warning, cmap.Span.Offset, ex.Message)); }
            }
            string encoding = font.TryGet("Encoding", out PdfValue ev) && Resolve(ev, objects) is PdfName en ? en.Value : "StandardEncoding";
            maps[key] = new(unicode, encoding);
        }
        return maps;
    }

    internal static PdfValue? Resolve(PdfValue value, IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> objects)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference)
        {
            var id = new PdfObjectId(reference.ObjectNumber, reference.Generation);
            if (!visited.Add(id) || !objects.TryGetValue(id, out PdfIndirectObject? item)) return null;
            value = item.Value;
        }
        return value;
    }

    private static PdfDictionary? FindTrailerDictionary(ReadOnlyMemory<byte> input)
    {
        int trailer = LastIndexOf(input.Span, "trailer"u8); if (trailer < 0) return null;
        int offset = trailer + 7;
        try { return new PdfLexer(input).ReadValue(ref offset) as PdfDictionary; } catch (PdfParseException) { return null; }
    }

    private static PdfIndirectObject? FindCatalog(IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> objects)
    {
        foreach (PdfIndirectObject item in objects.Values)
            if (item.Value is PdfDictionary dictionary && IsName(dictionary, "Type", "Catalog")) return item;
        return null;
    }

    private static bool HasEncryptionDictionary(IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> objects)
    {
        foreach (PdfIndirectObject item in objects.Values)
            if (item.Value is PdfDictionary dictionary && dictionary.TryGet("Encrypt", out _)) return true;
        return false;
    }

    private static PdfIndirectObject? FindXrefObject(IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> objects, int offset)
    {
        foreach (PdfIndirectObject item in objects.Values)
            if (item.Span.Offset == offset && item.Value is PdfStream stream && IsName(stream.Dictionary, "Type", "XRef")) return item;
        return null;
    }

    private static int[] ReadIndex(PdfDictionary dictionary)
    {
        if (dictionary.TryGet("Index", out PdfValue indexValue))
        {
            if (indexValue is not PdfArray array || array.Values.Count == 0 || array.Values.Count % 2 != 0) throw new PdfParseException("PDF_XREF_STREAM_INVALID", dictionary.Span.Offset, "Xref stream Index array is invalid.");
            var values = new int[array.Values.Count];
            for (int i = 0; i < values.Length; i++)
            {
                if (array.Values[i] is not PdfNumber { IsInteger: true } number || number.Value < 0 || number.Value > int.MaxValue) throw new PdfParseException("PDF_XREF_STREAM_INVALID", array.Values[i].Span.Offset, "Xref stream Index value is invalid.");
                values[i] = (int)number.Value;
            }
            return values;
        }
        int size = DirectInteger(dictionary, "Size", -1);
        if (size < 0) throw new PdfParseException("PDF_XREF_STREAM_INVALID", dictionary.Span.Offset, "Xref stream Size is missing or invalid.");
        return [0, size];
    }

    private static long ReadBigEndian(ReadOnlySpan<byte> data, ref int position, int width)
    {
        if (position > data.Length - width) throw new PdfParseException("PDF_XREF_STREAM_TRUNCATED", position, "Xref stream entry is truncated.");
        ulong value = 0;
        for (int i = 0; i < width; i++) value = (value << 8) | data[position++];
        return value > long.MaxValue ? -1 : (long)value;
    }

    private static void ApplyAuthoritativeXref(Dictionary<PdfObjectId, PdfIndirectObject> objects, RevisionState state, List<PdfIssue> issues)
    {
        foreach (PdfObjectId id in objects.Keys.ToArray())
        {
            if (!state.Entries.TryGetValue(id.Number, out XrefEntry entry))
            {
                objects.Remove(id);
                issues.Add(Issue("PDF_OBJECT_NOT_IN_XREF", PdfIssueSeverity.Warning, 0, "An unreferenced scanned object was excluded from the authoritative object set."));
                continue;
            }
            bool authoritative = entry.Type switch
            {
                1 => entry.Field3 == id.Generation && entry.Field2 == objects[id].Span.Offset,
                2 => id.Generation == 0,
                _ => false
            };
            if (!authoritative) objects.Remove(id);
        }
        foreach ((int number, XrefEntry entry) in state.Entries)
        {
            if (entry.Type != 1) continue;
            var id = new PdfObjectId(number, entry.Field3);
            if (!objects.ContainsKey(id)) issues.Add(Issue("PDF_XREF_OBJECT_MISSING", PdfIssueSeverity.Error, entry.Field2, "An authoritative in-use xref entry does not resolve to the declared indirect object."));
        }
    }

    private static int FindHeader(ReadOnlySpan<byte> data) { int max = Math.Min(data.Length - 8, 1024); for (int i = 0; i <= max; i++) if (Match(data, i, "%PDF-"u8) && data[i + 5] is >= (byte)'1' and <= (byte)'2' && data[i + 6] == (byte)'.' && data[i + 7] is >= (byte)'0' and <= (byte)'9') return i; return -1; }
    private static bool TryReadObjectHeader(ReadOnlySpan<byte> data, int offset, out int number, out int generation, out int body) { number = generation = body = 0; int cursor = offset; if (!TryUnsigned(data, ref cursor, out number) || !ConsumeWhite(data, ref cursor) || !TryUnsigned(data, ref cursor, out generation) || !ConsumeWhite(data, ref cursor) || !Match(data, cursor, "obj"u8)) return false; body = cursor + 3; return true; }
    private static int DirectInteger(PdfDictionary dictionary, string name, int fallback) => dictionary.TryGet(name, out PdfValue v) && v is PdfNumber { IsInteger: true } n && n.Value is >= int.MinValue and <= int.MaxValue ? (int)n.Value : fallback;
    private static int ResolveStreamLength(PdfDictionary dictionary, ReadOnlyMemory<byte> input, Dictionary<PdfObjectId, PdfIndirectObject> objects, PdfLimits limits)
    {
        if (!dictionary.TryGet("Length", out PdfValue value)) return -1;
        if (value is PdfNumber { IsInteger: true } number && number.Value is >= 0 and <= int.MaxValue) return (int)number.Value;
        if (value is not PdfReference reference) return -1;
        var id = new PdfObjectId(reference.ObjectNumber, reference.Generation);
        if (objects.TryGetValue(id, out PdfIndirectObject? existing) && existing.Value is PdfNumber { IsInteger: true } existingNumber && existingNumber.Value is >= 0 and <= int.MaxValue) return (int)existingNumber.Value;
        ReadOnlySpan<byte> data = input.Span;
        for (int offset = 0; offset < data.Length; offset++)
        {
            if (!TryReadObjectHeader(data, offset, out int objectNumber, out int generation, out int body) || objectNumber != id.Number || generation != id.Generation) continue;
            var lexer = new PdfLexer(input, limits);
            try
            {
                PdfValue candidate = lexer.ReadValue(ref body);
                if (candidate is PdfNumber { IsInteger: true } indirect && indirect.Value is >= 0 and <= int.MaxValue) return (int)indirect.Value;
            }
            catch (PdfParseException) { return -1; }
        }
        return -1;
    }
    private static int SkipSingleEol(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < data.Length && data[offset] == (byte)'\r') { offset++; if (offset < data.Length && data[offset] == (byte)'\n') offset++; }
        else if (offset < data.Length && data[offset] == (byte)'\n') offset++;
        return offset;
    }
    private static bool IsName(PdfDictionary dictionary, string key, string expected) => dictionary.TryGet(key, out PdfValue value) && value is PdfName name && string.Equals(name.Value, expected, StringComparison.Ordinal);
    private static bool Match(ReadOnlySpan<byte> data, int offset, ReadOnlySpan<byte> expected) => offset >= 0 && offset <= data.Length - expected.Length && data.Slice(offset, expected.Length).SequenceEqual(expected);
    private static int IndexOf(ReadOnlySpan<byte> data, ReadOnlySpan<byte> expected, int start) { int found = data[start..].IndexOf(expected); return found < 0 ? -1 : start + found; }
    private static int LastIndexOf(ReadOnlySpan<byte> data, ReadOnlySpan<byte> expected) => data.LastIndexOf(expected);
    private static bool TryUnsigned(ReadOnlySpan<byte> data, ref int offset, out int value) { long result = 0; int start = offset; while (offset < data.Length && data[offset] is >= (byte)'0' and <= (byte)'9') { result = result * 10 + data[offset++] - (byte)'0'; if (result > int.MaxValue) { value = 0; return false; } } value = (int)result; return offset > start; }
    private static bool ConsumeWhite(ReadOnlySpan<byte> data, ref int offset) { int start = offset; SkipWhite(data, ref offset); return offset > start; }
    private static void SkipWhite(ReadOnlySpan<byte> data, ref int offset) { while (offset < data.Length && PdfLexer.IsWhite(data[offset])) offset++; }
    private static PdfIssue Issue(string code, PdfIssueSeverity severity, int offset, string message) => new(code, severity, offset, message);
    private static List<PdfIssue> Append(List<PdfIssue> issues, PdfIssue issue) { issues.Add(issue); return issues; }
    private static PdfParseResult Result(PdfParseOutcome outcome, string? header, string? catalog, Dictionary<PdfObjectId, PdfIndirectObject> objects, IReadOnlyList<PdfTextRun> text, IReadOnlyList<PdfIssue> issues, bool recovery, PdfPassiveEvidence evidence) => new(outcome, header, catalog, PdfParseResult.Freeze(objects), text, issues.OrderBy(i => i.Offset).ThenBy(i => i.Code, StringComparer.Ordinal).ToArray(), recovery, evidence);
    private static PdfTextRun[] OrderedText(List<PdfTextRun> text) => text.OrderBy(t => t.PageIndex).ThenByDescending(t => t.Y).ThenBy(t => t.X).ThenBy(t => t.ContentOffset).ToArray();
    private static PdfPassiveEvidence MergeContentEvidence(PdfPassiveEvidence evidence, List<PdfEvidenceItem> markedContent, PdfInlineAssetCollector inlineAssets)
    {
        if (markedContent.Count > 0 || inlineAssets.Items.Count > 0)
            evidence = evidence with { Items = evidence.Items.Concat(markedContent).Concat(inlineAssets.Items).OrderBy(item => item.Offset).ThenBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Subtype, StringComparer.Ordinal).ToArray() };
        if (inlineAssets.Assets.Count > 0)
            evidence = evidence with { Assets = evidence.Assets.Concat(inlineAssets.Assets).OrderBy(asset => asset.StableId, StringComparer.Ordinal).ToArray() };
        return evidence;
    }

    private sealed record RevisionState(PdfDictionary? Trailer, IReadOnlyDictionary<int, XrefEntry> Entries);
    private readonly record struct XrefEntry(int Type, int Field2, int Field3);
}

internal sealed class PdfInlineAssetCollector
{
    private readonly PdfLimits _limits;
    private readonly HashSet<string> _stableIds = new(StringComparer.Ordinal);
    private long _assetBytes;
    private int _evidenceItems;

    public PdfInlineAssetCollector(IReadOnlyList<PdfPassiveAsset> existingAssets, int existingEvidenceItems, PdfLimits limits)
    {
        _limits = limits;
        _evidenceItems = existingEvidenceItems;
        foreach (PdfPassiveAsset asset in existingAssets)
        {
            _assetBytes = checked(_assetBytes + asset.Bytes.Length);
            _stableIds.Add(asset.StableId);
        }
    }

    public List<PdfPassiveAsset> Assets { get; } = [];
    public List<PdfEvidenceItem> Items { get; } = [];

    public void Add(PdfInlineImage image)
    {
        var properties = Properties(image.Dictionary);
        properties["DataOffset"] = image.DataOffset.ToString(CultureInfo.InvariantCulture);
        properties["EncodedFallback"] = bool.TrueString;
        properties["Execution"] = "disabled";
        AddAsset("inline-image", new(0, 0), image.EncodedBytes, image.OperatorOffset, properties);
        AddItem("image", "Inline", null, image.OperatorOffset, properties);
    }

    public void AddXObject(PdfStream stream, PdfReference? reference, int occurrenceOffset)
    {
        PdfObjectId id = reference is null ? new(0, 0) : new(reference.ObjectNumber, reference.Generation);
        var properties = Properties(stream.Dictionary);
        properties["EncodedFallback"] = bool.TrueString;
        properties["Execution"] = "disabled";
        properties["OccurrenceOffset"] = occurrenceOffset.ToString(CultureInfo.InvariantCulture);
        string subtype = properties.TryGetValue("Subtype", out string? value) ? value : "Unknown";
        AddAsset("xobject", id, stream.EncodedBytes, stream.Span.Offset, properties);
        AddItem("xobject", subtype, reference is null ? null : id, occurrenceOffset, properties);
    }

    public void AddXObjectOccurrence(PdfDictionary dictionary, PdfReference? reference, int occurrenceOffset)
    {
        PdfObjectId? id = reference is null ? null : new(reference.ObjectNumber, reference.Generation);
        var properties = Properties(dictionary);
        properties["Execution"] = "disabled";
        properties["Retained"] = bool.TrueString;
        string subtype = properties.TryGetValue("Subtype", out string? value) ? value : "Unknown";
        AddItem("xobject-occurrence", subtype, id, occurrenceOffset, properties);
    }

    private void AddAsset(string kind, PdfObjectId id, byte[] bytes, int sourceOffset, SortedDictionary<string, string> properties)
    {
        string digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string stableId = $"pdf-{kind}-r-current-{id.Number}-{id.Generation}-s{sourceOffset}-{digest[..16]}";
        if (!_stableIds.Add(stableId)) return;
        _assetBytes = checked(_assetBytes + bytes.Length);
        if (_assetBytes > _limits.MaxAssetBytes) throw new PdfParseException("PDF_ASSET_LIMIT", sourceOffset, "Passive asset byte limit exceeded.");
        Assets.Add(new(stableId, kind, null, MediaType(properties), id, bytes, new ReadOnlyDictionary<string, string>(properties)));
    }

    private void AddItem(string kind, string subtype, PdfObjectId? id, int offset, SortedDictionary<string, string> properties)
    {
        if (++_evidenceItems > _limits.MaxEvidenceItems) throw new PdfParseException("PDF_EVIDENCE_LIMIT", offset, "Passive content evidence item limit exceeded.");
        Items.Add(new(kind, subtype, id, offset, new ReadOnlyDictionary<string, string>(new SortedDictionary<string, string>(properties, StringComparer.Ordinal))));
    }

    private static SortedDictionary<string, string> Properties(PdfDictionary dictionary)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in new[] { "Width", "Height", "BitsPerComponent", "ColorSpace", "Filter", "DecodeParms", "ImageMask", "Interpolate", "Subtype" })
            if (dictionary.TryGet(key, out PdfValue value) && Scalar(value) is { } scalar) properties[key] = scalar;
        return properties;
    }

    private static string? Scalar(PdfValue value) => value switch
    {
        PdfName name => name.Value,
        PdfNumber number => number.Raw,
        PdfBoolean boolean => boolean.Value.ToString(CultureInfo.InvariantCulture),
        PdfArray array => string.Join(" ", array.Values.Select(item => Scalar(item) ?? item.GetType().Name)),
        PdfDictionary => "dictionary",
        _ => null
    };

    private static string MediaType(SortedDictionary<string, string> properties) => properties.TryGetValue("Filter", out string? filter) ? filter switch
    {
        "DCTDecode" => "image/jpeg",
        "JPXDecode" => "image/jp2",
        "JBIG2Decode" => "image/jbig2",
        "CCITTFaxDecode" => "image/tiff",
        _ => "application/octet-stream"
    } : "application/octet-stream";
}
