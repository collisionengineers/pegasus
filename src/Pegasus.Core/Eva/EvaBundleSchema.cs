using System.IO.Compression;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Eva;

public sealed record EvaReplayFields(
    string? WorkProvider,
    string? Vrm,
    string? VehicleModel,
    string? ClaimantName,
    string? Reference,
    string? IncidentDate,
    string? InstructionDate,
    string? InspectionDate,
    string? InspectionAddress,
    string? AccidentCircumstances,
    string? VatStatus,
    string? Mileage,
    string? MileageUnit);

public sealed record EvaBundleImage(
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    string FileName,
    string MediaType,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    ReadOnlyMemory<byte> Content,
    string Sha256,
    bool CustodyConfirmed,
    bool IsCurrent,
    int Ordinal = 0);

public sealed record EvaBundleImages(IReadOnlyList<EvaBundleImage> RetainedImages);

public sealed record EvaBundle(
    byte[] Content,
    string Sha256,
    byte[] JsonContent,
    string JsonSha256,
    string FileName);

public sealed record EvaHandoffImageOption(
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    int Ordinal = 0);

public sealed record EvaHandoffRevisionSummary(
    int Revision,
    string FileName,
    string BundleSha256,
    string JsonSha256,
    DateTimeOffset GeneratedAtUtc,
    string GeneratedBy,
    bool EstablishedFirstSentToEngineerProxy);

public sealed record EvaHandoffRevisionArtifact(
    int Revision,
    string FileName,
    byte[] Content,
    string BundleSha256)
{
    public const string MediaType = "application/zip";

    public long ContentLength => Content.LongLength;
}

public sealed record EvaHandoffPreparation(
    Guid CaseId,
    long CaseVersion,
    string Reference,
    IReadOnlyList<EvaHandoffImageOption> Images,
    IReadOnlyList<EvaHandoffRevisionSummary> Revisions,
    DateTimeOffset? FirstSentToEngineerAtUtc,
    IReadOnlyList<string> BlockingReasons,
    bool HandOffSwitchedOn = false)
{
    public bool CanGenerate => BlockingReasons.Count == 0;

    /// <summary>
    /// Whether an operator has anything to act on here. With the hand-off
    /// switched off there is nothing to generate and nothing they can do
    /// about it, so the surface says nothing rather than reporting a blocker
    /// against a capability that is not turned on — but any hand-off already
    /// generated keeps its place (PLAT-031).
    /// </summary>
    public bool IsWorthShowing => HandOffSwitchedOn || Revisions.Count > 0;
}

