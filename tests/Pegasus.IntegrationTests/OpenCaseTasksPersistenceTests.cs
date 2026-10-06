using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Work Centre's Open tasks read against SQL Server (FRD-15, operator,
/// 6 October 2026): every open task with its Case and the creation its
/// <c>case_task_created</c> history records, written by the real task store;
/// completed and cancelled tasks are not read; and the Work Centre's one row
/// per Case returns past a dismissal when a newer task is opened.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class OpenCaseTasksPersistenceTests
{
    private static readonly DateTimeOffset StartUtc = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OpenTasksAreReadWithTheirCreationAndTheWorkCentreListsOneRowPerCase()
    {
        var time = new MutableTimeProvider(StartUtc);
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: services => services.AddSingleton<TimeProvider>(time));
        var creatorId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var (twoTasksCaseId, oneTaskCaseId) = await SeedAsync(database, creatorId, assigneeId);
        var creator = ActionActor.Staff(creatorId, [StaffRole.User]);
        var reader = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var tasks = new TaskWriter(services, creator);

        // The Case with two open tasks: the older one assigned, the newer one
        // not; a completed and a cancelled task beside them are not read.
        var assigned = await tasks.CreateAsync(twoTasksCaseId, 1, "Chase the payment", assigneeId, "assigned");
        time.Advance(TimeSpan.FromHours(1));
        var unassigned = await tasks.CreateAsync(twoTasksCaseId, assigned.CaseVersion, "Send the invoice", null, "unassigned");
        time.Advance(TimeSpan.FromHours(1));
        var toComplete = await tasks.CreateAsync(twoTasksCaseId, unassigned.CaseVersion, "Book the inspection", null, "to-complete");
        var completed = await tasks.CompleteAsync(toComplete, "complete");
        var toCancel = await tasks.CreateAsync(twoTasksCaseId, completed.CaseVersion, "Request the estimate", null, "to-cancel");
        var cancelled = await tasks.CancelAsync(toCancel, "cancel");
        var only = await tasks.CreateAsync(oneTaskCaseId, 1, "Call the claimant", null, "only");

        var dashboard = services.GetRequiredService<IDashboardQueries>();
        var open = await dashboard.ListOpenCaseTasksAsync(CancellationToken.None);

        Assert.Equal(
            new HashSet<Guid> { assigned.Id, unassigned.Id, only.Id },
            open.Select(task => task.TaskId).ToHashSet());
        var first = Assert.Single(open, task => task.TaskId == assigned.Id);
        Assert.Equal(twoTasksCaseId, first.CaseId);
        Assert.Equal("OPEN31001", first.Reference);
        Assert.Equal(QdosPrincipal.Code, first.Principal);
        Assert.Equal("Chase the payment", first.Description);
        Assert.Equal(assigneeId, first.AssigneeId);
        Assert.Equal(StartUtc, first.CreatedAtUtc);
        Assert.Equal(creatorId, first.CreatedByStaffId);
        Assert.Null(Assert.Single(open, task => task.TaskId == unassigned.Id).AssigneeId);

        var rows = await OpenTasksRowsAsync(services, reader);
        Assert.Equal(2, rows.Count);
        var twoTasks = Assert.Single(rows, row => row.Id == twoTasksCaseId);
        Assert.Equal("Chase the payment", twoTasks.Reason);
        Assert.Equal(1, twoTasks.MoreCount);
        Assert.Equal(assigneeId, twoTasks.OwnerStaffId);
        Assert.Null(twoTasks.Due);
        Assert.Equal(NeedsAttentionPriority.Normal, twoTasks.Priority);
        Assert.Equal($"/Cases/{twoTasksCaseId:D}?section=tasks", twoTasks.Route);
        // Without an assignee, the row belongs to the staff member who created the task.
        Assert.Equal(creatorId, Assert.Single(rows, row => row.Id == oneTaskCaseId).OwnerStaffId);

        // Dismissed, the Case's row stays hidden until a task is opened after it.
        time.Advance(TimeSpan.FromMinutes(5));
        await services.GetRequiredService<IWorkCentreDismissalStore>().DismissAsync(
            twoTasksCaseId, time.GetUtcNow(), creator, CancellationToken.None);
        Assert.DoesNotContain(await OpenTasksRowsAsync(services, reader), row => row.Id == twoTasksCaseId);

        time.Advance(TimeSpan.FromMinutes(5));
        await tasks.CreateAsync(twoTasksCaseId, cancelled.CaseVersion, "Chase the repairer", null, "after-dismissal");
        var returned = Assert.Single(await OpenTasksRowsAsync(services, reader), row => row.Id == twoTasksCaseId);
        Assert.Equal("Chase the payment", returned.Reason);
        Assert.Equal(2, returned.MoreCount);
    }

    private static async Task<IReadOnlyList<NeedsAttentionItem>> OpenTasksRowsAsync(
        IServiceProvider services,
        ActionActor reader)
    {
        var snapshot = await services.GetRequiredService<IGetOperationsSnapshot>().ExecuteAsync(
            new NeedsAttentionQuery(reader, Kinds: [NeedsAttentionKind.OpenTasks]),
            CancellationToken.None);
        return snapshot.Attention.Items;
    }

    private static async Task<(Guid TwoTasksCaseId, Guid OneTaskCaseId)> SeedAsync(
        LocalDbTestDatabase database,
        Guid creatorId,
        Guid assigneeId)
    {
        await using var seedScope = database.CreateAsyncScope();
        var principal = await SeededPrincipals.QdosAsync(seedScope.ServiceProvider);
        await using var context = await database.CreateContextAsync();
        context.Users.AddRange(Staff(creatorId, "creator"), Staff(assigneeId, "assignee"));
        var twoTasksCaseId = AddCase(context, principal, "OPEN31001", 1);
        var oneTaskCaseId = AddCase(context, principal, "OPEN31002", 2);
        await context.SaveChangesAsync();
        await CaseWorkFixture.InsertPrimaryWorksAsync(context);
        return (twoTasksCaseId, oneTaskCaseId);
    }

    private static Guid AddCase(
        PegasusDbContext context,
        SeededPrincipalTestData principal,
        string reference,
        int sequence)
    {
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        context.AddRange(
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = $"{reference}.pdf",
                MediaType = "application/pdf",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"open-tasks:{receiptId:N}",
                ReceivedAtUtc = StartUtc,
                ProcessedAtUtc = StartUtc,
                SourceReaderKey = "open-tasks-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "Open tasks test fixture.",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = principal.Id,
                SequenceLineageId = principal.SequenceLineageId,
                Year = 2031,
                Sequence = sequence,
                Reference = reference,
                Type = "inspection",
                InitialState = "NotReady",
                CustodyState = "pending",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = StartUtc,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = nameof(CaseLifecycleState.Review),
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
        return caseId;
    }

    private static PegasusIdentityUser Staff(Guid id, string suffix) => new()
    {
        Id = id,
        UserName = $"{suffix}@example.test",
        NormalizedUserName = $"{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
        Email = $"{suffix}@example.test",
        NormalizedEmail = $"{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
        EmailConfirmed = true,
        IsEnabled = true,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N")
    };

    /// <summary>Each change through the real use cases, under a lease claimed at the Case's version.</summary>
    private sealed class TaskWriter(IServiceProvider services, ActionActor actor)
    {
        private const string Reason = "The task is required for case progression";

        public async Task<CaseTaskRecord> CreateAsync(
            Guid caseId,
            long caseVersion,
            string description,
            Guid? assigneeId,
            string key)
        {
            var lease = await ClaimAsync(caseId, caseVersion, key);
            return await services.GetRequiredService<ICreateCaseTask>().ExecuteAsync(
                new(caseId, Guid.NewGuid(), caseVersion, actor, $"create-{key}", Reason, lease.Token, description, assigneeId),
                CancellationToken.None);
        }

        public async Task<CaseTaskRecord> CompleteAsync(CaseTaskRecord task, string key)
        {
            var lease = await ClaimAsync(task.CaseId, task.CaseVersion, key);
            return await services.GetRequiredService<ICompleteCaseTask>().ExecuteAsync(
                new(task.CaseId, task.Id, task.CaseVersion, task.Version, actor, $"complete-{key}", Reason, lease.Token),
                CancellationToken.None);
        }

        public async Task<CaseTaskRecord> CancelAsync(CaseTaskRecord task, string key)
        {
            var lease = await ClaimAsync(task.CaseId, task.CaseVersion, key);
            return await services.GetRequiredService<ICancelCaseTask>().ExecuteAsync(
                new(task.CaseId, task.Id, task.CaseVersion, task.Version, actor, $"cancel-{key}", Reason, lease.Token),
                CancellationToken.None);
        }

        private Task<CaseEditLease> ClaimAsync(Guid caseId, long caseVersion, string key) =>
            services.GetRequiredService<IAcquireCaseEditLease>().ExecuteAsync(
                new(caseId, caseVersion, actor, $"claim-{key}"),
                CancellationToken.None);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan interval) => utcNow = utcNow.Add(interval);
    }
}
