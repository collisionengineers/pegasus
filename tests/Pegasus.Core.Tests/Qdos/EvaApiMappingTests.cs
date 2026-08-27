using Pegasus.Core.Eva;

namespace Pegasus.Core.Tests.Qdos;

/// <summary>
/// EXT-04. The API mapping renames the export's settled values into EVA's
/// field names; it must not restate them. These tests pin the rename, the
/// values EVA has no field for, and the address split.
/// </summary>
public sealed class EvaApiMappingTests
{
    private static readonly EvaInstructionSettings Settings = new(
        "COLLENGAPI",
        "Vehicle Damage Inspection",
        "digital@collisionengineers.co.uk");

    [Fact]
    public void TheExportsValuesTravelUnderEvasFieldNames()
    {
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", "QDOS", Settings, []);

        Assert.Equal("COLLENGAPI", payload.RequestFrom);
        Assert.Equal("QDOS26031", payload.ExternalRef);
        Assert.Equal("AKH/47743/1", payload.ClaimNumber);
        Assert.Equal("A Smith", payload.ClaimantName);
        Assert.Equal("QDOS", payload.Agent);
        Assert.Equal("MT15OYK", payload.VehicleRegistration);
        Assert.Equal("Land Rover Defender 110", payload.VehicleDescription);
        Assert.Equal(new DateOnly(2026, 1, 31), payload.IncidentDate);
        Assert.Equal("Rear-end collision at traffic lights.", payload.Cause);
        Assert.Equal("20%", payload.VatStatus);
        Assert.Equal("Vehicle Damage Inspection", payload.InspectionType);
        Assert.Equal("digital@collisionengineers.co.uk", payload.InstructionEmail);
    }

    /// <summary>
    /// The Pegasus case reference is the ExternalRef, deliberately not the
    /// Reference field — which since ENG-015 carries the work provider's own
    /// reference and can repeat across cases.
    /// </summary>
    [Fact]
    public void TheExternalReferenceIsThePegasusCaseReferenceNotTheProvidersOwn()
    {
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", "QDOS", Settings, []);

        Assert.Equal("QDOS26031", payload.ExternalRef);
        Assert.NotEqual(payload.ExternalRef, payload.ClaimNumber);
    }

    /// <summary>
    /// EVA's instruction model carries no inspection-date field and no
    /// mileage field. Both are named in the note rather than guessed into a
    /// field whose meaning no accepted source establishes.
    /// </summary>
    [Fact]
    public void ValuesEvaHasNoFieldForAreNamedInTheNote()
    {
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", "QDOS", Settings, []);

        Assert.Equal(
            string.Join(
                '\n',
                "Work Provider: Connexus",
                "Inspection Date: 10/02/2026",
                "Mileage: 43850 Miles"),
            payload.Notes);
    }

