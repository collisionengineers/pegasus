using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// System Actions (v42 Design A) is one batched read over the records the
/// actions wrote. One seeded row of each kind, and a dismissal of the
/// underlying record id hides that row only.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class SystemActionsPersistenceTests
{
    private static readonly DateTimeOffset At = new(2031, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EachKindIsReadFromItsRecordAndDismissalHidesThatRecordOnly()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var ids = await SeedAsync(database);

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ISystemActionQueries>();
        var page = await queries.ListAsync(At.AddDays(-1), SystemActionFilter.All, null, 1, 50, CancellationToken.None);

        Assert.Equal(4, page.TotalCount);
        Assert.Equal(1, page.KindCounts[SystemActionKind.AutoMatchedMail]);
        Assert.Equal(1, page.KindCounts[SystemActionKind.VehicleImagesPaired]);
        Assert.Equal(1, page.KindCounts[SystemActionKind.QueryOpenedAutomatically]);
        Assert.Equal(1, page.KindCounts[SystemActionKind.CancellationMatched]);

        var mail = Assert.Single(page.Items, row => row.Kind == SystemActionKind.AutoMatchedMail);
        Assert.Equal(ids.MailReceipt, mail.RecordId);
        Assert.Equal(ids.MailCase, mail.CaseId);
        Assert.Contains("conversation thread", mail.Signal, StringComparison.Ordinal);

        var images = Assert.Single(page.Items, row => row.Kind == SystemActionKind.VehicleImagesPaired);
        Assert.Equal(ids.ImageIntake, images.RecordId);
        Assert.Equal("GJ13EVC-01", images.Summary);

        var query = Assert.Single(page.Items, row => row.Kind == SystemActionKind.QueryOpenedAutomatically);
        Assert.Equal(ids.Query, query.RecordId);
        Assert.Equal(ids.Query, query.QueryId);
        Assert.True(query.QueryUnassigned);

        var cancellation = Assert.Single(page.Items, row => row.Kind == SystemActionKind.CancellationMatched);
        Assert.Equal(ids.CancellationReceipt, cancellation.RecordId);

        await scope.ServiceProvider.GetRequiredService<IWorkCentreDismissalStore>()
            .DismissAsync(ids.MailReceipt, At.AddMinutes(5), ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), CancellationToken.None);
        var after = await queries.ListAsync(At.AddDays(-1), SystemActionFilter.All, null, 1, 50, CancellationToken.None);
        Assert.DoesNotContain(after.Items, row => row.RecordId == ids.MailReceipt);
        Assert.Contains(after.Items, row => row.RecordId == ids.Query);
        Assert.Equal(0, after.KindCounts[SystemActionKind.AutoMatchedMail]);
    }

    private static async Task<Ids> SeedAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var principal = await SeededPrincipals.QdosAsync(context);
        var mailCase = Guid.NewGuid();
        var imageCase = Guid.NewGuid();
        var queryCase = Guid.NewGuid();
        var cancelCase = Guid.NewGuid();
        var mailReceipt = Guid.NewGuid();
        var cancelReceipt = Guid.NewGuid();
        var imageIntake = Guid.NewGuid();
        var queryId = Guid.NewGuid();
        var mergeKey = $"image-merge:{imageCase:N}";

        context.AddRange(
            Case(mailCase, principal, 1),
            Case(imageCase, principal, 2),
            Case(queryCase, principal, 3),
            Case(cancelCase, principal, 4),
            Workflow(mailCase),
            Workflow(imageCase),
            Workflow(queryCase),
            Workflow(cancelCase),
            Receipt(mailReceipt, "mail"),
            Receipt(cancelReceipt, "cancel"));
        context.AddRange(
            Association(mailReceipt, mailCase, "Automatic association from conversation thread."),
            Association(cancelReceipt, cancelCase, "Cancellation linked."),
            History(mailReceipt, mailCase),
            Message(mailReceipt, "mail", "Revised figures"),
            Message(cancelReceipt, "cancel", "Instruction cancellation"),
            Classification(cancelReceipt),
            new CaseWorkflowEventEntity
            {
                Id = Guid.NewGuid(),
                CaseId = imageCase,
                EventType = "image_initiated_case_merged",
                OperationKey = mergeKey,
                RequestHash = new string('b', 64),
                ActorKind = "SystemWorker",
                ActorSubjectId = "system-worker:image-intake",
                ActorRolesJson = "[]",
                Reason = "Image-initiated case GJ13EVC-01 merged into QDOS3100002: paired.",
                OccurredAtUtc = At,
                BeforeVersion = 0,
                AfterVersion = 1
            },
            new ActionHistoryEntity
            {
                Id = Guid.NewGuid(),
                AggregateType = "case",
                AggregateId = imageCase.ToString("D"),
                EventKind = "image_initiated_case_merged",
                ActorKind = "SystemWorker",
                ActorSubjectId = "system-worker:image-intake",
                ActorRolesJson = "[]",
                OccurredAtUtc = At,
                Outcome = "Succeeded",
                CorrelationId = mergeKey,
                AfterJson = $"{{\"ImageIntakeId\":\"{imageIntake:D}\",\"ImageIntakeReference\":\"GJ13EVC-01\",\"State\":\"merged\"}}"
            },
            new CaseQueryEntity
            {
                Id = queryId,
                CaseId = queryCase,
                Source = "Mail",
                IntakeReceiptId = mailReceipt,
                RaisedAtUtc = At.AddMinutes(1),
                RaisedByKind = "SystemWorker",
                RaisedBySubjectId = "system-worker:query-criteria",
                RaisedOperationKey = $"query:{queryId:N}"
            });
        await context.SaveChangesAsync();
        return new Ids(mailCase, mailReceipt, imageIntake, queryId, cancelReceipt);
    }

    private static CaseEntity Case(Guid id, SeededPrincipalTestData principal, int sequence) => new()
    {
        Id = id,
        PrincipalId = principal.Id,
        SequenceLineageId = principal.SequenceLineageId,
        Year = 2031,
        Sequence = sequence,
        Reference = $"QDOS310000{sequence}",
        Type = "inspection",
        InitialState = "Review",
        CustodyState = "Confirmed",
        CreatedAtUtc = At,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseWorkflowEntity Workflow(Guid caseId) => new()
    {
        CaseId = caseId,
        State = "Review",
        ConcurrencyToken = Guid.NewGuid()
    };

    private static IntakeReceiptEntity Receipt(Guid id, string token) => new()
    {
        Id = id,
        SourceFileName = $"{token}.eml",
        MediaType = "message/rfc822",
        SourceLength = 1,
        SourceHash = new string(token[0], 64),
        SourceChannel = "mailbox",
        ExternalReceiptToken = $"sys:{token}:{id:N}",
        ReceivedAtUtc = At,
        ProcessedAtUtc = At,
        SourceReaderKey = "test",
        SourceReaderVersion = "1",
        Decision = "associated",
        DecisionReason = "Test.",
        EvidenceJson = "[]",
        FieldsJson = "[]",
        OcrCandidatesJson = "[]"
    };

    private static IntakeManualAssociationEntity Association(Guid receiptId, Guid caseId, string reason) => new()
    {
        IntakeReceiptId = receiptId,
        CaseId = caseId,
        IsActive = true,
        Version = 0,
        LinkedAtUtc = At,
        ActorKind = "SystemWorker",
        ActorSubjectId = "system-worker:intake-processing",
        ActorRolesJson = "[]",
        Reason = reason,
        LastOperationKey = $"assoc:{receiptId:N}",
        MatchPolicyKey = "mail_case_association",
        MatchPolicyVersion = 2
    };

    private static IntakeMutationHistoryEntity History(Guid receiptId, Guid caseId) => new()
    {
        Id = Guid.NewGuid(),
        IntakeReceiptId = receiptId,
        CaseId = caseId,
        EventType = "intake_case_linked_automatic",
        ActorKind = "SystemWorker",
        ActorSubjectId = "system-worker:intake-processing",
        ActorRolesJson = "[]",
        Reason = "Automatic association from conversation thread.",
        OperationKey = $"link:{receiptId:N}",
        RequestFingerprint = new string('c', 64),
        OccurredAtUtc = At
    };

    private static RetainedMailboxMessageEntity Message(Guid receiptId, string token, string subject) => new()
    {
        Id = Guid.NewGuid(),
        MailboxAddress = "desk@example.test",
        FolderScope = "Inbox",
        FolderIdentity = "inbox",
        ImmutableMessageId = receiptId.ToString("N"),
        ExternalReceiptToken = $"sys:{token}:{receiptId:N}",
        ToAddressesJson = "[]",
        CcAddressesJson = "[]",
        Subject = subject,
        SourceLength = 1,
        SourceSha256 = new string('d', 64),
        ReceivedAtUtc = At,
        RetainedAtUtc = At
    };

    private static IntakeMailClassificationDecisionEntity Classification(Guid receiptId) => new()
    {
        IntakeReceiptId = receiptId,
        Outcome = "classified",
        Category = "cancellation",
        IsReplyContext = false,
        PredicatesJson = "[]",
        Reason = "Cancellation.",
        PolicyKey = "principal_mail_classification",
        PolicyVersion = 1,
        DecidedByActor = "system-worker:intake-processing",
        DecidedAtUtc = At,
        Version = 1,
        ConcurrencyToken = Guid.NewGuid()
    };

    private sealed record Ids(Guid MailCase, Guid MailReceipt, Guid ImageIntake, Guid Query, Guid CancellationReceipt);
}
