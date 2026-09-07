using System.Globalization;
using System.Text.Json.Serialization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;

namespace Pegasus.Core.Reports;

public sealed record CaseReportArtifact(
    Guid DocumentId, Guid VersionId, string Sha256, long ContentLength,
    string FileName, string MediaType, string ArtifactKind);
public sealed record CaseReportGeneration(
    Guid Id, Guid CaseId, long CaseVersion, long Version, string InputFingerprint,
    string TemplateVersion, string CalculationPolicyVersion, ActionActor GeneratedBy,
    DateTimeOffset GeneratedAtUtc, IReadOnlyList<CaseReportArtifact> Artifacts);
public sealed record GenerateCaseReportRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, string LeaseToken,
    string OperationKey, CaseReportArtifactKind Kind, string Reason);
public interface IGenerateCaseReport
{
    Task<CaseReportGenerationResult> ExecuteAsync(
        GenerateCaseReportRequest request, CancellationToken cancellationToken);
}
public interface ICaseReportGenerationQueries
{
    Task<CaseReportGeneration?> GetAsync(
        ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken);
}

/// <summary>
/// The two separately addressable artifacts one accepted snapshot produces.
/// Each is generated on its own request; neither is rendered speculatively.
/// </summary>
public enum CaseReportArtifactKind
{
    AssessmentReport,
    FeeNote,
}

/// <summary>
/// One artifact's durable custody state. <c>Pending</c> and <c>Unknown</c>
/// are retained, never silently retried away: a later attempt reuses the same
/// generation, snapshot hash and operation key.
/// </summary>
public enum CaseReportArtifactStatus
{
    Pending,
    Confirmed,
    Failed,
    Unknown,
}

/// <summary>
/// A generation is <c>Pending</c> until every artifact it was asked for is
/// confirmed, <c>Confirmed</c> once they are, and <c>Stale</c> once a material
/// Case fact moved. A stale generation keeps its bytes and its history: it is
/// simply no longer the Case's current one.
/// </summary>
public enum CaseReportGenerationState
{
    Pending,
    Confirmed,
    Stale,
}

/// <summary>
/// The three independent report output choices (v3 § Report). They select
/// what the generated report says; none of them deletes evidence.
/// </summary>
public sealed record CaseReportContentSwitches(
    bool DiscloseGuideSource,
    bool IncludeValuationCommentary,
    bool IncludeUnrelatedDamage)
{
    public static readonly CaseReportContentSwitches None = new(false, false, false);
}

/// <summary>
/// The guide provenance the report's source-aware wording reads. Only the
/// valuation guide decides the accepted Glass's sentence; when no Glass's
/// guide was used the sentence is omitted rather than rewritten (H5 — the
/// approved v3 specification supplies no substitute wording).
/// </summary>
public sealed record ReportGuideSources(IReadOnlyList<ValuationSource> ValuationGuides)
{
    public static readonly ReportGuideSources None = new([]);

    [JsonIgnore]
    public bool UsesGlassesValuationGuide => ValuationGuides.Contains(ValuationSource.Glasses);
}

/// <summary>
/// The actor a generation was frozen by, reduced to the two fields the frozen
/// snapshot needs. Roles are recorded on the action-history row, not here.
/// </summary>
public sealed record CaseReportActor(
    string Kind, string SubjectId, IReadOnlyList<string> Roles)
{
    public static readonly CaseReportActor None = new(string.Empty, string.Empty, []);

    public static CaseReportActor Of(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return new(
            actor.Kind.ToString(),
            actor.SubjectId,
            actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray());
    }

    /// <summary>The frozen actor as the identity model expresses it.</summary>
    public ActionActor ToActor() => Enum.Parse<ActorKind>(Kind) switch
    {
        ActorKind.Staff => ActionActor.Staff(
            Guid.Parse(SubjectId), Roles.Select(Enum.Parse<StaffRole>)),
        ActorKind.SystemWorker => ActionActor.SystemWorker(SubjectId),
        ActorKind.Automation => ActionActor.Automation(SubjectId),
        var kind => throw new InvalidDataException(
            $"A case report generation cannot be attributed to a '{kind}' actor."),
    };
}

