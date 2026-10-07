using System.Security.Cryptography;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Cases;
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
            [CaseAssetReportRole.Overview, CaseAssetReportRole.CloseUp],
            result.Images.Select(image => image.Role));
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

        // The name and signature are the account's, so the Case cannot clear it.
        var reason = AssertBlocked(result, CaseReportReadiness.SignatoryRequirement);
        Assert.Equal(CaseReportReadiness.SignOffAccountIncomplete, reason);
        Assert.Equal("An Administrator sets a name and signature on the account in Accounts.", reason.HowToResolve);
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

        // An account is offered, so staff choose it on the Case.
        var reason = AssertBlocked(result, CaseReportReadiness.SignatoryRequirement);
        Assert.Equal(CaseReportReadiness.SignOffEngineerNotChosen, reason);
        Assert.Equal("Choose the Sign-off Engineer on Case details.", reason.HowToResolve);
    }

    /// <summary>
    /// The preview names the Sign-off Engineer blocker as generation does, in
    /// each of its three cases.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ThePreviewNamesTheSameSignOffBlocker(bool resolved, bool accountsOffered)
    {
        // A resolved account here has a name and no signature.
        var profile = Profile() with { Signature = [], IsDefault = false };
        var generation = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            PersistedSignOffEngineerId = resolved ? SignatoryId : null,
            EligibleSignOffEngineers = accountsOffered ? [profile] : [],
        });
        var preview = AssessmentReportProjection.Prepare(
            AssessmentReportProjectionTests.ReadyAssessment(),
            Estimate(),
            resolved
                ? new ReportSignatory(
                    profile.PrintedName, profile.Qualifications, profile.Signature, profile.SignatureContentType)
                : null,
            accountsOffered);

        Assert.Equal(
            AssertBlocked(generation, CaseReportReadiness.SignatoryRequirement),
            Assert.Single(preview.Reasons, reason => reason.Requirement == CaseReportReadiness.SignatoryRequirement));
    }

    [Fact]
    public void AMissingCurrentEstimateBlocksGeneration()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with { CurrentEstimate = null });

        var reason = AssertBlocked(result, CaseReportReadiness.CurrentEstimateRequirement);
        Assert.Equal(CaseReportReadiness.CurrentEstimateMissing, reason);
    }

    [Fact]
    public void ACurrentEstimateWithNoLinesBlocksGeneration()
    {
        // Putting a spec in use no longer needs lines; readiness names the gap.
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            CurrentEstimate = Estimate() with { Lines = [] },
        });

        var reason = AssertBlocked(result, CaseReportReadiness.CurrentEstimateRequirement);
        Assert.Equal(CaseReportReadiness.CurrentEstimateEmpty, reason);
        Assert.Equal("The Current repair spec has no lines.", reason.WhyOutstanding);
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

    /// <summary>
    /// A repair spec whose repairer VAT status is Unknown charges VAT on
    /// nothing, so the report would understate the repair cost: it blocks
    /// generation (operator, 27 September 2026). The preview names the same
    /// item, which tells staff where the status is recorded.
    /// </summary>
    [Fact]
    public void AnUnknownRepairerVatStatusBlocksGenerationAndThePreviewAlike()
    {
        var estimate = Estimate();
        var unknown = estimate with { Details = estimate.Details with { Vat = null } };

        var generation = CaseReportReadiness.Evaluate(ReadyInput() with { CurrentEstimate = unknown });
        var preview = AssessmentReportProjection.Prepare(
            AssessmentReportProjectionTests.ReadyAssessment(),
            unknown,
            new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", [1, 2, 3], "image/png"));

        var reason = AssertBlocked(generation, CaseReportReadiness.RepairerVatRequirement);
        Assert.Equal(CaseReportReadiness.RepairerVatStatusUnknown, reason);
        Assert.Equal(reason, Assert.Single(generation.Reasons));
        Assert.Equal(reason, Assert.Single(preview.Reasons));
        Assert.Equal(
            "The Current repair spec does not say whether the repairer is VAT registered, so the report cannot work out the VAT.",
            reason.WhyOutstanding);
        Assert.Equal(
            "Choose Registered or Not registered as the Repairer VAT status on the Repair Spec section.",
            reason.HowToResolve);
    }

    /// <summary>
    /// The template words VAT on every cost and VAT on parts and paint only.
    /// Any other hand-picked set has no accepted wording, whatever the
    /// status, so it blocks generation and the preview too.
    /// </summary>
    [Theory]
    [InlineData(RepairerVatStatus.Registered, EstimateVatCategories.Labour)]
    [InlineData(RepairerVatStatus.NotRegistered, EstimateVatCategories.Parts | EstimateVatCategories.Specialist)]
    [InlineData(RepairerVatStatus.Unknown, EstimateVatCategories.All)]
    public void AVatTreatmentTheTemplateCannotWordBlocksGenerationAndThePreviewAlike(
        RepairerVatStatus status, EstimateVatCategories categories)
    {
        var estimate = Estimate();
        var handPicked = estimate with
        {
            Details = estimate.Details with
            {
                Vat = new EstimateVatPolicy(status, categories, CategoriesOverridden: true),
            },
        };

        var generation = CaseReportReadiness.Evaluate(ReadyInput() with { CurrentEstimate = handPicked });
        var preview = AssessmentReportProjection.Prepare(
            AssessmentReportProjectionTests.ReadyAssessment(),
            handPicked,
            new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", [1, 2, 3], "image/png"));

        var reason = AssertBlocked(generation, CaseReportReadiness.RepairerVatRequirement);
        // An unknown status is named first: recording it is what staff do.
        Assert.Equal(
            status == RepairerVatStatus.Unknown
                ? CaseReportReadiness.RepairerVatStatusUnknown
                : CaseReportReadiness.RepairerVatHandPicked,
            reason);
        Assert.Equal(reason, Assert.Single(preview.Reasons));
        Assert.Contains("on the Repair Spec section", reason.HowToResolve, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RepairerVatStatus.Registered)]
    [InlineData(RepairerVatStatus.NotRegistered)]
    public void ARecordedRepairerVatStatusIsReady(RepairerVatStatus status)
    {
        var estimate = Estimate();

        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            CurrentEstimate = estimate with
            {
                Details = estimate.Details with { Vat = EstimateVatPolicy.For(status) },
            },
        });

        Assert.True(result.IsReady, string.Join("; ", result.Reasons.Select(reason => reason.Requirement)));
    }

    /// <summary>
    /// A typed Engineer's Value is the Case's value: generation needs no
    /// applied valuation behind it (operator, 26 September 2026).
    /// </summary>
    [Fact]
    public void ATypedEngineersValueNeedsNoAppliedValuation()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with { AppliedValuation = null });

        Assert.True(result.IsReady, string.Join("; ", result.Reasons.Select(reason => reason.Requirement)));
    }

    /// <summary>
    /// The one image a report needs is its Overview; the Close-up is optional
    /// (operator, 7 October 2026).
    /// </summary>
    [Fact]
    public void AMissingCloseUpDoesNotBlockGeneration()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            Preparations = [Preparation(OverviewOccurrence, CaseAssetReportRole.Overview)],
        });

        Assert.True(result.IsReady, string.Join("; ", result.Reasons.Select(reason => reason.Requirement)));
        Assert.Equal(CaseAssetReportRole.Overview, Assert.Single(result.Images).Role);
    }

    [Fact]
    public void AMissingOverviewBlocksGeneration()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            Preparations = [Preparation(CloseUpOccurrence, CaseAssetReportRole.CloseUp)],
        });

        var reason = AssertBlocked(result, CaseReportReadiness.OverviewImageRequirement);
        Assert.Equal("Tag one Case image Overview on the Files section.", reason.HowToResolve);
    }

    /// <summary>
    /// An image out of the report never prints, so its Overview tag clears
    /// nothing until it is put back in (operator, 26 September 2026).
    /// </summary>
    [Fact]
    public void AnOverviewTaggedImageOutOfTheReportLeavesTheBlocker()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            Preparations =
            [
                Preparation(CloseUpOccurrence, CaseAssetReportRole.CloseUp),
                Preparation(OverviewOccurrence, CaseAssetReportRole.Overview) with { InReport = false },
            ],
        });

        var reason = AssertBlocked(result, CaseReportReadiness.OverviewImageRequirement);
        Assert.Contains("tagged Overview", reason.WhyOutstanding, StringComparison.Ordinal);
    }

    /// <summary>
    /// Staff cannot clear the Sign-off Engineer on the Case: the name and
    /// signature are the account's, set in Accounts (operator, 26 September 2026).
    /// </summary>
    [Fact]
    public void TheSignOffBlockerNamesAccounts()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with { EligibleSignOffEngineers = [] });

        var reason = AssertBlocked(result, CaseReportReadiness.SignatoryRequirement);
        Assert.Equal(CaseReportReadiness.SignOffAccountMissing, reason);
        Assert.Equal("An Administrator sets a name and signature on the account in Accounts.", reason.HowToResolve);
    }

    /// <summary>
    /// Only an image that can print counts (operator, 26 September 2026): one
    /// still being stored is not one of the report's images and raises no
    /// blocker, though it is in the report and has no confirmed source yet.
    /// </summary>
    [Fact]
    public void AnImageThatCannotPrintRaisesNoBlocker()
    {
        var input = ReadyInput();
        var arriving = Preparation(Guid.NewGuid(), CaseAssetReportRole.Overview) with { CanPrint = false };

        var result = CaseReportReadiness.Evaluate(input with { Preparations = [.. input.Preparations, arriving] });

        Assert.True(result.IsReady, string.Join("; ", result.Reasons.Select(reason => reason.Requirement)));
        Assert.DoesNotContain(result.Images, image => image.OccurrenceId == arriving.OccurrenceId);
    }

    /// <summary>
    /// A tag on an image that cannot print clears nothing: the blocker asks
    /// for the tag, and nothing names the image's storage.
    /// </summary>
    [Fact]
    public void AnOverviewTaggedImageThatCannotPrintLeavesOnlyTheOverviewBlocker()
    {
        var result = CaseReportReadiness.Evaluate(ReadyInput() with
        {
            Preparations =
            [
                Preparation(CloseUpOccurrence, CaseAssetReportRole.CloseUp),
                Preparation(OverviewOccurrence, CaseAssetReportRole.Overview) with { CanPrint = false },
            ],
        });

        Assert.Equal(
            CaseReportReadiness.OverviewImageRequirement,
            Assert.Single(result.Reasons).Requirement);
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

        // Staff cannot switch the image: the blocker names it and sends them to Files.
        var reason = AssertBlocked(result, CaseReportReadiness.ImageSourceRequirement);
        Assert.Equal("The stored version of close-up.png has changed.", reason.WhyOutstanding);
        Assert.Equal("Open the Files section to see the image as it is stored now.", reason.HowToResolve);
    }

    [Fact]
    public void AnImageWithoutAConfirmedSourceBlocksGeneration()
    {
        var input = ReadyInput();
        var missing = input.ConfirmedImageSources
            .Where(entry => entry.Key != CloseUpOccurrence)
            .ToDictionary();

        var result = CaseReportReadiness.Evaluate(input with { ConfirmedImageSources = missing });

        AssertBlocked(result, CaseReportReadiness.ImageSourceRequirement);
    }

    /// <summary>
    /// The report prints the trade value beside the Engineer's Value, so a
    /// Case without one is not generated; it is entered on Valuation
    /// (operator, 26 September 2026).
    /// </summary>
    [Fact]
    public void AMissingTradeValueBlocksGeneration()
    {
        var input = ReadyInput();
        var withoutTrade = input.Assessment.Fields
            .Where(field => field.Path != AssessmentVocabulary.ValueTrade)
            .ToArray();

        var result = CaseReportReadiness.Evaluate(
            input with { Assessment = input.Assessment with { Fields = withoutTrade } });

        var reason = AssertBlocked(result, "Trade value");
        Assert.Equal("Enter it on the Valuation section.", reason.HowToResolve);
    }

    /// <summary>
    /// The report says the damage was assessed on the Case's Inspection date,
    /// and entry to Review does not prove one is recorded.
    /// </summary>
    [Fact]
    public void AMissingInspectionDateBlocksGeneration()
    {
        var input = ReadyInput();

        var result = CaseReportReadiness.Evaluate(input with
        {
            Assessment = input.Assessment with
            {
                CaseOwned = input.Assessment.CaseOwned with { InspectionDate = null }
            }
        });

        Assert.False(result.IsReady);
        Assert.Equal("Inspection date", Assert.Single(result.Reasons).Requirement);
    }

    /// <summary>
    /// A report-level blocker about one recorded fact names that fact, so the
    /// Case page can send the operator to the section that records it; one
    /// about other material (the sign-off account, the Current repair spec,
    /// the report images) names neither a field nor a line.
    /// </summary>
    [Fact]
    public void ReportLevelBlockersNameTheirField()
    {
        var input = ReadyInput();
        AssessmentFieldValue[] fields =
        [
            .. input.Assessment.Fields.Where(field => field.Path != AssessmentVocabulary.DamageUnrelated),
            Field(AssessmentVocabulary.ReportValuationCommentary, "true"),
            Field(AssessmentVocabulary.ReportIncludeUnrelatedDamage, "true"),
        ];

        var reasons = CaseReportReadiness.Evaluate(input with
        {
            Assessment = input.Assessment with { Fields = fields },
            PersistedSignOffEngineerId = null,
            AssignedEngineerId = null,
            EligibleSignOffEngineers = [],
            CurrentEstimate = null,
            AppliedValuation = null,
            Preparations = [],
        }).Reasons;

        string? FieldOf(string requirement) =>
            Assert.Single(reasons, reason => reason.Requirement == requirement).Field;

        Assert.Equal(
            AssessmentVocabulary.ReportValuationCommentaryText,
            FieldOf(CaseReportReadiness.ValuationCommentaryRequirement));
        Assert.Equal(AssessmentVocabulary.DamageUnrelated, FieldOf(CaseReportReadiness.UnrelatedDamageRequirement));
        foreach (var requirement in new[]
        {
            CaseReportReadiness.SignatoryRequirement,
            CaseReportReadiness.CurrentEstimateRequirement,
            CaseReportReadiness.OverviewImageRequirement,
        })
        {
            var material = Assert.Single(reasons, item => item.Requirement == requirement);
            Assert.Null(material.Field);
        }
    }

    /// <summary>
    /// Each report-level resolution names the Case section that clears it, and
    /// the preview's Prepare names a missing sign-off Engineer and Current
    /// repair spec with generation's own items.
    /// </summary>
    [Fact]
    public void ReportReadinessResolutionsNameTheLiveCaseSections()
    {
        var nothingRecorded = ReadyInput() with
        {
            Assessment = AssessmentReportProjectionTests.ReadyAssessment() with { Fields = [], EstimateLines = [] },
            PersistedSignOffEngineerId = null,
            AssignedEngineerId = null,
            EligibleSignOffEngineers = [],
            CurrentEstimate = null,
            AppliedValuation = null,
            Preparations = [],
            ConfirmedImageSources = new Dictionary<Guid, DocumentVersion>(),
        };
        var reasons = CaseReportReadiness.Evaluate(nothingRecorded).Reasons;

        string HowToResolve(string requirement) =>
            Assert.Single(reasons, reason => reason.Requirement == requirement).HowToResolve;

        Assert.Contains(
            "in Accounts",
            HowToResolve(CaseReportReadiness.SignatoryRequirement),
            StringComparison.Ordinal);
        Assert.Contains(
            "the Repair Spec section",
            HowToResolve(CaseReportReadiness.CurrentEstimateRequirement),
            StringComparison.Ordinal);
        // The post-review Engineer's Value item is the one blocker for the missing value.
        var engineerValue = Assert.Single(reasons, reason => reason.Field == AssessmentVocabulary.ValueEngineer);
        Assert.Contains("the Valuation section", engineerValue.HowToResolve, StringComparison.Ordinal);
        Assert.Contains(
            "the Files section",
            HowToResolve(CaseReportReadiness.OverviewImageRequirement),
            StringComparison.Ordinal);

        var prepared = AssessmentReportProjection.Prepare(nothingRecorded.Assessment).Reasons;
        Assert.Equal(
            Assert.Single(reasons, reason => reason.Requirement == CaseReportReadiness.SignatoryRequirement),
            Assert.Single(prepared, reason => reason.Requirement == CaseReportReadiness.SignatoryRequirement));
        Assert.Equal(
            Assert.Single(reasons, reason => reason.Requirement == CaseReportReadiness.CurrentEstimateRequirement),
            Assert.Single(prepared, reason => reason.Requirement == CaseReportReadiness.CurrentEstimateRequirement));
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

    /// <summary>
    /// "Engineer's Value applied." is the reason the Case save records by
    /// itself: a message for the screen, not commentary. It does not satisfy
    /// the switch, so it can never print as the report's commentary.
    /// </summary>
    [Fact]
    public void TheCaseSavesOwnReasonDoesNotSatisfyTheValuationCommentarySwitch()
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Append(Field(AssessmentVocabulary.ReportValuationCommentary, "true"))
            .ToArray();

        var result = CaseReportReadiness.Evaluate(input with
        {
            Assessment = input.Assessment with { Fields = fields },
            AppliedValuation = Valuation() with { Reason = ValuationCalculationPolicy.AppliedReason },
        });

        AssertBlocked(result, CaseReportReadiness.ValuationCommentaryRequirement);
    }

    /// <summary>
    /// Phase 5b: the Engineer's written commentary satisfies the switch on its
    /// own, and it is what the report prints ahead of the applied valuation's
    /// reason; with neither there is nothing to print and no placeholder.
    /// </summary>
    [Fact]
    public void WrittenValuationCommentarySatisfiesTheSwitchAndIsWhatPrints()
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Append(Field(AssessmentVocabulary.ReportValuationCommentary, "true"))
            .Append(Field(AssessmentVocabulary.ReportValuationCommentaryText, "Low mileage for its age; the retail guide is adjusted up."))
            .ToArray();
        var assessment = input.Assessment with { Fields = fields };

        var result = CaseReportReadiness.Evaluate(input with
        {
            Assessment = assessment,
            AppliedValuation = Valuation() with { Reason = "  " },
        });

        Assert.DoesNotContain(result.Reasons, reason => reason.Requirement == CaseReportReadiness.ValuationCommentaryRequirement);
        Assert.Equal(
            "Low mileage for its age; the retail guide is adjusted up.",
            AssessmentReportProjection.ValuationCommentaryOf(assessment, "Applied from Glass's"));
        Assert.Equal("Applied from Glass's", AssessmentReportProjection.ValuationCommentaryOf(input.Assessment, "Applied from Glass's"));
        Assert.Null(AssessmentReportProjection.ValuationCommentaryOf(input.Assessment, "  "));
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
    public void AReportDateDefaultsOnlyAtGenerationAndARecordedDateWins()
    {
        var generatedOn = new DateOnly(2026, 9, 6);

        Assert.Equal(generatedOn, CaseReportReadiness.ResolveReportDate(null, generatedOn));
        Assert.Equal(
            new DateOnly(2026, 7, 4),
            CaseReportReadiness.ResolveReportDate(new DateOnly(2026, 7, 4), generatedOn));
    }

    [Fact]
    public async Task NotReadyRefusesBeforeAnyRenderOrCustodyCall()
    {
        var store = new FakeStore
        {
            Freeze = new(CaseReportFreezeOutcome.NotReady, null, null,
                [new AssessmentReadinessItem("Overview image", "Case files", "why", "how")]),
        };
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();

        var result = await Use(store, renderer, custody).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.NotReady, result.Outcome);
        Assert.Equal("Overview image", Assert.Single(result.Reasons).Requirement);
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

    [Theory]
    [InlineData(CaseReportArtifactKind.AssessmentReport, "CE_100_assessment.pdf")]
    [InlineData(CaseReportArtifactKind.FeeNote, "CE_100_fee_note.pdf")]
    [InlineData(CaseReportArtifactKind.RepairSpecification, "CE_100_repair_specification.pdf")]
    [InlineData(CaseReportArtifactKind.ImagePack, "CE_100_images.pdf")]
    public async Task EveryArtifactKindGetsItsOwnCustodyFileName(
        CaseReportArtifactKind kind, string expectedFileName)
    {
        var store = new FakeStore();
        var documents = kind == CaseReportArtifactKind.RepairSpecification
            ? new RenderedRepairSpecificationDocuments()
            : null;

        var result = await Use(
                store, new RecordingRenderer(), new RecordingCustody(), repairSpecificationDocuments: documents)
            .ExecuteAsync(Request(kind), default);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        Assert.Equal(expectedFileName, Assert.Single(store.Confirmations).FileName);
    }

    /// <summary>
    /// The report freezes fresh facts; a separate fee note names the
    /// generation it extends, so it is made from that report's frozen facts.
    /// </summary>
    [Fact]
    public async Task ASeparateFeeNoteNamesTheGenerationItExtends()
    {
        var store = new FakeStore();
        var targetGenerationId = Guid.NewGuid();

        await Use(store, new RecordingRenderer(), new RecordingCustody())
            .ExecuteAsync(Request(), default);
        await Use(store, new RecordingRenderer(), new RecordingCustody())
            .ExecuteAsync(
                Request(CaseReportArtifactKind.FeeNote) with { TargetGenerationId = targetGenerationId },
                default);

        Assert.Null(store.Freezes[0].TargetGenerationId);
        Assert.Equal(targetGenerationId, store.Freezes[1].TargetGenerationId);
    }

    /// <summary>
    /// The fee facts the fee note prints are a report readiness requirement,
    /// because every report ends with its fee note: a Case without an agreed
    /// fee is refused.
    /// </summary>
    [Fact]
    public void AMissingAgreedFeeBlocksGeneration()
    {
        var input = ReadyInput();
        var withoutFee = input.Assessment.Fields
            .Where(field => field.Path != AssessmentVocabulary.AgreedFee)
            .ToArray();

        var result = CaseReportReadiness.Evaluate(
            input with { Assessment = input.Assessment with { Fields = withoutFee } });

        var reason = AssertBlocked(result, "Agreed fee");
        Assert.Equal("Assessment record", reason.Source);
    }

    [Fact]
    public async Task RenderingAndRetentionHappenAfterTheFreezeTransactionCommits()
    {
        var store = new FakeStore();
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();

        await Use(store, renderer, custody).ExecuteAsync(Request(), default);

        // Custody is asked what it holds before anything is drawn.
        Assert.Equal(["freeze", "ask", "render", "retain", "confirm"], store.Sequence);
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

    /// <summary>
    /// A request can end after custody recorded the file and before the
    /// report's row was given its version. The retry asks custody by the
    /// operation key all the same, and confirms the file custody holds.
    /// </summary>
    [Fact]
    public async Task ARowWithNoRecordedVersionAsksCustodyBeforeRendering()
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var store = new FakeStore();
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
        Assert.Empty(renderer.Kinds);
        Assert.Equal(0, custody.Calls);
        var confirmed = Assert.Single(store.Confirmations);
        Assert.Equal(documentId, confirmed.DocumentId);
        Assert.Equal(versionId, confirmed.VersionId);
        Assert.Equal("box-file", confirmed.BoxFileId);
        Assert.Equal(["freeze", "ask", "confirm"], store.Sequence);
    }

    /// <summary>
    /// A drawn file carries its own creation time, so drawing a file custody
    /// still holds as Pending could never match it. The retry records what
    /// custody says, with custody's identities, and draws nothing.
    /// </summary>
    [Fact]
    public async Task AStillPendingFileIsRecordedAsPendingWithoutRenderingAgain()
    {
        var store = new FakeStore { PendingDocumentId = Guid.NewGuid(), PendingVersionId = Guid.NewGuid() };
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();
        var held = new CaseArtifactCustodyResult(
            CaseArtifactCustodyDisposition.Pending, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, null, Sha256Of([1, 2, 3]), 3, "application/pdf", "case_custody_pending", "pending-key");
        var status = new RecordingCustodyStatus { Result = held };

        var result = await Use(store, renderer, custody, status).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.Pending, result.Outcome);
        Assert.Equal("operation-1", status.LastOperationKey);
        Assert.Empty(renderer.Kinds);
        Assert.Equal(0, custody.Calls);
        Assert.Empty(store.Confirmations);
        var recorded = Assert.Single(store.Outcomes);
        Assert.Equal(CaseReportArtifactStatus.Pending, recorded.Status);
        Assert.Equal(held.DocumentId, recorded.DocumentId);
        Assert.Equal(held.VersionId, recorded.VersionId);
        Assert.Equal("pending-key", recorded.PendingContentStorageKey);
        Assert.Equal("case_custody_pending", recorded.FailureCode);
        Assert.Equal(["freeze", "ask", "record"], store.Sequence);
    }

    [Fact]
    public async Task AFailedFileIsRecordedAsFailedWithoutRenderingAgain()
    {
        var store = new FakeStore();
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();
        var held = new CaseArtifactCustodyResult(
            CaseArtifactCustodyDisposition.Failed, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, null, Sha256Of([1, 2, 3]), 3, "application/pdf", "case_custody_failed", null);
        var status = new RecordingCustodyStatus { Result = held };

        var result = await Use(store, renderer, custody, status).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.Failed, result.Outcome);
        Assert.Empty(renderer.Kinds);
        Assert.Equal(0, custody.Calls);
        var recorded = Assert.Single(store.Outcomes);
        Assert.Equal(CaseReportArtifactStatus.Failed, recorded.Status);
        Assert.Equal(held.VersionId, recorded.VersionId);
        Assert.Equal("case_custody_failed", recorded.FailureCode);
    }

    /// <summary>
    /// Custody holds a record of the file though it cannot say what became
    /// of it. A second drawing would still be refused against that record, so
    /// the retry keeps the outcome and draws nothing.
    /// </summary>
    [Fact]
    public async Task AFileCustodyCannotAccountForIsRecordedWithoutRenderingAgain()
    {
        var store = new FakeStore();
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();
        var held = new CaseArtifactCustodyResult(
            CaseArtifactCustodyDisposition.Unknown, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, null, null, null, null, null, null);
        var status = new RecordingCustodyStatus { Result = held };

        var result = await Use(store, renderer, custody, status).ExecuteAsync(Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.Pending, result.Outcome);
        Assert.Empty(renderer.Kinds);
        Assert.Equal(0, custody.Calls);
        var recorded = Assert.Single(store.Outcomes);
        Assert.Equal(CaseReportArtifactStatus.Unknown, recorded.Status);
        Assert.Equal(held.VersionId, recorded.VersionId);
    }

    /// <summary>
    /// The fee note, the Repair Spec and the image pack retry as the report
    /// does: a stored file is confirmed under its own name and nothing is
    /// drawn, the Repair Spec's own document included.
    /// </summary>
    [Theory]
    [InlineData(CaseReportArtifactKind.AssessmentReport, "CE_100_assessment.pdf")]
    [InlineData(CaseReportArtifactKind.FeeNote, "CE_100_fee_note.pdf")]
    [InlineData(CaseReportArtifactKind.RepairSpecification, "CE_100_repair_specification.pdf")]
    [InlineData(CaseReportArtifactKind.ImagePack, "CE_100_images.pdf")]
    public async Task ARetryOfEveryArtifactKindConfirmsTheStoredFileWithoutDrawingIt(
        CaseReportArtifactKind kind, string expectedFileName)
    {
        var store = new FakeStore();
        var renderer = new RecordingRenderer();
        var custody = new RecordingCustody();
        var status = new RecordingCustodyStatus
        {
            Result = new CaseArtifactCustodyResult(
                CaseArtifactCustodyDisposition.Confirmed, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "box-file", "box-version", Sha256Of([1, 2, 3]), 3, "application/pdf", null, null),
        };

        // The default Repair Spec document source refuses every call.
        var result = await Use(store, renderer, custody, status).ExecuteAsync(Request(kind), default);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        Assert.Empty(renderer.Kinds);
        Assert.Equal(0, custody.Calls);
        Assert.Equal(expectedFileName, Assert.Single(store.Confirmations).FileName);
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
        RecordingCustodyStatus? status = null,
        IRenderCaseEstimateDocument? repairSpecificationDocuments = null)
    {
        custody.Sequence = store.Sequence;
        renderer.Sequence = store.Sequence;
        status ??= new RecordingCustodyStatus();
        status.Sequence = store.Sequence;
        return new GenerateCaseReport(
            store,
            new FakeContentSource(),
            renderer,
            repairSpecificationDocuments ?? new RefusingRepairSpecificationDocuments(),
            custody,
            status,
            TimeProvider.System);
    }

    /// <summary>
    /// The repair specification document is not asked for by these tests: a
    /// call here would mean a companion artifact took the snapshot route.
    /// </summary>
    private sealed class RefusingRepairSpecificationDocuments : IRenderCaseEstimateDocument
    {
        public Task<RenderCaseEstimateDocumentResult> ExecuteAsync(
            Guid caseId, Guid estimateId, ActionActor actor, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No repair specification document was expected.");
    }

    private sealed class RenderedRepairSpecificationDocuments : IRenderCaseEstimateDocument
    {
        public Task<RenderCaseEstimateDocumentResult> ExecuteAsync(
            Guid caseId, Guid estimateId, ActionActor actor, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderCaseEstimateDocumentResult(
                RenderCaseEstimateDocumentOutcome.Rendered,
                new RenderedReportArtifact(
                    "CE_100_estimate.pdf", [1, 2, 3], 1, Sha256Of([1, 2, 3]),
                    AssessmentReportContract.TemplateVersion, "fake"),
                [], 2));
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

    /// <summary>An image in the report wearing the tag that prints it as <paramref name="role"/>.</summary>
    private static CaseAssetPreparation Preparation(Guid occurrenceId, CaseAssetReportRole role) => new(
        CaseId, occurrenceId, DocumentIdOf(occurrenceId), VersionIdOf(occurrenceId), 1,
        Sha256Of([(byte)role]), "image/png", true, null, CaseAssetRotation.None,
        CaseAssetCrop.Full, 1, "engineer-1", RecordedAtUtc)
    {
        TagIds = [role == CaseAssetReportRole.CloseUp ? ImageTagVocabulary.CloseUpId : ImageTagVocabulary.OverviewId],
        SourceFileName = role == CaseAssetReportRole.CloseUp ? "close-up.png" : "overview.png",
        RecordedAtUtc = RecordedAtUtc,
        CanPrint = true
    };

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
        path, value, ActorKind.Staff, "engineer-1", RecordedAtUtc);

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

        public List<FreezeCaseReportGenerationRequest> Freezes { get; } = [];

        public Task<CaseReportFreezeResult> FreezeAsync(
            FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken)
        {
            Sequence.Add("freeze");
            Freezes.Add(request);
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

        public Task<CaseReportGenerationRecord> GetForDeliveryAsync(
            SendCaseReportRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(Record(CaseReportArtifactKind.AssessmentReport));

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(Record(CaseReportArtifactKind.AssessmentReport));

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseReportGenerationRecord>>(
                [Record(CaseReportArtifactKind.AssessmentReport)]);

        public Task<int> MarkStaleAsync(
            Guid caseId, string reasonCode, CancellationToken cancellationToken)
        {
            Sequence.Add("stale");
            return Task.FromResult(1);
        }

        public List<RecordCaseReportDraftPreviewedRequest> Previews { get; } = [];

        public Task RecordDraftPreviewedAsync(
            RecordCaseReportDraftPreviewedRequest request, CancellationToken cancellationToken)
        {
            Sequence.Add("preview");
            Previews.Add(request);
            return Task.CompletedTask;
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

        private static CaseReportGenerationSnapshot Snapshot()
        {
            var estimate = Estimate();
            return new(
                CaseId, 7, "CE-100", "operation-1", CaseReportActor.Of(Engineer), RecordedAtUtc,
                SignatoryId, Sha256Of([1, 2, 3]), "image/png",
                estimate.SpecificationId, estimate.Version, ReportRepairCosts.For(estimate), 5_000m, Guid.NewGuid(),
                CaseReportContentSwitches.None, ReportGuideSources.None,
                new DateOnly(2026, 9, 6), 120m, ["Engineering assessment"], [], [],
                AssessmentReportContract.TemplateVersion, "fake",
                AssessmentReportRenderingTests.Snapshot(AssessmentReportOutcome.Repairable))
            {
                CurrentEstimate = estimate
            };
        }
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
        public List<string>? Sequence { get; set; }

        public (Guid CaseId, Guid DocumentId, Guid VersionId, Guid OccurrenceId)? LastQuery { get; private set; }

        public string? LastOperationKey { get; private set; }

        /// <summary>What custody holds under the operation key; nothing on a first request.</summary>
        public CaseArtifactCustodyResult? Result { get; init; }

        public Task<CaseArtifactCustodyResult> GetAsync(
            ActionActor actor, Guid caseId, Guid documentId, Guid versionId, Guid occurrenceId,
            CancellationToken cancellationToken)
        {
            LastQuery = (caseId, documentId, versionId, occurrenceId);
            return Task.FromResult(Result
                ?? throw new FileNotFoundException("Custody holds nothing under these identities."));
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
            Sequence?.Add("ask");
            return Task.FromResult(Result);
        }
    }
}
