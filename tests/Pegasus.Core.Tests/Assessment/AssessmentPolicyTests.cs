using System.Globalization;
using System.Reflection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Assessment;

public sealed class AssessmentPolicyTests
{
    [Fact]
    public void OriginalReportFieldsRequireAnAuditCase()
    {
        var paths = new[] { AssessmentVocabulary.OriginalReportAssessor };
        Assert.Throws<InvalidOperationException>(() =>
            AssessmentPolicy.RequireOriginalReportScope(paths, CaseType.Inspection));
        AssessmentPolicy.RequireOriginalReportScope(paths, CaseType.Audit);
        AssessmentPolicy.RequireOriginalReportScope(
            new[] { AssessmentVocabulary.Outcome }, CaseType.Inspection);
    }

    private static readonly ActionActor Automation = ActionActor.Automation("pegasus-automation");
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor PlainStaff =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    [InlineData(CaseLifecycleState.Held, false)]
    [InlineData(CaseLifecycleState.CreatedInError, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.PostReport, true)]
    [InlineData(CaseLifecycleState.PostReportComplete, true)]
    public void NativeAssessmentActionsRequireAWritableOrCompletedState(
        CaseLifecycleState state,
        bool expected)
    {
        var access = new AssessmentAccessState(state);

        Assert.Equal(expected, access.CanOpen);
    }