/// <summary>
/// One prepared image as the frozen snapshot pins it: the confirmed source
/// identity and hash plus the report role, order, rotation and crop. Bytes
/// are never frozen — they are reopened by exact hash at render time.
/// </summary>
public sealed record CaseReportSnapshotImage(
    Guid OccurrenceId,
    Guid VersionId,
    Guid DocumentId,
    long ContentLength,
    string Sha256,
    string ContentType,
    CaseAssetReportRole Role,
    int? Order,
    CaseAssetRotation Rotation,
    CaseAssetCrop Crop,
    string? BoxFileId,
    string? BoxVersionId);

/// <summary>
/// One accepted source document as the frozen snapshot pins it.
/// </summary>
public sealed record CaseReportSnapshotSource(
    string Name,
    string Version,
    Guid DocumentId,
    Guid VersionId,
    string Sha256,
    string? BoxFileId,
    string? BoxVersionId);

/// <summary>
/// The immutable record of everything a generated report is made of. It is
/// frozen inside one short transaction, hashed canonically, and never
/// rewritten: a later material change marks the generation stale instead.
/// </summary>
/// <remarks>
/// Bytes are deliberately absent. <see cref="Report"/> carries the rendered
/// facts with empty image and signature content; the signature digest and the
/// per-image and per-source hashes pin the exact bytes, which
/// <see cref="ICaseReportContentSource"/> reopens through custody at render
/// time and re-verifies against those hashes.
/// </remarks>
public sealed record CaseReportGenerationSnapshot(
    Guid CaseId,
    long CaseVersion,
    string CaseReference,
    string OperationKey,
    CaseReportActor GeneratedBy,
    DateTimeOffset GeneratedAtUtc,
    Guid SignatoryStaffId,
    string SignatureSha256,
    string SignatureContentType,
    Guid CurrentEstimateId,
    int CurrentEstimateVersion,
    ReportRepairCosts Costs,
    decimal AcceptedEngineerValue,
    Guid AppliedValuationId,
    CaseReportContentSwitches Content,
    ReportGuideSources Guides,
    DateOnly ReportDate,
    bool ReportDateOverridden,
    decimal AgreedFee,
    IReadOnlyList<string> FeeDescriptionLines,
    IReadOnlyList<CaseReportSnapshotSource> Sources,
    IReadOnlyList<CaseReportSnapshotImage> Images,
    string TemplateVersion,
    string RendererVersion,
    AssessmentReportSnapshot Report)
{
    /// <summary>The estimate calculation policy version the money was priced at.</summary>
    [JsonIgnore]
    public string CalculationPolicyVersion =>
        Costs.Totals.CalculationPolicyVersion.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// One artifact row of a generation. Logical identities are retained for a
/// Pending or Unknown custody outcome so a retry after process restart can
/// ask custody what actually happened instead of rendering again blindly.
/// </summary>
public sealed record CaseReportArtifactRecord(
    Guid Id,
    Guid GenerationId,
    CaseReportArtifactKind Kind,
    CaseReportArtifactStatus Status,
    string OperationKey,
    Guid? DocumentId,
    Guid? VersionId,
    string? Sha256,
    long? ContentLength,
    string? FileName,
    string? MediaType,
    string? BoxFileId,
    string? BoxVersionId,
    string? PendingContentStorageKey,
    string? FailureCode);

/// <summary>
/// A generation with its frozen snapshot and every artifact asked of it.
/// </summary>
public sealed record CaseReportGenerationRecord(
    Guid Id,
    Guid CaseId,
    long CaseVersion,
    long Version,
    string SnapshotHash,
    CaseReportGenerationSnapshot Snapshot,
    string TemplateVersion,
    string RendererVersion,
    CaseReportGenerationState State,
    DateTimeOffset GeneratedAtUtc,
    Guid? SupersededById,
    IReadOnlyList<CaseReportArtifactRecord> Artifacts)
{
    [JsonIgnore]
    public bool IsFullyConfirmed =>
        Artifacts.Count > 0
        && Artifacts.All(artifact => artifact.Status == CaseReportArtifactStatus.Confirmed);
}

public enum CaseReportGenerationOutcome
{
    Generated,
    NotReady,
    NotFound,
    Pending,
    Failed,
}

public sealed record CaseReportGenerationResult(
    CaseReportGenerationOutcome Outcome,
    CaseReportGenerationRecord? Generation,
    IReadOnlyList<AssessmentReadinessItem> Reasons);

public enum CaseReportFreezeOutcome
{
    /// <summary>The snapshot was frozen and an artifact row now awaits custody.</summary>
    Frozen,

    /// <summary>An earlier attempt already confirmed this exact artifact.</summary>
    AlreadyConfirmed,

    /// <summary>Persisted readiness refused the generation; nothing was written.</summary>
    NotReady,

    /// <summary>The Case does not exist or the actor cannot see it.</summary>
    NotFound,
}

public sealed record CaseReportFreezeResult(
    CaseReportFreezeOutcome Outcome,
    CaseReportGenerationRecord? Generation,
    Guid? ArtifactId,
    IReadOnlyList<AssessmentReadinessItem> Reasons);

public sealed record FreezeCaseReportGenerationRequest(
    ActionActor Actor,
    Guid CaseId,
    long ExpectedCaseVersion,
    string LeaseToken,
    string OperationKey,
    CaseReportArtifactKind Kind,
    string Reason,
    string TemplateVersion,
    string RendererVersion);

public sealed record ConfirmCaseReportArtifactRequest(
    ActionActor Actor,
    Guid CaseId,
    Guid GenerationId,
    Guid ArtifactId,
    Guid DocumentId,
    Guid VersionId,
    string Sha256,
    long ContentLength,
    string FileName,
    string MediaType,
    string? BoxFileId,
    string? BoxVersionId,
    DateTimeOffset OccurredAtUtc);

public sealed record RecordCaseReportArtifactOutcomeRequest(
    ActionActor Actor,
    Guid CaseId,
    Guid GenerationId,
    Guid ArtifactId,
    CaseReportArtifactStatus Status,
    Guid? DocumentId,
    Guid? VersionId,
    string? BoxFileId,
    string? BoxVersionId,
    string? PendingContentStorageKey,
    string? FailureCode,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// The persistence boundary for report generation. Every write is a short
/// serializable transaction; no browser, Box or HTTP work ever happens inside
/// one (H7 — <c>EvaSubmissionStore</c>'s shape, not
/// <c>EfMarketResearchAiJobCompletionStore</c>'s).
/// </summary>
public interface ICaseReportGenerationStore
{
    /// <summary>
    /// Reloads permission, lease, expected Case version and persisted
    /// readiness, freezes the immutable snapshot, and writes the generation
    /// plus one Pending artifact row for the requested kind. Replays by
    /// operation key. Requesting the second kind of an existing snapshot
    /// reuses that generation and adds its artifact row.
    /// </summary>
    Task<CaseReportFreezeResult> FreezeAsync(
        FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken);

    Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
        ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken);

    Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
        RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken);

    Task<CaseReportGenerationRecord?> GetAsync(
        ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken);

    Task<CaseReportGenerationRecord?> GetCurrentAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the Case's current generation stale. Superseded generations are
    /// never rewritten. Returns the number of generations marked.
    /// </summary>
    Task<int> MarkStaleAsync(Guid caseId, string reasonCode, CancellationToken cancellationToken);
}

