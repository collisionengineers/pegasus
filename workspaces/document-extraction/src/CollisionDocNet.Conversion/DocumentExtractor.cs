using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using CollisionDocNet.Core;
using CollisionDocNet.Email;
using CollisionDocNet.Model;
using CollisionDocNet.Outlook;
using CollisionDocNet.Pdf;
using CollisionDocNet.Storage.Detection;
using CollisionDocNet.Writer;
using CollisionDocNet.Writer.OpenXml;
using ModelContainer = CollisionDocNet.Model.DetectedContainer;
using ModelFormat = CollisionDocNet.Model.DetectedFormat;
using StorageContainer = CollisionDocNet.Storage.Detection.DetectedContainer;
using StorageFormat = CollisionDocNet.Storage.Detection.DetectedFormat;

namespace CollisionDocNet.Extraction;

/// <summary>The single public managed extraction entry point for EXT-API-001.</summary>
public static class DocumentExtractor
{
    public const string ExtractorVersion = "collisiondocnet/0.1";
    public const string SpecificationIdentity = "EXT-API-001/2026-07-23";

    public static async ValueTask<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var operation = new ExtractionOperation(request.Policy.Limits, cancellationToken);
        ReadOnlyMemory<byte> bytes;

        if (request.Input.Kind == ExtractionInputKind.Bytes)
        {
            bytes = request.Input.Bytes.AsMemory();
            if (!operation.Budget.TryCharge(ResourceKind.InputBytes, bytes.Length))
            {
                return operation.Complete(Failure(bytes, request, SignatureHint(bytes.Span),
                    ExtractionOutcome.ResourceLimitExceeded, "INPUT_LIMIT", "Input exceeded the configured byte limit."));
            }

            if (!operation.Check(out ExtractionOutcome interrupt))
            {
                return operation.Complete(Failure(bytes, request, SignatureHint(bytes.Span), interrupt,
                    InterruptCode(interrupt), InterruptMessage(interrupt)));
            }
        }
        else
        {
            MaterializeResult materialized;
            try
            {
                materialized = await MaterializeAsync(request.Input.Stream!, operation).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsRecoverableInputException(exception))
            {
                return operation.Complete(Failure(ReadOnlyMemory<byte>.Empty, request, default, ExtractionOutcome.TechnicalFailure,
                    "INPUT_READ_FAILED", "The caller-supplied stream could not be read."));
            }

            if (materialized.Outcome is ExtractionOutcome outcome)
            {
                return operation.Complete(Failure(materialized.Bytes, request, SignatureHint(materialized.Bytes.Span), outcome,
                    outcome == ExtractionOutcome.ResourceLimitExceeded ? "INPUT_LIMIT" : InterruptCode(outcome),
                    outcome == ExtractionOutcome.ResourceLimitExceeded ? "Input exceeded the configured byte limit." : InterruptMessage(outcome)));
            }

            bytes = materialized.Bytes;
        }

