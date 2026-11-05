using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Activity figures read from the real stores (FRD-15, v32 items C to H):
/// each figure counts only its own facts, only inside its own window, and
/// the sources settled with the operator on 5 October 2026. Reports sent
/// counts reports on their first observed send (R673).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class WorkCentreActivityPersistenceTests
{
    // Wednesday 7 October 2026 in Europe/London: the day starts at 23:00Z the
    // evening before and the week on Sunday 4 October at 23:00Z.
    private static readonly DateTimeOffset DayStart = new(2026, 10, 6, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeekStart = new(2026, 10, 4, 23, 0, 0, TimeSpan.Zero);

    private static readonly WorkCentreActivityCounts SeededCounts = new(
        NewCases: new(Today: 2, ThisWeek: 3),
        SentToEngineer: new(Today: 1, ThisWeek: 2),
        ReportsSent: new(Today: 3, ThisWeek: 4),
        EmailsReceived: new(Today: 2, ThisWeek: 3));

    [Fact]
    public async Task EachFigureCountsOnlyItsOwnFactsInsideItsOwnWindow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedAsync(database);

        await using var scope = database.CreateAsyncScope();
        var counts = await scope.ServiceProvider.GetRequiredService<IWorkCentreActivityQueries>()
            .GetAsync(DayStart, WeekStart, CancellationToken.None);

        Assert.Equal(SeededCounts, counts);
    }

    /// <summary>
    /// Each figure is one command whatever rows it reads: the week and the day
    /// come from one conditional aggregate, so a load sends four.
    /// </summary>
    [Fact]
    public async Task EachFigureIsOneCommandWhateverRowsItReads()
    {
        var counter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(counter));
        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IWorkCentreActivityQueries>();

        counter.Reset();
        _ = await queries.GetAsync(DayStart, WeekStart, CancellationToken.None);
        var withNoRows = counter.Count;

        await SeedAsync(database);
        counter.Reset();
        var counts = await queries.GetAsync(DayStart, WeekStart, CancellationToken.None);

        Assert.Equal(SeededCounts, counts);
        Assert.True(withNoRows <= 4, $"An empty estate sent {withNoRows} commands; one per figure is four at most.");
        Assert.True(
            counter.Count <= 4,
            $"The seeded estate sent {counter.Count} commands; one per figure is four at most."
            + Environment.NewLine + string.Join(Environment.NewLine, counter.Statements));
    }

    private static async Task SeedAsync(LocalDbTestDatabase database)
    {
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            // New cases: two today, one earlier this week, one last week, and a
            // Triage Case today that the New cases list leaves out (item G).
            var today = Case(Guid.NewGuid(), principal, 1, DayStart.AddHours(2));
            var todayToo = Case(Guid.NewGuid(), principal, 2, DayStart.AddHours(5));
            var thisWeek = Case(Guid.NewGuid(), principal, 3, WeekStart.AddHours(10));
            var lastWeek = Case(Guid.NewGuid(), principal, 4, WeekStart.AddDays(-2));
            var triage = Case(Guid.NewGuid(), principal, 5, DayStart.AddHours(1));
            triage.Reference = "TQDOS2600005";
            triage.Type = "triage";
            triage.InitialState = null;
            context.AddRange(today, todayToo, thisWeek, lastWeek, triage);
            context.AddRange(Workflow(today.Id), Workflow(todayToo.Id), Workflow(thisWeek.Id), Workflow(lastWeek.Id));

            // Sent to Engineer: a Case's first entry into With Engineer, once
            // per Case (item E): one today, one earlier this week, one last
            // week. A later return to the Engineer, or a With Engineer Case
            // handed to another Engineer or to Unassigned (#1213, the same
            // state event), is not a second send, so the second entries below
            // count for nothing.
            context.AddRange(
                Event(today.Id, "state_ReportPreparation", DayStart.AddHours(3), version: 4),
                Event(today.Id, "state_ReportPreparation", DayStart.AddHours(9), version: 5),
                Event(thisWeek.Id, "state_ReportPreparation", WeekStart.AddHours(12), version: 4),
                Event(lastWeek.Id, "state_ReportPreparation", WeekStart.AddDays(-1), version: 4),
                Event(lastWeek.Id, "state_ReportPreparation", DayStart.AddHours(2), version: 5));

            // Reports sent: each report once, on its first observed send, by
            // whoever sent it (R673): one first sent today, whose re-send later
            // today is not a second report; one first sent earlier this week,
            // re-sent today; one first sent last week, whose re-send today
            // counts for nothing; and an Inspection + Audit Case, created last
            // week, whose two reports went in one e-mail today and are two. A
            // report e-mail not yet seen in Sent Items is not a send, and a
            // reply on a Case is not a report.
            var inspectionAndAudit = Case(Guid.NewGuid(), principal, 6, WeekStart.AddDays(-3));
            inspectionAndAudit.Type = "inspection_and_audit";
            var auditWork = new CaseWorkEntity
            {
                Id = Guid.NewGuid(),
                CaseId = inspectionAndAudit.Id,
                Kind = CaseWorkKinds.Audit,
                CreatedAtUtc = WeekStart.AddDays(-1)
            };
            context.AddRange(inspectionAndAudit, auditWork);
            var todayReport = Generation(today.Id, today.Id, DayStart.AddHours(3));
            var todayTooReport = Generation(todayToo.Id, todayToo.Id, DayStart.AddHours(6));
            var thisWeekReport = Generation(thisWeek.Id, thisWeek.Id, WeekStart.AddHours(19));
            var lastWeekReport = Generation(lastWeek.Id, lastWeek.Id, WeekStart.AddDays(-1).AddHours(-1));
            var inspectionReport = Generation(inspectionAndAudit.Id, inspectionAndAudit.Id, WeekStart.AddDays(-2));
            var auditReport = Generation(inspectionAndAudit.Id, auditWork.Id, DayStart.AddHours(5));
            context.AddRange(todayReport, todayTooReport, thisWeekReport, lastWeekReport, inspectionReport, auditReport);
            context.AddRange(
                Send(todayReport.Id, DayStart.AddHours(4)),
                Send(todayReport.Id, DayStart.AddHours(9)),
                Send(thisWeekReport.Id, WeekStart.AddHours(20)),
                Send(thisWeekReport.Id, DayStart.AddHours(10)),
                Send(lastWeekReport.Id, WeekStart.AddDays(-1)),
                Send(lastWeekReport.Id, DayStart.AddHours(3)),
                Send(auditReport.Id, DayStart.AddHours(6), secondReportId: inspectionReport.Id),
                Send(todayTooReport.Id, DayStart.AddHours(7), observed: false),
                Send(todayToo.Id, DayStart.AddHours(8), purpose: StaffMailPurpose.GeneralCorrespondence));

            // E-mails received: mailbox receipts; an upload is also a receipt
            // and is not counted; yesterday's mail is this week's, not today's,
            // and last week's is neither.
            context.AddRange(
                Receipt("mailbox", DayStart.AddHours(1)),
                Receipt("mailbox", DayStart.AddHours(8)),
                Receipt("upload", DayStart.AddHours(2)),
                Receipt("mailbox", DayStart.AddHours(-3)),
                Receipt("mailbox", WeekStart.AddHours(-1)));
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task AnEmptyEstateReadsZeroForEveryFigure()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();

        var counts = await scope.ServiceProvider.GetRequiredService<IWorkCentreActivityQueries>()
            .GetAsync(DayStart, WeekStart, CancellationToken.None);

        var none = new WorkCentreActivityFigure(0, 0);
        Assert.Equal(new WorkCentreActivityCounts(none, none, none, none), counts);
    }

    private static CaseEntity Case(Guid id, SeededPrincipalTestData principal, int sequence, DateTimeOffset createdAtUtc) => new()
    {
        Id = id,
        PrincipalId = principal.Id,
        SequenceLineageId = principal.SequenceLineageId,
        Year = 2026,
        Sequence = sequence,
        Reference = $"QDOS2600{sequence}",
        Type = "inspection",
        InitialState = "Review",
        CustodyState = "Confirmed",
        CreatedAtUtc = createdAtUtc,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseWorkflowEntity Workflow(Guid caseId) => new()
    {
        CaseId = caseId,
        State = "Review",
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseWorkflowEventEntity Event(Guid caseId, string eventType, DateTimeOffset occurredAtUtc, long version = 2) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        EventType = eventType,
        OperationKey = $"activity:{caseId:N}:{eventType}:{occurredAtUtc:yyyyMMddHHmmss}",
        RequestHash = new string('b', 64),
        ActorKind = "Staff",
        ActorSubjectId = "activity-test",
        ActorRolesJson = "[]",
        OccurredAtUtc = occurredAtUtc,
        BeforeVersion = version - 1,
        AfterVersion = version
    };

    /// <summary>A report: one generation of one work.</summary>
    private static CaseReportGenerationEntity Generation(Guid caseId, Guid workId, DateTimeOffset generatedAtUtc)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            CaseId = caseId,
            WorkId = workId,
            SnapshotHash = $"{id:N}{id:N}",
            SnapshotJson = "{}",
            TemplateVersion = "1",
            RendererVersion = "1",
            State = "Confirmed",
            GeneratedAtUtc = generatedAtUtc
        };
    }

    /// <summary>
    /// A Pegasus e-mail: by default a report e-mail seen in Sent Items,
    /// carrying the generation <paramref name="contextId"/> names and, when
    /// it carried both works' reports, <paramref name="secondReportId"/>.
    /// </summary>
    private static StaffMailSendOperationEntity Send(
        Guid contextId,
        DateTimeOffset sentAtUtc,
        Guid? secondReportId = null,
        StaffMailPurpose purpose = StaffMailPurpose.CaseReport,
        bool observed = true)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            ActorKind = ActorKind.Staff,
            ActorSubjectId = Guid.NewGuid().ToString("D"),
            MailboxId = Guid.NewGuid(),
            MailboxGeneration = 1,
            OperationKey = $"send:{id:N}",
            PayloadHash = new string('7', 64),
            Purpose = purpose,
            ContextId = contextId,
            ContextVersion = 1,
            SecondContextId = secondReportId,
            SecondContextVersion = secondReportId is null ? null : 1,
            ComposeMode = StaffMailComposeMode.New,
            RecipientsJson = "[]",
            Subject = "report",
            Body = "report",
            AttachmentsJson = "[]",
            State = observed ? StaffMailState.Sent : StaffMailState.Submitted,
            CorrelationMarker = $"mail:{id:N}",
            CreatedAtUtc = sentAtUtc,
            RequestedAtUtc = sentAtUtc,
            SubmittedAtUtc = sentAtUtc,
            ObservedSentAtUtc = observed ? sentAtUtc : null,
            Version = 1,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private static IntakeReceiptEntity Receipt(string channel, DateTimeOffset receivedAtUtc)
    {
        var id = Guid.NewGuid();
        return new IntakeReceiptEntity
        {
            Id = id,
            SourceFileName = channel == "mailbox" ? "mail.eml" : "photo.jpg",
            MediaType = channel == "mailbox" ? "message/rfc822" : "image/jpeg",
            SourceLength = 1,
            SourceHash = new string('a', 64),
            SourceChannel = channel,
            ExternalReceiptToken = $"activity:{id:N}",
            ReceivedAtUtc = receivedAtUtc,
            ProcessedAtUtc = receivedAtUtc,
            SourceReaderKey = "test",
            SourceReaderVersion = "1",
            Decision = "case_created",
            DecisionReason = "Test.",
            EvidenceJson = "[]",
            FieldsJson = "[]",
            OcrCandidatesJson = "[]"
        };
    }
}
