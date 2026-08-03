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
    [InlineData("Our Ref: MFI/AKH/46553/1", "46553/1")]
    [InlineData("Our Ref: TG/45497/1", "45497/1")]
    [InlineData("Claim Reference: 46553/1", "46553/1")]
    [InlineData("Our claim Reference : 46684/1", "46684/1")]
    public void HandlerPrefixVariantsNormalizeToTheSameDurableClaimToken(
        string labelled,
        string expected)
    {
        var keys = Extract(subject: $"(EREF8) RTA on 18/06/2026 : Mr Nick Jones ({labelled})");

        Assert.Equal(expected, keys.DurableClaimToken);
    }

    [Fact]
    public void BareClaimTokenInTheSubjectIsExtracted()
    {
        var keys = Extract(subject: "46670/1 - Mohammed Jameel");

        Assert.Equal("46670/1", keys.DurableClaimToken);
    }

    [Fact]
    public void SpacedBareClaimTokenNormalizesToTheSameDurableToken()
    {
        var keys = Extract(subject: "46670 / 1 - Mohammed Jameel");

        Assert.Equal("46670/1", keys.DurableClaimToken);
    }

    [Fact]
    public void SpacedAndCompactFormsOfTheSameTokenAreOneDistinctValue()
    {
        var keys = Extract(
            subject: "46670 / 1 - Mohammed Jameel (Our Ref: TG/46670/1)");

        Assert.Equal("46670/1", keys.DurableClaimToken);
    }

    [Fact]
    public void QdosLawReferenceGrammarIsItsOwnDurableToken()
    {
        var keys = Extract(
            subject: "Our ref: ELM/NAK0011 - Mutual Client: Mr John Smith - Vehicle Reg; LT17 UCU");

        Assert.Equal("ELM/NAK0011", keys.DurableClaimToken);
        Assert.Equal("LT17UCU", keys.NormalizedVrm);
    }

    [Fact]
    public void ClaimShapedTokenInBodyProseWithoutALabelIsNotExtracted()
    {
        var keys = Extract(body: "As discussed the total came to 46553/1 across both parts.");

        Assert.Null(keys.DurableClaimToken);
    }

    [Fact]
    public void TwoDifferentClaimReferencesYieldNoClaimKey()
    {
        var keys = Extract(
            subject: "(EREF26) RTA (Our Ref: TG/45497/1)",
            body: "Please also update Our Ref: MFI/AKH/46553/1 while you are there.");

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
        var keys = Extract(body: "Registration: LT17 UCU");

        Assert.Equal("LT17UCU", keys.NormalizedVrm);
    }

    [Fact]
    public void VehicleLabelCarryingAModelNameIsNotARegistration()
    {
        var keys = Extract(body: "Vehicle Registration: Ford Fiesta");

        Assert.Null(keys.NormalizedVrm);
    }

    [Theory]
    [InlineData("Our Client: Mr Nick Jones", "JONES", "N")]
    [InlineData("Claimant Name: Mrs Vivien Healey", "HEALEY", "V")]
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
        var keys = Extract(subject: "(EREF8) RTA on 18/06/2026 : Mr Nick Jones");

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
            "TG/45497/1",
            "HN18 ABC",
            "Mrs Vivien Healey",
            new DateOnly(2026, 5, 20)));

        Assert.Equal("45497/1", index.DurableClaimToken);
        Assert.Equal("HN18ABC", index.NormalizedVrm);
        Assert.Equal("HEALEY", index.NormalizedSurname);
        Assert.Equal("V", index.NormalizedFirstInitial);
        Assert.Equal(new DateOnly(2026, 5, 20), index.IncidentDate);
    }

    [Fact]
    public void FullTemplatedSubjectYieldsClaimTokenAndDateTogether()
    {
        var keys = Extract(
            subject: "(EREF8) RTA on 18/06/2026 : Mr Nick Jones (Our Ref: MFI/AKH/46553/1, Vehicle: X)");

        Assert.Equal("46553/1", keys.DurableClaimToken);
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
