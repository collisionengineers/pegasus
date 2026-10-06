using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Documents;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.IntegrationTests.Support;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The durable half of the one-step report send (operator, 6 October 2026):
/// the generation store re-reads the Case, its lease and the generation's
/// rows for one delivery, the dispatch facts are read from the Case, its
/// Principal's report sending rules and its instruction, and the send
/// boundary re-reads the confirmed artifacts and filed estimates by exact
/// identity, hash and length. The transport is a recording double; nothing
/// here sends or records a Sent state.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseReportDeliveryPersistenceTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    private const string RouteSender = "origin-sender@principal.example";
    private const string DefaultMailboxAddress = "reports@collisionengineers.example";
    private const string InstructionMailboxAddress = "engineers@collisionengineers.example";
    private const string InstructionMailboxGraphId = "delivery-instruction-mailbox";
    private const string SendFromMailboxAddress = "principal-desk@collisionengineers.example";
    private static readonly Guid DefaultMailboxId = Guid.Parse("6f0d6a43-4a8e-4c55-9d4e-1f7a0d2c9b01");
    private static readonly Guid SendFromMailboxId = Guid.Parse("6f0d6a43-4a8e-4c55-9d4e-1f7a0d2c9b02");
    private static readonly Guid InstructionMailboxId = TestMailboxId.From(InstructionMailboxGraphId);

    private const string HoldText = "Authorise the garage.";
    private const string StopText = "Images must come from the assessor.";

    /// <summary>
    /// A Principal whose rules hold every send until the garage is
    /// authorised, remind staff to send the WhatsApp, and stop a send whose
    /// images came from the garage, which only staff can say.
    /// </summary>
    private static readonly PrincipalReportSendingRules HeldRules = PrincipalReportSendingRules.Default with
    {
        Hold = HoldText,
        Reminders = ["Send the WhatsApp."],
        Rules =
        [
            new(ReportSendingRuleMatch.All,
                [new(ReportSendingConditionKind.ImagesFrom, ["Garage"])],
                new([], [], Remind: "Chase the images.", Stop: StopText))
        ]
    };

    [Fact]
    public async Task SendHandsTheTransportTheConfirmedArtifactAndTheReviewedRecipients()
    {
        await using var harness = await Harness.CreateAsync();

        var command = await harness.SendAsync(harness.Request());

        Assert.Equal(harness.GenerationId, command.Mail.ContextId);
        Assert.Equal(1, command.Mail.ExpectedContextVersion);
        Assert.Equal(StaffMailPurpose.CaseReport, command.Mail.Purpose);
        // No registration is recorded and no instruction is replied to, so
        // the subject names the Case's reference.
        Assert.Equal(
            ("handler@principal.example", "DVR-31001 Report", "Please find attached our report."),
            (Assert.Single(command.Mail.To).Address, command.Mail.Subject, command.Mail.Body));
        var pinned = Assert.Single(command.Report.Artifacts);
        Assert.Equal(
            (harness.Artifact.DocumentId, harness.Artifact.VersionId, harness.Artifact.Sha256, harness.Artifact.ContentLength),
            (pinned.DocumentId!.Value, pinned.VersionId!.Value, pinned.Sha256, pinned.ContentLength));
        // Custody keeps "report.pdf"; the mail names the report for its readers.
        Assert.Equal("report.pdf", pinned.FileName);
        var sent = Assert.Single(command.Mail.Attachments);
        Assert.StartsWith("DVR-31001 ", sent.FileName, StringComparison.Ordinal);
        Assert.EndsWith(" report.pdf", sent.FileName, StringComparison.Ordinal);
        await harness.Readiness().RequireReadyAsync(command.Report, CancellationToken.None);
    }

    /// <summary>
    /// Without reviewed recipients the plan addresses the delivery: under the
    /// default rules, the effective sender of the instruction that opened the
    /// Case.
    /// </summary>
    [Fact]
    public async Task SendWithoutReviewedRecipientsAddressesThePlansRecipients()
    {
        await using var harness = await Harness.CreateAsync();

        var command = await harness.SendAsync(harness.Request() with { ReviewedRecipients = null });

        Assert.Equal(RouteSender, Assert.Single(command.Mail.To).Address);
        Assert.Empty(command.Mail.Cc);
    }

    [Fact]
    public async Task SendRefusesAPendingSupersededOrStaleVersionGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SetGenerationStateAsync(nameof(CaseReportGenerationState.Pending));
        await harness.RefusalAsync(harness.Request());

        await harness.SetGenerationStateAsync(nameof(CaseReportGenerationState.Confirmed));
        await harness.SupersedeGenerationAsync();
        await harness.RefusalAsync(harness.Request());

        await using var fresh = await Harness.CreateAsync();
        await fresh.SetGenerationVersionAsync(7);
        await fresh.RefusalAsync(fresh.Request(expectedGenerationVersion: 1));
    }

    [Fact]
    public async Task SendRefusesAGenerationWhoseReportIsNotConfirmed()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SetArtifactStateAsync(nameof(CaseReportArtifactStatus.Pending));

        await harness.RefusalAsync(harness.Request());
    }

    [Fact]
    public async Task SendRequiresTheHeldLeaseAndRefusesACaseVersionFromTheFuture()
    {
        await using var harness = await Harness.CreateAsync();

        await Assert.ThrowsAsync<CaseVersionConflictException>(
            () => harness.SendAsync(harness.Request(expectedCaseVersion: 2)));
        await AssertThrowsAsyncAny<CaseEditLeaseExpiredException, CaseEditLeaseConflictException>(
            () => harness.SendAsync(harness.Request(leaseToken: "foreign-lease")));
    }

    /// <summary>
    /// System work never ends a member of staff's edit session (operator,
    /// 6 October 2026), so a Case it moved after the page was read does not
    /// refuse the send made under the lease, at the store or at the send
    /// boundary.
    /// </summary>
    [Fact]
    public async Task SystemWorkThatMovedTheCaseDoesNotRefuseTheSend()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.MoveCaseVersionAsync(3);

        var command = await harness.SendAsync(harness.Request(expectedCaseVersion: 1));

        await harness.Readiness().RequireReadyAsync(command.Report, CancellationToken.None);
    }

    [Fact]
    public async Task ReadinessRefusesWhenAConfirmedArtifactChangedUnderneath()
    {
        await using var harness = await Harness.CreateAsync();
        var command = await harness.SendAsync(harness.Request());
        var readiness = harness.Readiness();

        // The attachment still matches its confirmed row: ready.
        await readiness.RequireReadyAsync(command.Report, CancellationToken.None);

        await harness.TamperVersionAsync(harness.ArtifactVersionId);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => readiness.RequireReadyAsync(command.Report, CancellationToken.None));
    }

    [Fact]
    public async Task ReadinessRefusesAGenerationThatWentStaleAfterTheSendBegan()
    {
        await using var harness = await Harness.CreateAsync();
        var command = await harness.SendAsync(harness.Request());

        await harness.SetGenerationStateAsync(nameof(CaseReportGenerationState.Stale));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Readiness().RequireReadyAsync(command.Report, CancellationToken.None));
    }

    /// <summary>
    /// The Inspection report of a Case that has its Audit is sent on its own
    /// work, and once sent it is sent again when needed (operator, 2 and
    /// 6 October 2026). It is not the current work's report.
    /// </summary>
    [Fact]
    public async Task TheInspectionReportIsSentOnItsOwnWorkOnceTheAuditExistsAndAgainOnceSent()
    {
        await using var harness = await Harness.CreateAsync();
        await CaseReportGenerationPersistenceTests.GiveAuditAsync(harness.Factory, harness.CaseId, StartUtc);
        var request = harness.Request() with { Work = CaseWorkSelector.Primary };

        await harness.SendAsync(request);
        await CaseReportGenerationPersistenceTests.LinkInspectionSentEvidenceAsync(harness.Factory, harness.CaseId, StartUtc);
        await harness.SendAsync(request with { OperationKey = "send-report-2" });

        await harness.RefusalAsync(request with { Work = CaseWorkSelector.Current });
    }

    /// <summary>
    /// A Case opened from an uploaded file holds no retained instruction: the
    /// facts carry the Principal's rules, the route decision's effective
    /// sender and the default staff-send mailbox, and nothing to reply to.
    /// </summary>
    [Fact]
    public async Task TheDispatchFactsOfACaseWithNoRetainedInstructionCarryTheRouteSenderAndNothingToReplyTo()
    {
        await using var harness = await Harness.CreateAsync();

        var facts = await harness.FactsAsync();

        Assert.NotNull(facts);
        Assert.Equal("DVR-31001", facts.CaseReference);
        Assert.Equal("Report delivery test", facts.PrincipalName);
        Assert.Equal(PrincipalReportSendingRules.Default, facts.Rules);
        Assert.Null(facts.Instruction);
        Assert.Null(facts.InstructionText);
        Assert.Equal(RouteSender, facts.OriginalSender);
        Assert.Equal(RouteSender, facts.Sender);
        Assert.Equal(DefaultMailboxAddress, facts.DefaultMailboxAddress);
        Assert.Null(facts.Registration);
        Assert.Empty(facts.FiledEstimates);
    }

    /// <summary>
    /// The instruction is the retained message of the Case's origin receipt,
    /// held in the approved mailbox it is replied from. Its sender is the
    /// route decision's effective sender, never the retained row's own
    /// sender, and its text is its subject and plain-text body.
    /// </summary>
    [Fact]
    public async Task TheDispatchFactsJoinTheRetainedInstructionAndTheMailboxHoldingIt()
    {
        await using var harness = await Harness.CreateAsync();
        var messageId = await harness.SeedRetainedInstructionAsync(cc: ["copy@principal.example"]);

        var facts = await harness.FactsAsync();

        Assert.NotNull(facts);
        var instruction = Assert.IsType<ReportInstructionMessage>(facts.Instruction);
        Assert.Equal(messageId, instruction.RetainedMessageId);
        Assert.Equal(InstructionMailboxId, instruction.MailboxId);
        Assert.Equal(InstructionMailboxAddress, instruction.MailboxAddress);
        Assert.Equal("immutable-instruction", instruction.ImmutableMessageId);
        Assert.Equal("<instruction@principal.example>", instruction.InternetMessageId);
        Assert.Equal("conversation-instruction", instruction.ConversationId);
        Assert.Equal(RouteSender, instruction.EffectiveSender);
        Assert.Equal(["copy@principal.example"], instruction.Cc);
        Assert.Equal("Claim for AB12 CDE", instruction.Subject);
        Assert.Equal("Claim for AB12 CDE\nPlease inspect the vehicle.", facts.InstructionText);
        Assert.Equal(RouteSender, facts.Sender);
    }

    /// <summary>
    /// A route decision that could not resolve the sender gives the report
    /// no sender at all, even though the retained message names one.
    /// </summary>
    [Fact]
    public async Task AnUnresolvedRouteSenderGivesNoSenderEvenWhenTheRetainedMessageNamesOne()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedRetainedInstructionAsync(cc: []);
        await harness.UnresolveRouteSenderAsync();

        var facts = await harness.FactsAsync();

        Assert.NotNull(facts);
        Assert.NotNull(facts.Instruction);
        Assert.Null(facts.Instruction.EffectiveSender);
        Assert.Null(facts.OriginalSender);
        Assert.Null(facts.Sender);
    }

    /// <summary>
    /// An uploaded e-mail sits in no mailbox: it gives the instruction text
    /// the rules search, but nothing to reply to.
    /// </summary>
    [Fact]
    public async Task AnUploadedEmailGivesItsTextButNothingToReplyTo()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedUploadedInstructionAsync();

        var facts = await harness.FactsAsync();

        Assert.NotNull(facts);
        Assert.Null(facts.Instruction);
        Assert.Equal("Uploaded claim\nSent through the CarClaims portal.", facts.InstructionText);
        Assert.Equal(RouteSender, facts.Sender);
    }

    /// <summary>
    /// The registration is the accepted value: the confirmed one, else the
    /// intake fact, never a suggestion.
    /// </summary>
    [Fact]
    public async Task TheRegistrationIsTheConfirmedValueElseTheIntakeFact()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedRegistrationAsync(fact: "AB12 CDE", suggestion: "ZZ99 ZZZ");

        Assert.Equal("AB12 CDE", (await harness.FactsAsync())!.Registration);

        await harness.ConfirmRegistrationAsync("AB12CDE");

        Assert.Equal("AB12CDE", (await harness.FactsAsync())!.Registration);
        var command = await harness.SendAsync(harness.Request());
        Assert.Equal("AB12CDE Report", command.Mail.Subject);
    }

    /// <summary>
    /// Only a filed estimate the Worker recognised is listed: confirmed in
    /// custody, current, not removed and not generated by Pegasus, with the
    /// format it was read in.
    /// </summary>
    [Fact]
    public async Task OnlyARecognisedCurrentConfirmedFiledEstimateIsListedWithItsFormat()
    {
        await using var harness = await Harness.CreateAsync();
        var filed = await harness.SeedFiledEstimateAsync();
        await harness.SeedFiledEstimateAsync(recognised: false, provider: null);
        await harness.SeedFiledEstimateAsync(recognised: null, provider: null);
        await harness.SeedFiledEstimateAsync(removed: true);
        await harness.SeedFiledEstimateAsync(current: false);
        await harness.SeedFiledEstimateAsync(custody: DocumentCustodyStatus.Pending);
        await harness.SeedFiledEstimateAsync(source: DocumentSource.Generated);

        var listed = await new EfFiledEstimateAttachmentQueries(harness.Factory)
            .ListAsync(harness.CaseId, CancellationToken.None);
        var facts = await harness.FactsAsync();

        Assert.Equal([new FiledEstimateAttachment(filed, ReportDispatchPolicy.AudatexProvider)], listed);
        Assert.Equal(listed, facts!.FiledEstimates);
    }

    /// <summary>
    /// With a retained instruction the report replies in its thread, from the
    /// mailbox that holds it, under its subject, to the plan's recipients;
    /// the operation the send prepares names the message it answers.
    /// </summary>
    [Fact]
    public async Task AReportRepliesInTheInstructionsThreadFromTheMailboxThatHoldsIt()
    {
        await using var harness = await Harness.CreateAsync();
        var messageId = await harness.SeedRetainedInstructionAsync(cc: ["copy@principal.example"]);

        var command = await harness.SendAsync(harness.Request() with { ReviewedRecipients = null });

        Assert.Equal(StaffMailComposeMode.Reply, command.Mail.ComposeMode);
        Assert.Equal(InstructionMailboxId, command.Mail.ApprovedMailboxId);
        Assert.Equal(
            new StaffMailOriginalMessage(
                messageId,
                InstructionMailboxId,
                "immutable-instruction",
                "<instruction@principal.example>",
                "conversation-instruction"),
            command.Mail.OriginalMessage);
        Assert.Equal("Re: Claim for AB12 CDE", command.Mail.Subject);
        Assert.Equal(RouteSender, Assert.Single(command.Mail.To).Address);
        Assert.Equal("copy@principal.example", Assert.Single(command.Mail.Cc).Address);

        var row = await harness.PrepareAsync(command);

        Assert.Equal(messageId, row.OriginalRetainedMessageId);
        Assert.Equal(StaffMailComposeMode.Reply, row.ComposeMode);
        Assert.Equal(InstructionMailboxId, row.MailboxId);
    }

    /// <summary>
    /// With no instruction to reply to, a new message leaves from the
    /// Principal's Send from mailbox, else from the default staff-send
    /// mailbox.
    /// </summary>
    [Theory]
    [InlineData("send-from", SendFromMailboxAddress)]
    [InlineData("default", null)]
    public async Task ANewMessageLeavesFromThePrincipalsSendFromMailboxElseTheDefault(
        string mailbox, string? sendFrom)
    {
        await using var harness = await Harness.CreateAsync(
            PrincipalReportSendingRules.Default with { SendFromMailbox = sendFrom });

        var command = await harness.SendAsync(harness.Request());

        Assert.Equal(StaffMailComposeMode.New, command.Mail.ComposeMode);
        Assert.Null(command.Mail.OriginalMessage);
        Assert.Equal(mailbox == "send-from" ? SendFromMailboxId : DefaultMailboxId, command.Mail.ApprovedMailboxId);
        Assert.Equal(3, command.Mail.ExpectedMailboxGeneration);
    }

    /// <summary>
    /// The instruction's own text answers a rule that asks whether it
    /// mentions something, so staff are not asked and the copy the rule adds
    /// is made.
    /// </summary>
    [Fact]
    public async Task AnInstructionThatMentionsARulesTextAnswersItsQuestion()
    {
        await using var harness = await Harness.CreateAsync(PrincipalReportSendingRules.Default with
        {
            Rules =
            [
                new(ReportSendingRuleMatch.All,
                    [new(ReportSendingConditionKind.Mentions, ["Car Claims"])],
                    new(["carclaims@principal.example"], []))
            ]
        });
        await harness.SeedRetainedInstructionAsync(cc: [], body: "Sent through the CarClaims portal.");

        var command = await harness.SendAsync(harness.Request() with { ReviewedRecipients = null });

        Assert.Equal("carclaims@principal.example", Assert.Single(command.Mail.Cc).Address);
        Assert.Empty(command.Mail.ReportDispatch!.Decisions);
    }

    /// <summary>
    /// The send records what staff decided on the form with its operation:
    /// their answers, the holds they ticked, the Stop they overrode with its
    /// reason, and the after-send list, which the prepared operation stores
    /// as its report dispatch.
    /// </summary>
    [Fact]
    public async Task TheSendRecordsTheAnswersTheHoldsTheOverriddenStopAndTheAfterSendTasks()
    {
        await using var harness = await Harness.CreateAsync(HeldRules);

        var command = await harness.SendAsync(harness.Request() with
        {
            Decisions = [new ReportRuleDecision(0, 0, true)],
            AcknowledgedHolds = [HoldText],
            StopOverrideReason = "  Agreed by phone.  "
        });

        var dispatch = command.Mail.ReportDispatch!;
        Assert.Equal([new ReportRuleDecision(0, 0, true)], dispatch.Decisions);
        Assert.Equal([HoldText], dispatch.Holds);
        Assert.Equal(StopText, dispatch.StopOverridden);
        Assert.Equal("Agreed by phone.", dispatch.StopOverrideReason);
        Assert.Equal(["Send the WhatsApp.", "Chase the images."], dispatch.AfterSendTasks);

        var row = await harness.PrepareAsync(command);

        using var stored = JsonDocument.Parse(row.ReportDispatchJson!);
        var root = stored.RootElement;
        var decision = Assert.Single(root.GetProperty("decisions").EnumerateArray());
        Assert.Equal(0, decision.GetProperty("ruleIndex").GetInt32());
        Assert.Equal(0, decision.GetProperty("conditionIndex").GetInt32());
        Assert.True(decision.GetProperty("holds").GetBoolean());
        Assert.Equal(HoldText, Assert.Single(root.GetProperty("holds").EnumerateArray()).GetString());
        Assert.Equal(StopText, root.GetProperty("stopOverridden").GetString());
        Assert.Equal("Agreed by phone.", root.GetProperty("stopOverrideReason").GetString());
        Assert.Equal(
            ["Send the WhatsApp.", "Chase the images."],
            root.GetProperty("afterSendTasks").EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    /// <summary>
    /// A send that does not apply the rule's Stop records no override, even
    /// when the form posted a reason.
    /// </summary>
    [Fact]
    public async Task AStopThatDoesNotApplyRecordsNoOverride()
    {
        await using var harness = await Harness.CreateAsync(HeldRules);

        var command = await harness.SendAsync(harness.Request() with
        {
            Decisions = [new ReportRuleDecision(0, 0, false)],
            AcknowledgedHolds = [HoldText],
            StopOverrideReason = "Agreed by phone."
        });

        var dispatch = command.Mail.ReportDispatch!;
        Assert.Null(dispatch.StopOverridden);
        Assert.Null(dispatch.StopOverrideReason);
        Assert.Equal(["Send the WhatsApp."], dispatch.AfterSendTasks);
    }

    /// <summary>
    /// Core refuses the send, and the transport is never reached, when the
    /// form was drawn from rules that have since changed, a question is
    /// unanswered, a Stop that applies has no reason, or a hold is not ticked.
    /// </summary>
    [Fact]
    public async Task TheSendIsRefusedUntilTheFormIsCurrentAnsweredOverriddenAndTicked()
    {
        await using var harness = await Harness.CreateAsync(HeldRules);
        var answered = harness.Request() with
        {
            Decisions = [new ReportRuleDecision(0, 0, true)],
            AcknowledgedHolds = [HoldText],
            StopOverrideReason = "Agreed by phone."
        };

        Assert.StartsWith(
            "Answer every question",
            await harness.RefusalAsync(answered with { Decisions = [] }),
            StringComparison.Ordinal);
        Assert.StartsWith(
            $"This delivery is stopped: {StopText}",
            await harness.RefusalAsync(answered with { StopOverrideReason = " " }),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Tick Done against every hold",
            await harness.RefusalAsync(answered with { AcknowledgedHolds = [] }),
            StringComparison.Ordinal);

        var drawn = answered with { DispatchFingerprint = await harness.FingerprintAsync() };
        await harness.SetRulesAsync(HeldRules with { Hold = "Check the WhatsApp group." });

        Assert.Equal(CaseReportDeliveryPolicy.DispatchChanged, await harness.RefusalAsync(drawn));
    }

    [Fact]
    public async Task ARequiredCompanionThatIsNotGeneratedRefusesTheSend()
    {
        await using var harness = await Harness.CreateAsync(PrincipalReportSendingRules.Default with
        {
            Attach = ReportSendingAttachments.Default with { VehicleImagesDocument = true }
        });

        Assert.Contains(
            CaseReportDeliveryPolicy.CompanionWords(CaseReportArtifactKind.ImagePack),
            await harness.RefusalAsync(harness.Request()),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A filed estimate goes with the report when the Principal's rules
    /// attach its format, and is re-read from its document version at the
    /// send boundary like every other attachment.
    /// </summary>
    [Fact]
    public async Task AFiledEstimateTheRulesAttachGoesWithTheReportAndMustStillMatchItsVersion()
    {
        await using var harness = await Harness.CreateAsync(PrincipalReportSendingRules.Default with
        {
            Attach = ReportSendingAttachments.Default with { Audatex = true }
        });
        var filed = await harness.SeedFiledEstimateAsync();

        var command = await harness.SendAsync(harness.Request());

        Assert.Equal(2, command.Mail.Attachments.Count);
        Assert.Equal(filed, command.Mail.Attachments[1]);
        Assert.Contains(filed, command.Report.Artifacts);
        var readiness = harness.Readiness();
        await readiness.RequireReadyAsync(command.Report, CancellationToken.None);

        await harness.TamperVersionAsync(filed.VersionId!.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => readiness.RequireReadyAsync(command.Report, CancellationToken.None));
    }

    private static async Task AssertThrowsAsyncAny<T1, T2>(Func<Task> action)
        where T1 : Exception
        where T2 : Exception
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is T1 or T2)
        {
            return;
        }

        Assert.Fail($"Expected {typeof(T1).Name} or {typeof(T2).Name}.");
    }

    /// <summary>
    /// Seeds one case at version 1 with an active staff edit lease, one
    /// Confirmed generation at version 1, and one Confirmed artifact joined
    /// to a real custody version row — the minimum a delivery re-reads — with
    /// its Principal's report sending rules and the default staff-send
    /// mailbox.
    /// </summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly LocalDbTestDatabase database;

        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid caseId,
            ActionActor staff,
            CaseEditLease lease,
            SeededArtifact artifact)
        {
            this.database = database;
            Factory = factory;
            CaseId = caseId;
            Staff = staff;
            Lease = lease;
            GenerationId = artifact.GenerationId;
            ArtifactVersionId = artifact.VersionId;
            Artifact = artifact;
            // A delivery reads the generation's rows; it never freezes a
            // snapshot or opens a document, so neither source is reached.
            Store = new EfCaseReportGenerationStore(
                factory,
                new UnusedSnapshotSource(),
                RecordingLogicalDocumentVersionReader.Refusing(),
                new FixedTimeProvider(StartUtc));
        }

        public PooledDbContextFactory<PegasusDbContext> Factory { get; }

        public Guid CaseId { get; }

        public ActionActor Staff { get; }

        public CaseEditLease Lease { get; }

        public Guid GenerationId { get; }

        public Guid ArtifactVersionId { get; }

        public SeededArtifact Artifact { get; }

        public EfCaseReportGenerationStore Store { get; }

        public static async Task<Harness> CreateAsync(PrincipalReportSendingRules? rules = null)
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                // Report delivery is a staff casework action. Keep this
                // persistence suite on a User actor so every positive and
                // stale/readiness refusal below exercises the broadened path.
                var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
                var caseId = await SeedCaseAsync(factory, rules ?? PrincipalReportSendingRules.Default);
                var artifact = await SeedConfirmedGenerationAsync(factory, caseId);
                var lease = await new AcquireCaseEditLease(
                        new EfCaseWorkflowStore(factory, new FixedTimeProvider(StartUtc)))
                    .ExecuteAsync(new(caseId, 1, staff, "lease-send-report"), CancellationToken.None);
                return new(database, factory, caseId, staff, lease, artifact);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        /// <summary>
        /// The form's fields as staff submit them. The dispatch fingerprint is
        /// left for <see cref="SendAsync"/> to read, as a form drawn now would
        /// carry it.
        /// </summary>
        public SendCaseReportRequest Request(
            long expectedCaseVersion = 1,
            long expectedGenerationVersion = 1,
            string leaseToken = "lease") => new(
            Staff,
            CaseId,
            expectedCaseVersion,
            leaseToken == "lease" ? Lease.Token : leaseToken,
            GenerationId,
            expectedGenerationVersion,
            "send-report-1",
            "Please find attached our report.",
            new(["handler@principal.example"], []));

        public Task<ReportDispatchFacts?> FactsAsync(CaseWorkSelector work = CaseWorkSelector.Current) =>
            new EfReportRecipientSuggestionQueries(Factory).GetAsync(CaseId, work, CancellationToken.None);

        /// <summary>The fingerprint a delivery form drawn now carries.</summary>
        public async Task<string> FingerprintAsync(CaseWorkSelector work = CaseWorkSelector.Current) =>
            (await FactsAsync(work))!.Fingerprint;

        /// <summary>
        /// One send through the real use case over this database: the real
        /// generation store, dispatch facts and send history, the approved
        /// mailboxes, and a transport that records the command it was handed
        /// and returns it.
        /// </summary>
        public async Task<StaffReportSendCommand> SendAsync(SendCaseReportRequest request)
        {
            var drawn = await DrawnAsync(request);
            var send = new RecordingSend();
            await UseCase(send).ExecuteAsync(drawn, CancellationToken.None);
            return Assert.Single(send.Commands);
        }

        /// <summary>A send Core refuses before the transport is reached; returns the refusal.</summary>
        public async Task<string> RefusalAsync(SendCaseReportRequest request)
        {
            var drawn = await DrawnAsync(request);
            var send = new RecordingSend();
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(
                () => UseCase(send).ExecuteAsync(drawn, CancellationToken.None));
            Assert.Empty(send.Commands);
            return refused.Message;
        }

        /// <summary>The send boundary's re-check over this database.</summary>
        public ReportSendReadiness Readiness() =>
            new(Store, new EfFiledEstimateAttachmentQueries(Factory));

        /// <summary>
        /// The staff-mail operation the send's command prepares, through the
        /// real store, read back as its row.
        /// </summary>
        public async Task<StaffMailSendOperationEntity> PrepareAsync(StaffReportSendCommand command)
        {
            var operation = await new EfStaffMailSendStore(Factory).PrepareAsync(
                command.Mail, new string('A', 64), StartUtc, CancellationToken.None);
            await using var context = await Factory.CreateDbContextAsync();
            return await context.Set<StaffMailSendOperationEntity>().AsNoTracking()
                .SingleAsync(item => item.Id == operation.Id);
        }

        private async Task<SendCaseReportRequest> DrawnAsync(SendCaseReportRequest request) =>
            request.DispatchFingerprint is not null
                ? request
                : request with { DispatchFingerprint = await FingerprintAsync(request.Work) };

        private SendCaseReport UseCase(IStaffReportSend send) => new(
            Store,
            new EfCaseReportSendHistoryQueries(Factory),
            new EfReportRecipientSuggestionQueries(Factory),
            new FixedMailboxes(),
            send,
            new FixedTimeProvider(StartUtc));

        public async Task SetGenerationStateAsync(string state)
        {
            await database.ExecuteAsync(
                $"UPDATE CaseReportGenerations SET State = '{state}' WHERE Id = '{GenerationId:D}'");
        }

        public async Task SetGenerationVersionAsync(long version)
        {
            await database.ExecuteAsync(
                $"UPDATE CaseReportGenerations SET [Version] = {version} WHERE Id = '{GenerationId:D}'");
        }

        public async Task SetArtifactStateAsync(string state)
        {
            await database.ExecuteAsync(
                $"UPDATE GeneratedCaseArtifacts SET State = '{state}'");
        }

        public async Task SupersedeGenerationAsync()
        {
            await database.ExecuteAsync(
                $"UPDATE CaseReportGenerations SET SupersededById = '{Guid.NewGuid():D}' WHERE Id = '{GenerationId:D}'");
        }

        /// <summary>
        /// Changes a confirmed version row's content length underneath the
        /// pinned attachment: the send boundary must refuse bytes that no
        /// longer match what the send named.
        /// </summary>
        public Task TamperVersionAsync(Guid versionId) => database.ExecuteAsync(
            $"UPDATE DocumentVersions SET ContentLength = 999 WHERE Id = '{versionId:D}'");

        /// <summary>A Case write after the page was read: the live version moves.</summary>
        public Task MoveCaseVersionAsync(long version) => database.ExecuteAsync(
            $"UPDATE CaseWorkflows SET [Version] = {version} WHERE CaseId = '{CaseId:D}'");

        /// <summary>An Administrator changes the Principal's report sending rules.</summary>
        public async Task SetRulesAsync(PrincipalReportSendingRules rules)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var principal = await context.Principals.SingleAsync(item => item.Code == "DVRP");
            principal.ReportSendingRulesJson = EfOrganizationAdministration.ToReportSendingJson(rules);
            await context.SaveChangesAsync();
        }

        /// <summary>The route decision could not resolve who sent the instruction.</summary>
        public Task UnresolveRouteSenderAsync() => database.ExecuteAsync(
            "UPDATE IntakeMailRouteDecisions SET EffectiveSenderAddress = NULL, EffectiveSenderSourceLabel = NULL");

        /// <summary>
        /// The origin receipt becomes a polled-mailbox one, with its retained
        /// message held in an approved mailbox. The retained row names a
        /// sender of its own, which the route decision does not.
        /// </summary>
        public async Task<Guid> SeedRetainedInstructionAsync(
            IReadOnlyList<string> cc, string body = "Please inspect the vehicle.")
        {
            await database.ExecuteAsync("UPDATE IntakeReceipts SET SourceChannel = 'mailbox'");
            await using var context = await Factory.CreateDbContextAsync();
            var token = await context.IntakeReceipts.Select(item => item.ExternalReceiptToken).SingleAsync();
            var mailboxId = await TestMailboxId.EnsureApprovedAsync(
                context, InstructionMailboxGraphId, InstructionMailboxAddress, StartUtc.AddDays(-1));
            var message = Retained(
                mailboxId, InstructionMailboxAddress, "inbox", token, "Claim for AB12 CDE", body, cc);
            context.RetainedMailboxMessages.Add(message);
            await context.SaveChangesAsync();
            return message.Id;
        }

        /// <summary>The origin receipt's e-mail, uploaded by staff: retained in no mailbox.</summary>
        public async Task SeedUploadedInstructionAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            var token = await context.IntakeReceipts.Select(item => item.ExternalReceiptToken).SingleAsync();
            context.RetainedMailboxMessages.Add(Retained(
                null, "uploaded@collisionengineers.example", "upload", token,
                "Uploaded claim", "Sent through the CarClaims portal.", []));
            await context.SaveChangesAsync();
        }

        private static RetainedMailboxMessageEntity Retained(
            Guid? mailboxId,
            string mailboxAddress,
            string folder,
            string token,
            string subject,
            string body,
            IReadOnlyList<string> cc) => new()
            {
                Id = Guid.NewGuid(),
                MailboxId = mailboxId,
                MailboxAddress = mailboxAddress,
                FolderScope = folder,
                FolderIdentity = folder,
                ImmutableMessageId = "immutable-instruction",
                InternetMessageIdentity = "<instruction@principal.example>",
                ConversationIdentity = "conversation-instruction",
                ExternalReceiptToken = token,
                SenderAddress = "someone-else@principal.example",
                ToAddressesJson = "[]",
                CcAddressesJson = JsonSerializer.Serialize(cc, SnapshotJsonOptions),
                Subject = subject,
                BodyPlainText = body,
                SourceLength = 1,
                SourceSha256 = new string('A', 64),
                ReceivedAtUtc = StartUtc,
                RetainedAtUtc = StartUtc
            };

        /// <summary>The intake read the registration as a fact and suggested another.</summary>
        public async Task SeedRegistrationAsync(string fact, string suggestion)
        {
            await using var context = await Factory.CreateDbContextAsync();
            context.Set<CaseDataSnapshotEntity>().Add(new()
            {
                WorkId = CaseId,
                CompletenessPolicyKey = "delivery-test",
                CompletenessPolicyVersion = 1,
                CompletenessPolicySatisfied = true,
                AcceptedAtUtc = StartUtc,
                Fields =
                [
                    Registration(CaseDataCodes.Fact, fact),
                    Registration(CaseDataCodes.Suggestion, suggestion)
                ]
            });
            await context.SaveChangesAsync();
        }

        /// <summary>Staff confirm the registration.</summary>
        public async Task ConfirmRegistrationAsync(string value)
        {
            await using var context = await Factory.CreateDbContextAsync();
            context.Set<CaseDataFieldEntity>().Add(Registration(CaseDataCodes.Confirmed, value));
            await context.SaveChangesAsync();
        }

        private CaseDataFieldEntity Registration(string kind, string value) => new()
        {
            WorkId = CaseId,
            FieldName = CaseDataFieldNames.VehicleRegistration,
            ValueKind = kind,
            ValueType = CaseDataCodes.Text,
            Value = value,
            SourceKind = kind == CaseDataCodes.Confirmed ? "staff_correction" : CaseDataCodes.IntakeEvidence,
            SourceIdentity = $"delivery-test:{kind}",
            SourceLabel = "Report delivery test",
            PolicyKey = "delivery-test",
            PolicyVersion = 1,
            ConfirmedByActor = kind == CaseDataCodes.Confirmed ? Staff.SubjectId : null,
            ConfirmedAtUtc = kind == CaseDataCodes.Confirmed ? StartUtc : null
        };

        /// <summary>
        /// A file filed on the Case, which the Worker did or did not read as
        /// an estimate. Each document takes the Case's next free ordinal.
        /// </summary>
        public async Task<StaffMailAttachment> SeedFiledEstimateAsync(
            bool? recognised = true,
            string? provider = ReportDispatchPolicy.AudatexProvider,
            bool removed = false,
            bool current = true,
            DocumentCustodyStatus custody = DocumentCustodyStatus.Confirmed,
            DocumentSource source = DocumentSource.Intake)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var ordinal = 1 + (await context.Set<CaseDocumentEntity>()
                .Where(item => item.CaseId == CaseId)
                .MaxAsync(item => (int?)item.Ordinal) ?? 0);
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            var sha256 = new string('e', 64);
            var fileName = $"estimate-{documentId:N}.pdf";
            context.AddRange(
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = CaseId,
                    Ordinal = ordinal,
                    SourceOccurrenceIdentity = $"estimate:{documentId:N}"
                },
                new DocumentVersionEntity
                {
                    Id = versionId,
                    DocumentId = documentId,
                    Version = 1,
                    FileName = fileName,
                    MediaType = "application/pdf",
                    ContentLength = 500,
                    Sha256 = sha256,
                    CustodyStatus = custody,
                    CreatedAtUtc = StartUtc,
                    CreatedBy = "Intake",
                    IsCurrent = current,
                    IsLogicallyRemoved = removed,
                    RemovalReason = removed ? "Filed in error" : null,
                    RemovalOperationKey = removed ? $"remove-{versionId:N}" : null,
                    IsRecognisedEstimate = recognised,
                    RecognisedEstimateProvider = provider
                },
                new DocumentOccurrenceEntity
                {
                    Id = occurrenceId,
                    CaseId = CaseId,
                    DocumentId = documentId,
                    VersionId = versionId,
                    Ordinal = ordinal,
                    SemanticRole = DocumentSemanticRole.Other,
                    Source = source,
                    SourceOccurrenceIdentity = $"estimate:{occurrenceId:N}",
                    RecordedAtUtc = StartUtc,
                    OperationKey = $"seed:{occurrenceId:N}"
                });
            await context.SaveChangesAsync();
            return new StaffMailAttachment(documentId, versionId, sha256, 500, fileName, "application/pdf");
        }

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        private static async Task<Guid> SeedCaseAsync(
            PooledDbContextFactory<PegasusDbContext> factory,
            PrincipalReportSendingRules rules)
        {
            await using var context = await factory.CreateDbContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Id = organizationId, Name = "Report delivery test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = StartUtc },
                new PrincipalEntity
                {
                    Id = principalId,
                    OrganizationId = organizationId,
                    SequenceLineageId = lineageId,
                    Code = "DVRP",
                    IsActive = true,
                    ReportGenerationPolicy = "Pegasus",
                    ReportSendingRulesJson = EfOrganizationAdministration.ToReportSendingJson(rules),
                    Version = 0
                },
                // The approved mailbox a new message leaves from when the
                // Principal names none.
                new ApprovedMailboxEntity
                {
                    Id = DefaultMailboxId,
                    Address = DefaultMailboxAddress,
                    AllowStaffSend = true,
                    AllowSentEvidence = true,
                    IsDefaultStaffSend = true,
                    MailboxGeneration = 3,
                    VerifiedEncodedMessageSizeLimit = 10_000_000,
                    State = ApprovedMailboxState.Approved.ToString(),
                    MailboxIdentity = "delivery-default-mailbox",
                    InboxFolderIdentity = "inbox",
                    SentFolderIdentity = "sent",
                    ActivatedAtUtc = StartUtc.AddDays(-1),
                    Version = 1
                },
                new IntakeReceiptEntity
                {
                    Id = receiptId,
                    SourceFileName = "delivery-origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('0', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"delivery:{receiptId:N}",
                    ReceivedAtUtc = StartUtc,
                    ProcessedAtUtc = StartUtc,
                    SourceReaderKey = "delivery-test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "Report delivery test",
                    EvidenceJson = "[]",
                    FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                },
                new IntakeMailRouteDecisionEntity
                {
                    IntakeReceiptId = receiptId,
                    Disposition = "accepted",
                    PredicatesJson = "[]",
                    Reason = "Report delivery origin sender",
                    PolicyKey = "delivery-test",
                    PolicyVersion = 1,
                    TransportIdentitiesJson = "[]",
                    OriginalIdentitiesJson = "[]",
                    EffectiveSenderAddress = RouteSender,
                    EffectiveSenderSourceLabel = "original-header"
                },
                new CaseEntity
                {
                    Id = caseId,
                    PrincipalId = principalId,
                    SequenceLineageId = lineageId,
                    Year = 2031,
                    Sequence = 1,
                    Reference = "DVR-31001",
                    Type = "inspection",
                    InitialState = "NotReady",
                    CustodyState = "confirmed",
                    OriginIntakeReceiptId = receiptId,
                    CreatedAtUtc = StartUtc,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEntity
                {
                    CaseId = caseId,
                    State = "ReportPreparation",
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
            return caseId;
        }

        private static async Task<SeededArtifact> SeedConfirmedGenerationAsync(
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid caseId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var generationId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            var content = "report-delivery"u8.ToArray();
            var sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));
            var input = AssessmentReportDraftWebTests.ReadyInput(caseId);
            var report = AssessmentReportProjection.Project(input).Snapshot!;
            var snapshot = new CaseReportGenerationSnapshot(
                caseId, 1, "DVR-31001", "send-report-1", CaseReportActor.None, StartUtc,
                Guid.Empty, new string('0', 64), "image/png", input.CurrentEstimate!.SpecificationId, input.CurrentEstimate.Version,
                report.Costs, report.EngineerValue, Guid.Empty, report.Content, report.Guides,
                report.ReportDate, report.ReportDateOverridden, report.AgreedFee,
                report.FeeDescriptionLines, [], [], report.PayloadVersion, "renderer/v1", report with { Photos = [] })
            {
                CurrentEstimate = input.CurrentEstimate
            };
            context.AddRange(
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = caseId,
                    Ordinal = 1,
                    SourceOccurrenceIdentity = $"delivery:{documentId:N}"
                },
                new DocumentVersionEntity
                {
                    Id = versionId,
                    DocumentId = documentId,
                    Version = 1,
                    FileName = "report.pdf",
                    MediaType = "application/pdf",
                    ContentLength = content.LongLength,
                    Sha256 = sha256,
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = StartUtc,
                    CreatedBy = "Staff:test",
                    IsCurrent = true
                },
                new DocumentOccurrenceEntity
                {
                    Id = occurrenceId,
                    CaseId = caseId,
                    DocumentId = documentId,
                    VersionId = versionId,
                    SemanticRole = DocumentSemanticRole.EngineerReport,
                    Source = DocumentSource.Generated,
                    SourceOccurrenceIdentity = $"delivery:{occurrenceId:N}",
                    RecordedAtUtc = StartUtc,
                    OperationKey = $"seed:{occurrenceId:N}"
                },
                new CaseReportGenerationEntity
                {
                    Id = generationId,
                    CaseId = caseId,
                    WorkId = caseId,
                    CaseVersion = 1,
                    SnapshotHash = new string('1', 64),
                    SnapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions),
                    TemplateVersion = "assessment-report/v1",
                    RendererVersion = "renderer/v1",
                    State = nameof(CaseReportGenerationState.Confirmed),
                    GeneratedAtUtc = StartUtc,
                    Version = 1
                },
                new GeneratedCaseArtifactEntity
                {
                    Id = Guid.NewGuid(),
                    GenerationId = generationId,
                    VersionId = versionId,
                    Kind = nameof(CaseReportArtifactKind.AssessmentReport),
                    Sha256 = sha256,
                    State = nameof(CaseReportArtifactStatus.Confirmed),
                    OperationKey = "artifact-1"
                });
            await context.SaveChangesAsync();
            return new(generationId, versionId, documentId, sha256, content.LongLength);
        }

        internal sealed record SeededArtifact(
            Guid GenerationId, Guid VersionId, Guid DocumentId, string Sha256, long ContentLength);

        private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => utcNow;
        }
    }

    private sealed class UnusedSnapshotSource : ICaseReportSnapshotSource
    {
        public Task<CaseReportFreezeInputs?> GetAsync(
            Guid caseId,
            ActionActor actor,
            CaseWorkSelector work,
            ReportProjectionReuse? reuse,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("A delivery freezes no snapshot.");
    }

    /// <summary>
    /// The approved mailboxes a report leaves from, each bound for staff send
    /// and report-sent evidence: the default staff-send mailbox, the one that
    /// holds the instruction, and a Principal's own Send from mailbox.
    /// </summary>
    private sealed class FixedMailboxes : IApprovedMailboxStore
    {
        private static readonly IReadOnlyList<ApprovedMailbox> Mailboxes =
        [
            Mailbox(DefaultMailboxId, DefaultMailboxAddress),
            Mailbox(InstructionMailboxId, InstructionMailboxAddress),
            Mailbox(SendFromMailboxId, SendFromMailboxAddress)
        ];

        private static ApprovedMailbox Mailbox(Guid id, string address) => new(
            id,
            address,
            [ApprovedMailboxRouteScope.StaffSend, ApprovedMailboxRouteScope.SentEvidence],
            ApprovedMailboxState.Approved,
            "identity",
            "inbox",
            "sent",
            IdentityIsBound: true,
            ActivatedAtUtc: StartUtc,
            Version: 1,
            FolderBindings: [],
            Generation: 3);

        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Mailboxes);

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApprovedMailbox> SetDefaultAsync(
            SetDefaultApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class RecordingSend : IStaffReportSend
    {
        public List<StaffReportSendCommand> Commands { get; } = [];

        public Task<StaffMailOperation> SendAsync(
            StaffReportSendCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(new StaffMailOperation(
                Guid.NewGuid(),
                StaffMailState.Unknown,
                null,
                1,
                StartUtc,
                null,
                null,
                null,
                command.Mail.ApprovedMailboxId,
                command.Mail.ExpectedMailboxGeneration,
                new string('d', 64),
                null,
                null,
                command.Mail.Purpose,
                command.Mail.ContextId,
                command.Mail.ExpectedContextVersion,
                null));
        }
    }
}
