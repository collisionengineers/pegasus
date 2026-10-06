using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The report delivery policy and its one send use case (operator, 6 October
/// 2026). Generation is not delivery — nothing here records a Sent state,
/// transport observation stays with Stream A, and every rule reads persisted
/// structured facts. The Principal's report sending rules are planned by
/// <see cref="ReportDispatchPolicy"/> (<c>ReportDispatchPolicyTests</c>); this
/// class pins how the send follows that plan. The store's own lease and work
/// checks are <c>CaseReportGenerationPersistenceTests</c>'s job.
/// </summary>
public sealed class CaseReportDeliveryTests
{
    private static readonly DateTimeOffset GeneratedAtUtc = new(2026, 9, 6, 11, 0, 0, TimeSpan.Zero);

    private const string Instructor = "instructor@insurer.example";
    private const string ReportsMailbox = "reports@collisionengineers.example";
    private const string EngineersMailbox = "engineers@collisionengineers.example";

    private static readonly StaffMailAttachment ReportAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), 120, "report.pdf", "application/pdf");

    private static readonly StaffMailAttachment FeeAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('b', 64), 60, "fee-note.pdf", "application/pdf");

    private static readonly StaffMailAttachment RepairSpecificationAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('f', 64), 80, "repair-specification.pdf", "application/pdf");

    private static readonly StaffMailAttachment ImagePackAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('9', 64), 90, "images.pdf", "application/pdf");

    private static readonly StaffMailAttachment AudatexEstimate = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('1', 64), 500, "audatex-estimate.pdf", "application/pdf");

    private static readonly StaffMailAttachment OtherEstimate = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('2', 64), 500, "estimate.pdf", "application/pdf");

    // ---- addressing -----------------------------------------------------------

    [Fact]
    public void AddressingFollowsThePrincipalsRules()
    {
        var addressing = CaseReportDeliveryPolicy.Address(Facts(
            PrincipalReportSendingRules.Default with
            {
                SendTo = ["handler@principal.example"],
                Cc = ["copy@principal.example"]
            },
            Instruction(cc: ["a@insurer.example"])));

        Assert.Equal("handler@principal.example", Assert.Single(addressing.To).Address);
        Assert.Equal(["a@insurer.example", "copy@principal.example"], addressing.Cc.Select(item => item.Address));
        Assert.Equal("Re: Instruction for PK12 TMZ", addressing.Subject);
    }

    [Fact]
    public void AddressingRepliesToTheOriginalSenderAndNeverCopiesThemAgain()
    {
        var addressing = CaseReportDeliveryPolicy.Address(Facts(
            PrincipalReportSendingRules.Default with { Cc = ["Instructor@Insurer.example"] }));

        Assert.Equal(Instructor, Assert.Single(addressing.To).Address);
        Assert.Empty(addressing.Cc);
    }

    [Fact]
    public void AddressingRefusesACaseWithNobodyToSendTo()
    {
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.Address(Facts(withInstruction: false)));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.Address(Facts(instruction: Instruction(sender: null))));
    }

    [Fact]
    public void ReviewedAddressingKeepsStaffChosenRecipients()
    {
        var addressing = CaseReportDeliveryPolicy.ReviewedAddress(
            Facts(),
            new(["reviewed@recipient.example"], ["copy@recipient.example"]));

        Assert.Equal("reviewed@recipient.example", Assert.Single(addressing.To).Address);
        Assert.Equal("copy@recipient.example", Assert.Single(addressing.Cc).Address);
        Assert.Equal("Re: Instruction for PK12 TMZ", addressing.Subject);
    }

    [Fact]
    public void ReviewedAddressingDropsDuplicatesWithoutCaseAndRefusesABadOrEmptyTo()
    {
        var addressing = CaseReportDeliveryPolicy.ReviewedAddress(
            Facts(),
            new([" a@recipient.example ", "A@Recipient.example"], ["a@recipient.example", "copy@recipient.example", " "]));

        Assert.Equal("a@recipient.example", Assert.Single(addressing.To).Address);
        Assert.Equal("copy@recipient.example", Assert.Single(addressing.Cc).Address);
        Assert.Throws<ArgumentException>(() => CaseReportDeliveryPolicy.ReviewedAddress(
            Facts(), new(["not an address"], [])));
        Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.ReviewedAddress(
            Facts(), new([" "], ["copy@recipient.example"])));
    }

    [Fact]
    public void TheSuggestedReviewIsThePlansToAndCc()
    {
        var plan = ReportDispatchPolicy.Plan(
            Facts(PrincipalReportSendingRules.Default with { Cc = ["copy@principal.example"] },
                Instruction(cc: ["a@insurer.example"])),
            [], GeneratedAtUtc);

        var review = CaseReportDeliveryPolicy.SuggestedReview(plan);

        Assert.Equal([Instructor], review.To);
        Assert.Equal(["a@insurer.example", "copy@principal.example"], review.Cc);
    }

    // ---- what a send may go ahead on -------------------------------------------

    /// <summary>
    /// The form was drawn from other rules or another instruction: refused
    /// first, whatever else the plan still asks.
    /// </summary>
    [Fact]
    public void AChangedFingerprintRefusesBeforeAnythingElse()
    {
        var facts = DecidingFacts();
        var plan = ReportDispatchPolicy.Plan(facts, [], GeneratedAtUtc);

        var stale = Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.RequireDispatchDecided(
            facts, plan, "stale", null, null));
        var missing = Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.RequireDispatchDecided(
            facts, plan, null, null, null));

        Assert.Equal(CaseReportDeliveryPolicy.DispatchChanged, stale.Message);
        Assert.Equal(CaseReportDeliveryPolicy.DispatchChanged, missing.Message);
    }

    [Fact]
    public void AnUnansweredQuestionRefusesBeforeAStopOrAHold()
    {
        var facts = DecidingFacts();
        var plan = ReportDispatchPolicy.Plan(facts, [], GeneratedAtUtc);

        var refused = Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.RequireDispatchDecided(
            facts, plan, facts.Fingerprint, null, null));

        Assert.Contains("Answer every question", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARulesStopNeedsAReasonBeforeTheHoldsAreChecked()
    {
        var facts = DecidingFacts();
        var plan = ReportDispatchPolicy.Plan(facts, [new(1, 0, false)], GeneratedAtUtc);

        var refused = Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.RequireDispatchDecided(
            facts, plan, facts.Fingerprint, null, null));
        var blank = Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.RequireDispatchDecided(
            facts, plan, facts.Fingerprint, "  ", null));

        Assert.Contains("Do not send yet.", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Do not send yet.", blank.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryHoldMustBeTickedOnceTheStopIsOverridden()
    {
        var facts = DecidingFacts();
        var plan = ReportDispatchPolicy.Plan(facts, [new(1, 0, false)], GeneratedAtUtc);

        var refused = Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.RequireDispatchDecided(
            facts, plan, facts.Fingerprint, "Garage agreed by phone.", null));
        Assert.Contains("Tick Done", refused.Message, StringComparison.Ordinal);

        CaseReportDeliveryPolicy.RequireDispatchDecided(
            facts, plan, facts.Fingerprint, "Garage agreed by phone.", ["Authorise the garage."]);
    }

    // ---- which documents go ----------------------------------------------------

    [Fact]
    public void AttachChoiceAddsTheRequiredCompanionsAndRefusesAMissingOne()
    {
        var plan = ReportDispatchPolicy.Plan(
            Facts(PrincipalReportSendingRules.Default with
            {
                Attach = ReportSendingAttachments.Default with { VehicleImagesDocument = true }
            }),
            [], GeneratedAtUtc);
        CaseReportArtifactKind[] confirmed =
            [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.ImagePack, CaseReportArtifactKind.FeeNote];

        var refused = Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.AttachChoice(
            plan, null, [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.FeeNote]));
        Assert.Contains("images document", refused.Message, StringComparison.Ordinal);

        // The operator's choice gains the required companions and never loses them.
        Assert.Equal(
            [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.ImagePack],
            CaseReportDeliveryPolicy.AttachChoice(plan, [CaseReportArtifactKind.AssessmentReport], confirmed));
        Assert.Equal(
            [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.ImagePack],
            CaseReportDeliveryPolicy.AttachChoice(
                plan, [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.ImagePack], confirmed));
        // No choice means every confirmed document, as it was.
        Assert.Null(CaseReportDeliveryPolicy.AttachChoice(plan, null, confirmed));
        var empty = CaseReportDeliveryPolicy.AttachChoice(plan, [], confirmed);
        Assert.NotNull(empty);
        Assert.Empty(empty);
    }

    [Fact]
    public void CompanionsAreNamedAsStaffReadThem()
    {
        Assert.Equal("images document", CaseReportDeliveryPolicy.CompanionWords(CaseReportArtifactKind.ImagePack));
        Assert.Equal("figure breakdown", CaseReportDeliveryPolicy.CompanionWords(CaseReportArtifactKind.RepairSpecification));
        Assert.Equal("fee note", CaseReportDeliveryPolicy.CompanionWords(CaseReportArtifactKind.FeeNote));
    }

    // ---- generation and readiness ------------------------------------------------

    [Theory]
    [InlineData(CaseReportGenerationState.Pending, true)]
    [InlineData(CaseReportGenerationState.Stale, true)]
    [InlineData(CaseReportGenerationState.Confirmed, false)]
    public void DeliveryRequiresACurrentConfirmedGeneration(CaseReportGenerationState state, bool refused)
    {
        var generationId = Guid.NewGuid();
        if (refused)
        {
            Assert.Throws<InvalidOperationException>(
                () => CaseReportDeliveryPolicy.RequireDeliverable(
                    generationId, state, isCurrent: true, version: 3, expectedVersion: 3));
        }
        else
        {
            CaseReportDeliveryPolicy.RequireDeliverable(
                generationId, state, isCurrent: true, version: 3, expectedVersion: 3);
        }
    }

    [Fact]
    public void DeliveryRequiresTheSupersededGenerationToStayRefused()
    {
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireDeliverable(
                Guid.NewGuid(), CaseReportGenerationState.Confirmed, isCurrent: false, version: 3,
                expectedVersion: 3));
    }

    [Fact]
    public void DeliveryRequiresTheExpectedGenerationVersion()
    {
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireDeliverable(
                Guid.NewGuid(), CaseReportGenerationState.Confirmed, isCurrent: true, version: 4,
                expectedVersion: 3));
    }

    [Fact]
    public void AttachmentsRequireEveryArtifactConfirmedAndPresent()
    {
        var generationId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.Attachments(generationId, []));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.Attachments(generationId, [Artifact(CaseReportArtifactStatus.Pending)]));

        var attachments = CaseReportDeliveryPolicy.Attachments(
            generationId,
            [Artifact(CaseReportArtifactStatus.Confirmed, CaseReportArtifactKind.FeeNote),
             Artifact(CaseReportArtifactStatus.Confirmed, CaseReportArtifactKind.AssessmentReport)]);
        Assert.Equal([ReportAttachment, FeeAttachment], attachments);
    }

    [Fact]
    public void AnAttachmentWithoutAConfirmedIdentityFailsClosed()
    {
        var artifact = Artifact(CaseReportArtifactStatus.Confirmed) with { Sha256 = null };
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.AttachmentOf(artifact));
    }

    [Fact]
    public void ReadinessRequiresTheExactGenerationAndItsFacts()
    {
        var generation = Generation();
        var request = ReadyRequest();
        CaseReportDeliveryPolicy.RequireReady(request, generation);

        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request with { CaseId = Guid.NewGuid() }, generation));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request with { GenerationId = Guid.NewGuid() }, generation));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(
                request with { ExpectedGenerationVersion = 2 }, generation));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request with { Artifacts = [FeeAttachment] }, generation));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request with { Artifacts = [] }, generation));
    }

    [Theory]
    [InlineData(CaseReportGenerationState.Stale, false)]
    [InlineData(CaseReportGenerationState.Confirmed, true)]
    public void ReadinessRefusesAStaleOrSupersededGeneration(CaseReportGenerationState state, bool superseded)
    {
        var generation = Generation() with
        {
            State = state,
            SupersededById = superseded ? Guid.NewGuid() : null,
        };

        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(ReadyRequest(), generation));
    }

    [Fact]
    public void ReadinessRefusesAnAttachmentThatNoLongerMatchesItsConfirmedArtifact()
    {
        var generation = Generation(CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.FeeNote);
        var tampered = FeeAttachment with { Sha256 = new string('c', 64) };
        var request = ReadyRequest() with { Artifacts = [ReportAttachment, tampered] };

        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request, generation));
    }

    /// <summary>
    /// A send may attach the Case's recognised filed estimates: each is
    /// matched against that document as it is filed now.
    /// </summary>
    [Fact]
    public void ReadinessAcceptsAFiledEstimateOnlyAsItIsFiledNow()
    {
        var generation = Generation();
        var request = ReadyRequest() with { Artifacts = [ReportAttachment, AudatexEstimate] };

        CaseReportDeliveryPolicy.RequireReady(request, generation, [AudatexEstimate]);

        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request, generation));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request, generation, [OtherEstimate]));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(
                request, generation, [AudatexEstimate with { Sha256 = new string('3', 64) }]));
    }

    [Fact]
    public async Task ReadinessReadsTheCasesFiledEstimates()
    {
        var request = ReadyRequest() with { Artifacts = [ReportAttachment, AudatexEstimate] };

        await new ReportSendReadiness(
                new FixedGenerations(Generation()),
                new FixedFiledEstimates(new FiledEstimateAttachment(AudatexEstimate, "Audatex")))
            .RequireReadyAsync(request, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReportSendReadiness(
                new FixedGenerations(Generation()), new FixedFiledEstimates())
            .RequireReadyAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ReadinessFailsClosedWhenTheGenerationIsMissing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReportSendReadiness(
                new FixedGenerations(null), new FixedFiledEstimates())
            .RequireReadyAsync(ReadyRequest(), CancellationToken.None));
    }

    // ---- the one send ---------------------------------------------------------------

    [Fact]
    public async Task DeliveryIsAStaffActAndRefusesOtherActors()
    {
        var generations = new FixedGenerations(Generation());
        var send = new RecordingSend();
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => Use(generations, send)
            .ExecuteAsync(
                Request() with { Actor = ActionActor.SystemWorker("delivery-test") },
                CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new ReportSendReadiness(generations, new FixedFiledEstimates())
            .RequireReadyAsync(ReadyRequest(ActionActor.SystemWorker("delivery-test")), CancellationToken.None));
        Assert.Empty(send.Commands);
        Assert.Empty(generations.Deliveries);
    }

    [Fact]
    public async Task SendRequiresItsIdentifiersAndFailsClosedOnAMissingCase()
    {
        var generations = new FixedGenerations(Generation());
        var send = new RecordingSend();
        var sendReport = Use(generations, send);

        await Assert.ThrowsAsync<ArgumentException>(() => sendReport.ExecuteAsync(
            Request() with { LeaseToken = " " }, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => sendReport.ExecuteAsync(
            Request() with { GenerationId = Guid.Empty }, CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sendReport.ExecuteAsync(
            Request() with { CaseId = Guid.NewGuid() }, CancellationToken.None));
        Assert.Empty(send.Commands);
    }

    /// <summary>
    /// The one step (operator, 6 October 2026): the generation is read under
    /// the lease the request carries and Stream A receives one command under
    /// the caller's operation key, replying in the instruction's thread from
    /// the mailbox that holds it. Its returned state is the outcome as is.
    /// </summary>
    [Fact]
    public async Task SendRepliesToTheInstructionInOneCommandAndReturnsTheTransportStateAsIs()
    {
        var generations = new FixedGenerations(Generation());
        var send = new RecordingSend();

        var returned = await UseMailboxes(
                generations, send, Facts(), Mailbox(id: OtherMailboxId, address: EngineersMailbox), Mailbox())
            .ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(StaffMailState.Unknown, returned.State);
        var delivery = Assert.Single(generations.Deliveries);
        Assert.Equal("lease", delivery.LeaseToken);
        Assert.Equal(4, delivery.ExpectedCaseVersion);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailPurpose.CaseReport, command.Mail.Purpose);
        Assert.Equal(StaffMailComposeMode.Reply, command.Mail.ComposeMode);
        Assert.Equal(MailboxId, command.Mail.ApprovedMailboxId);
        var original = Assert.IsType<StaffMailOriginalMessage>(command.Mail.OriginalMessage);
        Assert.Equal(RetainedId, original.RetainedMessageId);
        Assert.Equal(MailboxId, original.ApprovedMailboxId);
        Assert.Equal("immutable-1", original.ImmutableMessageId);
        Assert.Equal("<instruction@insurer.example>", original.InternetMessageId);
        Assert.Equal("conversation-1", original.ConversationId);
        Assert.Equal(RetainedId, returned.OriginalRetainedMessageId);
        Assert.Equal("send-1", command.Mail.OperationKey);
        Assert.Equal("Re: Instruction for PK12 TMZ", command.Mail.Subject);
        Assert.Equal(Instructor, Assert.Single(command.Mail.To).Address);
        Assert.Empty(command.Mail.Cc);
        // A03's report context is the immutable generation: the transport
        // re-checks the generation identity and version, not the Case's.
        Assert.Equal(GenerationId, command.Mail.ContextId);
        Assert.Equal(1, command.Mail.ExpectedContextVersion);
        Assert.Equal(GenerationId, command.Report.GenerationId);
        Assert.Equal(1, command.Report.ExpectedGenerationVersion);
        // Readiness pins custody's own identity; the mail names the report
        // for the people who read it.
        Assert.Equal([ReportAttachment], command.Report.Artifacts);
        Assert.Equal(
            [ReportAttachment with { FileName = "DVR-31001 PK12 TMZ Repairable report.pdf" }],
            command.Mail.Attachments);
        // Nothing was asked, held or stopped.
        var record = Assert.IsType<ReportDispatchRecord>(command.Mail.ReportDispatch);
        Assert.Empty(record.Decisions);
        Assert.Empty(record.Holds);
        Assert.Null(record.StopOverridden);
        Assert.Null(record.StopOverrideReason);
        Assert.Empty(record.AfterSendTasks);
    }

    [Fact]
    public async Task WithoutAnInstructionANewMessageLeavesFromTheSendFromMailboxFoundByAddress()
    {
        var send = new RecordingSend();
        var facts = Facts(
                PrincipalReportSendingRules.Default with { SendFromMailbox = "Reports@CollisionEngineers.example" },
                withInstruction: false)
            with { OriginalSender = Instructor, DefaultMailboxAddress = EngineersMailbox };

        await UseMailboxes(
                new FixedGenerations(Generation()), send, facts,
                Mailbox(id: OtherMailboxId, address: EngineersMailbox), Mailbox())
            .ExecuteAsync(Request(facts), CancellationToken.None);

        var command = Assert.Single(send.Commands).Mail;
        Assert.Equal(StaffMailComposeMode.New, command.ComposeMode);
        Assert.Null(command.OriginalMessage);
        Assert.Equal(MailboxId, command.ApprovedMailboxId);
        Assert.Equal("PK12 TMZ Report", command.Subject);
        Assert.Equal(Instructor, Assert.Single(command.To).Address);
    }

    [Fact]
    public async Task WithNoSendFromMailboxANewMessageLeavesFromTheDefaultMailbox()
    {
        var send = new RecordingSend();
        var facts = Facts(withInstruction: false) with { OriginalSender = Instructor, DefaultMailboxAddress = EngineersMailbox };

        await UseMailboxes(
                new FixedGenerations(Generation()), send, facts,
                Mailbox(), Mailbox(id: OtherMailboxId, address: EngineersMailbox))
            .ExecuteAsync(Request(facts), CancellationToken.None);

        var command = Assert.Single(send.Commands).Mail;
        Assert.Equal(StaffMailComposeMode.New, command.ComposeMode);
        Assert.Equal(OtherMailboxId, command.ApprovedMailboxId);
    }

    /// <summary>
    /// Stream A review (blocker 2): the report journey needs an approved,
    /// identified mailbox bound for both staff send and Sent evidence with a
    /// valid Generation; anything else fails closed and names the address.
    /// </summary>
    [Theory]
    [InlineData("disabled")]
    [InlineData("unbound")]
    [InlineData("no staff send")]
    [InlineData("no sent evidence")]
    [InlineData("generation zero")]
    [InlineData("not listed")]
    public async Task SendNamesTheMailboxItCannotUseAndFailsClosed(string flaw)
    {
        var send = new RecordingSend();
        ApprovedMailbox[] mailboxes = flaw switch
        {
            "disabled" => [Mailbox() with { State = ApprovedMailboxState.Disabled }],
            "unbound" => [Mailbox() with { IdentityIsBound = false }],
            "no staff send" => [Mailbox(scopes: [ApprovedMailboxRouteScope.SentEvidence])],
            "no sent evidence" => [Mailbox(scopes: [ApprovedMailboxRouteScope.StaffSend])],
            "generation zero" => [Mailbox(generation: 0)],
            // The instruction's own mailbox is not among the approved ones.
            _ => [Mailbox(id: OtherMailboxId)]
        };

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => UseMailboxes(
                new FixedGenerations(Generation()), send, Facts(), mailboxes)
            .ExecuteAsync(Request(), CancellationToken.None));

        Assert.Contains(ReportsMailbox, refused.Message, StringComparison.Ordinal);
        Assert.Empty(send.Commands);
    }

    [Fact]
    public async Task SendFailsClosedWithoutAnyApprovedMailbox()
    {
        var send = new RecordingSend();

        await Assert.ThrowsAsync<InvalidOperationException>(() => UseMailboxes(
                new FixedGenerations(Generation()), send, Facts())
            .ExecuteAsync(Request(), CancellationToken.None));
        Assert.Empty(send.Commands);
    }

    /// <summary>
    /// Stream A review (blocker 2): the transport's mailbox guard is the
    /// G14 Generation, never the administration row's concurrency Version —
    /// here deliberately distinct so a swap cannot pass unnoticed.
    /// </summary>
    [Fact]
    public async Task SendUsesTheMailboxGenerationNotItsAdministrationVersion()
    {
        var send = new RecordingSend();

        await Use(new FixedGenerations(Generation()), send, Mailbox(version: 5, generation: 3))
            .ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(3, Assert.Single(send.Commands).Mail.ExpectedMailboxGeneration);
    }

    /// <summary>
    /// v28 P23: one dot is added to the report's name for each report of the
    /// work already sent.
    /// </summary>
    [Fact]
    public async Task SendNamesAReissueWithOneDotForEachReportAlreadySent()
    {
        var send = new RecordingSend();

        await Use(new FixedGenerations(Generation()), send, history: new(2, new DateOnly(2026, 9, 1)))
            .ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(
            "DVR-31001 PK12 TMZ Repairable report...pdf",
            Assert.Single(send.Commands).Mail.Attachments[0].FileName);
    }

    [Theory]
    [InlineData(0, "PK12 TMZ Initial.pdf")]
    [InlineData(1, "PK12 TMZ Supplementary.pdf")]
    public async Task SendNamesTheReportByThePrincipalsPattern(int sentCount, string expected)
    {
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with
        {
            AttachmentName = new("{reg} Initial", "{reg} Supplementary")
        });

        await Use(
                new FixedGenerations(Generation()), send, facts: facts,
                history: sentCount == 0 ? CaseReportSendHistory.None : new(sentCount, null))
            .ExecuteAsync(Request(facts), CancellationToken.None);

        var command = Assert.Single(send.Commands);
        Assert.Equal(expected, command.Mail.Attachments[0].FileName);
        // Custody keeps its own name for the same bytes.
        Assert.Equal([ReportAttachment], command.Report.Artifacts);
    }

    [Fact]
    public async Task SendUsesTheReviewedRecipientsInsteadOfTheSuggestions()
    {
        var send = new RecordingSend();

        await Use(new FixedGenerations(Generation()), send).ExecuteAsync(
            Request() with
            {
                ReviewedRecipients = new(["reviewed@recipient.example"], ["copy@recipient.example"]),
            },
            CancellationToken.None);

        var command = Assert.Single(send.Commands);
        Assert.Equal("reviewed@recipient.example", Assert.Single(command.Mail.To).Address);
        Assert.Equal("copy@recipient.example", Assert.Single(command.Mail.Cc).Address);
    }

    /// <summary>Removals win: a never-cc address staff typed anywhere is not sent to.</summary>
    [Fact]
    public async Task ANeverCcAddressStaffTypedIsNotSentTo()
    {
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with { NeverCc = ["never@insurer.example"] });

        await Use(new FixedGenerations(Generation()), send, facts: facts).ExecuteAsync(
            Request(facts) with
            {
                ReviewedRecipients = new(
                    [Instructor, "never@insurer.example"],
                    ["Never@Insurer.example", "ok@insurer.example"]),
            },
            CancellationToken.None);

        var command = Assert.Single(send.Commands).Mail;
        Assert.Equal(Instructor, Assert.Single(command.To).Address);
        Assert.Equal("ok@insurer.example", Assert.Single(command.Cc).Address);
    }

    /// <summary>
    /// The same click answers the rules' questions and sends: a copy the
    /// answer adds is sent to though the form could not show it.
    /// </summary>
    [Fact]
    public async Task SendRefusesWhileARuleQuestionIsUnansweredAndSendsTheCopyAnAnswerAdds()
    {
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with
        {
            Rules =
            [
                new(ReportSendingRuleMatch.All,
                    [new(ReportSendingConditionKind.Mentions, ["Luton"])],
                    new(["luton@rule.example"], []))
            ]
        });
        var sendReport = Use(new FixedGenerations(Generation()), send, facts: facts);
        var request = Request(facts) with { ReviewedRecipients = new([Instructor], []) };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sendReport.ExecuteAsync(request, CancellationToken.None));
        Assert.Empty(send.Commands);

        await sendReport.ExecuteAsync(request with { Decisions = [new(0, 0, true)] }, CancellationToken.None);

        var command = Assert.Single(send.Commands).Mail;
        Assert.Equal("luton@rule.example", Assert.Single(command.Cc).Address);
        var decision = Assert.Single(Assert.IsType<ReportDispatchRecord>(command.ReportDispatch).Decisions);
        Assert.Equal(new ReportRuleDecision(0, 0, true), decision);
    }

    [Fact]
    public async Task SendRefusesAFormDrawnFromOtherRules()
    {
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with { Hold = "Wait." });

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => Use(
                new FixedGenerations(Generation()), send, facts: facts)
            .ExecuteAsync(Request() with { AcknowledgedHolds = ["Wait."] }, CancellationToken.None));

        Assert.Equal(CaseReportDeliveryPolicy.DispatchChanged, refused.Message);
        Assert.Empty(send.Commands);
    }

    [Fact]
    public async Task ARulesStopNeedsAReasonAndTheSendRecordsTheOverride()
    {
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with
        {
            Rules =
            [
                new(ReportSendingRuleMatch.All,
                    [new(ReportSendingConditionKind.Outcome, ["repairable"])],
                    new([], [], Stop: "Do not send yet."))
            ]
        }) with { Outcome = "repairable" };
        var sendReport = Use(new FixedGenerations(Generation()), send, facts: facts);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sendReport.ExecuteAsync(Request(facts), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sendReport.ExecuteAsync(Request(facts) with { StopOverrideReason = "  " }, CancellationToken.None));
        Assert.Empty(send.Commands);

        await sendReport.ExecuteAsync(
            Request(facts) with { StopOverrideReason = " Garage agreed by phone. " }, CancellationToken.None);

        var record = Assert.IsType<ReportDispatchRecord>(Assert.Single(send.Commands).Mail.ReportDispatch);
        Assert.Equal("Do not send yet.", record.StopOverridden);
        Assert.Equal("Garage agreed by phone.", record.StopOverrideReason);
    }

    [Fact]
    public async Task AnOverrideReasonIsDroppedWhenNothingStopsTheDelivery()
    {
        var send = new RecordingSend();

        await Use(new FixedGenerations(Generation()), send).ExecuteAsync(
            Request() with { StopOverrideReason = "Not needed" }, CancellationToken.None);

        var record = Assert.IsType<ReportDispatchRecord>(Assert.Single(send.Commands).Mail.ReportDispatch);
        Assert.Null(record.StopOverridden);
        Assert.Null(record.StopOverrideReason);
    }

    [Fact]
    public async Task EveryHoldMustBeTickedBeforeTheReportIsSent()
    {
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with
        {
            Hold = "Authorise the garage.",
            Rules =
            [
                new(ReportSendingRuleMatch.All,
                    [new(ReportSendingConditionKind.Outcome, ["repairable"])],
                    new([], [], Hold: "Check the WhatsApp group."))
            ]
        }) with { Outcome = "repairable" };
        var sendReport = Use(new FixedGenerations(Generation()), send, facts: facts);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sendReport.ExecuteAsync(Request(facts), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sendReport.ExecuteAsync(
            Request(facts) with { AcknowledgedHolds = ["Authorise the garage."] }, CancellationToken.None));
        Assert.Empty(send.Commands);

        await sendReport.ExecuteAsync(
            Request(facts) with { AcknowledgedHolds = ["Check the WhatsApp group.", "Authorise the garage."] },
            CancellationToken.None);

        var record = Assert.IsType<ReportDispatchRecord>(Assert.Single(send.Commands).Mail.ReportDispatch);
        Assert.Equal(["Authorise the garage.", "Check the WhatsApp group."], record.Holds);
    }

    [Fact]
    public async Task TheSendRecordsWhatStaffStillOweAfterSending()
    {
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with
        {
            Reminders = ["Send the WhatsApp."],
            GarageFigures = true,
            Rules =
            [
                new(ReportSendingRuleMatch.All,
                    [new(ReportSendingConditionKind.ImagesFrom, ["SWINTON"])],
                    new([], [], Remind: "Tell Andy."))
            ]
        }) with { Outcome = "repairable" };

        await Use(new FixedGenerations(Generation()), send, facts: facts).ExecuteAsync(
            Request(facts) with { Decisions = [new(0, 0, true)] }, CancellationToken.None);

        var record = Assert.IsType<ReportDispatchRecord>(Assert.Single(send.Commands).Mail.ReportDispatch);
        Assert.Equal(
            ["Send the WhatsApp.", "Tell Andy.", ReportDispatchPolicy.GarageFiguresTask],
            record.AfterSendTasks);
    }

    /// <summary>
    /// The template only pre-fills the message: what staff submitted is sent,
    /// with plain line endings.
    /// </summary>
    [Fact]
    public async Task SendCarriesTheMessageStaffSubmitted()
    {
        var send = new RecordingSend();

        await Use(new FixedGenerations(Generation()), send).ExecuteAsync(
            Request() with { CoveringMessage = "Edited by staff.\r\n\r\nKind regards\r\n" },
            CancellationToken.None);

        Assert.Equal("Edited by staff.\n\nKind regards", Assert.Single(send.Commands).Mail.Body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public async Task SendRefusesABlankMessageBeforeAnythingIsRead(string message)
    {
        var generations = new FixedGenerations(Generation());
        var send = new RecordingSend();
        var sendReport = Use(generations, send);

        await Assert.ThrowsAsync<ArgumentException>(() => sendReport.ExecuteAsync(
            Request() with { CoveringMessage = message }, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sendReport.ExecuteAsync(
            Request() with { CoveringMessage = new string('a', EmailTemplates.MaximumBodyLength + 1) },
            CancellationToken.None));
        Assert.Empty(generations.Deliveries);
        Assert.Empty(send.Commands);
    }

    /// <summary>
    /// v28 P22: the delivery attaches the report and the documents the
    /// operator chose, and nothing the generation holds besides.
    /// </summary>
    [Fact]
    public async Task SendAttachesTheDocumentsTheOperatorChose()
    {
        var generation = Generation(CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.FeeNote);
        var send = new RecordingSend();

        await Use(new FixedGenerations(generation), send).ExecuteAsync(
            Request() with { Attach = [CaseReportArtifactKind.AssessmentReport] },
            CancellationToken.None);

        Assert.Equal([ReportAttachment], Assert.Single(send.Commands).Report.Artifacts);
    }

    [Fact]
    public async Task SendAddsTheRequiredCompanionsToTheOperatorsChoice()
    {
        var generation = Generation(
            CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.FeeNote,
            CaseReportArtifactKind.RepairSpecification);
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with
        {
            Attach = ReportSendingAttachments.Default with { FigureBreakdown = true }
        });

        await Use(new FixedGenerations(generation), send, facts: facts).ExecuteAsync(
            Request(facts) with { Attach = [CaseReportArtifactKind.AssessmentReport] },
            CancellationToken.None);

        Assert.Equal(
            [ReportAttachment, RepairSpecificationAttachment],
            Assert.Single(send.Commands).Report.Artifacts);
    }

    [Fact]
    public async Task SendRefusesARequiredCompanionThatIsNotGenerated()
    {
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with
        {
            Attach = ReportSendingAttachments.Default with { VehicleImagesDocument = true }
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Use(
                new FixedGenerations(Generation()), send, facts: facts)
            .ExecuteAsync(Request(facts), CancellationToken.None));
        Assert.Empty(send.Commands);

        await Use(
                new FixedGenerations(Generation(CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.ImagePack)),
                send, facts: facts)
            .ExecuteAsync(Request(facts), CancellationToken.None);
        Assert.Equal([ReportAttachment, ImagePackAttachment], Assert.Single(send.Commands).Report.Artifacts);
    }

    /// <summary>
    /// The Audatex tick attaches the Case's filed Audatex estimates after the
    /// generation's documents, the report leading.
    /// </summary>
    [Fact]
    public async Task SendAttachesTheFiledEstimatesTheRulesTickAfterTheGenerationsDocuments()
    {
        var generation = Generation(CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.FeeNote);
        var send = new RecordingSend();
        var facts = Facts(PrincipalReportSendingRules.Default with
        {
            Attach = ReportSendingAttachments.Default with { Audatex = true }
        }) with
        {
            FiledEstimates = [new(AudatexEstimate, "Audatex"), new(OtherEstimate, "Glass's")]
        };

        await Use(new FixedGenerations(generation), send, facts: facts)
            .ExecuteAsync(Request(facts), CancellationToken.None);

        var command = Assert.Single(send.Commands);
        Assert.Equal([ReportAttachment, FeeAttachment, AudatexEstimate], command.Report.Artifacts);
        Assert.Equal(
            [ReportAttachment with { FileName = "DVR-31001 PK12 TMZ Repairable report.pdf" }, FeeAttachment, AudatexEstimate],
            command.Mail.Attachments);
    }

    /// <summary>
    /// A generation whose report is not confirmed has nothing to attach, and
    /// the transport is never invoked.
    /// </summary>
    [Fact]
    public async Task SendRefusesAGenerationWithoutAConfirmedReport()
    {
        var generation = Generation() with { Artifacts = [Artifact(CaseReportArtifactStatus.Pending)] };
        var send = new RecordingSend();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Use(new FixedGenerations(generation), send)
            .ExecuteAsync(Request(), CancellationToken.None));
        Assert.Empty(send.Commands);
    }

    // ---- fixtures ------------------------------------------------------------------

    private static SendCaseReport Use(
        FixedGenerations generations,
        RecordingSend send,
        ApprovedMailbox? mailbox = null,
        ReportDispatchFacts? facts = null,
        CaseReportSendHistory? history = null) => new(
        generations,
        new FixedSendHistory(history ?? CaseReportSendHistory.None),
        new FixedSuggestions(facts ?? Facts()),
        new FixedMailboxes(mailbox ?? Mailbox()),
        send,
        new FixedTime());

    private static SendCaseReport UseMailboxes(
        FixedGenerations generations,
        RecordingSend send,
        ReportDispatchFacts facts,
        params ApprovedMailbox[] mailboxes) => new(
        generations,
        new FixedSendHistory(CaseReportSendHistory.None),
        new FixedSuggestions(facts),
        new FixedMailboxes(mailboxes),
        send,
        new FixedTime());

    private static ActionActor Staff() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    /// <summary>A send drawn from <paramref name="facts"/> as they are now.</summary>
    private static SendCaseReportRequest Request(ReportDispatchFacts? facts = null) => new(
        Staff(), CaseId, 4, "lease", GenerationId, 1, "send-1", "Message",
        DispatchFingerprint: (facts ?? Facts()).Fingerprint);

    private static ReportSendReadinessRequest ReadyRequest(ActionActor? actor = null) => new(
        actor ?? Staff(), CaseId, GenerationId, 1, [ReportAttachment]);

    /// <summary>
    /// Rules with a Principal hold, a Stop that holds on the repairable
    /// outcome, and a question the Case cannot answer.
    /// </summary>
    private static ReportDispatchFacts DecidingFacts() => Facts(PrincipalReportSendingRules.Default with
    {
        Hold = "Authorise the garage.",
        Rules =
        [
            new(ReportSendingRuleMatch.All,
                [new(ReportSendingConditionKind.Outcome, ["repairable"])],
                new([], [], Stop: "Do not send yet.")),
            new(ReportSendingRuleMatch.All,
                [new(ReportSendingConditionKind.ImagesFrom, ["SWINTON"])],
                new(["swinton@rule.example"], []))
        ]
    }) with { Outcome = "repairable" };

    private static ReportDispatchFacts Facts(
        PrincipalReportSendingRules? rules = null,
        ReportInstructionMessage? instruction = null,
        bool withInstruction = true) => new(
        "DVR-31001",
        "PK12 TMZ",
        "Principal Ltd",
        rules ?? PrincipalReportSendingRules.Default,
        withInstruction ? instruction ?? Instruction() : null);

    private static ReportInstructionMessage Instruction(
        string? sender = Instructor, IReadOnlyList<string>? cc = null) => new(
        RetainedId, MailboxId, ReportsMailbox, "immutable-1", "<instruction@insurer.example>",
        "conversation-1", sender, cc ?? [], "Instruction for PK12 TMZ");

    /// <summary>A current, confirmed generation at version 1 holding the named confirmed documents.</summary>
    private static CaseReportGenerationRecord Generation(params CaseReportArtifactKind[] kinds)
    {
        var estimate = AssessmentReportProjectionTests.ReadyCurrentEstimate();
        var snapshot = new CaseReportGenerationSnapshot(
            CaseId, 4, "DVR-31001", "operation-1", CaseReportActor.Of(Staff()), GeneratedAtUtc,
            Guid.NewGuid(), new string('e', 64), "image/png",
            estimate.SpecificationId, estimate.Version, ReportRepairCosts.For(estimate), 5_000m, Guid.NewGuid(),
            CaseReportContentSwitches.None, ReportGuideSources.None,
            new DateOnly(2026, 9, 6), false, 120m, ["Engineering assessment"], [], [],
            AssessmentReportContract.TemplateVersion, "fake",
            AssessmentReportRenderingTests.Snapshot(AssessmentReportOutcome.Repairable))
        {
            CurrentEstimate = estimate
        };
        return new(
            GenerationId, CaseId, 4, 1, new string('c', 64), snapshot,
            AssessmentReportContract.TemplateVersion, "fake", CaseReportGenerationState.Confirmed,
            GeneratedAtUtc, null,
            [
                .. (kinds.Length == 0 ? [CaseReportArtifactKind.AssessmentReport] : kinds)
                    .Select(kind => Artifact(CaseReportArtifactStatus.Confirmed, kind))
            ]);
    }

    private static CaseReportArtifactRecord Artifact(
        CaseReportArtifactStatus status,
        CaseReportArtifactKind kind = CaseReportArtifactKind.AssessmentReport)
    {
        var attachment = kind switch
        {
            CaseReportArtifactKind.FeeNote => FeeAttachment,
            CaseReportArtifactKind.RepairSpecification => RepairSpecificationAttachment,
            CaseReportArtifactKind.ImagePack => ImagePackAttachment,
            _ => ReportAttachment
        };
        return new(
            Guid.NewGuid(), GenerationId, kind, status, "artifact-1",
            attachment.DocumentId, attachment.VersionId, attachment.Sha256,
            attachment.ContentLength, attachment.FileName, attachment.MediaType,
            "box-file", "box-version", null, null);
    }

    private static ApprovedMailbox Mailbox(
        IReadOnlyList<ApprovedMailboxRouteScope>? scopes = null,
        int version = 5,
        long generation = 3,
        Guid? id = null,
        string address = ReportsMailbox) => new(
        id ?? MailboxId, address,
        scopes ?? [ApprovedMailboxRouteScope.StaffSend, ApprovedMailboxRouteScope.SentEvidence],
        ApprovedMailboxState.Approved,
        "identity", "inbox", "sent", IdentityIsBound: true, ActivatedAtUtc: GeneratedAtUtc, version, [],
        Generation: generation);

    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid GenerationId = Guid.NewGuid();
    private static readonly Guid MailboxId = Guid.NewGuid();
    private static readonly Guid OtherMailboxId = Guid.NewGuid();
    private static readonly Guid RetainedId = Guid.NewGuid();

    /// <summary>The moment the tests send at.</summary>
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => GeneratedAtUtc;
    }

    private sealed class FixedSendHistory(CaseReportSendHistory history) : ICaseReportSendHistoryQueries
    {
        public Task<CaseReportSendHistory> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult(history);
    }

    private sealed class FixedFiledEstimates(params FiledEstimateAttachment[] estimates) : IFiledEstimateAttachmentQueries
    {
        public Task<IReadOnlyList<FiledEstimateAttachment>> ListAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FiledEstimateAttachment>>(caseId == CaseId ? estimates : []);
    }

    /// <summary>One generation, read by its identity and handed to a delivery as it stands.</summary>
    private sealed class FixedGenerations(CaseReportGenerationRecord? generation) : ICaseReportGenerationStore
    {
        public List<SendCaseReportRequest> Deliveries { get; } = [];

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken) =>
            Task.FromResult(generation is not null && caseId == CaseId && generationId == GenerationId
                ? generation
                : null);

        public Task<CaseReportGenerationRecord> GetForDeliveryAsync(
            SendCaseReportRequest request, CancellationToken cancellationToken)
        {
            Deliveries.Add(request);
            return Task.FromResult(generation
                ?? throw new InvalidOperationException("The case report generation is unavailable."));
        }

        public Task<CaseReportFreezeResult> FreezeAsync(
            FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
            ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
            RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> MarkStaleAsync(Guid caseId, string reasonCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RecordDraftPreviewedAsync(
            RecordCaseReportDraftPreviewedRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedSuggestions(ReportDispatchFacts facts)
        : IReportRecipientSuggestionQueries
    {
        public Task<ReportDispatchFacts?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<ReportDispatchFacts?>(caseId == CaseId ? facts : null);
    }

    private sealed class FixedMailboxes(params ApprovedMailbox[] mailboxes) : IApprovedMailboxStore
    {
        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedMailbox>>(mailboxes);

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApprovedMailbox> SetDefaultAsync(
            SetDefaultApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsApprovedAsync(
            string mailboxAddress, ApprovedMailboxRouteScope routeScope, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class RecordingSend : IStaffReportSend
    {
        public List<StaffReportSendCommand> Commands { get; } = [];

        private static readonly StaffMailOperation Result = new(
            Guid.NewGuid(), StaffMailState.Unknown, null, 1, GeneratedAtUtc, null, null, null,
            Guid.NewGuid(), 1, new string('d', 64), null, null,
            StaffMailPurpose.CaseReport, GenerationId, 1, null);

        public Task<StaffMailOperation> SendAsync(
            StaffReportSendCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            // The operation carries the context of the command that made it (G25).
            return Task.FromResult(Result with
            {
                Purpose = command.Mail.Purpose,
                ContextId = command.Mail.ContextId,
                ExpectedContextVersion = command.Mail.ExpectedContextVersion,
                OriginalRetainedMessageId = command.Mail.OriginalMessage?.RetainedMessageId,
            });
        }
    }
}
