using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Custody;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Service health rows that read tables no other port exposes
/// — the Sent-items poll cursors, the intake dispatcher by state, and the
/// EVA failure and pending-work reads — resolved through the registered
/// adapters against a real database.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ServiceHealthPersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private const string Mailbox = "instructions@collisionengineers.co.uk";

    [Fact]
    public async Task SentEvidencePollStatusReadsEachCursorRow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            context.ApprovedSentPollStates.AddRange(
                new ApprovedSentPollStateEntity
                {
                    MailboxId = "mailbox-b",
                    MailboxAddress = "reports@collisionengineers.co.uk",
                    SentFolderIdentity = "sent-b",
                    DueAtUtc = FixedUtcNow.AddMinutes(5),
                    LastCompletedAtUtc = null,
                    LastFailureCode = "graph_unavailable"
                },
                new ApprovedSentPollStateEntity
                {
                    MailboxId = "mailbox-a",
                    MailboxAddress = Mailbox,
                    SentFolderIdentity = "sent-a",
                    DueAtUtc = FixedUtcNow.AddMinutes(5),
                    LastCompletedAtUtc = FixedUtcNow.AddMinutes(-4),
                    LastFailureCode = null
                });
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IServiceHealthQueries>();

        var status = await queries.ListSentEvidencePollStatusAsync(CancellationToken.None);

        Assert.Collection(
            status,
            row => Assert.Equal(new SentEvidencePollStatus(Mailbox, FixedUtcNow.AddMinutes(5), FixedUtcNow.AddMinutes(-4), null), row),
            row => Assert.Equal(new SentEvidencePollStatus("reports@collisionengineers.co.uk", FixedUtcNow.AddMinutes(5), null, "graph_unavailable"), row));
    }

    [Fact]
    public async Task IntakeDispatchHealthCountsByStateAndNamesTheNewestCompletion()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            context.IntakeStagedReceipts.AddRange(
                Staged("pending", completedAtUtc: null),
                Staged("processing", completedAtUtc: null),
                Staged("retry_scheduled", completedAtUtc: null),
                Staged("failed", completedAtUtc: null),
                Staged("completed", FixedUtcNow.AddMinutes(-9)),
                Staged("completed", FixedUtcNow.AddMinutes(-2)));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IServiceHealthQueries>();

        var health = await queries.GetIntakeDispatchHealthAsync(CancellationToken.None);

        Assert.Equal(new IntakeDispatchHealth(2, 1, 1, FixedUtcNow.AddMinutes(-2)), health);
    }

    [Fact]
    public async Task IntakeDispatchHealthOnAnEmptyQueueHasNoEvidence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IServiceHealthQueries>();

        Assert.Equal(new IntakeDispatchHealth(0, 0, 0, null), await queries.GetIntakeDispatchHealthAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EvaFailuresAndActivityReadTheRecordedAttempts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            context.EvaSubmissions.AddRange(
                Submission(caseId, EvaSubmissionOutcome.Rejected, FixedUtcNow.AddHours(-2), "validation"),
                Submission(caseId, EvaSubmissionOutcome.Unknown, FixedUtcNow.AddDays(-3), "timeout"),
                Submission(caseId, EvaSubmissionOutcome.Succeeded, FixedUtcNow.AddHours(-1), null));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEvaSubmissionQueries>();

        var failures = await queries.GetRecentFailuresAsync(FixedUtcNow.AddDays(-1), 20, CancellationToken.None);
        var activity = await queries.GetActivityAsync(CancellationToken.None);

        var failure = Assert.Single(failures);
        Assert.Equal(new EvaSubmissionFailure(caseId, EvaSubmissionOutcome.Rejected, "validation", FixedUtcNow.AddHours(-2)), failure);
        Assert.Equal(new EvaSubmissionActivity(FixedUtcNow.AddHours(-1)), activity);
    }

    [Fact]
    public async Task EvaActivityWithoutAnyAttemptIsEmpty()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEvaSubmissionQueries>();

        Assert.Equal(new EvaSubmissionActivity(null), await queries.GetActivityAsync(CancellationToken.None));
        Assert.Empty(await queries.GetRecentFailuresAsync(FixedUtcNow.AddDays(-1), 20, CancellationToken.None));
    }

    /// <summary>
    /// The snapshot reads its ten sources in thirteen statements. Overlapping
    /// them changes how long the page waits, not how many statements it
    /// sends, so this pins the count the overlap must not change. The Health
    /// page adds the ingress switch (through the request's own context), the
    /// shell's reads and the metrics read below.
    /// </summary>
    [Fact]
    public async Task TheSnapshotReadsItsSourcesInThirteenStatements()
    {
        var counter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(counter));
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var health = new GetServiceHealth(
            services.GetRequiredService<IApprovedMailboxPollStatusQueries>(),
            services.GetRequiredService<IServiceHealthQueries>(),
            services.GetRequiredService<GetRequestOperations>(),
            services.GetRequiredService<IEvaSubmissionQueries>(),
            services.GetRequiredService<IAiJobQueries>(),
            services.GetRequiredService<ISendToAiControl>(),
            new IngressSwitch(),
            new EfAutomationActivityStore(
                services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()),
            TimeProvider.System);
        counter.Reset();

        var snapshot = await health.ExecuteAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            CancellationToken.None);

        // Mailbox polls 1, Sent-items polls 1, intake dispatch 2, external
        // work 1, EVA activity 1, EVA failures 1, AI counts 2, newest AI job
        // 1, Send to AI switch 1, Automation activity 2.
        Assert.Equal(13, counter.Count);
        Assert.Equal(
            new[]
            {
                ServiceHealthArea.Intake,
                ServiceHealthArea.Custody,
                ServiceHealthArea.Eva,
                ServiceHealthArea.Ai,
                ServiceHealthArea.Automation
            },
            snapshot.Rows.Select(row => row.Area));
    }

    /// <summary>The metrics read, which the page now runs beside the snapshot, is eight statements.</summary>
    [Fact]
    public async Task TheAdministrationMetricsReadIsEightStatements()
    {
        var counter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(counter));
        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAdministrationHealthMetricsQueries>();
        counter.Reset();

        await queries.GetAsync(FixedUtcNow, CancellationToken.None);

        Assert.Equal(8, counter.Count);
    }

    /// <summary>
    /// The Health page sends the same statements however many rows the
    /// sources hold, and reads the Sent-items cursors twice: the poll list in
    /// the snapshot and the failure count in the metrics. Both reads now run
    /// beside each other rather than one after another.
    /// </summary>
    [Fact]
    public async Task TheHealthPageSendsTheSameStatementsWhateverTheRowCount()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        // The first request pays one-off start-up reads; count the next.
        _ = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Health");

        counter.Reset();
        _ = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Health");
        var withNoRows = counter.Count;
        Assert.True(withNoRows > 0, "The interceptor observed no statements at all.");
        Assert.Equal(2, counter.CountContaining("[ApprovedSentPollStates]"));

        await using (var context = await factory.Database.CreateContextAsync())
        {
            for (var index = 0; index < 3; index++)
            {
                context.ApprovedSentPollStates.Add(new ApprovedSentPollStateEntity
                {
                    MailboxId = $"health-page-{index}",
                    MailboxAddress = $"health-page-{index}@collisionengineers.co.uk",
                    SentFolderIdentity = $"sent-{index}",
                    DueAtUtc = FixedUtcNow.AddMinutes(5),
                    LastCompletedAtUtc = FixedUtcNow.AddMinutes(-4),
                    LastFailureCode = index == 0 ? "graph_unavailable" : null
                });
            }

            await context.SaveChangesAsync();
        }

        counter.Reset();
        var page = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Health");
        Assert.Contains("health-page-1@collisionengineers.co.uk", page, StringComparison.Ordinal);
        Assert.Equal(withNoRows, counter.Count);
        Assert.Equal(2, counter.CountContaining("[ApprovedSentPollStates]"));
    }

    private sealed class IngressSwitch : IAutomationIngressStatusQueries
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private static IntakeStagedReceiptEntity Staged(string state, DateTimeOffset? completedAtUtc)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            SourceFileName = "instruction.eml",
            MediaType = "message/rfc822",
            SourceLength = 1,
            SourceHash = new string('1', 64),
            SourceChannel = "mailbox",
            ExternalReceiptToken = $"health:{id:N}",
            ReceivedAtUtc = FixedUtcNow.AddMinutes(-10),
            Actor = "worker",
            StorageKey = $"staged/{id:N}",
            StagedAtUtc = FixedUtcNow.AddMinutes(-10),
            WorkItem = new IntakeWorkItemEntity
            {
                Id = Guid.NewGuid(),
                StagedReceiptId = id,
                OperationKey = $"health-op:{id:N}",
                State = state,
                AttemptCount = state == "pending" ? 0 : 1,
                DueAtUtc = FixedUtcNow,
                CompletedAtUtc = completedAtUtc
            }
        };
    }

    private static EvaSubmissionEntity Submission(
        Guid caseId,
        EvaSubmissionOutcome outcome,
        DateTimeOffset submittedAtUtc,
        string? failureCode) =>
        new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            WorkflowVersion = 1,
            ExternalRef = "EVA31003",
            OperationKey = $"eva:{Guid.NewGuid():N}",
            Outcome = outcome.ToString(),
            IsDelivered = outcome is EvaSubmissionOutcome.Succeeded or EvaSubmissionOutcome.Partial,
            EvaId = outcome == EvaSubmissionOutcome.Succeeded ? "eva-1" : null,
            FileReference = outcome == EvaSubmissionOutcome.Succeeded ? "FR-1" : null,
            FailureCode = failureCode,
            ImagesSent = outcome == EvaSubmissionOutcome.Succeeded ? 3 : 0,
            AttemptCount = 1,
            ActorSubjectId = Guid.NewGuid().ToString("D"),
            SubmittedAtUtc = submittedAtUtc
        };

    private static ExternalWorkItemEntity Work(Guid caseId, string kind, string state) =>
        new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Kind = kind,
            OperationKey = $"work:{Guid.NewGuid():N}",
            State = state,
            AttemptCount = state == "completed" ? 1 : 0,
            DueAtUtc = FixedUtcNow
        };

    private static async Task<Guid> SeedCaseAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = "EVA test", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = FixedUtcNow },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = "EVA",
                IsActive = true,
                ReportGenerationPolicy = "EvaManualApi",
                Version = 0
            },
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "eva-origin.pdf",
                MediaType = "application/pdf",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"eva:{receiptId:N}",
                ReceivedAtUtc = FixedUtcNow,
                ProcessedAtUtc = FixedUtcNow,
                SourceReaderKey = "eva-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "EVA test",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = principalId,
                SequenceLineageId = lineageId,
                Year = 2031,
                Sequence = 3,
                Reference = "EVA31003",
                Type = "inspection",
                InitialState = "Review",
                CustodyState = "Confirmed",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = FixedUtcNow,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "Review",
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
        return caseId;
    }
}
