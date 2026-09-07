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

        var selection = Select(selector, Readable("QDOS instruction\nRegistration: AB12 CDE"));

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
        var selection = Select(selector, Readable("Forwarded from QDOS by a broker."));

        Assert.Equal(InstructionPolicySelectionOutcome.NotApplicable, selection.Outcome);
        Assert.Null(selection.Policy);
        Assert.Empty(selection.Matches);
    }

    [Fact]
    public void ANegativeSignalDisqualifiesAnOtherwiseMatchingDocument()
    {
        var selector = new InstructionExtractionPolicySelector(
            [Profile("QDOS", ["QDOS", "Registration:"], ["Connexus Vehicle Assessors"])]);

        var selection = Select(selector, Readable(
            "QDOS\nRegistration: AB12 CDE\nPrepared by Connexus Vehicle Assessors"));

        Assert.Equal(InstructionPolicySelectionOutcome.NotApplicable, selection.Outcome);
    }

    [Fact]
    public void TwoMatchingProfilesAreAmbiguousAndBothAreNamed()
    {
        var selector = new InstructionExtractionPolicySelector(
            [Profile("QDOS", ["Registration:"]), Profile("PCH", ["Registration:"])]);

        var selection = Select(selector, Readable("Registration: AB12 CDE"));

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

        var forwards = Select(new InstructionExtractionPolicySelector([qdos, pch]), document);
        var backwards = Select(new InstructionExtractionPolicySelector([pch, qdos]), document);

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
            Select(selector, Readable("anything at all")).Outcome);
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
            Select(selector, unreadable).Outcome);
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
            Select(selector, qdosLetter).Outcome);

        var otherLetter = Readable(
            "Alison Law\nClaimant Name: Jane Smith\nRegistration: AB12 CDE");
        Assert.Equal(
            InstructionPolicySelectionOutcome.NotApplicable,
            Select(selector, otherLetter).Outcome);

        // And the negative signals do their job on a letter that copies the
        // labels wholesale.
        var lookalike = Readable(
            "QDOS style letter\nOur Client’s Vehicle: Ford Focus\nRegistration: AB12 CDE\n"
            + "Exclusive Vehicle Assessors");
        Assert.Equal(
            InstructionPolicySelectionOutcome.NotApplicable,
            Select(selector, lookalike).Outcome);
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

        var selection = Select(selector, readResult);

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
            Select(selector, readResult).Outcome);
    }

    [Fact]
    public void AProfileForAnotherDocumentRoleIsNotSelectedForAnInstruction()
    {
        // The signals match perfectly. The profile describes a third-party
        // report, and this caller is reading an instruction, so it is not a
        // candidate: selection is by signature AND role.
        var selector = new InstructionExtractionPolicySelector(
            [Profile("REPORT", ["Registration:"], documentRole: "third-party-report")]);

        Assert.Equal(
            InstructionPolicySelectionOutcome.NotApplicable,
            Select(selector, Readable("Registration: AB12 CDE")).Outcome);
        Assert.Equal(
            InstructionPolicySelectionOutcome.Selected,
            selector.Select(Readable("Registration: AB12 CDE"), "third-party-report").Outcome);
    }

    [Fact]
    public void AProfileWithVariantsNeedsOneOfThemAndNamesWhichMatched()
    {
        var selector = new InstructionExtractionPolicySelector(
            [Profile("PCH", ["Registration No:"]) with
            {
                Variants =
                [
                    new("pch-performance", new("instruction", ["Performance Car Hire"], [])),
                    new("pch-lawshield", new("instruction", ["Lawshield"], []))
                ]
            }]);

        var performance = Select(selector, Readable(
            "Registration No: AB12CDE\nPerformance Car Hire, Warrington"));

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, performance.Outcome);
        Assert.Equal(["pch-performance"], performance.MatchedVariantKeys);
        Assert.False(performance.HasAmbiguousVariant);
    }

    [Fact]
    public void TwoVariantsOfOneProfileLeaveTheTemplateAmbiguousAndTheProfileSettled()
    {
        // The real PCH footers co-occur: "Performance Car Hire Ltd is an
        // appointed representative of Lawshield UK Ltd". Which template was
        // used is genuinely unknown; WHO the principal is never was, so the
        // profile is selected and the variant is recorded as ambiguous.
        var selector = new InstructionExtractionPolicySelector(
            [Profile("PCH", ["Registration No:"]) with
            {
                Variants =
                [
                    new("pch-performance", new("instruction", ["Performance Car Hire"], [])),
                    new("pch-lawshield", new("instruction", ["Lawshield"], []))
                ]
            }]);

        var selection = Select(selector, Readable(
            "Registration No: AB12CDE\n"
            + "Performance Car Hire Ltd is an appointed representative of Lawshield UK Ltd"));

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal(["pch-lawshield", "pch-performance"], selection.MatchedVariantKeys);
        Assert.True(selection.HasAmbiguousVariant);
    }

    [Fact]
    public void AProfileWhoseVariantsAllFailIsNotSelectedOnItsSharedSignalsAlone()
    {
        // An unproved variant matches nothing. The shared labels are not
        // enough on their own, which is the point of recording variants
        // rather than merging them into one broader signature.
        var selector = new InstructionExtractionPolicySelector(
            [Profile("PCH", ["Registration No:"]) with
            {
                Variants =
                [
                    new("pch-performance", new("instruction", ["Performance Car Hire"], []))
                ]
            }]);

        Assert.Equal(
            InstructionPolicySelectionOutcome.NotApplicable,
            Select(selector, Readable("Registration No: AB12CDE\nEverywhen Legal Ltd")).Outcome);
    }

    /// <summary>
    /// The shipped PCH profile against the shapes its own originals carry: one
    /// footer, both footers, and the audit heading whose "Connexus" is not the
    /// "Connexus Vehicle Assessors" negative signal.
    /// </summary>
    [Fact]
    public void TheShippedPchProfileReadsItsOwnFootersAndIsNotTrippedByTheAuditHeading()
    {
        var selector = new InstructionExtractionPolicySelector(
            [new PchInstructionExtractionPolicy()]);

        var performanceOnly = Select(selector, Readable(
            "URGENT NEW INSTRUCTION (Connexus Audit Report)\nVehicle Make: VOLVO XC90\n"
            + "Registration No: VN20XFC\n"
            + "Performance Car Hire, 1210 Centre Park Square, Warrington, WA1 1RU"));
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, performanceOnly.Outcome);
        Assert.Equal(
            [PchInstructionExtractionPolicy.PerformanceVariantKey],
            performanceOnly.MatchedVariantKeys);

        var bothFooters = Select(selector, Readable(
            "URGENT NEW INSTRUCTION (Connexus Audit Report)\nVehicle Make: BMW 220i\n"
            + "Registration No: BD69NJY\n"
            + "Performance Car Hire Limited is an appointed representative of Lawshield UK Ltd"));
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, bothFooters.Outcome);
        Assert.True(bothFooters.HasAmbiguousVariant);

        // The assessor firms' own letters share the labels and are not PCH.
        var lookalike = Select(selector, Readable(
            "Vehicle Make: BMW 220i\nRegistration No: BD69NJY\n"
            + "Performance Car Hire\nPrepared by Connexus Vehicle Assessors"));
        Assert.Equal(InstructionPolicySelectionOutcome.NotApplicable, lookalike.Outcome);
    }

    [Fact]
    public void TheShippedQdosAndPchProfilesDoNotClaimEachOthersLetters()
    {
        var selector = new InstructionExtractionPolicySelector(
            [new QdosInstructionExtractionPolicy(), new PchInstructionExtractionPolicy()]);

        var qdosLetter = Select(selector, Readable(
            "QDOS Assist\nOur Client: Jane Smith\nOur Client’s Vehicle: Ford Focus\n"
            + "Registration: AB12 CDE"));
        Assert.Equal("QDOS", qdosLetter.Policy!.PrincipalCode);

        var pchLetter = Select(selector, Readable(
            "Policyholder Name: Jane Smith\nVehicle Make: Ford Focus\n"
            + "Registration No: AB12CDE\nPerformance Car Hire"));
        Assert.Equal("PCH", pchLetter.Policy!.PrincipalCode);
    }

    /// <summary>
    /// Every call the production caller makes names the instruction role;
    /// spelled once here so a test cannot accidentally assert a different one.
    /// </summary>
    private static InstructionPolicySelection Select(
        InstructionExtractionPolicySelector selector,
        IntakeSourceReadResult readResult) =>
        selector.Select(readResult, InstructionDocumentSignature.InstructionRole);

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
        string[]? negative = null,
        string documentRole = InstructionDocumentSignature.InstructionRole) =>
        new(principalCode, new(documentRole, required, negative ?? []));

    internal sealed record StubProfilePolicy(
        string PrincipalCode,
        InstructionDocumentSignature Signature)
        : IInstructionExtractionPolicy, IInstructionDocumentProfile
    {
        public string DocumentProfileKey =>
            $"{PrincipalCode.ToLowerInvariant()}_instruction_document";

        public int DocumentProfileVersion => 1;

        public IReadOnlyList<InstructionTemplateVariant> Variants { get; init; } = [];

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
