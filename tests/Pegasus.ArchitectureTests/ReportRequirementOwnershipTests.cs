using System.Reflection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// Issue #834: readiness required facts nothing could write. Every fact the
/// report needs has exactly one writer staff can reach, and every blocker
/// links to the Case section that clears it.
/// </summary>
public sealed class ReportRequirementOwnershipTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly AssessmentCaseOwnedData CompleteCaseOwned = new(
        Registration: "AB12CDE",
        Make: "Ford",
        Model: "Focus",
        Year: "2012",
        Mileage: 80_000,
        MileageUnit: "miles",
        MileageSource: "owner",
        IncidentDate: new DateOnly(2026, 8, 1),
        ReceivedDate: new DateOnly(2026, 8, 2),
        InspectionMode: nameof(CaseInspectionMode.ImageBasedAssessment),
        InspectionAddress: "Image Based Assessment",
        InspectionDate: new DateOnly(2026, 8, 3),
        ClaimantName: "Alex Example",
        ClaimNumber: "P-100");

    // Only what every Case has: its received date, and a mileage source that
    // reads tbc until a mileage is recorded.
    private static readonly AssessmentCaseOwnedData NothingRecordedCaseOwned = new(
        Registration: null,
        Make: null,
        Model: null,
        Year: null,
        Mileage: null,
        MileageUnit: null,
        MileageSource: "tbc",
        IncidentDate: null,
        ReceivedDate: new DateOnly(2026, 8, 2),
        InspectionMode: null,
        InspectionAddress: null,
        InspectionDate: null,
        ClaimantName: null,
        ClaimNumber: null);

    [Fact]
    public void EveryUnconditionalReportRequirementHasExactlyOneWriter()
    {
        var complete = Complete();
        Assert.Empty(AssessmentPolicy.EvaluatePostReviewReadiness(complete));

        var failures = new List<string>();
        var named = new List<AssessmentReadinessItem>();
        foreach (var path in AssessmentVocabulary.Definitions.Keys)
        {
            // Nothing named means the report does not require the path.
            var items = AssessmentPolicy.EvaluatePostReviewReadiness(With(complete, (path, null)));
            if (items.Count == 0)
            {
                continue;
            }

            // One missing fact, one blocker.
            var item = Assert.Single(items);
            var writers = new List<string>();
            if (CaseWorkspaceLabels.Editors.IsStaffConfirmable(path))
            {
                writers.Add("its Case section");
            }
            // AssessmentWriteSet derives these from the damage impacts.
            if (AssessmentVocabulary.DerivedPaths.Contains(path)
                && CaseWorkspaceLabels.Editors.IsStaffConfirmable(AssessmentVocabulary.DamageImpacts))
            {
                writers.Add("the damage derivation");
            }
            // The Case Save's valuation adoption records these.
            if (AssessmentVocabulary.AdoptedFindingPaths.Contains(path))
            {
                writers.Add("the valuation adoption");
            }
            // The DVLA/DVSA lookup records these.
            if (AssessmentVocabulary.LookupDerivedPaths.Contains(path))
            {
                writers.Add("the vehicle lookup");
            }
            // None is the #834 defect; two means an adopted or derived path
            // leaked into an editor.
            if (writers.Count != 1)
            {
                failures.Add($"{path} ('{item.Requirement}'): writers [{string.Join(", ", writers)}]");
            }
            named.Add(item);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.All(named, AssertLinks);
    }

    [Fact]
    public void EveryCaseFactTheReportPrintsIsANamedBlocker()
    {
        var complete = Complete();
        var constructor = Assert.Single(typeof(AssessmentCaseOwnedData).GetConstructors());
        var parameters = constructor.GetParameters();
        var recorded = parameters
            .Select(parameter =>
                typeof(AssessmentCaseOwnedData).GetProperty(parameter.Name!)!.GetValue(complete.CaseOwned))
            .ToArray();
        // Read from the record's own nullability, so a Case fact added to it
        // is covered without editing this test. The received date every Case
        // has is never absent.
        var nullability = new NullabilityInfoContext();
        var absentable = parameters
            .Where(parameter => nullability.Create(parameter).WriteState == NullabilityState.Nullable)
            .ToArray();
        Assert.Contains(absentable, parameter => parameter.Name == nameof(AssessmentCaseOwnedData.InspectionDate));
        Assert.DoesNotContain(absentable, parameter => parameter.Name == nameof(AssessmentCaseOwnedData.ReceivedDate));

        foreach (var parameter in absentable)
        {
            object?[] arguments = [.. recorded];
            arguments[parameter.Position] = null;
            var assessment = complete with { CaseOwned = (AssessmentCaseOwnedData)constructor.Invoke(arguments) };
            // The whole rail, so the make, model and year Review entry names
            // are held to the same rule as the report's own Case facts.
            Assert.All(AssessmentPolicy.EvaluateReadiness(assessment), item =>
            {
                Assert.Equal("Case record", item.Source);
                AssertLinks(item);
            });
        }
    }

    [Fact]
    public void EveryReportBlockerLinksASectionTheCaseRecordHas()
    {
        Assert.All(
            ReportBlockerTriggers().SelectMany(input => CaseReportReadiness.Evaluate(input).Reasons),
            AssertLinks);
    }

    [Fact]
    public void TheAutomationWriteSurfaceIsTheStaffEditorSurface()
    {
        var failures = new List<string>();
        var accepted = new List<string>();
        foreach (var (path, definition) in AssessmentVocabulary.Definitions)
        {
            if (AssessmentVocabulary.DerivedPaths.Contains(path)
                || AssessmentVocabulary.LookupDerivedPaths.Contains(path))
            {
                continue;
            }

            var automationWrites = AutomationRefusal(path) is null;
            var staffConfirmOrClear = !definition.IsFinding && CaseWorkspaceLabels.Editors.IsStaffConfirmable(path);
            if (automationWrites != staffConfirmOrClear)
            {
                failures.Add(automationWrites
                    ? $"{path}: automation writes it and staff cannot confirm or clear it on the Case"
                    : $"{path}: staff confirm it on the Case and automation cannot write it");
            }
            if (automationWrites)
            {
                accepted.Add(path);
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.Contains(
            "no staff editor", AutomationRefusal(AssessmentVocabulary.RateCard)!.Message, StringComparison.Ordinal);
        Assert.Null(AutomationRefusal(AssessmentVocabulary.CostRecoveryCharge));
        // The 20 non-finding Decisions editors, 4 original report, 8 Report,
        // 14 Damage and 4 Vehicle editors, the vehicle history and condition,
        // and the 5 typed Case-save paths. A new editor changes this count on
        // purpose: it widens what automation may write.
        Assert.Equal(57, accepted.Count);
    }

    [Theory]
    [InlineData(AssessmentVocabulary.ValueRetail, null, null, "valuation")]
    [InlineData(AssessmentVocabulary.ValueTrade, null, null, "valuation")]
    [InlineData(AssessmentVocabulary.ValueEngineer, null, null, "valuation")]
    [InlineData(AssessmentVocabulary.Outcome, null, null, "settlement")]
    [InlineData(AssessmentVocabulary.LegalStatus, null, null, "settlement")]
    [InlineData(AssessmentVocabulary.SalvageCategory, null, null, "settlement")]
    [InlineData(AssessmentVocabulary.SettlementContractSum, null, null, "settlement")]
    [InlineData(AssessmentVocabulary.SettlementClaimantVatRegistered, null, null, "claim")]
    [InlineData(AssessmentVocabulary.ReportDiscloseGuideSource, null, null, "valuation")]
    [InlineData(AssessmentVocabulary.VehicleType, null, null, "vehicle")]
    [InlineData(AssessmentVocabulary.VehicleCondition, null, null, "vehicle")]
    [InlineData(AssessmentVocabulary.HistoryCheck, null, null, "vehicle")]
    [InlineData(AssessmentVocabulary.ImpactSeverity, null, null, "damage")]
    [InlineData(AssessmentVocabulary.DamageUnrelated, null, null, "damage")]
    [InlineData(AssessmentVocabulary.AgreedFee, null, null, "report")]
    [InlineData(AssessmentVocabulary.ReportDate, null, null, "report")]
    [InlineData(AssessmentVocabulary.ReportValuationCommentaryText, null, null, "report")]
    [InlineData(CaseDataFieldNames.InspectionDate, null, null, "inspection")]
    [InlineData(CaseDataFieldNames.InspectionAddress, null, null, "inspection")]
    [InlineData(CaseDataFieldNames.InspectionMode, null, null, "inspection")]
    [InlineData(CaseDataFieldNames.ClaimantName, null, null, "claim")]
    [InlineData(CaseDataFieldNames.ClaimNumber, null, null, "overview")]
    [InlineData(CaseDataFieldNames.IncidentDate, null, null, "overview")]
    [InlineData(AssessmentVocabulary.VehicleFuel, null, null, "vehicle")]
    [InlineData(AssessmentVocabulary.VehicleTransmission, null, null, "vehicle")]
    [InlineData(AssessmentVocabulary.VehicleAirbagsDeployed, null, null, "damage")]
    [InlineData(AssessmentVocabulary.VehicleTemporaryRepairsPossible, null, null, "settlement")]
    [InlineData(AssessmentVocabulary.VehicleTemporaryRepairMethod, null, null, "settlement")]
    [InlineData(AssessmentVocabulary.VehicleTemporaryRepairCost, null, null, "settlement")]
    [InlineData(null, 3, null, "estimate")]
    [InlineData(null, null, CaseReportReadiness.SignatoryRequirement, "overview")]
    [InlineData(null, null, CaseReportReadiness.CurrentEstimateRequirement, "estimate")]
    [InlineData(null, null, CaseReportReadiness.LabourRateRequirement, "estimate")]
    [InlineData(null, null, CaseReportReadiness.CloseUpImageRequirement, "files")]
    [InlineData(null, null, CaseReportReadiness.OverviewImageRequirement, "files")]
    [InlineData(null, null, CaseReportReadiness.ImageSourceRequirement, "files")]
    public void BlockerSectionMapsEachBlockerToTheSectionThatClearsIt(
        string? field, int? estimateLine, string? requirement, string? section)
    {
        var item = new AssessmentReadinessItem(
            requirement ?? "Requirement", "Source", "Why outstanding", "How to resolve", field, estimateLine);

        Assert.Equal(section, CaseWorkspaceLabels.Report.BlockerSection(item));
    }

    /// <summary>
    /// Engine capacity, fuel, colour, tax expiry and MOT expiry are the
    /// DVLA/DVSA lookup's alone (operator, 24 September 2026): no Case editor,
    /// automation write or field save records one, each answer sets all of
    /// them, and a blocker naming one links the Vehicle section that shows it.
    /// </summary>
    [Fact]
    public void TheVehicleLookupIsTheOnlyWriterOfTheFactsItDerives()
    {
        var notFound = new VehicleLookupResult(
            "AB12CDE", VehicleLookupOutcome.NotFound, "dvla-ves+dvsa-mot-history", "ves-1.2+mot-history-v1",
            "response", RecordedAtUtc, null, null, null, [], null);
        var writes = VehicleLookupFillPolicy.DerivedAssessmentWrites(notFound);

        Assert.Equal(
            AssessmentVocabulary.LookupDerivedPaths.Order(StringComparer.Ordinal).ToArray(),
            writes.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.All(writes.Values, value => Assert.Null(value));

        Assert.All(AssessmentVocabulary.LookupDerivedPaths, path =>
        {
            Assert.Contains(path, AssessmentVocabulary.Definitions);
            Assert.DoesNotContain(path, AssessmentVocabulary.DerivedPaths);
            Assert.DoesNotContain(path, AssessmentVocabulary.AdoptedFindingPaths);
            Assert.DoesNotContain(path, AssessmentVocabulary.CaseOwnedPaths);
            Assert.False(CaseWorkspaceLabels.Editors.IsStaffConfirmable(path));
            // MCP lets the path through, so Core's field save names the refusal.
            Assert.Null(AutomationRefusal(path));
            var refusal = Assert.Throws<InvalidOperationException>(() =>
                AssessmentPolicy.NormalizeWritableField(path, "1"));
            Assert.Contains("filled by the DVLA/DVSA vehicle lookup", refusal.Message, StringComparison.Ordinal);
            Assert.Equal("vehicle", CaseWorkspaceLabels.Editors.SectionOf(path));
        });
    }

    /// <summary>
    /// The blocker links a section the Case record has, and its resolution
    /// names that section. An unconfirmed value's resolution says "its Case
    /// section": the link is what names it.
    /// </summary>
    private static void AssertLinks(AssessmentReadinessItem item)
    {
        var key = CaseWorkspaceLabels.Report.BlockerSection(item);
        var section = OperatorLabels.CaseWorkspace.Sections.FirstOrDefault(candidate => candidate.Key == key);
        Assert.True(
            section is not null,
            $"'{item.Requirement}' links {key ?? "no section"}, which is not a section of the Case record.");
        if (item.Requirement.EndsWith(" awaits review", StringComparison.Ordinal))
        {
            return;
        }

        var label = section!.Label;
        Assert.True(
            item.HowToResolve.Contains($"the {label} section", StringComparison.Ordinal),
            $"'{item.Requirement}' links the {label} section, but its resolution reads: {item.HowToResolve}");
    }

    /// <summary>
    /// Inputs that trigger each kind of report blocker, the conditional ones
    /// included. They are triggers, not a copy of the requirement list: a
    /// new requirement is covered once any of them names it.
    /// </summary>
    private static IEnumerable<CaseReportReadinessInput> ReportBlockerTriggers()
    {
        var complete = Complete();
        yield return NothingElseRecorded(complete with { Fields = [], CaseOwned = NothingRecordedCaseOwned });

        foreach (var category in AssessmentVocabulary.Definitions[AssessmentVocabulary.SalvageCategory].Codes!
            .Where(code => !AssessmentReportContract.PrintsSalvageCategory(code)))
        {
            yield return NothingElseRecorded(With(
                complete,
                (AssessmentVocabulary.Outcome, "total_loss"),
                (AssessmentVocabulary.SalvageCategory, category)));
        }

        yield return NothingElseRecorded(With(
            complete,
            (AssessmentVocabulary.LegalStatus, "unroadworthy"),
            (AssessmentVocabulary.UnroadworthyReason, null)));
        yield return NothingElseRecorded(With(
            complete,
            (AssessmentVocabulary.Outcome, "contract_repair"),
            (AssessmentVocabulary.SettlementContractSum, null)));
        yield return NothingElseRecorded(complete with
        {
            CaseOwned = complete.CaseOwned with
            {
                InspectionMode = nameof(CaseInspectionMode.PhysicalAddress),
                InspectionAddress = null
            }
        });
        yield return NothingElseRecorded(With(
            complete,
            (AssessmentVocabulary.ReportDateOverride, "true"),
            (AssessmentVocabulary.ReportDate, null),
            (AssessmentVocabulary.ReportValuationCommentary, "true"),
            (AssessmentVocabulary.ReportValuationCommentaryText, null),
            (AssessmentVocabulary.ReportIncludeUnrelatedDamage, "true"),
            (AssessmentVocabulary.DamageUnrelated, null)));
        yield return NothingElseRecorded(complete with
        {
            Fields =
            [
                .. complete.Fields.Where(field => field.Path != AssessmentVocabulary.VehicleCondition),
                new AssessmentFieldValue(
                    AssessmentVocabulary.VehicleCondition, "good", ActorKind.Automation, "automation",
                    RecordedAtUtc, null, null)
            ]
        });
        yield return NothingElseRecorded(complete with
        {
            EstimateLines =
            [
                new CaseEstimateLineRecord(
                    Guid.NewGuid(), 3, "repair", null, "Nearside door", 2.5m, null, false, null, null,
                    null, null, null, ActorKind.Automation, "automation", RecordedAtUtc, null, null)
            ]
        });
        yield return NothingElseRecorded(complete) with
        {
            CurrentEstimate = new RepairSpecificationVersion(
                SpecificationId: Guid.NewGuid(),
                CaseId: complete.CaseId,
                Version: 1,
                State: RepairSpecificationState.Accepted,
                Source: new RepairSpecificationSource(RepairSpecificationSourceRoute.Manual, null, null, null),
                Lines: [],
                CalculationBasis: null,
                CreatedBy: "engineer-1",
                CreatedAtUtc: RecordedAtUtc,
                AcceptedBy: "engineer-1",
                AcceptedAtUtc: RecordedAtUtc,
                SupersedesSpecificationId: null,
                SupersessionReason: null,
                Details: new EstimateDetails("Repair spec", LabourRate: null, OtherCosts: null, VatPercent: 20m),
                IsCurrent: true)
        };
    }

    /// <summary>
    /// Report readiness over <paramref name="assessment"/> with nothing else
    /// recorded: no sign-off account, Current repair spec, adopted valuation
    /// or report image.
    /// </summary>
    private static CaseReportReadinessInput NothingElseRecorded(CaseAssessmentProjection assessment) => new(
        assessment,
        PersistedSignOffEngineerId: null,
        AssignedEngineerId: null,
        EligibleSignOffEngineers: [],
        CurrentEstimate: null,
        AppliedValuation: null,
        Preparations: [],
        ConfirmedImageSources: new Dictionary<Guid, DocumentVersion>());

    /// <summary>
    /// A Case in Report preparation with every assessment path confirmed by
    /// staff and every Case fact the report prints: the report names nothing.
    /// </summary>
    private static CaseAssessmentProjection Complete() => new(
        Guid.NewGuid(),
        "CE-100",
        1,
        CaseLifecycleState.ReportPreparation,
        null,
        [
            .. AssessmentVocabulary.Definitions.Values.Select(
                definition => Confirmed(definition.Path, CompleteValue(definition)))
        ],
        [],
        CompleteCaseOwned);

    private static string CompleteValue(AssessmentFieldDefinition definition) => definition.Path switch
    {
        AssessmentVocabulary.Outcome => "repairable",
        AssessmentVocabulary.LegalStatus => "roadworthy",
        AssessmentVocabulary.SalvageCategory => AssessmentReportContract.PrintableSalvageCategory,
        _ => definition.Type switch
        {
            AssessmentFieldType.Text => "value",
            AssessmentFieldType.Enumerated => definition.Codes![0],
            AssessmentFieldType.WholeNumber => "1",
            AssessmentFieldType.Money => "1.00",
            AssessmentFieldType.Flag => "false",
            AssessmentFieldType.Date => "2026-09-03",
            AssessmentFieldType.Json => "[]",
            _ => throw new ArgumentOutOfRangeException(
                nameof(definition), definition.Type, "A new field type needs a recorded value here.")
        }
    };

    /// <summary>The projection with each path recorded by staff, or removed where the value is null.</summary>
    private static CaseAssessmentProjection With(
        CaseAssessmentProjection projection, params (string Path, string? Value)[] changes) => projection with
        {
            Fields =
            [
                .. projection.Fields.Where(field => !changes.Any(change => change.Path == field.Path)),
                .. changes
                    .Where(change => change.Value is not null)
                    .Select(change => Confirmed(change.Path, change.Value!))
            ]
        };

    private static AssessmentFieldValue Confirmed(string path, string value) => new(
        path, value, ActorKind.Staff, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc);

    private static Exception? AutomationRefusal(string path) =>
        Record.Exception(() => AssessmentMcpTools.RequireGenericWrite(path));
}
