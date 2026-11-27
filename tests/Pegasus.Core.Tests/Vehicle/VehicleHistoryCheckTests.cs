using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Tests.Assessment;
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
            VehicleHistoryCheckNarrative.Compose(VehicleHistoryCheckResult.Found([])));
    }

    /// <summary>SV20VXT as UAT answered it on 20 November 2026 (R1218).</summary>
    [Fact]
    public void OneEntryIsOneSentenceInTheReportsCase()
    {
        Assert.Equal(
            "According to the Experian database:\n"
            + "An entry exists on the MIAFTR register: Cat S Structural Damage - Not Stolen - loss dated 09/04/2022.",
            VehicleHistoryCheckNarrative.Compose(VehicleHistoryCheckResult.Found(
                [new("CAT S STRUCTURAL DAMAGE", "NOT STOLEN", new DateOnly(2022, 4, 9))])));
    }

    /// <summary>
    /// SA15HNH as UAT answered it, its entries in Experian's order reversed:
    /// newest first under a count and the latest loss date, then the plate
    /// change, without Experian's guidance (R1218).
    /// </summary>
    [Fact]
    public void SeveralEntriesAreCountedAndListedNewestFirst()
    {
        var text = VehicleHistoryCheckNarrative.Compose(VehicleHistoryCheckResult.Found(
            [
                new("CAT D VEHICLE DAMAGED", "NOT STOLEN", new DateOnly(2016, 7, 2)),
                new("CAT N NON STRUCTURAL DAMAGE", "NOT STOLEN", new DateOnly(2023, 10, 5)),
            ],
            plateChangeOnRecord: true));

        Assert.Equal(
            "According to the Experian database:\n"
            + "There are 2 entries on the MIAFTR register, the most recent with a loss dated 05/10/2023:\n"
            + "Cat N Non Structural Damage - Not Stolen - loss dated 05/10/2023.\n"
            + "Cat D Vehicle Damaged - Not Stolen - loss dated 02/07/2016.\n"
            + "There is a plate change on record.",
            text);
    }

    /// <summary>WV10WPY's recovered entry keeps its hyphenated words (R1218).</summary>
    [Fact]
    public void ARecoveredEntryReadsAsTheReportsDid()
    {
        Assert.EndsWith(
            "An entry exists on the MIAFTR register: Recovered - Non-Structure Damage - Recovered - loss dated 16/03/2026.",
            VehicleHistoryCheckNarrative.Compose(VehicleHistoryCheckResult.Found(
                [new("RECOVERED - NON-STRUCTURE DAMAGE", "RECOVERED", new DateOnly(2026, 3, 16))])),
            StringComparison.Ordinal);
    }

    [Fact]
    public void APlateChangeAloneIsNeverTheCleanSentence()
    {
        Assert.Equal(
            "According to the Experian database:\nThere is a plate change on record.",
            VehicleHistoryCheckNarrative.Compose(VehicleHistoryCheckResult.Found([], plateChangeOnRecord: true)));
    }

    /// <summary>BP60XUK's import and AB12ABC's missing DVLA registration (R1218).</summary>
    [Fact]
    public void AnImportAndNoDvlaRegistrationEachHaveTheirLine()
    {
        Assert.Equal(
            "According to the Experian database:\n"
            + "The vehicle was imported from outside the EU. It was first registered in the UK in 2020; its year of manufacture is 2010.\n"
            + "There is no DVLA registration for this vehicle.",
            VehicleHistoryCheckNarrative.Compose(VehicleHistoryCheckResult.Found(
                [],
                importedFromOutsideEu: new VehicleImportRecord(2020, 2010),
                noDvlaRegistration: true)));
        Assert.EndsWith(
            "\nThe vehicle was imported from outside the EU.",
            VehicleHistoryCheckNarrative.Compose(VehicleHistoryCheckResult.Found(
                [], importedFromOutsideEu: new VehicleImportRecord(null, null))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnAbsentValueIsLeftOutWithItsDash()
    {
        Assert.EndsWith(
            "register: Cat S Structural Damage - loss dated 09/04/2022.",
            VehicleHistoryCheckNarrative.Compose(VehicleHistoryCheckResult.Found(
                [new("CAT S STRUCTURAL DAMAGE", " ", new DateOnly(2022, 4, 9))])),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFoundVehicleReplacesTheVehicleHistoryThroughOneAssessmentSave()
    {
        var store = new RecordingAssessmentStore();
        var check = Check(new AnsweringProvider(VehicleHistoryCheckResult.Found(
            [new("CAT S STRUCTURAL DAMAGE", "NOT STOLEN", new DateOnly(2020, 2, 1))])), store);

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
            VehicleHistoryCheckNarrative.FoundLead
            + "\nAn entry exists on the MIAFTR register: Cat S Structural Damage - Not Stolen - loss dated 01/02/2020.",
            field.Value);
    }

    [Fact]
    public async Task APlateChangeOnRecordIsRecordedWithTheVehicleHistory()
    {
        var store = new RecordingAssessmentStore();
        var check = Check(new AnsweringProvider(VehicleHistoryCheckResult.Found([], plateChangeOnRecord: true)), store);

        await check.ExecuteAsync(Request(), default);

        Assert.Equal(
            VehicleHistoryCheckNarrative.FoundLead + "\n" + VehicleHistoryCheckNarrative.PlateChangeLine,
            Assert.Single(Assert.Single(store.Saves).Fields).Value);
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

    /// <summary>
    /// The automatic Experian check (R1286, R1291) is Run Experian check run by
    /// the system: it claims the Case's edit lease for that one command as the
    /// Automation recorder of the check, records the text under it through the
    /// same save, and gives the lease back. It asks only for an accepted
    /// registration, so a Case with no mileage is checked.
    /// </summary>
    [Fact]
    public async Task AnAutomaticCheckRecordsUnderALeaseItClaimsAndGivesBack()
    {
        var caseId = Guid.NewGuid();
        var saves = new RecordingAssessmentStore();
        var provider = new AnsweringProvider(VehicleHistoryCheckResult.Found([]));
        var leases = new ScriptedCaseLeases();
        var store = new RecordingAutomaticStore(AutomaticRequestDoubles.Open());

        var outcome = await AutomaticCheck(provider, saves, store, leases)
            .ExecuteAsync(new(WorkItem, caseId, AutomaticRequestKind.VehicleHistoryCheck), default);

        Assert.Equal(AutomaticRequestOutcome.Recorded, outcome);
        Assert.Equal(["begin", "complete:Recorded"], store.Steps);
        var actor = AutomaticRequestPolicy.Actor(AutomaticRequestKind.VehicleHistoryCheck);
        Assert.Equal(ActorKind.Automation, actor.Kind);
        Assert.Equal("vehicle-history-check", actor.SubjectId);
        var claim = Assert.Single(leases.Claims);
        Assert.Same(actor, claim.Actor);
        Assert.Equal(caseId, claim.CaseId);
        var save = Assert.Single(saves.Saves);
        Assert.Same(actor, save.Actor);
        Assert.Equal(ScriptedCaseLeases.Token, save.EditLeaseToken);
        Assert.Equal(claim.ExpectedVersion, save.ExpectedVersion);
        Assert.Equal(VehicleHistoryCheckNarrative.Clean, Assert.Single(save.Fields).Value);
        var release = Assert.Single(leases.Releases);
        Assert.Equal(ScriptedCaseLeases.Token, release.LeaseToken);
        Assert.Same(actor, release.Actor);
    }

    /// <summary>
    /// The automatic check waits, asking nothing and claiming nothing, for an
    /// accepted registration, while anyone holds the Case's edit lease, and
    /// while the Case's assessment is not writable (a Held Case), as Run
    /// Experian check is offered only while the Vehicle history text is
    /// editable (R746).
    /// </summary>
    [Theory]
    [InlineData("registration")]
    [InlineData("lease")]
    [InlineData("held")]
    public async Task AnAutomaticCheckWaitsWithoutAskingExperian(string reason)
    {
        var provider = new AnsweringProvider(VehicleHistoryCheckResult.Found([]));
        var leases = new ScriptedCaseLeases();
        var store = new RecordingAutomaticStore(reason switch
        {
            "lease" => AutomaticRequestDoubles.Open() with { EditLeaseExpiresAtUtc = Now.AddMinutes(5) },
            "held" => AutomaticRequestDoubles.Open() with { AcceptsAssessment = false },
            _ => AutomaticRequestDoubles.Open(),
        });

        var outcome = await AutomaticCheck(
                provider, new RecordingAssessmentStore(), store, leases, reason == "registration" ? " " : "AB12CDE")
            .ExecuteAsync(new(WorkItem, Guid.NewGuid(), AutomaticRequestKind.VehicleHistoryCheck), default);

        Assert.Equal(AutomaticRequestOutcome.Waiting, outcome);
        Assert.Equal(0, provider.Calls);
        Assert.Empty(leases.Claims);
        Assert.Equal([$"defer:{Now + AutomaticRequestPolicy.Wait:O}"], store.Steps);
    }

    /// <summary>
    /// Vehicle history text that already holds words is never replaced by the
    /// automatic check (R1291), and Experian not being connected settles the
    /// request unasked; neither claims a lease.
    /// </summary>
    [Theory]
    [InlineData("text", AutomaticRequestOutcome.NotNeeded)]
    [InlineData("offline", AutomaticRequestOutcome.Unavailable)]
    public async Task AnAutomaticCheckIsSettledUnasked(string reason, AutomaticRequestOutcome expected)
    {
        var provider = new AnsweringProvider(VehicleHistoryCheckResult.Found([]));
        var saves = new RecordingAssessmentStore();
        var leases = new ScriptedCaseLeases();
        var store = new RecordingAutomaticStore(reason == "text"
            ? AutomaticRequestDoubles.Open() with { HoldsAnswer = true }
            : AutomaticRequestDoubles.Open());
        var caseData = new FixedCaseData("AB12CDE");
        var historyCheck = reason == "offline"
            ? new RunVehicleHistoryCheck([], caseData, new SaveAssessment(saves))
            : new RunVehicleHistoryCheck([provider], caseData, new SaveAssessment(saves));

        var outcome = await AutomaticRequestDoubles.Runner(
                new NoGuideValuations(), caseData, store, Now, historyCheck: historyCheck, leases: leases)
            .ExecuteAsync(new(WorkItem, Guid.NewGuid(), AutomaticRequestKind.VehicleHistoryCheck), default);

        Assert.Equal(expected, outcome);
        Assert.Equal(0, provider.Calls);
        Assert.Empty(leases.Claims);
        Assert.Empty(saves.Saves);
        Assert.Equal([$"complete:{expected}"], store.Steps);
    }

    /// <summary>
    /// A vehicle Experian holds no data for settles the request with nothing
    /// written and no notice (R1291); the lease is still given back.
    /// </summary>
    [Fact]
    public async Task AnAutomaticCheckOfAVehicleExperianDoesNotHoldWritesNothing()
    {
        var saves = new RecordingAssessmentStore();
        var leases = new ScriptedCaseLeases();
        var store = new RecordingAutomaticStore(AutomaticRequestDoubles.Open());

        var outcome = await AutomaticCheck(
                new AnsweringProvider(VehicleHistoryCheckResult.NotFound), saves, store, leases)
            .ExecuteAsync(new(WorkItem, Guid.NewGuid(), AutomaticRequestKind.VehicleHistoryCheck), default);

        Assert.Equal(AutomaticRequestOutcome.NotValued, outcome);
        Assert.Empty(saves.Saves);
        Assert.Equal(["begin", "complete:NotValued"], store.Steps);
        Assert.Single(leases.Releases);
    }

    /// <summary>
    /// A lease someone else claimed first leaves the request waiting and
    /// unasked: it is not marked asked before its lease is held, so the one
    /// request is not spent.
    /// </summary>
    [Fact]
    public async Task AnAutomaticCheckRefusedItsLeaseWaitsUnasked()
    {
        var caseId = Guid.NewGuid();
        var provider = new AnsweringProvider(VehicleHistoryCheckResult.Found([]));
        var leases = new ScriptedCaseLeases { ClaimRefusal = new CaseEditLeaseConflictException(caseId, 1) };
        var store = new RecordingAutomaticStore(AutomaticRequestDoubles.Open());

        var outcome = await AutomaticCheck(provider, new RecordingAssessmentStore(), store, leases)
            .ExecuteAsync(new(WorkItem, caseId, AutomaticRequestKind.VehicleHistoryCheck), default);

        Assert.Equal(AutomaticRequestOutcome.Waiting, outcome);
        Assert.Equal(0, provider.Calls);
        Assert.Single(leases.Claims);
        Assert.Empty(leases.Releases);
        Assert.Equal([$"defer:{Now + AutomaticRequestPolicy.Wait:O}"], store.Steps);
    }

    private static readonly Guid WorkItem = Guid.NewGuid();

    private static RunAutomaticRequest AutomaticCheck(
        IVehicleHistoryCheckProvider provider,
        RecordingAssessmentStore saves,
        RecordingAutomaticStore store,
        ScriptedCaseLeases leases,
        string registration = "AB12CDE")
    {
        var caseData = new FixedCaseData(registration);
        return AutomaticRequestDoubles.Runner(
            new NoGuideValuations(),
            caseData,
            store,
            Now,
            historyCheck: new RunVehicleHistoryCheck([provider], caseData, new SaveAssessment(saves)),
            leases: leases);
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
                new(null, null, null, null, null, null, "tbc", null, new DateOnly(2026, 9, 21), null, null, null, null)));
        }
    }
}