public sealed record GenerateEvaHandoffRequest(
    Guid CaseId,
    long ExpectedCaseVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public enum GenerateEvaHandoffOutcome
{
    Generated,
    Blocked,
    Conflict,
    NotFound
}

public sealed record GenerateEvaHandoffResult(
    GenerateEvaHandoffOutcome Outcome,
    EvaBundle? Bundle,
    IReadOnlyList<string> Reasons,
    int? Revision = null,
    bool FirstSentToEngineerRecorded = false);

public interface IEvaHandoffQueries
{
    Task<EvaHandoffPreparation?> GetPreparationAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);

    Task<EvaHandoffRevisionArtifact?> GetRevisionAsync(
        Guid caseId,
        int revision,
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

public interface IGenerateEvaHandoff
{
    Task<GenerateEvaHandoffResult> ExecuteAsync(
        GenerateEvaHandoffRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEvaHandoffPersistence
{
    Task<GenerateEvaHandoffResult> GenerateAsync(
        GenerateEvaHandoffRequest request,
        string requestHash,
        EvaHandoffPolicyAuthority policy,
        CancellationToken cancellationToken);

    Task<DownloadEvaHandoffResult> DownloadAsync(
        DownloadEvaHandoffRequest request,
        string normalizedReason,
        string requestHash,
        EvaHandoffPolicyAuthority policy,
        CancellationToken cancellationToken);
}

public sealed class GenerateEvaHandoff(IEvaHandoffPersistence persistence) : IGenerateEvaHandoff
{
    public Task<GenerateEvaHandoffResult> ExecuteAsync(
        GenerateEvaHandoffRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedReason = EvaHandoffCommandPolicy.ValidateActorAndCommand(
            request.CaseId,
            request.ExpectedCaseVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken);
        var normalized = request with
        {
            OperationKey = request.OperationKey.Trim(),
            Reason = normalizedReason,
            EditLeaseToken = request.EditLeaseToken.Trim()
        };
        return persistence.GenerateAsync(
            normalized,
            EvaHandoffCommandPolicy.GenerationRequestHash(normalized),
            EvaHandoffPolicyAuthority.Core,
            cancellationToken);
    }
}

public sealed record DownloadEvaHandoffRequest(
    Guid CaseId,
    int Revision,
    long ExpectedCaseVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public enum DownloadEvaHandoffOutcome
{
    Prepared,
    Replay,
    Conflict,
    Refused,
    NotFound
}

public sealed record DownloadEvaHandoffResult(
    DownloadEvaHandoffOutcome Outcome,
    EvaHandoffRevisionArtifact? Artifact,
    string Message);

/// <summary>
/// CASE-019: an operator's own download of a case, as the EVA-format archive.
///
/// Deliberately not a hand-off. It takes no case version, no operation key and
/// no edit lease, because it changes nothing: no revision is recorded, no
/// first-hand-off proxy is written, and the case version does not move. It is
/// a read that answers with a file, like a document download.
/// </summary>
public sealed record ExportCaseBundleRequest(Guid CaseId, ActionActor Actor);

public sealed record ExportCaseBundleResult(
    EvaBundle? Bundle,
    IReadOnlyList<string> UnrecordedFields,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsExported => Bundle is not null;
}

public interface IExportCaseBundle
{
    Task<ExportCaseBundleResult?> ExecuteAsync(
        ExportCaseBundleRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDownloadEvaHandoff
{
    Task<DownloadEvaHandoffResult> ExecuteAsync(
        DownloadEvaHandoffRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DownloadEvaHandoff(IEvaHandoffPersistence persistence) : IDownloadEvaHandoff
{
    public Task<DownloadEvaHandoffResult> ExecuteAsync(
        DownloadEvaHandoffRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Revision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
        var normalized = EvaHandoffCommandPolicy.ValidateActorAndCommand(
            request.CaseId,
            request.ExpectedCaseVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken,
            humanOnly: true);
        var material = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            request.CaseId,
            request.Revision,
            request.ExpectedCaseVersion,
            actorKind = request.Actor.Kind.ToString(),
            request.Actor.SubjectId,
            roles = request.Actor.Roles.OrderBy(value => value).Select(value => value.ToString()).ToArray(),
            operationKey = request.OperationKey.Trim(),
            reason = normalized,
            leaseToken = request.EditLeaseToken.Trim()
        });
        var requestHash = Hash(Encoding.UTF8.GetBytes(material));
        return persistence.DownloadAsync(
            request, normalized, requestHash, EvaHandoffPolicyAuthority.Core, cancellationToken);
    }

    private static string Hash(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}

public static class EvaHandoffCommandPolicy
{
    public static string GenerationRequestHash(GenerateEvaHandoffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "generate-eva-handoff/v1");
        Append(hash, request.CaseId.ToString("D"));
        Append(hash, request.ExpectedCaseVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, request.Actor.Kind.ToString());
        Append(hash, request.Actor.SubjectId);
        foreach (var role in request.Actor.Roles.OrderBy(role => role))
        {
            Append(hash, role.ToString());
        }
        Append(hash, request.Reason);
        Append(hash, Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.EditLeaseToken))).ToLowerInvariant());
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    public static string ValidateActorAndCommand(
        Guid caseId,
        long expectedCaseVersion,
        ActionActor actor,
        string operationKey,
        string reason,
        string editLeaseToken,
        bool humanOnly = false)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (humanOnly && actor.Kind != ActorKind.Staff)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
        if (actor.Kind != ActorKind.Automation)
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        }
        if (caseId == Guid.Empty || expectedCaseVersion < 0)
        {
            throw new ArgumentException("A current Case and rendered workflow version are required.");
        }
        _ = Required(operationKey, 100, nameof(operationKey));
        _ = Required(editLeaseToken, 200, nameof(editLeaseToken));
        return Required(reason, 500, nameof(reason));
    }

    private static string Required(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The value is invalid.", parameterName);
        }
        return normalized;
    }
}

public sealed record EvaHandoffEligibility(
    CaseLifecycleState State,
    bool IsArchived,
    long RenderedWorkflowVersion,
    long AcceptedEvidenceVersion,
    bool CaseCustodyConfirmed,
    bool AuditRequired,
    bool AuditCustodyConfirmed,
    bool MappingAccepted,
    int EligibleImageCount);

public sealed record EvaHandoffImageCandidate(
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    bool CustodyConfirmed,
    bool IsCurrent,
    bool IsLogicallyRemoved,
    bool IsThirdPartyVehicle,
    int Ordinal);

public sealed record EvaHandoffRevisionDecision(
    bool ReuseExisting,
    int BusinessRevision,
    bool RecordFirstProxy);

public enum EvaOperationReplayDecision
{
    New,
    Replay,
    Conflict
}

/// <summary>
/// Capability passed by the Core use cases to persistence. Infrastructure may
/// load state and apply transitions, but cannot manufacture policy authority.
/// </summary>
public sealed class EvaHandoffPolicyAuthority
{
    private readonly Func<EvaHandoffEligibility, IReadOnlyList<string>> evaluate;
    private readonly Func<IEnumerable<EvaHandoffImageCandidate>, IReadOnlyList<EvaHandoffImageCandidate>> selectImages;
    private readonly Func<int?, int, bool, EvaHandoffRevisionDecision> decideRevision;
    private readonly Func<long, long, string?> renderedVersionConflict;

    private EvaHandoffPolicyAuthority()
    {
        evaluate = EvaHandoffPolicy.Evaluate;
        selectImages = EvaHandoffPolicy.SelectEligibleImages;
        decideRevision = EvaHandoffPolicy.DecideRevision;
        renderedVersionConflict = EvaHandoffPolicy.RenderedVersionConflict;
    }

    public static EvaHandoffPolicyAuthority Core { get; } = new();

    public IReadOnlyList<string> Evaluate(EvaHandoffEligibility eligibility) =>
        evaluate(eligibility);

    public IReadOnlyList<EvaHandoffImageCandidate> SelectEligibleImages(
        IEnumerable<EvaHandoffImageCandidate> candidates) =>
        selectImages(candidates);

    public EvaHandoffRevisionDecision DecideRevision(
        int? matchingRevision,
        int currentMaximumRevision,
        bool firstProxyAlreadyRecorded) =>
        decideRevision(matchingRevision, currentMaximumRevision, firstProxyAlreadyRecorded);

    public string? RenderedVersionConflict(long renderedVersion, long currentVersion) =>
        renderedVersionConflict(renderedVersion, currentVersion);

    public EvaOperationReplayDecision DecideReplay(
        bool operationExists,
        bool requestMatches)
    {
        _ = evaluate;
        return operationExists
            ? requestMatches ? EvaOperationReplayDecision.Replay : EvaOperationReplayDecision.Conflict
            : EvaOperationReplayDecision.New;
    }
}

public static class EvaHandoffPolicy
{
    /// <summary>
    /// The one wording for "this case has no photographs to send". The
    /// hand-off and the operator export (CASE-019) both refuse for the same
    /// reason and say so with the same sentence.
    /// </summary>
    public const string NoRetainedImagesReason =
        "At least one stored vehicle image is required.";

    public static IReadOnlyList<EvaHandoffImageCandidate> SelectEligibleImages(
        IEnumerable<EvaHandoffImageCandidate> candidates) => candidates
        .Where(candidate => candidate.SemanticRole == DocumentSemanticRole.Image
            && candidate.CustodyConfirmed
            && candidate.IsCurrent
            && !candidate.IsLogicallyRemoved
            && !candidate.IsThirdPartyVehicle
            && candidate.MediaType is "image/jpeg" or "image/png")
        .OrderBy(candidate => candidate.Ordinal)
        .ToArray();

    public static EvaHandoffRevisionDecision DecideRevision(
        int? matchingRevision,
        int currentMaximumRevision,
        bool firstProxyAlreadyRecorded)
    {
        if (matchingRevision is > 0)
        {
            return new(true, matchingRevision.Value, false);
        }
        return new(
            false,
            checked(currentMaximumRevision + 1),
            !firstProxyAlreadyRecorded);
    }

    public static string? RenderedVersionConflict(long renderedVersion, long currentVersion) =>
        renderedVersion == currentVersion
            ? null
            : "The case changed after the EVA handoff was loaded. Reload before retrying.";

    public static IReadOnlyList<string> Evaluate(EvaHandoffEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(eligibility);
        var reasons = new List<string>();
        if (eligibility.IsArchived)
        {
            reasons.Add("Archived cases cannot generate EVA handoffs.");
        }
        if (eligibility.State != CaseLifecycleState.Review)
        {
            reasons.Add("Available while the case is in Review.");
        }
        if (eligibility.RenderedWorkflowVersion != eligibility.AcceptedEvidenceVersion)
        {
            reasons.Add("Accepted case evidence is stale relative to the current case version.");
        }
        if (!eligibility.CaseCustodyConfirmed)
        {
            reasons.Add("Case custody has not been confirmed.");
        }
        if (eligibility.AuditRequired && !eligibility.AuditCustodyConfirmed)
        {
            reasons.Add("Audit custody has not been confirmed.");
        }
        if (!eligibility.MappingAccepted)
        {
            reasons.Add(CaseEvaMapping.ActivationGateReason);
        }
        if (eligibility.EligibleImageCount <= 0)
        {
            reasons.Add(NoRetainedImagesReason);
        }
        return reasons;
    }
}

public sealed record EvaHandoffProxyRequest(
    Guid CaseId,
    int Revision,
    string BundleSha256,
    ActionActor Actor,
    string OperationKey);

public sealed record EvaHandoffProxyReceipt(
    string AdapterKey,
    string AdapterVersion,
    DateTimeOffset RecordedAtUtc,
    bool ClaimsExternalDelivery,
    bool ClaimsEngineerAssignment);

public interface IEvaHandoffProxy
{
    Task<EvaHandoffProxyReceipt> RecordFirstGenerationAsync(
        EvaHandoffProxyRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Produces replay-identical manual EVA bundles without making an EVA or other network call.
/// The archive is the ordered thirteen-key JSON and Images/, and nothing else.
/// JSON keys, archive entries, image order, timestamps, and hashes are explicit.
/// </summary>
public static class EvaBundleSchema
{
    public const string SchemaVersion = "eva-handoff-v2";
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] FieldOrder =
    [
        "Work Provider",
        "VRM",
        "Vehicle Model",
        "Claimant Name",
        "Reference",
        "Incident Date",
        "Instruction Date",
        "Inspection Date",
        "Inspection Address",
        "Accident Circumstances",
        "VAT Status",
        "Mileage",
        "Mileage Unit"
    ];

    public static EvaBundle CreateOfflineReplay(
        EvaBundleSource source,
        EvaBundleImages images)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(images.RetainedImages);

        var normalizedSource = ValidateSource(source);
        var reference = SafeFileComponent(normalizedSource.Fields.Reference!);
        var jsonName = $"EVA-{reference}.json";
        var json = WriteOrderedJson(normalizedSource.Fields);
        var jsonHash = Hash(json);
        var imageEntries = ValidateAndNameImages(images);
        var archive = WriteArchive(jsonName, json, imageEntries);

        return new(
            archive,
            Hash(archive),
            json,
            jsonHash,
            $"EVA-{reference}.zip");
    }

    private static EvaBundleSource ValidateSource(EvaBundleSource source)
    {
        ArgumentNullException.ThrowIfNull(source.Fields);
        ArgumentNullException.ThrowIfNull(source.Provenance);
        if (!string.Equals(source.MappingKey, CaseEvaMapping.MappingKey, StringComparison.Ordinal)
            || source.MappingVersion != CaseEvaMapping.MappingVersion
            || string.IsNullOrWhiteSpace(source.MappingAcceptanceEvidence))
        {
            throw new InvalidOperationException(
                "The EVA bundle requires an explicitly accepted mapping/config version.");
        }

        // CASE-019: what this method guards is the archive FORMAT — an
        // accepted mapping, the exact ordered field set, provenance that
        // covers it, and values that match that provenance. It used to also
        // assert the hand-off's EVIDENCE BAR: every field non-empty, every
        // status accepted or corrected. That was never the gate it looked
        // like. The hand-off reaches here only through
        // CaseEvaMapping.MapForProduction, which returns a null source unless
        // all thirteen fields already carry accepted, provenanced, non-empty
        // evidence — so these two rules were unreachable duplicates of a bar
        // enforced upstream, and one that an operator export legitimately does
        // not have to clear. The bar stays exactly where it is enforced; the
        // copy of it is gone.
        var normalized = CaseEvaMapping.MapOfflineReplay(source.Fields);
        var values = OrderedFields(normalized).ToArray();
        if (source.Provenance.Count != FieldOrder.Length)
        {
            throw new InvalidDataException("EVA field provenance must cover the exact ordered field set.");
        }

        var provenance = new EvaFieldProvenance[FieldOrder.Length];
        for (var index = 0; index < FieldOrder.Length; index++)
        {
            var item = source.Provenance[index]
                ?? throw new InvalidDataException("An EVA field provenance entry is missing.");
            var field = values[index];
            if (!string.Equals(item.Name, FieldOrder[index], StringComparison.Ordinal)
                || !string.Equals(
                    CaseEvaMapping.MapOfflineReplay(FieldWithValue(item.Name, item.Value))
                        .GetValue(item.Name),
                    field.Value,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(item.Source)
                || string.IsNullOrWhiteSpace(item.SourceVersion))
            {
                throw new InvalidDataException(
                    "EVA field provenance does not match the accepted ordered field values.");
            }

            provenance[index] = item with
            {
                Value = field.Value ?? string.Empty,
                Source = item.Source.Trim(),
                SourceVersion = item.SourceVersion.Trim()
            };
        }

        return new(
            normalized,
            provenance,
            CaseEvaMapping.MappingKey,
            CaseEvaMapping.MappingVersion,
            source.MappingAcceptanceEvidence.Trim());
    }

    private static List<ImageEntry> ValidateAndNameImages(EvaBundleImages images)
    {
        var ids = new HashSet<Guid>();
        var retained = new List<ValidatedImage>(images.RetainedImages.Count);
        foreach (var image in images.RetainedImages)
        {
            var validated = ValidateImage(image);
            if (!ids.Add(validated.Image.OccurrenceId))
            {
                throw new InvalidDataException(
                    "Retained EVA image occurrence identities must be unique.");
            }

            retained.Add(validated);
        }

        if (retained.Count == 0)
        {
            throw new InvalidDataException("At least one retained EVA image is required.");
        }

        if (retained.Select(item => item.Image.Ordinal).Any(value => value <= 0)
            || retained.Select(item => item.Image.Ordinal).Distinct().Count() != retained.Count)
        {
            throw new InvalidDataException("Retained EVA images require distinct persisted evidence ordinals.");
        }

        return retained
            .OrderBy(item => item.Image.Ordinal)
            .Select(CreateImageEntry)
            .ToList();
    }

    private static ValidatedImage ValidateImage(EvaBundleImage? image)
    {
        if (image is null)
        {
            throw new InvalidDataException("A retained EVA image is missing.");
        }
        if (image.OccurrenceId == Guid.Empty
            || image.DocumentId == Guid.Empty
            || image.VersionId == Guid.Empty
            || image.Version <= 0)
        {
            throw new InvalidDataException(
                "Retained EVA images require occurrence, document, and version identities.");
        }
        if (!image.CustodyConfirmed || !image.IsCurrent)
        {
            throw new InvalidOperationException(
                "Every retained EVA image must be the custody-confirmed current document version.");
        }
        if (image.SemanticRole != DocumentSemanticRole.Image
            || !IsSupportedImageMediaType(image.MediaType))
        {
            throw new InvalidDataException("Only retained JPEG or PNG image documents may enter EVA.");
        }
        if (string.IsNullOrWhiteSpace(image.FileName)
            || string.IsNullOrWhiteSpace(image.SourceOccurrenceIdentity)
            || image.Content.IsEmpty)
        {
            throw new InvalidDataException(
                "A retained EVA image is missing content or source provenance.");
        }

        var actualHash = Hash(image.Content.Span);
        if (!string.Equals(actualHash, image.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A retained EVA image failed SHA-256 integrity validation.");
        }

        return new(image, actualHash);
    }

    private static ImageEntry CreateImageEntry(ValidatedImage image) => new(
        $"Images/{image.Image.Ordinal:000} {SafeFileComponent(image.Image.FileName)}",
        image.Image,
        image.Sha256);

    private static byte[] WriteOrderedJson(EvaReplayFields fields)
    {
        using var stream = new MemoryStream();
        // Three explicit choices, all of them parity with what EVA accepts.
        //
        // Indented, two spaces, which is what every known-good sample uses.
        //
        // NewLine pinned rather than left to JsonWriterOptions' default of
        // Environment.NewLine: the archive's SHA-256 is the revision
        // InputFingerprint, so a writer whose bytes depend on the host OS is
        // not the replay-identical bundle this type promises. CRLF is also
        // what all three known-good samples use.
        //
        // UnsafeRelaxedJsonEscaping because the predecessor extractor -- the
        // one whose output EVA actually accepts -- dumps with
        // ensure_ascii=False, so non-ASCII travels as literal UTF-8. The
        // default JavaScriptEncoder would escape it, and & < > + ' besides,
        // as \uXXXX. The name is about HTML/JS embedding: this is a file
        // written to disk and dragged into a desktop application, never
        // interpolated into markup, so that escaping buys nothing here and
        // costs the parity. A claimant name with an accent, or the en-dash
        // QDOS letters demonstrably use, would otherwise diverge.
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = true,
                NewLine = "\r\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }))
        {
            writer.WriteStartObject();
            foreach (var field in OrderedFields(fields))
            {
                // Every key is always present and always a string: an importer
                // reads the same thirteen keys whether or not the case knew
                // the answer. A field the case does not hold is empty, never
                // null and never absent.
                writer.WriteString(field.Name, field.Value ?? string.Empty);
            }
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static byte[] WriteArchive(
        string jsonName,
        byte[] json,
        IReadOnlyList<ImageEntry> images)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            WriteEntry(archive, jsonName, json);
            foreach (var image in images)
            {
                WriteEntry(archive, image.Name, image.Image.Content.Span);
            }
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, ReadOnlySpan<byte> content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = DeterministicTimestamp;
        entry.ExternalAttributes = 0;
        using var entryStream = entry.Open();
        entryStream.Write(content);
    }

    private static IEnumerable<(string Name, string? Value)> OrderedFields(EvaReplayFields fields)
    {
        yield return ("Work Provider", fields.WorkProvider);
        yield return ("VRM", fields.Vrm);
        yield return ("Vehicle Model", fields.VehicleModel);
        yield return ("Claimant Name", fields.ClaimantName);
        yield return ("Reference", fields.Reference);
        yield return ("Incident Date", fields.IncidentDate);
        yield return ("Instruction Date", fields.InstructionDate);
        yield return ("Inspection Date", fields.InspectionDate);
        yield return ("Inspection Address", fields.InspectionAddress);
        yield return ("Accident Circumstances", fields.AccidentCircumstances);
        yield return ("VAT Status", fields.VatStatus);
        yield return ("Mileage", fields.Mileage);
        yield return ("Mileage Unit", fields.MileageUnit);
    }

    private static EvaReplayFields FieldWithValue(string name, string value) => name switch
    {
        "Work Provider" => new(value, null, null, null, null, null, null, null, null, null, null, null, null),
        "VRM" => new(null, value, null, null, null, null, null, null, null, null, null, null, null),
        "Vehicle Model" => new(null, null, value, null, null, null, null, null, null, null, null, null, null),
        "Claimant Name" => new(null, null, null, value, null, null, null, null, null, null, null, null, null),
        "Reference" => new(null, null, null, null, value, null, null, null, null, null, null, null, null),
        "Incident Date" => new(null, null, null, null, null, value, null, null, null, null, null, null, null),
        "Instruction Date" => new(null, null, null, null, null, null, value, null, null, null, null, null, null),
        "Inspection Date" => new(null, null, null, null, null, null, null, value, null, null, null, null, null),
        "Inspection Address" => new(null, null, null, null, null, null, null, null, value, null, null, null, null),
        "Accident Circumstances" => new(null, null, null, null, null, null, null, null, null, value, null, null, null),
        "VAT Status" => new(null, null, null, null, null, null, null, null, null, null, value, null, null),
        "Mileage" => new(null, null, null, null, null, null, null, null, null, null, null, value, null),
        "Mileage Unit" => new(null, null, null, null, null, null, null, null, null, null, null, null, value),
        _ => throw new InvalidDataException($"Unknown EVA field '{name}'.")
    };

    private static string? GetValue(this EvaReplayFields fields, string name) => name switch
    {
        "Work Provider" => fields.WorkProvider,
        "VRM" => fields.Vrm,
        "Vehicle Model" => fields.VehicleModel,
        "Claimant Name" => fields.ClaimantName,
        "Reference" => fields.Reference,
        "Incident Date" => fields.IncidentDate,
        "Instruction Date" => fields.InstructionDate,
        "Inspection Date" => fields.InspectionDate,
        "Inspection Address" => fields.InspectionAddress,
        "Accident Circumstances" => fields.AccidentCircumstances,
        "VAT Status" => fields.VatStatus,
        "Mileage" => fields.Mileage,
        "Mileage Unit" => fields.MileageUnit,
        _ => throw new InvalidDataException($"Unknown EVA field '{name}'.")
    };

    private static bool IsSupportedImageMediaType(string mediaType) =>
        string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase);

    private static string SafeFileComponent(string value)
    {
        var trimmed = value.Trim();
        var separator = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        var fileName = separator < 0 ? trimmed : trimmed[(separator + 1)..];
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(
                char.IsControl(character) || "<>:\"/\\|?*".Contains(character, StringComparison.Ordinal)
                    ? '_'
                    : character);
        }

        var result = builder.ToString().Trim().TrimEnd('.');
        return string.IsNullOrEmpty(result) ? "unnamed" : result;
    }

    private static string Hash(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed record ValidatedImage(EvaBundleImage Image, string Sha256);

    private sealed record ImageEntry(
        string Name,
        EvaBundleImage Image,
        string Sha256);
}
