using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// Selection is from the DOCUMENT, never from the route: a signature either
/// matches or it does not, more than one match is Ambiguous rather than a
/// winner, and nothing about the order the policies are injected in can change
/// the answer.
/// </summary>
public sealed class InstructionExtractionPolicySelectorTests
{
    [Fact]
    public void ADocumentCarryingEveryRequiredSignalSelectsThatProfile()
    {
        var selector = new InstructionExtractionPolicySelector(
            [Profile("QDOS", ["QDOS", "Registration:"]), Profile("PCH", ["PCH Assist"])]);

        var selection = selector.Select(Readable("QDOS instruction\nRegistration: AB12 CDE"));

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("QDOS", selection.Policy!.PrincipalCode);
        Assert.Equal("QDOS", Assert.Single(selection.Matches).PrincipalCode);
    }

    [Fact]
    public void AMissingRequiredSignalIsNotApplicableRatherThanAPartialMatch()
    {
        var selector = new InstructionExtractionPolicySelector(
            [Profile("QDOS", ["QDOS", "Registration:"])]);

        // The word QDOS alone is not a QDOS instruction. There is no partial
        // credit and no score to fall back on.
        var selection = selector.Select(Readable("Forwarded from QDOS by a broker."));

        Assert.Equal(InstructionPolicySelectionOutcome.NotApplicable, selection.Outcome);
        Assert.Null(selection.Policy);
        Assert.Empty(selection.Matches);
    }

    [Fact]
    public void ANegativeSignalDisqualifiesAnOtherwiseMatchingDocument()
    {
        var selector = new InstructionExtractionPolicySelector(
            [Profile("QDOS", ["QDOS", "Registration:"], ["Connexus Vehicle Assessors"])]);

        var selection = selector.Select(Readable(
            "QDOS\nRegistration: AB12 CDE\nPrepared by Connexus Vehicle Assessors"));

        Assert.Equal(InstructionPolicySelectionOutcome.NotApplicable, selection.Outcome);
    }

    [Fact]
    public void TwoMatchingProfilesAreAmbiguousAndBothAreNamed()
    {
        var selector = new InstructionExtractionPolicySelector(
            [Profile("QDOS", ["Registration:"]), Profile("PCH", ["Registration:"])]);

        var selection = selector.Select(Readable("Registration: AB12 CDE"));

        Assert.Equal(InstructionPolicySelectionOutcome.Ambiguous, selection.Outcome);
        Assert.Null(selection.Policy);
        Assert.Equal(
            ["PCH", "QDOS"],
            selection.Matches.Select(policy => policy.PrincipalCode));
    }

    [Fact]
    public void TheInjectionOrderOfThePoliciesCannotChangeTheAnswer()
    {
        var qdos = Profile("QDOS", ["Registration:"]);
        var pch = Profile("PCH", ["Registration:"]);
        var document = Readable("Registration: AB12 CDE");

        var forwards = new InstructionExtractionPolicySelector([qdos, pch]).Select(document);
        var backwards = new InstructionExtractionPolicySelector([pch, qdos]).Select(document);

        Assert.Equal(forwards.Outcome, backwards.Outcome);
        Assert.Equal(
            forwards.Matches.Select(policy => policy.PrincipalCode),
            backwards.Matches.Select(policy => policy.PrincipalCode));
    }

    [Fact]
    public void APolicyWithoutADocumentProfileIsNeverSelectedFromContent()
    {
        // A route-only policy stays reachable through an established route and
        // is invisible here; that is the whole distinction the interface makes.
        var selector = new InstructionExtractionPolicySelector([new RouteOnlyPolicy()]);

        Assert.Equal(
            InstructionPolicySelectionOutcome.NotApplicable,
            selector.Select(Readable("anything at all")).Outcome);
    }

    [Fact]
    public void AnUnreadableDocumentSelectsNothing()
    {
        var selector = new InstructionExtractionPolicySelector(
            [Profile("QDOS", ["Registration:"])]);

        var unreadable = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Unsupported,
            [new(IntakeEvidenceSource.DocumentContent, "uploaded file", "Registration: AB12 CDE")],
            [],
            [],
            RequiresOcr: false);

