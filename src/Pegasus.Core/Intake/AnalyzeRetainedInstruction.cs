using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Intake;

public enum RetainedInstructionAnalysisOutcome
{
    /// <summary>One profile matched and its candidates were recorded.</summary>
    Analyzed,

    /// <summary>No profile's signature matched the document.</summary>
    NoProfile,

    /// <summary>More than one profile matched; every match is named.</summary>
    Ambiguous,

    /// <summary>The immutable logical source could not be opened or read.</summary>
    SourceUnavailable,

    /// <summary>
    /// The receipt moved under the request, or the operation key was already
    /// used for a different request.
    /// </summary>
    Conflict
}

/// <summary>
/// Analyse one retained, unresolved instruction: read the immutable source,
/// decide which provider's instruction it is FROM THE DOCUMENT, and record what
/// the document says about each field.
///
/// It allocates nothing. No Case, no PO, no principal assignment, no receipt
/// decision and no draft are written — a matching document only PROPOSES a
/// principal, recorded as the ordinary candidate
/// <see cref="SuggestedPrincipalField"/>. Everything staff act on stays a staff
/// decision.
/// </summary>
/// <param name="Actor">
/// A typed server actor: a member of staff with casework rights, or an
/// Automation actor. Never an actor-id string — authorization is Core's, made
/// here through <see cref="StaffAuthorization"/> rather than at a surface.
/// </param>
/// <param name="IntakeAssetId">
/// The asset to analyse. Null means the receipt's own retained source asset,
/// which is the ordinary case; an explicit id analyses one attachment of a
/// multi-part receipt.
/// </param>
public sealed record AnalyzeRetainedInstructionRequest(
    ActionActor Actor,
    Guid ReceiptId,
    long ExpectedReceiptVersion,
    string OperationKey,
    Guid? IntakeAssetId = null,
    CompletedOcrEvidence? OcrEvidence = null);

public sealed record CompletedOcrEvidence(
    string SourceSha256,
    IReadOnlyList<int> QualifiedPages,
    IntakeOcrResult Result);

/// <summary>
/// One field the document states, exactly as recorded: the raw value as
/// printed, the normalized value where the extraction engine canonicalizes one,
/// and the locator that says where it was read. A conflicting field records
/// EVERY competing candidate — the point of the record is to show staff what
/// the document actually says, not to pick for them.
/// </summary>
public sealed record RetainedInstructionCandidate(
    Guid Id,
    string DocumentRole,
    string Field,
    string? PartyRole,
    string? ReferenceRole,
    string? RawValue,
    string? NormalizedValue,
    string? Unit,
    string? Currency,
    string SourceLabel,
    int? Page,
    int Occurrence,
    string ReaderKey,
    string ReaderVersion,
    string PolicyKey,
    string PolicyVersion,
    SourceCandidateDisposition Disposition,
    IntakeSourceLocator? Locator = null);

public sealed record RetainedInstructionAnalysis(
    Guid Id,
    Guid ReceiptId,
    Guid IntakeAssetId,
    string SourceSha256,
    string OperationKey,
    RetainedInstructionAnalysisOutcome Outcome,
    long ExpectedReceiptVersion,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<RetainedInstructionCandidate> Candidates);

public sealed record AnalyzeRetainedInstructionResult(
    RetainedInstructionAnalysisOutcome Outcome,
    RetainedInstructionAnalysis? Analysis,
    string Reason,
    IReadOnlyList<string> MatchingPrincipalCodes,
    bool IsReplay)
{
    public static AnalyzeRetainedInstructionResult From(
        RetainedInstructionAnalysis analysis,
        string reason,
        bool isReplay) =>
        new(analysis.Outcome, analysis, reason, [], isReplay);
}

