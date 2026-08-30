using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// ENG-026: the one totals owner, the operation mapping, the estimate
/// policy, and the actor rules of the named-estimate use cases.
/// </summary>
public sealed class EstimateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor User = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly ActionActor Client = ActionActor.Automation("pegasus-automation");
    private static readonly string Lease = new('l', CaseEditAuthority.LeaseTokenLength);

    [Fact]
    public void TotalsFollowTheFrdFormulaWithVatRoundedToPence()
    {
        var estimate = Estimate(
            new("Repairer estimate", 3, 40m, 30m, 25m, 10m, 17.5m, null),
            Line("new_part", price: 100m, quantity: 2),
            Line("repair", workUnits: 2.5m),
            Line("paint_repair", paintWorkUnits: 1.5m),
            Line("new_part", price: 33.33m));

        var totals = EstimateTotals.Compute(estimate);

        Assert.Equal(233.33m, totals.Parts);
        Assert.Equal(100m, totals.Labour);
        Assert.Equal(70m, totals.Paint);
        Assert.Equal(10m, totals.Other);
        Assert.Equal(413.33m, totals.Subtotal);
        Assert.Equal(72.33m, totals.Vat);
        Assert.Equal(485.66m, totals.Total);
    }

    [Fact]
    public void TotalsWithoutRatesOrVatAreThePartsAlone()
    {
        var estimate = Estimate(
            new("Parts only", null, null, null, null, null, 0m, null),
            Line("new_part", price: 50m),
            Line("repair", workUnits: 4m));

        var totals = EstimateTotals.Compute(estimate);

        Assert.Equal(50m, totals.Subtotal);
        Assert.Equal(0m, totals.Vat);
        Assert.Equal(50m, totals.Total);
    }

    [Fact]
    public void EveryLineTypeMapsToExactlyOneOperationAndBack()
    {
        foreach (var type in EstimateLineCodes.Types)
        {
            var operation = EstimateOperations.FromLineType(type);
            Assert.Equal(operation, EstimateOperations.FromLineType(EstimateOperations.ToLineType(operation)));
        }
        Assert.Equal("new_part", EstimateOperations.ToLineType(EstimateOperation.Replace));
        Assert.Equal("rnr", EstimateOperations.ToLineType(EstimateOperation.RemoveAndRefit));
        Assert.Equal(EstimateOperation.Paint, EstimateOperations.FromLineType("paint_blend"));
        Assert.Equal(EstimateOperation.Other, EstimateOperations.FromLineType("check_labour"));
        Assert.True(EstimateOperations.TryParse("R&I", out var parsed));
        Assert.Equal(EstimateOperation.RemoveAndRefit, parsed);
        Assert.False(EstimateOperations.TryParse("Weld", out _));
        Assert.Throws<InvalidOperationException>(() => EstimateOperations.FromLineType("weld"));
    }

    [Theory]
    [InlineData("", 20, 40)]
    [InlineData("Estimate", 120, 40)]
    [InlineData("Estimate", 20.005, 40)]
    [InlineData("Estimate", 20, -1)]
    [InlineData("Estimate", 20, 40.123)]
    public void DetailsOutsideTheirBoundsAreRefused(string name, double vat, double rate) =>
        Assert.Throws<ArgumentException>(() => EstimatePolicy.ValidateDetails(
            new(name, null, (decimal)rate, null, null, null, (decimal)vat, null)));

    [Fact]
    public void DetailsAreTrimmedAndNotesBlankedToNull()
    {
        var details = EstimatePolicy.ValidateDetails(new("  Glass's  ", 2, 40m, null, null, null, 20m, "   "));
        Assert.Equal("Glass's", details.Name);
        Assert.Null(details.Notes);
    }

    [Fact]
    public void AStaffUserWhoIsNotAnEngineerCannotSaveAnEstimate() =>
        Assert.Throws<InvalidOperationException>(() =>
            EstimatePolicy.ValidateSave(SaveRequest(User, RepairSpecificationSourceRoute.Manual)));

    [Fact]
    public void TheAutomationActorMayOnlySaveAiDraftsThatCiteAJob()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EstimatePolicy.ValidateSave(SaveRequest(Client, RepairSpecificationSourceRoute.Manual, jobId: Guid.NewGuid())));
        Assert.Throws<InvalidOperationException>(() =>
            EstimatePolicy.ValidateSave(SaveRequest(Client, RepairSpecificationSourceRoute.AiDraft, jobId: null)));
        var validated = EstimatePolicy.ValidateSave(
            SaveRequest(Client, RepairSpecificationSourceRoute.AiDraft, jobId: Guid.NewGuid()));
        Assert.Equal(RepairSpecificationSourceRoute.AiDraft, validated.Source.Route);
    }

    [Fact]
    public async Task SaveRefusesAJobThatIsNotAnEstimateJobOnThisCaseHeldByTheClient()
    {
        var jobs = new FakeJobStore();
        var store = new FakeSpecificationStore();
        var save = new SaveEstimate(store, jobs, new FixedClock());

        var wrongKind = jobs.Add(Job(AiJobKind.UnidentifiedResolution, CaseId, AiJobState.Taken, Client.SubjectId));
        var wrongCase = jobs.Add(Job(AiJobKind.Estimate, Guid.NewGuid(), AiJobState.Taken, Client.SubjectId));
        var otherClient = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Taken, "other-client"));
        var lapsed = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Taken, Client.SubjectId, Now - TimeSpan.FromMinutes(1)));
        var held = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Taken, Client.SubjectId));

        foreach (var refused in new[] { Guid.NewGuid(), wrongKind, wrongCase, otherClient, lapsed })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                save.ExecuteAsync(SaveRequest(Client, RepairSpecificationSourceRoute.AiDraft, refused), CancellationToken.None));
        }
        Assert.Empty(store.Saved);

        var saved = await save.ExecuteAsync(
            SaveRequest(Client, RepairSpecificationSourceRoute.AiDraft, held), CancellationToken.None);
        Assert.Equal(held, saved.AiJobId);
        Assert.Single(store.Saved);
    }

    [Fact]
    public async Task AnEngineerEditingAnAiDraftKeepsItsJobWhicheverStateTheJobIsIn()
    {
        var jobs = new FakeJobStore();
        var completed = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Completed, Client.SubjectId));
        var save = new SaveEstimate(new FakeSpecificationStore(), jobs, new FixedClock());

        var saved = await save.ExecuteAsync(
            SaveRequest(Engineer, RepairSpecificationSourceRoute.AiDraft, completed), CancellationToken.None);

        Assert.Equal(completed, saved.AiJobId);
    }

    [Fact]
    public void OnlyADraftIsEditableAndTheAutomationOnlyEditsAiDrafts()
    {
        var accepted = Estimate(Details(), Line("repair", workUnits: 1m)) with { State = RepairSpecificationState.Accepted };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateEditable(accepted, Engineer));
        var manualDraft = Estimate(Details(), Line("repair", workUnits: 1m));
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateEditable(manualDraft, Client));
        EstimatePolicy.ValidateEditable(manualDraft, Engineer);
    }

    [Fact]
    public void AnAcceptedOrCurrentEstimateCannotBeDiscardedAndDiscardedCannotBeDuplicated()
    {
        var accepted = Estimate(Details(), Line("repair", workUnits: 1m)) with { State = RepairSpecificationState.Accepted };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateDiscard(accepted));
        var discarded = Estimate(Details(), Line("repair", workUnits: 1m)) with { State = RepairSpecificationState.Discarded };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateDiscard(discarded));
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateDuplicate(discarded));
        EstimatePolicy.ValidateDiscard(Estimate(Details(), Line("repair", workUnits: 1m)));
    }

    [Fact]
    public void MakingCurrentIsTheEngineersAcceptanceWithTheTotalsOwnersBasis()
    {
        var draft = Estimate(new("Draft", 2, 40m, 30m, 25m, 0m, 20m, null),
            Line("new_part", price: 100m), Line("repair", workUnits: 2m));
        EstimatePolicy.ValidateSetCurrent(draft, Engineer);
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateSetCurrent(draft, User));

        var basis = EstimatePolicy.BasisFor(draft);
        var totals = EstimateTotals.Compute(draft);
        Assert.Equal(totals.Total, basis.Total);
        Assert.Equal(totals.Vat, basis.Vat);
        Assert.Equal("repair-specification/v2", basis.PolicyVersion);
        Assert.Equal(basis, RepairSpecificationPolicy.ValidateCalculationBasis(basis));

        var unconfirmed = draft with { Lines = [Line("repair", workUnits: 1m, confirmed: false)] };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateSetCurrent(unconfirmed, Engineer));
        var discarded = draft with { State = RepairSpecificationState.Discarded };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateSetCurrent(discarded, Engineer));
        EstimatePolicy.ValidateSetCurrent(draft with { State = RepairSpecificationState.Accepted }, Engineer);
    }

    [Fact]
    public async Task SetCurrentConfirmsOnlyADraftReadyJobTheEstimateCites()
    {
        var jobs = new FakeJobStore();
        var ready = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.DraftReady, Client.SubjectId));
        var cancelled = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Cancelled, Client.SubjectId));
        var confirm = new RecordingConfirm();
        var store = new FakeSpecificationStore();
        var setCurrent = new SetCurrentEstimate(store, jobs, confirm, new FixedClock());

        store.Current = Estimate(Details(), Line("repair", workUnits: 1m)) with { AiJobId = ready, IsCurrent = true };
        await setCurrent.ExecuteAsync(SetCurrentRequest(Engineer, "op-ready"), CancellationToken.None);
        var command = Assert.Single(confirm.Commands);
        Assert.Equal(ready, command.JobId);
        Assert.Equal("op-ready:job", command.OperationKey);

        store.Current = store.Current with { AiJobId = cancelled };
        await setCurrent.ExecuteAsync(SetCurrentRequest(Engineer, "op-cancelled"), CancellationToken.None);
        Assert.Single(confirm.Commands);

        store.Current = store.Current with { AiJobId = null };
        await setCurrent.ExecuteAsync(SetCurrentRequest(Engineer, "op-none"), CancellationToken.None);
        Assert.Single(confirm.Commands);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setCurrent.ExecuteAsync(SetCurrentRequest(User, "op-user"), CancellationToken.None));
    }

    private static EstimateDetails Details() => new("Estimate 1", null, 40m, null, null, null, 20m, null);

    private static SaveEstimateRequest SaveRequest(
        ActionActor actor, RepairSpecificationSourceRoute route, Guid? jobId = null) => new(
        CaseId, 3, actor, "op-save", "Recorded an estimate.", Lease, null, Details(),
        [new("repair", null, "Repair door", 1.5m, null, false, null, null, null, null, null)],
        new(route, null, null, null), jobId);

    private static SetCurrentEstimateRequest SetCurrentRequest(ActionActor actor, string operationKey) => new(
        CaseId, 3, actor, operationKey, "Use estimate.", Lease, Guid.NewGuid());

    private static RepairSpecificationVersion Estimate(EstimateDetails details, params CaseEstimateLineRecord[] lines) => new(
        Guid.NewGuid(), CaseId, 1, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        lines, null, Engineer.SubjectId, Now, null, null, null, null, details);

    private static CaseEstimateLineRecord Line(
        string type, decimal? workUnits = null, decimal? paintWorkUnits = null,
        decimal? price = null, int? quantity = null, bool confirmed = true) => new(
        Guid.NewGuid(), 1, type, null, "Line", workUnits, price, false, null, null, null, null, null,
        ActorKind.Staff, Engineer.SubjectId, Now, confirmed ? Engineer.SubjectId : null, confirmed ? Now : null,
        paintWorkUnits, quantity);

    private static AiJobRecord Job(
        AiJobKind kind, Guid subjectId, AiJobState state, string takenBy, DateTimeOffset? leaseExpires = null) => new(
        Guid.NewGuid(), kind, AiJobPolicy.SubjectKindFor(kind), subjectId, "CE-1", "Draft an estimate.",
        kind == AiJobKind.Estimate ? 50 : null, kind == AiJobKind.Estimate ? 12000m : null, state,
        ActorKind.Staff, Engineer.SubjectId, Now - TimeSpan.FromHours(1), Now + TimeSpan.FromHours(23),
        takenBy, Now - TimeSpan.FromMinutes(5), leaseExpires ?? Now + TimeSpan.FromMinutes(25),
        null, null, null, null, null, null, 2);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeJobStore : IAiJobStore
    {
        private readonly Dictionary<Guid, AiJobRecord> jobs = [];

        public Guid Add(AiJobRecord job)
        {
            jobs[job.JobId] = job;
            return job.JobId;
        }

        public Task<AiJobRecord> CreateAsync(NewAiJob job, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobRecord?> GetAsync(Guid jobId, CancellationToken cancellationToken) =>
            Task.FromResult(jobs.GetValueOrDefault(jobId));

        public Task<AiJobRecord> TransitionAsync(AiJobTransition transition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingConfirm : IConfirmAiJob
    {
        public List<ConfirmAiJobCommand> Commands { get; } = [];

        public Task<AiJobRecord> ExecuteAsync(ConfirmAiJobCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(Job(AiJobKind.Estimate, CaseId, AiJobState.Completed, Client.SubjectId));
        }
    }

    private sealed class FakeSpecificationStore : IRepairSpecificationStore
    {
        public List<SaveEstimateRequest> Saved { get; } = [];

        public RepairSpecificationVersion Current { get; set; } = Estimate(Details(), Line("repair", workUnits: 1m));

        public Task<RepairSpecificationVersion> SaveEstimateAsync(SaveEstimateRequest request, CancellationToken cancellationToken)
        {
            Saved.Add(request);
            return Task.FromResult(Estimate(request.Details) with { AiJobId = request.AiJobId, Source = request.Source });
        }

        public Task<RepairSpecificationVersion> SetCurrentEstimateAsync(SetCurrentEstimateRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Current);

        public Task<RepairSpecificationVersion> StartDraftAsync(StartRepairSpecificationDraftRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> AcceptAsync(AcceptRepairSpecificationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion?> GetVersionAsync(Guid caseId, Guid specificationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion?> GetCurrentAcceptedAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion?> GetCurrentDraftAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> DuplicateEstimateAsync(DuplicateEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> DiscardEstimateAsync(DiscardEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepairSpecificationVersion>> ListEstimatesAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
