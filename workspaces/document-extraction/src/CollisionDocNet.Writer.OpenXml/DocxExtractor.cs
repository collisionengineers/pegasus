using System.Diagnostics;
using System.Globalization;
using System.Text;
using CollisionDocNet.Core;
using CollisionDocNet.Model;
using CollisionDocNet.Storage.Detection;
using CollisionDocNet.Storage.Opc;
using CollisionDocNet.Storage.Xml;
using CollisionDocNet.Storage.Zip;
using ModelContainer = CollisionDocNet.Model.DetectedContainer;
using ModelFormat = CollisionDocNet.Model.DetectedFormat;
using StorageFormat = CollisionDocNet.Storage.Detection.DetectedFormat;

namespace CollisionDocNet.Writer.OpenXml;

/// <summary>
/// Read-only extraction for a deliberately declared WordprocessingML subset.
/// Relationships are traversed from the package office-document root; external
/// targets and active parts are retained as passive evidence and never resolved.
/// </summary>
internal static class DocxExtractor
{
    private const string TransitionalWord = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string StrictWord = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    private const string TransitionalRelationshipBase = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    private const string StrictRelationshipBase = "http://purl.oclc.org/ooxml/officeDocument/relationships/";
    private const string TransitionalRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string StrictRelationships = "http://purl.oclc.org/ooxml/officeDocument/relationships";
    private const string CorePropertiesRelationship = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";
    private const string Mce = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private const string DublinCore = "http://purl.org/dc/elements/1.1/";
    private const string CoreProperties = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private const string TransitionalDrawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string StrictDrawing = "http://purl.oclc.org/ooxml/drawingml/main";
    private const string TransitionalWordprocessingDrawing = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private const string StrictWordprocessingDrawing = "http://purl.oclc.org/ooxml/drawingml/wordprocessingDrawing";
    private const string TransitionalPicture = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    private const string StrictPicture = "http://purl.oclc.org/ooxml/drawingml/picture";
    private const string WordprocessingShape = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
    private const string Office2010Drawing = "http://schemas.microsoft.com/office/drawing/2010/main";
    private const string Office2010WordprocessingDrawing = "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing";

    private const string DocumentContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string TemplateContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml";
    private const string MacroDocumentContentType = "application/vnd.ms-word.document.macroEnabled.main+xml";
    private const string MacroTemplateContentType = "application/vnd.ms-word.template.macroEnabledTemplate.main+xml";

    public static ExtractionResult Extract(
        ReadOnlyMemory<byte> source,
        DocxExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.ResourceLimits);
        ArgumentNullException.ThrowIfNull(options.OpcLimits);
        ArgumentNullException.ThrowIfNull(options.TimeProvider);

        Stopwatch stopwatch = Stopwatch.StartNew();
        var context = new Context(source, options, stopwatch, cancellationToken);
        using var deadline = new CancellationTokenSource(options.ResourceLimits.MaxElapsed, options.TimeProvider);
        using var interrupted = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        try
        {
            context.Check();
            context.Charge(ResourceKind.InputBytes, source.Length, "DOCX_INPUT_LIMIT");

            FormatDetectionResult detection = FileFormatDetector.Detect(
                source,
                "input.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                cancellationToken: interrupted.Token);
            foreach (FormatCandidate candidate in detection.Candidates)
            {
                if (candidate.Format != StorageFormat.EncryptedOpenXml)
                {
                    continue;
                }

                context.SetContainer(ModelContainer.CompoundFile);
                return context.Result(ExtractionOutcome.Encrypted, "DOCX_ENCRYPTED", "The input is an encrypted OOXML compound-file wrapper.");
            }

            OpcLimits opcLimits = EffectiveOpcLimits(options);
            OpcReadResult read = OpcPackageReader.Read(source, opcLimits, interrupted.Token);
            if (!read.IsSuccess)
            {
                OpcFailure failure = ClassifyOpcFailure(source, read, opcLimits, context, deadline, cancellationToken);
                return context.Result(failure.Outcome, failure.Code, failure.Message);
            }

            context.SetContainer(ModelContainer.ZipPackage);
            context.Extract(read.Package!, interrupted.Token);
            context.Check();
            return context.Result(context.StructurallyInvalid
                ? ExtractionOutcome.Corrupt
                : context.HasIncompleteEvidence ? ExtractionOutcome.Partial : ExtractionOutcome.Complete);
        }
        catch (ExtractionAbortException exception)
        {
            return context.Result(exception.Outcome, exception.Code, exception.Message);
        }
        catch (OperationCanceledException)
        {
            ExtractionOutcome outcome = cancellationToken.IsCancellationRequested
                ? ExtractionOutcome.Cancelled
                : ExtractionOutcome.TimedOut;
            return context.Result(outcome, outcome == ExtractionOutcome.Cancelled ? "DOCX_CANCELLED" : "DOCX_TIMED_OUT",
                outcome == ExtractionOutcome.Cancelled ? "Extraction was cancelled." : "Extraction exceeded the configured elapsed-time limit.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return context.Result(ExtractionOutcome.TechnicalFailure, "DOCX_TECHNICAL_FAILURE", "An unexpected managed extraction failure occurred.");
        }
    }

    private static OpcLimits EffectiveOpcLimits(DocxExtractionOptions options) => options.OpcLimits with
    {
        Zip = options.OpcLimits.Zip with
        {
            MaximumInputBytes = checked((int)Math.Min(options.ResourceLimits.MaxInputBytes, int.MaxValue)),
            MaximumEntryBytes = Math.Min(options.OpcLimits.Zip.MaximumEntryBytes, options.ResourceLimits.MaxDecodedBytes),
            MaximumTotalExpandedBytes = Math.Min(options.OpcLimits.Zip.MaximumTotalExpandedBytes, options.ResourceLimits.MaxDecodedBytes),
        },
        Xml = options.OpcLimits.Xml with
        {
            MaximumInputBytes = checked((int)Math.Min(options.ResourceLimits.MaxDecodedBytes, int.MaxValue)),
            MaximumNodes = Math.Min(options.OpcLimits.Xml.MaximumNodes, options.ResourceLimits.MaxObjects),
            MaximumTextCharacters = Math.Min(options.OpcLimits.Xml.MaximumTextCharacters, options.ResourceLimits.MaxTextCharacters),
        },
        MaximumRelationships = Math.Min(options.OpcLimits.MaximumRelationships, options.ResourceLimits.MaxObjects),
    };

    private static OpcFailure ClassifyOpcFailure(
        ReadOnlyMemory<byte> source,
        OpcReadResult result,
        OpcLimits limits,
        Context context,
        CancellationTokenSource deadline,
        CancellationToken callerCancellation)
    {
        if (callerCancellation.IsCancellationRequested)
        {
            return new(ExtractionOutcome.Cancelled, "DOCX_CANCELLED", "Extraction was cancelled.");
        }

        if (deadline.IsCancellationRequested || context.ControlState == ExtractionControlState.TimedOut)
        {
            return new(ExtractionOutcome.TimedOut, "DOCX_TIMED_OUT", "Extraction exceeded the configured elapsed-time limit.");
        }

        if (result.Error == OpcReadError.Cancelled)
        {
            return new(ExtractionOutcome.TimedOut, "DOCX_TIMED_OUT", "Extraction exceeded the configured elapsed-time limit.");
        }

        if (IsLimit(result))
        {
            string code = result.Error == OpcReadError.RelationshipLimitExceeded
                ? "DOCX_RELATIONSHIP_LIMIT"
                : "DOCX_ZIP_LIMIT";
            return new(ExtractionOutcome.ResourceLimitExceeded, code, "A configured OPC package resource limit was exceeded.");
        }

        // OPC reports XML-limit failures as structural errors. Re-read only the named
        // infrastructure entry on this failure path so a configured bound outranks Corrupt.
        if (result.PartName is not null &&
            result.Error is OpcReadError.ContentTypesInvalid or OpcReadError.RelationshipPartInvalid)
        {
            BoundedZipReadResult archive = BoundedZipReader.Read(source, limits.Zip, callerCancellation);
            if (archive.IsSuccess)
            {
                BoundedZipEntry? entry = archive.Archive!.Entries.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, result.PartName, StringComparison.Ordinal));
                if (entry is not null)
                {
                    BoundedXmlReadResult xml = BoundedXmlReader.Read(entry.Content.AsMemory(), limits.Xml, callerCancellation);
                    if (IsXmlLimit(xml.Error))
                    {
                        return new(ExtractionOutcome.ResourceLimitExceeded, "DOCX_XML_LIMIT", "A configured OPC XML resource limit was exceeded.");
                    }
                }
            }
        }

