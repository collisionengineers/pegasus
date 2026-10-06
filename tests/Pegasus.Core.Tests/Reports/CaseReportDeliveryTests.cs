using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The report delivery policy and its one send use case. Generation is not
/// delivery — nothing here records a Sent state, transport observation stays
/// with Stream A, and every rule reads persisted structured facts. The
/// store's own lease and work checks are
/// <c>CaseReportGenerationPersistenceTests</c>'s job.
/// </summary>
public sealed class CaseReportDeliveryTests
{
    private static readonly DateTimeOffset GeneratedAtUtc = new(2026, 9, 6, 11, 0, 0, TimeSpan.Zero);

    private static readonly StaffMailAttachment ReportAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), 120, "report.pdf", "application/pdf");

    private static readonly StaffMailAttachment FeeAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('b', 64), 60, "fee-note.pdf", "application/pdf");

    [Fact]
    public void AddressingUsesConfiguredRecipientsAndTheOriginalInstructionSender()
    {
        var addressing = CaseReportDeliveryPolicy.Address(Suggestions(
            ["handler@principal.example"],
            includeOriginalInstructionSender: true,
            originalInstructionSender: "instructor@insurer.example"));

        Assert.Equal(["instructor@insurer.example", "handler@principal.example"],
            addressing.To.Select(item => item.Address));
        Assert.Empty(addressing.Cc);
        Assert.Equal("DVR-31001", addressing.Subject);
    }

    [Fact]
    public void AddressingDeduplicatesTheOriginalInstructionSender()
    {
        var addressing = CaseReportDeliveryPolicy.Address(Suggestions(
            ["handler@principal.example"],
            includeOriginalInstructionSender: true,
            originalInstructionSender: "Handler@Principal.Example"));

        Assert.Equal("Handler@Principal.Example", Assert.Single(addressing.To).Address);
        Assert.Empty(addressing.Cc);
    }

    [Fact]
    public void AddressingRefusesACaseWithNoConfiguredRecipient()
    {
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.Address(Suggestions([])));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.Address(Suggestions(
                [], includeOriginalInstructionSender: true)));
    }

    [Fact]
    public void ReviewedAddressingKeepsStaffChosenRecipients()
    {
        var addressing = CaseReportDeliveryPolicy.ReviewedAddress(
            Suggestions(["suggested@principal.example"]),
            new(["reviewed@recipient.example"], ["copy@recipient.example"]));

        Assert.Equal("reviewed@recipient.example", Assert.Single(addressing.To).Address);
        Assert.Equal("copy@recipient.example", Assert.Single(addressing.Cc).Address);
    }

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

    [Fact]
    public async Task ReadinessFailsClosedWhenTheGenerationIsMissing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReportSendReadiness(
                new FixedGenerations(null))
            .RequireReadyAsync(ReadyRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task DeliveryIsAStaffActAndRefusesOtherActors()
    {
        var generations = new FixedGenerations(Generation());
        var send = new RecordingSend();
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => Use(generations, send)
            .ExecuteAsync(
                Request() with { Actor = ActionActor.SystemWorker("delivery-test") },
                CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new ReportSendReadiness(generations)
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
    /// the caller's operation key. Its returned state is the outcome as is.
    /// </summary>
    [Fact]
    public async Task SendHandsStreamAOneCommandAndReturnsTheTransportStateAsIs()
    {
        var generations = new FixedGenerations(Generation());
        var send = new RecordingSend();

        var returned = await Use(generations, send).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(StaffMailState.Unknown, returned.State);
        var delivery = Assert.Single(generations.Deliveries);
        Assert.Equal("lease", delivery.LeaseToken);
        Assert.Equal(4, delivery.ExpectedCaseVersion);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailPurpose.CaseReport, command.Mail.Purpose);
        Assert.Equal(StaffMailComposeMode.New, command.Mail.ComposeMode);
        Assert.Equal("send-1", command.Mail.OperationKey);
        Assert.Equal("DVR-31001", command.Mail.Subject);
        Assert.Equal("handler@principal.example", Assert.Single(command.Mail.To).Address);
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
    }

    /// <summary>
    /// v28 P23: one dot is added to the report's name for each report of the
    /// work already sent.
    /// </summary>
    [Fact]
    public async Task SendNamesAReissueWithOneDotForEachReportAlreadySent()
    {
        var send = new RecordingSend();
        var sendReport = new SendCaseReport(
            new FixedGenerations(Generation()),
            new FixedSendHistory(new(2, new DateOnly(2026, 9, 1))),
            new FixedSuggestions(Suggestions(["handler@principal.example"])),
            new FixedMailboxes(Mailbox()),
            send);

        await sendReport.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(
            "DVR-31001 PK12 TMZ Repairable report...pdf",
            Assert.Single(send.Commands).Mail.Attachments[0].FileName);
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
    /// Stream A review (blocker 2): a mailbox bound only for Sent-evidence
    /// observation cannot send — the report journey needs the StaffSend
    /// capability on the same approved, identified mailbox.
    /// </summary>
    [Fact]
    public async Task SendFailsClosedWithoutTheStaffSendScope()
    {
        var send = new RecordingSend();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Use(
                new FixedGenerations(Generation()),
                send,
                Mailbox(scopes: [ApprovedMailboxRouteScope.SentEvidence]))
            .ExecuteAsync(Request(), CancellationToken.None));
        Assert.Empty(send.Commands);
    }

    [Fact]
    public async Task SendFailsClosedWithoutExactlyOneSentEvidenceMailbox()
    {
        var send = new RecordingSend();
        var sendReport = new SendCaseReport(
            new FixedGenerations(Generation()),
            new FixedSendHistory(CaseReportSendHistory.None),
            new FixedSuggestions(Suggestions(["handler@principal.example"])),
            new FixedMailboxes(),
            send);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sendReport.ExecuteAsync(Request(), CancellationToken.None));
        Assert.Empty(send.Commands);
    }

    private static SendCaseReport Use(
        FixedGenerations generations, RecordingSend send, ApprovedMailbox? mailbox = null) => new(
        generations,
        new FixedSendHistory(CaseReportSendHistory.None),
        new FixedSuggestions(Suggestions(["handler@principal.example"])),
        new FixedMailboxes(mailbox ?? Mailbox()),
        send);

    private static ActionActor Staff() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    private static SendCaseReportRequest Request() => new(
        Staff(), CaseId, 4, "lease", GenerationId, 1, "send-1", "Message");

    private static ReportSendReadinessRequest ReadyRequest(ActionActor? actor = null) => new(
        actor ?? Staff(), CaseId, GenerationId, 1, [ReportAttachment]);

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
        var attachment = kind == CaseReportArtifactKind.FeeNote ? FeeAttachment : ReportAttachment;
        return new(
            Guid.NewGuid(), GenerationId, kind, status, "artifact-1",
            attachment.DocumentId, attachment.VersionId, attachment.Sha256,
            attachment.ContentLength, attachment.FileName, attachment.MediaType,
            "box-file", "box-version", null, null);
    }

    private static ApprovedMailbox Mailbox(
        IReadOnlyList<ApprovedMailboxRouteScope>? scopes = null,
        int version = 5,
        long generation = 3) => new(
        Guid.NewGuid(), "reports@collisionengineers.example",
        scopes ?? [ApprovedMailboxRouteScope.StaffSend, ApprovedMailboxRouteScope.SentEvidence],
        ApprovedMailboxState.Approved,
        "identity", "inbox", "sent", IdentityIsBound: true, ActivatedAtUtc: GeneratedAtUtc, version, [],
        Generation: generation);

    private static ReportRecipientSuggestions Suggestions(
        IReadOnlyList<string> additionalAddresses,
        bool includeOriginalInstructionSender = false,
        string? originalInstructionSender = null) => new(
        "DVR-31001",
        PrincipalReportRecipientSettings.Normalize(
            includeOriginalInstructionSender, additionalAddresses),
        originalInstructionSender);

    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid GenerationId = Guid.NewGuid();

    private sealed class FixedSendHistory(CaseReportSendHistory history) : ICaseReportSendHistoryQueries
    {
        public Task<CaseReportSendHistory> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult(history);
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

    private sealed class FixedSuggestions(ReportRecipientSuggestions suggestions)
        : IReportRecipientSuggestionQueries
    {
        public Task<ReportRecipientSuggestions?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<ReportRecipientSuggestions?>(caseId == CaseId ? suggestions : null);
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
