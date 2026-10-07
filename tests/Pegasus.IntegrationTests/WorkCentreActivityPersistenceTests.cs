using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Activity figures read from the real stores (FRD-15, v32 items C to H):
/// each figure counts only its own facts, only inside its own window, and
/// the sources settled with the operator on 5 October 2026.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class WorkCentreActivityPersistenceTests
{
    // Wednesday 7 October 2026 in Europe/London: the day starts at 23:00Z the
    // evening before and the week on Sunday 4 October at 23:00Z.
    private static readonly DateTimeOffset DayStart = new(2026, 10, 6, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeekStart = new(2026, 10, 4, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EachFigureCountsOnlyItsOwnFactsInsideItsOwnWindow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
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
            // week. A later return to the Engineer is not a second send, so
            // the second entries below count for nothing.
            context.AddRange(
                Event(today.Id, "state_ReportPreparation", DayStart.AddHours(3)),
                Event(today.Id, "state_ReportPreparation", DayStart.AddHours(9)),
                Event(thisWeek.Id, "state_ReportPreparation", WeekStart.AddHours(12)),
                Event(lastWeek.Id, "state_ReportPreparation", WeekStart.AddDays(-1)),
                Event(lastWeek.Id, "state_ReportPreparation", DayStart.AddHours(2)));

            // Reports sent: sent report e-mails as MI-01 counts them (item F).
            // A sent general e-mail and a failed report send are not reports sent.
            context.AddRange(
                Mail(StaffMailPurpose.CaseReport, StaffMailState.Sent, DayStart.AddHours(4)),
                Mail(StaffMailPurpose.CaseReport, StaffMailState.Sent, WeekStart.AddHours(20)),
                Mail(StaffMailPurpose.CaseReport, StaffMailState.Sent, WeekStart.AddDays(-3)),
                Mail(StaffMailPurpose.GeneralCorrespondence, StaffMailState.Sent, DayStart.AddHours(4)),
                Mail(StaffMailPurpose.CaseReport, StaffMailState.Failed, null));

            // Completed: every entry into Complete this week (item H), whether by
            // Complete, by a reply to a post-report query or by a withdrawn query;
            // a query received leaves Complete and is not one; last week is not counted.
            context.AddRange(
                Event(today.Id, "case_completed", DayStart.AddHours(6)),
                Event(thisWeek.Id, "case_query_replied", WeekStart.AddHours(30)),
                Event(thisWeek.Id, "case_query_withdrawn", WeekStart.AddHours(40), version: 3),
                Event(lastWeek.Id, "case_completed", WeekStart.AddDays(-1)),
                Event(todayToo.Id, "case_query_received", DayStart.AddHours(7)));

            // E-mails received: mailbox receipts today; an upload is also a
            // receipt and is not counted; yesterday's mail is not today's.
            context.AddRange(
                Receipt("mailbox", DayStart.AddHours(1)),
                Receipt("mailbox", DayStart.AddHours(8)),
                Receipt("upload", DayStart.AddHours(2)),
                Receipt("mailbox", DayStart.AddHours(-3)));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var counts = await scope.ServiceProvider.GetRequiredService<IWorkCentreActivityQueries>()
            .GetAsync(DayStart, WeekStart, CancellationToken.None);

        Assert.Equal(new WorkCentreActivityCounts(
            NewCasesToday: 2,
            SentToEngineerToday: 1,
            SentToEngineerThisWeek: 2,
            ReportsSentToday: 1,
            ReportsSentThisWeek: 2,
            CompletedThisWeek: 3,
            EmailsReceivedToday: 2), counts);
    }

    [Fact]
    public async Task AnEmptyEstateReadsZeroForEveryFigure()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();

        var counts = await scope.ServiceProvider.GetRequiredService<IWorkCentreActivityQueries>()
            .GetAsync(DayStart, WeekStart, CancellationToken.None);

        Assert.Equal(new WorkCentreActivityCounts(0, 0, 0, 0, 0, 0, 0), counts);
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

    private static StaffMailSendOperationEntity Mail(StaffMailPurpose purpose, StaffMailState state, DateTimeOffset? sentAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        ActorSubjectId = Guid.NewGuid().ToString("D"),
        MailboxId = Guid.NewGuid(),
        MailboxGeneration = 1,
        OperationKey = $"send:{Guid.NewGuid():N}",
        PayloadHash = new string('2', 64),
        Purpose = purpose,
        ContextId = Guid.NewGuid(),
        ContextVersion = 1,
        ComposeMode = StaffMailComposeMode.New,
        RecipientsJson = "[]",
        Subject = "report",
        Body = "report",
        AttachmentsJson = "[]",
        State = state,
        CorrelationMarker = "test",
        CreatedAtUtc = sentAtUtc ?? DayStart.AddHours(4),
        RequestedAtUtc = sentAtUtc ?? DayStart.AddHours(4),
        ObservedSentAtUtc = sentAtUtc,
        Version = 1,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseWorkflowEventEntity Event(Guid caseId, string eventType, DateTimeOffset occurredAtUtc, long version = 2) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        EventType = eventType,
        OperationKey = $"activity:{caseId:N}:{eventType}:{occurredAtUtc:O}",
        RequestHash = new string('b', 64),
        ActorKind = "Staff",
        ActorSubjectId = "activity-test",
        ActorRolesJson = "[]",
        OccurredAtUtc = occurredAtUtc,
        BeforeVersion = version - 1,
        AfterVersion = version
    };

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