/// <summary>
/// Reopens a confirmed generated artifact's immutable bytes. It never
/// regenerates and never returns a Pending, Failed or Unknown artifact.
/// </summary>
public interface IGeneratedCaseArtifactStore
{
    Task<LogicalDocumentContent> OpenAsync(
        ActionActor actor, Guid caseId, Guid generationId, Guid artifactId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Everything one freeze needs, read once from persisted state at the Case
/// version the freeze is guarded against. Image bytes are deliberately absent:
/// freezing pins hashes, and the bytes are opened once at render time.
/// </summary>
public sealed record CaseReportFreezeInputs(
    AssessmentReportProjectionInput Projection,
    CaseReportReadinessInput Readiness,
    string CaseReference,
    long CaseVersion);

/// <summary>
/// The one read model a freeze loads. It is composed from the same accepted
/// queries the Assessment screen and the report preview already use.
/// </summary>
public interface ICaseReportSnapshotSource
{
    Task<CaseReportFreezeInputs?> GetAsync(
        Guid caseId, ActionActor actor, CancellationToken cancellationToken);
}

/// <summary>
/// Rehydrates a frozen snapshot into a renderable
/// <see cref="AssessmentReportSnapshot"/> by reopening the pinned image and
/// signature bytes through custody and re-verifying them against the frozen
/// hashes.
/// </summary>
public interface ICaseReportContentSource
{
    Task<AssessmentReportSnapshot> ComposeAsync(
        CaseReportGenerationSnapshot snapshot,
        ActionActor actor,
        CancellationToken cancellationToken);
}

/// <summary>
/// The reasons a Case's material facts changed under a generation. They are
/// recorded on the stale transition so the operator sees why regeneration is
/// required.
/// </summary>
public static class CaseReportStaleReasons
{
    public const string AssessmentFactsChanged = "assessment_facts_changed";
    public const string EstimateChanged = "estimate_changed";
    public const string ValuationChanged = "valuation_changed";
    public const string ImagePreparationChanged = "image_preparation_changed";
    public const string SignatoryChanged = "signatory_changed";
    public const string ReportContentChanged = "report_content_changed";
    public const string SourceDocumentsChanged = "source_documents_changed";
}

/// <summary>
/// The inputs post-review report readiness is decided from, all reloaded from
/// persisted state. EVA is deliberately absent: a missing optional hand-off
/// never blocks a complete Pegasus report (H3, plan B05).
/// </summary>
public sealed record CaseReportReadinessInput(
    CaseAssessmentProjection Assessment,
    Guid? PersistedSignOffEngineerId,
    Guid? AssignedEngineerId,
    IReadOnlyList<SignOffEngineerProfile> EligibleSignOffEngineers,
    RepairSpecificationVersion? CurrentEstimate,
    AppliedValuation? AppliedValuation,
    IReadOnlyList<CaseAssetPreparation> Preparations,
    IReadOnlyDictionary<Guid, DocumentVersion> ConfirmedImageSources);

public sealed record CaseReportReadinessResult(
    IReadOnlyList<AssessmentReadinessItem> Reasons,
    SignOffEngineerProfile? Signatory,
    IReadOnlyList<PreparedReportImage> Images,
    CaseReportContentSwitches Content,
    DateOnly? RecordedReportDate,
    bool ReportDateOverridden)
{
    public bool IsReady => Reasons.Count == 0;
}

/// <summary>
/// The one owner of "may this Case's report be generated". It reloads only
/// persisted facts, never re-decides Review-entry lifecycle gates, and never
/// asks about EVA. The retired D18 Engineer name/qualification/signature
/// items are gone: the selected sign-off account owns those facts.
/// </summary>
public static class CaseReportReadiness
{
    public const string SignatoryRequirement = "Sign-off Engineer";
    public const string CurrentEstimateRequirement = "Current estimate required";
    public const string LabourRateRequirement = "Current estimate labour rate";
    public const string EngineerValueRequirement = "Accepted Engineer's Value";
    public const string CloseUpImageRequirement = "Close-up image";
    public const string OverviewImageRequirement = "Overview image";
    public const string ImageSourceRequirement = "Report image sources";
    public const string ReportDateRequirement = "Report date";
    public const string ValuationCommentaryRequirement = "Valuation commentary";
    public const string UnrelatedDamageRequirement = "Unrelated damage";

    public static CaseReportReadinessResult Evaluate(CaseReportReadinessInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var assessment = input.Assessment;
        var reasons = new List<AssessmentReadinessItem>(
            AssessmentPolicy.EvaluatePostReviewReadiness(assessment));

        void Require(bool ok, string requirement, string source, string why, string how)
        {
            if (!ok)
            {
                reasons.Add(new(requirement, source, why, how));
            }
        }

        var signatory = CaseSignOffEngineerResolver.Resolve(
            input.PersistedSignOffEngineerId,
            input.AssignedEngineerId,
            input.EligibleSignOffEngineers);
        Require(
            signatory is not null && IsComplete(signatory),
            SignatoryRequirement, "Case sign-off account",
            "The Case has no eligible sign-off Engineer with a complete signature on file.",
            "Select an eligible sign-off Engineer with a signature on file.");

        Require(
            input.CurrentEstimate is not null,
            CurrentEstimateRequirement, "Estimates",
            "No estimate is marked Current on the case (EXT-09).",
            "Use an estimate on the Assessment page.");
        Require(
            input.CurrentEstimate is null || input.CurrentEstimate.Details.HourlyRate > 0m,
            LabourRateRequirement, "Estimates",
            "The Current estimate has no labour rate, and the report prints the hourly rate.",
            "Record the labour rate on the Current estimate.");

        Require(
            input.AppliedValuation is { AcceptedEngineerValue: > 0m },
            EngineerValueRequirement, "Valuation",
            "No accepted Engineer's Value has been applied from a valuation.",
            "Apply a valuation calculation to accept the Engineer's Value.");

        var images = CaseAssetPreparationPolicy.ForReport(input.Preparations);
        Require(
            images.Count(image => image.Role == CaseAssetReportRole.CloseUp) == 1,
            CloseUpImageRequirement, "Case files",
            "The report requires exactly one Close-up image.",
            "Mark one confirmed case image as the Close-up.");
        Require(
            images.Count(image => image.Role == CaseAssetReportRole.Overview) == 1,
            OverviewImageRequirement, "Case files",
            "The report requires exactly one Overview image.",
            "Mark one confirmed case image as the Overview.");
        Require(
            images.All(image => MatchesConfirmedSource(image, input.ConfirmedImageSources)),
            ImageSourceRequirement, "Case files",
            "A selected report image no longer matches its custody-confirmed source version.",
            "Re-select the affected image after its custody version settles.");

        var content = ContentOf(assessment);
        var overridden = Flag(assessment, AssessmentVocabulary.ReportDateOverride);
        var recordedDate = Date(assessment, AssessmentVocabulary.ReportDate);
        Require(
            !overridden || recordedDate is not null,
            ReportDateRequirement, "Report",
            "The report date is overridden but no date is recorded.",
            "Record the report date, or clear the override so generation sets it.");
        Require(
            !content.IncludeValuationCommentary
                || !string.IsNullOrWhiteSpace(input.AppliedValuation?.Reason),
            ValuationCommentaryRequirement, "Valuation",
            "Valuation commentary is selected for the report but the applied valuation records none.",
            "Record the reason on the applied valuation, or turn the choice off.");
        Require(
            !content.IncludeUnrelatedDamage
                || !string.IsNullOrWhiteSpace(Value(assessment, AssessmentVocabulary.DamageUnrelated)),
            UnrelatedDamageRequirement, "Report",
            "Unrelated damage is selected for the report but none is recorded.",
            "Record the unrelated damage, or turn the choice off.");

        return new(reasons, signatory, images, content, recordedDate, overridden);
    }

    /// <summary>
    /// The report content switches as persisted. Absent means off.
    /// </summary>
    public static CaseReportContentSwitches ContentOf(CaseAssessmentProjection assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        return new(
            Flag(assessment, AssessmentVocabulary.ReportDiscloseGuideSource),
            Flag(assessment, AssessmentVocabulary.ReportValuationCommentary),
            Flag(assessment, AssessmentVocabulary.ReportIncludeUnrelatedDamage));
    }

    /// <summary>
    /// The report date a generation freezes: the recorded override when the
    /// operator set one, otherwise the date generation itself is happening on.
    /// A report date is never defaulted before generation.
    /// </summary>
    public static (DateOnly Date, bool Overridden) ResolveReportDate(
        DateOnly? recorded, bool overridden, DateOnly generatedOn) => overridden
            ? (recorded ?? throw new InvalidDataException(
                "The report date is overridden but no date is recorded."), true)
            : (generatedOn, false);

    private static bool IsComplete(SignOffEngineerProfile profile) =>
        !string.IsNullOrWhiteSpace(profile.PrintedName)
        && profile.Signature is { Length: > 0 }
        && ReportImageEvidence.IsAcceptedContentType(profile.SignatureContentType);

    private static bool MatchesConfirmedSource(
        PreparedReportImage image,
        IReadOnlyDictionary<Guid, DocumentVersion> confirmed) =>
        !confirmed.TryGetValue(image.OccurrenceId, out var version)
        || (version.Id == image.VersionId
            && string.Equals(version.Sha256, image.Sha256, StringComparison.Ordinal));

    private static string? Value(CaseAssessmentProjection assessment, string path) =>
        assessment.Field(path)?.Value;

    private static bool Flag(CaseAssessmentProjection assessment, string path) =>
        string.Equals(Value(assessment, path), "true", StringComparison.Ordinal);

    private static DateOnly? Date(CaseAssessmentProjection assessment, string path) =>
        Value(assessment, path) is { } value
            && DateOnly.TryParseExact(
                value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}

/// <summary>
/// Generates exactly one requested artifact from an immutable frozen
/// snapshot. The sequence is fixed: guard and freeze in a short transaction,
/// render and retain outside every transaction, then confirm or record the
/// custody outcome in a second short transaction. A concurrent material Case
/// change between freeze and confirm leaves the generation confirmed but
/// stale — rendering finishing later never makes it current again.
/// </summary>
public sealed class GenerateCaseReport(
    ICaseReportGenerationStore store,
    ICaseReportContentSource contentSource,
    IAssessmentReportRenderer renderer,
    ICaseArtifactCustody custody,
    ICaseArtifactCustodyStatus custodyStatus,
    TimeProvider timeProvider) : IGenerateCaseReport
{
    public async Task<CaseReportGenerationResult> ExecuteAsync(
        GenerateCaseReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);

        var frozen = await store.FreezeAsync(
            new FreezeCaseReportGenerationRequest(
                request.Actor,
                request.CaseId,
                request.ExpectedCaseVersion,
                request.LeaseToken,
                request.OperationKey,
                request.Kind,
                request.Reason,
                AssessmentReportContract.TemplateVersion,
                renderer.EngineVersion),
            cancellationToken).ConfigureAwait(false);

        switch (frozen.Outcome)
        {
            case CaseReportFreezeOutcome.NotFound:
                return new(CaseReportGenerationOutcome.NotFound, null, []);
            case CaseReportFreezeOutcome.NotReady:
                return new(CaseReportGenerationOutcome.NotReady, null, frozen.Reasons);
            case CaseReportFreezeOutcome.AlreadyConfirmed:
                return new(CaseReportGenerationOutcome.Generated, frozen.Generation, []);
        }

        var generation = frozen.Generation
            ?? throw new InvalidOperationException("A frozen generation is required.");
        var artifact = generation.Artifacts.Single(item => item.Id == frozen.ArtifactId);

        // Restart-safe retry: a retained Pending or Unknown artifact already
        // has a logical version, so ask custody what actually happened before
        // rendering the same bytes again. The status read is occurrence-exact
        // (G24) and the artifact record keeps no occurrence id, so the read is
        // by the retain operation key, which is this artifact's recovery
        // identity (G15) and addresses the same object.
        if (artifact is { VersionId: not null, DocumentId: not null }
            && artifact.Status is CaseReportArtifactStatus.Pending or CaseReportArtifactStatus.Unknown)
        {
            var status = await custodyStatus
                .FindByOperationKeyAsync(request.Actor, request.CaseId, artifact.OperationKey, cancellationToken)
                .ConfigureAwait(false);
            if (status is { Disposition: CaseArtifactCustodyDisposition.Confirmed })
            {
                return Result(await ConfirmAsync(request, generation, artifact, status, cancellationToken)
                    .ConfigureAwait(false));
            }
        }

        var report = await contentSource
            .ComposeAsync(generation.Snapshot, request.Actor, cancellationToken)
            .ConfigureAwait(false);
        report.Validate();

        RenderedReportArtifact rendered;
        using (var render = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            render.CancelAfter(AssessmentReportRenderPolicy.RenderTimeout);
            rendered = await new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(report, request.Kind, render.Token)
                .ConfigureAwait(false);
        }

        await using var content = new MemoryStream(rendered.Pdf, writable: false);
        var retained = await custody.RetainAsync(
            new CaseArtifactCustodyRequest(
                request.Actor,
                request.CaseId,
                IntakeReceiptId: null,
                OccurrenceIdentity: OccurrenceIdentityOf(generation.Id, request.Kind),
                OperationKey: artifact.OperationKey,
                FileName: rendered.SuggestedFileName,
                MediaType: "application/pdf",
                ContentLength: rendered.Pdf.LongLength,
                Sha256: rendered.Sha256,
                Content: content),
            cancellationToken).ConfigureAwait(false);

        if (retained.Disposition == CaseArtifactCustodyDisposition.Confirmed)
        {
            return Result(await ConfirmAsync(request, generation, artifact, retained, cancellationToken)
                .ConfigureAwait(false));
        }

        var recorded = await store.RecordArtifactOutcomeAsync(
            new RecordCaseReportArtifactOutcomeRequest(
                request.Actor,
                request.CaseId,
                generation.Id,
                artifact.Id,
                StatusOf(retained.Disposition),
                retained.DocumentId,
                retained.VersionId,
                retained.BoxFileId,
                retained.BoxVersionId,
                retained.PendingContentStorageKey,
                retained.FailureCode,
                timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return new(
            retained.Disposition == CaseArtifactCustodyDisposition.Failed
                ? CaseReportGenerationOutcome.Failed
                : CaseReportGenerationOutcome.Pending,
            recorded,
            []);
    }

    /// <summary>
    /// The custody occurrence identity of one generated artifact. It is
    /// derived from the generation and the kind, so a retry of the same
    /// generation addresses the same object.
    /// </summary>
    public static string OccurrenceIdentityOf(Guid generationId, CaseReportArtifactKind kind) =>
        $"case-report:{generationId:D}:{kind}";

    private Task<CaseReportGenerationRecord> ConfirmAsync(
        GenerateCaseReportRequest request,
        CaseReportGenerationRecord generation,
        CaseReportArtifactRecord artifact,
        CaseArtifactCustodyResult custodyResult,
        CancellationToken cancellationToken) => store.ConfirmArtifactAsync(
            new ConfirmCaseReportArtifactRequest(
                request.Actor,
                request.CaseId,
                generation.Id,
                artifact.Id,
                custodyResult.DocumentId ?? throw new InvalidOperationException(
                    "A confirmed case artifact must carry its logical document identity."),
                custodyResult.VersionId ?? throw new InvalidOperationException(
                    "A confirmed case artifact must carry its logical version identity."),
                custodyResult.Sha256 ?? throw new InvalidOperationException(
                    "A confirmed case artifact must carry its content hash."),
                custodyResult.ContentLength ?? throw new InvalidOperationException(
                    "A confirmed case artifact must carry its content length."),
                FileNameOf(generation, artifact.Kind),
                custodyResult.MediaType ?? "application/pdf",
                custodyResult.BoxFileId,
                custodyResult.BoxVersionId,
                timeProvider.GetUtcNow()),
            cancellationToken);

    private static string FileNameOf(CaseReportGenerationRecord generation, CaseReportArtifactKind kind) =>
        $"{Slug(generation.Snapshot.CaseReference)}_{(kind == CaseReportArtifactKind.FeeNote ? "fee_note" : "assessment")}.pdf";

    private static string Slug(string value) =>
        new(value.ToUpperInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

    private static CaseReportGenerationResult Result(CaseReportGenerationRecord generation) =>
        new(CaseReportGenerationOutcome.Generated, generation, []);

    private static CaseReportArtifactStatus StatusOf(CaseArtifactCustodyDisposition disposition) =>
        disposition switch
        {
            CaseArtifactCustodyDisposition.Pending => CaseReportArtifactStatus.Pending,
            CaseArtifactCustodyDisposition.Failed => CaseReportArtifactStatus.Failed,
            CaseArtifactCustodyDisposition.Unknown => CaseReportArtifactStatus.Unknown,
            _ => throw new ArgumentOutOfRangeException(
                nameof(disposition), disposition, "A confirmed custody disposition is not recorded as an outcome."),
        };
}
