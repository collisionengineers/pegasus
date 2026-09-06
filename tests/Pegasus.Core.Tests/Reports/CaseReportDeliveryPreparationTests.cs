using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
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
    public void AddressingResolvesTheContactAndCopiesADifferentClaimSourceAddress()
    {
        var addressing = CaseReportDeliveryPolicy.Address(Projection(
            contactEmail: "handler@principal.example",
            contactName: "Principal Handler",
            claimSourceEmail: "instructor@insurer.example",
            claimSourceName: "Insurer Instructor"));

        var to = Assert.Single(addressing.To);
        Assert.Equal("handler@principal.example", to.Address);
        Assert.Equal("Principal Handler", to.DisplayName);
        var cc = Assert.Single(addressing.Cc);
        Assert.Equal("instructor@insurer.example", cc.Address);
        Assert.Equal("Insurer Instructor", cc.DisplayName);
        Assert.Equal("DVR-31001", addressing.Subject);
    }

    [Fact]
    public void AddressingCopiesNoClaimSourceWhenItsAddressMatchesOrIsAbsent()
    {
        var same = CaseReportDeliveryPolicy.Address(Projection(
            contactEmail: "handler@principal.example",
            claimSourceEmail: "Handler@Principal.Example"));
        Assert.Empty(same.Cc);

        var absent = CaseReportDeliveryPolicy.Address(Projection(contactEmail: "handler@principal.example"));
        Assert.Empty(absent.Cc);
    }

    [Fact]
    public void AddressingRefusesACaseWithNoContactEmailAddress()
    {
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.Address(Projection(contactEmail: " ")));
        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.Address(Projection(contactEmail: null)));
    }

    [Fact]
    public void AddressingIsRefusedWhenTheContactsChangedSincePreparation()
    {
        var prepared = CaseReportDeliveryPolicy.Address(Projection(contactEmail: "handler@principal.example"));
        var current = CaseReportDeliveryPolicy.Address(Projection(contactEmail: "reassigned@principal.example"));

        Assert.Throws<InvalidOperationException>(
            () => CaseReportDeliveryPolicy.RequireAddressingCurrent(prepared, current));
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
                new RefusingStore(), new RefusingCaseData())
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
            new RefusingStore(), new FixedCaseData(Projection(contactEmail: "handler@principal.example")));
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
            new FixedCaseData(Projection(contactEmail: "handler@principal.example")),
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
            new FixedCaseData(Projection(contactEmail: "handler@principal.example")),
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
            new FixedCaseData(Projection(contactEmail: "handler@principal.example")),
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
            // The contact was edited after the preparation pinned its
            // addressing: the intent changed, so the send is refused rather
            // than delivered to the address it no longer names.
            new FixedCaseData(Projection(contactEmail: "reassigned@principal.example")),
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
        long currentCaseVersion = 1) => new(
        new CaseReportDeliveryPreparation(
            PreparationId, CaseId, GenerationId, 1, 1,
            pinned ?? [ReportAttachment], Staff(), PreparedAtUtc),
        new CaseReportDeliveryAddressing(
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
        Guid.NewGuid(), 1, new string('d', 64), null, null);

    private static ApprovedMailbox Mailbox() => new(
        Guid.NewGuid(), "reports@collisionengineers.example",
        [ApprovedMailboxRouteScope.SentEvidence], ApprovedMailboxState.Approved,
        "identity", "inbox", "sent", IdentityIsBound: true, ActivatedAtUtc: PreparedAtUtc, 1, []);

    private static CaseDataProjection Projection(
        string? contactEmail,
        string? contactName = null,
        string? claimSourceEmail = null,
        string? claimSourceName = null)
    {
        var emptyString = new CaseField<string>(null, null, null);
        var emptyDate = new CaseField<DateOnly>(null, null, null);
        return new(
            new CaseIdentity(CaseId, "QDOS", 2031, 1, "DVR-31001"),
            new CaseOriginIdentity(
                Guid.NewGuid(), IntakeSourceChannel.Mailbox, "test", new string('a', 64),
                PreparedAtUtc, "test", "1", null, null),
            PreparedAtUtc,
            1,
            CaseLifecycleState.ReportPreparation,
            new CaseCompletenessProjection(
                new CaseCompleteness(false, false, false, false),
                new CaseCompletenessEvaluation(false, "test", 1)),
            new CaseProviderData(emptyString),
            new CaseClaimantData(emptyString, emptyString, emptyString),
            new CaseClaimData(emptyString),
            new CaseVehicleData(
                emptyString, emptyString, emptyString,
                new CaseField<long>(null, null, null), emptyString),
            new CaseAccidentData(emptyDate, emptyString),
            new CaseContactData(
                Field(contactName), Field(contactEmail), emptyString),
            new CaseInstructionData(emptyDate, emptyString),
            new CaseInspectionData(
                emptyDate, emptyDate, emptyString,
                new CaseField<CaseInspectionMode>(null, null, null),
                emptyString, emptyString),
            Workspace: new CaseWorkspaceData(
                claimSourceEmail is null && claimSourceName is null
                    ? null
                    : new CaseWorkspaceClaimSource(
                        Guid.NewGuid(), 1, "Instructing insurer", claimSourceName, null,
                        claimSourceEmail, null),
                null, null, null, null, null, null, null, null, null, null));
    }

    private static CaseField<string> Field(string? value) => value is null
        ? new CaseField<string>(null, null, null)
        : new CaseField<string>(
            new CaseDataValue<string>(
                value, CaseDataValueKind.Confirmed,
                new CaseDataSource(
                    CaseDataSourceKind.CaseAcceptance, "test", "Test", "test", 1)),
            null, null);

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

    private sealed class RefusingCaseData : ICaseDataQueries
    {
        public Task<CaseDataProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
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

    private sealed class FixedCaseData(CaseDataProjection projection) : ICaseDataQueries
    {
        public Task<CaseDataProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult(caseId == CaseId ? projection : null);
    }

    private sealed class FixedMailboxes(params ApprovedMailbox[] mailboxes) : IApprovedMailboxStore
    {
        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedMailbox>>(mailboxes);

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
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
            Guid.NewGuid(), 1, new string('d', 64), null, null);

        public Task<StaffMailOperation> SendAsync(
            StaffReportSendCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(Result);
        }
    }
}
