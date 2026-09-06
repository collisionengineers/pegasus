using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// PLAT-048 / MI-01. The Engineer Report counts against a real database:
/// reports are case-linked Sent evidence on the Engineer's cases, queries are
/// post-report mailbox receipts associated with them (D12), both bounded by
/// the half-open period, and an association the operator reversed no longer
/// counts.
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

        await using (var context = await database.CreateContextAsync())
        {
            // The send actor is the engineering-history dimension. It is deliberately
            // neither the case's assigned engineer nor its signatory.
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(engineerB, From.AddDays(3)),
                SentOperation(engineerA, From.AddDays(10)),
                SentOperation(engineerB, To.AddSeconds(-1)),
                SentOperation(engineerA, To),
                SentOperation(engineerA, From.AddSeconds(-1)),
                SentOperation("not-a-staff-id", From.AddDays(5)));
            context.IntakeReceipts.AddRange(
                Query(From.AddDays(1), "post-report-emails", caseA, active: true),
                Query(From.AddDays(2), "post-report-emails", caseA, active: true),
                Query(From.AddDays(2), "post-report-emails", caseA, active: false),
                Query(From.AddDays(2), "instructions", caseA, active: true),
                Query(To, "post-report-emails", caseA, active: true),
                Query(From.AddDays(4), "post-report-emails", caseB, active: true),
                Query(From.AddDays(4), "post-report-emails", unassigned, active: true));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEngineerActivityQueries>();

        var all = await queries.GetAsync(From, To, null, CancellationToken.None);
        var onlyB = await queries.GetAsync(From, To, engineerB, CancellationToken.None);

        Assert.Equal(
            new[] { new EngineerActivityCounts(engineerA, 1, 2), new EngineerActivityCounts(engineerB, 2, 1) }
                .OrderBy(item => item.EngineerId),
            all);
        Assert.Equal([new EngineerActivityCounts(engineerB, 2, 1)], onlyB);
    }

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

    private static StaffMailSendOperationEntity SentOperation(object actor, DateTimeOffset sentAtUtc)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            ActorSubjectId = actor.ToString()!,
            MailboxId = Guid.NewGuid(),
            MailboxGeneration = 1,
            OperationKey = $"send:{id:N}",
            PayloadHash = new string('7', 64),
            Purpose = Pegasus.Core.Operations.StaffMailPurpose.CaseReport,
            ContextId = Guid.NewGuid(),
            ContextVersion = 1,
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

    private static IntakeReceiptEntity Query(
        DateTimeOffset receivedAtUtc,
        string family,
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
                Direction = "received",
                Family = family,
                AmbiguousCandidatesJson = "[]",
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
                new OrganizationEntity { Id = organizationId, Name = "Report test", Version = 0 },
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
        public async Task<Guid> SeedCaseAsync(Guid? engineerId, Guid? signatoryId, int sequence)
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
                    State = engineerId is null ? "Review" : "PostReport",
                    AssignedEngineerId = engineerId,
                    SignOffEngineerId = signatoryId,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
            return caseId;
        }
    }
}
