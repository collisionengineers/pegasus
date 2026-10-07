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
/// rows for one delivery, and the send boundary re-reads the confirmed
/// artifacts by exact identity, hash and length. The transport is a recording
/// double; nothing here sends or records a Sent state.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseReportDeliveryPersistenceTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SendHandsTheTransportTheConfirmedArtifactAndTheReviewedRecipients()
    {
        await using var harness = await Harness.CreateAsync();

        var command = await harness.SendAsync(harness.Request());

        Assert.Equal(harness.GenerationId, command.Mail.ContextId);
        Assert.Equal(1, command.Mail.ExpectedContextVersion);
        Assert.Equal(
            ("handler@principal.example", "DVR-31001", "Please find attached our report."),
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
        await new ReportSendReadiness(harness.Store).RequireReadyAsync(command.Report, CancellationToken.None);
    }

    /// <summary>
    /// Without reviewed recipients the Principal's suggestions address the
    /// delivery: here the sender of the instruction that opened the Case.
    /// </summary>
    [Fact]
    public async Task SendWithoutReviewedRecipientsUsesThePrincipalSuggestions()
    {
        await using var harness = await Harness.CreateAsync();

        var command = await harness.SendAsync(harness.Request() with { ReviewedRecipients = null });

        Assert.Equal("origin-sender@principal.example", Assert.Single(command.Mail.To).Address);
    }

    [Fact]
    public async Task SendRefusesAPendingSupersededOrStaleVersionGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SetGenerationStateAsync(nameof(CaseReportGenerationState.Pending));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SendAsync(harness.Request()));

        await harness.SetGenerationStateAsync(nameof(CaseReportGenerationState.Confirmed));
        await harness.SupersedeGenerationAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SendAsync(harness.Request()));

        await using var fresh = await Harness.CreateAsync();
        await fresh.SetGenerationVersionAsync(7);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fresh.SendAsync(fresh.Request(expectedGenerationVersion: 1)));
    }

    [Fact]
    public async Task SendRefusesAGenerationWhoseReportIsNotConfirmed()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SetArtifactStateAsync(nameof(CaseReportArtifactStatus.Pending));

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SendAsync(harness.Request()));
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
    /// boundary. The two-step delivery this replaces refused it for good.
    /// </summary>
    [Fact]
    public async Task SystemWorkThatMovedTheCaseDoesNotRefuseTheSend()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.MoveCaseVersionAsync(3);

        var command = await harness.SendAsync(harness.Request(expectedCaseVersion: 1));

        await new ReportSendReadiness(harness.Store).RequireReadyAsync(command.Report, CancellationToken.None);
    }

    [Fact]
    public async Task ReadinessRefusesWhenAConfirmedArtifactChangedUnderneath()
    {
        await using var harness = await Harness.CreateAsync();
        var command = await harness.SendAsync(harness.Request());
        var readiness = new ReportSendReadiness(harness.Store);

        // The attachment still matches its confirmed row: ready.
        await readiness.RequireReadyAsync(command.Report, CancellationToken.None);

        await harness.TamperArtifactVersionAsync();
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
            () => new ReportSendReadiness(harness.Store).RequireReadyAsync(command.Report, CancellationToken.None));
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.SendAsync(request with { Work = CaseWorkSelector.Current }));
    }

    [Fact]
    public async Task RecipientSuggestionsUseTheOriginReceiptRouteSender()
    {
        await using var harness = await Harness.CreateAsync();

        var suggestions = await new EfReportRecipientSuggestionQueries(harness.Factory)
            .GetAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);

        Assert.NotNull(suggestions);
        Assert.True(suggestions!.Settings.IncludeOriginalInstructionSender);
        Assert.Equal("origin-sender@principal.example", suggestions.OriginalInstructionSender);
        Assert.Equal("Report delivery test", suggestions.PrincipalName);
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
    /// to a real custody version row — the minimum a delivery re-reads.
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

        public static async Task<Harness> CreateAsync()
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
                var caseId = await SeedCaseAsync(factory);
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

        /// <summary>
        /// One send through the real use case over this database: the real
        /// generation store, recipient suggestions and send history, one
        /// approved mailbox, and a transport that records the command it was
        /// handed and returns it.
        /// </summary>
        public async Task<StaffReportSendCommand> SendAsync(SendCaseReportRequest request)
        {
            var send = new RecordingSend();
            await new SendCaseReport(
                    Store,
                    new EfCaseReportSendHistoryQueries(Factory),
                    new EfReportRecipientSuggestionQueries(Factory),
                    new FixedMailboxes(),
                    send)
                .ExecuteAsync(request, CancellationToken.None);
            return Assert.Single(send.Commands);
        }

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
        /// Changes the confirmed version row's content length underneath the
        /// pinned attachment: the send boundary must refuse bytes that no
        /// longer match what the send named.
        /// </summary>
        public Task TamperArtifactVersionAsync() => database.ExecuteAsync(
            $"UPDATE DocumentVersions SET ContentLength = 999 WHERE Id = '{ArtifactVersionId:D}'");

        /// <summary>A Case write after the page was read: the live version moves.</summary>
        public Task MoveCaseVersionAsync(long version) => database.ExecuteAsync(
            $"UPDATE CaseWorkflows SET [Version] = {version} WHERE CaseId = '{CaseId:D}'");

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        private static async Task<Guid> SeedCaseAsync(
            PooledDbContextFactory<PegasusDbContext> factory)
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
                    IncludeOriginalInstructionSender = true,
                    ReportRecipientAddressesJson = "[]",
                    Version = 0
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
                    EffectiveSenderAddress = "origin-sender@principal.example",
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
                report.ReportDate, report.AgreedFee,
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

    /// <summary>The one approved mailbox bound for staff send and report-sent evidence.</summary>
    private sealed class FixedMailboxes : IApprovedMailboxStore
    {
        private static readonly ApprovedMailbox Mailbox = new(
            Guid.NewGuid(),
            "reports@collisionengineers.example",
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
            Task.FromResult<IReadOnlyList<ApprovedMailbox>>([Mailbox]);

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
