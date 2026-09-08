using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class V1ActivityReportPersistenceTests
{
    private static readonly DateTimeOffset From = new(2031, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = From.AddDays(31);

    [Fact]
    public async Task CountsReadyTransitionsInPeriodForPriorGenerationWithoutInflatingGenerationMetrics()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var generatedAt = From.AddDays(2);
        var sentAt = From.AddDays(3);
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var generationId = Guid.NewGuid();
            var priorGenerationId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var sha256 = new string('a', 64);
            context.AddRange(
                Receipt(receiptId),
                new CaseEntity
                {
                    Id = caseId,
                    PrincipalId = principal.Id,
                    SequenceLineageId = principal.SequenceLineageId,
                    Year = 2031,
                    Sequence = 1,
                    Reference = "QDOS31001",
                    Type = "Inspection",
                    InitialState = "Review",
                    CustodyState = "Confirmed",
                    OriginIntakeReceiptId = receiptId,
                    CreatedAtUtc = From,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEntity
                {
                    CaseId = caseId,
                    State = "Held",
                    PreHoldState = "Review",
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEventEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    EventType = "case_held",
                    OperationKey = "hold:report-test",
                    RequestHash = new string('c', 64),
                    ActorKind = "Staff",
                    ActorSubjectId = Guid.NewGuid().ToString("D"),
                    ActorRolesJson = "[]",
                    Reason = "Waiting for evidence",
                    OccurredAtUtc = From.AddDays(4),
                    BeforeVersion = 0,
                    AfterVersion = 1
                },
                new TriageEntity
                {
                    Id = Guid.NewGuid(),
                    Sequence = 1,
                    Reference = "T-00001",
                    PrincipalId = principal.Id,
                    OriginReceiptId = receiptId,
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = "triage:report-test",
                    SourceHash = new string('d', 64),
                    EvaluationRevisionId = Guid.NewGuid(),
                    NormalizedVehicleRegistration = "AB12CDE",
                    State = "open",
                    CreatedAtUtc = From.AddDays(1),
                    CreationOperationKey = "triage:report-test",
                    Version = 0,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseReportGenerationEntity
                {
                    Id = generationId,
                    CaseId = caseId,
                    CaseVersion = 1,
                    SnapshotHash = new string('b', 64),
                    SnapshotJson = "{}",
                    TemplateVersion = "test",
                    RendererVersion = "test",
                    State = "ready",
                    GeneratedAtUtc = generatedAt,
                    Version = 2
                },
                new CaseReportGenerationEntity
                {
                    Id = priorGenerationId,
                    CaseId = caseId,
                    CaseVersion = 0,
                    SnapshotHash = new string('c', 64),
                    SnapshotJson = "{}",
                    TemplateVersion = "test",
                    RendererVersion = "test",
                    State = "ready",
                    GeneratedAtUtc = From.AddDays(-1),
                    Version = 1
                },
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = caseId,
                    Ordinal = 1,
                    SourceOccurrenceIdentity = "report-output"
                },
                new DocumentVersionEntity
                {
                    Id = versionId,
                    DocumentId = documentId,
                    Version = 1,
                    FileName = "report.pdf",
                    MediaType = "application/pdf",
                    ContentLength = 1,
                    Sha256 = sha256,
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = generatedAt,
                    CreatedBy = "test",
                    IsCurrent = true
                },
                Artifact(generationId, versionId, sha256, "PDF", "Confirmed"),
                Artifact(generationId, null, null, "DOCX", "Pending"),
                Artifact(priorGenerationId, versionId, sha256, "PDF", "Confirmed"),
                new ActionHistoryEntity
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "case",
                    AggregateId = caseId.ToString("D"),
                    EventKind = "case_report_generation_ready",
                    ActorKind = "Staff",
                    ActorSubjectId = Guid.NewGuid().ToString("D"),
                    ActorRolesJson = "[]",
                    OccurredAtUtc = generatedAt.AddHours(2),
                    Outcome = "Succeeded",
                    CorrelationId = "report-ready:test",
                    AfterJson = $"{{\"generationId\":\"{generationId:D}\"}}"
                },
                new ActionHistoryEntity
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "case",
                    AggregateId = caseId.ToString("D"),
                    EventKind = "case_report_generation_ready",
                    ActorKind = "Staff",
                    ActorSubjectId = Guid.NewGuid().ToString("D"),
                    ActorRolesJson = "[]",
                    OccurredAtUtc = From.AddDays(1),
                    Outcome = "Succeeded",
                    CorrelationId = "report-ready:prior-generation",
                    AfterJson = $"{{\"generationId\":\"{priorGenerationId:D}\"}}"
                },
                new ActionHistoryEntity
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "case",
                    AggregateId = caseId.ToString("D"),
                    EventKind = "case_report_generation_ready",
                    ActorKind = "Staff",
                    ActorSubjectId = Guid.NewGuid().ToString("D"),
                    ActorRolesJson = "[]",
                    OccurredAtUtc = generatedAt.AddHours(3),
                    Outcome = "Succeeded",
                    CorrelationId = "report-ready:stale-generation",
                    AfterJson = $"{{\"generationId\":\"{Guid.NewGuid():D}\"}}"
                },
                SentOperation(generationId, contextVersion: 1, sentAt));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>();
        var rows = await queries.GetAsync(From, To, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("QDOS", row.PrincipalCode);
        Assert.Equal(1, row.GenerationEvents);
        Assert.Equal(1, row.GeneratedArtifacts);
        Assert.Equal(1, row.Sent);
        Assert.Equal(2, row.Ready);
        Assert.Equal(TimeSpan.FromDays(2), row.AverageReceivedToGeneration);
        Assert.Equal(TimeSpan.FromDays(2), row.AverageReceivedToGeneratedArtifact);
        Assert.Equal(TimeSpan.FromHours(37), row.AverageReceivedToReady);
        Assert.Equal(TimeSpan.FromDays(3), row.AverageReceivedToSent);
        Assert.Equal(0, row.MissingOriginForReadyTurnaround);
        Assert.Equal(1, row.CurrentTriage);
        Assert.Equal(From.AddDays(1), row.OldestCurrentTriageCreatedAtUtc);
        Assert.Equal(1, row.CurrentHeldCases);
        Assert.Equal(From.AddDays(4), row.OldestHeldAtUtc);
        Assert.Equal(0, row.HeldWithoutRecordedHoldEvent);
        Assert.Equal(
        [
            new PrincipalReportArtifactTypeActivity("DOCX", 0, 1),
            new PrincipalReportArtifactTypeActivity("PDF", 1, 0)
        ], row.ArtifactTypes);
    }

    private static IntakeReceiptEntity Receipt(Guid id) => new()
    {
        Id = id,
        SourceFileName = "origin.pdf",
        MediaType = "application/pdf",
        SourceLength = 1,
        SourceHash = new string('1', 64),
        SourceChannel = "manual_upload",
        ExternalReceiptToken = $"origin:{id:N}",
        ReceivedAtUtc = From,
        ProcessedAtUtc = From,
        SourceReaderKey = "test",
        SourceReaderVersion = "1",
        Version = 0,
        Decision = "case_created",
        DecisionReason = "test",
        EvidenceJson = "[]",
        FieldsJson = "[]",
        OcrCandidatesJson = "[]"
    };

    private static GeneratedCaseArtifactEntity Artifact(
        Guid generationId,
        Guid? versionId,
        string? sha256,
        string kind,
        string state) => new()
    {
        Id = Guid.NewGuid(),
        GenerationId = generationId,
        VersionId = versionId,
        Kind = kind,
        Sha256 = sha256,
        State = state,
        OperationKey = $"artifact:{kind}:{Guid.NewGuid():N}"
    };

    private static StaffMailSendOperationEntity SentOperation(
        Guid generationId,
        long contextVersion,
        DateTimeOffset sentAt) => new()
    {
        Id = Guid.NewGuid(),
        ActorSubjectId = Guid.NewGuid().ToString("D"),
        MailboxId = Guid.NewGuid(),
        MailboxGeneration = 1,
        OperationKey = $"send:{Guid.NewGuid():N}",
        PayloadHash = new string('2', 64),
        Purpose = StaffMailPurpose.CaseReport,
        ContextId = generationId,
        ContextVersion = contextVersion,
        ComposeMode = StaffMailComposeMode.New,
        RecipientsJson = "[]",
        Subject = "report",
        Body = "report",
        AttachmentsJson = "[]",
        State = StaffMailState.Sent,
        CorrelationMarker = "test",
        CreatedAtUtc = sentAt,
        RequestedAtUtc = sentAt,
        ObservedSentAtUtc = sentAt,
        Version = 1,
        ConcurrencyToken = Guid.NewGuid()
    };
}
