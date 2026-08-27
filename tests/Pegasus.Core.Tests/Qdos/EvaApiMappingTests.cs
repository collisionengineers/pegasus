using Pegasus.Core.Eva;

namespace Pegasus.Core.Tests.Qdos;

/// <summary>
/// EXT-04. The API mapping renames the export's settled values into EVA's
/// field names; it must not restate them. These tests pin the rename, the
/// four values EVA has no field for, and the address split.
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
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", Settings, []);

        Assert.Equal("COLLENGAPI", payload.RequestFrom);
        Assert.Equal("QDOS26031", payload.ExternalRef);
        Assert.Equal("AKH/47743/1", payload.ClaimNumber);
        Assert.Equal("Connexus", payload.InsurerName);
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
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", Settings, []);

        Assert.Equal("QDOS26031", payload.ExternalRef);
        Assert.NotEqual(payload.ExternalRef, payload.ClaimNumber);
    }

    /// <summary>
    /// EVA's instruction model carries no mileage, no instruction date, no
    /// inspection date, and no field established as the claimant's name. They
    /// are named in the note rather than guessed into PrincipalName or TPName.
    /// </summary>
    [Fact]
    public void ValuesEvaHasNoFieldForAreNamedInTheNote()
    {
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", Settings, []);

        Assert.Equal(
            string.Join(
                '\n',
                "Claimant Name: A Smith",
                "Instruction Date: 05/02/2026",
                "Inspection Date: 10/02/2026",
                "Mileage: 43850 Miles"),
            payload.Notes);
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
            Fields() with { Mileage = null, MileageUnit = null, ClaimantName = null },
            "QDOS26031",
            Settings,
            []);

        Assert.Equal(
            string.Join('\n', "Instruction Date: 05/02/2026", "Inspection Date: 10/02/2026"),
            payload.Notes);
    }

    [Fact]
    public void ACaseHoldingNoneOfThemSendsAnEmptyNote()
    {
        var payload = CaseEvaApiMapping.Map(
            new EvaReplayFields(
                "Connexus", "MT15OYK", "Defender", null, "AKH/47743/1",
                null, null, null, null, null, null, null, null),
            "QDOS26031",
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
        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", Settings, []);

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

        var payload = CaseEvaApiMapping.Map(Fields(), "QDOS26031", Settings, files);

        Assert.Equal(files, payload.Files);
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