        Assert.Equal(
            InstructionPolicySelectionOutcome.NotApplicable,
            selector.Select(unreadable).Outcome);
    }

    [Fact]
    public void ASignatureWithNoRequiredSignalIsRefusedRatherThanMatchingEverything()
    {
        Assert.Throws<ArgumentException>(() =>
            InstructionDocumentSignature.Validate(new("instruction", [], [])));
        Assert.Throws<ArgumentException>(() =>
            InstructionDocumentSignature.Validate(new("instruction", ["  "], [])));
    }

    /// <summary>
    /// The shipped QDOS signature against a real-shaped letter and against a
    /// letter that is emphatically not one: the profile must not claim a
    /// document merely because it mentions a vehicle registration.
    /// </summary>
    [Fact]
    public void TheShippedQdosProfileMatchesAQdosLetterAndNotAnother()
    {
        var selector = new InstructionExtractionPolicySelector(
            [new QdosInstructionExtractionPolicy()]);

        var qdosLetter = Readable(
            "QDOS Assist\nOur Client: Jane Smith\nOur Client’s Vehicle: Ford Focus\n"
            + "Registration: AB12 CDE\nDate of Accident: 01/02/2031");
        Assert.Equal(
            InstructionPolicySelectionOutcome.Selected,
            selector.Select(qdosLetter).Outcome);

        var otherLetter = Readable(
            "Alison Law\nClaimant Name: Jane Smith\nRegistration: AB12 CDE");
        Assert.Equal(
            InstructionPolicySelectionOutcome.NotApplicable,
            selector.Select(otherLetter).Outcome);

        // And the negative signals do their job on a letter that copies the
        // labels wholesale.
        var lookalike = Readable(
            "QDOS style letter\nOur Client’s Vehicle: Ford Focus\nRegistration: AB12 CDE\n"
            + "Exclusive Vehicle Assessors");
        Assert.Equal(
            InstructionPolicySelectionOutcome.NotApplicable,
            selector.Select(lookalike).Outcome);
    }

    [Fact]
    public void StructuredFragmentsAreReadForSignalsLikeAnyOtherContent()
    {
        // The reader now reports cells and form fields as their own fragments.
        // A signature signal printed only in a cell is still something the
        // document says, so it must still identify the profile.
        var selector = new InstructionExtractionPolicySelector(
            [Profile("CELL", ["Assessment Instruction", "Claim Number"])]);
        var readResult = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.DocumentContent, "uploaded instruction.docx", "Assessment Instruction"),
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "uploaded instruction.docx, table 1 row 1 column 1",
                    "Claim Number",
                    IntakeSourceLocator.ForCell(1, 1, 1)),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "uploaded instruction.pdf, form field txtClaimRef",
                    "CLM-1",
                    IntakeSourceLocator.ForFormField("txtClaimRef", page: 1))
            ],
            [],
            [],
            RequiresOcr: false);

        var selection = selector.Select(readResult);

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("CELL", selection.Policy!.PrincipalCode);
    }

    [Fact]
    public void ANegativeSignalQuotedInAnEarlierMessageStillDisqualifiesTheProfile()
    {
        // The quoted history is part of what the document says. A profile that
        // declares a negative signal is not rescued by that signal appearing
        // only beneath a forwarded header — selection reads content, and the
        // quoted fragment is content.
        var selector = new InstructionExtractionPolicySelector(
            [Profile("QUOTED", ["Assessment Instruction"], ["Third Party Engineer"])]);
        var readResult = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message, email body",
                    "Assessment Instruction",
                    IntakeSourceLocator.ForMessagePart(IntakeMessagePart.CurrentBody)),
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message, quoted history",
                    "From: someone " + Environment.NewLine + "Third Party Engineer report attached",
                    IntakeSourceLocator.ForMessagePart(IntakeMessagePart.QuotedHistory))
            ],
            [],
            [],
            RequiresOcr: false);

        Assert.Equal(
            InstructionPolicySelectionOutcome.NotApplicable,
            selector.Select(readResult).Outcome);
    }

    private static IntakeSourceReadResult Readable(string text) =>
        new(
            IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.DocumentContent, "uploaded instruction.pdf", text)],
            [],
            [],
            RequiresOcr: false);

    internal static StubProfilePolicy Profile(
        string principalCode,
        string[] required,
        string[]? negative = null) =>
        new(principalCode, new("instruction", required, negative ?? []));

    internal sealed record StubProfilePolicy(
        string PrincipalCode,
        InstructionDocumentSignature Signature)
        : IInstructionExtractionPolicy, IInstructionDocumentProfile
    {
        public string DocumentProfileKey =>
            $"{PrincipalCode.ToLowerInvariant()}_instruction_document";

        public int DocumentProfileVersion => 1;

        public IReadOnlyList<InstructionReviewField> Fields { get; init; } = [];

        /// <summary>
        /// What the command handed the policy as the established principal. The
        /// point of the assertion is that it is the DOCUMENT profile's identity
        /// and never a mail route's.
        /// </summary>
        public EstablishedPrincipalContext? ObservedPrincipalContext { get; private set; }

        public InstructionExtractionResult Extract(
            IntakeSourceReadResult readResult,
            DateTimeOffset processedAtUtc,
            EstablishedPrincipalContext principalContext)
        {
            ObservedPrincipalContext = principalContext;
            return new(
                InstructionPolicyApplicability.Applicable,
                [],
                Fields,
                // Deliberately no draft: the selector's own tests never assert
                // one, and the analysis command never writes one either.
                null,
                [],
                principalContext.PolicyKey,
                principalContext.PolicyVersion);
        }
    }

    private sealed class RouteOnlyPolicy : IInstructionExtractionPolicy
    {
        public string PrincipalCode => "ROUTE";

        public InstructionExtractionResult Extract(
            IntakeSourceReadResult readResult,
            DateTimeOffset processedAtUtc,
            EstablishedPrincipalContext principalContext) =>
            throw new NotSupportedException("Not used by these tests.");
    }
}