/// <summary>
/// Persistence for the analysis record and its candidates. One analysis row per
/// (receipt, asset, operation key), unique in the database, so a replay can
/// never write a second set of candidates.
/// </summary>
public interface IRetainedInstructionAnalysisStore
{
    /// <summary>
    /// Writes the analysis and its candidates in one serializable transaction,
    /// after probing for an existing row under the same operation key. A row
    /// found under the key for a DIFFERENT receipt, asset or expected receipt
    /// version is a <see cref="RetainedInstructionAnalysisConflictException"/>;
    /// the same request replays and returns the stored analysis unchanged.
    /// </summary>
    Task<(RetainedInstructionAnalysis Analysis, bool IsReplay)> RecordAsync(
        RetainedInstructionAnalysis analysis,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The stored analysis for an operation key, or null. Read before any
    /// source is opened, so a replay costs no storage read and no extraction.
    /// </summary>
    Task<RetainedInstructionAnalysis?> FindByOperationKeyAsync(
        string operationKey,
        CancellationToken cancellationToken = default);

    /// <summary>The latest analysis of a receipt, with its candidates.</summary>
    Task<RetainedInstructionAnalysis?> FindLatestForReceiptAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default);
}

public sealed class RetainedInstructionAnalysisConflictException()
    : Exception("The analysis operation key was already used for a different request.");

/// <summary>
/// The small read model the Received page renders: the latest analysis of a
/// receipt and the candidates it recorded. Staff-authorized in Core, like every
/// other intake query.
/// </summary>
public sealed record LatestRetainedInstructionAnalysisQuery(Guid ReceiptId, ActionActor Actor);

