using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace CollisionDocNet.Pdf;

internal static class PdfPassiveEvidenceExtractor
{
    private static readonly string[] InfoKeys = ["Title", "Author", "Subject", "Keywords", "Creator", "Producer", "CreationDate", "ModDate", "Trapped"];
    private static readonly string[] ActionKeys = ["URI", "F", "D", "JS", "NewWindow", "Win", "Mac", "Unix"];

    public static PdfPassiveEvidence Extract(
        ReadOnlyMemory<byte> input,
        IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> objects,
        PdfDictionary? catalog,
        PdfDictionary? trailer,
        PdfLimits limits,
        List<PdfIssue> issues,
        CancellationToken cancellationToken)
    {
        var items = new List<PdfEvidenceItem>();
        var assets = new List<PdfPassiveAsset>();
        var signatures = new List<PdfSignatureEvidence>();
        Dictionary<PdfObjectId, IReadOnlyDictionary<string, string>> fileSpecifications = FileSpecifications(objects);
        long assetBytes = 0;

        foreach ((PdfObjectId id, PdfIndirectObject indirect) in objects.OrderBy(pair => pair.Key.Number).ThenBy(pair => pair.Key.Generation))
        {
            cancellationToken.ThrowIfCancellationRequested();
            PdfDictionary? dictionary = indirect.Value switch { PdfDictionary value => value, PdfStream stream => stream.Dictionary, _ => null };
            if (dictionary is null) continue;

            AddInfo(dictionary, id, items, limits);
            AddStructuralItems(dictionary, id, items, limits);
            AddNestedStructuralItems(dictionary, id, items, limits, 0);
            AddAction(dictionary, id, items, limits);
            AddSignature(input.Length, dictionary, id, signatures, items, limits);

            if (indirect.Value is PdfStream passiveStream)
            {
                if (IsName(dictionary, "Type", "Metadata") || IsName(dictionary, "Subtype", "XML"))
                {
                    (byte[] bytes, bool encodedFallback) = DecodeOrEncoded(passiveStream, limits, issues, cancellationToken);
                    AddAsset("metadata", "XMP", "application/rdf+xml", id, bytes, dictionary, assets, ref assetBytes, limits, encodedFallback: encodedFallback);
                    AddXmpClaims(bytes, id, indirect.Span.Offset, items, issues, limits, cancellationToken);
                }
                else if (IsName(dictionary, "Subtype", "Image"))
                {
                    AddAsset("image", null, ImageMediaType(dictionary), id, passiveStream.EncodedBytes, dictionary, assets, ref assetBytes, limits);
                    Add(items, limits, new("image", "XObject", id, indirect.Span.Offset, Properties(dictionary, "Width", "Height", "ColorSpace", "BitsPerComponent", "Interpolate", "ImageMask", "Mask", "SMask")));
                }
                else if (IsName(dictionary, "Type", "EmbeddedFile"))
                {
                    (byte[] bytes, bool encodedFallback) = DecodeOrEncoded(passiveStream, limits, issues, cancellationToken);
                    fileSpecifications.TryGetValue(id, out IReadOnlyDictionary<string, string>? specification);
                    string? fileName = specification is not null && specification.TryGetValue("Name", out string? specifiedName) ? specifiedName : null;
                    AddAsset("embedded-file", fileName, Name(dictionary, "Subtype"), id, bytes, dictionary, assets, ref assetBytes, limits, specification, encodedFallback);
                }
                else if (IsName(dictionary, "Subtype", "Form"))
                {
                    Add(items, limits, new("xobject", "Form", id, indirect.Span.Offset, Properties(dictionary, "BBox", "Matrix", "Group", "OC", "StructParent")));
                }
                else if (IsMediaStream(dictionary))
                {
                    AddAsset("media", null, Name(dictionary, "Subtype"), id, passiveStream.EncodedBytes, dictionary, assets, ref assetBytes, limits);
                }
            }
        }

        AddCatalogEvidence(catalog, objects, items, limits);
        PdfEncryptionEvidence? encryption = ClassifyEncryption(trailer, objects);
        return new(
            items.OrderBy(item => item.Offset).ThenBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Subtype, StringComparer.Ordinal).ToArray(),
            assets.OrderBy(asset => asset.StableId, StringComparer.Ordinal).ToArray(),
            signatures.OrderBy(signature => signature.ObjectId.Number).ThenBy(signature => signature.ObjectId.Generation).ToArray(),
            encryption);
    }

    private static void AddInfo(PdfDictionary dictionary, PdfObjectId id, List<PdfEvidenceItem> items, PdfLimits limits)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in InfoKeys)
            if (dictionary.TryGet(key, out PdfValue value) && Scalar(value) is { } text) properties[key] = text;
        if (properties.Count > 0) Add(items, limits, new("metadata", "Info", id, dictionary.Span.Offset, new ReadOnlyDictionary<string, string>(properties)));
    }

    private static void AddStructuralItems(PdfDictionary dictionary, PdfObjectId id, List<PdfEvidenceItem> items, PdfLimits limits)
    {
        string? type = Name(dictionary, "Type");
        string? subtype = Name(dictionary, "Subtype");
        if (type is "StructTreeRoot" or "StructElem" || dictionary.TryGet("ActualText", out _))
            Add(items, limits, new("tagged-structure", type ?? "MarkedContent", id, dictionary.Span.Offset, Properties(dictionary, "S", "ActualText", "Alt", "Lang", "K", "Pg")));
        if (type is "OCG" or "OCMD") Add(items, limits, new("optional-content", type, id, dictionary.Span.Offset, Properties(dictionary, "Name", "Usage", "VE", "P")));
        if (type is "Annot" || subtype is not null && dictionary.TryGet("Rect", out _))
            Add(items, limits, new("annotation", subtype ?? "Unknown", id, dictionary.Span.Offset, Properties(dictionary, "Contents", "NM", "M", "T", "Subj", "Rect", "F")));
        if (dictionary.TryGet("FT", out PdfValue fieldType) && fieldType is PdfName field)
            Add(items, limits, new("form-field", field.Value, id, dictionary.Span.Offset, Properties(dictionary, "T", "TU", "TM", "V", "DV", "Ff", "Kids")));
        if (type is "Filespec") Add(items, limits, new("file-specification", Name(dictionary, "AFRelationship") ?? "Unspecified", id, dictionary.Span.Offset, Properties(dictionary, "F", "UF", "Desc", "AFRelationship", "EF")));
    }

    private static void AddNestedStructuralItems(PdfDictionary dictionary, PdfObjectId id, List<PdfEvidenceItem> items, PdfLimits limits, int depth)
    {
        if (depth >= limits.MaxDepth) return;
        foreach (PdfValue child in dictionary.Values.Values)
        {
            if (child is PdfDictionary nested)
            {
                AddStructuralItems(nested, id, items, limits);
                AddAction(nested, id, items, limits);
                AddNestedStructuralItems(nested, id, items, limits, depth + 1);
            }
            else if (child is PdfArray array)
            {
                foreach (PdfDictionary nestedArrayItem in array.Values.OfType<PdfDictionary>())
                {
                    AddStructuralItems(nestedArrayItem, id, items, limits);
                    AddAction(nestedArrayItem, id, items, limits);
                    AddNestedStructuralItems(nestedArrayItem, id, items, limits, depth + 1);
                }
            }
        }
    }

    private static void AddCatalogEvidence(PdfDictionary? catalog, IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> objects, List<PdfEvidenceItem> items, PdfLimits limits)
    {
        if (catalog is null) return;
        foreach ((string key, string kind) in new[] { ("Outlines", "outlines"), ("PageLabels", "page-labels"), ("Names", "name-trees"), ("StructTreeRoot", "tagged-structure"), ("OCProperties", "optional-content"), ("AcroForm", "acroform"), ("Collection", "portfolio"), ("AF", "associated-files") })
        {
            if (!catalog.TryGet(key, out PdfValue value)) continue;
            PdfValue resolved = PdfParser.Resolve(value, objects) ?? value;
            Add(items, limits, new(kind, "Catalog", null, value.Span.Offset, resolved is PdfDictionary dictionary ? Properties(dictionary, "Count", "Dests", "EmbeddedFiles", "Fields", "XFA", "View", "Schema") : EmptyProperties()));
        }
        if (catalog.TryGet("MarkInfo", out PdfValue mark)) Add(items, limits, new("tagged-structure", "MarkInfo", null, mark.Span.Offset, Properties(PdfParser.Resolve(mark, objects) as PdfDictionary, "Marked", "Suspects", "UserProperties")));
    }

    private static void AddAction(PdfDictionary dictionary, PdfObjectId id, List<PdfEvidenceItem> items, PdfLimits limits)
    {
        string? action = Name(dictionary, "S");
        if (action is null || action is not ("JavaScript" or "URI" or "Launch" or "GoTo" or "GoToR" or "SubmitForm" or "ImportData" or "Rendition" or "Movie" or "Sound" or "GoTo3DView" or "RichMediaExecute")) return;
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["Execution"] = "disabled", ["Retrieval"] = "disabled" };
        foreach (string key in ActionKeys) if (dictionary.TryGet(key, out PdfValue value) && Scalar(value) is { } text) properties[key] = text;
        Add(items, limits, new("action", action, id, dictionary.Span.Offset, new ReadOnlyDictionary<string, string>(properties)));
    }

    private static void AddSignature(int inputLength, PdfDictionary dictionary, PdfObjectId id, List<PdfSignatureEvidence> signatures, List<PdfEvidenceItem> items, PdfLimits limits)
    {
        if (!IsName(dictionary, "Type", "Sig") && !IsName(dictionary, "FT", "Sig")) return;
        var ranges = new List<long>();
        if (dictionary.TryGet("ByteRange", out PdfValue rangeValue) && rangeValue is PdfArray array)
            foreach (PdfValue rangeItem in array.Values) if (rangeItem is PdfNumber { IsInteger: true } number) ranges.Add((long)number.Value);
        bool structurallyValid = ranges.Count >= 4 && ranges.Count % 2 == 0;
        long previousEnd = 0;
        for (int index = 0; structurallyValid && index < ranges.Count; index += 2)
        {
            long start = ranges[index]; long length = ranges[index + 1];
            structurallyValid = start >= previousEnd && length >= 0 && start <= inputLength && length <= inputLength - start;
            previousEnd = structurallyValid ? start + length : previousEnd;
        }
        PdfString? signatureContents = dictionary.TryGet("Contents", out PdfValue contents) ? contents as PdfString : null;
        bool coversWhole = structurallyValid && ranges.Count == 4 && signatureContents is not null && ranges[0] == 0 && ranges[1] == signatureContents.Span.Offset && ranges[2] == signatureContents.Span.End && previousEnd == inputLength;
        int signatureBytes = signatureContents?.Bytes.Length ?? 0;
        signatures.Add(new(id, ranges.ToArray(), structurallyValid, coversWhole, Name(dictionary, "SubFilter"), signatureBytes));
        Add(items, limits, new("signature", Name(dictionary, "SubFilter") ?? "Unknown", id, dictionary.Span.Offset, new ReadOnlyDictionary<string, string>(new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["ByteRangeStructurallyValid"] = structurallyValid.ToString(CultureInfo.InvariantCulture),
            ["CoversWholeInput"] = coversWhole.ToString(CultureInfo.InvariantCulture),
            ["CryptographicTrustValidated"] = "false"
        })));
    }

    internal static PdfEncryptionEvidence? ClassifyEncryption(PdfDictionary? trailer, IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> objects)
    {
        if (trailer is null || !trailer.TryGet("Encrypt", out PdfValue encryptValue) || PdfParser.Resolve(encryptValue, objects) is not PdfDictionary encrypt) return null;
        string handler = Name(encrypt, "Filter") ?? "Unknown";
        string? subFilter = Name(encrypt, "SubFilter");
        return new(handler, Integer(encrypt, "V"), Integer(encrypt, "R"), subFilter, handler is "Adobe.PubSec" || subFilter?.Contains("adbe.pkcs7", StringComparison.Ordinal) == true);
    }

    private static void AddXmpClaims(byte[] bytes, PdfObjectId id, int offset, List<PdfEvidenceItem> items, List<PdfIssue> issues, PdfLimits limits, CancellationToken cancellationToken)
    {
        var claims = new SortedDictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = limits.MaxDecodedStreamBytes, IgnoreComments = true, IgnoreProcessingInstructions = true });
            bool hasNode = reader.Read();
            while (hasNode)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element || reader.IsEmptyElement) { hasNode = reader.Read(); continue; }
                string key = reader.LocalName;
                if (key is not ("part" or "conformance" or "amd" or "rev" or "GTS_PDFXVersion" or "GTS_PDFXConformance")) { hasNode = reader.Read(); continue; }
                string value = reader.ReadElementContentAsString();
                if (value.Length <= 256) claims[key] = value;
                hasNode = !reader.EOF;
            }
            if (claims.Count > 0)
            {
                claims["Validation"] = "not-performed";
                Add(items, limits, new("profile-claim", "XMP", id, offset, new ReadOnlyDictionary<string, string>(claims)));
            }
        }
        catch (XmlException)
        {
            issues.Add(new("PDF_XMP_INVALID", PdfIssueSeverity.Warning, offset, "XMP metadata was not well-formed XML; no profile validation was attempted."));
        }
    }

    private static (byte[] Bytes, bool EncodedFallback) DecodeOrEncoded(PdfStream stream, PdfLimits limits, List<PdfIssue> issues, CancellationToken cancellationToken)
    {
        try { return (PdfStreamDecoder.Decode(stream, limits, cancellationToken), false); }
        catch (PdfParseException ex) { issues.Add(new(ex.Code, PdfIssueSeverity.Warning, stream.Span.Offset, ex.Message)); return (stream.EncodedBytes, true); }
    }

    private static void AddAsset(string kind, string? name, string? mediaType, PdfObjectId id, byte[] bytes, PdfDictionary dictionary, List<PdfPassiveAsset> assets, ref long totalBytes, PdfLimits limits, IReadOnlyDictionary<string, string>? linkedProperties = null, bool encodedFallback = false)
    {
        totalBytes = checked(totalBytes + bytes.Length);
        if (totalBytes > limits.MaxAssetBytes) throw new PdfParseException("PDF_ASSET_LIMIT", dictionary.Span.Offset, "Passive asset byte limit exceeded.");
        string digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string stableId = $"pdf-{kind}-r-current-{id.Number}-{id.Generation}-o0-{digest[..16]}";
        var properties = new SortedDictionary<string, string>(Properties(dictionary, "Subtype", "Width", "Height", "BitsPerComponent", "AFRelationship", "Params"), StringComparer.Ordinal);
        if (linkedProperties is not null) foreach ((string key, string value) in linkedProperties) properties[key] = value;
        properties["Revision"] = "authoritative-current";
        properties["Occurrence"] = "0";
        properties["EncodedFallback"] = encodedFallback.ToString(CultureInfo.InvariantCulture);
        assets.Add(new(stableId, kind, name, mediaType, id, bytes, new ReadOnlyDictionary<string, string>(properties)));
    }

    private static Dictionary<PdfObjectId, IReadOnlyDictionary<string, string>> FileSpecifications(IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> objects)
    {
        var result = new Dictionary<PdfObjectId, IReadOnlyDictionary<string, string>>();
        foreach ((PdfObjectId _, PdfIndirectObject indirect) in objects.OrderBy(pair => pair.Key.Number).ThenBy(pair => pair.Key.Generation))
        {
            if (indirect.Value is not PdfDictionary dictionary || !IsName(dictionary, "Type", "Filespec") || !dictionary.TryGet("EF", out PdfValue efValue) || PdfParser.Resolve(efValue, objects) is not PdfDictionary ef) continue;
            string? fileName = dictionary.TryGet("UF", out PdfValue unicodeName) ? Scalar(unicodeName) : dictionary.TryGet("F", out PdfValue name) ? Scalar(name) : null;
            string relationship = Name(dictionary, "AFRelationship") ?? "Unspecified";
            foreach (PdfReference reference in ef.Values.Values.OfType<PdfReference>())
            {
                var target = new PdfObjectId(reference.ObjectNumber, reference.Generation);
                if (result.ContainsKey(target)) continue;
                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["AFRelationship"] = relationship };
                if (fileName is not null) properties["Name"] = fileName;
                result[target] = new ReadOnlyDictionary<string, string>(properties);
            }
        }
        return result;
    }

    private static bool IsMediaStream(PdfDictionary dictionary) => Name(dictionary, "Type") is "Rendition" or "MediaClip" or "Sound" || Name(dictionary, "Subtype") is "3D" or "RichMedia";
    private static string? ImageMediaType(PdfDictionary dictionary) => Name(dictionary, "Filter") switch { "DCTDecode" => "image/jpeg", "JPXDecode" => "image/jp2", "JBIG2Decode" => "image/jbig2", "CCITTFaxDecode" => "image/tiff", _ => "application/octet-stream" };
    private static int? Integer(PdfDictionary dictionary, string key) => dictionary.TryGet(key, out PdfValue value) && value is PdfNumber { IsInteger: true } number && number.Value is >= int.MinValue and <= int.MaxValue ? (int)number.Value : null;
    private static string? Name(PdfDictionary dictionary, string key) => dictionary.TryGet(key, out PdfValue value) && value is PdfName name ? name.Value : null;
    private static bool IsName(PdfDictionary dictionary, string key, string expected) => string.Equals(Name(dictionary, key), expected, StringComparison.Ordinal);

    private static ReadOnlyDictionary<string, string> Properties(PdfDictionary? dictionary, params ReadOnlySpan<string> keys)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (dictionary is not null) foreach (string key in keys) if (dictionary.TryGet(key, out PdfValue value) && Scalar(value) is { } text) properties[key] = text;
        return new ReadOnlyDictionary<string, string>(properties);
    }

    private static ReadOnlyDictionary<string, string> EmptyProperties() => new(new SortedDictionary<string, string>(StringComparer.Ordinal));

    private static string? Scalar(PdfValue value) => value switch
    {
        PdfName name => name.Value,
        PdfString text => DecodeString(text.Bytes),
        PdfNumber number => number.Raw,
        PdfBoolean boolean => boolean.Value.ToString(CultureInfo.InvariantCulture),
        PdfReference reference => $"{reference.ObjectNumber} {reference.Generation} R",
        PdfArray array => string.Join(" ", array.Values.Select(item => Scalar(item) ?? item.GetType().Name)),
        _ => null
    };

    private static string DecodeString(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        return Encoding.Latin1.GetString(bytes);
    }

    private static void Add(List<PdfEvidenceItem> items, PdfLimits limits, PdfEvidenceItem item)
    {
        if (items.Count >= limits.MaxEvidenceItems) throw new PdfParseException("PDF_EVIDENCE_LIMIT", item.Offset, "Passive evidence item limit exceeded.");
        items.Add(item);
    }
}
