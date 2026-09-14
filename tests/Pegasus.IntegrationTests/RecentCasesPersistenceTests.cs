using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class RecentCasesPersistenceTests
{
    private static readonly string[] ExpectedAutomationChangeKinds =
        ["operator_note", "case_field_updated", "audit_case_created"];
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
        Assert.Equal([ids.AuditOriginal, ids.Guidance], second.Items.Select(item => item.CaseId));
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

    private static async Task<CaseIds> SeedAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var principal = await SeededPrincipals.QdosAsync(context);
        var mailReceiptId = Guid.NewGuid();
        var mail = Guid.NewGuid();
        var replacement = Guid.NewGuid();
        var guidance = Guid.NewGuid();
        var auditOriginal = Guid.NewGuid();
        var initialEdit = Guid.NewGuid();
        var note = Guid.NewGuid();
        context.Add(Receipt(mailReceiptId));
        context.AddRange(
            Case(mail, principal, 1, Since.AddMinutes(50), mailReceiptId),
            Case(replacement, principal, 2, Since.AddMinutes(51)),
            Case(guidance, principal, 3, Since.AddMinutes(52)),
            Case(auditOriginal, principal, 4, Since.AddDays(-1)),
            Case(initialEdit, principal, 5, Since.AddDays(-1)),
            Case(note, principal, 6, Since.AddDays(-1)),
            Workflow(mail),
            Workflow(replacement),
            Workflow(guidance),
            Workflow(auditOriginal),
            Workflow(initialEdit),
            Workflow(note),
            Event(replacement, "case_created_as_replacement", Since.AddMinutes(51), 0, 0),
            Event(guidance, "case_guidance_applied", Since.AddMinutes(52), 0, 0),
            Event(auditOriginal, "audit_case_created", Since.AddMinutes(53), 0, 1),
            Event(initialEdit, "case_field_updated", Since.AddMinutes(54), 0, 1),
            Event(note, "operator_note", Since.AddMinutes(55), 0, 0));
        await context.SaveChangesAsync();
        return new(mail, replacement, guidance, auditOriginal, initialEdit, note);
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
        Type = "Inspection",
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
        Guid AuditOriginal,
        Guid InitialEdit,
        Guid Note);
}
