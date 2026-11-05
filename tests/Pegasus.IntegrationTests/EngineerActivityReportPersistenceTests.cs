using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// MI-01. The Engineer Report counts against a real database: each report
/// counts once, on its first observed send, for whoever made that send, and
/// every later send of it is a re-send counted for its own sender (R673);
/// queries are CaseQueries rows that are open or Done, not withdrawn
/// (operator, 27 October 2026); both bounded by the half-open period. A
/// mailbox receipt is not a query. Classified mail does not count.
/// Automation's report sends are a row of their own.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class EngineerActivityReportPersistenceTests
{
    private static readonly DateTimeOffset From = new(2031, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2031, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CountsSentReportsByRecordedActorAndQueriesByAssignedEngineerWithinThePeriod()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var engineerA = Guid.NewGuid();
        var engineerB = Guid.NewGuid();
        var estate = await SeedEstateAsync(database);
        var caseA = await estate.SeedCaseAsync(engineerA, engineerB, 1);
        var caseB = await estate.SeedCaseAsync(engineerB, engineerA, 2);
        var unassigned = await estate.SeedCaseAsync(null, null, 3);
        // Each send carries a report of its own, from a Case with no Engineer,
        // so each is its report's first send. The send actor is the
        // engineering-history dimension: it is deliberately neither the case's
        // assigned engineer nor its signatory.
        var sentByBEarly = await estate.SeedReportAsync(4);
        var sentByA = await estate.SeedReportAsync(5);
        var sentByBLate = await estate.SeedReportAsync(6);
        var sentAtTheEnd = await estate.SeedReportAsync(7);
        var sentBeforeTheStart = await estate.SeedReportAsync(8);
        var sentByUnknownActor = await estate.SeedReportAsync(9);
        var sentByAutomation = await estate.SeedReportAsync(10);

        await using (var context = await database.CreateContextAsync())
        {
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(engineerB, From.AddDays(3), sentByBEarly),
                SentOperation(engineerA, From.AddDays(10), sentByA),
                SentOperation(engineerB, To.AddSeconds(-1), sentByBLate),
                SentOperation(engineerA, To, sentAtTheEnd),
                SentOperation(engineerA, From.AddSeconds(-1), sentBeforeTheStart),
                SentOperation("not-a-staff-id", From.AddDays(5), sentByUnknownActor),
                // Item J: Automation's send is its own row, so the total is every send.
                SentOperation("connector", From.AddDays(6), sentByAutomation, kind: Pegasus.Core.Identity.ActorKind.Automation));
            context.IntakeReceipts.AddRange(
                Query(From.AddDays(1), "cancellation", caseA, active: true),
                Query(From.AddDays(2), "cancellation", caseA, active: true),
                Query(From.AddDays(2), "cancellation", caseA, active: false),
                Query(From.AddDays(2), "new-instruction", caseA, active: true),
                Query(To, "cancellation", caseA, active: true),
                Query(From.AddDays(4), "cancellation", caseB, active: true),
                Query(From.AddDays(4), "cancellation", unassigned, active: true));
            context.CaseQueries.AddRange(
                LoggedQuery(caseA, From.AddDays(1)),
                LoggedQuery(caseA, From.AddDays(2)),
                LoggedQuery(caseA, From.AddDays(2), withdrawn: true),
                LoggedQuery(caseA, To),
                LoggedQuery(caseB, From.AddDays(4)),
                LoggedQuery(unassigned, From.AddDays(4)));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEngineerActivityQueries>();

        var all = await queries.GetAsync(From, To, null, CancellationToken.None);

        // Every report was received at From: each sender's time to send is the
        // average of their first sends' times from it.
        Assert.Equal(
            new[]
            {
                new EngineerActivityCounts(engineerA, 1, 2, AverageReceivedToSent: TimeSpan.FromDays(10)),
                new EngineerActivityCounts(
                    engineerB,
                    2,
                    1,
                    AverageReceivedToSent: (TimeSpan.FromDays(3) + (To.AddSeconds(-1) - From)) / 2)
            }
                .OrderBy(item => item.EngineerId)
                .Append(new EngineerActivityCounts(Guid.Empty, 1, 0, AverageReceivedToSent: TimeSpan.FromDays(6))
                {
                    SenderKind = Pegasus.Core.Identity.ActorKind.Automation
                }),
            all);

        // F254 (R326): one staff member asked for is exactly that sender's row of
        // the unfiltered read, with no other sender's sends or queries; one
        // with no activity is absent.
        foreach (var engineer in new[] { engineerA, engineerB })
        {
            var chosen = await queries.GetAsync(From, To, engineer, CancellationToken.None);
            Assert.Equal(all.Where(item => item.EngineerId == engineer), chosen);
        }
        Assert.Empty(await queries.GetAsync(From, To, Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>
    /// MI-01's queries, Audit uplift and the Engineer's own turnaround: an
    /// amendment request is a query; a
    /// report sent on an Audit Case counts as an Audit report; the send's
    /// generation names the Case whose origin receipt gives the turnaround.
    /// </summary>
    [Fact]
    public async Task CountsQueryTypesAuditReportsAndTheEngineersTurnaround()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var engineer = Guid.NewGuid();
        var estate = await SeedEstateAsync(database);
        var inspection = await estate.SeedCaseAsync(engineer, null, 1, "inspection");
        var audit = await estate.SeedCaseAsync(engineer, null, 2, "audit");
        var inspectionAndAudit = await estate.SeedCaseAsync(engineer, null, 3, "inspection_and_audit");
        var inspectionGeneration = Guid.NewGuid();
        var auditGeneration = Guid.NewGuid();
        var inspectionAndAuditGeneration = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            context.AddRange(
                Generation(inspectionGeneration, inspection, From.AddDays(1)),
                Generation(auditGeneration, audit, From.AddDays(2)),
                Generation(inspectionAndAuditGeneration, inspectionAndAudit, From.AddDays(3)));
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(engineer, From.AddDays(2), inspectionGeneration),
                SentOperation(engineer, From.AddDays(4), auditGeneration),
                SentOperation(engineer, From.AddDays(3), inspectionAndAuditGeneration));
            context.IntakeReceipts.AddRange(
                Query(From.AddDays(1), "cancellation", inspection, active: true),
                Query(From.AddDays(2), "cancellation", inspection, active: true),
                Query(From.AddDays(3), "cancellation", audit, active: true));
            context.CaseQueries.AddRange(
                LoggedQuery(inspection, From.AddDays(1)),
                LoggedQuery(inspection, From.AddDays(2)),
                LoggedQuery(audit, From.AddDays(3)));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEngineerActivityQueries>();
        var row = Assert.Single(await queries.GetAsync(From, To, null, CancellationToken.None));

        Assert.Equal(3, row.ReportsSent);
        Assert.Equal(1, row.AuditReportsSent);
        Assert.Equal(0, row.Resends);
        Assert.Equal(3, row.QueriesReceived);
        // All Cases were received at From; sent two, three and four days later.
        Assert.Equal(TimeSpan.FromDays(3), row.AverageReceivedToSent);
    }

    /// <summary>
    /// MI-01 per work: once an Inspection + Audit Case has its Audit, the
    /// Audit work's report is an Audit report and its turnaround runs from the
    /// Audit's creation; the Inspection report keeps its receipt turnaround.
    /// Two e-mails on different days are one report sent each, each in the
    /// period of its own e-mail (R673).
    /// </summary>
    [Fact]
    public async Task AnAuditWorksReportIsAnAuditReportTimedFromTheAuditsCreation()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var engineer = Guid.NewGuid();
        var estate = await SeedEstateAsync(database);
        var caseId = await estate.SeedCaseAsync(engineer, null, 1, "inspection_and_audit");
        var auditWorkId = Guid.NewGuid();
        var inspectionGeneration = Guid.NewGuid();
        var auditGeneration = Guid.NewGuid();
        var createAudit = From.AddDays(5);
        await using (var context = await database.CreateContextAsync())
        {
            context.CaseWorks.Add(AuditWork(auditWorkId, caseId, createAudit));
            context.AddRange(
                Generation(inspectionGeneration, caseId, From.AddDays(1)),
                Generation(auditGeneration, caseId, From.AddDays(6), auditWorkId));
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(engineer, From.AddDays(2), inspectionGeneration),
                SentOperation(engineer, From.AddDays(8), auditGeneration));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEngineerActivityQueries>();
        var row = Assert.Single(await queries.GetAsync(From, To, null, CancellationToken.None));

        Assert.Equal(2, row.ReportsSent);
        Assert.Equal(1, row.AuditReportsSent);
        Assert.Equal(0, row.Resends);
        // The Inspection report was sent two days after the receipt; the
        // Audit's three days after the Audit was created.
        Assert.Equal(TimeSpan.FromHours(60), row.AverageReceivedToSent);

        Assert.Equal(
            new EngineerActivityCounts(engineer, 1, 0, AverageReceivedToSent: TimeSpan.FromDays(2)),
            Assert.Single(await queries.GetAsync(From, createAudit, null, CancellationToken.None)));
        Assert.Equal(
            new EngineerActivityCounts(engineer, 1, 0, AuditReportsSent: 1, AverageReceivedToSent: TimeSpan.FromDays(3)),
            Assert.Single(await queries.GetAsync(createAudit, To, null, CancellationToken.None)));
    }

    /// <summary>
    /// R673: one e-mail that carried both of an Inspection + Audit Case's
    /// reports is a send of each: two reports sent, one of them the Audit's,
    /// and no re-send. Each is timed from its own start: the Inspection
    /// report from the receipt, the Audit report from Create audit.
    /// </summary>
    [Fact]
    public async Task OneEmailCarryingBothReportsIsTwoReportsSentOneOfThemTheAudit()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var engineer = Guid.NewGuid();
        var estate = await SeedEstateAsync(database);
        var caseId = await estate.SeedCaseAsync(engineer, null, 1, "inspection_and_audit");
        var auditWorkId = Guid.NewGuid();
        var inspectionGeneration = Guid.NewGuid();
        var auditGeneration = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            context.CaseWorks.Add(AuditWork(auditWorkId, caseId, From.AddDays(5)));
            context.AddRange(
                Generation(inspectionGeneration, caseId, From.AddDays(2)),
                Generation(auditGeneration, caseId, From.AddDays(7), auditWorkId));
            context.Set<StaffMailSendOperationEntity>().Add(
                SentOperation(engineer, From.AddDays(8), auditGeneration, inspectionGeneration));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEngineerActivityQueries>();
        var row = Assert.Single(await queries.GetAsync(From, To, null, CancellationToken.None));

        // The Inspection report eight days after the receipt, the Audit
        // report three days after Create audit.
        Assert.Equal(
            new EngineerActivityCounts(engineer, 2, 0, AuditReportsSent: 1, AverageReceivedToSent: TimeSpan.FromHours(132)),
            row);
    }

    /// <summary>
    /// R673: a re-send is counted on its own, for whoever made it, and is
    /// never another report sent. First send and re-send are told apart over
    /// every sender's sends before the period or the Person narrows them, so
    /// a report first sent before the period, or by someone else, is a
    /// re-send in it.
    /// </summary>
    [Fact]
    public async Task AResendIsCountedForItsOwnSenderAndIsNeverAnotherReportSent()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var firstSender = Guid.NewGuid();
        var resender = Guid.NewGuid();
        var estate = await SeedEstateAsync(database);
        var sentInPeriod = await estate.SeedReportAsync(1);
        var sentBeforePeriod = await estate.SeedReportAsync(2);
        var periodStart = From.AddDays(10);
        await using (var context = await database.CreateContextAsync())
        {
            context.Set<StaffMailSendOperationEntity>().AddRange(
                // First sent in the period, then re-sent by someone else.
                SentOperation(firstSender, periodStart.AddDays(2), sentInPeriod),
                SentOperation(resender, periodStart.AddDays(5), sentInPeriod),
                // First sent before the period by the re-sender, then re-sent
                // in it by the first sender.
                SentOperation(resender, From.AddDays(3), sentBeforePeriod),
                SentOperation(firstSender, periodStart.AddDays(4), sentBeforePeriod));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEngineerActivityQueries>();

        // Three sends in the period are one report sent, twelve days after
        // the receipt, and two re-sends, one for each sender.
        var firstSenderRow = new EngineerActivityCounts(firstSender, 1, 0, AverageReceivedToSent: TimeSpan.FromDays(12), Resends: 1);
        var resenderRow = new EngineerActivityCounts(resender, 0, 0, Resends: 1);
        Assert.Equal(
            new[] { firstSenderRow, resenderRow }.OrderBy(item => item.EngineerId),
            await queries.GetAsync(periodStart, To, null, CancellationToken.None));

        // The Person who only re-sent in the period has their re-send and no
        // report sent; each Person's row is theirs in the unfiltered read.
        Assert.Equal(resenderRow, Assert.Single(await queries.GetAsync(periodStart, To, resender, CancellationToken.None)));
        Assert.Equal(firstSenderRow, Assert.Single(await queries.GetAsync(periodStart, To, firstSender, CancellationToken.None)));

        // Over the whole month each report counts once, for whoever first sent it.
        Assert.Equal(
            new[]
            {
                firstSenderRow,
                new EngineerActivityCounts(resender, 1, 0, AverageReceivedToSent: TimeSpan.FromDays(3), Resends: 1)
            }.OrderBy(item => item.EngineerId),
            await queries.GetAsync(From, To, null, CancellationToken.None));
    }

    /// <summary>
    /// R673: after Create audit, one e-mail that carried the Audit report for
    /// the first time and the Inspection report again is one report sent, the
    /// Audit's, timed from Create audit, and one re-send, the Inspection's.
    /// </summary>
    [Fact]
    public async Task AnEmailThatResendsTheInspectionAndFirstSendsTheAuditIsOneReportSentAndOneResend()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var engineer = Guid.NewGuid();
        var estate = await SeedEstateAsync(database);
        var caseId = await estate.SeedCaseAsync(engineer, null, 1, "inspection_and_audit");
        var auditWorkId = Guid.NewGuid();
        var inspectionGeneration = Guid.NewGuid();
        var auditGeneration = Guid.NewGuid();
        var createAudit = From.AddDays(10);
        await using (var context = await database.CreateContextAsync())
        {
            context.CaseWorks.Add(AuditWork(auditWorkId, caseId, createAudit));
            context.AddRange(
                Generation(inspectionGeneration, caseId, From.AddDays(1)),
                Generation(auditGeneration, caseId, createAudit.AddDays(1), auditWorkId));
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(engineer, From.AddDays(2), inspectionGeneration),
                SentOperation(engineer, createAudit.AddDays(3), auditGeneration, inspectionGeneration));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEngineerActivityQueries>();

        Assert.Equal(
            new EngineerActivityCounts(engineer, 1, 0, AuditReportsSent: 1, AverageReceivedToSent: TimeSpan.FromDays(3), Resends: 1),
            Assert.Single(await queries.GetAsync(createAudit, To, null, CancellationToken.None)));

        // Over the month each report counts once: the Inspection report two
        // days after the receipt, the Audit report three days after Create audit.
        Assert.Equal(
            new EngineerActivityCounts(engineer, 2, 0, AuditReportsSent: 1, AverageReceivedToSent: TimeSpan.FromHours(60), Resends: 1),
            Assert.Single(await queries.GetAsync(From, To, null, CancellationToken.None)));
    }

    private static CaseReportGenerationEntity Generation(
        Guid id,
        Guid caseId,
        DateTimeOffset generatedAtUtc,
        Guid? workId = null) => new()
    {
        Id = id,
        CaseId = caseId,
        WorkId = workId ?? caseId,
        SnapshotHash = $"{id:N}{id:N}",
        SnapshotJson = "{}",
        TemplateVersion = "1",
        RendererVersion = "1",
        State = "Generated",
        GeneratedAtUtc = generatedAtUtc
    };

    private static CaseWorkEntity AuditWork(Guid id, Guid caseId, DateTimeOffset createdAtUtc) => new()
    {
        Id = id,
        CaseId = caseId,
        Kind = CaseWorkKinds.Audit,
        CreatedAtUtc = createdAtUtc
    };

    [Fact]
    public async Task AnEngineerWithNoActivityInThePeriodIsAbsent()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        await estate.SeedCaseAsync(Guid.NewGuid(), null, 1);

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEngineerActivityQueries>();

        Assert.Empty(await queries.GetAsync(From, To, null, CancellationToken.None));
    }

    /// <summary>
    /// A report e-mail seen in Sent Items, carrying the report generation
    /// <paramref name="report"/> and, when it carried both works' reports,
    /// <paramref name="secondReport"/>.
    /// </summary>
    private static StaffMailSendOperationEntity SentOperation(
        object actor,
        DateTimeOffset sentAtUtc,
        Guid report,
        Guid? secondReport = null,
        Pegasus.Core.Identity.ActorKind kind = Pegasus.Core.Identity.ActorKind.Staff)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            ActorKind = kind,
            ActorSubjectId = actor.ToString()!,
            MailboxId = Guid.NewGuid(),
            MailboxGeneration = 1,
            OperationKey = $"send:{id:N}",
            PayloadHash = new string('7', 64),
            Purpose = Pegasus.Core.Operations.StaffMailPurpose.CaseReport,
            ContextId = report,
            ContextVersion = 1,
            SecondContextId = secondReport,
            SecondContextVersion = secondReport is null ? null : 1,
            ComposeMode = Pegasus.Core.Operations.StaffMailComposeMode.New,
            RecipientsJson = "[]",
            Subject = "report",
            Body = "report",
            AttachmentsJson = "[]",
            State = Pegasus.Core.Operations.StaffMailState.Sent,
            CorrelationMarker = $"mail:{id:N}",
            CreatedAtUtc = sentAtUtc,
            RequestedAtUtc = sentAtUtc,
            ObservedSentAtUtc = sentAtUtc,
            Version = 1,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private static CaseQueryEntity LoggedQuery(Guid caseId, DateTimeOffset raisedAtUtc, bool withdrawn = false)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            CaseId = caseId,
            Source = "Staff",
            Channel = "Phone",
            Details = "A query.",
            RaisedAtUtc = raisedAtUtc,
            RaisedByKind = "Staff",
            RaisedBySubjectId = Guid.NewGuid().ToString("D"),
            RaisedOperationKey = $"activity-query:{id:N}",
            ClosedAs = withdrawn ? "Withdrawn" : null,
            ClosedAtUtc = withdrawn ? raisedAtUtc.AddMinutes(1) : null,
            ClosedByKind = withdrawn ? "Staff" : null,
            ClosedBySubjectId = withdrawn ? Guid.NewGuid().ToString("D") : null
        };
    }

    private static IntakeReceiptEntity Query(
        DateTimeOffset receivedAtUtc,
        string category,
        Guid caseId,
        bool active)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            SourceFileName = $"{id:N}.eml",
            MediaType = "message/rfc822",
            SourceLength = 1,
            SourceHash = new string('2', 64),
            SourceChannel = "mailbox",
            ExternalReceiptToken = $"mail:{id:N}",
            ReceivedAtUtc = receivedAtUtc,
            ProcessedAtUtc = receivedAtUtc,
            SourceReaderKey = "report-test",
            SourceReaderVersion = "1",
            Version = 0,
            Decision = "needs_sorting",
            DecisionReason = "report test",
            EvidenceJson = "[]",
            FieldsJson = "[]",
            OcrCandidatesJson = "[]",
            MailClassificationDecision = new IntakeMailClassificationDecisionEntity
            {
                IntakeReceiptId = id,
                Outcome = "classified",
                Category = category,
                PredicatesJson = "[]",
                Reason = "report test",
                PolicyKey = "report-test",
                PolicyVersion = 1,
                DecidedByActor = "worker",
                DecidedAtUtc = receivedAtUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            ManualAssociation = new IntakeManualAssociationEntity
            {
                IntakeReceiptId = id,
                CaseId = caseId,
                IsActive = active,
                Version = 0,
                LinkedAtUtc = receivedAtUtc,
                UnlinkedAtUtc = active ? null : receivedAtUtc.AddMinutes(1),
                ActorKind = "Staff",
                ActorSubjectId = Guid.NewGuid().ToString("D"),
                ActorRolesJson = "[]",
                Reason = "report test",
                LastOperationKey = $"assoc:{id:N}"
            }
        };
    }

    private static async Task<Estate> SeedEstateAsync(LocalDbTestDatabase database)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            context.AddRange(
                new OrganizationEntity { Role = "principal", Id = organizationId, Name = "Report test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = From },
                new PrincipalEntity
                {
                    Id = principalId,
                    OrganizationId = organizationId,
                    SequenceLineageId = lineageId,
                    Code = "EVA",
                    IsActive = true,
                    Version = 0
                });
            await context.SaveChangesAsync();
        }

        return new(database, principalId, lineageId);
    }

    private sealed record Estate(LocalDbTestDatabase Database, Guid PrincipalId, Guid LineageId)
    {
        public async Task<Guid> SeedCaseAsync(Guid? engineerId, Guid? signatoryId, int sequence, string type = "inspection")
        {
            await using var context = await Database.CreateContextAsync();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new IntakeReceiptEntity
                {
                    Id = receiptId,
                    SourceFileName = "origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('0', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"origin:{receiptId:N}",
                    ReceivedAtUtc = From,
                    ProcessedAtUtc = From,
                    SourceReaderKey = "report-test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "report test",
                    EvidenceJson = "[]",
                    FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                },
                new CaseEntity
                {
                    Id = caseId,
                    PrincipalId = PrincipalId,
                    SequenceLineageId = LineageId,
                    Year = 2031,
                    Sequence = sequence,
                    Reference = $"EVA3100{sequence}",
                    Type = type,
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
                    State = engineerId is null ? "Review" : "ReportSent",
                    AssignedEngineerId = engineerId,
                    SignOffEngineerId = signatoryId,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
            return caseId;
        }

        /// <summary>An Inspection Case with no Engineer, received at From, and its report; returns the report's generation.</summary>
        public async Task<Guid> SeedReportAsync(int sequence)
        {
            var caseId = await SeedCaseAsync(null, null, sequence);
            var generationId = Guid.NewGuid();
            await using var context = await Database.CreateContextAsync();
            context.Add(Generation(generationId, caseId, From));
            await context.SaveChangesAsync();
            return generationId;
        }
    }
}
