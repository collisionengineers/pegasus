using System.Security.Cryptography;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// Post-review report readiness and the generation sequence. Nothing here
/// touches a database, a browser, Box or Graph: the custody, custody-status,
/// renderer and store boundaries are all faked.
/// </summary>
public sealed class CaseReportGenerationTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid CaseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SignatoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CloseUpOccurrence = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OverviewOccurrence = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.Parse("55555555-5555-5555-5555-555555555555"), [StaffRole.Engineer]);

    [Fact]
    public void ACompleteCaseIsReady()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput());

        Assert.True(result.IsReady);
        Assert.Empty(result.Reasons);
        Assert.Equal(SignatoryId, result.Signatory!.StaffId);
        Assert.Equal(
            [CaseAssetReportRole.CloseUp, CaseAssetReportRole.Overview],
            result.Images.Select(image => image.Role));
    }

    [Fact]
    public void EvaIsNeverAReadinessItem()
    {
        // H3/B-F-05: a missing optional EVA hand-off never blocks a complete
        // Pegasus report, so no readiness input names it at all.
        var input = ReadyInput();

        Assert.DoesNotContain(
            typeof(CaseReportReadinessInput).GetProperties(),
            property => property.Name.Contains("Eva", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            CaseReportReadiness.Evaluate(input with { CurrentEstimate = null }).Reasons,
            reason => reason.Requirement.Contains("EVA", StringComparison.OrdinalIgnoreCase)
                || reason.Source.Contains("EVA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheRetiredD18EngineerItemsAreGone()
    {
        var input = ReadyInput();
        var withoutD18 = input.Assessment.Fields
            .Where(field => field.Path is not (AssessmentVocabulary.EngineerName
                or AssessmentVocabulary.EngineerQualifications
                or AssessmentVocabulary.EngineerSignature))
            .ToArray();

        var result = CaseReportReadiness.Evaluate(
            input with { Assessment = input.Assessment with { Fields = withoutD18 } });

        Assert.True(result.IsReady);
    }

    [Fact]
    public void AMissingSignOffEngineerBlocksGeneration()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            PersistedSignOffEngineerId = null,
            AssignedEngineerId = null,
            EligibleSignOffEngineers = [],
        });

        AssertBlocked(result, CaseReportReadiness.SignatoryRequirement);
        Assert.Null(result.Signatory);
    }

    [Theory]
    [InlineData("", true, "image/png")]
    [InlineData("Ed Mawdsley", false, "image/png")]
    [InlineData("Ed Mawdsley", true, "image/gif")]
    public void AnIncompleteSignOffEngineerBlocksGeneration(
        string printedName, bool hasSignature, string contentType)
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            EligibleSignOffEngineers =
            [
                new SignOffEngineerProfile(
                    SignatoryId, printedName, "ATA VDA AQP",
                    hasSignature ? [1, 2, 3] : [], contentType, IsDefault: true),
            ],
        });

        AssertBlocked(result, CaseReportReadiness.SignatoryRequirement);
    }

    [Fact]
    public void AnIneligibleSignOffEngineerIsNotResolved()
    {
        // The persisted id is not on the eligible list, and there is no
        // assigned Engineer and no default: nothing resolves.
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            PersistedSignOffEngineerId = Guid.NewGuid(),
            AssignedEngineerId = null,
            EligibleSignOffEngineers = [Profile() with { IsDefault = false }],
        });

        AssertBlocked(result, CaseReportReadiness.SignatoryRequirement);
    }

    [Fact]
    public void AMissingCurrentEstimateBlocksGeneration()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with { CurrentEstimate = null });

        var reason = AssertBlocked(result, CaseReportReadiness.CurrentEstimateRequirement);
        Assert.Contains("EXT-09", reason.WhyOutstanding, StringComparison.Ordinal);
    }

    [Fact]
    public void ACurrentEstimateWithoutARateBlocksGeneration()
    {
        var estimate = Estimate();
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            CurrentEstimate = estimate with
            {
                Details = estimate.Details with { LabourRate = null, Rate = null },
            },
        });

        AssertBlocked(result, CaseReportReadiness.LabourRateRequirement);
    }

    [Fact]
    public void AMissingAcceptedEngineerValueBlocksGeneration()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with { AppliedValuation = null });

        AssertBlocked(result, CaseReportReadiness.EngineerValueRequirement);
    }

    [Fact]
    public void AMissingCloseUpBlocksGeneration()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            Preparations = [Preparation(OverviewOccurrence, CaseAssetReportRole.Overview)],
        });

        AssertBlocked(result, CaseReportReadiness.CloseUpImageRequirement);
    }

    [Fact]
    public void AMissingOverviewBlocksGeneration()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            Preparations = [Preparation(CloseUpOccurrence, CaseAssetReportRole.CloseUp)],
        });

        AssertBlocked(result, CaseReportReadiness.OverviewImageRequirement);
    }

    [Fact]
    public void AnImageWhoseConfirmedSourceMovedBlocksGeneration()
    {
        var input = ReadyInput();
        var moved = input.ConfirmedImageSources.ToDictionary(
            entry => entry.Key,
            entry => entry.Key == CloseUpOccurrence
                ? entry.Value with { Sha256 = new string('b', 64) }
                : entry.Value);

        var result = CaseReportReadiness.Evaluate(input with { ConfirmedImageSources = moved });

        AssertBlocked(result, CaseReportReadiness.ImageSourceRequirement);
    }

    [Fact]
    public void AnOverriddenReportDateWithoutADateBlocksGeneration()
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Append(Field(AssessmentVocabulary.ReportDateOverride, "true"))
            .ToArray();

        var result = CaseReportReadiness.Evaluate(
            input with { Assessment = input.Assessment with { Fields = fields } });

        AssertBlocked(result, CaseReportReadiness.ReportDateRequirement);
    }

    [Fact]
    public void ValuationCommentaryWithoutCommentaryBlocksGeneration()
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Append(Field(AssessmentVocabulary.ReportValuationCommentary, "true"))
            .ToArray();

        var result = CaseReportReadiness.Evaluate(input with
        {
            Assessment = input.Assessment with { Fields = fields },
            AppliedValuation = Valuation() with { Reason = "  " },
        });

        AssertBlocked(result, CaseReportReadiness.ValuationCommentaryRequirement);
    }

    [Fact]
    public void UnrelatedDamageWithoutUnrelatedDamageBlocksGeneration()
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Where(field => field.Path != AssessmentVocabulary.DamageUnrelated)
            .Append(Field(AssessmentVocabulary.ReportIncludeUnrelatedDamage, "true"))
            .ToArray();

        var result = CaseReportReadiness.Evaluate(
            input with { Assessment = input.Assessment with { Fields = fields } });

        AssertBlocked(result, CaseReportReadiness.UnrelatedDamageRequirement);
    }

    [Fact]
    public void AReportDateDefaultsOnlyAtGenerationAndAnOverrideIsFrozen()
    {
        var generatedOn = new DateOnly(2026, 9, 6);

        Assert.Equal(
            (generatedOn, false),
            CaseReportReadiness.ResolveReportDate(null, overridden: false, generatedOn));
        Assert.Equal(
            (generatedOn, false),
            CaseReportReadiness.ResolveReportDate(
                new DateOnly(2026, 7, 4), overridden: false, generatedOn));
        Assert.Equal(
            (new DateOnly(2026, 7, 4), true),
            CaseReportReadiness.ResolveReportDate(
                new DateOnly(2026, 7, 4), overridden: true, generatedOn));
        Assert.Throws<InvalidDataException>(() =>
            CaseReportReadiness.ResolveReportDate(null, overridden: true, generatedOn));
    }

    [Fact]
    public async Task NotReadyRefusesBeforeAnyRenderOrCustodyCall()
    {
        var store = new FakeStore
        {
            Freeze = new(CaseReportFreezeOutcome.NotReady, null, null,
                [new AssessmentReadinessItem("Close-up image", "Case files", "why", "how")]),
        };
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();

        var result = await Use(store, renderer, custody).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.NotReady, result.Outcome);
        Assert.Equal("Close-up image", Assert.Single(result.Reasons).Requirement);
        Assert.Empty(renderer.Kinds);
        Assert.Equal(0, custody.Calls);
    }

    [Fact]
    public async Task OnlyTheRequestedKindIsRenderedAndRetained()
    {
        var store = new FakeStore();
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();

        var result = await Use(store, renderer, custody)
            .ExecuteAsync(Request(CaseReportArtifactKind.FeeNote), default);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        Assert.Equal([CaseReportArtifactKind.FeeNote], renderer.Kinds);
        Assert.Equal(1, custody.Calls);
        Assert.Equal(
            GenerateCaseReport.OccurrenceIdentityOf(store.GenerationId, CaseReportArtifactKind.FeeNote),
            custody.LastRequest!.OccurrenceIdentity);
        Assert.Equal("operation-1", custody.LastRequest.OperationKey);
        Assert.Single(store.Confirmations);
        Assert.Empty(store.Outcomes);
    }

    [Fact]
    public async Task RenderingAndRetentionHappenAfterTheFreezeTransactionCommits()
    {
        var store = new FakeStore();
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();

        await Use(store, renderer, custody).ExecuteAsync(Request(), default);

        Assert.Equal(["freeze", "render", "retain", "confirm"], store.Sequence);
    }

    [Fact]
    public async Task APendingCustodyOutcomeIsRetainedWithItsLogicalIdentities()
    {
        var store = new FakeStore();
        var custody = new RecordingCustody
        {
            Result = new CaseArtifactCustodyResult(
                CaseArtifactCustodyDisposition.Pending, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "box-file", "box-version", null, null, null, null, "pending-key"),
        };

        var result = await Use(store, new RecordingRenderer(), custody).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.Pending, result.Outcome);
        var recorded = Assert.Single(store.Outcomes);
        Assert.Equal(CaseReportArtifactStatus.Pending, recorded.Status);
        Assert.Equal(custody.Result.DocumentId, recorded.DocumentId);
        Assert.Equal(custody.Result.VersionId, recorded.VersionId);
        Assert.Equal("box-file", recorded.BoxFileId);
        Assert.Equal("box-version", recorded.BoxVersionId);
        Assert.Equal("pending-key", recorded.PendingContentStorageKey);
        Assert.Empty(store.Confirmations);
    }

    [Fact]
    public async Task AnUnknownCustodyOutcomeIsRetainedAndNeverTreatedAsSuccess()
    {
        var store = new FakeStore();
        var custody = new RecordingCustody
        {
            Result = new CaseArtifactCustodyResult(
                CaseArtifactCustodyDisposition.Unknown, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                null, null, null, null, null, null, null),
        };

        var result = await Use(store, new RecordingRenderer(), custody).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.Pending, result.Outcome);
        Assert.Equal(CaseReportArtifactStatus.Unknown, Assert.Single(store.Outcomes).Status);
    }

    [Fact]
    public async Task AFailedCustodyOutcomeIsReportedAsFailed()
    {
        var store = new FakeStore();
        var custody = new RecordingCustody
        {
            Result = new CaseArtifactCustodyResult(
                CaseArtifactCustodyDisposition.Failed, null, null, null,
                null, null, null, null, null, "storage_unavailable", null),
        };

        var result = await Use(store, new RecordingRenderer(), custody).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.Failed, result.Outcome);
        Assert.Equal("storage_unavailable", Assert.Single(store.Outcomes).FailureCode);
    }

    [Fact]
    public async Task ARetainedPendingArtifactIsResolvedFromCustodyStatusWithoutRenderingAgain()
    {
        // The restart-safe retry: the artifact already has its logical
        // identities, so custody is asked what happened before the same bytes
        // are rendered a second time.
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var store = new FakeStore { PendingDocumentId = documentId, PendingVersionId = versionId };
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();
        var status = new RecordingCustodyStatus
        {
            Result = new CaseArtifactCustodyResult(
                CaseArtifactCustodyDisposition.Confirmed, documentId, versionId, Guid.NewGuid(),
                "box-file", "box-version", Sha256Of([1, 2, 3]), 3, "application/pdf", null, null),
        };

        var result = await Use(store, renderer, custody, status).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        Assert.Equal("operation-1", status.LastOperationKey);
        Assert.Null(status.LastQuery);
        Assert.Empty(renderer.Kinds);
        Assert.Equal(0, custody.Calls);
        Assert.Single(store.Confirmations);
    }

    [Fact]
    public async Task AStillPendingArtifactIsRenderedAgainUnderTheSameOperationKey()
    {
        var store = new FakeStore { PendingDocumentId = Guid.NewGuid(), PendingVersionId = Guid.NewGuid() };
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();
        var status = new RecordingCustodyStatus
        {
            Result = new CaseArtifactCustodyResult(
                CaseArtifactCustodyDisposition.Pending, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                null, null, null, null, null, null, "pending-key"),
        };

        await Use(store, renderer, custody, status).ExecuteAsync(Request(), default);

        Assert.Equal([CaseReportArtifactKind.AssessmentReport], renderer.Kinds);
        Assert.Equal("operation-1", custody.LastRequest!.OperationKey);
    }

    [Fact]
    public async Task AnAlreadyConfirmedArtifactIsReplayedWithoutRendering()
    {
        var store = new FakeStore { FreezeOutcome = CaseReportFreezeOutcome.AlreadyConfirmed };
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();

        var result = await Use(store, renderer, custody).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        Assert.Empty(renderer.Kinds);
        Assert.Equal(0, custody.Calls);
    }

    [Fact]
    public async Task AnUnknownCaseIsNotFound()
    {
        var store = new FakeStore { FreezeOutcome = CaseReportFreezeOutcome.NotFound };

        var result = await Use(store, new RecordingRenderer(), new RecordingCustody())
            .ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task AnActorWithoutCaseworkRightsIsRefused()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => Use(store, new RecordingRenderer(), new RecordingCustody()).ExecuteAsync(
                Request() with { Actor = ActionActor.SystemWorker("worker") }, default));
        Assert.Empty(store.Sequence);
    }

    [Fact]
    public void TheOccurrenceIdentityIsDerivedFromTheGenerationAndKind()
    {
        var generationId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        Assert.Equal(
            "case-report:66666666-6666-6666-6666-666666666666:AssessmentReport",
            GenerateCaseReport.OccurrenceIdentityOf(generationId, CaseReportArtifactKind.AssessmentReport));
        Assert.Equal(
            "case-report:66666666-6666-6666-6666-666666666666:FeeNote",
            GenerateCaseReport.OccurrenceIdentityOf(generationId, CaseReportArtifactKind.FeeNote));
    }

    private static GenerateCaseReport Use(
        FakeStore store,
        RecordingRenderer renderer,
        RecordingCustody custody,
        RecordingCustodyStatus? status = null)
    {
        custody.Sequence = store.Sequence;
        renderer.Sequence = store.Sequence;
        return new GenerateCaseReport(
            store,
            new FakeContentSource(),
            renderer,
            custody,
            status ?? new RecordingCustodyStatus(),
            TimeProvider.System);
    }

    private static GenerateCaseReportRequest Request(
        CaseReportArtifactKind kind = CaseReportArtifactKind.AssessmentReport) => new(
            Engineer, CaseId, 7, "lease-1", "operation-1", kind, "Generate the case report");

    private static AssessmentReadinessItem AssertBlocked(
        CaseReportReadinessResult result, string requirement)
    {
        Assert.False(result.IsReady);
        return Assert.Single(result.Reasons, item => item.Requirement == requirement);
    }

    private static string Sha256Of(byte[] content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    private static CaseReportReadinessInput ReadyInput() => new(
        AssessmentReportProjectionTests.ReadyAssessment(),
        SignatoryId,
        null,
        [Profile()],
        Estimate(),
        Valuation(),
        [
            Preparation(CloseUpOccurrence, CaseAssetReportRole.CloseUp),
            Preparation(OverviewOccurrence, CaseAssetReportRole.Overview),
        ],
        new Dictionary<Guid, DocumentVersion>
        {
            [CloseUpOccurrence] = Version(CloseUpOccurrence),
            [OverviewOccurrence] = Version(OverviewOccurrence),
        });

    private static SignOffEngineerProfile Profile() =>
        new(SignatoryId, "Ed Mawdsley", "ATA VDA AQP", [1, 2, 3], "image/png", IsDefault: true);

    private static AppliedValuation Valuation() => new(
        Guid.Parse("77777777-7777-7777-7777-777777777777"),
        CaseId,
        7,
        Guid.NewGuid(),
        RecordedAtUtc,
        new ValuationCalculation(5_000m, false, 0m, 5_000m, null, 0m, [], 0m, 0m, 5_000m),
        5_000m,
        "engineer-1",
        RecordedAtUtc,
        "Accepted the guide value",
        "case-valuation-calculation/v1");

    private static RepairSpecificationVersion Estimate() =>
        AssessmentReportProjectionTests.ReadyCurrentEstimate();

    private static CaseAssetPreparation Preparation(Guid occurrenceId, CaseAssetReportRole role) => new(
        CaseId, occurrenceId, DocumentIdOf(occurrenceId), VersionIdOf(occurrenceId), 1,
        Sha256Of([(byte)role]), "image/png", role, null, CaseAssetRotation.None,
        CaseAssetCrop.Full, 1, "engineer-1", RecordedAtUtc);

    private static DocumentVersion Version(Guid occurrenceId) => new(
        VersionIdOf(occurrenceId), DocumentIdOf(occurrenceId), 1, "photo.png", "image/png", 8,
        Sha256Of([(byte)(occurrenceId == CloseUpOccurrence
            ? CaseAssetReportRole.CloseUp
            : CaseAssetReportRole.Overview)]),
        DocumentCustodyStatus.Confirmed, RecordedAtUtc, "engineer-1", true, false, null);

    private static Guid DocumentIdOf(Guid occurrenceId) =>
        new([.. occurrenceId.ToByteArray().Select(value => (byte)(value ^ 0x11))]);

    private static Guid VersionIdOf(Guid occurrenceId) =>
        new([.. occurrenceId.ToByteArray().Select(value => (byte)(value ^ 0x22))]);

    private static AssessmentFieldValue Field(string path, string value) => new(
        path, value, ActorKind.Staff, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc);

    private sealed class FakeStore : ICaseReportGenerationStore
    {
        public Guid GenerationId { get; } = Guid.Parse("88888888-8888-8888-8888-888888888888");

        public Guid ArtifactId { get; } = Guid.Parse("99999999-9999-9999-9999-999999999999");

        public CaseReportFreezeOutcome FreezeOutcome { get; init; } = CaseReportFreezeOutcome.Frozen;

        public CaseReportFreezeResult? Freeze { get; init; }

        public Guid? PendingDocumentId { get; init; }

        public Guid? PendingVersionId { get; init; }

        public List<string> Sequence { get; } = [];

        public List<ConfirmCaseReportArtifactRequest> Confirmations { get; } = [];

        public List<RecordCaseReportArtifactOutcomeRequest> Outcomes { get; } = [];

        public Task<CaseReportFreezeResult> FreezeAsync(
            FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken)
        {
            Sequence.Add("freeze");
            if (Freeze is not null)
            {
                return Task.FromResult(Freeze);
            }
            if (FreezeOutcome is CaseReportFreezeOutcome.NotFound)
            {
                return Task.FromResult(new CaseReportFreezeResult(FreezeOutcome, null, null, []));
            }

            return Task.FromResult(new CaseReportFreezeResult(
                FreezeOutcome, Record(request.Kind), ArtifactId, []));
        }

        public Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
            ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken)
        {
            Sequence.Add("confirm");
            Confirmations.Add(request);
            return Task.FromResult(Record(CaseReportArtifactKind.AssessmentReport));
        }

        public Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
            RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken)
        {
            Sequence.Add("record");
            Outcomes.Add(request);
            return Task.FromResult(Record(CaseReportArtifactKind.AssessmentReport));
        }

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(Record(CaseReportArtifactKind.AssessmentReport));

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(Record(CaseReportArtifactKind.AssessmentReport));

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseReportGenerationRecord>>(
                [Record(CaseReportArtifactKind.AssessmentReport)]);

        public Task<int> MarkStaleAsync(
            Guid caseId, string reasonCode, CancellationToken cancellationToken)
        {
            Sequence.Add("stale");
            return Task.FromResult(1);
        }

        private CaseReportGenerationRecord Record(CaseReportArtifactKind kind) => new(
            GenerationId, CaseId, 7, 1, new string('c', 64), Snapshot(),
            AssessmentReportContract.TemplateVersion, "fake", CaseReportGenerationState.Pending,
            RecordedAtUtc, null,
            [
                new CaseReportArtifactRecord(
                    ArtifactId, GenerationId, kind, CaseReportArtifactStatus.Pending, "operation-1",
                    PendingDocumentId, PendingVersionId, null, null, null, null, null, null, null, null),
            ]);

        private static CaseReportGenerationSnapshot Snapshot() => new(
            CaseId, 7, "CE-100", "operation-1", CaseReportActor.Of(Engineer), RecordedAtUtc,
            SignatoryId, Sha256Of([1, 2, 3]), "image/png",
            Guid.NewGuid(), 2, ReportRepairCosts.For(Estimate()), 5_000m, Guid.NewGuid(),
            CaseReportContentSwitches.None, ReportGuideSources.None,
            new DateOnly(2026, 9, 6), false, 120m, ["Engineering assessment"], [], [],
            AssessmentReportContract.TemplateVersion, "fake",
            AssessmentReportRenderingTests.Snapshot(AssessmentReportOutcome.Repairable));
    }

    private sealed class FakeContentSource : ICaseReportContentSource
    {
        public Task<AssessmentReportSnapshot> ComposeAsync(
            CaseReportGenerationSnapshot snapshot, ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot.Report);
    }

    private sealed class RecordingRenderer : IAssessmentReportRenderer
    {
        public List<string>? Sequence { get; set; }

        public List<CaseReportArtifactKind> Kinds { get; } = [];

        public string EngineVersion => "fake";

        public Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default)
        {
            Kinds.Add(kind);
            Sequence?.Add("render");
            byte[] pdf = [1, 2, 3];
            return Task.FromResult(new RenderedReportArtifact(
                $"{kind}.pdf", pdf, 1, Sha256Of(pdf),
                AssessmentReportContract.TemplateVersion, EngineVersion));
        }
    }

    private sealed class RecordingCustody : ICaseArtifactCustody
    {
        public List<string>? Sequence { get; set; }

        public int Calls { get; private set; }

        public CaseArtifactCustodyRequest? LastRequest { get; private set; }

        public CaseArtifactCustodyResult Result { get; init; } = new(
            CaseArtifactCustodyDisposition.Confirmed, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "box-file", "box-version", null, 3, "application/pdf", null, null);

        public Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            Sequence?.Add("retain");
            return Task.FromResult(Result with { Sha256 = Result.Sha256 ?? request.Sha256 });
        }
    }

    private sealed class RecordingCustodyStatus : ICaseArtifactCustodyStatus
    {
        public (Guid CaseId, Guid DocumentId, Guid VersionId, Guid OccurrenceId)? LastQuery { get; private set; }

        public string? LastOperationKey { get; private set; }

        public CaseArtifactCustodyResult Result { get; init; } = new(
            CaseArtifactCustodyDisposition.Unknown, null, null, null,
            null, null, null, null, null, null, null);

        public Task<CaseArtifactCustodyResult> GetAsync(
            ActionActor actor, Guid caseId, Guid documentId, Guid versionId, Guid occurrenceId,
            CancellationToken cancellationToken)
        {
            LastQuery = (caseId, documentId, versionId, occurrenceId);
            return Task.FromResult(Result);
        }

        /// <summary>
        /// The generated artifact's recovery identity is its retain operation
        /// key (G15), so a restart-safe retry reads by it.
        /// </summary>
        public Task<CaseArtifactCustodyResult?> FindByOperationKeyAsync(
            ActionActor actor, Guid caseId, string operationKey,
            CancellationToken cancellationToken)
        {
            LastOperationKey = operationKey;
            return Task.FromResult<CaseArtifactCustodyResult?>(Result);
        }
    }
}
