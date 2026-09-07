using System.Collections.Immutable;
using Pegasus.Core.AiWork;
using Pegasus.Core.Custody;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

public sealed class ServiceHealthTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private const string Mailbox = "instructions@collisionengineers.co.uk";

    [Theory]
    [InlineData(ServiceHealthState.Partial, false, true)]
    [InlineData(ServiceHealthState.Partial, true, true)]
    [InlineData(ServiceHealthState.Failed, false, true)]
    [InlineData(ServiceHealthState.Running, false, false)]
    [InlineData(ServiceHealthState.Configured, true, false)]
    [InlineData(ServiceHealthState.ReviewRequired, false, false)]
    public void PartialDataNoticeDependsOnlyOnPartialOrFailedRows(
        ServiceHealthState state,
        bool externalWorkLimitReached,
        bool expected)
    {
        var snapshot = new ServiceHealthSnapshot(
            FixedUtcNow,
            [new(ServiceHealthArea.Mail, Mailbox, state, null, ServiceHealthDependency.MicrosoftGraph)],
            externalWorkLimitReached);

        Assert.Equal(expected, ServiceHealthPolicy.HasPartialData(snapshot));
    }

    [Theory]
    [InlineData(null, null, ServiceHealthState.Configured)]
    [InlineData(-5, null, ServiceHealthState.Current)]
    [InlineData(-15, null, ServiceHealthState.Current)]
    [InlineData(-16, null, ServiceHealthState.Partial)]
    [InlineData(-1, "graph_unavailable", ServiceHealthState.Failed)]
    [InlineData(null, "graph_unavailable", ServiceHealthState.Failed)]
    public void PollStateFollowsTheRecordedCursor(
        int? completedMinutesAgo,
        string? failureCode,
        ServiceHealthState expected)
    {
        DateTimeOffset? completed = completedMinutesAgo is { } minutes
            ? FixedUtcNow.AddMinutes(minutes)
            : null;

        Assert.Equal(expected, ServiceHealthPolicy.PollState(completed, failureCode, FixedUtcNow));
    }

    [Theory]
    [InlineData(0, 0, 0, false, ServiceHealthState.Configured)]
    [InlineData(0, 0, 0, true, ServiceHealthState.Current)]
    [InlineData(3, 0, 0, false, ServiceHealthState.Running)]
    [InlineData(3, 1, 0, true, ServiceHealthState.Partial)]
    [InlineData(3, 1, 1, true, ServiceHealthState.Failed)]
    public void DispatchStateRanksFailureOverBackoffOverActivityAndNeedsEvidenceToBeCurrent(
        int active,
        int retryScheduled,
        int failed,
        bool hasCompleted,
        ServiceHealthState expected)
    {
        Assert.Equal(
            expected,
            ServiceHealthPolicy.DispatchState(
                new(active, retryScheduled, failed, hasCompleted ? FixedUtcNow : null)));
    }

    [Fact]
    public void EvaStateAsksForAPersonOnARecentFailureAndReportsPendingWork()
    {
        var failure = new EvaSubmissionFailure(
            Guid.NewGuid(),
            EvaSubmissionOutcome.Rejected,
            "validation",
            FixedUtcNow.AddHours(-1));

        Assert.Equal(
            ServiceHealthState.ReviewRequired,
            ServiceHealthPolicy.EvaState(new(0, FixedUtcNow.AddHours(-1)), [failure]));
        Assert.Equal(
            ServiceHealthState.Running,
            ServiceHealthPolicy.EvaState(new(2, FixedUtcNow.AddDays(-2)), []));
        Assert.Equal(
            ServiceHealthState.Configured,
            ServiceHealthPolicy.EvaState(new(0, null), []));
        Assert.Equal(
            ServiceHealthState.Current,
            ServiceHealthPolicy.EvaState(new(0, FixedUtcNow.AddDays(-2)), []));
    }

    [Fact]
    public void AiStateTreatsTheSwitchAsConfigurationAndAFailedJobAsReview()
    {
        Assert.Equal(
            ServiceHealthState.Configured,
            ServiceHealthPolicy.AiState(false, new(3, 1), FixedUtcNow));
        Assert.Equal(
            ServiceHealthState.ReviewRequired,
            ServiceHealthPolicy.AiState(true, new(3, 1), FixedUtcNow));
        Assert.Equal(
            ServiceHealthState.Running,
            ServiceHealthPolicy.AiState(true, new(3, 0), FixedUtcNow));
        Assert.Equal(
            ServiceHealthState.Configured,
            ServiceHealthPolicy.AiState(true, new(0, 0), null));
        Assert.Equal(
            ServiceHealthState.Current,
            ServiceHealthPolicy.AiState(true, new(0, 0), FixedUtcNow));
    }

    [Fact]
    public void ExternalWorkRowsCarryTheRetryIdentityOfEachRetryableFailure()
    {
        var failedId = Guid.NewGuid();
        var rows = ServiceHealthPolicy.ExternalWorkRows(
        [
            ExternalWork(Guid.NewGuid(), ExternalWorkKinds.CreateCaseCustody, RequestOperationState.Completed, 1, false, FixedUtcNow.AddMinutes(-30)),
            ExternalWork(failedId, ExternalWorkKinds.CreateCaseCustody, RequestOperationState.Failed, 3, true, FixedUtcNow.AddMinutes(-10)),
            UploadLink(Guid.NewGuid())
        ]);

        var row = Assert.Single(rows);
        Assert.Equal(ServiceHealthArea.Custody, row.Area);
        Assert.Equal(ExternalWorkKinds.CreateCaseCustody, row.Service);
        Assert.Equal(ServiceHealthState.Failed, row.State);
        Assert.Equal(FixedUtcNow.AddMinutes(-10), row.LatestEvidenceAtUtc);
        Assert.Equal(ServiceHealthDependency.Box, row.Dependency);
        Assert.Equal(new ServiceHealthRetryTarget(failedId, 3), row.RetryTarget);
    }

    [Fact]
    public void ExternalWorkWithoutFailuresIsOneRowNamingTheNewestEvidence()
    {
        var rows = ServiceHealthPolicy.ExternalWorkRows(
        [
            ExternalWork(Guid.NewGuid(), ExternalWorkKinds.VehicleLookup, RequestOperationState.Completed, 1, false, FixedUtcNow.AddMinutes(-30)),
            ExternalWork(Guid.NewGuid(), ExternalWorkKinds.SubmitCaseToEva, RequestOperationState.Pending, 0, false, FixedUtcNow.AddMinutes(-2))
        ]);

        var row = Assert.Single(rows);
        Assert.Equal(ServiceHealthPolicy.ExternalWorkService, row.Service);
        Assert.Equal(ServiceHealthState.Running, row.State);
        Assert.Equal(FixedUtcNow.AddMinutes(-2), row.LatestEvidenceAtUtc);
        Assert.Null(row.RetryTarget);

        var empty = Assert.Single(ServiceHealthPolicy.ExternalWorkRows([UploadLink(Guid.NewGuid())]));
        Assert.Equal(ServiceHealthState.Configured, empty.State);
        Assert.Null(empty.LatestEvidenceAtUtc);
    }

    [Fact]
    public async Task MailboxFailureRetainsItsCodeAndTheSeparatePriorSuccessTime()
    {
        var lastSuccess = FixedUtcNow.AddHours(-1);
        var sources = new Sources
        {
            MailboxPolls = [new(Guid.NewGuid(), Mailbox, FixedUtcNow, lastSuccess, "graph_unavailable")],
            SentPolls = [new(Mailbox, FixedUtcNow, lastSuccess, "graph_throttled")]
        };

        var snapshot = await Build(sources).ExecuteAsync(StaffActor(), CancellationToken.None);
        var rows = snapshot.Rows.Where(row => row.Area == ServiceHealthArea.Mail).ToArray();

        Assert.Equal(2, rows.Length);
        Assert.All(rows, row =>
        {
            Assert.Equal(ServiceHealthState.Failed, row.State);
            Assert.Equal(lastSuccess, row.LatestEvidenceAtUtc);
        });
        Assert.Equal("graph_unavailable", rows[0].FailureCode);
        Assert.Equal("graph_throttled", rows[1].FailureCode);
    }

    [Fact]
    public async Task SnapshotComposesOneRowPerSourceAndNamesEachEvidenceTime()
    {
        var mailboxId = Guid.NewGuid();
        var jobTime = FixedUtcNow.AddMinutes(-3);
        var sources = new Sources
        {
            MailboxPolls = [new(mailboxId, Mailbox, FixedUtcNow.AddMinutes(5), FixedUtcNow.AddMinutes(-4), null)],
            SentPolls = [new(Mailbox, FixedUtcNow.AddMinutes(5), FixedUtcNow.AddMinutes(-20), null)],
            Dispatch = new(1, 0, 0, FixedUtcNow.AddMinutes(-6)),
            Operations = [],
            LimitReached = true,
            EvaActivity = new(0, FixedUtcNow.AddHours(-3)),
            AiCounts = new(0, 0),
            RecentJobs = [Job(jobTime, closedAtUtc: null)],
            SendToAiEnabled = true,
            IngressEnabled = false,
            LatestAutomationActivityAtUtc = FixedUtcNow.AddMinutes(-40)
        };
        var useCase = Build(sources);

        var snapshot = await useCase.ExecuteAsync(StaffActor(), CancellationToken.None);

        Assert.Equal(FixedUtcNow, snapshot.AsOfUtc);
        Assert.True(snapshot.ExternalWorkLimitReached);
        Assert.Collection(
            snapshot.Rows,
            row =>
            {
                Assert.Equal((ServiceHealthArea.Mail, Mailbox, ServiceHealthState.Current), (row.Area, row.Service, row.State));
                Assert.Equal(FixedUtcNow.AddMinutes(-4), row.LatestEvidenceAtUtc);
                Assert.Equal(ServiceHealthDependency.MicrosoftGraph, row.Dependency);
            },
            row =>
            {
                Assert.Equal(ServiceHealthArea.Mail, row.Area);
                Assert.Contains(Mailbox, row.Service, StringComparison.Ordinal);
                Assert.Equal(ServiceHealthState.Partial, row.State);
                Assert.Equal(FixedUtcNow.AddMinutes(-20), row.LatestEvidenceAtUtc);
            },
            row =>
            {
                Assert.Equal((ServiceHealthArea.Intake, ServiceHealthState.Running), (row.Area, row.State));
                Assert.Equal(FixedUtcNow.AddMinutes(-6), row.LatestEvidenceAtUtc);
                Assert.Equal(ServiceHealthDependency.Worker, row.Dependency);
            },
            row => Assert.Equal((ServiceHealthArea.Custody, ServiceHealthState.Configured), (row.Area, row.State)),
            row =>
            {
                Assert.Equal((ServiceHealthArea.Eva, ServiceHealthState.Current), (row.Area, row.State));
                Assert.Equal(FixedUtcNow.AddHours(-3), row.LatestEvidenceAtUtc);
                Assert.Equal(ServiceHealthDependency.EvaApi, row.Dependency);
            },
            row =>
            {
                Assert.Equal((ServiceHealthArea.Ai, ServiceHealthState.Current), (row.Area, row.State));
                Assert.Equal(jobTime, row.LatestEvidenceAtUtc);
                Assert.Equal(ServiceHealthDependency.AiConnector, row.Dependency);
            },
            row =>
            {
                Assert.Equal((ServiceHealthArea.Automation, ServiceHealthState.Configured), (row.Area, row.State));
                Assert.Equal(FixedUtcNow.AddMinutes(-40), row.LatestEvidenceAtUtc);
                Assert.Equal(ServiceHealthDependency.AutomationClient, row.Dependency);
            });
        Assert.Equal(FixedUtcNow - ServiceHealthPolicy.EvaRecentFailureWindow, sources.EvaFailuresSinceUtc);
    }

    [Fact]
    public async Task SnapshotUsesTheClosedTimeOfTheNewestAiJobWhenItIsLater()
    {
        var created = FixedUtcNow.AddMinutes(-30);
        var closed = FixedUtcNow.AddMinutes(-1);
        var useCase = Build(new Sources { RecentJobs = [Job(created, closed)] });

        var snapshot = await useCase.ExecuteAsync(StaffActor(), CancellationToken.None);

        var ai = Assert.Single(snapshot.Rows, row => row.Area == ServiceHealthArea.Ai);
        Assert.Equal(closed, ai.LatestEvidenceAtUtc);
    }

    [Fact]
    public async Task SnapshotRejectsASystemWorkerBeforeReadingAnything()
    {
        var sources = new Sources();
        var useCase = Build(sources);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            useCase.ExecuteAsync(ActionActor.SystemWorker("worker"), CancellationToken.None));

        Assert.False(sources.Read);
    }

    private static GetServiceHealth Build(Sources sources) =>
        new(
            sources,
            sources,
            new GetRequestOperations(sources, new FixedTimeProvider(FixedUtcNow)),
            sources,
            sources,
            sources,
            sources,
            sources,
            new FixedTimeProvider(FixedUtcNow));

    private static ActionActor StaffActor() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    private static AiJobRecord Job(DateTimeOffset createdAtUtc, DateTimeOffset? closedAtUtc) =>
        new(
            Guid.NewGuid(),
            AiJobKind.Estimate,
            AiJobSubjectKind.Case,
            Guid.NewGuid(),
            "EVA31003",
            "Draft an estimate",
            80,
            1000m,
            closedAtUtc is null ? AiJobState.Queued : AiJobState.Completed,
            ActorKind.Staff,
            Guid.NewGuid().ToString("D"),
            createdAtUtc,
            createdAtUtc.AddHours(4),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            closedAtUtc,
            null,
            1);

    private static RequestOperationProjection ExternalWork(
        Guid id,
        string kind,
        RequestOperationState state,
        int attemptCount,
        bool canRetry,
        DateTimeOffset lastActivityAtUtc) =>
        new(
            id,
            RequestOperationKind.ExternalWork,
            state,
            Guid.NewGuid(),
            "EVA31003",
            "EVA",
            lastActivityAtUtc,
            ExpiresAtUtc: null,
            Version: null,
            AcceptedFileCount: null,
            AcceptedByteCount: null,
            MaximumFileCount: null,
            MaximumByteCount: null,
            LimitsVersion: null,
            kind,
            attemptCount,
            state == RequestOperationState.Failed ? "box_unavailable" : null,
            null,
            canRetry,
            CanRevoke: false,
            CaseVersion: 1,
            RequestCaseEditLeaseState.Available,
            CaseEditLeaseExpiresAtUtc: null);

    private static RequestOperationProjection UploadLink(Guid id) =>
        new(
            id,
            RequestOperationKind.PegasusUploadLink,
            RequestOperationState.Active,
            Guid.NewGuid(),
            "EVA31003",
            "EVA",
            FixedUtcNow.AddMinutes(-1),
            FixedUtcNow.AddDays(1),
            Version: 1,
            AcceptedFileCount: 0,
            AcceptedByteCount: 0,
            MaximumFileCount: 10,
            MaximumByteCount: 25_000_000,
            LimitsVersion: "1",
            ExternalKind: null,
            AttemptCount: null,
            FailureCode: null,
            FailureReason: null,
            CanRetry: false,
            CanRevoke: true,
            CaseVersion: 1,
            RequestCaseEditLeaseState.Available,
            CaseEditLeaseExpiresAtUtc: null);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    /// <summary>
    /// Every source the snapshot reads, in one fake, so a test states the
    /// whole estate it is describing in one place.
    /// </summary>
    private sealed class Sources :
        IApprovedMailboxPollStatusQueries,
        IServiceHealthQueries,
        IRequestOperationsProjectionStore,
        IEvaSubmissionQueries,
        IAiJobQueries,
        ISendToAiControl,
        IAutomationIngressStatusQueries,
        IAutomationActivityQueries
    {
        public IReadOnlyList<ApprovedMailboxPollStatus> MailboxPolls { get; init; } = [];
        public IReadOnlyList<SentEvidencePollStatus> SentPolls { get; init; } = [];
        public IntakeDispatchHealth Dispatch { get; init; } = new(0, 0, 0, null);
        public IReadOnlyList<RequestOperationProjection> Operations { get; init; } = [];
        public EvaSubmissionActivity EvaActivity { get; init; } = new(0, null);
        public IReadOnlyList<EvaSubmissionFailure> EvaFailures { get; init; } = [];
        public AiJobCounts AiCounts { get; init; } = new(0, 0);
        public IReadOnlyList<AiJobRecord> RecentJobs { get; init; } = [];
        public bool SendToAiEnabled { get; init; } = true;
        public bool IngressEnabled { get; init; } = true;
        public DateTimeOffset? LatestAutomationActivityAtUtc { get; init; }

        public bool Read { get; private set; }
        public DateTimeOffset? EvaFailuresSinceUtc { get; private set; }

        Task<IReadOnlyList<ApprovedMailboxPollStatus>> IApprovedMailboxPollStatusQueries.ListAsync(
            CancellationToken cancellationToken)
        {
            Read = true;
            return Task.FromResult(MailboxPolls);
        }

        public Task<IReadOnlyList<SentEvidencePollStatus>> ListSentEvidencePollStatusAsync(
            CancellationToken cancellationToken) => Task.FromResult(SentPolls);

        public Task<IntakeDispatchHealth> GetIntakeDispatchHealthAsync(
            CancellationToken cancellationToken) => Task.FromResult(Dispatch);

        public Task<RequestOperationsProjection> GetAsync(
            int maximumItems,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RequestOperationsProjection(Operations.ToImmutableArray(), LimitReached));

        public bool LimitReached { get; init; }

        public Task<EvaSubmissionRecord?> GetLatestAsync(
            Guid caseId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by the snapshot.");

        public Task<IReadOnlyList<EvaSubmissionFailure>> GetRecentFailuresAsync(
            DateTimeOffset sinceUtc,
            int maximumResults,
            CancellationToken cancellationToken = default)
        {
            EvaFailuresSinceUtc = sinceUtc;
            return Task.FromResult(EvaFailures);
        }

        public Task<EvaSubmissionActivity> GetActivityAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(EvaActivity);

        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the snapshot.");

        public Task<AiJobQueryPage> ListOpenPageAsync(AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the snapshot.");

        public Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(
            Guid subjectId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the snapshot.");

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken)
        {
            Assert.Equal(1, max);
            return Task.FromResult(RecentJobs);
        }

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(AiCounts);

        Task<bool> ISendToAiControl.IsEnabledAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SendToAiEnabled);

        Task<bool> IAutomationIngressStatusQueries.IsEnabledAsync(CancellationToken cancellationToken) =>
            Task.FromResult(IngressEnabled);

        public Task<bool> SetEnabledAsync(
            bool enabled,
            ActionActor actor,
            string reason,
            string operationKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the snapshot.");

        public Task<ListAutomationActivityResult> ListAsync(
            ListAutomationActivityRequest request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(1, request.PageSize);
            IReadOnlyList<AutomationActivityRecord> records = LatestAutomationActivityAtUtc is { } at
                ?
                [
                    new(
                        Guid.NewGuid(),
                        AutomationActivityRecordType.ActionHistory,
                        "ai_job_taken",
                        "automation",
                        at,
                        "succeeded",
                        "corr-1",
                        null,
                        null,
                        null)
                ]
                : [];
            return Task.FromResult(new ListAutomationActivityResult(records, null, 1, 1, false, false));
        }
    }
}
