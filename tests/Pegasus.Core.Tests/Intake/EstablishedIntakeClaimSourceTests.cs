using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// R1014, R1017: a new Case takes the Claim Source its instruction names, when
/// that name is an active Claim Source's; otherwise the one linked Claim
/// Source its intermediary sender matches; otherwise none. Synthetic names
/// and addresses only.
/// </summary>
public sealed class EstablishedIntakeClaimSourceTests
{
    private static readonly ContactChoice Named = Source("Fleet Route Solutions", null);
    private static readonly ContactChoice Sender = Source("Claims Route", "desk@claims-route.example", "PCH", "QDOS");
    private static readonly ContactChoice Unlinked = Source("Other Route", "desk@other-route.example");

    [Theory]
    [InlineData(EstablishedIntakeClaimSource.IntroducerField, "Fleet Route Solutions")]
    [InlineData(EstablishedIntakeClaimSource.IntroducerField, "FLEET ROUTE SOLUTIONS")]
    [InlineData(EstablishedIntakeClaimSource.ClaimSourceField, "  fleet route solutions ")]
    public void TheInstructionsNamedClaimSourceIsTaken(string fieldName, string printed)
    {
        var field = Field(fieldName, printed);

        var established = EstablishedIntakeClaimSource.For([field], null, null, [Named, Sender, Unlinked]);

        Assert.NotNull(established);
        Assert.Same(Named, established.ClaimSource);
        Assert.Equal(IntakeClaimSourceOrigin.Instruction, established.Origin);
        Assert.Same(field, established.Field);
    }

    [Fact]
    public void TheInstructionComesBeforeTheSender()
    {
        var established = EstablishedIntakeClaimSource.For(
            [Field(EstablishedIntakeClaimSource.IntroducerField, "Fleet Route Solutions")],
            "PCH",
            "reports@claims-route.example",
            [Named, Sender]);

        Assert.Same(Named, established?.ClaimSource);
    }

    [Fact]
    public void ANameThatIsNoClaimSourceFallsToTheSender()
    {
        var established = EstablishedIntakeClaimSource.For(
            [Field(EstablishedIntakeClaimSource.ClaimSourceField, "A firm the directory does not hold")],
            "PCH",
            "reports@claims-route.example",
            [Named, Sender]);

        Assert.NotNull(established);
        Assert.Same(Sender, established.ClaimSource);
        Assert.Equal(IntakeClaimSourceOrigin.Sender, established.Origin);
        Assert.Null(established.Field);
    }

    [Fact]
    public void ANameThatIsNoClaimSourceAndNoIntermediaryEstablishNothing()
    {
        var fields = new[] { Field(EstablishedIntakeClaimSource.ClaimSourceField, "A firm the directory does not hold") };

        Assert.True(EstablishedIntakeClaimSource.HasEvidence(fields, null));
        Assert.Null(EstablishedIntakeClaimSource.For(fields, null, "reports@claims-route.example", [Named, Sender]));
    }

    [Fact]
    public void AConflictedFieldNamesNothing()
    {
        var conflicted = new InstructionReviewField(
            EstablishedIntakeClaimSource.IntroducerField, "Fleet Route Solutions", [], false, HasConflict: true);

        Assert.Null(EstablishedIntakeClaimSource.NamedIn([conflicted]));
        Assert.False(EstablishedIntakeClaimSource.HasEvidence([conflicted], null));
    }

    [Fact]
    public void TwoFieldsNamingDifferentClaimSourcesNameNothing()
    {
        var fields = new[]
        {
            Field(EstablishedIntakeClaimSource.ClaimSourceField, "Fleet Route Solutions"),
            Field(EstablishedIntakeClaimSource.IntroducerField, "Claims Route")
        };

        Assert.Null(EstablishedIntakeClaimSource.NamedIn(fields));
        Assert.Null(EstablishedIntakeClaimSource.For(fields, null, null, [Named, Sender]));
    }

    [Fact]
    public void AnotherFieldIsNotAClaimSource()
    {
        var fields = new[] { Field("Hire company", "Fleet Route Solutions"), Field("Source", "Fleet Route Solutions") };

        Assert.False(EstablishedIntakeClaimSource.HasEvidence(fields, null));
        Assert.Null(EstablishedIntakeClaimSource.For(fields, null, null, [Named]));
    }

    [Theory]
    [InlineData("exact-domain", "PCH", "reports@claims-route.example", true)]
    [InlineData("upper-case-sender", "QDOS", "REPORTS@CLAIMS-ROUTE.EXAMPLE", true)]
    [InlineData("another-principal", "QCL", "reports@claims-route.example", false)]
    [InlineData("subdomain", "PCH", "reports@mail.claims-route.example", false)]
    [InlineData("unlinked-claim-source", "PCH", "reports@other-route.example", false)]
    [InlineData("malformed-sender", "PCH", "claims-route.example", false)]
    public void AnIntermediarySenderGivesItsLinkedClaimSource(
        string row, string principalCode, string sender, bool expected)
    {
        Assert.NotEmpty(row);

        var established = EstablishedIntakeClaimSource.For([], principalCode, sender, [Named, Sender, Unlinked]);

        Assert.Equal(expected, established is not null);
        if (expected)
        {
            Assert.Same(Sender, established!.ClaimSource);
        }
    }

    [Fact]
    public void ADirectRouteGivesNoClaimSource()
    {
        Assert.False(EstablishedIntakeClaimSource.HasEvidence([], null));
        Assert.Null(EstablishedIntakeClaimSource.For([], null, "reports@claims-route.example", [Sender]));
    }

    [Fact]
    public void TwoLinkedClaimSourcesOnTheSendersDomainGiveNone()
    {
        var second = Source("Claims Route North", "north@claims-route.example", "PCH");

        Assert.Null(EstablishedIntakeClaimSource.For([], "PCH", "reports@claims-route.example", [Sender, second]));
    }

    [Fact]
    public void ASharedMailDomainMatchesOnlyTheClaimSourcesMailbox()
    {
        var mailbox = Source("Mailbox Route", "claims.route@gmail.com", "YML");

        Assert.Same(
            mailbox,
            EstablishedIntakeClaimSource.For([], "YML", "Claims.Route@GMAIL.com", [mailbox])?.ClaimSource);
        Assert.Null(EstablishedIntakeClaimSource.For([], "YML", "someone.else@gmail.com", [mailbox]));
    }

    private static InstructionReviewField Field(string name, string value) =>
        new(
            name,
            value,
            [new(value, IntakeEvidenceSource.PdfContent, "instruction.pdf")],
            IsDefaulted: false,
            HasConflict: false);

    private static ContactChoice Source(string name, string? email, params string[] linkedPrincipalCodes) =>
        new(
            Guid.NewGuid(),
            name,
            ContactRole.ClaimSource,
            Version: 1,
            Email: email,
            Telephone: null,
            Address: null,
            Postcode: null,
            NotesOnEveryCase: null,
            LinkedPrincipalCodes: linkedPrincipalCodes);
}
