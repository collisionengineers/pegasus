using Pegasus.Core.Assessment;
using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Vehicle;

public sealed class VehicleLookupFillPolicyTests
{
    private static readonly DateTimeOffset RetrievedAtUtc = new(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void ALookupFillsOnlyWhatTheCaseDoesNotAlreadyHold(
        bool hasFact,
        bool hasConfirmed,
        bool expected) =>
        Assert.Equal(expected, VehicleLookupFillPolicy.Fills(hasFact, hasConfirmed));

    [Fact]
    public void TheModelComesFromDvsaBesideTheDvlaDescription()
    {
        // VES answers no model at all, so before the DVSA vehicle object was
        // read every production lookup held a null model.
        var merged = VehicleLookupFillPolicy.Merge(
            new("TOYOTA", null, 2007, 2362, "PETROL"),
            new("TOYOTA", "ALPHARD", 2007, null, null));

        Assert.Equal(new VehicleDetails("TOYOTA", "ALPHARD", 2007, 2362, "PETROL"), merged);
    }

    [Fact]
    public void DvsaAloneDescribesTheVehicle()
    {
        var merged = VehicleLookupFillPolicy.Merge(null, new("TOYOTA", "ALPHARD", 2007, 2362, "PETROL"));

        Assert.Equal(new VehicleDetails("TOYOTA", "ALPHARD", 2007, 2362, "PETROL"), merged);
    }

    [Fact]
    public void DvlaAloneDescribesTheVehicle()
    {
        var merged = VehicleLookupFillPolicy.Merge(new("FORD", null, 2020, 999, "PETROL"), null);

        Assert.Equal(new VehicleDetails("FORD", null, 2020, 999, "PETROL"), merged);
    }

    [Fact]
    public void NeitherProviderDescribingTheVehicleIsNoVehicle() =>
        Assert.Null(VehicleLookupFillPolicy.Merge(null, null));

    [Fact]
    public void ARecordThatDescribesNothingIsNoVehicle()
    {
        // An all-null merge is not evidence, and a blank member would fail
        // VehicleLookupResult.EnsureValidFor rather than read as absent.
        Assert.Null(VehicleLookupFillPolicy.Merge(
            new(null, null, null, null, null, null, null, null),
            new("  ", "", null, null, "\t", " ", "\t", 0, "  ", null)));

        // A colour alone is evidence of the vehicle.
        Assert.Equal(
            new VehicleDetails(null, null, null, null, null, Colour: "BLUE"),
            VehicleLookupFillPolicy.Merge(new(null, null, null, null, null, Colour: " BLUE "), null));
    }

    [Fact]
    public void BlankProviderMembersAreNormalisedToAbsent()
    {
        var merged = VehicleLookupFillPolicy.Merge(
            new(" ", null, 0, 0, ""),
            new("TOYOTA", " ALPHARD ", 2007, 2362, "PETROL"));

        Assert.Equal(new VehicleDetails("TOYOTA", "ALPHARD", 2007, 2362, "PETROL"), merged);
    }

    [Fact]
    public void DvlaAnswersFirstWhereTheProvidersDisagree()
    {
        var merged = VehicleLookupFillPolicy.Merge(
            new("FORD", null, 2020, 999, "PETROL"),
            new("TOYOTA", "ALPHARD", 2007, 2362, "DIESEL"));

        Assert.Equal(new VehicleDetails("FORD", "ALPHARD", 2020, 999, "PETROL"), merged);
    }

    [Fact]
    public void DvlaAnswersFirstForTheVehicleTypeSignals()
    {
        var merged = VehicleLookupFillPolicy.Merge(
            new("FORD", null, 2020, 999, "PETROL", "N1", "2 AXLE RIGID BODY", 3_500),
            new("TOYOTA", "ALPHARD", 2007, 2362, "DIESEL", "M1", "2-WHEEL", 1_800));

        Assert.Equal(
            new VehicleDetails(
                "FORD",
                "ALPHARD",
                2020,
                999,
                "PETROL",
                "N1",
                "2 AXLE RIGID BODY",
                3_500),
            merged);
    }

    [Fact]
    public void DvlaAnswersFirstForColourAndDvsaFillsIt()
    {
        var merged = VehicleLookupFillPolicy.Merge(
            new("FORD", null, 2020, 999, "PETROL", Colour: "BLUE", TaxDueDate: new(2027, 3, 1)),
            new("FORD", "FOCUS", 2020, 999, "PETROL", Colour: "Blue"));

        Assert.NotNull(merged);
        Assert.Equal("BLUE", merged.Colour);
        Assert.Equal(new DateOnly(2027, 3, 1), merged.TaxDueDate);

        // DVLA answering no colour leaves DVSA's primary colour.
        var dvsaColour = VehicleLookupFillPolicy.Merge(
            new("FORD", null, 2020, 999, "PETROL"),
            new("FORD", "FOCUS", 2020, 999, "PETROL", Colour: "Silver"));

        Assert.NotNull(dvsaColour);
        Assert.Equal("Silver", dvsaColour.Colour);
    }

    [Fact]
    public void ACompleteAnswerSetsEveryDerivedFact()
    {
        var writes = VehicleLookupFillPolicy.DerivedAssessmentWrites(Result(
            VehicleLookupOutcome.Current,
            new("FORD", "FOCUS", 2019, 1461, "DIESEL", Colour: "BLUE", TaxDueDate: new(2027, 3, 1)),
            [
                Test(new(2024, 9, 20), new(2025, 9, 24)),
                Test(new(2025, 9, 20), new(2026, 9, 24)),
            ],
            null));

        AssertWrites(
            writes,
            (AssessmentVocabulary.VehicleEngineCc, "1461"),
            (AssessmentVocabulary.VehicleFuel, "DIESEL"),
            (AssessmentVocabulary.VehicleColour, "BLUE"),
            (AssessmentVocabulary.VehicleTaxExpiry, "2027-03-01"),
            (AssessmentVocabulary.VehicleMotExpiry, "2026-09-24"));
    }

    [Fact]
    public void ACompleteAnswerClearsWhatItNoLongerCarries()
    {
        // An electric vehicle too new for an MOT has no engine capacity and no
        // MOT expiry: the earlier values are cleared, not kept.
        var writes = VehicleLookupFillPolicy.DerivedAssessmentWrites(Result(
            VehicleLookupOutcome.Current,
            new("TESLA", "MODEL 3", 2024, null, "ELECTRICITY", Colour: "WHITE", TaxDueDate: new(2027, 1, 1)),
            [],
            null));

        AssertWrites(
            writes,
            (AssessmentVocabulary.VehicleEngineCc, null),
            (AssessmentVocabulary.VehicleFuel, "ELECTRICITY"),
            (AssessmentVocabulary.VehicleColour, "WHITE"),
            (AssessmentVocabulary.VehicleTaxExpiry, "2027-01-01"),
            (AssessmentVocabulary.VehicleMotExpiry, null));
    }

    [Fact]
    public void BothProvidersNotFindingTheVehicleClearsEveryDerivedFact()
    {
        var writes = VehicleLookupFillPolicy.DerivedAssessmentWrites(Result(
            VehicleLookupOutcome.NotFound, null, [], null));

        AssertWrites(
            writes,
            (AssessmentVocabulary.VehicleEngineCc, null),
            (AssessmentVocabulary.VehicleFuel, null),
            (AssessmentVocabulary.VehicleColour, null),
            (AssessmentVocabulary.VehicleTaxExpiry, null),
            (AssessmentVocabulary.VehicleMotExpiry, null));
    }

    [Fact]
    public void APartialAnswerLeavesWhatItDoesNotCarry()
    {
        // DVLA failed, so DVSA's silence on the engine and the tax due date
        // leaves the earlier values standing.
        var writes = VehicleLookupFillPolicy.DerivedAssessmentWrites(Result(
            VehicleLookupOutcome.Partial,
            new("FORD", "FOCUS", 2019, null, "DIESEL", Colour: "Blue"),
            [Test(new(2025, 9, 20), new(2026, 9, 24))],
            new VehicleLookupFailure("dvla_not_found", Retryable: false)));

        AssertWrites(
            writes,
            (AssessmentVocabulary.VehicleFuel, "DIESEL"),
            (AssessmentVocabulary.VehicleColour, "Blue"),
            (AssessmentVocabulary.VehicleMotExpiry, "2026-09-24"));
    }

    [Fact]
    public void AFailedLookupWritesNothing() =>
        Assert.Empty(VehicleLookupFillPolicy.DerivedAssessmentWrites(Result(
            VehicleLookupOutcome.Throttled,
            null,
            [],
            new VehicleLookupFailure("dvla_throttled", Retryable: true, RetryAfter: TimeSpan.FromSeconds(30)))));

    [Fact]
    public void AValueTheVocabularyCannotHoldIsNotCarried()
    {
        var writes = VehicleLookupFillPolicy.DerivedAssessmentWrites(Result(
            VehicleLookupOutcome.Current,
            new("FORD", "FOCUS", 2019, 1461, new string('X', 41), Colour: "BLUE", TaxDueDate: DateOnly.MinValue),
            [],
            null));

        Assert.Null(writes[AssessmentVocabulary.VehicleFuel]);
        Assert.Null(writes[AssessmentVocabulary.VehicleTaxExpiry]);
        Assert.Equal("BLUE", writes[AssessmentVocabulary.VehicleColour]);
    }

    /// <summary>Exactly <paramref name="expected"/>: no other derived fact is written.</summary>
    private static void AssertWrites(
        IReadOnlyDictionary<string, string?> writes, params (string Path, string? Value)[] expected)
    {
        Assert.Equal(
            expected.Select(item => item.Path).Order(StringComparer.Ordinal).ToArray(),
            writes.Keys.Order(StringComparer.Ordinal).ToArray());
        foreach (var (path, value) in expected)
        {
            Assert.Equal(value, writes[path]);
        }
    }

    private static VehicleLookupResult Result(
        VehicleLookupOutcome outcome,
        VehicleDetails? vehicle,
        IReadOnlyList<MotTestObservation> tests,
        VehicleLookupFailure? failure) => new(
        "AB12CDE",
        outcome,
        "dvla-ves+dvsa-mot-history",
        "ves-1.2+mot-history-v1",
        "response",
        RetrievedAtUtc,
        null,
        vehicle is null && tests.Count == 0 ? null : RetrievedAtUtc,
        vehicle,
        tests,
        failure);

    private static MotTestObservation Test(DateOnly testDate, DateOnly expiryDate) =>
        new(testDate, "PASSED", expiryDate, 50_000, VehicleMileageUnit.Miles);
}
