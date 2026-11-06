using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Vehicle;

/// <summary>
/// Run Experian check (ADR-0089; operator, 6 November 2026, R741 to R748):
/// the wording it records and the one save it makes.
/// </summary>
public sealed class VehicleHistoryCheckTests
{
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ACleanVehicleReadsTheReferenceSentenceWithoutAutoCheck()
    {
        Assert.Equal(
            "We confirm having checked this vehicle on the Experian database and there is no record of any adverse history.",
            VehicleHistoryCheckNarrative.Compose([]));
    }

    [Fact]
    public void EachWriteOffIsOneLineUnderTheLeadSentence()
    {
        var text = VehicleHistoryCheckNarrative.Compose(
        [
            new("Category S", "Accident", new DateOnly(2021, 3, 12), "Aviva", "123456"),
            new("Category N", "Water", new DateOnly(2023, 1, 5), "Direct Line", "CL-9"),
        ]);

        Assert.Equal(
            "We confirm having checked this vehicle on the Experian database, which records:\n"
            + "Write-off: Category S, Accident, loss date 12/03/2021, Aviva, claim 123456\n"
            + "Write-off: Category N, Water, loss date 05/01/2023, Direct Line, claim CL-9",
            text);
    }

    [Fact]
    public void AnAbsentValueIsLeftOutWithItsComma()
    {
        var text = VehicleHistoryCheckNarrative.Compose([new("Category S", null, null, " Aviva ", " ")]);

        Assert.EndsWith("\nWrite-off: Category S, Aviva", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFoundVehicleReplacesTheVehicleHistoryThroughOneAssessmentSave()
    {
        var store = new RecordingAssessmentStore();
        var check = Check(new AnsweringProvider(VehicleHistoryCheckResult.Found(
            [new("Category S", "Fire", new DateOnly(2020, 2, 1), "Aviva", null)])), store);

        var outcome = await check.ExecuteAsync(Request(), default);

        Assert.Equal(VehicleHistoryCheckOutcome.Recorded, outcome);
        var save = Assert.Single(store.Saves);
        Assert.Equal(RunVehicleHistoryCheck.Reason, save.Reason);
        Assert.Equal(Engineer, save.Actor);
        Assert.Equal("op-1", save.OperationKey);
        Assert.Equal("lease-1", save.EditLeaseToken);
        Assert.Equal(7, save.ExpectedVersion);
        var field = Assert.Single(save.Fields);
        Assert.Equal(AssessmentVocabulary.HistoryCheck, field.Key);
        Assert.Equal(
            VehicleHistoryCheckNarrative.FoundLead + "\nWrite-off: Category S, Fire, loss date 01/02/2020, Aviva",
            field.Value);
    }

    [Fact]
    public async Task AVehicleExperianDoesNotHoldWritesNothing()
    {
        var store = new RecordingAssessmentStore();
        var check = Check(new AnsweringProvider(VehicleHistoryCheckResult.NotFound), store);

        Assert.Equal(VehicleHistoryCheckOutcome.NotFound, await check.ExecuteAsync(Request(), default));
        Assert.Empty(store.Saves);
    }

    [Fact]
    public async Task WithNoProviderTheCheckIsUnavailableAndNotConnected()
    {
        var store = new RecordingAssessmentStore();
        var check = new RunVehicleHistoryCheck([], new FixedCaseData("AB12CDE"), new SaveAssessment(store));

        Assert.False(check.IsConnected);
        await Assert.ThrowsAsync<VehicleHistoryCheckUnavailableException>(() => check.ExecuteAsync(Request(), default));
        Assert.Empty(store.Saves);
    }

    [Fact]
    public async Task ARegistrationOtherThanTheAcceptedOneIsRefusedBeforeExperianIsAsked()
    {
        var provider = new AnsweringProvider(VehicleHistoryCheckResult.Found([]));
        var check = Check(provider, new RecordingAssessmentStore());

        await Assert.ThrowsAsync<AcceptedVehicleRegistrationConflictException>(() =>
            check.ExecuteAsync(Request() with { Registration = "XY63ZZZ" }, default));
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task AnActorWithoutCaseworkIsRefused()
    {
        var provider = new AnsweringProvider(VehicleHistoryCheckResult.Found([]));
        var check = Check(provider, new RecordingAssessmentStore());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            check.ExecuteAsync(Request() with { Actor = ActionActor.SystemWorker("worker") }, default));
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task TheAutomationActorMayRunTheCheck()
    {
        var store = new RecordingAssessmentStore();
        var check = Check(new AnsweringProvider(VehicleHistoryCheckResult.Found([])), store);
        var automation = ActionActor.Automation("mcp-client");

        await check.ExecuteAsync(Request() with { Actor = automation }, default);

        Assert.Equal(automation, Assert.Single(store.Saves).Actor);
    }

    private static RunVehicleHistoryCheck Check(IVehicleHistoryCheckProvider provider, RecordingAssessmentStore store) =>
        new([provider], new FixedCaseData("AB12CDE"), new SaveAssessment(store));

    private static RunVehicleHistoryCheckRequest Request() =>
        new(Guid.NewGuid(), 7, Engineer, "op-1", "lease-1", "AB12CDE");

    private sealed class AnsweringProvider(VehicleHistoryCheckResult result) : IVehicleHistoryCheckProvider
    {
        public int Calls { get; private set; }

        public Task<VehicleHistoryCheckResult> CheckAsync(string registration, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FixedCaseData(string registration) : ICaseDataQueries
    {
        public Task<CaseDataProjection?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken)
        {
            var text = new CaseField<string>(null, null, null);
            var date = new CaseField<DateOnly>(null, null, null);
            var number = new CaseField<long>(null, null, null);
            var source = new CaseDataSource(CaseDataSourceKind.StaffCorrection, "test", "Test", "test", 1);
            return Task.FromResult<CaseDataProjection?>(new(
                new CaseIdentity(caseId, "QDOS", 2031, 1, "QDOS3100001"),
                new(null, null, null, null, null, null, null, null, null),
                DateTimeOffset.UnixEpoch,
                1,
                CaseLifecycleState.Review,
                new(new(false, false), new(false, "test", 1)),
                new(text),
                new(text, text, text),
                new(text),
                new(
                    new(new(registration, CaseDataValueKind.Fact, source), null, null),
                    text,
                    text,
                    text,
                    number,
                    text),
                new(date, text),
                new(new DateOnly(1970, 1, 1), text),
                new(date, date, text, new(null, null, null))));
        }
    }

    private sealed class RecordingAssessmentStore : ICaseAssessmentStore
    {
        public List<SaveAssessmentRequest> Saves { get; } = [];

        public Task<CaseAssessmentProjection?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseAssessmentProjection> SaveAsync(SaveAssessmentRequest request, CancellationToken cancellationToken)
        {
            Saves.Add(request);
            return Task.FromResult(new CaseAssessmentProjection(
                request.CaseId,
                "CASE-1",
                request.ExpectedVersion + 1,
                CaseLifecycleState.Review,
                null,
                [.. request.Fields.Select(item => new AssessmentFieldValue(
                    item.Key, item.Value!, request.Actor.Kind, request.Actor.SubjectId, Now))],
                [],
                new(null, null, null, null, null, null, "tbc", null, new DateOnly(2026, 9, 21), null, null, null, null, null)));
        }
    }
}
