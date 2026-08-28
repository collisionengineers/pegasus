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
    public async Task CountsReportsAndQueriesPerAssignedEngineerWithinThePeriod()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var engineerA = Guid.NewGuid();
        var engineerB = Guid.NewGuid();
        var estate = await SeedEstateAsync(database);
        var caseA = await estate.SeedCaseAsync(engineerA, 1);
        var caseB = await estate.SeedCaseAsync(engineerB, 2);
        var unassigned = await estate.SeedCaseAsync(null, 3);

        await using (var context = await database.CreateContextAsync())
        {
            context.CaseReportSentEvidence.AddRange(
                SentEvidence(caseA, From.AddDays(3)),
                SentEvidence(caseA, From.AddDays(10)),
                SentEvidence(caseB, To.AddSeconds(-1)),
                SentEvidence(caseB, To),
                SentEvidence(caseA, From.AddSeconds(-1)),
                SentEvidence(unassigned, From.AddDays(5)),
                SentEvidence(null, From.AddDays(5)));
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
            new[] { new EngineerActivityCounts(engineerA, 2, 2), new EngineerActivityCounts(engineerB, 1, 1) }
                .OrderBy(item => item.EngineerId),
            all);
        Assert.Equal([new EngineerActivityCounts(engineerB, 1, 1)], onlyB);
    }

    [Fact]
    public async Task AnEngineerWithNoActivityInThePeriodIsAbsent()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        await estate.SeedCaseAsync(Guid.NewGuid(), 1);

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEngineerActivityQueries>();

        Assert.Empty(await queries.GetAsync(From, To, null, CancellationToken.None));
    }

    private static CaseReportSentEvidenceEntity SentEvidence(Guid? caseId, DateTimeOffset sentAtUtc)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            CaseId = caseId,
            MailboxIdentity = "instructions@collisionengineers.co.uk",
            SentFolderIdentity = "sent",
            ImmutableItemIdentity = $"item-{id:N}",
            InternetMessageIdentity = $"message-{id:N}",
            ConversationIdentity = $"conversation-{id:N}",
            ReplyChainIdentity = $"reply-chain-{id:N}",
            SourceOccurrenceIdentity = $"occurrence-{id:N}",
            SourceSha256 = new string('7', 64),
            MimeSha256 = new string('8', 64),
            SentAtUtc = sentAtUtc,
            DiscoveredAtUtc = sentAtUtc.AddMinutes(1),
            DiscoveredByKind = "SystemWorker",
            DiscoveredBySubjectId = "sent-evidence-poll",
            RetentionOperationKey = $"retain:{id:N}",
            RetentionRequestHash = new string('0', 64)
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
        public async Task<Guid> SeedCaseAsync(Guid? engineerId, int sequence)
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
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
            return caseId;
        }
    }
}