    /// <summary>
    /// EVA sets the instruction date when the instruction arrives, so for an
    /// API submission that instant is the instruction date. Sending the case's
    /// own value would overwrite a truth with a guess at it, so it is not sent
    /// at all - not in a field, and not in the note.
    /// </summary>
    [Fact]
    public void TheInstructionDateIsLeftToEvaToSetOnReceipt()
    {
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", "QDOS", Settings, []);

        Assert.DoesNotContain("Instruction Date", payload.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("05/02/2026", payload.Notes, StringComparison.Ordinal);
    }

    /// <summary>
    /// A value the case does not hold is left out of the note entirely. A line
    /// reading "Mileage:" with nothing after it tells an assessor less than
    /// saying nothing at all.
    /// </summary>
    [Fact]
    public void AValueTheCaseDoesNotHoldIsOmittedFromTheNote()
    {
        var payload = CaseEvaApiMapping.Map(
            Fields() with { Mileage = null, MileageUnit = null },
            "QDOS26031",
            "QDOS",
            Settings,
            []);

        Assert.Equal(
            string.Join(
                '\n',
                "Work Provider: Connexus",
                "Inspection Date: 10/02/2026"),
            payload.Notes);
    }

    [Fact]
    public void ACaseHoldingNoneOfThemSendsAnEmptyNote()
    {
        var payload = CaseEvaApiMapping.Map(
            new EvaReplayFields(
                null, "MT15OYK", "Defender", "A Smith", "AKH/47743/1",
                null, null, null, null, null, null, null, null),
            "QDOS26031",
            "QDOS",
            Settings,
            []);

        Assert.Equal(string.Empty, payload.Notes);
    }

    /// <summary>
    /// Mileage without its unit is meaningless, so the two travel as one
    /// value; the case is required to save them together.
    /// </summary>
    [Fact]
    public void MileageTravelsWithItsUnit()
    {
        var payload = CaseEvaApiMapping.Map(
            Fields() with { MileageUnit = "Km" },
            "QDOS26031",
            "QDOS",
            Settings,
            []);

        Assert.Contains("Mileage: 43850 Km", payload.Notes, StringComparison.Ordinal);
    }

    /// <summary>
    /// The export resolves the inspection address to exactly six lines; this
    /// distributes that same resolution across EVA's named location fields
    /// rather than resolving it again.
    /// </summary>
    [Fact]
    public void TheSixLineInspectionAddressIsSplitAcrossEvasLocationFields()
    {
        var payload = CaseEvaApiMapping.Map(
            Fields() with
            {
                InspectionAddress = "15 High Street\nWatford\nLondon\nHertfordshire\n\nWD17 1AA"
            },
            "QDOS26031",
            "QDOS",
            Settings,
            []);

        Assert.Equal("15 High Street", payload.Location.Address);
        Assert.Equal("Watford", payload.Location.Town);
        Assert.Equal("London", payload.Location.City);
        Assert.Equal("Hertfordshire", payload.Location.County);
        Assert.Equal("WD17 1AA", payload.Location.Postcode);
    }

    /// <summary>
    /// An image-based assessment reaches EVA as the same literal the
    /// drag-and-drop bundle sends, not as an invented address.
    /// </summary>
    [Fact]
    public void AnImageBasedAssessmentTravelsAsEvasOwnLiteral()
    {
        var payload = CaseEvaApiMapping.Map(
            Fields() with { InspectionAddress = CaseEvaMapping.ImageBasedAssessment },
            "QDOS26031",
            "QDOS",
            Settings,
            []);

        Assert.Equal(
            CaseEvaMapping.ImageBasedAssessmentExportValue,
            payload.Location.Address);
    }

    /// <summary>
    /// Values Pegasus does not hold are sent as EVA's own "not known" members
    /// rather than as a plausible guess.
    /// </summary>
    [Fact]
    public void UnheldValuesAreSentAsEvasOwnNotKnownMembers()
    {
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", "QDOS", Settings, []);

        Assert.Equal(EvaInstructionDefaults.NotKnown, payload.InUse);
        Assert.Equal(EvaInstructionDefaults.NotKnown, payload.VehicleDriveable);
        Assert.Equal(EvaInstructionDefaults.CoverTypeToBeAdvised, payload.CoverType);
    }

    /// <summary>
    /// The registration is normalised by the export's own rules, so the API
    /// and the archive cannot state the same vehicle differently.
    /// </summary>
    [Fact]
    public void TheRegistrationUsesTheExportsOwnNormalisation()
    {
        var payload = CaseEvaApiMapping.Map(
            Fields() with { Vrm = " mt15 oyk " },
            "QDOS26031",
            "QDOS",
            Settings,
            []);

        Assert.Equal("MT15OYK", payload.VehicleRegistration);
    }

    /// <summary>
    /// A date that is not in the export's own dd/MM/yyyy shape is not guessed
    /// at — parsing loosely would let a differently-shaped value through and
    /// silently change what day it means.
    /// </summary>
    [Fact]
    public void AMalformedIncidentDateIsNotGuessedAt() =>
        Assert.Null(CaseEvaApiMapping.Map(
            Fields() with { IncidentDate = "2026-01-31" },
            "QDOS26031",
            "QDOS",
            Settings,
            []).IncidentDate);

    [Fact]
    public void FilesTravelInTheOrderTheyWereGiven()
    {
        EvaInstructionFile[] files =
        [
            new("001 front", ".jpg", new byte[] { 1 }),
            new("002 rear", ".png", new byte[] { 2 })
        ];

        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", "QDOS", Settings, files);

        Assert.Equal(files, payload.Files);
    }

    /// <summary>
    /// RequestFrom identifies Collision Engineers to EVA and never varies;
    /// Agent says which Principal the work arrived for and is the only field
    /// that does.
    /// </summary>
    [Fact]
    public void RequestFromIsFixedAndAgentCarriesThePrincipal()
    {
        var one = CaseEvaApiMapping.Map(Fields(), "QDOS26031", "QDOS", Settings, []);
        var two = CaseEvaApiMapping.Map(Fields(), "AKH26100", "AKH", Settings, []);

        Assert.Equal(one.RequestFrom, two.RequestFrom);
        Assert.Equal("QDOS", one.Agent);
        Assert.Equal("AKH", two.Agent);
    }

    /// <summary>
    /// Agent already carries the Principal, so repeating it as a note line is
    /// noise.
    /// </summary>
    [Fact]
    public void TheWorkProviderIsNotNotedWhenItIsSimplyThePrincipal()
    {
        var payload = CaseEvaApiMapping.Map(
            Fields() with { WorkProvider = "QDOS" },
            "QDOS26031",
            "QDOS",
            Settings,
            []);

        Assert.DoesNotContain("Work Provider", payload.Notes, StringComparison.Ordinal);
    }

    /// <summary>
    /// A work provider that is not the Principal the case was allocated to is
    /// worth an assessor reading, so it is still named.
    /// </summary>
    [Fact]
    public void AWorkProviderDifferingFromThePrincipalIsNoted()
    {
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", "QDOS", Settings, []);

        Assert.Contains("Work Provider: Connexus", payload.Notes, StringComparison.Ordinal);
    }

    /// <summary>
    /// Five body lines into four fields. The fifth must not vanish: EVA is
    /// told slightly more than it asked for rather than less than the case
    /// holds.
    /// </summary>
    [Fact]
    public void AFifthAddressLineIsNotLost()
    {
        var payload = CaseEvaApiMapping.Map(
            Fields() with
            {
                InspectionAddress = "Unit 4\nTrade Park\nWatford\nLondon\nHertfordshire\nWD17 1AA"
            },
            "QDOS26031",
            "QDOS",
            Settings,
            []);

        Assert.Equal("Unit 4", payload.Location.Address);
        Assert.Equal("Trade Park", payload.Location.Town);
        Assert.Equal("Watford", payload.Location.City);
        Assert.Equal("London Hertfordshire", payload.Location.County);
        Assert.Equal("WD17 1AA", payload.Location.Postcode);
    }

    private static EvaReplayFields Fields() => new(
        WorkProvider: "Connexus",
        Vrm: "MT15OYK",
        VehicleModel: "Land Rover Defender 110",
        ClaimantName: "A Smith",
        Reference: "AKH/47743/1",
        IncidentDate: "31/01/2026",
        InstructionDate: "05/02/2026",
        InspectionDate: "10/02/2026",
        InspectionAddress: "Image Based Assessment",
        AccidentCircumstances: "Rear-end collision at traffic lights.",
        VatStatus: "20%",
        Mileage: "43850",
        MileageUnit: "Miles");
}