public interface IGetLatestRetainedInstructionAnalysis
{
    Task<RetainedInstructionAnalysis?> ExecuteAsync(
        LatestRetainedInstructionAnalysisQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class GetLatestRetainedInstructionAnalysis(
    IRetainedInstructionAnalysisStore store) : IGetLatestRetainedInstructionAnalysis
{
    public Task<RetainedInstructionAnalysis?> ExecuteAsync(
        LatestRetainedInstructionAnalysisQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        return query.ReceiptId == Guid.Empty
            ? Task.FromResult<RetainedInstructionAnalysis?>(null)
            : store.FindLatestForReceiptAsync(query.ReceiptId, cancellationToken);
    }
}

/// <summary>
/// The analysis command as its callers need it. Automatic re-analysis after an
/// OCR reading depends on the behaviour, not on the class, so the OCR path can
/// be exercised without standing up a reader, a selector and a store it has no
/// business knowing about.
/// </summary>
public interface IAnalyzeRetainedInstruction
{
    Task<AnalyzeRetainedInstructionResult> ExecuteAsync(
        AnalyzeRetainedInstructionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AnalyzeRetainedInstruction(
    IIntakeReceiptQueries receiptQueries,
    IReadLogicalDocumentVersion documentReader,
    IIntakeSourceReader sourceReader,
    InstructionExtractionPolicySelector selector,
    IRetainedInstructionAnalysisStore store,
    TimeProvider timeProvider,
    VehicleRegistrationCandidateLookup? vehicleRegistrationCandidateLookup = null) : IAnalyzeRetainedInstruction
{
    private static readonly JsonSerializerOptions LocatorJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// The field name a matching document's proposed principal is recorded
    /// under. It is a candidate like any other — deliberately NOT a decision,
    /// an allocation or a draft value.
    /// </summary>
    public const string SuggestedPrincipalField = "Suggested principal code";

    public const string PrincipalPartyRole = "principal";

    public const string VehicleLookupAttemptField = "Vehicle registration lookup attempt";
    public const string VehicleLookupAlternativeField = "Vehicle registration proved alternative";

    /// <summary>
    /// The field name the matched accepted template variant is recorded under,
    /// where the profile has more than one. Separate from the suggested
    /// principal: which template a principal used is not who the principal is.
    /// </summary>
    public const string MatchedTemplateVariantField = "Matched template variant";

    /// <summary>
    /// The policy key recorded when no profile matched, or several did: the
    /// analysis row still exists (so the receipt shows that the question was
    /// asked and answered) but no policy owns it.
    /// </summary>
    public const string NoPolicyKey = "none";

    public async Task<AnalyzeRetainedInstructionResult> ExecuteAsync(
        AnalyzeRetainedInstructionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Core authorization on a typed actor, never an actor-id string.
        // PerformCasework is the right that admits exactly Staff and the
        // Automation Actor (ADR-0011): a request-link, provider or
        // system-worker actor has no business reading a provider's
        // instruction, and each of those fails closed here.
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.ReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt identifier is required.", nameof(request));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedReceiptVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        var operationKey = request.OperationKey.Trim();
        if (operationKey.Length > 100)
        {
            throw new ArgumentException(
                "The operation key must be 100 characters or fewer.",
                nameof(request));
        }

        // Replay first: the same key at the same version returns the stored
        // analysis without opening the source, reading it again, or writing a
        // second set of candidates. A different request under the same key is
        // a conflict, never an overwrite.
        var stored = await store.FindByOperationKeyAsync(operationKey, cancellationToken);
        if (stored is not null)
        {
            return stored.ReceiptId == request.ReceiptId
                && stored.ExpectedReceiptVersion == request.ExpectedReceiptVersion
                && (request.IntakeAssetId is null || stored.IntakeAssetId == request.IntakeAssetId)
                    ? AnalyzeRetainedInstructionResult.From(
                        stored,
                        "The analysis had already been recorded under this operation key.",
                        isReplay: true)
                    : Conflict("The operation key was already used for a different request.");
        }

        if (request.OcrEvidence is not null && vehicleRegistrationCandidateLookup is null)
        {
            return new(
                RetainedInstructionAnalysisOutcome.SourceUnavailable,
                null,
                "OCR registration review is unavailable in this host.",
                [],
                false);
        }

        var receipt = await receiptQueries.GetAsync(request.ReceiptId, cancellationToken);
        if (receipt is null)
        {
            return Conflict("The intake receipt does not exist.");
        }

        if (receipt.Version != request.ExpectedReceiptVersion)
        {
            return Conflict(
                "The receipt changed after it was loaded; re-read it and analyse again.");
        }

        var asset = SelectAsset(receipt, request.IntakeAssetId);
        if (asset is null)
        {
            return new(
                RetainedInstructionAnalysisOutcome.SourceUnavailable,
                null,
                "The receipt has no readable retained source to analyse.",
                [],
                false);
        }

        if (request.OcrEvidence is { } ocrEvidence
            && !IsValidOcrEvidence(request.Actor, asset, ocrEvidence))
        {
            return new(
                RetainedInstructionAnalysisOutcome.SourceUnavailable,
                null,
                "The completed OCR output cannot be attributed to this retained source.",
                [],
                false);
        }

        IntakeSourceReadResult readResult;
        try
        {
            // The immutable logical source, opened by identity with the
            // recorded hash and length as the expectation. No storage key
            // crosses this boundary and the reader refuses a mismatch.
            await using var content = await documentReader.OpenAsync(
                new(
                    request.Actor,
                    DocumentId: null,
                    VersionId: null,
                    IntakeAssetId: asset.Id,
                    CaseId: null,
                    IntakeReceiptId: receipt.Id,
                    asset.ContentHash,
                    asset.ContentLength),
                cancellationToken);

            using var buffer = new MemoryStream();
            await content.Content.CopyToAsync(buffer, cancellationToken);
            readResult = request.OcrEvidence is { } completedOcr
                ? CreateOcrReadResult(completedOcr)
                : await sourceReader.ReadAsync(
                    new(
                        content.FileName,
                        content.MediaType,
                        buffer.ToArray(),
                        receipt.ReceivedAtUtc,
                        ActorLabel(request.Actor),
                        receipt.SourceIdentity),
                    cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // A refusal to open, a hash mismatch or a reader fault all mean the
            // same thing to a member of staff: the retained material could not
            // be read this time. Nothing is recorded, so a later attempt with
            // the same key still analyses rather than replaying a failure.
            return new(
                RetainedInstructionAnalysisOutcome.SourceUnavailable,
                null,
                "The retained source could not be opened or read.",
                [],
                false);
        }

        if (readResult.Status != IntakeSourceReadStatus.Readable)
        {
            return new(
                RetainedInstructionAnalysisOutcome.SourceUnavailable,
                null,
                readResult.FailureReason ?? "The retained source is not readable.",
                [],
                false);
        }

        // A PARTIALLY read source is not a source to extract from, and every
        // one of the fifteen extraction policies says so by throwing
        // ArgumentException on an incomplete result. Selection sits outside the
        // try/catch above, so without this guard a readable-but-partial
        // document - a legacy .doc whose binary parser reports Partial, an
        // e-mail whose attachment could not be processed - left the command as
        // an unhandled exception on the /Received re-evaluation path instead of
        // as an answer.
        //
        // The honest answer is the one an unopenable source already gets: the
        // same typed outcome, carrying the reader's OWN account of what is
        // missing, and nothing recorded - so a later attempt under the same key
        // still analyses rather than replaying a stored failure for ever. The
        // guard belongs here rather than in the policies: a policy refusing
        // material it cannot extract from is right, and the caller deciding
        // what that refusal means to staff is this command's job.
        if (readResult.IsIncomplete)
        {
            return new(
                RetainedInstructionAnalysisOutcome.SourceUnavailable,
                null,
                IncompleteSourceReason(readResult),
                [],
                false);
        }

        // Selection is by document signature AND document role: this use case
        // reads instructions, so a profile written for another document role
        // is not a candidate for it.
        var selection = selector.Select(
            readResult,
            InstructionDocumentSignature.InstructionRole);
        var completedAtUtc = timeProvider.GetUtcNow();
        if (selection.Outcome != InstructionPolicySelectionOutcome.Selected)
        {
            var matches = selection.Matches.Select(policy => policy.PrincipalCode).ToArray();
            var outcome = selection.Outcome == InstructionPolicySelectionOutcome.Ambiguous
                ? RetainedInstructionAnalysisOutcome.Ambiguous
                : RetainedInstructionAnalysisOutcome.NoProfile;

            // A row without candidates: the question was asked and the honest
            // answer recorded, so the page can say so and a re-evaluation under
            // a new key is distinguishable from never having run.
            var (empty, emptyReplay) = await store.RecordAsync(
                new(
                    Guid.NewGuid(),
                    receipt.Id,
                    asset.Id,
                    asset.ContentHash,
                    operationKey,
                    outcome,
                    request.ExpectedReceiptVersion,
                    completedAtUtc,
                    []),
                cancellationToken);
            return new(
                outcome,
                empty,
                outcome == RetainedInstructionAnalysisOutcome.Ambiguous
                    ? $"More than one document profile matched: {string.Join(", ", matches)}."
                    : "No document profile matched this instruction.",
                matches,
                emptyReplay);
        }

        var policy = selection.Policy!;
        var profile = (IInstructionDocumentProfile)policy;
        var extraction = policy.Extract(
            readResult,
            completedAtUtc,
            // The principal is recorded as PROPOSED BY THE DOCUMENT: the policy
            // key and version carried here are the selector's document-profile
            // identity, not a mail route's, so nothing downstream can mistake
            // this for a route-established principal.
            new(policy.PrincipalCode, profile.DocumentProfileKey, profile.DocumentProfileVersion));

        var candidates = BuildCandidates(
            extraction,
            profile,
            policy.PrincipalCode,
            readResult.ReaderKey,
            readResult.ReaderVersion,
            selection.MatchedVariantKeys,
            policy as IInstructionFieldRoles,
            request.OcrEvidence is not null).ToList();
        if (request.OcrEvidence is { } lookupOcr)
        {
            var registrations = extraction.Fields.Where(field =>
                string.Equals(field.Name, "Vehicle registration", StringComparison.Ordinal)
                && !field.HasConflict
                && field.Candidates.Count == 1).ToArray();
            if (registrations.Length == 1)
            {
                var registration = registrations[0];
                var raw = registration.Candidates[0];
                var sourceReference = $"ocr:{lookupOcr.SourceSha256}:response:{lookupOcr.Result.ResponseSha256}:page:{raw.Locator?.Page}";
                var lookup = await vehicleRegistrationCandidateLookup!.LookupAsync(
                    new(raw.SourceValue, MachineReadRegistrationSource.DocumentOcr, sourceReference),
                    cancellationToken);
                candidates.AddRange(BuildVehicleLookupCandidates(
                    lookup,
                    profile,
                    readResult.ReaderKey,
                    readResult.ReaderVersion));
            }
        }

        var (analysis, isReplay) = await store.RecordAsync(
            new(
                Guid.NewGuid(),
                receipt.Id,
                asset.Id,
                asset.ContentHash,
                operationKey,
                RetainedInstructionAnalysisOutcome.Analyzed,
                request.ExpectedReceiptVersion,
                completedAtUtc,
                candidates),
            cancellationToken);
        return AnalyzeRetainedInstructionResult.From(
            analysis,
            $"The document matched the {policy.PrincipalCode} instruction profile.",
            isReplay);
    }

    /// <summary>
    /// Maps the extraction's review fields onto recorded candidates. A field
    /// with one candidate is Usable; a field with none is Missing and still
    /// recorded, because "the document does not state this" is a finding staff
    /// need; a conflicting field records EVERY candidate as Conflicting, so
    /// nothing is silently dropped. Occurrence orders the candidates of one
    /// field as they were read.
    /// </summary>
    private static RetainedInstructionCandidate[] BuildCandidates(
        InstructionExtractionResult extraction,
        IInstructionDocumentProfile profile,
        string principalCode,
        string readerKey,
        string readerVersion,
        IReadOnlyList<string> matchedVariantKeys,
        IInstructionFieldRoles? fieldRoles,
        bool forceReviewOnly = false)
    {
        var policyVersion = profile.DocumentProfileVersion.ToString(CultureInfo.InvariantCulture);
        var documentRole = profile.Signature.DocumentRole;
        var candidates = new List<RetainedInstructionCandidate>
        {
            // The document proposes the principal. It is a candidate, never an
            // allocation: nothing reads it as an assignment.
            new(
                Guid.NewGuid(),
                documentRole,
                SuggestedPrincipalField,
                PrincipalPartyRole,
                null,
                principalCode,
                principalCode,
                null,
                null,
                $"{profile.DocumentProfileKey} document signature",
                null,
                0,
                readerKey,
                readerVersion,
                profile.DocumentProfileKey,
                policyVersion,
                forceReviewOnly ? SourceCandidateDisposition.Ambiguous : SourceCandidateDisposition.Usable)
        };

        // Which accepted template of the profile the document matched. One is
        // a reading; two are recorded as ambiguous rather than resolved,
        // because the principal is settled and the template is not.
        var variantDisposition = forceReviewOnly || matchedVariantKeys.Count > 1
            ? SourceCandidateDisposition.Ambiguous
            : SourceCandidateDisposition.Usable;
        var variantOccurrence = 0;
        foreach (var variantKey in matchedVariantKeys)
        {
            candidates.Add(new(
                Guid.NewGuid(),
                documentRole,
                MatchedTemplateVariantField,
                null,
                null,
                variantKey,
                variantKey,
                null,
                null,
                $"{profile.DocumentProfileKey} template variant",
                null,
                variantOccurrence++,
                readerKey,
                readerVersion,
                profile.DocumentProfileKey,
                policyVersion,
                variantDisposition));
        }

        foreach (var field in extraction.Fields)
        {
            if (field.Candidates.Count == 0)
            {
                var missingRole = Role(fieldRoles, field.Name);
                candidates.Add(new(
                    Guid.NewGuid(),
                    documentRole,
                    field.Name,
                    missingRole.PartyRole,
                    missingRole.ReferenceRole,
                    null,
                    field.SuggestedValue,
                    null,
                    null,
                    "not stated",
                    null,
                    0,
                    readerKey,
                    readerVersion,
                    profile.DocumentProfileKey,
                    policyVersion,
                    SourceCandidateDisposition.Missing));
                continue;
            }

            // Two readings the document itself supports are AMBIGUOUS, not
            // conflicting: nothing has contradicted a confirmed fact, the
            // document simply says two things and neither may be picked here.
            // Conflicting stays reserved for a candidate that contradicts a
            // fact staff or an Engineer already confirmed.
            var disposition = forceReviewOnly || field.HasConflict
                ? SourceCandidateDisposition.Ambiguous
                : SourceCandidateDisposition.Usable;
            var occurrence = 0;
            var role = Role(fieldRoles, field.Name);
            foreach (var candidate in field.Candidates)
            {
                candidates.Add(new(
                    Guid.NewGuid(),
                    documentRole,
                    field.Name,
                    role.PartyRole,
                    role.ReferenceRole,
                    candidate.Value,
                    // The engine canonicalizes only the field it accepted; a
                    // competing candidate of a conflicting field has no
                    // canonical form to record, and inventing one would make
                    // the conflict look resolved.
                    field.HasConflict ? null : field.SuggestedValue,
                    null,
                    null,
                    candidate.SourceLabel,
                    // The reader's own locator states the page; the source
                    // label is parsed only for a fragment that carries none.
                    candidate.Locator?.Page ?? PageFrom(candidate.SourceLabel),
                    occurrence++,
                    readerKey,
                    readerVersion,
                    profile.DocumentProfileKey,
                    policyVersion,
                    disposition,
                    candidate.Locator));
            }
        }

        return candidates.ToArray();
    }

    private static IEnumerable<RetainedInstructionCandidate> BuildVehicleLookupCandidates(
        VehicleRegistrationCandidateLookupResult lookup,
        IInstructionDocumentProfile profile,
        string readerKey,
        string readerVersion)
    {
        var policyVersion = profile.DocumentProfileVersion.ToString(CultureInfo.InvariantCulture);
        foreach (var attempt in lookup.Attempts.OrderBy(attempt => attempt.Order))
        {
            yield return new(
                Guid.NewGuid(),
                profile.Signature.DocumentRole,
                VehicleLookupAttemptField,
                "claimant",
                null,
                attempt.Registration,
                attempt.Result.Outcome.ToString(),
                null,
                null,
                JsonSerializer.Serialize(new
                {
                    lookup.Reading.RawValue,
                    lookup.Reading.SourceReference,
                    attempt.Order,
                    Result = attempt.Result
                }, LocatorJsonOptions),
                null,
                attempt.Order,
                readerKey,
                readerVersion,
                profile.DocumentProfileKey,
                policyVersion,
                SourceCandidateDisposition.Ambiguous);
        }

        if (lookup.AcceptedRegistration is { } accepted
            && (lookup.Candidates.Count == 0
                || !string.Equals(accepted, lookup.Candidates[0], StringComparison.Ordinal)))
        {
            yield return new(
                Guid.NewGuid(),
                profile.Signature.DocumentRole,
                VehicleLookupAlternativeField,
                "claimant",
                null,
                lookup.Reading.RawValue,
                accepted,
                null,
                null,
                lookup.Reading.SourceReference,
                null,
                0,
                readerKey,
                readerVersion,
                profile.DocumentProfileKey,
                policyVersion,
                SourceCandidateDisposition.Ambiguous);
        }
    }

    /// <summary>
    /// The role the reading policy declares for one of its own fields. A
    /// policy that declares none leaves both roles unstated, which is the
    /// truth about a candidate whose owner nobody has said.
    /// </summary>
    private static InstructionFieldRole Role(IInstructionFieldRoles? fieldRoles, string field) =>
        fieldRoles is not null && fieldRoles.FieldRoles.TryGetValue(field, out var role)
            ? role
            : new(null, null);

    /// <summary>
    /// The reader records a page only inside the fragment's own source label,
    /// as the trailing <c>", page n"</c>. Parsed here rather than guessed at,
    /// and left null when the label carries no page (an e-mail body, a DOCX,
    /// a synthesized fragment).
    /// </summary>
    public static int? PageFrom(string sourceLabel)
    {
        ArgumentNullException.ThrowIfNull(sourceLabel);
        const string marker = ", page ";
        var index = sourceLabel.LastIndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var tail = sourceLabel[(index + marker.Length)..];
        return int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var page)
            ? page
            : null;
    }

    /// <summary>
    /// The locator recorded with each candidate. One JSON shape, written here
    /// once, so persistence never invents a second.
    ///
    /// Version 2 carries the structured locator the reader produced — page,
    /// table cell, PDF form field, bounded region, message part and occurrence —
    /// beside the source label version 1 recorded. A version 1 envelope still
    /// reads: its structured half is simply absent, which is the truth about a
    /// candidate recorded before the reader reported structure.
    /// </summary>
    public static string LocatorJson(string sourceLabel, int? page, IntakeSourceLocator? locator = null) =>
        JsonSerializer.Serialize(new LocatorEnvelope(
            locator?.Sha256 is not null || locator?.DocumentRole is not null ? 3 : locator is null ? 1 : 2,
            sourceLabel,
            page ?? locator?.Page,
            locator?.Kind,
            locator?.Table,
            locator?.Row,
            locator?.Column,
            locator?.FormField,
            locator?.Region,
            locator?.MessagePart,
            locator?.Occurrence,
            locator?.Sha256,
            locator?.DocumentRole), LocatorJsonOptions);

    public static (string SourceLabel, int? Page, IntakeSourceLocator? Locator) ReadLocator(string locatorJson)
    {
        var envelope = JsonSerializer.Deserialize<LocatorEnvelope>(locatorJson, LocatorJsonOptions)
            ?? throw new InvalidDataException("The source locator envelope is unreadable.");
        if (envelope.Version is not (1 or 2 or 3))
        {
            throw new InvalidDataException("The source locator envelope version is unsupported.");
        }

        return (envelope.SourceLabel, envelope.Page, ReadLocator(envelope));
    }

    /// <summary>
    /// The stored envelope back as the reader's own locator, or null when the
    /// row carries none. Nothing is invented: a version 1 envelope that recorded
    /// only a page comes back as a page locator, and one that recorded nothing
    /// comes back null.
    /// </summary>
    private static IntakeSourceLocator? ReadLocator(LocatorEnvelope envelope) =>
        envelope.Kind is not { } kind
            ? envelope.Page is { } onlyPage ? IntakeSourceLocator.ForPage(onlyPage) : null
            : new(
                kind,
                envelope.Page,
                envelope.Table,
                envelope.Row,
                envelope.Column,
                envelope.FormField,
                envelope.Region,
                envelope.MessagePart ?? IntakeMessagePart.None,
                envelope.Occurrence ?? 0,
                envelope.Sha256,
                envelope.DocumentRole);

    public sealed record LocatorEnvelope(
        int Version,
        string SourceLabel,
        int? Page,
        IntakeLocatorKind? Kind = null,
        int? Table = null,
        int? Row = null,
        int? Column = null,
        string? FormField = null,
        string? Region = null,
        IntakeMessagePart? MessagePart = null,
        int? Occurrence = null,
        string? Sha256 = null,
        string? DocumentRole = null);

    private static bool IsValidOcrEvidence(
        ActionActor actor,
        IntakeAssetRecord asset,
        CompletedOcrEvidence evidence)
    {
        var result = evidence.Result;
        var qualified = evidence.QualifiedPages;
        return actor.Kind == ActorKind.Automation
            && string.Equals(actor.SubjectId, ReconcileUnidentifiedDestinations.AutomationActorId, StringComparison.Ordinal)
            && IsSha256(evidence.SourceSha256)
            && string.Equals(evidence.SourceSha256, asset.ContentHash, StringComparison.OrdinalIgnoreCase)
            && result.State == IntakeOcrState.Completed
            && IsSha256(result.ResponseSha256)
            && string.Equals(result.Provider, IntakeOcrProviderIdentity.Provider, StringComparison.Ordinal)
            && string.Equals(result.ModelId, IntakeOcrProviderIdentity.ModelId, StringComparison.Ordinal)
            && string.Equals(result.ApiVersion, IntakeOcrProviderIdentity.ApiVersion, StringComparison.Ordinal)
            && qualified.Count > 0
            && qualified.All(page => page > 0)
            && qualified.Distinct().Count() == qualified.Count
            && result.PageResults.Select(page => page.Number).Order().SequenceEqual(qualified.Order())
            && result.PageResults.All(page => !string.IsNullOrWhiteSpace(page.Text));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => char.IsAsciiHexDigit(character));

    internal static IntakeSourceReadResult CreateOcrReadResult(CompletedOcrEvidence evidence)
    {
        var responseSha = evidence.Result.ResponseSha256!;
        var content = evidence.Result.PageResults
            .OrderBy(page => page.Number)
            .Select(page => new IntakeContentFragment(
                IntakeEvidenceSource.DocumentContent,
                $"OCR page {page.Number}; response {responseSha}",
                page.Text,
                new(
                    IntakeLocatorKind.Page,
                    Page: page.Number,
                    Region: JsonSerializer.Serialize(
                        new OcrPageProvenance(page.Lines, page.Tables, responseSha),
                        LocatorJsonOptions),
                    Sha256: evidence.SourceSha256,
                    DocumentRole: "ocr")))
            .ToArray();
        return new(
            IntakeSourceReadStatus.Readable,
            content,
            [],
            [],
            false,
            ReaderKey: $"{evidence.Result.Provider}/{evidence.Result.ModelId}",
            ReaderVersion: evidence.Result.ApiVersion);
    }

    private sealed record OcrPageProvenance(
        IReadOnlyList<IntakeOcrLine> Lines,
        IReadOnlyList<IntakeOcrTable> Tables,
        string ResponseSha256);

    /// <summary>
    /// The receipt's own retained source asset by default; an explicit id picks
    /// one of its other retained parts. Never a storage key and never an asset
    /// belonging to another receipt — the receipt scopes the lookup.
    ///
    /// Which asset IS the source is <see cref="IntakeFileIdentity.SourceAsset"/>'s
    /// rule, not a second copy of it here: the page decides whether to offer the
    /// action from the same owner, so the two cannot disagree about whether
    /// analysis is possible.
    /// </summary>
    /// <summary>
    /// Why a readable source was not read completely, in the reader's words.
    /// The reader records an issue for every gap it knows about; repeating them
    /// is what lets staff see that a legacy Word original lost its embedded
    /// objects rather than being told only that "something" was missing.
    /// </summary>
    private static string IncompleteSourceReason(IntakeSourceReadResult readResult) =>
        readResult.Issues.Count == 0
            ? "The retained source could not be read completely."
            : "The retained source could not be read completely: "
                + string.Join("; ", readResult.Issues.Select(issue => issue.Reason));

    private static IntakeAssetRecord? SelectAsset(IntakeReceipt receipt, Guid? assetId) =>
        assetId is { } explicitId
            ? receipt.AssetRecords.SingleOrDefault(asset => asset.Id == explicitId)
            : IntakeFileIdentity.SourceAsset(receipt);

    private static string ActorLabel(ActionActor actor) =>
        $"{actor.Kind.ToString().ToLowerInvariant()}:{actor.SubjectId}";

    private static AnalyzeRetainedInstructionResult Conflict(string reason) =>
        new(RetainedInstructionAnalysisOutcome.Conflict, null, reason, [], false);
}