        ExtractionResult extracted = ExtractMaterialized(bytes, request, operation);
        FormatDetectionResult? rootDetection = operation.Detection;
        extracted = AddDetectionAndRequestEvidence(extracted, request, rootDetection);
        var accumulation = NestedAccumulation.From(extracted);
        var ancestors = new HashSet<string>(StringComparer.Ordinal) { extracted.SourceHash.Hex };
        extracted = ExpandNested(extracted, request, operation, accumulation, ancestors, "$", 0);
        extracted = ApplyPublicPayloadContract(extracted);
        operation.Detection = rootDetection;
        return operation.Complete(Reconcile(extracted, request, operation));
    }

    public static ValueTask<ExtractionResult> ExtractAsync(
        ReadOnlyMemory<byte> bytes,
        string sourceIdentity,
        string? fileName = null,
        string? declaredMediaType = null,
        ExtractionPolicy? policy = null,
        CancellationToken cancellationToken = default) =>
        ExtractAsync(new ExtractionRequest(ExtractionInput.FromBytes(bytes.Span), sourceIdentity, fileName,
            declaredMediaType, policy ?? ExtractionPolicy.CreateDefault()), cancellationToken);

    private static async ValueTask<MaterializeResult> MaterializeAsync(Stream stream, ExtractionOperation operation)
    {
        int initialCapacity = (int)Math.Min(operation.Limits.MaxInputBytes, 64 * 1024);
        using var buffer = new MemoryStream(initialCapacity);
        byte[] block = new byte[16 * 1024];
        while (true)
        {
            if (!operation.Check(out ExtractionOutcome interrupt))
            {
                return new(buffer.ToArray(), interrupt);
            }

            int read;
            try
            {
                read = await stream.ReadAsync(block.AsMemory(), operation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new(buffer.ToArray(), operation.InterruptionOutcome);
            }

            if (read == 0)
            {
                return new(buffer.ToArray(), null);
            }

            if (!operation.Budget.TryCharge(ResourceKind.InputBytes, read))
            {
                return new(buffer.ToArray(), ExtractionOutcome.ResourceLimitExceeded);
            }

            buffer.Write(block, 0, read);
        }
    }

    private static ExtractionResult ExtractMaterialized(
        ReadOnlyMemory<byte> bytes,
        ExtractionRequest request,
        ExtractionOperation operation)
    {
        SignatureDetection signature = SignatureHint(bytes.Span);
        FormatDetectionResult detection;
        try
        {
            detection = FileFormatDetector.Detect(bytes, request.FileName, request.DeclaredMediaType,
                new FileFormatDetectionLimits
                {
                    MaximumInputBytes = checked((int)Math.Min(request.Policy.Limits.MaxInputBytes, int.MaxValue)),
                }, operation.Token);
        }
        catch (OperationCanceledException)
        {
            ExtractionOutcome outcome = operation.InterruptionOutcome;
            return Failure(bytes, request, signature, outcome, InterruptCode(outcome), InterruptMessage(outcome));
        }
        catch (OverflowException)
        {
            return Failure(bytes, request, signature, ExtractionOutcome.ResourceLimitExceeded,
                "DETECTION_ARITHMETIC_LIMIT", "Checked detector accounting exceeded a supported bound.");
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or IOException)
        {
            return Failure(bytes, request, signature, signature.Container == ModelContainer.Unknown
                ? ExtractionOutcome.UnsupportedFormat : ExtractionOutcome.Corrupt,
                "DETECTION_INVALID_STRUCTURE", "The candidate container was structurally invalid.");
        }
        catch (Exception)
        {
            return Failure(bytes, request, signature, ExtractionOutcome.TechnicalFailure,
                "DETECTION_TECHNICAL_FAILURE", "Format detection failed without exposing input content.");
        }
        operation.Detection = detection;

        if (detection.DiagnosticCode == "cancelled")
        {
            ExtractionOutcome outcome = operation.InterruptionOutcome;
            return Failure(bytes, request, signature, outcome, InterruptCode(outcome), InterruptMessage(outcome));
        }

        if (detection.DiagnosticCode == "input-limit-exceeded")
        {
            return Failure(bytes, request, signature, ExtractionOutcome.ResourceLimitExceeded,
                "DETECTION_INPUT_LIMIT", "Input exceeded the detector limit.");
        }

        if (detection.IsAmbiguous)
        {
            return Failure(bytes, request, signature, ExtractionOutcome.UnsupportedFeature,
                "AMBIGUOUS_FORMAT", "The input matched more than one supported format; no parser was selected.");
        }

        SignatureDetection detected = detection.Candidates.Length == 1
            ? MapDetection(detection.Candidates[0])
            : signature;
        try
        {
            return detection.Format switch
            {
                StorageFormat.Pdf => MapPdf(bytes, request, operation.Token),
                StorageFormat.WordBinary => MapWordBinary(bytes, request, operation.Token),
                StorageFormat.WordprocessingMl => DocxExtractor.Extract(bytes,
                    new DocxExtractionOptions { ResourceLimits = request.Policy.Limits }, operation.Token),
                StorageFormat.OutlookItem => MapMsg(bytes, request, operation.Token),
                StorageFormat.InternetMessage => EmlExtractor.Extract(bytes, request.Policy.Limits,
                    cancellationToken: operation.Token),
                StorageFormat.EncryptedOpenXml => Failure(bytes, request, detected, ExtractionOutcome.Encrypted,
                    "OOXML_ENCRYPTED", "The encrypted OOXML package was classified without decryption."),
                _ when detection.DiagnosticCode?.StartsWith("cfb-", StringComparison.Ordinal) == true =>
                    Failure(bytes, request, detected, ExtractionOutcome.Corrupt,
                        "CFB_STRUCTURE_INVALID", CompoundFailureMessage(detection)),
                _ when detected.Container != ModelContainer.Unknown => Failure(bytes, request, detected, ExtractionOutcome.Corrupt,
                    "DETECTED_CONTAINER_CORRUPT", "A supported container signature was present, but its required structure was invalid."),
                _ => Failure(bytes, request, detected, ExtractionOutcome.UnsupportedFormat,
                    "UNSUPPORTED_FORMAT", "The input did not match a supported format."),
            };
        }
        catch (OperationCanceledException)
        {
            ExtractionOutcome outcome = operation.InterruptionOutcome;
            return Failure(bytes, request, detected, outcome, InterruptCode(outcome), InterruptMessage(outcome));
        }
        catch (OverflowException)
        {
            return Failure(bytes, request, detected, ExtractionOutcome.ResourceLimitExceeded,
                "EXTRACTION_ARITHMETIC_LIMIT", "Checked resource accounting exceeded a supported bound.");
        }
        catch (NotSupportedException)
        {
            return Failure(bytes, request, detected, ExtractionOutcome.UnsupportedFeature,
                "EXTRACTION_UNSUPPORTED_FEATURE", "The detected input uses an unsupported feature.");
        }
        catch (Exception exception) when (exception is InvalidDataException or PdfParseException)
        {
            return Failure(bytes, request, detected, ExtractionOutcome.Corrupt,
                "EXTRACTION_INVALID_STRUCTURE", "The detected input is structurally invalid.");
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            return Failure(bytes, request, detected, ExtractionOutcome.Corrupt,
                "EXTRACTION_INVALID_STRUCTURE", "The detected input is structurally invalid.");
        }
        catch (Exception)
        {
            return Failure(bytes, request, detected, ExtractionOutcome.TechnicalFailure,
                "EXTRACTION_TECHNICAL_FAILURE", "The selected extractor failed without exposing input content.");
        }
    }

    private static ExtractionResult AddDetectionAndRequestEvidence(
        ExtractionResult result,
        ExtractionRequest request,
        FormatDetectionResult? detection)
    {
        var metadata = result.Metadata.ToBuilder();
        int metadataOrder = NextOrder(metadata.Select(static item => item.Order));
        metadata.Add(new(metadataOrder++, "sourceIdentity", request.SourceIdentity, null));
        if (request.FileName is not null) metadata.Add(new(metadataOrder++, "inputFileName", request.FileName, null));
        if (request.DeclaredMediaType is not null) metadata.Add(new(metadataOrder, "declaredMediaType", request.DeclaredMediaType, null));

        var issues = result.Issues.ToBuilder();
        int issueOrder = NextOrder(issues.Select(static item => item.Order));
        if (detection?.FilenameHintMismatch == true)
        {
            issues.Add(new(issueOrder++, ExtractionIssueSeverity.Warning, "FILENAME_HINT_MISMATCH",
                "The filename extension did not match byte-level detection.", null));
        }

        if (detection?.MediaTypeHintMismatch == true)
        {
            issues.Add(new(issueOrder, ExtractionIssueSeverity.Warning, "MEDIA_TYPE_HINT_MISMATCH",
                "The declared media type did not match byte-level detection.", null));
        }

        ExtractionOutcome outcome = result.Outcome == ExtractionOutcome.Complete && issues.Count != result.Issues.Length
            ? ExtractionOutcome.Partial
            : result.Outcome;
        return Rebuild(result, outcome, issues, metadata: metadata, policyIdentity: request.Policy.PolicyId);
    }

    private static ExtractionResult ExpandNested(
        ExtractionResult parent,
        ExtractionRequest rootRequest,
        ExtractionOperation operation,
        NestedAccumulation accumulation,
        HashSet<string> ancestors,
        string parentPath,
        int depth)
    {
        if (parent.Assets.IsEmpty || IsTerminal(parent.Outcome))
        {
            return parent;
        }

        var nested = parent.NestedResults.ToBuilder();
        var relationships = parent.Relationships.ToBuilder();
        var issues = parent.Issues.ToBuilder();
        ExtractionOutcome outcome = parent.Outcome;
        int relationshipOrder = NextOrder(relationships.Select(static item => item.Order));
        int issueOrder = NextOrder(issues.Select(static item => item.Order));

        foreach (ReviewAsset asset in parent.Assets)
        {
            if (!IsNestedCandidate(asset))
            {
                continue;
            }

            if (!operation.Check(out ExtractionOutcome interrupt))
            {
                issues.Add(new(issueOrder++, ExtractionIssueSeverity.Error, InterruptCode(interrupt),
                    InterruptMessage(interrupt), asset.SourceLocation));
                outcome = interrupt;
                break;
            }

            int childDepth = checked(depth + 1);
            if (childDepth > operation.Limits.MaxNestingDepth)
            {
                issues.Add(new(issueOrder++, ExtractionIssueSeverity.Error, "NESTED_DEPTH_LIMIT",
                    "An embedded source exceeded the configured nesting-depth limit and was not interpreted.", asset.SourceLocation));
                outcome = ExtractionOutcome.ResourceLimitExceeded;
                break;
            }

            if (ancestors.Contains(asset.ContentHash.Hex))
            {
                issues.Add(new(issueOrder++, ExtractionIssueSeverity.Warning, "NESTED_CYCLE_SUPPRESSED",
                    "An embedded source repeated an ancestor content hash and was retained without recursive interpretation.", asset.SourceLocation));
                outcome = Incomplete(outcome);
                continue;
            }

            string childPath = string.Concat(parentPath, "/", asset.StableId);
            string childIdentity = StableIdentity.Create("nested-source", rootRequest.SourceIdentity,
                childPath, asset.StableId, asset.ContentHash.Hex);
            ExtractionPolicy childPolicy = accumulation.CreateRemainingPolicy(
                rootRequest.Policy, asset.Content.Length, childDepth, operation.Remaining);
            var childRequest = new ExtractionRequest(ExtractionInput.FromBytes(asset.Content.AsSpan()),
                childIdentity, null, null, childPolicy);

            FormatDetectionResult? savedDetection = operation.Detection;
            operation.Detection = null;
            ExtractionResult child = ExtractMaterialized(asset.Content.AsMemory(), childRequest, operation);
            FormatDetectionResult? childDetection = operation.Detection;
            operation.Detection = savedDetection;
            child = AddDetectionAndRequestEvidence(child, childRequest, childDetection);
            child = AddNestingEvidence(child, childPath, asset, parent.SourceHash, rootRequest.Policy.PolicyId);

            if (child.Outcome == ExtractionOutcome.UnsupportedFormat)
            {
                issues.Add(new(issueOrder++, ExtractionIssueSeverity.Information, "NESTED_FORMAT_UNSUPPORTED",
                    "An embedded source did not match a supported format; its hash and descriptor were retained without emitting its bytes.", asset.SourceLocation));
                continue;
            }

            if (!accumulation.TryAddParsedTree(child, operation.Limits))
            {
                child = AddTerminalIssue(child, ExtractionOutcome.ResourceLimitExceeded,
                    "NESTED_CUMULATIVE_RESOURCE_LIMIT", "The embedded extraction exceeded the remaining cumulative resource budget.");
            }

            var childAncestors = new HashSet<string>(ancestors, StringComparer.Ordinal) { child.SourceHash.Hex };
            child = ExpandNested(child, rootRequest, operation, accumulation, childAncestors, childPath, childDepth);
            nested.Add(child);
            relationships.Add(new(relationshipOrder++, "nested-extraction", asset.StableId, childIdentity,
                asset.SourceLocation));

            if (child.Outcome is ExtractionOutcome.Cancelled or ExtractionOutcome.TimedOut or ExtractionOutcome.ResourceLimitExceeded)
            {
                issues.Add(new(issueOrder++, ExtractionIssueSeverity.Error, "NESTED_TERMINAL_OUTCOME",
                    "An embedded extraction reached a terminal operation outcome.", asset.SourceLocation));
                outcome = child.Outcome;
                break;
            }

            if (child.Outcome != ExtractionOutcome.Complete)
            {
                issues.Add(new(issueOrder++, ExtractionIssueSeverity.Warning, "NESTED_INCOMPLETE",
                    "An embedded source was not extracted completely; its local result contains the details.", asset.SourceLocation));
                outcome = Incomplete(outcome);
            }
        }

        return Rebuild(parent, outcome, issues, relationships: relationships, nestedResults: nested);
    }

    private static ExtractionResult AddNestingEvidence(
        ExtractionResult result,
        string path,
        ReviewAsset parentAsset,
        Sha256Digest parentHash,
        string policyIdentity)
    {
        var metadata = result.Metadata.ToBuilder();
        int order = NextOrder(metadata.Select(static item => item.Order));
        metadata.Add(new(order++, "nestingPath", path, parentAsset.SourceLocation));
        metadata.Add(new(order++, "parentAssetStableId", parentAsset.StableId, parentAsset.SourceLocation));
        metadata.Add(new(order++, "parentSourceHash", parentHash.Hex, parentAsset.SourceLocation));
        metadata.Add(new(order++, "embeddedContentHash", parentAsset.ContentHash.Hex, parentAsset.SourceLocation));
        if (parentAsset.OriginalName is not null)
        {
            metadata.Add(new(order++, "embeddedOriginalName", parentAsset.OriginalName, parentAsset.SourceLocation));
        }
        if (parentAsset.MediaType is not null)
        {
            metadata.Add(new(order, "embeddedMediaType", parentAsset.MediaType, parentAsset.SourceLocation));
        }
        return Rebuild(result, result.Outcome, result.Issues, metadata: metadata, policyIdentity: policyIdentity);
    }

    private static bool IsNestedCandidate(ReviewAsset asset)
    {
        if (asset.Kind != "mime-part")
        {
            return asset.Kind is "attachment" or "embedded-file" or "embedded-package" or "ole-embedding" or "ole-object";
        }

        return asset.MediaType is null ||
            !asset.MediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) &&
            !asset.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
            !asset.MediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) &&
            !asset.MediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTerminal(ExtractionOutcome outcome) => outcome is
        ExtractionOutcome.ResourceLimitExceeded or ExtractionOutcome.Cancelled or ExtractionOutcome.TimedOut;

    private static ExtractionOutcome Incomplete(ExtractionOutcome outcome) =>
        outcome == ExtractionOutcome.Complete ? ExtractionOutcome.Partial : outcome;

    private static ExtractionResult ApplyPublicPayloadContract(ExtractionResult result)
    {
        var assets = ImmutableArray.CreateBuilder<ReviewAsset>();
        var metadata = result.Metadata.ToBuilder();
        var issues = result.Issues.ToBuilder();
        var nested = ImmutableArray.CreateBuilder<ExtractionResult>(result.NestedResults.Length);
        int metadataOrder = NextOrder(metadata.Select(static item => item.Order));
        int issueOrder = NextOrder(issues.Select(static item => item.Order));
        ExtractionOutcome outcome = result.Outcome;

        foreach (ReviewAsset asset in result.Assets)
        {
            if (ImagePayloadPolicy.TryNormalize(asset, out ReviewAsset image))
            {
                assets.Add(image);
                continue;
            }

            metadata.Add(new(metadataOrder++, "nonPayload.binary", ImagePayloadPolicy.Describe(asset), asset.SourceLocation));
            if (ImagePayloadPolicy.IsClaimedImage(asset))
            {
                issues.Add(new(issueOrder++, ExtractionIssueSeverity.Warning, "IMAGE_ASSET_UNSUPPORTED_ENCODING",
                    "A claimed image did not use a currently validated image encoding, so its bytes were not emitted.", asset.SourceLocation));
                outcome = Incomplete(outcome);
            }
            else
            {
                issues.Add(new(issueOrder++, ExtractionIssueSeverity.Information, "NON_IMAGE_ASSET_NOT_EMITTED",
                    "A non-image binary was recorded as control evidence without emitting its bytes.", asset.SourceLocation));
            }
        }

        foreach (ExtractionResult child in result.NestedResults)
        {
            nested.Add(ApplyPublicPayloadContract(child));
        }

        return Rebuild(result, outcome, issues, metadata: metadata, assets: assets, nestedResults: nested);
    }

    private static ExtractionResult Reconcile(
        ExtractionResult result,
        ExtractionRequest request,
        ExtractionOperation operation)
    {
        ResourceMeasurements handler = result.Measurements;
        long decoded = SumDecoded(result);
        long text = SumText(result);
        long assetBytes = SumAssets(result);
        int objects = CountObjects(result);
        int nesting = Math.Max(handler.MaximumNestingDepth, MaximumNesting(result));
        bool limited =
            !ChargeMaximum(operation.Budget, ResourceKind.DecodedBytes, decoded) |
            !ChargeMaximum(operation.Budget, ResourceKind.Objects, Math.Max(handler.Objects, objects)) |
            !ChargeMaximum(operation.Budget, ResourceKind.TextCharacters, Math.Max(handler.TextCharacters, text)) |
            !ChargeMaximum(operation.Budget, ResourceKind.Assets, Math.Max(handler.Assets, result.Assets.Length)) |
            !ChargeMaximum(operation.Budget, ResourceKind.AssetBytes, Math.Max(handler.AssetBytes, assetBytes)) |
            !operation.Budget.TryObserveNestingDepth(nesting);

        if (!operation.Check(out ExtractionOutcome interrupt))
        {
            return AddTerminalIssue(result, interrupt, InterruptCode(interrupt), InterruptMessage(interrupt));
        }

        if (!limited)
        {
            return Rebuild(result, result.Outcome, result.Issues,
                measurements: ResourceMeasurements.FromSnapshot(operation.Budget.GetSnapshot(), operation.Elapsed),
                policyIdentity: request.Policy.PolicyId);
        }

        ExtractionResult limitedResult = AddTerminalIssue(result, ExtractionOutcome.ResourceLimitExceeded,
            "CUMULATIVE_RESOURCE_LIMIT", "Cumulative extraction output exceeded the configured operation budget.");
        return Rebuild(limitedResult, ExtractionOutcome.ResourceLimitExceeded, limitedResult.Issues,
            measurements: ResourceMeasurements.FromSnapshot(operation.Budget.GetSnapshot(), operation.Elapsed),
            policyIdentity: request.Policy.PolicyId);
    }

    private static bool ChargeMaximum(ResourceBudget budget, ResourceKind kind, long amount) =>
        amount == 0 || budget.TryCharge(kind, amount);

    private static long SumText(ExtractionResult result)
    {
        long count = 0;
        foreach (ContentSegment item in result.Content) count = checked(count + item.Text.Length);
        foreach (ExtractionResult nested in result.NestedResults) count = checked(count + SumText(nested));
        return count;
    }

    private static long SumDecoded(ExtractionResult result)
    {
        long count = result.Measurements.DecodedBytes;
        foreach (ExtractionResult nested in result.NestedResults)
        {
            if (nested.Metadata.Any(static item => item.Name == "nestingPath"))
            {
                count = checked(count + SumDecoded(nested));
            }
        }
        return count;
    }

    private static long SumAssets(ExtractionResult result)
    {
        long count = 0;
        foreach (ReviewAsset asset in result.Assets) count = checked(count + asset.Length);
        foreach (ExtractionResult nested in result.NestedResults) count = checked(count + SumAssets(nested));
        return count;
    }

    private static int CountObjects(ExtractionResult result)
    {
        int count = checked(result.Content.Length + result.Metadata.Length + result.Participants.Length +
            result.Relationships.Length + result.Assets.Length + result.Issues.Length + result.NestedResults.Length);
        foreach (ExtractionResult nested in result.NestedResults) count = checked(count + CountObjects(nested));
        return count;
    }

    private static int MaximumNesting(ExtractionResult result)
    {
        int maximum = 0;
        foreach (ExtractionResult nested in result.NestedResults)
        {
            maximum = Math.Max(maximum, checked(1 + MaximumNesting(nested)));
        }
        return maximum;
    }

    private static ExtractionResult MapPdf(ReadOnlyMemory<byte> bytes, ExtractionRequest request, CancellationToken cancellationToken)
    {
        ResourceLimits limits = request.Policy.Limits;
        PdfParseResult parsed = PdfParser.Parse(bytes, new PdfLimits
        {
            MaxInputBytes = checked((int)Math.Min(limits.MaxInputBytes, int.MaxValue)),
            MaxObjects = limits.MaxObjects,
            MaxDepth = limits.MaxNestingDepth,
            MaxDecodedStreamBytes = checked((int)Math.Min(limits.MaxDecodedBytes, int.MaxValue)),
            MaxAssetBytes = checked((int)Math.Min(limits.MaxAssetBytes, int.MaxValue)),
        }, cancellationToken: cancellationToken);
        var content = parsed.TextRuns.Select((run, index) => new ContentSegment(index, "pdf-text", run.Text,
            new SourceLocation(SourceLocationKind.LogicalRange, "pdf", $"page/{run.PageIndex}", run.ContentOffset, 0))).ToList();
        var metadata = parsed.Evidence.Items.Select((item, index) => new MetadataEntry(index,
            $"pdf.evidence.{item.Kind}.{item.Subtype}", SerializeProperties(item.Properties),
            new SourceLocation(SourceLocationKind.ByteRange, "pdf", "source", item.Offset, 0))).ToList();
        int metadataOrder = metadata.Count;
        foreach (PdfSignatureEvidence signature in parsed.Evidence.Signatures)
        {
            metadata.Add(new(metadataOrder++, "pdf.signature", SerializeSignature(signature),
                new SourceLocation(SourceLocationKind.ContainerEntry, "pdf", $"object/{signature.ObjectId.Number}/{signature.ObjectId.Generation}", 0, 0)));
        }
        if (parsed.Evidence.Encryption is PdfEncryptionEvidence encryption)
        {
            metadata.Add(new(metadataOrder, "pdf.encryption", SerializeEncryption(encryption), null));
        }
        var assets = parsed.Evidence.Assets.Select(asset => new ReviewAsset(asset.StableId, asset.Kind, asset.MediaType,
            asset.Name, asset.Bytes, new SourceLocation(SourceLocationKind.ContainerEntry, "pdf",
                $"object/{asset.ObjectId.Number}/{asset.ObjectId.Generation}", 0, asset.Bytes.Length)));
        var issues = parsed.Issues.Select((issue, index) => new ExtractionIssue(index,
            issue.Severity switch { PdfIssueSeverity.Error => ExtractionIssueSeverity.Error, PdfIssueSeverity.Warning => ExtractionIssueSeverity.Warning, _ => ExtractionIssueSeverity.Information },
            issue.Code, issue.Message, new SourceLocation(SourceLocationKind.ByteRange, "pdf", "source", Math.Max(0, issue.Offset), 0)));
        return new(ModelContainer.FlatBinary, ModelFormat.Pdf, Map(parsed.Outcome), Sha256Digest.Compute(bytes.Span),
            "collisiondocnet-pdf/0.1", "ISO-32000-1:2008;ISO-32000-2:2020", request.Policy.PolicyId,
            Measure(bytes.Length, parsed.Objects.Count, content.Sum(static run => run.Text.Length),
                parsed.Evidence.Assets.Sum(static asset => (long)asset.Bytes.Length), parsed.Evidence.Assets.Count),
            content, metadata, assets: assets, issues: issues);
    }

    private static ExtractionResult MapWordBinary(ReadOnlyMemory<byte> bytes, ExtractionRequest request, CancellationToken cancellationToken)
    {
        ResourceLimits limits = request.Policy.Limits;
        WordBinaryExtractionResult parsed = WordBinaryExtractor.Extract(bytes, new WordBinaryExtractionLimits
        {
            MaximumInputBytes = checked((int)Math.Min(limits.MaxInputBytes, int.MaxValue)),
            MaximumPassiveAssets = limits.MaxAssets,
        }, cancellationToken);
        var content = parsed.Stories.SelectMany(static story => story.Segments.Select(segment => (story, segment)))
            .Select((item, index) => new ContentSegment(index, $"doc-{item.story.Kind}-{item.segment.Kind}", item.segment.Text,
                new SourceLocation(SourceLocationKind.ByteRange, "doc", "WordDocument", item.segment.FileOffset, item.segment.ByteLength))).ToList();
        var metadata = parsed.Metadata.Select((item, index) => new MetadataEntry(index, item.Name, item.Value,
            new SourceLocation(SourceLocationKind.ContainerEntry, "doc", item.PropertySet, item.Offset, 0))).ToList();
        var relationships = new List<EvidenceRelationship>();
        var assets = new List<ReviewAsset>();
        var issues = parsed.Issues.Select((issue, index) => new ExtractionIssue(index, ExtractionIssueSeverity.Warning,
            issue.Code, issue.Message, issue.Offset is long offset ? new SourceLocation(SourceLocationKind.ByteRange,
                "doc", parsed.SelectedTableStream ?? "source", Math.Max(0, offset), 0) : null)).ToList();
        int order = metadata.Count;
        bool omittedAsset = false;
        foreach (WordPassiveAsset asset in parsed.PassiveAssets)
        {
            string identity = $"doc-passive:{asset.StableId}";
            metadata.Add(new(order++, "doc.passiveAsset", SerializeWordAsset(asset),
                new SourceLocation(SourceLocationKind.ContainerEntry, "doc", asset.SourcePath, 0, checked((long)asset.Length))));
            relationships.Add(new(relationships.Count, "passive-asset", request.SourceIdentity, identity, null));
            bool hashMatches = Sha256Digest.TryParse(asset.Sha256, out Sha256Digest expected) &&
                expected == Sha256Digest.Compute(asset.Content.AsSpan());
            if ((ulong)asset.Content.Length == asset.Length && hashMatches)
            {
                assets.Add(new(asset.StableId, WordAssetKind(asset.Kind), null, asset.SourceName,
                    asset.Content.AsMemory(), new SourceLocation(SourceLocationKind.ContainerEntry, "doc",
                        asset.SourcePath, 0, asset.Content.Length)));
            }
            else
            {
                omittedAsset = true;
                issues.Add(new(issues.Count, ExtractionIssueSeverity.Warning, "DOC_PASSIVE_ASSET_BYTES_INVALID",
                    "A passive binary occurrence was inventoried, but its supplied length or digest did not match its bytes.", null));
            }
        }
        ExtractionOutcome outcome = Map(parsed.Outcome);
        if (omittedAsset && outcome == ExtractionOutcome.Complete) outcome = ExtractionOutcome.Partial;
        return new(ModelContainer.CompoundFile, ModelFormat.WordBinary, outcome, Sha256Digest.Compute(bytes.Span),
            "collisiondocnet-doc/0.1", "MS-DOC/2026-02-17", request.Policy.PolicyId,
            Measure(bytes.Length, parsed.Pieces.Length + parsed.Structures.Length,
                parsed.Stories.Sum(static story => story.Segments.Sum(static segment => segment.Text.Length)),
                assets.Sum(static asset => asset.Length), assets.Count),
            content, metadata, relationships: relationships, assets: assets, issues: issues);
    }

    private static ExtractionResult MapMsg(ReadOnlyMemory<byte> bytes, ExtractionRequest request, CancellationToken cancellationToken)
    {
        ResourceLimits limits = request.Policy.Limits;
        MsgDocument parsed = MsgReader.Read(bytes, new MsgReadLimits(limits.MaxObjects, limits.MaxObjects, limits.MaxAssets,
            limits.MaxNestingDepth, limits.MaxDecodedBytes, checked((int)Math.Min(limits.MaxDecodedBytes, int.MaxValue))), cancellationToken);
        var content = new List<ContentSegment>();
        AddText(content, "msg-body-canonical", parsed.Bodies.CanonicalText);
        AddText(content, "msg-body-plain", parsed.Bodies.PlainText);
        AddText(content, "msg-body-html", parsed.Bodies.HtmlText);
        AddText(content, "msg-body-rtf", parsed.Bodies.RtfText);
        var metadata = parsed.Projection.Fields.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select((pair, index) => new MetadataEntry(index, pair.Key, pair.Value, null)).ToList();
        var participants = parsed.Recipients.Where(static recipient => !string.IsNullOrWhiteSpace(recipient.DisplayName) || !string.IsNullOrWhiteSpace(recipient.EmailAddress))
            .Select((recipient, index) => new Participant(index, recipient.Role, EmptyToNull(recipient.DisplayName), EmptyToNull(recipient.EmailAddress), null));
        var assets = new List<ReviewAsset>();
        var relationships = new List<EvidenceRelationship>();
        var issues = parsed.Issues.Select((issue, index) => new ExtractionIssue(index, ExtractionIssueSeverity.Warning,
            issue.Code, issue.Message, issue.StorageId is uint id ? new SourceLocation(SourceLocationKind.ContainerEntry,
                "msg", $"storage/{id}", 0, 0) : null)).ToList();
        foreach (MsgAttachment attachment in parsed.Attachments)
        {
            string identity = StableIdentity.Create("msg-attachment", attachment.SourceOrder.ToString(CultureInfo.InvariantCulture),
                attachment.Content.IsDefaultOrEmpty ? "passive" : Sha256Digest.Compute(attachment.Content.AsSpan()).Hex);
            if (!attachment.Content.IsDefaultOrEmpty)
            {
                assets.Add(new(identity, "attachment", attachment.MediaType, attachment.FileName, attachment.Content.AsMemory(),
                    new SourceLocation(SourceLocationKind.ContainerEntry, "msg", $"storage/{attachment.StorageId}", 0, attachment.Content.Length)));
            }
            relationships.Add(new(relationships.Count, attachment.IsInline ? "inline-attachment" : "attachment",
                request.SourceIdentity, identity, null));
            if (attachment.EmbeddedMessage is not null)
            {
                metadata.Add(new(metadata.Count, "msg.embeddedMessage", $"stableId={identity};kind={attachment.EmbeddedMessage.Projection.Kind}", null));
                issues.Add(new(issues.Count, ExtractionIssueSeverity.Warning, "MSG_EMBEDDED_MESSAGE_PASSIVE",
                    "An embedded message occurrence was inventoried; its original compound bytes were not exposed for nested source hashing.", null));
            }
            if (attachment.PassiveReference is not null || !attachment.PassiveStorages.IsEmpty || attachment.Content.IsDefaultOrEmpty)
            {
                metadata.Add(new(metadata.Count, "msg.passiveAttachment",
                    $"stableId={identity};method={attachment.Method.ToString(CultureInfo.InvariantCulture)};storages={attachment.PassiveStorages.Length.ToString(CultureInfo.InvariantCulture)}", null));
                issues.Add(new(issues.Count, ExtractionIssueSeverity.Warning, "MSG_ATTACHMENT_PASSIVE",
                    "An attachment occurrence could not be materialised and remains passive evidence.", null));
            }
        }
        return new(ModelContainer.CompoundFile, ModelFormat.OutlookItem, Map(parsed.Outcome), Sha256Digest.Compute(bytes.Span),
            "collisiondocnet-msg/0.1", MsgReader.SpecificationIdentity, request.Policy.PolicyId,
            Measure(bytes.Length, parsed.Properties.Length, content.Sum(static item => item.Text.Length),
                assets.Sum(static item => item.Length), assets.Count), content, metadata, participants,
            relationships, assets, issues);
    }

    private static string SerializeProperties(IReadOnlyDictionary<string, string> properties) =>
        string.Join(";", properties.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair => $"{pair.Key}={pair.Value}"));

    private static string SerializeSignature(PdfSignatureEvidence value) =>
        $"object={value.ObjectId.Number.ToString(CultureInfo.InvariantCulture)}/{value.ObjectId.Generation.ToString(CultureInfo.InvariantCulture)};byteRange={string.Join(',', value.ByteRange.Select(static item => item.ToString(CultureInfo.InvariantCulture)))};valid={value.ByteRangeStructurallyValid.ToString(CultureInfo.InvariantCulture)};wholeInput={value.CoversWholeInput.ToString(CultureInfo.InvariantCulture)};subFilter={value.SubFilter ?? string.Empty};signatureBytes={value.SignatureByteCount.ToString(CultureInfo.InvariantCulture)}";

    private static string SerializeEncryption(PdfEncryptionEvidence value) =>
        $"handler={value.Handler};version={value.Version?.ToString(CultureInfo.InvariantCulture) ?? string.Empty};revision={value.Revision?.ToString(CultureInfo.InvariantCulture) ?? string.Empty};subFilter={value.SubFilter ?? string.Empty};publicKey={value.IsPublicKeyHandler.ToString(CultureInfo.InvariantCulture)}";

    private static string SerializeWordAsset(WordPassiveAsset value) =>
        $"stableId={value.StableId};kind={value.Kind};length={value.Length.ToString(CultureInfo.InvariantCulture)};sha256={value.Sha256};streamId={value.StreamId.ToString(CultureInfo.InvariantCulture)}";

    private static string WordAssetKind(WordPassiveAssetKind kind) => kind switch
    {
        WordPassiveAssetKind.PictureData => "image",
        WordPassiveAssetKind.OleObject => "ole-object",
        WordPassiveAssetKind.EmbeddedPackage => "embedded-package",
        WordPassiveAssetKind.MacroProject => "macro",
        WordPassiveAssetKind.OfficeForm => "office-form",
        WordPassiveAssetKind.DrawingData => "drawing-data",
        WordPassiveAssetKind.CustomData => "custom-data",
        WordPassiveAssetKind.PropertySet => "property-set",
        _ => "unknown-stream",
    };

    private static void AddText(List<ContentSegment> content, string kind, string? value)
    {
        if (!string.IsNullOrEmpty(value)) content.Add(new(content.Count, kind, value, null));
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static ResourceMeasurements Measure(long input, int objects, long characters, long assetBytes, int assets) =>
        new(input, 0, Math.Max(0, objects), checked((int)Math.Min(Math.Max(0, characters), int.MaxValue)),
            Math.Max(0, assets), Math.Max(0, assetBytes), 0, 0);

    private static ExtractionResult Failure(ReadOnlyMemory<byte> bytes, ExtractionRequest request,
        SignatureDetection detection, ExtractionOutcome outcome, string code, string message) =>
        new(detection.Container, detection.Format, outcome, Sha256Digest.Compute(bytes.Span), ExtractorVersion,
            SpecificationIdentity, request.Policy.PolicyId, Measure(bytes.Length, 0, 0, 0, 0),
            issues: [new ExtractionIssue(0, ExtractionIssueSeverity.Error, code, message, null)]);

    private static ExtractionResult AddTerminalIssue(ExtractionResult result, ExtractionOutcome outcome, string code, string message)
    {
        var issues = result.Issues.ToBuilder();
        issues.Add(new(NextOrder(issues.Select(static item => item.Order)), ExtractionIssueSeverity.Error, code, message, null));
        return Rebuild(result, outcome, issues);
    }

    private static ExtractionResult Rebuild(ExtractionResult value, ExtractionOutcome outcome,
        IEnumerable<ExtractionIssue> issues, IEnumerable<MetadataEntry>? metadata = null,
        ResourceMeasurements? measurements = null, string? policyIdentity = null,
        IEnumerable<EvidenceRelationship>? relationships = null,
        IEnumerable<ReviewAsset>? assets = null,
        IEnumerable<ExtractionResult>? nestedResults = null) =>
        new(value.DetectedContainer, value.DetectedFormat, outcome, value.SourceHash, value.ExtractorVersion,
            value.SpecificationIdentity, policyIdentity ?? value.PolicyIdentity, measurements ?? value.Measurements,
            value.Content, metadata ?? value.Metadata, value.Participants, relationships ?? value.Relationships,
            assets ?? value.Assets, issues, nestedResults ?? value.NestedResults);

    private static int NextOrder(IEnumerable<int> orders)
    {
        int maximum = -1;
        foreach (int order in orders) maximum = Math.Max(maximum, order);
        return checked(maximum + 1);
    }

    private static SignatureDetection MapDetection(FormatCandidate candidate) => new(
        candidate.Container switch
        {
            StorageContainer.Pdf => ModelContainer.FlatBinary,
            StorageContainer.CompoundFile => ModelContainer.CompoundFile,
            StorageContainer.Zip => ModelContainer.ZipPackage,
            StorageContainer.InternetMessage => ModelContainer.InternetMessage,
            _ => ModelContainer.Unknown,
        },
        candidate.Format switch
        {
            StorageFormat.Pdf => ModelFormat.Pdf,
            StorageFormat.WordBinary => ModelFormat.WordBinary,
            StorageFormat.WordprocessingMl => ModelFormat.WordprocessingMl,
            StorageFormat.OutlookItem => ModelFormat.OutlookItem,
            StorageFormat.InternetMessage => ModelFormat.InternetMessage,
            _ => ModelFormat.Unknown,
        });

    private static SignatureDetection SignatureHint(ReadOnlySpan<byte> bytes)
    {
        int pdfLength = Math.Min(bytes.Length, 1024);
        if (bytes[..pdfLength].IndexOf("%PDF-"u8) >= 0) return new(ModelContainer.FlatBinary, ModelFormat.Pdf);
        ReadOnlySpan<byte> compoundSignature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
        if (bytes.StartsWith(compoundSignature)) return new(ModelContainer.CompoundFile, ModelFormat.Unknown);
        if (bytes.StartsWith("PK\u0003\u0004"u8) || bytes.StartsWith("PK\u0005\u0006"u8) || bytes.StartsWith("PK\u0007\u0008"u8)) return new(ModelContainer.ZipPackage, ModelFormat.Unknown);
        return default;
    }

    private static string InterruptCode(ExtractionOutcome outcome) => outcome == ExtractionOutcome.TimedOut ? "EXTRACTION_TIMED_OUT" : "EXTRACTION_CANCELLED";
    private static string InterruptMessage(ExtractionOutcome outcome) => outcome == ExtractionOutcome.TimedOut ? "Extraction exceeded the configured elapsed-time limit." : "Extraction was cancelled.";

    private static string CompoundFailureMessage(FormatDetectionResult detection) =>
        detection.DiagnosticLocation is uint location
            ? $"Compound-file validation failed with {detection.DiagnosticCode} at structural index {location}."
            : $"Compound-file validation failed with {detection.DiagnosticCode}.";

    private static bool IsRecoverableInputException(Exception exception) => exception is not
        OperationCanceledException and not OutOfMemoryException and not StackOverflowException and
        not AccessViolationException and not AppDomainUnloadedException;

    private static ExtractionOutcome Map(PdfParseOutcome outcome) => outcome switch
    {
        PdfParseOutcome.Complete => ExtractionOutcome.Complete,
        PdfParseOutcome.Partial => ExtractionOutcome.Partial,
        PdfParseOutcome.Encrypted => ExtractionOutcome.Encrypted,
        PdfParseOutcome.Corrupt => ExtractionOutcome.Corrupt,
        PdfParseOutcome.ResourceLimitExceeded => ExtractionOutcome.ResourceLimitExceeded,
        _ => ExtractionOutcome.UnsupportedFormat,
    };

    private static ExtractionOutcome Map(WordBinaryOutcome outcome) => outcome switch
    {
        WordBinaryOutcome.Complete => ExtractionOutcome.Complete,
        WordBinaryOutcome.Partial => ExtractionOutcome.Partial,
        WordBinaryOutcome.Encrypted => ExtractionOutcome.Encrypted,
        WordBinaryOutcome.Corrupt => ExtractionOutcome.Corrupt,
        WordBinaryOutcome.UnsupportedFormat => ExtractionOutcome.UnsupportedFormat,
        WordBinaryOutcome.UnsupportedFeature => ExtractionOutcome.UnsupportedFeature,
        WordBinaryOutcome.ResourceLimitExceeded => ExtractionOutcome.ResourceLimitExceeded,
        WordBinaryOutcome.Cancelled => ExtractionOutcome.Cancelled,
        _ => ExtractionOutcome.TechnicalFailure,
    };

    private static ExtractionOutcome Map(MsgReadOutcome outcome) => outcome switch
    {
        MsgReadOutcome.Complete => ExtractionOutcome.Complete,
        MsgReadOutcome.Partial => ExtractionOutcome.Partial,
        MsgReadOutcome.Encrypted => ExtractionOutcome.Encrypted,
        MsgReadOutcome.Corrupt => ExtractionOutcome.Corrupt,
        MsgReadOutcome.ResourceLimitExceeded => ExtractionOutcome.ResourceLimitExceeded,
        MsgReadOutcome.Cancelled => ExtractionOutcome.Cancelled,
        _ => ExtractionOutcome.TechnicalFailure,
    };

    private readonly record struct SignatureDetection(ModelContainer Container, ModelFormat Format);
    private readonly record struct MaterializeResult(ReadOnlyMemory<byte> Bytes, ExtractionOutcome? Outcome);

    private sealed class ExtractionOperation : IDisposable
    {
        private readonly CancellationToken _callerToken;
        private readonly CancellationTokenSource _deadline;
        private readonly CancellationTokenSource _linked;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        internal ExtractionOperation(ResourceLimits limits, CancellationToken callerToken)
        {
            Limits = limits;
            Budget = new(limits);
            _callerToken = callerToken;
            _deadline = new(limits.MaxElapsed, TimeProvider.System);
            _linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _deadline.Token);
        }

        internal ResourceLimits Limits { get; }
        internal ResourceBudget Budget { get; }
        internal CancellationToken Token => _linked.Token;
        internal TimeSpan Elapsed => _stopwatch.Elapsed;
        internal TimeSpan Remaining
        {
            get
            {
                TimeSpan remaining = Limits.MaxElapsed - _stopwatch.Elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromTicks(1);
            }
        }
        internal FormatDetectionResult? Detection { get; set; }
        internal ExtractionOutcome InterruptionOutcome => _callerToken.IsCancellationRequested
            ? ExtractionOutcome.Cancelled
            : _deadline.IsCancellationRequested ? ExtractionOutcome.TimedOut : ExtractionOutcome.Cancelled;

        internal bool Check(out ExtractionOutcome outcome)
        {
            if (!_linked.IsCancellationRequested)
            {
                outcome = default;
                return true;
            }
            outcome = InterruptionOutcome;
            return false;
        }

        internal ExtractionResult Complete(ExtractionResult result)
        {
            _stopwatch.Stop();
            return result;
        }

        public void Dispose()
        {
            _linked.Dispose();
            _deadline.Dispose();
        }
    }

    private sealed class NestedAccumulation
    {
        private long _decodedBytes;
        private int _objects;
        private int _textCharacters;
        private int _assets;
        private long _assetBytes;

        private NestedAccumulation() { }

        internal static NestedAccumulation From(ExtractionResult root)
        {
            var value = new NestedAccumulation();
            value.AddUnchecked(root);
            return value;
        }

        internal bool TryAddParsedTree(ExtractionResult result, ResourceLimits limits)
        {
            long decoded = result.Measurements.DecodedBytes;
            int objects = CountObjects(result);
            long text = SumText(result);
            int assets = CountAssets(result);
            long assetBytes = SumAssets(result);
            bool fits = Fits(_decodedBytes, decoded, limits.MaxDecodedBytes) &&
                Fits(_objects, objects, limits.MaxObjects) &&
                Fits(_textCharacters, text, limits.MaxTextCharacters) &&
                Fits(_assets, assets, limits.MaxAssets) &&
                Fits(_assetBytes, assetBytes, limits.MaxAssetBytes);
            Add(decoded, objects, text, assets, assetBytes);
            return fits;
        }

        internal ExtractionPolicy CreateRemainingPolicy(
            ExtractionPolicy original,
            int sourceLength,
            int depth,
            TimeSpan remaining)
        {
            ResourceLimits limits = original.Limits;
            var childLimits = new ResourceLimits(limits.PolicyId,
                Math.Max(1, sourceLength),
                Math.Max(1, limits.MaxDecodedBytes - Math.Min(limits.MaxDecodedBytes, _decodedBytes)),
                Math.Max(1, limits.MaxObjects - Math.Min(limits.MaxObjects, _objects)),
                Math.Max(1, limits.MaxTextCharacters - Math.Min(limits.MaxTextCharacters, _textCharacters)),
                Math.Max(0, limits.MaxAssets - Math.Min(limits.MaxAssets, _assets)),
                Math.Max(0, limits.MaxAssetBytes - Math.Min(limits.MaxAssetBytes, _assetBytes)),
                Math.Max(0, limits.MaxNestingDepth - depth),
                remaining);
            return new(original.PolicyId, original.NormalisationPolicyId, original.StableIdentityPolicyId, childLimits);
        }

        private void AddUnchecked(ExtractionResult result) => Add(result.Measurements.DecodedBytes,
            CountObjects(result), SumText(result), CountAssets(result), SumAssets(result));

        private void Add(long decoded, int objects, long text, int assets, long assetBytes)
        {
            _decodedBytes = SaturatingAdd(_decodedBytes, decoded);
            _objects = SaturatingAdd(_objects, objects);
            _textCharacters = text >= int.MaxValue
                ? int.MaxValue
                : SaturatingAdd(_textCharacters, checked((int)text));
            _assets = SaturatingAdd(_assets, assets);
            _assetBytes = SaturatingAdd(_assetBytes, assetBytes);
        }

        private static int CountAssets(ExtractionResult result)
        {
            int count = result.Assets.Length;
            foreach (ExtractionResult nested in result.NestedResults)
            {
                count = SaturatingAdd(count, CountAssets(nested));
            }
            return count;
        }

        private static bool Fits(long current, long amount, long maximum) => amount >= 0 && current <= maximum && amount <= maximum - current;
        private static bool Fits(int current, int amount, int maximum) => amount >= 0 && current <= maximum && amount <= maximum - current;
        private static long SaturatingAdd(long left, long right) => left > long.MaxValue - right ? long.MaxValue : left + right;
        private static int SaturatingAdd(int left, int right) => left > int.MaxValue - right ? int.MaxValue : left + right;
    }
}