    [Theory]
    [InlineData(CaseLifecycleState.ReportPreparation, false)]
    [InlineData(CaseLifecycleState.PostReport, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, true)]
    [InlineData(CaseLifecycleState.NotReady, false)]
    [InlineData(CaseLifecycleState.Review, false)]
    [InlineData(CaseLifecycleState.Held, true)]
    [InlineData(CaseLifecycleState.CreatedInError, true)]
    public void AssessmentAccessIsReadOnlyOutsideWritableStates(
        CaseLifecycleState state,
        bool expected)
    {
        var access = new AssessmentAccessState(state);

        Assert.Equal(expected, access.IsReadOnly);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true, false)]
    [InlineData(CaseLifecycleState.Held, false, true)]
    [InlineData(CaseLifecycleState.Review, true, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, true, false)]
    [InlineData(CaseLifecycleState.PostReport, true, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, true, true)]
    [InlineData(CaseLifecycleState.Query, false, true)]
    [InlineData(CaseLifecycleState.ProviderCancelled, false, true)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected, false, true)]
    [InlineData(CaseLifecycleState.CreatedInError, false, true)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked, false, true)]
    public void AssessmentAccessPinsOpenAndReadOnlyPerLifecycleState(
        CaseLifecycleState state,
        bool expectedCanOpen,
        bool expectedIsReadOnly)
    {
        var access = new AssessmentAccessState(state);

        Assert.Equal(expectedCanOpen, access.CanOpen);
        Assert.Equal(expectedIsReadOnly, access.IsReadOnly);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.PostReport, true)]
    [InlineData(CaseLifecycleState.Held, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, false)]
    [InlineData(CaseLifecycleState.Query, false)]
    [InlineData(CaseLifecycleState.ProviderCancelled, false)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected, false)]
    [InlineData(CaseLifecycleState.CreatedInError, false)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked, false)]
    public void WorkspaceAssessmentWritesUseOnlyTheSupportedLifecycleStates(
        CaseLifecycleState state, bool expected)
    {
        Assert.Equal(expected, AssessmentPolicy.IsWritableState(state));
    }

    /// <summary>
    /// The table above is the owner of writability per lifecycle state, and web
    /// tests read it rather than re-rendering a page per state. That only holds
    /// if it names every state: Query was absent until this guard was written,
    /// so a state added to the enum now fails here instead of going unasserted.
    /// </summary>
    [Fact]
    public void TheWritableStateTableNamesEveryLifecycleState()
    {
        var method = typeof(AssessmentPolicyTests).GetMethod(
            nameof(WorkspaceAssessmentWritesUseOnlyTheSupportedLifecycleStates))!;
        var named = method.GetCustomAttributes<InlineDataAttribute>(inherit: false)
            .SelectMany(data => data.GetData(method))
            .Select(row => (CaseLifecycleState)row[0]!)
            .ToHashSet();

        var missing = Enum.GetValues<CaseLifecycleState>()
            .Where(state => !named.Contains(state))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"No writable-state row for: {string.Join(", ", missing)}.");
    }

    [Fact]
    public void SettlementCannotWriteASecondEstimateRepairDuration()
    {
        const string obsoletePath = "settlement.repair_duration";

        Assert.False(AssessmentVocabulary.Definitions.ContainsKey(obsoletePath));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.NormalizeWritableField(obsoletePath, "3", Automation));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(Request(new() { [obsoletePath] = "3" })));
    }

    [Fact]
    public void UnknownFieldPathFailsClosed()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["vehicle.not_a_field"] = "red" })));
        Assert.Contains("vehicle.not_a_field", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("vehicle.registration", "AB12CDE")]
    [InlineData("incident.assessed", "2026-08-03")]
    public void CaseOwnedPathFailsClosedNamingTheCaseDetailEditPath(string path, string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { [path] = value })));
        Assert.Contains("case-detail edit", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The report's assessed date is the Case's Inspection date, and the
    /// repairer's VAT position is the Current repair spec's, so neither keeps
    /// an assessment path of its own (#834).
    /// </summary>
    [Fact]
    public void TheRetiredAssessmentPathsAreNotVocabulary()
    {
        Assert.False(AssessmentVocabulary.Definitions.ContainsKey("incident.assessed"));
        Assert.False(AssessmentVocabulary.Definitions.ContainsKey("costs.repairer_vat_registered"));

        var exception = Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["costs.repairer_vat_registered"] = "true" })));
        Assert.Contains("not part of the assessment vocabulary", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryEnumeratedCodeFromTheScreenRoundTrips()
    {
        foreach (var definition in AssessmentVocabulary.Definitions.Values
            .Where(value => value.Type == AssessmentFieldType.Enumerated
                && !AssessmentVocabulary.DerivedPaths.Contains(value.Path)))
        {
            foreach (var code in definition.Codes!)
            {
                var normalized = AssessmentPolicy.ValidateAndNormalize(
                    Request(new() { [definition.Path] = code }, Engineer));
                Assert.Equal(code, normalized.Fields[definition.Path]);
            }

            Assert.ThrowsAny<ArgumentException>(() =>
                AssessmentPolicy.ValidateAndNormalize(
                    Request(new() { [definition.Path] = "unrecognized_code" }, Engineer)));
        }
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("12.345")]
    [InlineData("not-a-number")]
    public void MoneyValidationRefusesBadAmounts(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["costs.recovery_charge"] = value })));
    }

    [Fact]
    public void PositiveMoneyFieldsRefuseZero()
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { [AssessmentVocabulary.AgreedFee] = "0" }, Engineer)));
    }

    [Fact]
    public void MoneyIsCanonicalizedToTwoDecimalPlaces()
    {
        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.AgreedFee] = "12500.5" }, Engineer));
        Assert.Equal("12500.50", normalized.Fields[AssessmentVocabulary.AgreedFee]);
    }

    [Fact]
    public void FlagsAcceptOnlyTrueOrFalse()
    {
        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.SettlementClaimantVatRegistered] = "TRUE" }));
        Assert.Equal("true", normalized.Fields[AssessmentVocabulary.SettlementClaimantVatRegistered]);
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { [AssessmentVocabulary.SettlementClaimantVatRegistered] = "yes" })));
    }

    [Fact]
    public void DatesAreExactIsoDates()
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { [AssessmentVocabulary.SettlementHireStart] = "03/08/2026" })));
    }

    /// <summary>
    /// Only the DVLA/DVSA vehicle lookup records these facts (operator, 24
    /// September 2026), so every field save refuses one before its value is
    /// read, whoever saves it.
    /// </summary>
    [Theory]
    [InlineData(AssessmentVocabulary.VehicleEngineCc, "1461")]
    [InlineData(AssessmentVocabulary.VehicleFuel, "DIESEL")]
    [InlineData(AssessmentVocabulary.VehicleColour, "BLUE")]
    [InlineData(AssessmentVocabulary.VehicleTaxExpiry, "2027-03-01")]
    [InlineData(AssessmentVocabulary.VehicleMotExpiry, "2026-09-24")]
    public void LookupDerivedFactsAreRefusedOnEveryFieldSave(string path, string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssessmentPolicy.ValidateAndNormalize(Request(new() { [path] = value })));
        Assert.Contains("filled by the DVLA/DVSA vehicle lookup", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Paths that had no owner, or duplicated another owner's fact, are retired
    /// from the vocabulary (operator decision, 24 September 2026): no save
    /// records one.
    /// </summary>
    [Theory]
    [InlineData("narrative.nature_of_incident")]
    [InlineData("statement_of_truth")]
    [InlineData("vehicle.history_notes")]
    [InlineData("vehicle.vin_checked")]
    [InlineData("vehicle.fault_codes")]
    [InlineData("vehicle.modifications")]
    [InlineData("vehicle.engineer_notes")]
    public void AnOwnerlessOrDuplicateAssessmentPathIsRetired(string path)
    {
        Assert.False(AssessmentVocabulary.Definitions.ContainsKey(path));

        var exception = Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(Request(new() { [path] = "value" })));
        Assert.Contains("not part of the assessment vocabulary", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("th-TH")]
    [InlineData("ar-SA")]
    [InlineData("en-GB")]
    public void DateValuesUseTheGregorianCalendarRegardlessOfCurrentCulture(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            var dateFields = AssessmentVocabulary.Definitions.Values
                .Where(definition => definition.Type == AssessmentFieldType.Date
                    && !AssessmentVocabulary.DerivedPaths.Contains(definition.Path)
                    && !AssessmentVocabulary.AdoptedFindingPaths.Contains(definition.Path)
                    && !AssessmentVocabulary.LookupDerivedPaths.Contains(definition.Path))
                .ToArray();
            Assert.NotEmpty(dateFields);
            foreach (var definition in dateFields)
            {
                // Calendar probes, not new case evidence.
                foreach (var value in new[] { "2027-01-02", "2028-02-29" })
                {
                    var normalized = AssessmentPolicy.ValidateAndNormalize(
                        Request(new() { [definition.Path] = value }, Engineer));
                    Assert.Equal(value, normalized.Fields[definition.Path]);
                }

                foreach (var value in new[] { "2027-02-29", "02/01/2027", "0001-01-01" })
                {
                    Assert.Throws<ArgumentException>(() =>
                        AssessmentPolicy.ValidateAndNormalize(
                            Request(new() { [definition.Path] = value }, Engineer)));
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void EveryWritableVocabularyPathRoundTripsThroughItsCoreNormalizer()
    {
        foreach (var definition in AssessmentVocabulary.Definitions.Values
            .Where(definition => !AssessmentVocabulary.DerivedPaths.Contains(definition.Path)
                && !AssessmentVocabulary.AdoptedFindingPaths.Contains(definition.Path)
                && !AssessmentVocabulary.LookupDerivedPaths.Contains(definition.Path)))
        {
            var value = definition.Type switch
            {
                // A VIN is validated as ISO 3779 (Phase 5b), so it round-trips a real one.
                AssessmentFieldType.Text when definition.Path == AssessmentVocabulary.VehicleVin => "WVWZZZ1JZXW000001",
                AssessmentFieldType.Text => "value",
                AssessmentFieldType.Enumerated => definition.Codes![0],
                AssessmentFieldType.WholeNumber => "1",
                AssessmentFieldType.Money => "1.00",
                AssessmentFieldType.Flag => "true",
                AssessmentFieldType.Date => "2026-09-03",
                AssessmentFieldType.Json => "[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"value\"}]",
                _ => throw new ArgumentOutOfRangeException()
            };

            var normalized = AssessmentPolicy.ValidateAndNormalize(
                Request(new() { [definition.Path] = value }, Engineer));

            Assert.NotNull(normalized.Fields[definition.Path]);
        }
    }

    [Fact]
    public void DamageImpactsAreCanonicalAndDeriveHeadlineValues()
    {
        // The areas of a disc are written in the vocabulary's order, whatever
        // order the browser sent them in.
        const string json = "[ { \"note\": \" Bonnet \" , \"severity\": \"light\", \"areas\": [\"front\"] }, { \"areas\": [\"left_rear\", \"rear\"], \"severity\": \"heavy\", \"note\": \"Quarter\" } ]";

        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.DamageImpacts] = json }));

        Assert.Equal("[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"Bonnet\"},{\"areas\":[\"rear\",\"left_rear\"],\"severity\":\"heavy\",\"note\":\"Quarter\"}]", normalized.Fields[AssessmentVocabulary.DamageImpacts]);
        Assert.Equal(("multiple", "heavy"), AssessmentPolicy.DeriveImpactValues(normalized.Fields[AssessmentVocabulary.DamageImpacts]));
        Assert.Equal(("underside", "heavy"), AssessmentPolicy.DeriveImpactValues("[{\"areas\":[\"underside\"],\"severity\":\"heavy\",\"note\":\"\"}]"));
        // Two discs over the same area are one headline location.
        Assert.Equal(("rear", "moderate"), AssessmentPolicy.DeriveImpactValues("[{\"areas\":[\"rear\"],\"severity\":\"light\",\"note\":\"\"},{\"areas\":[\"rear\"],\"severity\":\"moderate\",\"note\":\"\"}]"));
    }

    [Fact]
    public void ADamageRecordedByAreaKeepsExactlyItsAreas()
    {
        // No disc: the areas are recorded as named, with nothing added around
        // them (the 20 September completion rule is retired).
        foreach (var json in new[]
        {
            "[{\"areas\":[\"front\",\"rear\"],\"severity\":\"light\",\"note\":\"\"}]",
            "[{\"areas\":[\"front\",\"left_front\",\"left_side\"],\"severity\":\"light\",\"note\":\"\"}]"
        })
        {
            var normalized = AssessmentPolicy.ValidateAndNormalize(
                Request(new() { [AssessmentVocabulary.DamageImpacts] = json }));

            Assert.Equal(json, normalized.Fields[AssessmentVocabulary.DamageImpacts]);
        }
    }

    [Fact]
    public void ADrawnDiscIsKeptAsDrawnAndNamesTheAreasItTouches()
    {
        // The operator's case (23 September 2026): a small disc on the centre
        // line stays small and names the two sides it touches, whatever areas
        // the browser sent with it; it never grows to cover the vehicle.
        const string centre =
            "[{\"areas\":[\"front\"],\"disc\":{\"x\":0.5,\"y\":0.53,\"r\":0.08},\"severity\":\"light\",\"note\":\"\"}]";

        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.DamageImpacts] = centre }));

        Assert.Equal(
            "[{\"areas\":[\"left_side\",\"right_side\"],\"disc\":{\"x\":0.5,\"y\":0.53,\"r\":0.08},\"severity\":\"light\",\"note\":\"\"}]",
            normalized.Fields[AssessmentVocabulary.DamageImpacts]);
        var impact = Assert.Single(AssessmentPolicy.ParseImpacts(normalized.Fields[AssessmentVocabulary.DamageImpacts]));
        Assert.Equal(new DamageDisc(0.5, 0.53, 0.08), impact.Disc);

        // A disc inside one area names that area alone; the stored disc keeps
        // four decimals.
        var inside = AssessmentPolicy.ParseImpacts(
            "[{\"areas\":[\"left_side\"],\"disc\":{\"x\":0.200004,\"y\":0.53,\"r\":0.08},\"severity\":\"light\",\"note\":\"\"}]");
        Assert.Equal(["left_side"], Assert.Single(inside).Areas);
        Assert.Equal(new DamageDisc(0.2, 0.53, 0.08), Assert.Single(inside).Disc);

        // The widest disc the plan allows is recorded as drawn.
        var widest = Assert.Single(AssessmentPolicy.ParseImpacts(
            "[{\"areas\":[\"front\"],\"disc\":{\"x\":0.5,\"y\":0.5,\"r\":0.5},\"severity\":\"heavy\",\"note\":\"\"}]"));
        Assert.Equal(DamageAreaGeometry.MaxRadius, widest.Disc!.Radius);
        Assert.Contains("left_side", widest.Areas);
        Assert.Contains("right_side", widest.Areas);
    }

    [Fact]
    public void PlanAreaTangencyIsNotPositiveCoverage()
    {
        var disc = new DamageDisc(
            DamageAreaGeometry.LeftBand + DamageAreaGeometry.BaseRadius,
            DamageAreaGeometry.FrontBand / 2,
            DamageAreaGeometry.BaseRadius);

        Assert.DoesNotContain(
            "left_front",
            DamageAreaGeometry.IntersectedPlanAreas(disc, 1, 1));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("[{\"areas\":[\"front\"],\"severity\":\"light\"}]")]
    [InlineData("[{\"areas\":[\"unknown\"],\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"severity\":\"unknown\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"x\",\"extra\":\"unexpected\"}]")]
    [InlineData("[{\"areas\":[],\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\",\"front\"],\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"underside\",\"front\"],\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"underside\"],\"severity\":\"light\",\"note\":\"x\"},{\"areas\":[\"underside\"],\"severity\":\"heavy\",\"note\":\"y\"}]")]
    [InlineData("[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"disc\":{\"x\":0.5,\"y\":0.1,\"r\":0.6},\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"disc\":{\"x\":0.5,\"y\":0.1,\"r\":0.01},\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"disc\":{\"x\":1.2,\"y\":0.1,\"r\":0.1},\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"disc\":{\"x\":0.5,\"y\":-0.1,\"r\":0.1},\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"underside\"],\"disc\":{\"x\":0.5,\"y\":0.5,\"r\":0.1},\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"disc\":{\"x\":0.5,\"y\":0.1,\"r\":0.1,\"z\":1},\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"disc\":{\"x\":\"0.5\",\"y\":0.1,\"r\":0.1},\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"disc\":[0.5,0.1,0.1],\"severity\":\"light\",\"note\":\"x\"}]")]
    public void DamageImpactsFailClosed(string json)
    {
        Assert.ThrowsAny<ArgumentException>(() => AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.DamageImpacts] = json })));
    }

    [Fact]
    public void DamageImpactNoteAndSerializedValueBoundsFailClosed()
    {
        var longNote = new string('x', 201);
        Assert.Throws<ArgumentException>(() => AssessmentPolicy.ValidateAndNormalize(Request(new()
        {
            [AssessmentVocabulary.DamageImpacts] = $"[{{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"{longNote}\"}}]"
        })));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssessmentPolicy.ValidateAndNormalize(Request(new()
        {
            [AssessmentVocabulary.DamageImpacts] = "[" + new string(' ', 4001) + "]"
        })));
    }

    [Theory]
    [InlineData(AssessmentVocabulary.ImpactLocation)]
    [InlineData(AssessmentVocabulary.ImpactSeverity)]
    public void DerivedImpactFieldsCannotBeWrittenDirectly(string path)
    {
        Assert.Throws<InvalidOperationException>(() => AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [path] = "front" })));
    }

    [Fact]
    public void AGenericFieldSaveNeverWritesOrClearsAnAdoptedValuationFinding()
    {
        // The Case Save's valuation adoption records the accepted Engineer's
        // Value together with the retail and trade of the basis card it was
        // calculated from (operator, 24 September 2026). A Web or MCP field
        // save that touched one would rewrite a professional finding apart
        // from the calculation that is its evidence, so both a value and a
        // clearance fail closed — for an Engineer too.
        string[] adopted =
        [
            AssessmentVocabulary.ValueRetail,
            AssessmentVocabulary.ValueTrade,
            AssessmentVocabulary.ValueEngineer
        ];
        Assert.Equal(
            adopted.Order(StringComparer.Ordinal),
            AssessmentVocabulary.AdoptedFindingPaths.Order(StringComparer.Ordinal));

        foreach (var path in adopted)
        {
            foreach (var actor in new[] { Engineer, Automation, PlainStaff })
            {
                foreach (var value in new string?[] { "4500.00", null })
                {
                    var exception = Assert.Throws<InvalidOperationException>(() =>
                        AssessmentPolicy.ValidateAndNormalize(
                            Request(new() { [path] = value }, actor)));
                    Assert.Contains(path, exception.Message, StringComparison.Ordinal);
                    Assert.Contains("adopts an Engineer's Value", exception.Message, StringComparison.Ordinal);
                }
            }
        }
    }

    [Fact]
    public void TheDamageAreasAreTheEightPlanAreasAndTheThreeOthers()
    {
        Assert.Equal(8, AssessmentVocabulary.DamagePlanAreas.Count);
        Assert.Equal(3, AssessmentVocabulary.DamageOtherAreas.Count);
        Assert.Equal(
            AssessmentVocabulary.DamagePlanAreas.Concat(AssessmentVocabulary.DamageOtherAreas).Order(StringComparer.Ordinal),
            AssessmentVocabulary.DamageAreas.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            [.. AssessmentVocabulary.DamagePlanAreas, .. AssessmentVocabulary.DamageOtherAreas, "multiple"],
            AssessmentVocabulary.Definitions[AssessmentVocabulary.ImpactLocation].Codes!);
        Assert.Equal("LH Rear", AssessmentVocabulary.DamageAreas["left_rear"]);
        Assert.True(AssessmentVocabulary.DamageAreaOrder("front") < AssessmentVocabulary.DamageAreaOrder("underside"));
        Assert.Equal(AssessmentVocabulary.DamagePlanAreas.Count + 3, AssessmentVocabulary.DamageAreaOrder("unknown"));

        // Every plan area has a centre on the unit plan.
        Assert.Equal(AssessmentVocabulary.DamagePlanAreas.Order(StringComparer.Ordinal), DamageAreaGeometry.Centres.Keys.Order(StringComparer.Ordinal));

        // A one-area disc sits on the area's centre; a wider disc grows to
        // reach every area it names; the other areas draw nothing.
        var one = DamageAreaGeometry.RenderDisc(["front"], 100, 200)!;
        Assert.Equal((50d, 20d, 12d), (one.CentreX, one.CentreY, one.Radius));
        var two = DamageAreaGeometry.RenderDisc(["rear", "left_rear"], 100, 200)!;
        Assert.True(two.Radius > one.Radius);
        Assert.True(two.CentreX < 50);
        Assert.Null(DamageAreaGeometry.RenderDisc(["underside"], 100, 200));
    }

    [Fact]
    public void AreaDiscsReachTheirAreasAndAreNeverWiderThanTheVehicle()
    {
        // On the workspace plan (the canonical shape) a disc drawn for two
        // neighbouring areas reaches both of their centres.
        const double width = 156;
        const double height = 394;
        Assert.Equal(height / width, DamageAreaGeometry.CanonicalHeight / DamageAreaGeometry.CanonicalWidth, 10);
        foreach (var areas in new[]
        {
            new[] { "front", "left_front" },
            new[] { "rear", "left_rear" },
            new[] { "left_side", "left_rear" },
            new[] { "right_front", "right_side" }
        })
        {
            var disc = DamageAreaGeometry.RenderDisc(areas, width, height)!;
            foreach (var area in areas)
            {
                var centre = DamageAreaGeometry.Centres[area];
                Assert.True(
                    Math.Sqrt(Math.Pow(centre.X * width - disc.CentreX, 2) + Math.Pow(centre.Y * height - disc.CentreY, 2)) <= disc.Radius,
                    $"The rendered disc for {string.Join('+', areas)} does not reach {area}.");
            }
        }

        // However many areas a damage names, its disc is never wider than
        // the vehicle, at the workspace's scale or the report's.
        foreach (var (renderWidth, renderHeight) in new[] { (156d, 394d), (124d, 364d) })
        {
            var all = DamageAreaGeometry.RenderDisc(AssessmentVocabulary.DamagePlanAreas, renderWidth, renderHeight)!;
            Assert.True(all.Radius <= DamageAreaGeometry.MaxRadius * renderWidth + 1e-9);
        }

        // A drawn disc renders as drawn, its radius scaled by the width.
        var drawn = DamageAreaGeometry.RenderDisc(["left_side", "right_side"], 100, 200, new DamageDisc(0.5, 0.53, 0.08))!;
        Assert.Equal(50d, drawn.CentreX, 9);
        Assert.Equal(106d, drawn.CentreY, 9);
        Assert.Equal(8d, drawn.Radius, 9);
    }

    [Fact]
    public void OverlappingDiscsAreIndependentEntries()
    {
        // Two discs may cover the same area: each stays its own damage with
        // its own severity and note, and the headline location reads the
        // distinct areas across them.
        const string json =
            "[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"Small\"},"
            + "{\"areas\":[\"front\",\"right_front\"],\"severity\":\"heavy\",\"note\":\"Wide\"}]";

        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.DamageImpacts] = json }));

        Assert.Equal(json, normalized.Fields[AssessmentVocabulary.DamageImpacts]);
        Assert.Equal(
            ("multiple", "heavy"),
            AssessmentPolicy.DeriveImpactValues(normalized.Fields[AssessmentVocabulary.DamageImpacts]));

        var alone = AssessmentPolicy.DeriveImpactValues(
            "[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"Small\"}]");
        Assert.Equal(("front", "light"), alone);
    }

    [Fact]
    public void PostReviewReadinessNoLongerAsksForTheRetiredEngineerIdentityFields()
    {
        // Operator decision D18: the signing Engineer is the selected sign-off
        // account, so typed copies of that account's name, qualifications and
        // signature are no longer readiness items.
        var readiness = AssessmentPolicy.EvaluatePostReviewReadiness(Projection([]));
        var requirements = readiness.Select(item => item.Requirement).ToArray();

        Assert.DoesNotContain("Engineer name", requirements);
        Assert.DoesNotContain("Engineer qualifications", requirements);
        Assert.DoesNotContain("Signature", requirements);
        Assert.Contains("Agreed fee", requirements);
    }

    [Fact]
    public void SaveBoundCoversTheWholeVocabulary()
    {
        Assert.True(AssessmentPolicy.MaximumFieldsPerSave >= AssessmentVocabulary.Definitions.Count);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(ActorKind.Automation, true)]
    [InlineData(ActorKind.Staff, false)]
    public void AFillLandsOnlyWhereStaffHaveNotRecordedAValue(ActorKind? recordedByKind, bool lands) =>
        Assert.Equal(lands, AssessmentPolicy.FillLands(recordedByKind));

    [Fact]
    public void AutomationCannotRecordAFindingField()
    {
        // A professional finding is recorded only by staff: the Automation
        // actor never records one, so no AI value can be a finding.
        var refused = Assert.Throws<InvalidOperationException>(() => AssessmentPolicy.ValidateAndNormalize(
            Request(new() { ["assessment.legal_status"] = "roadworthy" })));
        Assert.Contains("professional finding", refused.Message, StringComparison.Ordinal);

        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { ["vehicle.condition"] = "good" }));
        Assert.Equal("good", normalized.Fields["vehicle.condition"]);
    }

    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public void EveryStaffRoleMayRecordEveryWritableFindingField(StaffRole role)
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [role]);
        foreach (var definition in AssessmentVocabulary.Definitions.Values.Where(
            definition => definition.IsFinding
                && !AssessmentVocabulary.DerivedPaths.Contains(definition.Path)
                && !AssessmentVocabulary.AdoptedFindingPaths.Contains(definition.Path)))
        {
            var value = FindingValue(definition);
            var normalized = AssessmentPolicy.ValidateAndNormalize(
                Request(new() { [definition.Path] = value }, actor));

            Assert.Equal(value, normalized.Fields[definition.Path]);
        }
    }

    [Fact]
    public void NonEngineerStaffMayRecordOrdinaryFields()
    {
        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { ["vehicle.condition"] = "good" }, PlainStaff));
        Assert.Equal("good", normalized.Fields["vehicle.condition"]);
    }

    [Fact]
    public void SystemWorkerActorsAreRefused()
    {
        // The typed refusal Core actually gives a non Staff/Automation actor.
        // ThrowsAny would also have accepted a null reference or any unrelated
        // fault. (The audit expected InvalidOperationException here; the real
        // type is the domain authorization one, which is a stronger contract.)
        Assert.Throws<StaffAuthorizationException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(
                    new() { ["vehicle.condition"] = "good" },
                    ActionActor.SystemWorker("worker"))));
    }

    [Fact]
    public void UnroadworthyRequiresAReasonInTheMergedState()
    {
        var saved = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["assessment.legal_status"] = "unroadworthy"
        };
        Assert.Throws<InvalidOperationException>(() =>
            AssessmentPolicy.ValidateMergedState(
                saved,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["assessment.legal_status"] = "unroadworthy"
                }));
        AssessmentPolicy.ValidateMergedState(
            saved,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assessment.legal_status"] = "unroadworthy",
                ["assessment.unroadworthy_reason"] = "Suspension damage"
            });
    }

    [Fact]
    public void TotalLossRequiresCategoryAndSalvageValueInTheMergedState()
    {
        var saved = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["assessment.outcome"] = "total_loss"
        };
        Assert.Throws<InvalidOperationException>(() =>
            AssessmentPolicy.ValidateMergedState(
                saved,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["assessment.outcome"] = "total_loss",
                    ["assessment.category"] = "S"
                }));
        AssessmentPolicy.ValidateMergedState(
            saved,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assessment.outcome"] = "total_loss",
                ["assessment.category"] = "S",
                ["assessment.salvage_value"] = "1500.00"
            });
    }

    [Fact]
    public void ConfirmedPositiveSumEstablishesContractRepair()
    {
        var writes = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.Outcome] = "repairable",
            [AssessmentVocabulary.SettlementContractSum] = "4500.00"
        };
        AssessmentPolicy.CompleteCoupledWrites(writes,
            new Dictionary<string, string>(StringComparer.Ordinal), ActorKind.Staff);

        Assert.Equal("contract_repair", writes[AssessmentVocabulary.Outcome]);
        AssessmentPolicy.ValidateMergedState(writes,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.Outcome] = "contract_repair",
                [AssessmentVocabulary.SettlementContractSum] = "4500.00"
            });
    }

    [Fact]
    public void LeavingContractRepairClearsTheSumAndInapplicableSalvage()
    {
        var current = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.Outcome] = "contract_repair",
            [AssessmentVocabulary.SettlementContractSum] = "4500.00",
            [AssessmentVocabulary.SalvageValue] = "500.00",
            [AssessmentVocabulary.SettlementSalvageAgent] = "Old agent"
        };
        var writes = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.Outcome] = "repairable",
            [AssessmentVocabulary.SettlementContractSum] = "4500.00"
        };
        AssessmentPolicy.CompleteCoupledWrites(writes, current, ActorKind.Staff);

        Assert.Null(writes[AssessmentVocabulary.SettlementContractSum]);
        Assert.Null(writes[AssessmentVocabulary.SalvageValue]);
        Assert.Null(writes[AssessmentVocabulary.SettlementSalvageAgent]);
    }

    [Fact]
    public void EmptyContractRepairSumIsRefusedAndAutomationDoesNotEstablishTheOutcome()
    {
        var writes = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.SettlementContractSum] = "4500.00"
        };
        AssessmentPolicy.CompleteCoupledWrites(writes,
            new Dictionary<string, string>(StringComparer.Ordinal), ActorKind.Automation);
        Assert.False(writes.ContainsKey(AssessmentVocabulary.Outcome));

        Assert.Throws<InvalidOperationException>(() => AssessmentPolicy.ValidateMergedState(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.Outcome] = "contract_repair"
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.Outcome] = "contract_repair"
            }));
    }

    [Fact]
    public void EstimateLinesValidateTypePrecisionAndUnpricedRules()
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.NormalizeRepairSpecificationLines([Line("unknown_type")]));
        // Hours are kept at the provider's own precision (B04): a quarter of
        // an hour is a real time, a seventh decimal place is not.
        AssessmentPolicy.NormalizeRepairSpecificationLines([Line("repair") with { WorkUnits = 1.25m }]);
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.NormalizeRepairSpecificationLines([Line("repair") with { WorkUnits = 1.2345678m }]));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.NormalizeRepairSpecificationLines([Line("new_part") with { Unpriced = true, Price = 10m }]));

        var normalized = AssessmentPolicy.NormalizeRepairSpecificationLines(
            [
                Line("repair") with { WorkUnits = 2.5m },
                Line("new_part") with { Price = 120.50m, EvidenceLabel = "official" }
            ]);
        Assert.Equal(2, normalized.Count);
    }

    [Fact]
    public void AnEmptySaveIsRefused()
    {
        // An assessment save carries fields only; estimate lines change
        // through the repair spec commands.
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(Request(new())));
    }

    [Fact]
    public void ReadinessNamesMissingRequirementsAndHonoursTheTbcMileageRule()
    {
        var empty = Projection([]);
        var readiness = AssessmentPolicy.EvaluateReadiness(empty);
        Assert.Contains(readiness, item => item.Requirement == "Vehicle type");
        // The repairer's VAT position is the Current repair spec's, and the
        // assessed date is the Case's Inspection date (#834).
        Assert.DoesNotContain(readiness, item => item.Requirement == "Repairer VAT answer");
        Assert.DoesNotContain(readiness, item => item.Requirement == "Assessed date");
        Assert.Contains(
            readiness,
            item => item.Requirement == "Inspection date"
                && item.Source == "Case record"
                && item.Field == CaseDataFieldNames.InspectionDate);

        // The year is the case record's own fact now, named beside the make
        // and the model rather than asked for again on the Vehicle section.
        Assert.Contains(
            readiness,
            item => item.Requirement == "Vehicle year" && item.Source == "Case record");
        Assert.DoesNotContain(readiness, item => item.Requirement == "Mileage source");

        // The report's mileage source is derived from the mileage's own
        // provenance, so a case with no mileage reads To be confirmed and the
        // odometer requirement is waived exactly as it was when staff typed it.
        Assert.DoesNotContain(readiness, item => item.Requirement == "Odometer reading");

        var recorded = Projection(
            [],
            new(null, null, null, "2012", 80_000, "miles", "owner", null, ReceivedOn, null, null, null, null, null));
        Assert.DoesNotContain(
            AssessmentPolicy.EvaluateReadiness(recorded),
            item => item.Requirement is "Odometer reading" or "Vehicle year");
    }

    [Fact]
    public void ContractRepairRequiresAnAgreedSum()
    {
        var missing = Projection([Field(AssessmentVocabulary.Outcome, "contract_repair")]);
        Assert.Contains(
            AssessmentPolicy.EvaluatePostReviewReadiness(missing),
            item => item.Requirement == "Agreed contract sum");

        var recorded = Projection(
            [
                Field(AssessmentVocabulary.Outcome, "contract_repair"),
                Field(AssessmentVocabulary.SettlementContractSum, "4500.00"),
            ]);
        Assert.DoesNotContain(
            AssessmentPolicy.EvaluatePostReviewReadiness(recorded),
            item => item.Requirement == "Agreed contract sum");
    }

    /// <summary>
    /// A recorded value is the Case's value whoever recorded it (operator, 25
    /// September 2026): there is no per-field review, so a value the
    /// Automation actor, the vehicle lookup or the original-report extraction
    /// recorded is never named as a blocker because of who recorded it.
    /// </summary>
    [Fact]
    public void ARecordedValueBlocksNothingWhoeverRecordedIt()
    {
        AssessmentFieldValue Recorded(string path, string value, string recorder) =>
            Field(path, value) with { RecordedByKind = ActorKind.Automation, RecordedBy = recorder };
        var projection = Projection(
        [
            Recorded(AssessmentVocabulary.VehicleCondition, "good", "pegasus-automation"),
            Recorded(AssessmentVocabulary.VehicleType, "car", "vehicle-lookup"),
            Recorded(AssessmentVocabulary.OriginalReportDate, "2026-09-01", "original-report-extraction"),
            Recorded(AssessmentVocabulary.OriginalReportAssessor, "A N Other", "original-report-extraction")
        ]);

        var readiness = AssessmentPolicy.EvaluateReadiness(projection);

        Assert.DoesNotContain(readiness, item => item.Requirement.Contains("review", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(readiness, item => item.Source.StartsWith("Recorded by", StringComparison.Ordinal));
        foreach (var field in projection.Fields)
        {
            Assert.DoesNotContain(readiness, item => item.Field == field.Path);
        }
    }

    /// <summary>
    /// Entry to Review proves only instruction and image completeness, so each
    /// Case fact the report prints is a post-Review blocker naming its Case
    /// field and the section that records it (operator, 24 September 2026).
    /// The date instructions were received is the Case's received date, which
    /// every Case has, so it is never one of them.
    /// </summary>
    [Theory]
    [InlineData(CaseDataFieldNames.ClaimantName, "Claimant name", "the Claim section")]
    [InlineData(CaseDataFieldNames.ClaimNumber, "Claim reference", "the Case details section")]
    [InlineData(CaseDataFieldNames.IncidentDate, "Incident date", "the Case details section")]
    [InlineData(CaseDataFieldNames.VehicleRegistration, "Vehicle registration", "the Vehicle section")]
    [InlineData(CaseDataFieldNames.InspectionMode, "Inspection type", "the Inspection details section")]
    [InlineData(CaseDataFieldNames.InspectionDate, "Inspection date", "the Inspection details section")]
    [InlineData(CaseDataFieldNames.InspectionAddress, "Inspection address", "the Inspection details section")]
    public void PostReviewReadinessNamesEachCaseFactTheReportPrints(
        string field, string requirement, string section)
    {
        Assert.DoesNotContain(
            AssessmentPolicy.EvaluatePostReviewReadiness(Projection([], CompleteCaseOwned)),
            item => item.Source == "Case record");

        var readiness = AssessmentPolicy.EvaluatePostReviewReadiness(
            Projection([], WithoutCaseFact(CompleteCaseOwned, field)));

        var named = Assert.Single(readiness, item => item.Source == "Case record");
        Assert.Equal(requirement, named.Requirement);
        Assert.Equal(field, named.Field);
        Assert.Contains(section, named.HowToResolve, StringComparison.Ordinal);

        // The vehicle make, model and year are Review-entry facts only.
        var noVehicleIdentity = Projection(
            [],
            CompleteCaseOwned with { Make = null, Model = null, Year = null });
        string?[] reviewEntryFacts =
        [
            CaseDataFieldNames.VehicleMake,
            CaseDataFieldNames.VehicleModel,
            CaseDataFieldNames.VehicleYear
        ];
        Assert.DoesNotContain(
            AssessmentPolicy.EvaluatePostReviewReadiness(noVehicleIdentity),
            item => item.Source == "Case record");
        Assert.Equal(
            reviewEntryFacts,
            AssessmentPolicy.EvaluateReadiness(noVehicleIdentity)
                .Where(item => item.Source == "Case record")
                .Select(item => item.Field));
    }

    /// <summary>
    /// The active total-loss template has accepted wording for Category S
    /// only (operator, 24 September 2026), so any other category is named
    /// before a report is projected rather than refused at render.
    /// </summary>
    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("N")]
    [InlineData("N/A")]
    public void ATotalLossPrintsOnlyCategoryS(string category)
    {
        var readiness = AssessmentPolicy.EvaluatePostReviewReadiness(Projection(TotalLoss(category)));

        var salvage = Assert.Single(readiness, item => item.Requirement == "Salvage category");
        Assert.Contains("Category S", salvage.WhyOutstanding, StringComparison.Ordinal);
        Assert.Contains($"Category {category}", salvage.WhyOutstanding, StringComparison.Ordinal);
        Assert.Equal(AssessmentVocabulary.SalvageCategory, salvage.Field);

        Assert.DoesNotContain(
            AssessmentPolicy.EvaluatePostReviewReadiness(Projection(TotalLoss("S"))),
            item => item.Requirement == "Salvage category");
    }

    [Fact]
    public void AnOverriddenReportDateWithoutADateIsNamed()
    {
        var overridden = AssessmentPolicy.EvaluatePostReviewReadiness(
            Projection([Field(AssessmentVocabulary.ReportDateOverride, "true")]));

        var reportDate = Assert.Single(overridden, item => item.Field == AssessmentVocabulary.ReportDate);
        Assert.Equal("Report date", reportDate.Requirement);

        var dated = AssessmentPolicy.EvaluatePostReviewReadiness(Projection(
        [
            Field(AssessmentVocabulary.ReportDateOverride, "true"),
            Field(AssessmentVocabulary.ReportDate, "2026-08-19")
        ]));
        var notOverridden = AssessmentPolicy.EvaluatePostReviewReadiness(
            Projection([Field(AssessmentVocabulary.ReportDateOverride, "false")]));
        Assert.DoesNotContain(dated, item => item.Requirement == "Report date");
        Assert.DoesNotContain(notOverridden, item => item.Requirement == "Report date");
    }

    /// <summary>
    /// The report's retail and trade are the Engineer's Value basis card's,
    /// recorded by the adoption, so a missing trade is cleared on that card
    /// and a Case save, never by a field of its own.
    /// </summary>
    [Fact]
    public void TradeValueBlockerPointsAtTheBasisCard()
    {
        var withoutTrade = AssessmentPolicy.EvaluatePostReviewReadiness(Projection(
        [
            Field(AssessmentVocabulary.ValueEngineer, "5000.00"),
            Field(AssessmentVocabulary.ValueRetail, "5000.00")
        ]));

        var trade = Assert.Single(withoutTrade, item => item.Requirement == "Trade value");
        Assert.Equal("Valuation", trade.Source);
        Assert.Equal(AssessmentVocabulary.ValueTrade, trade.Field);
        Assert.Contains("basis card", trade.HowToResolve, StringComparison.Ordinal);
        Assert.Contains("the Valuation section", trade.HowToResolve, StringComparison.Ordinal);
        Assert.DoesNotContain(withoutTrade, item => item.Field == AssessmentVocabulary.ValueRetail);

        var adopted = AssessmentPolicy.EvaluatePostReviewReadiness(Projection(
        [
            Field(AssessmentVocabulary.ValueEngineer, "5000.00"),
            Field(AssessmentVocabulary.ValueRetail, "5000.00"),
            Field(AssessmentVocabulary.ValueTrade, "4000.00")
        ]));
        Assert.DoesNotContain(
            adopted,
            item => item.Field is AssessmentVocabulary.ValueRetail or AssessmentVocabulary.ValueTrade);
    }

    /// <summary>
    /// Before any adoption the Engineer's Value item names the one Save that
    /// records the value, retail and trade together, so retail and trade are
    /// not named as blockers of their own.
    /// </summary>
    [Fact]
    public void BeforeAnAdoptionOnlyTheEngineersValueIsNamed()
    {
        var readiness = AssessmentPolicy.EvaluatePostReviewReadiness(Projection([]));

        var engineerValue = Assert.Single(readiness, item => item.Field == AssessmentVocabulary.ValueEngineer);
        Assert.Equal("Engineer's Value", engineerValue.Requirement);
        Assert.DoesNotContain(
            readiness,
            item => item.Field is AssessmentVocabulary.ValueRetail or AssessmentVocabulary.ValueTrade);
    }

    /// <summary>
    /// FRD-13: a blocker identifies exactly which field or material it names,
    /// so the Case page can send the operator to the section that clears it
    /// without reading requirement text.
    /// </summary>
    [Fact]
    public void EveryReadinessItemNamesItsField()
    {
        var projection = Projection(
            [Field(AssessmentVocabulary.LegalStatus, "unroadworthy")],
            NoCaseFacts with { InspectionMode = nameof(CaseInspectionMode.PhysicalAddress) });

        var readiness = AssessmentPolicy.EvaluateReadiness(projection);

        Assert.All(readiness, item => Assert.True(
            item.Field is not null,
            $"'{item.Requirement}' names no field."));
        Assert.Equal(
            CaseDataFieldNames.VehicleRegistration,
            Assert.Single(readiness, item => item.Requirement == "Vehicle registration").Field);
        Assert.Equal(
            AssessmentVocabulary.UnroadworthyReason,
            Assert.Single(readiness, item => item.Requirement == "Unroadworthy reason").Field);
    }

    /// <summary>
    /// Each resolution names a section the Case page has. The retired
    /// Assessment page, its Apply command and the old section names are gone
    /// from every blocker, conditional ones included.
    /// </summary>
    [Fact]
    public void ReadinessResolutionsNameTheLiveCaseSections()
    {
        CaseAssessmentProjection[] projections =
        [
            Projection([], NoCaseFacts with { InspectionMode = nameof(CaseInspectionMode.PhysicalAddress) }),
            Projection([Field(AssessmentVocabulary.LegalStatus, "unroadworthy")]),
            Projection([Field(AssessmentVocabulary.Outcome, "total_loss")]),
            Projection([Field(AssessmentVocabulary.Outcome, "contract_repair")]),
        ];
        var items = projections.SelectMany(AssessmentPolicy.EvaluateReadiness).ToArray();

        string HowToResolve(string requirement) =>
            items.First(item => item.Requirement == requirement).HowToResolve;

        Assert.Equal("Record it on the Vehicle section.", HowToResolve("Vehicle type"));
        Assert.Equal("Record it on the Fee tab of the Report section.", HowToResolve("Agreed fee"));
        Assert.Equal("Record it on the Inspection details section.", HowToResolve("Inspection date"));

        // Every conditional blocker was reached, so the wording check below
        // covers it.
        Assert.Contains(items, item => item.Requirement == "Inspection address");
        Assert.Contains(items, item => item.Requirement == "Unroadworthy reason");
        Assert.Contains(items, item => item.Requirement == "Salvage category");
        Assert.Contains(items, item => item.Requirement == "Agreed contract sum");

        foreach (var retired in new[]
        {
            "Assessment page", "Apply", "EXT-09", "assigned staff member", "Findings section",
            "Settlement section", "Incident and impact", "Report content", "Estimate section", "case details"
        })
        {
            Assert.DoesNotContain(
                items,
                item => item.HowToResolve.Contains(retired, StringComparison.Ordinal)
                    || item.WhyOutstanding.Contains(retired, StringComparison.Ordinal));
        }
    }

    private static SaveAssessmentRequest Request(
        Dictionary<string, string?>? fields = null,
        ActionActor? actor = null) => new(
        Guid.NewGuid(),
        0,
        actor ?? Automation,
        "mcp:test-operation",
        "Test save",
        "lease-token",
        fields ?? new Dictionary<string, string?>(StringComparer.Ordinal));

    private static EstimateLineInput Line(string type) =>
        new(type, null, "Test line", null, null, false, null, null, null, null);

    private static string FindingValue(AssessmentFieldDefinition definition) => definition.Type switch
    {
        AssessmentFieldType.Text => "value",
        AssessmentFieldType.Enumerated => definition.Codes![0],
        AssessmentFieldType.WholeNumber => "1",
        AssessmentFieldType.Money => "1.00",
        AssessmentFieldType.Flag => "true",
        AssessmentFieldType.Date => "2026-09-03",
        AssessmentFieldType.Json => "[]",
        _ => throw new ArgumentOutOfRangeException(nameof(definition))
    };

    private static AssessmentFieldValue Field(string path, string value) => new(
        path,
        value,
        ActorKind.Staff,
        "staff",
        DateTimeOffset.UtcNow);

    private static AssessmentFieldValue[] TotalLoss(string category) =>
    [
        Field(AssessmentVocabulary.Outcome, "total_loss"),
        Field(AssessmentVocabulary.SalvageCategory, category),
        Field(AssessmentVocabulary.SalvageValue, "500.00")
    ];

    /// <summary>The Case's received date, which every Case has.</summary>
    private static readonly DateOnly ReceivedOn = new(2026, 8, 2);

    /// <summary>A Case record with nothing recorded beyond its received date.</summary>
    private static readonly AssessmentCaseOwnedData NoCaseFacts =
        new(null, null, null, null, null, null, "tbc", null, ReceivedOn, null, null, null, null, null);

    /// <summary>A Case record holding every fact the report prints.</summary>
    private static readonly AssessmentCaseOwnedData CompleteCaseOwned = new(
        "AB12CDE", "Ford", "Focus", "2012", 80_000, "miles", "owner",
        new DateOnly(2026, 8, 1), ReceivedOn, "ImageBasedAssessment", "Image Based Assessment",
        new DateOnly(2026, 8, 3), "Alex Example", "P-100");

    /// <summary>
    /// <paramref name="owned"/> without the one printed Case fact named by its
    /// Case field. An address is printed only for a vehicle inspected at a
    /// physical location, so the address is removed from such an inspection.
    /// </summary>
    private static AssessmentCaseOwnedData WithoutCaseFact(AssessmentCaseOwnedData owned, string field) => field switch
    {
        CaseDataFieldNames.ClaimantName => owned with { ClaimantName = null },
        CaseDataFieldNames.ClaimNumber => owned with { ClaimNumber = null },
        CaseDataFieldNames.IncidentDate => owned with { IncidentDate = null },
        CaseDataFieldNames.VehicleRegistration => owned with { Registration = null },
        CaseDataFieldNames.InspectionMode => owned with { InspectionMode = null },
        CaseDataFieldNames.InspectionDate => owned with { InspectionDate = null },
        CaseDataFieldNames.InspectionAddress => owned with
        {
            InspectionMode = nameof(CaseInspectionMode.PhysicalAddress),
            InspectionAddress = null
        },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Not a Case fact the report prints.")
    };

    private static CaseAssessmentProjection Projection(
        IReadOnlyList<AssessmentFieldValue> fields,
        AssessmentCaseOwnedData? caseOwned = null,
        IReadOnlyList<CaseEstimateLineRecord>? lines = null) => new(
        Guid.NewGuid(),
        "CE-QDOS-31-00001",
        0,
        CaseLifecycleState.Review,
        null,
        fields,
        lines ?? [],
        caseOwned ?? NoCaseFacts);
}
