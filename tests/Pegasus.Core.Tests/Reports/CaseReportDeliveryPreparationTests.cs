using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// CASE-047 B07: the delivery-preparation policy and use cases. Generation is
/// not delivery — nothing here records a Sent state, transport observation
/// stays with Stream A, and every rule reads persisted structured facts the
/// operator never types into a preparation form. The store's own
/// transaction/replay/conflict behaviour is
/// <c>CaseReportDeliveryPreparationPersistenceTests</c>'s job.
/// </summary>
public sealed class CaseReportDeliveryPreparationTests
{
    private static readonly DateTimeOffset PreparedAtUtc = new(2026, 9, 6, 11, 0, 0, TimeSpan.Zero);

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
    public void AddressingIsRefusedWhenTheContactsChangedSincePreparation()
    {
        var prepared = Suggestions(["handler@principal.example"]);
        var current = Suggestions(["reassigned@principal.example"]);

        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireSuggestionCurrent(
                prepared.Fingerprint, current.Fingerprint));
    }

    [Fact]
    public void ReviewedAddressingFreezesStaffChosenRecipients()
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
    public async Task ReadinessRequiresTheExactPreparationAndItsFacts()
    {
        var record = Record();
        var request = ReadyRequest();
        CaseReportDeliveryPolicy.RequireReady(request, record);

        await Task.CompletedTask;
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request with { CaseId = Guid.NewGuid() }, record));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request with { GenerationId = Guid.NewGuid() }, record));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(
                request with { ExpectedPreparationVersion = 2 }, record));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request with { Artifacts = [FeeAttachment] }, record));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request with { Artifacts = [] }, record));
    }

    [Fact]
    public void ReadinessRefusesAnAttachmentThatNoLongerMatchesItsConfirmedArtifact()
    {
        var record = Record(confirmed: [ReportAttachment, FeeAttachment]);
        var tampered = FeeAttachment with { Sha256 = new string('c', 64) };
        var request = ReadyRequest() with { Artifacts = [ReportAttachment, tampered] };

        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request, record));
    }

    /// <summary>
    /// Stream A review (multiplicity): a request that duplicates one
    /// artifact and omits another has the same count as the pin and every
    /// item contained — a count-plus-contains check would pass it and send
    /// without the fee note. Exact identity equality refuses it before the
    /// transport is ever invoked.
    /// </summary>
    [Fact]
    public void ReadinessRefusesADuplicatedArtifactThatOmitsAnother()
    {
        var pinned = new[] { ReportAttachment, FeeAttachment };
        var record = Record(pinned: pinned, confirmed: pinned);
        var request = ReadyRequest() with { Artifacts = [ReportAttachment, ReportAttachment] };

        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireReady(request, record));
    }

    [Fact]
    public async Task DeliveryIsAStaffActAndRefusesOtherActors()
    {
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new PrepareCaseReportDelivery(
                new RefusingStore(), new RefusingSuggestions())
            .ExecuteAsync(
                new(ActionActor.SystemWorker("delivery-test"), Guid.NewGuid(), 1, "lease", Guid.NewGuid(), 1,
                    "prepare-1"),
                CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new ReportSendReadiness(
                new RefusingStore())
            .RequireReadyAsync(ReadyRequest(ActionActor.SystemWorker("delivery-test")), CancellationToken.None));
    }

    [Fact]
    public async Task PreparationRequiresItsIdentifiersAndFailsClosedOnAMissingCase()
    {
        var prepare = new PrepareCaseReportDelivery(
            new RefusingStore(), new FixedSuggestions(Suggestions(["handler@principal.example"])));
        var actor = Staff();

        await Assert.ThrowsAsync<ArgumentException>(() => prepare.ExecuteAsync(
            new(actor, Guid.NewGuid(), 1, " ", Guid.NewGuid(), 1, "prepare-1"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => prepare.ExecuteAsync(
            new(actor, Guid.NewGuid(), 1, "lease", Guid.Empty, 1, "prepare-1"), CancellationToken.None));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => prepare.ExecuteAsync(
            new(actor, Guid.NewGuid(), 1, "lease", Guid.NewGuid(), 1, "prepare-1"), CancellationToken.None));
    }

    [Fact]
    public async Task SendHandsStreamAOneCommandAndReturnsTheTransportStateAsIs()
    {
        var operation = Operation(StaffMailState.Unknown);
        var send = new RecordingSend { Result = operation };
        var sendPrepared = new SendPreparedCaseReport(
            new FixedStore(Record()),
            new FixedSuggestions(Suggestions(["handler@principal.example"])),
            new FixedMailboxes(Mailbox()),
            new ReportSendReadiness(new FixedStore(Record())),
            send);

        var returned = await sendPrepared.ExecuteAsync(
            new(Staff(), CaseId, PreparationId, 1, "send-1"), CancellationToken.None);

        Assert.Equal(StaffMailState.Unknown, returned.State);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailPurpose.CaseReport, command.Mail.Purpose);
        Assert.Equal(StaffMailComposeMode.New, command.Mail.ComposeMode);
        Assert.Equal("send-1", command.Mail.OperationKey);
        Assert.Equal("handler@principal.example", Assert.Single(command.Mail.To).Address);
        Assert.Equal([ReportAttachment], command.Mail.Attachments);
        Assert.Equal(command.Report.PreparationId, PreparationId);
        // A03's report context is the immutable generation: the transport
        // re-checks the generation identity and version, not the Case's.
        Assert.Equal(GenerationId, command.Mail.ContextId);
        Assert.Equal(1, command.Mail.ExpectedContextVersion);
        Assert.Equal(1, command.Report.ExpectedCaseVersion);
    }

    [Fact]
    public async Task SendUsesTheFrozenReviewedRecipientInsteadOfReplacingItWithSuggestions()
    {
        var send = new RecordingSend();
        var reviewed = new CaseReportDeliveryAddressing(
            [new("reviewed@recipient.example", null)],
            [new("copy@recipient.example", null)],
            "DVR-31001");
        var sendPrepared = new SendPreparedCaseReport(
            new FixedStore(Record(addressing: reviewed)),
            new FixedSuggestions(Suggestions(["handler@principal.example"])),
            new FixedMailboxes(Mailbox()),
            new ReportSendReadiness(new FixedStore(Record(addressing: reviewed))),
            send);

        await sendPrepared.ExecuteAsync(
            new(Staff(), CaseId, PreparationId, 1, "send-reviewed"), CancellationToken.None);

        var command = Assert.Single(send.Commands);
        Assert.Equal("reviewed@recipient.example", Assert.Single(command.Mail.To).Address);
        Assert.Equal("copy@recipient.example", Assert.Single(command.Mail.Cc).Address);
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
        var sendPrepared = new SendPreparedCaseReport(
            new FixedStore(Record()),
            new FixedSuggestions(Suggestions(["handler@principal.example"])),
            new FixedMailboxes(Mailbox(version: 5, generation: 3)),
            new ReportSendReadiness(new FixedStore(Record())),
            send);

        await sendPrepared.ExecuteAsync(
            new(Staff(), CaseId, PreparationId, 1, "send-1"), CancellationToken.None);

        var command = Assert.Single(send.Commands);
        Assert.Equal(3, command.Mail.ExpectedMailboxGeneration);
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
        var sendPrepared = new SendPreparedCaseReport(
            new FixedStore(Record()),
            new FixedSuggestions(Suggestions(["handler@principal.example"])),
            new FixedMailboxes(Mailbox(scopes: [ApprovedMailboxRouteScope.SentEvidence])),
            new ReportSendReadiness(new FixedStore(Record())),
            send);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sendPrepared.ExecuteAsync(
            new(Staff(), CaseId, PreparationId, 1, "send-1"), CancellationToken.None));
        Assert.Empty(send.Commands);
    }

    /// <summary>
    /// Stream A review: the preparation froze the Case version it was made
    /// at; any later Case mutation — even one that leaves the addressing and
    /// artifacts intact — must refuse the send rather than deliver stale
    /// bytes, and the transport is never invoked.
    /// </summary>
    [Fact]
    public async Task SendRefusesWhenTheCaseMovedAfterPreparation()
    {
        var send = new RecordingSend();
        var sendPrepared = new SendPreparedCaseReport(
            new FixedStore(Record(frozenCaseVersion: 1, currentCaseVersion: 2)),
            new FixedSuggestions(Suggestions(["handler@principal.example"])),
            new FixedMailboxes(Mailbox()),
            new ReportSendReadiness(new FixedStore(Record(frozenCaseVersion: 1, currentCaseVersion: 2))),
            send);

        await Assert.ThrowsAsync<CaseVersionConflictException>(() => sendPrepared.ExecuteAsync(
            new(Staff(), CaseId, PreparationId, 1, "send-1"), CancellationToken.None));
        Assert.Empty(send.Commands);
    }

    [Fact]
    public async Task SendFailsClosedWithoutExactlyOneSentEvidenceMailbox()
    {
        var send = new RecordingSend();
        var sendPrepared = new SendPreparedCaseReport(
            new FixedStore(Record()),
            new FixedSuggestions(Suggestions(["handler@principal.example"])),
            new FixedMailboxes(),
            new ReportSendReadiness(new FixedStore(Record())),
            send);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sendPrepared.ExecuteAsync(
            new(Staff(), CaseId, PreparationId, 1, "send-1"), CancellationToken.None));
        Assert.Empty(send.Commands);
    }

    [Fact]
    public async Task SendRefusesWhenTheAddressingChangedSincePreparation()
    {
        var send = new RecordingSend();
        var sendPrepared = new SendPreparedCaseReport(
            new FixedStore(Record()),
            // Principal recipient suggestions changed after preparation, so
            // the stale intent is refused even though its reviewed recipients
            // stay immutable.
            new FixedSuggestions(Suggestions(["reassigned@principal.example"])),
            new FixedMailboxes(Mailbox()),
            new ReportSendReadiness(new FixedStore(Record())),
            send);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sendPrepared.ExecuteAsync(
            new(Staff(), CaseId, PreparationId, 1, "send-1"), CancellationToken.None));
        Assert.Empty(send.Commands);
    }

    [Fact]
    public async Task ReadinessFailsClosedWhenThePreparationIsMissing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReportSendReadiness(
                new FixedStore(null))
            .RequireReadyAsync(ReadyRequest(), CancellationToken.None));
    }

    private static ActionActor Staff() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    private static ReportSendReadinessRequest ReadyRequest(ActionActor? actor = null) => new(
        actor ?? Staff(), CaseId, 1, GenerationId, 1, PreparationId, 1, [ReportAttachment]);

    private static CaseReportDeliveryPreparationRecord Record(
        IReadOnlyList<StaffMailAttachment>? pinned = null,
        IReadOnlyList<StaffMailAttachment>? confirmed = null,
        long frozenCaseVersion = 1,
        long currentCaseVersion = 1,
        CaseReportDeliveryAddressing? addressing = null) => new(
        new CaseReportDeliveryPreparation(
            PreparationId, CaseId, GenerationId, 1, 1,
            pinned ?? [ReportAttachment], Staff(), PreparedAtUtc,
            Suggestions(["handler@principal.example"]).Fingerprint),
        addressing ?? new CaseReportDeliveryAddressing(
            [new("handler@principal.example", null)], [], "DVR-31001"),
        frozenCaseVersion,
        currentCaseVersion,
        CaseReportGenerationState.Confirmed,
        GenerationIsCurrent: true,
        CurrentGenerationVersion: 1,
        confirmed ?? pinned ?? [ReportAttachment]);

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

    private static StaffMailOperation Operation(StaffMailState state) => new(
        Guid.NewGuid(), state, null, 1, PreparedAtUtc, null, null, null,
        Guid.NewGuid(), 1, new string('d', 64), null, null,
        StaffMailPurpose.CaseReport, PreparationId, 1, null);

    private static ApprovedMailbox Mailbox(
        IReadOnlyList<ApprovedMailboxRouteScope>? scopes = null,
        int version = 5,
        long generation = 3) => new(
        Guid.NewGuid(), "reports@collisionengineers.example",
        scopes ?? [ApprovedMailboxRouteScope.StaffSend, ApprovedMailboxRouteScope.SentEvidence],
        ApprovedMailboxState.Approved,
        "identity", "inbox", "sent", IdentityIsBound: true, ActivatedAtUtc: PreparedAtUtc, version, [],
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
    private static readonly Guid PreparationId = Guid.NewGuid();

    private sealed class RefusingStore : ICaseReportDeliveryPreparationStore
    {
        public Task<CaseReportDeliveryPreparationRecord> PrepareAsync(
            PrepareCaseReportDeliveryCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportDeliveryPreparationRecord?> GetAsync(
            ActionActor actor, Guid caseId, Guid preparationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportDeliveryPreparationRecord?> GetCurrentAsync(
            ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RefusingSuggestions : IReportRecipientSuggestionQueries
    {
        public Task<ReportRecipientSuggestions?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedStore(CaseReportDeliveryPreparationRecord? record)
        : ICaseReportDeliveryPreparationStore
    {
        public Task<CaseReportDeliveryPreparationRecord> PrepareAsync(
            PrepareCaseReportDeliveryCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportDeliveryPreparationRecord?> GetAsync(
            ActionActor actor, Guid caseId, Guid preparationId, CancellationToken cancellationToken) =>
            Task.FromResult(record is not null && caseId == CaseId && preparationId == PreparationId
                ? record
                : null);

        public Task<CaseReportDeliveryPreparationRecord?> GetCurrentAsync(
            ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult(record);
    }

    private sealed class FixedSuggestions(ReportRecipientSuggestions suggestions)
        : IReportRecipientSuggestionQueries
    {
        public Task<ReportRecipientSuggestions?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
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

        public StaffMailOperation Result { get; init; } = new(
            Guid.NewGuid(), StaffMailState.Unknown, null, 1, PreparedAtUtc, null, null, null,
            Guid.NewGuid(), 1, new string('d', 64), null, null,
            StaffMailPurpose.CaseReport, PreparationId, 1, null);

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
