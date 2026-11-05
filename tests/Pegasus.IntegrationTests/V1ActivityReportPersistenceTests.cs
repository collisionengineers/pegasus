using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Reports by Principal, Turnaround and Queues read from the real stores
/// (MI-02, MI-03, MI-05; R673): each report counts once in Reports sent, at
/// its first observed send, with the fee frozen in the report that send
/// carried and its time from the instruction's receipt, or for an
/// Inspection + Audit Case's Audit from Create audit; every later send of it
/// is a re-send, counted on its own.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class V1ActivityReportPersistenceTests
{
    private static readonly DateTimeOffset From = new(2031, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = From.AddDays(31);

    [Fact]
    public async Task CountsEachReportOnItsFirstSendWithTheFeeFrozenInTheReportItCarriedBesideTheQueuesNow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var triageCaseId = Guid.NewGuid();
            var unsentGenerationId = Guid.NewGuid();
            var generationId = Guid.NewGuid();
            var resentGenerationId = Guid.NewGuid();
            context.AddRange(
                Receipt(receiptId),
                Case(principal, caseId, receiptId, 1, "inspection"),
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
                // A Triage is a Case of the same Principal: its Case row
                // carries the Principal and the t. Case/PO.
                new CaseEntity
                {
                    Id = triageCaseId,
                    PrincipalId = principal.Id,
                    SequenceLineageId = principal.SequenceLineageId,
                    Year = 2031,
                    Sequence = 2,
                    Reference = "t.QDOS31002",
                    Type = "triage",
                    InitialState = null,
                    CustodyState = "pending",
                    OriginIntakeReceiptId = receiptId,
                    CreatedAtUtc = From.AddDays(1),
                    Version = 0,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new TriageEntity
                {
                    CaseId = triageCaseId,
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
                // A Generate is not a send: this report was regenerated before
                // it was sent, and only the report the first send carried counts.
                Generation(unsentGenerationId, caseId, From.AddDays(-1), "{\"agreedFee\":1.00}"),
                Generation(generationId, caseId, From.AddDays(2), "{\"agreedFee\":111.25}"),
                // Regenerated after its first send and sent again: a re-send,
                // whose report's fee is not the report's.
                Generation(resentGenerationId, caseId, From.AddDays(5), "{\"agreedFee\":500.00}"),
                SentOperation(From.AddDays(3), generationId),
                SentOperation(From.AddDays(6), resentGenerationId));
            context.CaseAssessmentFields.Add(new CaseAssessmentFieldEntity
            {
                WorkId = caseId,
                FieldPath = AssessmentVocabulary.AgreedFee,
                Value = "999.99",
                RecordedByKind = "Staff",
                RecordedBy = Guid.NewGuid().ToString("D"),
                RecordedAtUtc = From.AddDays(3)
            });
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>();
        var rows = await queries.GetAsync(From, To, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("QDOS", row.PrincipalCode);
        Assert.Equal(1, row.Sent);
        Assert.Equal(1, row.Resends);
        Assert.Equal(0, row.AuditSent);
        Assert.Equal(0, row.AuditResends);
        // The fee frozen in the report the first send carried: not the unsent
        // Generate's, the re-sent report's or the Case's live agreed fee.
        Assert.Equal(111.25m, row.AgreedFeeTotal);
        Assert.Equal(0m, row.AuditAgreedFeeTotal);
        // Received at From and first sent three days later; a re-send is not
        // a time to send.
        Assert.Equal(TimeSpan.FromDays(3), row.AverageReceivedToSent);
        Assert.Equal(1, row.CurrentTriage);
        Assert.Equal(From.AddDays(1), row.OldestCurrentTriageCreatedAtUtc);
        Assert.Equal(1, row.CurrentHeldCases);
        Assert.Equal(From.AddDays(4), row.OldestHeldAtUtc);
        Assert.Equal(0, row.HeldWithoutRecordedHoldEvent);
    }

    /// <summary>
    /// R673: one e-mail that carried both of an Inspection + Audit Case's
    /// reports is two reports sent and no re-send, each with the fee frozen
    /// in its own report and its own time to send: the Inspection report's
    /// from the receipt, the Audit report's from Create audit.
    /// </summary>
    [Fact]
    public async Task OneEmailCarryingBothReportsCountsEachWithItsOwnFeeAndTimeToSend()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var auditWorkId = Guid.NewGuid();
            var inspectionGenerationId = Guid.NewGuid();
            var auditGenerationId = Guid.NewGuid();
            context.AddRange(
                Receipt(receiptId),
                Case(principal, caseId, receiptId, 1, "inspection_and_audit"),
                AuditWork(auditWorkId, caseId, From.AddDays(5)),
                Generation(inspectionGenerationId, caseId, From.AddDays(2), "{\"agreedFee\":100.00}"),
                Generation(auditGenerationId, caseId, From.AddDays(7), "{\"agreedFee\":40.00}", auditWorkId),
                SentOperation(From.AddDays(8), auditGenerationId, inspectionGenerationId));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>();
        var row = Assert.Single(await queries.GetAsync(From, To, CancellationToken.None));

        Assert.Equal((2, 1, 1), (row.Sent, row.AuditSent, row.InspectionSent));
        Assert.Equal((0, 0), (row.Resends, row.AuditResends));
        Assert.Equal((140m, 40m, 100m), (row.AgreedFeeTotal, row.AuditAgreedFeeTotal, row.InspectionAgreedFeeTotal));
        // The Inspection report eight days after the receipt, the Audit report
        // three days after Create audit.
        Assert.Equal(TimeSpan.FromHours(132), row.AverageReceivedToSent);
    }

    /// <summary>
    /// R673: the Inspection report first sent on its own, then after Create
    /// audit one e-mail carrying the Audit report for the first time and the
    /// Inspection report again. Each e-mail is one report sent in its own
    /// period; the second is also the Inspection report's re-send, which adds
    /// no fee and no time to send.
    /// </summary>
    [Fact]
    public async Task AnEmailThatResendsTheInspectionAndFirstSendsTheAuditIsOneReportSentAndOneResend()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var createAudit = From.AddDays(10);
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var auditWorkId = Guid.NewGuid();
            var inspectionGenerationId = Guid.NewGuid();
            var auditGenerationId = Guid.NewGuid();
            context.AddRange(
                Receipt(receiptId),
                Case(principal, caseId, receiptId, 1, "inspection_and_audit"),
                AuditWork(auditWorkId, caseId, createAudit),
                Generation(inspectionGenerationId, caseId, From.AddDays(1), "{\"agreedFee\":100.00}"),
                Generation(auditGenerationId, caseId, createAudit.AddDays(1), "{\"agreedFee\":40.00}", auditWorkId),
                SentOperation(From.AddDays(2), inspectionGenerationId),
                SentOperation(createAudit.AddDays(3), auditGenerationId, inspectionGenerationId));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>();

        // Before Create audit: the Inspection report, two days after the receipt.
        var before = Assert.Single(await queries.GetAsync(From, createAudit, CancellationToken.None));
        Assert.Equal((1, 0, 0), (before.Sent, before.AuditSent, before.Resends));
        Assert.Equal(100m, before.AgreedFeeTotal);
        Assert.Equal(TimeSpan.FromDays(2), before.AverageReceivedToSent);

        // After it: the Audit report, three days after Create audit, and the
        // Inspection report's re-send.
        var after = Assert.Single(await queries.GetAsync(createAudit, To, CancellationToken.None));
        Assert.Equal((1, 1, 0), (after.Sent, after.AuditSent, after.InspectionSent));
        Assert.Equal((1, 0, 1), (after.Resends, after.AuditResends, after.InspectionResends));
        Assert.Equal((40m, 40m), (after.AgreedFeeTotal, after.AuditAgreedFeeTotal));
        Assert.Equal(TimeSpan.FromDays(3), after.AverageReceivedToSent);

        // The whole month counts each report once.
        var whole = Assert.Single(await queries.GetAsync(From, To, CancellationToken.None));
        Assert.Equal((2, 1, 1, 0), (whole.Sent, whole.AuditSent, whole.Resends, whole.AuditResends));
        Assert.Equal((140m, 40m), (whole.AgreedFeeTotal, whole.AuditAgreedFeeTotal));
        Assert.Equal(TimeSpan.FromHours(60), whole.AverageReceivedToSent);
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

    private static CaseEntity Case(
        SeededPrincipalTestData principal,
        Guid id,
        Guid receiptId,
        int sequence,
        string type) => new()
    {
        Id = id,
        PrincipalId = principal.Id,
        SequenceLineageId = principal.SequenceLineageId,
        Year = 2031,
        Sequence = sequence,
        Reference = $"QDOS3100{sequence}",
        Type = type,
        InitialState = "Review",
        CustodyState = "Confirmed",
        OriginIntakeReceiptId = receiptId,
        CreatedAtUtc = From,
        Version = 1,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseWorkEntity AuditWork(Guid id, Guid caseId, DateTimeOffset createdAtUtc) => new()
    {
        Id = id,
        CaseId = caseId,
        Kind = CaseWorkKinds.Audit,
        CreatedAtUtc = createdAtUtc
    };

    private static CaseReportGenerationEntity Generation(
        Guid id,
        Guid caseId,
        DateTimeOffset generatedAtUtc,
        string snapshotJson,
        Guid? workId = null) => new()
    {
        Id = id,
        CaseId = caseId,
        WorkId = workId ?? caseId,
        CaseVersion = 1,
        SnapshotHash = $"{id:N}{id:N}",
        SnapshotJson = snapshotJson,
        TemplateVersion = "test",
        RendererVersion = "test",
        State = nameof(CaseReportGenerationState.Confirmed),
        GeneratedAtUtc = generatedAtUtc,
        Version = 1
    };

    /// <summary>
    /// A report e-mail seen in Sent Items, from a staff member of its own,
    /// carrying the report generation <paramref name="generationId"/> and,
    /// when it carried both works' reports, <paramref name="secondGenerationId"/>.
    /// </summary>
    private static StaffMailSendOperationEntity SentOperation(
        DateTimeOffset sentAt,
        Guid generationId,
        Guid? secondGenerationId = null) => new()
    {
        Id = Guid.NewGuid(),
        ActorSubjectId = Guid.NewGuid().ToString("D"),
        MailboxId = Guid.NewGuid(),
        MailboxGeneration = 1,
        OperationKey = $"send:{Guid.NewGuid():N}",
        PayloadHash = new string('2', 64),
        Purpose = StaffMailPurpose.CaseReport,
        ContextId = generationId,
        ContextVersion = 1,
        SecondContextId = secondGenerationId,
        SecondContextVersion = secondGenerationId is null ? null : 1,
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
