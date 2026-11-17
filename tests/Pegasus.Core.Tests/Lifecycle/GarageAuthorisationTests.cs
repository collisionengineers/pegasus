using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

/// <summary>
/// The garage authorisation milestone of a Garage authorisation and invoice
/// Audit (operator, 17 November 2026; R1141 to R1144, R1159, R1161).
/// </summary>
public sealed class GarageAuthorisationTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly ActionActor Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly CaseWorkflowConfiguration Configuration = new("case-workflow", 1);
    private static readonly CaseCompleteness Complete = new(true, true);

    private static CaseGarageFacts Garage(
        bool authorised = false,
        bool waived = false,
        bool externalEstimateFiled = false,
        bool repairInvoiceFiled = false) =>
        new(true, authorised, waived, externalEstimateFiled, repairInvoiceFiled);

    [Theory]
    [InlineData(CaseType.Audit, AuditWorkflowKind.GarageAuthorisationAndInvoice, true)]
    [InlineData(CaseType.Audit, AuditWorkflowKind.OriginalEngineerReport, false)]
    [InlineData(CaseType.Inspection, AuditWorkflowKind.GarageAuthorisationAndInvoice, false)]
    [InlineData(CaseType.InspectionAndAudit, AuditWorkflowKind.GarageAuthorisationAndInvoice, false)]
    public void TheMilestoneBelongsToAStandaloneAuditOnTheGarageWorkflowAlone(
        CaseType caseType, AuditWorkflowKind workflow, bool applies) =>
        Assert.Equal(applies, GarageAuthorisationPolicy.Applies(caseType, workflow));

    /// <summary>A total loss or a cash in lieu has no repair to authorise or to invoice (R1143).</summary>
    [Theory]
    [InlineData("total_loss", true)]
    [InlineData("cash_in_lieu", true)]
    [InlineData("repairable", false)]
    [InlineData("contract_repair", false)]
    [InlineData(null, false)]
    public void ATotalLossOrCashInLieuOutcomeWaivesTheMilestone(string? outcome, bool waived) =>
        Assert.Equal(waived, GarageAuthorisationPolicy.IsWaived(outcome));

    /// <summary>
    /// A new Garage authorisation and invoice Audit is Not ready until a
    /// confirmed External estimate is filed, beside its other requirements
    /// (R1141). Another Case never names it.
    /// </summary>
    [Fact]
    public void TheWayIntoReviewNeedsAnExternalEstimate()
    {
        var waiting = CaseCompletenessPolicy.VehicleGate(CaseLifecycleState.NotReady, "AB12CDE", 41_500, Garage());
        Assert.False(waiting.IsOpen);
        Assert.Equal(
            [CaseWorkflowConfiguration.ExternalEstimateRequirement],
            Configuration.MissingRequirements(Complete, waiting));
        Assert.False(CaseCompletenessPolicy.Evaluate(Complete, Configuration, waiting).SatisfiesPolicy);

        var created = CaseCompletenessPolicy.VehicleGate(null, "AB12CDE", 41_500, Garage());
        Assert.False(created.ExternalEstimateFiled);

        var filed = CaseCompletenessPolicy.VehicleGate(
            CaseLifecycleState.NotReady, "AB12CDE", 41_500, Garage(externalEstimateFiled: true));
        Assert.True(filed.IsOpen);
        Assert.True(CaseCompletenessPolicy.Evaluate(Complete, Configuration, filed).SatisfiesPolicy);

        var other = CaseCompletenessPolicy.VehicleGate(
            CaseLifecycleState.NotReady, "AB12CDE", 41_500, CaseGarageFacts.None);
        Assert.True(other.IsOpen);
    }

    /// <summary>The gate stands only on the way in: a Case past Not ready is never sent back for it.</summary>
    [Theory]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.ReportSent)]
    public void ACasePastNotReadyHasPassedTheExternalEstimateGate(CaseLifecycleState state) =>
        Assert.True(CaseCompletenessPolicy.VehicleGate(state, null, null, Garage()).IsOpen);

    /// <summary>
    /// Once the garage is authorised the Case is Not ready for its repair
    /// invoice alone (R1144): it passed the other gates on its way into
    /// Review. A confirmed Repair invoice or a waiving Outcome ends the wait.
    /// </summary>
    [Fact]
    public void AnAuthorisedCaseWaitsForItsRepairInvoiceAlone()
    {
        var waiting = CaseCompletenessPolicy.VehicleGate(
            CaseLifecycleState.NotReady, null, null, Garage(authorised: true));
        Assert.Equal(
            [CaseWorkflowConfiguration.RepairInvoiceRequirement],
            Configuration.MissingRequirements(Complete, waiting));

        Assert.True(CaseCompletenessPolicy.VehicleGate(
            CaseLifecycleState.NotReady, null, null, Garage(authorised: true, repairInvoiceFiled: true)).IsOpen);
        Assert.True(CaseCompletenessPolicy.VehicleGate(
            CaseLifecycleState.NotReady, null, null, Garage(authorised: true, waived: true)).IsOpen);
    }

    /// <summary>
    /// Authorise garage is offered while the Audit is With Engineer with an
    /// Engineer, not yet authorised and not waived (R1142, R1143).
    /// </summary>
    [Fact]
    public void AuthoriseGarageIsOfferedWithEngineerUntilItIsDoneOrWaived()
    {
        Assert.True(GarageAuthorisationPolicy.CanAuthorise(
            CaseLifecycleState.ReportPreparation, EngineerId, Garage()));

        Assert.False(GarageAuthorisationPolicy.CanAuthorise(
            CaseLifecycleState.ReportPreparation, null, Garage()));
        Assert.False(GarageAuthorisationPolicy.CanAuthorise(
            CaseLifecycleState.ReportPreparation, EngineerId, Garage(authorised: true)));
        Assert.False(GarageAuthorisationPolicy.CanAuthorise(
            CaseLifecycleState.ReportPreparation, EngineerId, Garage(waived: true)));
        Assert.False(GarageAuthorisationPolicy.CanAuthorise(
            CaseLifecycleState.ReportPreparation, EngineerId, CaseGarageFacts.None));
        foreach (var other in Enum.GetValues<CaseLifecycleState>())
        {
            if (other != CaseLifecycleState.ReportPreparation)
            {
                Assert.False(GarageAuthorisationPolicy.CanAuthorise(other, EngineerId, Garage()));
            }
        }

        var refusal = Assert.Throws<InvalidOperationException>(() =>
            GarageAuthorisationPolicy.RequireCanAuthorise(CaseLifecycleState.Review, EngineerId, Garage()));
        Assert.Equal(GarageAuthorisationPolicy.AuthoriseNotOffered, refusal.Message);
    }

    /// <summary>Undo is offered once the garage is authorised, on an open Case that is not held (R1142).</summary>
    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.ReportSent, true)]
    [InlineData(CaseLifecycleState.Held, false)]
    [InlineData(CaseLifecycleState.PrincipalCancelled, false)]
    public void UndoIsOfferedOnAnOpenAuthorisedCaseThatIsNotHeld(CaseLifecycleState state, bool offered)
    {
        Assert.Equal(offered, GarageAuthorisationPolicy.CanUndo(state, isArchived: false, Garage(authorised: true)));
        Assert.False(GarageAuthorisationPolicy.CanUndo(state, isArchived: true, Garage(authorised: true)));
        Assert.False(GarageAuthorisationPolicy.CanUndo(state, isArchived: false, Garage()));
        Assert.False(GarageAuthorisationPolicy.CanUndo(state, isArchived: false, CaseGarageFacts.None));
    }

    [Fact]
    public void UndoRefusedNamesTheAct()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() =>
            GarageAuthorisationPolicy.RequireCanUndo(CaseLifecycleState.ReportPreparation, false, Garage()));
        Assert.Equal(GarageAuthorisationPolicy.UndoNotOffered, refusal.Message);
    }

    /// <summary>
    /// Marking the garage authorised leaves the Case waiting unless its
    /// Repair invoice is already filed, and the wait ends on a confirmed
    /// Repair invoice or a waiving Outcome (R1143, R1144).
    /// </summary>
    [Fact]
    public void TheWaitFollowsTheRepairInvoiceAndTheOutcome()
    {
        Assert.True(GarageAuthorisationPolicy.WaitsAfterAuthorisation(Garage()));
        Assert.False(GarageAuthorisationPolicy.WaitsAfterAuthorisation(Garage(repairInvoiceFiled: true)));

        Assert.False(GarageAuthorisationPolicy.WaitIsOver(CaseLifecycleState.NotReady, Garage(authorised: true)));
        Assert.True(GarageAuthorisationPolicy.WaitIsOver(
            CaseLifecycleState.NotReady, Garage(authorised: true, repairInvoiceFiled: true)));
        Assert.True(GarageAuthorisationPolicy.WaitIsOver(
            CaseLifecycleState.NotReady, Garage(authorised: true, waived: true)));
        // A Case that is not waiting has no wait to end.
        Assert.False(GarageAuthorisationPolicy.WaitIsOver(
            CaseLifecycleState.NotReady, Garage(repairInvoiceFiled: true)));
        Assert.False(GarageAuthorisationPolicy.WaitIsOver(
            CaseLifecycleState.ReportPreparation, Garage(authorised: true, repairInvoiceFiled: true)));
        Assert.False(GarageAuthorisationPolicy.WaitIsOver(CaseLifecycleState.NotReady, CaseGarageFacts.None));
    }

    /// <summary>Mark done takes no reason from staff: Core writes the one the history shows (R1161).</summary>
    [Fact]
    public async Task MarkDoneRecordsCoresReasonWhateverTheCallerSent()
    {
        var store = new RecordingStore();

        await new AuthoriseGarage(store).ExecuteAsync(
            new(CaseId, 3, Actor, "authorise-garage", string.Empty, "lease-token"), default);
        await new AuthoriseGarage(store).ExecuteAsync(
            new(CaseId, 4, Actor, "authorise-garage-again", "The caller's own words.", "lease-token"), default);

        Assert.Equal(2, store.Authorisations.Count);
        Assert.All(store.Authorisations, request => Assert.Equal("Garage authorised.", request.Reason));
    }

    /// <summary>Undo garage authorisation takes its reason from staff and refuses without one (R1142).</summary>
    [Fact]
    public async Task UndoNeedsAReason()
    {
        var store = new RecordingStore();
        var sut = new UndoGarageAuthorisation(store);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ExecuteAsync(
            new(CaseId, 3, Actor, "undo-garage", " ", "lease-token"), default));
        Assert.Empty(store.Undos);

        await sut.ExecuteAsync(
            new(CaseId, 3, Actor, "undo-garage", "Marked on the wrong Case.", "lease-token"), default);
        Assert.Equal("Marked on the wrong Case.", Assert.Single(store.Undos).Reason);
    }

    private sealed class RecordingStore : IGarageAuthorisationStore
    {
        public List<AuthoriseGarageRequest> Authorisations { get; } = [];
        public List<UndoGarageAuthorisationRequest> Undos { get; } = [];

        public Task<CaseWorkflowRecord> AuthoriseGarageAsync(
            AuthoriseGarageRequest request, CancellationToken cancellationToken)
        {
            Authorisations.Add(request);
            return Task.FromResult(Workflow(CaseLifecycleState.NotReady));
        }

        public Task<CaseWorkflowRecord> UndoGarageAuthorisationAsync(
            UndoGarageAuthorisationRequest request, CancellationToken cancellationToken)
        {
            Undos.Add(request);
            return Task.FromResult(Workflow(CaseLifecycleState.ReportPreparation));
        }

        private static CaseWorkflowRecord Workflow(CaseLifecycleState state) =>
            new(
                CaseId,
                new(CaseId, "ULA", 2026, 1, "a.ULA26001"),
                state,
                EngineerId,
                null,
                null,
                null,
                null,
                null,
                null,
                1);
    }
}