        return new(ExtractionOutcome.Corrupt, "DOCX_OPC_INVALID", $"The OPC package could not be read ({result.Error}/{result.ZipError}).");
    }

    private static bool IsLimit(OpcReadResult result) => result.Error == OpcReadError.RelationshipLimitExceeded ||
        result.ZipError is BoundedZipReadError.InputLimitExceeded or
            BoundedZipReadError.EntryCountLimitExceeded or
            BoundedZipReadError.EntrySizeLimitExceeded or
            BoundedZipReadError.TotalExpandedLimitExceeded or
            BoundedZipReadError.CompressionRatioLimitExceeded;

    private static bool IsXmlLimit(BoundedXmlReadError error) => error is
        BoundedXmlReadError.InputLimitExceeded or BoundedXmlReadError.DepthLimitExceeded or
        BoundedXmlReadError.NodeLimitExceeded or BoundedXmlReadError.AttributeLimitExceeded or
        BoundedXmlReadError.TextLimitExceeded;

    private readonly record struct OpcFailure(ExtractionOutcome Outcome, string Code, string Message);

    private sealed class Context
    {
        private readonly ReadOnlyMemory<byte> _source;
        private readonly DocxExtractionOptions _options;
        private readonly Stopwatch _stopwatch;
        private readonly ResourceBudget _budget;
        private readonly ExtractionControl _control;
        private readonly List<ContentSegment> _content = [];
        private readonly List<MetadataEntry> _metadata = [];
        private readonly List<EvidenceRelationship> _relationships = [];
        private readonly List<ReviewAsset> _assets = [];
        private readonly List<ExtractionIssue> _issues = [];
        private readonly HashSet<string> _issueKeys = new(StringComparer.Ordinal);
        private int _contentOrder;
        private int _metadataOrder;
        private int _relationshipOrder;
        private int _issueOrder;
        private ModelContainer _container;

        internal Context(
            ReadOnlyMemory<byte> source,
            DocxExtractionOptions options,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            _source = source;
            _options = options;
            _stopwatch = stopwatch;
            _budget = new ResourceBudget(options.ResourceLimits);
            _control = new ExtractionControl(options.ResourceLimits.MaxElapsed, options.TimeProvider, cancellationToken);
        }

        internal bool HasIncompleteEvidence { get; private set; }
        internal bool StructurallyInvalid { get; private set; }
        internal ExtractionControlState ControlState => _control.Check();

        internal void SetContainer(ModelContainer container) => _container = container;

        internal void Check()
        {
            switch (_control.Check())
            {
                case ExtractionControlState.Cancelled:
                    throw new ExtractionAbortException(ExtractionOutcome.Cancelled, "DOCX_CANCELLED", "Extraction was cancelled.");
                case ExtractionControlState.TimedOut:
                    throw new ExtractionAbortException(ExtractionOutcome.TimedOut, "DOCX_TIMED_OUT", "Extraction exceeded the configured elapsed-time limit.");
            }
        }

        internal void Charge(ResourceKind kind, long amount, string code)
        {
            Check();
            if (!_budget.TryCharge(kind, amount))
            {
                throw new ExtractionAbortException(ExtractionOutcome.ResourceLimitExceeded, code, "A configured extraction resource limit was exceeded.");
            }
        }

        internal void Extract(OpcPackage package, CancellationToken cancellationToken)
        {
            foreach (BoundedZipEntry entry in package.Archive.Entries)
            {
                Charge(ResourceKind.DecodedBytes, entry.Content.Length, "DOCX_DECODED_LIMIT");
            }

            var bySource = new Dictionary<string, List<OpcRelationship>>(
                Math.Min(package.Relationships.Length, 64),
                StringComparer.Ordinal);
            foreach (OpcRelationship relationship in package.Relationships)
            {
                Charge(ResourceKind.Objects, 1, "DOCX_RELATIONSHIP_LIMIT");
                if (!bySource.TryGetValue(relationship.SourcePart, out List<OpcRelationship>? values))
                {
                    values = [];
                    bySource.Add(relationship.SourcePart, values);
                }

                values.Add(relationship);
            }

            List<OpcRelationship> roots = bySource.GetValueOrDefault("/") ?? [];
            OpcRelationship? root = null;
            foreach (OpcRelationship relationship in roots)
            {
                if (RelationshipKind(relationship.Type) == "officeDocument" && !relationship.IsExternal)
                {
                    if (root is not null)
                    {
                        StructuralIssue("DOCX_MAIN_RELATIONSHIP_AMBIGUOUS", "More than one recognised officeDocument relationship identifies a main story.", "/");
                        return;
                    }

                    root = relationship;
                }
            }

            if (root?.ResolvedPart is null)
            {
                StructuralIssue("DOCX_MAIN_RELATIONSHIP_MISSING", "No exact internal package officeDocument relationship identifies the main story.", "/");
                return;
            }

            OpcPart? main = Part(package, root.ResolvedPart);
            if (main is null || MainVariant(main.ContentType) is null)
            {
                StructuralIssue("DOCX_MAIN_PART_INVALID", "The officeDocument target is absent or does not have an exact recognised WordprocessingML main content type.", root.ResolvedPart);
                return;
            }

            var reached = new HashSet<string>(StringComparer.Ordinal) { main.Name };
            AddRelationship(root, "officeDocument");
            string variant = MainVariant(main.ContentType)!;
            Metadata("docx.variant", variant, main.Name);
            if (variant != "document")
            {
                Issue("DOCX_VARIANT", $"The package is a passive {variant} variant rather than a plain DOCX document.", true, main.Name);
            }

            foreach (OpcRelationship relationship in roots)
            {
                if (ReferenceEquals(relationship, root))
                {
                    continue;
                }

                ProcessRootRelationship(package, relationship, reached, cancellationToken);
            }

            var stories = new Queue<(string Kind, OpcPart Part)>();
            var queuedStories = new HashSet<string>(StringComparer.Ordinal) { main.Name };
            stories.Enqueue(("main", main));
            while (stories.Count != 0)
            {
                Check();
                (string storyKind, OpcPart story) = stories.Dequeue();
                ExtractStory(storyKind, story, cancellationToken);
                if (!bySource.TryGetValue(story.Name, out List<OpcRelationship>? relations))
                {
                    continue;
                }

                foreach (OpcRelationship relationship in relations)
                {
                    ProcessStoryRelationship(package, relationship, reached, queuedStories, stories, cancellationToken);
                }
            }

            foreach (OpcRelationship relationship in package.Relationships)
            {
                if (relationship.SourcePart != "/" && !reached.Contains(relationship.SourcePart))
                {
                    IssueOnce("orphan-rel:" + relationship.SourcePart, "DOCX_ORPHAN_RELATIONSHIP_SOURCE",
                        "A relationship source is outside the reachable office-document graph and was not followed.", true, relationship.SourcePart);
                }
            }

            foreach (OpcPart part in package.Parts)
            {
                if (!reached.Contains(part.Name))
                {
                    Issue("DOCX_ORPHAN_PART", "A package part is outside the reachable office-document graph and was not interpreted.", true, part.Name);
                }
            }
        }

        private void ProcessRootRelationship(
            OpcPackage package,
            OpcRelationship relationship,
            HashSet<string> reached,
            CancellationToken cancellationToken)
        {
            string? kind = RelationshipKind(relationship.Type);
            if (kind is not ("coreProperties" or "extendedProperties" or "customProperties"))
            {
                Issue("DOCX_UNKNOWN_RELATIONSHIP", "An unrecognised package relationship was not followed.", true, relationship.SourcePart);
                return;
            }

            AddRelationship(relationship, kind);
            if (relationship.IsExternal || relationship.ResolvedPart is null)
            {
                Issue("DOCX_EXTERNAL_RELATIONSHIP", "An external relationship was retained but was not retrieved.", true, relationship.SourcePart);
                return;
            }

            OpcPart? part = Part(package, relationship.ResolvedPart);
            if (part is null)
            {
                StructuralIssue("DOCX_PROPERTY_PART_MISSING", "A reachable document-property part is absent.", relationship.ResolvedPart);
                return;
            }

            reached.Add(part.Name);
            ExtractPropertyPart(part, cancellationToken);
        }

        private void ProcessStoryRelationship(
            OpcPackage package,
            OpcRelationship relationship,
            HashSet<string> reached,
            HashSet<string> queuedStories,
            Queue<(string Kind, OpcPart Part)> stories,
            CancellationToken cancellationToken)
        {
            Check();
            string? kind = RelationshipKind(relationship.Type);
            if (kind is null)
            {
                Issue("DOCX_UNKNOWN_RELATIONSHIP", "An unrecognised WordprocessingML relationship was not followed.", true, relationship.SourcePart);
                return;
            }

            AddRelationship(relationship, relationship.IsExternal ? "external" : kind);
            if (relationship.IsExternal)
            {
                bool payloadMissing = kind != "hyperlink";
                Issue("DOCX_EXTERNAL_RELATIONSHIP", "An external relationship was retained but was not retrieved.", payloadMissing, relationship.SourcePart);
                return;
            }

            if (relationship.ResolvedPart is null)
            {
                Issue("DOCX_RELATIONSHIP_TARGET_UNAVAILABLE", "An internal relationship did not resolve to a package part.", true, relationship.SourcePart);
                return;
            }

            OpcPart? part = Part(package, relationship.ResolvedPart);
            if (part is null)
            {
                StructuralIssue("DOCX_RELATIONSHIP_TARGET_MISSING", "A reachable internal relationship target is absent.", relationship.ResolvedPart);
                return;
            }

            if (kind is "header" or "footer" or "footnotes" or "endnotes" or "comments")
            {
                if (!IsStoryContentType(kind, part.ContentType))
                {
                    Issue("DOCX_STORY_CONTENT_TYPE_INVALID", "A reachable story target has an unexpected content type and was not parsed.", true, part.Name);
                    return;
                }

                reached.Add(part.Name);
                if (queuedStories.Add(part.Name))
                {
                    stories.Enqueue((kind, part));
                }

                return;
            }

            if (kind is "styles" or "numbering" or "settings" or "webSettings" or "fontTable" or "theme")
            {
                if (!IsDependencyContentType(kind, part.ContentType))
                {
                    Issue("DOCX_DEPENDENCY_CONTENT_TYPE_INVALID", $"The reachable {kind} dependency has an unexpected content type and was not interpreted.", true, part.Name);
                    return;
                }

                reached.Add(part.Name);
                ExtractDependency(kind, part, cancellationToken);
                return;
            }

            if (kind is "image" or "oleObject" or "package" or "aFChunk" or "vbaProject" or "control" or
                "chart" or "diagramData" or "diagramLayout" or "diagramStyle" or "diagramColors" or "customXml")
            {
                reached.Add(part.Name);
                ExtractPassiveAsset(kind, part);
                return;
            }

            if (kind == "hyperlink")
            {
                Issue("DOCX_INTERNAL_HYPERLINK_TARGET", "An internal hyperlink target was retained without traversal.", false, relationship.SourcePart);
                return;
            }

            Issue("DOCX_RELATIONSHIP_PASSIVE", $"The recognised {kind} relationship is not semantically interpreted by this subset.", true, relationship.SourcePart);
        }

        private void ExtractStory(string kind, OpcPart part, CancellationToken cancellationToken)
        {
            BoundedXmlDocument? document = ReadXml(part, kind == "main", cancellationToken);
            if (document is null)
            {
                return;
            }

            bool? strict = null;
            var current = new SegmentBuffer();
            var special = new SegmentBuffer();
            TextMode mode = TextMode.None;
            int textDepth = -1;
            int deletionDepth = -1;
            int simpleFieldDepth = -1;
            int skipDepth = -1;
            string skipNamespace = string.Empty;
            string skipLocalName = string.Empty;
            int paragraphDepth = -1;
            int tableCount = 0;
            int sectionCount = 0;
            int drawingCount = 0;

            foreach (BoundedXmlNode node in document.Nodes)
            {
                Check();
                if (skipDepth >= 0)
                {
                    if (node.Kind == BoundedXmlNodeKind.ElementEnd && node.Depth == skipDepth &&
                        node.NamespaceUri == skipNamespace && node.LocalName == skipLocalName)
                    {
                        skipDepth = -1;
                        skipNamespace = string.Empty;
                        skipLocalName = string.Empty;
                    }

                    continue;
                }

                if (node.Kind == BoundedXmlNodeKind.ElementStart && node.NamespaceUri == Mce)
                {
                    if (node.LocalName == "AlternateContent")
                    {
                        Issue("DOCX_MCE_PARTIAL", "Markup compatibility was handled conservatively by selecting fallback content only.", true, part.Name, node);
                    }
                    else if (node.LocalName == "Choice")
                    {
                        skipDepth = node.Depth;
                        skipNamespace = node.NamespaceUri;
                        skipLocalName = node.LocalName;
                    }
                    else if (node.LocalName != "Fallback")
                    {
                        IssueOnce("mce:" + node.LocalName, "DOCX_UNKNOWN_MARKUP", "Unknown markup-compatibility content was not interpreted.", true, part.Name, node);
                    }

                    continue;
                }

                if (node.Kind == BoundedXmlNodeKind.ElementStart && IsDrawingMarkup(node))
                {
                    if (node.LocalName == "t" && node.NamespaceUri is TransitionalDrawing or StrictDrawing)
                    {
                        mode = TextMode.Drawing;
                        textDepth = node.Depth;
                    }

                    if (node.LocalName is "docPr" or "cNvPr")
                    {
                        AddDrawingDescription(node, part.Name);
                    }

                    continue;
                }

                if (node.Kind == BoundedXmlNodeKind.ElementStart && !IsWord(node))
                {
                    IssueOnce("foreign:" + node.NamespaceUri + ":" + node.LocalName, "DOCX_UNKNOWN_MARKUP",
                        "Foreign story markup was not interpreted.", true, part.Name, node);
                    skipDepth = node.Depth;
                    skipNamespace = node.NamespaceUri;
                    skipLocalName = node.LocalName;
                    continue;
                }

                if (node.Kind == BoundedXmlNodeKind.ElementStart && IsWord(node))
                {
                    strict ??= node.NamespaceUri == StrictWord;
                    if (!IsRecognisedWordElement(node.LocalName))
                    {
                        IssueOnce("word:" + node.LocalName, "DOCX_UNKNOWN_MARKUP", "Unknown WordprocessingML markup was not interpreted.", true, part.Name, node);
                        skipDepth = node.Depth;
                        skipNamespace = node.NamespaceUri;
                        skipLocalName = node.LocalName;
                        continue;
                    }

                    switch (node.LocalName)
                    {
                        case "p": paragraphDepth = node.Depth; break;
                        case "tbl": tableCount++; break;
                        case "sectPr": sectionCount++; break;
                        case "del":
                            Flush(current, $"docx.{kind}.text", part.Name);
                            deletionDepth = node.Depth;
                            break;
                        case "t":
                            mode = deletionDepth >= 0 ? TextMode.Deleted : TextMode.Current;
                            textDepth = node.Depth;
                            break;
                        case "delText":
                            mode = TextMode.Deleted;
                            textDepth = node.Depth;
                            break;
                        case "instrText":
                            if (simpleFieldDepth >= 0)
                            {
                                break;
                            }
                            Flush(current, $"docx.{kind}.text", part.Name);
                            mode = TextMode.Instruction;
                            textDepth = node.Depth;
                            break;
                        case "fldSimple":
                            simpleFieldDepth = node.Depth;
                            string? instruction = Attribute(node, "instr");
                            if (!string.IsNullOrEmpty(instruction))
                            {
                                Flush(current, $"docx.{kind}.text", part.Name);
                                Emit($"docx.{kind}.field-instruction", instruction, part.Name, node);
                            }
                            break;
                        case "tab": Append(deletionDepth >= 0 ? special : current, "\t", node); break;
                        case "br":
                        case "cr": Append(deletionDepth >= 0 ? special : current, "\n", node); break;
                        case "noBreakHyphen": Append(deletionDepth >= 0 ? special : current, "\u2011", node); break;
                        case "softHyphen": Append(deletionDepth >= 0 ? special : current, "\u00ad", node); break;
                        case "bookmarkStart": Metadata("bookmark", Attribute(node, "name") ?? "(unnamed)", part.Name, node); break;
                        case "hyperlink": Metadata("hyperlink.relationship", RelationshipAttribute(node, "id") ?? "(anchor)", part.Name, node); break;
                        case "drawing":
                        case "pict": drawingCount++; break;
                        case "altChunk": Issue("DOCX_ALTCHUNK_PASSIVE", "An altChunk reference was retained but its payload was not interpreted.", true, part.Name, node); break;
                        case "sdt": Issue("DOCX_CONTENT_CONTROL_INSPECTED", "Content-control text was extracted, but binding and form semantics are unresolved.", true, part.Name, node); break;
                    }
                }
                else if (node.Kind is BoundedXmlNodeKind.Text or BoundedXmlNodeKind.CData)
                {
                    if (textDepth >= 0 && !string.IsNullOrEmpty(node.Value))
                    {
                        Append(mode is TextMode.Current or TextMode.Drawing ? current : special, node.Value, node);
                    }
                }
                else if (node.Kind == BoundedXmlNodeKind.ElementEnd && IsWord(node))
                {
                    if (node.LocalName == "fldSimple" && simpleFieldDepth == node.Depth)
                    {
                        simpleFieldDepth = -1;
                    }
                    if (textDepth == node.Depth && node.LocalName is "t" or "delText" or "instrText")
                    {
                        if (mode == TextMode.Instruction)
                        {
                            Flush(special, $"docx.{kind}.field-instruction", part.Name);
                        }
                        textDepth = -1;
                        mode = TextMode.None;
                    }

                    if (node.LocalName == "del" && deletionDepth == node.Depth)
                    {
                        Flush(special, $"docx.{kind}.deleted", part.Name);
                        deletionDepth = -1;
                    }
                    else if (node.LocalName == "p" && paragraphDepth == node.Depth)
                    {
                        Flush(current, $"docx.{kind}.paragraph", part.Name);
                        paragraphDepth = -1;
                    }
                }
                else if (node.Kind == BoundedXmlNodeKind.ElementEnd && IsDrawingMarkup(node) &&
                    node.LocalName == "t" && textDepth == node.Depth && mode == TextMode.Drawing)
                {
                    Flush(current, $"docx.{kind}.drawing-text", part.Name);
                    textDepth = -1;
                    mode = TextMode.None;
                }
            }

            Flush(current, $"docx.{kind}.text", part.Name);
            Flush(special, deletionDepth >= 0 ? $"docx.{kind}.deleted" : $"docx.{kind}.field-instruction", part.Name);
            Metadata($"story.{kind}.namespace", strict == true ? "strict" : "transitional", part.Name);
            Metadata($"story.{kind}.tables", tableCount.ToString(CultureInfo.InvariantCulture), part.Name);
            Metadata($"story.{kind}.sections", sectionCount.ToString(CultureInfo.InvariantCulture), part.Name);
            Metadata($"story.{kind}.drawings", drawingCount.ToString(CultureInfo.InvariantCulture), part.Name);
            if (drawingCount != 0)
            {
                Issue("DOCX_DRAWING_PASSIVE", "Drawing layout is not rendered; standard drawing text, descriptions and reachable passive assets were retained.", false, part.Name);
            }
        }

        private BoundedXmlDocument? ReadXml(OpcPart part, bool structural, CancellationToken cancellationToken)
        {
            BoundedXmlReadResult xml = BoundedXmlReader.Read(part.Content.AsMemory(), _options.OpcLimits.Xml, cancellationToken);
            if (!xml.IsSuccess)
            {
                if (xml.Error == BoundedXmlReadError.Cancelled)
                {
                    Check();
                    throw new OperationCanceledException(cancellationToken);
                }

                if (IsXmlLimit(xml.Error))
                {
                    throw new ExtractionAbortException(ExtractionOutcome.ResourceLimitExceeded, "DOCX_XML_LIMIT", "A cumulative XML resource limit was exceeded.");
                }

                if (structural)
                {
                    StructuralIssue("DOCX_STORY_XML_INVALID", "The main story XML is structurally invalid or prohibited.", part.Name);
                }
                else
                {
                    Issue("DOCX_STORY_XML_INVALID", "A related story XML is invalid or prohibited and was omitted.", true, part.Name);
                }

                return null;
            }

            ChargeXml(xml.Document!);
            return xml.Document;
        }

        private void ChargeXml(BoundedXmlDocument document)
        {
            foreach (BoundedXmlNode node in document.Nodes)
            {
                Charge(ResourceKind.Objects, 1, "DOCX_XML_NODE_LIMIT");
                Charge(ResourceKind.TextCharacters, node.Value.Length, "DOCX_XML_TEXT_LIMIT");
                foreach (BoundedXmlAttributeValue attribute in node.Attributes)
                {
                    Charge(ResourceKind.TextCharacters, attribute.Value.Length, "DOCX_XML_TEXT_LIMIT");
                }
            }
        }

        private void ExtractPropertyPart(OpcPart part, CancellationToken cancellationToken)
        {
            BoundedXmlDocument? document = ReadXml(part, false, cancellationToken);
            if (document is null) return;
            BoundedXmlNode? element = null;
            foreach (BoundedXmlNode node in document.Nodes)
            {
                if (node.Kind == BoundedXmlNodeKind.ElementStart && node.Depth == 1)
                {
                    element = node;
                }
                else if (element is not null && node.Kind is BoundedXmlNodeKind.Text or BoundedXmlNodeKind.CData)
                {
                    if (!string.IsNullOrWhiteSpace(node.Value) &&
                        (element.NamespaceUri == DublinCore || element.NamespaceUri == CoreProperties || part.Name != "/docProps/core.xml"))
                    {
                        Metadata($"property.{element.LocalName}", node.Value, part.Name, node);
                    }
                    element = null;
                }
            }
        }

        private void ExtractDependency(string kind, OpcPart part, CancellationToken cancellationToken)
        {
            BoundedXmlDocument? document = ReadXml(part, false, cancellationToken);
            if (document is null) return;
            int elements = 0;
            bool protectedDocument = false;
            foreach (BoundedXmlNode node in document.Nodes)
            {
                if (node.Kind != BoundedXmlNodeKind.ElementStart) continue;
                elements++;
                protectedDocument |= kind == "settings" && IsWord(node) && node.LocalName == "documentProtection";
            }

            Metadata($"dependency.{kind}.elements", elements.ToString(CultureInfo.InvariantCulture), part.Name);
            if (protectedDocument)
            {
                Metadata("document.protection", "present-passive", part.Name);
                Issue("DOCX_DOCUMENT_PROTECTION_PASSIVE", "Editing protection was detected but is not treated as cryptographic encryption.", true, part.Name);
            }
            else
            {
                Issue("DOCX_DEPENDENCY_INVENTORY_ONLY", $"The {kind} dependency was inventoried as non-text document context.", false, part.Name);
            }
        }

        private void AddDrawingDescription(BoundedXmlNode node, string part)
        {
            foreach (BoundedXmlAttributeValue attribute in node.Attributes)
            {
                if (attribute.LocalName is not ("descr" or "title" or "name") || string.IsNullOrWhiteSpace(attribute.Value))
                {
                    continue;
                }

                Metadata($"drawing.{attribute.LocalName}", attribute.Value, part, node);
            }
        }

        private void ExtractPassiveAsset(string relationshipKind, OpcPart part)
        {
            string kind = AssetKind(relationshipKind);
            Charge(ResourceKind.Assets, 1, "DOCX_ASSET_COUNT_LIMIT");
            Charge(ResourceKind.AssetBytes, part.Content.Length, "DOCX_ASSET_BYTES_LIMIT");
            Charge(ResourceKind.Objects, 1, "DOCX_OBJECT_LIMIT");
            Sha256Digest hash = Sha256Digest.Compute(part.Content.AsSpan());
            string stableId = StableIdentity.Create("docx-asset", part.Name, hash.Hex);
            _assets.Add(new(stableId, kind, part.ContentType, Path.GetFileName(part.Name), part.Content.AsMemory(), PartLocation(part.Name, part.Content.Length)));
            if (kind is "macro" or "activex" or "ole-embedding")
            {
                Issue("DOCX_ACTIVE_CONTENT_PASSIVE", $"A {kind} part was retained without execution.", true, part.Name);
            }
            else if (kind is "custom-xml" or "embedded-package" or "alternative-content")
            {
                Issue("DOCX_EMBEDDED_CONTENT_PASSIVE", $"A {kind} part was retained without nested interpretation.", true, part.Name);
            }
            else if (kind == "graphical-data")
            {
                Issue("DOCX_SPECIAL_PART_PASSIVE", "Graphical data was retained without semantic rendering.", true, part.Name);
            }
        }

        internal ExtractionResult Result(ExtractionOutcome outcome, string? code = null, string? message = null)
        {
            if (code is not null)
            {
                AddTerminalIssue(code, message!, outcome is not ExtractionOutcome.Complete);
            }

            _stopwatch.Stop();
            ResourceBudgetSnapshot snapshot = _budget.GetSnapshot();
            return new(_container, ModelFormat.WordprocessingMl, outcome,
                Sha256Digest.Compute(_source.Span), DocxExtractionOptions.Extractor, DocxExtractionOptions.Specification,
                _options.ResourceLimits.PolicyId, ResourceMeasurements.FromSnapshot(snapshot, _stopwatch.Elapsed),
                _content, _metadata, relationships: _relationships, assets: _assets, issues: _issues);
        }

        private static void Append(SegmentBuffer buffer, string text, BoundedXmlNode source)
        {
            if (text.Length == 0) return;
            buffer.Source ??= source;
            buffer.Text.Append(text);
        }

        private void Flush(SegmentBuffer buffer, string kind, string part)
        {
            if (buffer.Text.Length == 0) return;
            Emit(kind, buffer.Text.ToString(), part, buffer.Source);
            buffer.Text.Clear();
            buffer.Source = null;
        }

        private void Emit(string kind, string text, string part, BoundedXmlNode? node)
        {
            Charge(ResourceKind.Objects, 1, "DOCX_OBJECT_LIMIT");
            _content.Add(new(_contentOrder++, kind, text, node is null ? PartLocation(part, 0) : XmlLocation(part, node)));
        }

        private void Metadata(string name, string value, string part, BoundedXmlNode? node = null)
        {
            Charge(ResourceKind.Objects, 1, "DOCX_OBJECT_LIMIT");
            _metadata.Add(new(_metadataOrder++, name, value, node is null ? PartLocation(part, 0) : XmlLocation(part, node)));
        }

        private void AddRelationship(OpcRelationship relationship, string kind)
        {
            _relationships.Add(new(_relationshipOrder++, kind, relationship.SourcePart,
                relationship.ResolvedPart ?? relationship.Target, RelationshipLocation(relationship.SourcePart)));
        }

        private void StructuralIssue(string code, string message, string part)
        {
            StructurallyInvalid = true;
            Issue(code, message, true, part);
        }

        private void Issue(string code, string message, bool incomplete, string part, BoundedXmlNode? node = null)
        {
            Charge(ResourceKind.Objects, 1, "DOCX_ISSUE_LIMIT");
            HasIncompleteEvidence |= incomplete;
            _issues.Add(new(_issueOrder++, incomplete ? ExtractionIssueSeverity.Warning : ExtractionIssueSeverity.Information,
                code, message, node is null ? PartLocation(part, 0) : XmlLocation(part, node)));
        }

        private void IssueOnce(string key, string code, string message, bool incomplete, string part, BoundedXmlNode? node = null)
        {
            if (_issueKeys.Add(key))
            {
                Issue(code, message, incomplete, part, node);
            }
        }

        private void AddTerminalIssue(string code, string message, bool incomplete)
        {
            HasIncompleteEvidence |= incomplete;
            _ = _budget.TryCharge(ResourceKind.Objects, 1);
            _issues.Add(new(_issueOrder++, incomplete ? ExtractionIssueSeverity.Warning : ExtractionIssueSeverity.Information,
                code, message, PartLocation("/", 0)));
        }

        private sealed class SegmentBuffer
        {
            internal StringBuilder Text { get; } = new();
            internal BoundedXmlNode? Source { get; set; }
        }
    }

    private enum TextMode
    {
        None,
        Current,
        Deleted,
        Instruction,
        Drawing,
    }

    private sealed class ExtractionAbortException(ExtractionOutcome outcome, string code, string message) : Exception(message)
    {
        internal ExtractionOutcome Outcome { get; } = outcome;
        internal string Code { get; } = code;
    }

    private static OpcPart? Part(OpcPackage package, string name)
    {
        foreach (OpcPart part in package.Parts)
        {
            if (string.Equals(part.Name, name, StringComparison.Ordinal)) return part;
        }

        return null;
    }

    private static string? RelationshipKind(string type) => type switch
    {
        TransitionalRelationshipBase + "officeDocument" or StrictRelationshipBase + "officeDocument" => "officeDocument",
        CorePropertiesRelationship => "coreProperties",
        TransitionalRelationshipBase + "extended-properties" or StrictRelationshipBase + "extended-properties" => "extendedProperties",
        TransitionalRelationshipBase + "custom-properties" or StrictRelationshipBase + "custom-properties" => "customProperties",
        TransitionalRelationshipBase + "header" or StrictRelationshipBase + "header" => "header",
        TransitionalRelationshipBase + "footer" or StrictRelationshipBase + "footer" => "footer",
        TransitionalRelationshipBase + "footnotes" or StrictRelationshipBase + "footnotes" => "footnotes",
        TransitionalRelationshipBase + "endnotes" or StrictRelationshipBase + "endnotes" => "endnotes",
        TransitionalRelationshipBase + "comments" or StrictRelationshipBase + "comments" => "comments",
        TransitionalRelationshipBase + "styles" or StrictRelationshipBase + "styles" => "styles",
        TransitionalRelationshipBase + "numbering" or StrictRelationshipBase + "numbering" => "numbering",
        TransitionalRelationshipBase + "settings" or StrictRelationshipBase + "settings" => "settings",
        TransitionalRelationshipBase + "webSettings" or StrictRelationshipBase + "webSettings" => "webSettings",
        TransitionalRelationshipBase + "fontTable" or StrictRelationshipBase + "fontTable" => "fontTable",
        TransitionalRelationshipBase + "theme" or StrictRelationshipBase + "theme" => "theme",
        TransitionalRelationshipBase + "image" or StrictRelationshipBase + "image" => "image",
        TransitionalRelationshipBase + "hyperlink" or StrictRelationshipBase + "hyperlink" => "hyperlink",
        TransitionalRelationshipBase + "oleObject" or StrictRelationshipBase + "oleObject" => "oleObject",
        TransitionalRelationshipBase + "package" or StrictRelationshipBase + "package" => "package",
        TransitionalRelationshipBase + "aFChunk" or StrictRelationshipBase + "aFChunk" => "aFChunk",
        TransitionalRelationshipBase + "vbaProject" or StrictRelationshipBase + "vbaProject" => "vbaProject",
        TransitionalRelationshipBase + "control" or StrictRelationshipBase + "control" => "control",
        TransitionalRelationshipBase + "chart" or StrictRelationshipBase + "chart" => "chart",
        TransitionalRelationshipBase + "diagramData" or StrictRelationshipBase + "diagramData" => "diagramData",
        TransitionalRelationshipBase + "diagramLayout" or StrictRelationshipBase + "diagramLayout" => "diagramLayout",
        TransitionalRelationshipBase + "diagramStyle" or StrictRelationshipBase + "diagramStyle" => "diagramStyle",
        TransitionalRelationshipBase + "diagramColors" or StrictRelationshipBase + "diagramColors" => "diagramColors",
        TransitionalRelationshipBase + "customXml" or StrictRelationshipBase + "customXml" => "customXml",
        _ => null,
    };

    private static string? MainVariant(string contentType) => contentType switch
    {
        DocumentContentType => "document",
        TemplateContentType => "template",
        MacroDocumentContentType => "macro-enabled-document",
        MacroTemplateContentType => "macro-enabled-template",
        _ => null,
    };

    private static bool IsStoryContentType(string kind, string contentType) => (kind, contentType) switch
    {
        ("header", "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml") => true,
        ("footer", "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml") => true,
        ("footnotes", "application/vnd.openxmlformats-officedocument.wordprocessingml.footnotes+xml") => true,
        ("endnotes", "application/vnd.openxmlformats-officedocument.wordprocessingml.endnotes+xml") => true,
        ("comments", "application/vnd.openxmlformats-officedocument.wordprocessingml.comments+xml") => true,
        _ => false,
    };

    private static bool IsDependencyContentType(string kind, string contentType) => (kind, contentType) switch
    {
        ("styles", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml") => true,
        ("numbering", "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml") => true,
        ("settings", "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml") => true,
        ("webSettings", "application/vnd.openxmlformats-officedocument.wordprocessingml.webSettings+xml") => true,
        ("fontTable", "application/vnd.openxmlformats-officedocument.wordprocessingml.fontTable+xml") => true,
        ("theme", "application/vnd.openxmlformats-officedocument.theme+xml") => true,
        _ => false,
    };

    private static string AssetKind(string relationshipKind) => relationshipKind switch
    {
        "image" => "image",
        "vbaProject" => "macro",
        "control" => "activex",
        "oleObject" => "ole-embedding",
        "package" => "embedded-package",
        "aFChunk" => "alternative-content",
        "customXml" => "custom-xml",
        _ => "graphical-data",
    };

    private static bool IsWord(BoundedXmlNode node) => node.NamespaceUri is TransitionalWord or StrictWord;

    private static bool IsDrawingMarkup(BoundedXmlNode node) => node.NamespaceUri is
        TransitionalDrawing or StrictDrawing or
        TransitionalWordprocessingDrawing or StrictWordprocessingDrawing or
        TransitionalPicture or StrictPicture or WordprocessingShape or
        Office2010Drawing or Office2010WordprocessingDrawing;

    private static bool IsRecognisedWordElement(string name) => name is
        "document" or "body" or "hdr" or "ftr" or "footnotes" or "footnote" or "endnotes" or "endnote" or "comments" or "comment" or
        "p" or "pPr" or "r" or "rPr" or "t" or "delText" or "instrText" or "fldSimple" or "fldChar" or
        "tab" or "br" or "cr" or "noBreakHyphen" or "softHyphen" or "del" or "ins" or "moveFrom" or "moveTo" or
        "tbl" or "tblPr" or "tblGrid" or "gridCol" or "tr" or "trPr" or "trHeight" or "tc" or "tcPr" or "tcW" or "gridSpan" or
        "tblInd" or "tblLook" or "tblStyle" or "tblW" or
        "sectPr" or "pgSz" or "pgMar" or "cols" or "docGrid" or
        "bookmarkStart" or "bookmarkEnd" or "hyperlink" or "drawing" or "pict" or "object" or
        "altChunk" or "sdt" or "sdtPr" or "sdtContent" or "smartTag" or "proofErr" or "permStart" or "permEnd" or
        "lastRenderedPageBreak" or "sym" or "footnoteReference" or "endnoteReference" or "commentReference" or
        "style" or "name" or "basedOn" or "next" or "link" or "numPr" or "ilvl" or "numId" or "tabs" or
        "spacing" or "ind" or "jc" or "b" or "bCs" or "i" or "iCs" or "u" or "color" or "highlight" or
        "sz" or "szCs" or "rFonts" or "lang" or "noProof" or "pStyle" or "rStyle";

    private static string? Attribute(BoundedXmlNode node, string localName)
    {
        foreach (BoundedXmlAttributeValue attribute in node.Attributes)
        {
            if (attribute.LocalName == localName &&
                (attribute.NamespaceUri.Length == 0 || attribute.NamespaceUri is TransitionalWord or StrictWord))
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private static string? RelationshipAttribute(BoundedXmlNode node, string localName)
    {
        foreach (BoundedXmlAttributeValue attribute in node.Attributes)
        {
            if (attribute.LocalName == localName &&
                attribute.NamespaceUri is TransitionalRelationships or StrictRelationships)
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private static SourceLocation XmlLocation(string part, BoundedXmlNode node) =>
        new(SourceLocationKind.LogicalRange, "xml-line-column", part,
            Math.Max(0, node.Source.LineNumber), Math.Max(0, node.Source.LinePosition));

    private static SourceLocation PartLocation(string part, long length) =>
        new(SourceLocationKind.ContainerEntry, "docx-part", part, 0, Math.Max(0, length));

    private static SourceLocation RelationshipLocation(string sourcePart) =>
        new(SourceLocationKind.ContainerEntry, "docx-opc-relationship", sourcePart, 0, 0);
}
