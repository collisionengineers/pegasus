using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Qdos;

public sealed class QdosCaseMatchPolicyTests
{
    [Fact]
    public void PolicyKeyAndVersionAreStable()
    {
        var sut = new QdosCaseMatchPolicy();
        Assert.Equal("QDOS", sut.WorkProviderCode);
        Assert.Equal("qdos_case_match", sut.PolicyKey);
        Assert.Equal(1, sut.PolicyVersion);
    }

    [Theory]
    [InlineData("Our Ref: ABC/DEF/12345/1", "12345/1")]
    [InlineData("Our Ref: AB/98765/1", "98765/1")]
    [InlineData("Claim Reference: 12345/1", "12345/1")]
    [InlineData("Our claim Reference : 45678/1", "45678/1")]
    public void HandlerPrefixVariantsNormalizeToTheSameDurableClaimToken(
        string labelled,
        string expected)
    {
        var keys = Extract(subject: $"(EREF8) RTA on 18/06/2026 : Mrs Jane Example ({labelled})");

        Assert.Equal(expected, keys.DurableClaimToken);
    }

    [Fact]
    public void BareClaimTokenInTheSubjectIsExtracted()
    {
        var keys = Extract(subject: "56789/1 - Jane Example");

        Assert.Equal("56789/1", keys.DurableClaimToken);
    }

    [Fact]
    public void SpacedBareClaimTokenNormalizesToTheSameDurableToken()
    {
        var keys = Extract(subject: "56789 / 1 - Jane Example");

        Assert.Equal("56789/1", keys.DurableClaimToken);
    }

    [Fact]
    public void SpacedAndCompactFormsOfTheSameTokenAreOneDistinctValue()
    {
        var keys = Extract(
            subject: "56789 / 1 - Jane Example (Our Ref: AB/56789/1)");

        Assert.Equal("56789/1", keys.DurableClaimToken);
    }

    [Fact]
    public void QdosLawReferenceGrammarIsItsOwnDurableToken()
    {
        var keys = Extract(
            subject: "Our ref: ABC/DEF0123 - Mutual Client: Mr John Smith - Vehicle Reg; CD34 EFG");

        Assert.Equal("ABC/DEF0123", keys.DurableClaimToken);
        Assert.Equal("CD34EFG", keys.NormalizedVrm);
    }

    [Fact]
    public void ClaimShapedTokenInBodyProseWithoutALabelIsNotExtracted()
    {
        var keys = Extract(body: "As discussed the total came to 12345/1 across both parts.");

        Assert.Null(keys.DurableClaimToken);
    }

    [Fact]
    public void TwoDifferentClaimReferencesYieldNoClaimKey()
    {
        var keys = Extract(
            subject: "(EREF26) RTA (Our Ref: AB/98765/1)",
            body: "Please also update Our Ref: ABC/DEF/12345/1 while you are there.");

        Assert.Null(keys.DurableClaimToken);
    }

    [Theory]
    [InlineData("Our registered offices are at Offices 1 and 2 Riverside Park.")]
    [InlineData("The Model X5 now comes with a tow bar.")]
    [InlineData("We will review this in OCTOBER at the latest.")]
    [InlineData("The B8 corridor and LS8 and BD8 areas are covered.")]
    public void PredecessorFalsePositiveShapesAreNeverExtractedFromFreeText(string body)
    {
        var keys = Extract(body: body);

        Assert.Null(keys.NormalizedVrm);
    }

    [Fact]
    public void TpRegistrationIsNeverAMatchKey()
    {
        var keys = Extract(body: "TP Registration: AB12CDE\nTP Vehicle: BMW 320d");

        Assert.Null(keys.NormalizedVrm);
    }

    [Fact]
    public void ClientRegistrationLabelIsExtractedAndCompacted()
    {
        var keys = Extract(body: "Registration: CD34 EFG");

        Assert.Equal("CD34EFG", keys.NormalizedVrm);
    }

    [Fact]
    public void VehicleLabelCarryingAModelNameIsNotARegistration()
    {
        var keys = Extract(body: "Vehicle Registration: Ford Fiesta");

        Assert.Null(keys.NormalizedVrm);
    }

    [Theory]
    [InlineData("Our Client: Mrs Jane Example", "EXAMPLE", "J")]
    [InlineData("Claimant Name: Mr Sam Sample", "SAMPLE", "S")]
    [InlineData("Claimant: Dr Sarah Jane O'Neill", "O'NEILL", "S")]
    public void NamesNormalizeToTitleStrippedSurnameAndInitial(
        string labelled,
        string surname,
        string initial)
    {
        var keys = Extract(body: labelled);

        Assert.Equal(surname, keys.NormalizedSurname);
        Assert.Equal(initial, keys.NormalizedFirstInitial);
    }

    [Fact]
    public void PossessiveClientVehicleLineDoesNotBecomeAName()
    {
        var keys = Extract(body: "Our Client's Vehicle: Ford Fiesta");

        Assert.Null(keys.NormalizedSurname);
    }

    [Fact]
    public void SubjectIncidentDateIsExtractedFromTheTemplate()
    {
        var keys = Extract(subject: "(EREF8) RTA on 18/06/2026 : Mrs Jane Example");

        Assert.Equal(new DateOnly(2026, 6, 18), keys.IncidentDate);
    }

    [Fact]
    public void LabelledAccidentDateIsExtractedFromLetterText()
    {
        var keys = Extract(body: "Date of Accident: 20/05/2026");

        Assert.Equal(new DateOnly(2026, 5, 20), keys.IncidentDate);
    }

    [Fact]
    public void DeriveIndexKeysUsesTheSameGrammarsAsExtraction()
    {
        var index = new QdosCaseMatchPolicy().DeriveIndexKeys(new(
            "AB/98765/1",
            "EF56 GHJ",
            "Mrs Jane Example",
            new DateOnly(2026, 5, 20)));

        Assert.Equal("98765/1", index.DurableClaimToken);
        Assert.Equal("EF56GHJ", index.NormalizedVrm);
        Assert.Equal("EXAMPLE", index.NormalizedSurname);
        Assert.Equal("J", index.NormalizedFirstInitial);
        Assert.Equal(new DateOnly(2026, 5, 20), index.IncidentDate);
    }

    [Fact]
    public void FullTemplatedSubjectYieldsClaimTokenAndDateTogether()
    {
        var keys = Extract(
            subject: "(EREF8) RTA on 18/06/2026 : Mrs Jane Example (Our Ref: ABC/DEF/12345/1, Vehicle: X)");

        Assert.Equal("12345/1", keys.DurableClaimToken);
        Assert.Equal(new DateOnly(2026, 6, 18), keys.IncidentDate);
        Assert.Null(keys.NormalizedVrm);
    }

    private static CaseMatchKeys Extract(string? subject = null, string? body = null)
    {
        var content = new List<IntakeContentFragment>();
        if (body is not null)
        {
            content.Add(new(IntakeEvidenceSource.EmailBody, "message body", body));
        }

        return new QdosCaseMatchPolicy().ExtractMatchKeys(new(
            IntakeSourceReadStatus.Readable,
            content,
            subject is null ? [] : [new(IntakeEvidenceSource.Subject, subject)],
            [],
            false));
    }
}
