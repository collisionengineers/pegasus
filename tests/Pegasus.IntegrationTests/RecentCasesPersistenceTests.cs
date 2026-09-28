using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class RecentCasesPersistenceTests
{
    private static readonly string[] ExpectedAutomationChangeKinds =
        ["operator_note", "case_field_updated", "audit_created"];
    private static readonly DateTimeOffset Since =
        new(2031, 5, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreationEventsSetArrivalButDoNotDuplicateNewCasesAndAutomationEditsIncludeInitialAndNoteVersions()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var ids = await SeedAsync(database);

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRecentCaseQueries>();
        var first = await queries.ListAsync(Since, 1, 2, CancellationToken.None);
        var second = await queries.ListAsync(Since, 2, 2, CancellationToken.None);
        var third = await queries.ListAsync(Since, 3, 2, CancellationToken.None);

        Assert.Equal(6, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal([ids.Note, ids.InitialEdit], first.Items.Select(item => item.CaseId));
        Assert.Equal([ids.Audited, ids.Guidance], second.Items.Select(item => item.CaseId));
        Assert.Equal([ids.Replacement, ids.Mail], third.Items.Select(item => item.CaseId));
        Assert.Equal(CaseArrival.Email, Assert.Single(third.Items, item => item.CaseId == ids.Mail).Arrival);
        Assert.Equal(CaseArrival.Automation, Assert.Single(third.Items, item => item.CaseId == ids.Replacement).Arrival);
        Assert.Equal(CaseArrival.Manual, Assert.Single(second.Items, item => item.CaseId == ids.Guidance).Arrival);
        Assert.All(
            first.Items.Concat(second.Items).Where(item => item.Kind == RecentCaseRowKind.ChangedByAutomation),
            item => Assert.Contains(item.ChangeKind, ExpectedAutomationChangeKinds));
        Assert.DoesNotContain(
            first.Items.Concat(second.Items).Concat(third.Items),
            item => item.Kind == RecentCaseRowKind.ChangedByAutomation
                && item.ChangeKind is "case_created_as_replacement" or "case_guidance_applied");
    }

    [Fact]
    public async Task EveryPageSizeWalksTheSameRowsInTheSameOrderAsOneWholePage()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedTiesAsync(database);

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRecentCaseQueries>();
        var whole = await queries.ListAsync(Since, 1, 100, CancellationToken.None);

        // Six Cases created at one moment, one after it, and the Automation's
        // changes to two older Cases at the same moments: the Triage Case, a
        // creation event and version-0 guidance never appear.
        Assert.Equal(12, whole.TotalCount);
        Assert.Equal(12, whole.Items.Count);
        Assert.DoesNotContain(whole.Items, item => item.Reference.StartsWith('T'));
        Assert.DoesNotContain(
            whole.Items,
            item => item.ChangeKind is "manual_case_created" or "case_guidance_applied");
        Assert.Equal(
            whole.Items
                .OrderByDescending(item => item.OccurredAtUtc)
                .ThenBy(item => item.Reference, StringComparer.Ordinal)
                .Select(item => (item.OccurredAtUtc, item.Reference)),
            whole.Items.Select(item => (item.OccurredAtUtc, item.Reference)));

        foreach (var pageSize in new[] { 1, 2, 3, 5 })
        {
            var walked = new List<RecentCaseRow>();
            for (var page = 1; ; page++)
            {
                var result = await queries.ListAsync(Since, page, pageSize, CancellationToken.None);
                Assert.Equal(whole.TotalCount, result.TotalCount);
                walked.AddRange(result.Items);
                if (page >= result.TotalPages)
                {
                    break;
                }
            }

            // Ties on one Case at one moment have a fixed order, so the
            // walk is the whole page row for row.
            Assert.Equal(whole.Items, walked);
        }

        var again = await queries.ListAsync(Since, 1, 100, CancellationToken.None);
        Assert.Equal(whole.Items, again.Items);
    }

    [Fact]
    public async Task APageBeyondAnyIntegerIsPastTheEndRatherThanAFailedRead()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedTiesAsync(database);

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRecentCaseQueries>();
        var page = await queries.ListAsync(Since, int.MaxValue, 50, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(12, page.TotalCount);
    }

    private static async Task SeedTiesAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var principal = await SeededPrincipals.QdosAsync(context);
        var tie = Since.AddMinutes(10);
        var later = Since.AddMinutes(11);
        var created = Enumerable.Range(11, 6)
            .Select(sequence => Case(Guid.NewGuid(), principal, sequence, tie))
            .ToArray();
        var afterTie = Case(Guid.NewGuid(), principal, 17, later);
        var olderFirst = Case(Guid.NewGuid(), principal, 18, Since.AddDays(-1));
        var olderSecond = Case(Guid.NewGuid(), principal, 19, Since.AddDays(-1));
        var triage = Case(Guid.NewGuid(), principal, 20, tie);
        triage.Reference = "TQDOS3100020";
        triage.Type = "triage";
        triage.InitialState = null;
        context.AddRange(created);
        context.AddRange(afterTie, olderFirst, olderSecond, triage);
        context.AddRange(created.Append(afterTie).Append(olderFirst).Append(olderSecond)
            .Select(item => Workflow(item.Id)));
        context.AddRange(
            Event(created[0].Id, "manual_case_created", tie, 0, 0),
            Event(created[1].Id, "case_guidance_applied", tie, 0, 1),
            Event(olderFirst.Id, "case_field_updated", tie, 1, 2),
            Event(olderFirst.Id, "audit_created", tie, 2, 3),
            Event(olderFirst.Id, "operator_note", later, 3, 3),
            Event(olderSecond.Id, "case_field_updated", tie, 1, 2),
            Event(olderSecond.Id, "operator_note", tie, 2, 2));
        await context.SaveChangesAsync();
    }

    private static async Task<CaseIds> SeedAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var principal = await SeededPrincipals.QdosAsync(context);
        var mailReceiptId = Guid.NewGuid();
        var mail = Guid.NewGuid();
        var replacement = Guid.NewGuid();
        var guidance = Guid.NewGuid();
        var audited = Guid.NewGuid();
        var initialEdit = Guid.NewGuid();
        var note = Guid.NewGuid();
        context.Add(Receipt(mailReceiptId));
        context.AddRange(
            Case(mail, principal, 1, Since.AddMinutes(50), mailReceiptId),
            Case(replacement, principal, 2, Since.AddMinutes(51)),
            Case(guidance, principal, 3, Since.AddMinutes(52)),
            Case(audited, principal, 4, Since.AddDays(-1)),
            Case(initialEdit, principal, 5, Since.AddDays(-1)),
            Case(note, principal, 6, Since.AddDays(-1)),
            Workflow(mail),
            Workflow(replacement),
            Workflow(guidance),
            Workflow(audited),
            Workflow(initialEdit),
            Workflow(note),
            Event(replacement, "case_created_as_replacement", Since.AddMinutes(51), 0, 0),
            Event(guidance, "case_guidance_applied", Since.AddMinutes(52), 0, 0),
            Event(audited, "audit_created", Since.AddMinutes(53), 0, 1),
            Event(initialEdit, "case_field_updated", Since.AddMinutes(54), 0, 1),
            Event(note, "operator_note", Since.AddMinutes(55), 0, 0));
        await context.SaveChangesAsync();
        return new(mail, replacement, guidance, audited, initialEdit, note);
    }

    private static IntakeReceiptEntity Receipt(Guid id) => new()
    {
        Id = id,
        SourceFileName = "mail.eml",
        MediaType = "message/rfc822",
        SourceLength = 1,
        SourceHash = new string('a', 64),
        SourceChannel = "mailbox",
        ExternalReceiptToken = $"recent:{id:N}",
        ReceivedAtUtc = Since,
        ProcessedAtUtc = Since,
        SourceReaderKey = "test",
        SourceReaderVersion = "1",
        Decision = "case_created",
        DecisionReason = "Test.",
        EvidenceJson = "[]",
        FieldsJson = "[]",
        OcrCandidatesJson = "[]"
    };

    private static CaseEntity Case(
        Guid id,
        SeededPrincipalTestData principal,
        int sequence,
        DateTimeOffset createdAtUtc,
        Guid? originReceiptId = null) => new()
    {
        Id = id,
        PrincipalId = principal.Id,
        SequenceLineageId = principal.SequenceLineageId,
        Year = 2031,
        Sequence = sequence,
        Reference = $"QDOS3100{sequence}",
        Type = "inspection",
        InitialState = "Review",
        CustodyState = "Confirmed",
        OriginIntakeReceiptId = originReceiptId,
        CreatedAtUtc = createdAtUtc,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseWorkflowEntity Workflow(Guid caseId) => new()
    {
        CaseId = caseId,
        State = "Review",
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseWorkflowEventEntity Event(
        Guid caseId,
        string eventType,
        DateTimeOffset occurredAtUtc,
        long beforeVersion,
        long afterVersion) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        EventType = eventType,
        OperationKey = $"recent:{caseId:N}:{eventType}",
        RequestHash = new string('b', 64),
        ActorKind = "Automation",
        ActorSubjectId = "recent-cases-test",
        ActorRolesJson = "[]",
        OccurredAtUtc = occurredAtUtc,
        BeforeVersion = beforeVersion,
        AfterVersion = afterVersion
    };

    private sealed record CaseIds(
        Guid Mail,
        Guid Replacement,
        Guid Guidance,
        Guid Audited,
        Guid InitialEdit,
        Guid Note);
}
