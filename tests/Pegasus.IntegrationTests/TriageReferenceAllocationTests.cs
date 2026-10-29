using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A Triage Case takes its <c>t.</c> Case/PO from the same Principal-lineage
/// and year sequence every Case uses, proved against the real database.
/// </summary>
/// <remarks>
/// Every case here creates its Triage through the production
/// <c>ICreateTriageFromIntake</c>/<c>ITriageStore</c> path over real SQL, with
/// its receipt and evaluation prepared beforehand. The receipt and evaluation
/// fixtures are <c>TriageQueuesWebTests</c>' own, reused rather than copied.
/// The host clock is fixed in 2031, so the QDOS sequence is <c>QDOS31NNN</c>.
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed class TriageReferenceAllocationTests
{
    private const string QdosTriagePrefix = "t.QDOS31";

    [Fact]
    public async Task TheFirstTwoTriageCasesTakeTheFirstTwoNumbersOfTheSharedSequence()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var first = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-ALLOC-A");
        var second = await OpenTriageAsync(services, "XY12ZZZ", "TRIAGE-ALLOC-B");

        Assert.Equal("t.QDOS31001", first.Reference);
        Assert.Equal("t.QDOS31002", second.Reference);

        var summaries = await ListTriageAsync(services);
        var firstSummary = Assert.Single(summaries, item => item.CaseId == first.CaseId);
        // The provider claim number is a fact about the sender and keeps its
        // own member: it is not what the queue calls the reference.
        Assert.Equal("t.QDOS31001", firstSummary.Reference);
        Assert.Equal("TRIAGE-ALLOC-A", firstSummary.ClaimNumber);
        Assert.Equal(
            "t.QDOS31002",
            Assert.Single(summaries, item => item.CaseId == second.CaseId).Reference);
    }

    [Fact]
    public async Task CreationReplayReturnsTheOriginalReferenceAndTakesNoSecondNumber()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var operationKey = $"triage-alloc-replay:{Guid.NewGuid():N}";

        var prepared = await PrepareAsync(services, "AB12CDE", "TRIAGE-ALLOC-REPLAY");
        var created = await CreateAsync(services, prepared, operationKey);
        var replayed = await CreateAsync(services, prepared, operationKey);

        Assert.Equal("t.QDOS31001", created.Reference);
        Assert.Equal(created.Reference, replayed.Reference);
        Assert.Equal(created.CaseId, replayed.CaseId);
        Assert.Single(await ListTriageAsync(services));

        // The replay consumed nothing, so the next genuine creation takes the
        // very next number.
        var next = await OpenTriageAsync(services, "XY12ZZZ", "TRIAGE-ALLOC-NEXT");
        Assert.Equal("t.QDOS31002", next.Reference);
    }

    /// <summary>
    /// Triage Cases and manually created Cases racing on one Principal's
    /// sequence: the locked allocation queues them, so none deadlocks, none
    /// shares a number, and no number is skipped.
    /// </summary>
    [Fact]
    public async Task ConcurrentTriageAndManualCreationsShareTheSequenceWithoutDeadlockOrGap()
    {
        const int triageCreations = 4;
        const int manualCreations = 4;
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // Receipts and evaluations are prepared sequentially; only the
        // allocations race, so the one sequence row is what is contended.
        var prepared = new List<PreparedTriage>();
        for (var index = 0; index < triageCreations; index++)
        {
            prepared.Add(await PrepareAsync(services, $"AL{index:00}CAT", $"TRIAGE-ALLOC-{index:00}"));
        }

        var triageTasks = prepared.Select(item => Task.Run(async () =>
            (await CreateAsync(
                factory.Services,
                item,
                $"triage-alloc-concurrent:{item.ReceiptId:N}")).Reference));
        var manualTasks = Enumerable.Range(0, manualCreations).Select(index => Task.Run(async () =>
        {
            await using var manualScope = factory.Services.CreateAsyncScope();
            var identity = await manualScope.ServiceProvider.GetRequiredService<ICreateManualCase>().ExecuteAsync(
                new(
                    ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
                    $"triage-alloc-manual:{Guid.NewGuid():N}",
                    "QDOS",
                    CaseType.Inspection,
                    new(
                        ClaimantName: "Jane Doe",
                        ClaimNumber: $"C-{index}",
                        VehicleRegistration: $"MN{index:00}CAT")),
                CancellationToken.None);
            return identity.Reference;
        }));

        var references = await Task.WhenAll(triageTasks.Concat(manualTasks));

        var sequences = references
            .Select(reference => int.Parse(
                reference.StartsWith(QdosTriagePrefix, StringComparison.Ordinal)
                    ? reference[QdosTriagePrefix.Length..]
                    : reference["QDOS31".Length..],
                NumberStyles.None,
                CultureInfo.InvariantCulture))
            .Order()
            .ToArray();
        Assert.Equal(Enumerable.Range(1, triageCreations + manualCreations).ToArray(), sequences);
        Assert.Equal(
            triageCreations,
            references.Count(reference => reference.StartsWith(QdosTriagePrefix, StringComparison.Ordinal)));

        // Each Triage Case's reference is the one persisted against it.
        var listed = await ListTriageAsync(services);
        Assert.Equal(triageCreations, listed.Count);
        var queries = services.GetRequiredService<ITriageQueries>();
        foreach (var summary in listed)
        {
            var detail = await queries.GetAsync(summary.CaseId, CancellationToken.None);
            Assert.Equal(
                summary.Reference,
                Assert.IsType<TriageDetail>(detail).Record.Reference);
        }
    }

    [Fact]
    public async Task TheAllocatedReferenceReachesTheCaseRecord()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var triage = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-ALLOC-PAGE");

        Assert.Equal("t.QDOS31001", triage.Reference);

        using var response = await client.GetAsync($"/Cases/{triage.CaseId:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("t.QDOS31001", html, StringComparison.Ordinal);
        Assert.Contains("Triage reference", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheReferenceSurvivesEveryLaterMutation()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-ALLOC-IMMUTABLE");

        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var lease = await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, created.Version, actor,
                $"alloc-immutable-await-edit:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var awaited = await services.GetRequiredService<IAwaitTriageInformation>().ExecuteAsync(
            new TriageTransitionRequest(
                created.CaseId,
                created.Version,
                actor,
                $"alloc-immutable-await:{Guid.NewGuid():N}")
            {
                EditLeaseToken = lease.Token
            },
            CancellationToken.None);

        Assert.Equal("t.QDOS31001", created.Reference);
        Assert.Equal(created.Reference, awaited.Reference);
        var reread = Assert.IsType<TriageDetail>(
            await services.GetRequiredService<ITriageQueries>()
                .GetAsync(created.CaseId, CancellationToken.None));
        Assert.Equal(created.Reference, reread.Record.Reference);
    }

    [Fact]
    public async Task AColleagueCanTakeOverTriageAndThePreviousTokenCannotRenew()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-TAKEOVER");
        var leases = services.GetRequiredService<IEditScopeLeases>();
        var first = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var second = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var held = await leases.ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, created.Version, first, "triage-first"),
            CancellationToken.None);

        var taken = await leases.ClaimAsync(
            new ClaimEditScopeRequest(
                EditScopeKind.Triage, created.CaseId, created.Version, second, "triage-takeover")
            {
                TakeOver = true
            }, CancellationToken.None);

        Assert.NotEqual(held.Token, taken.Token);
        await Assert.ThrowsAsync<EditScopeConflictException>(() => leases.HeartbeatAsync(
            new(EditScopeKind.Triage, created.CaseId, first, held.Token), CancellationToken.None));
        var detail = Assert.IsType<TriageDetail>(
            await services.GetRequiredService<ITriageQueries>()
                .GetAsync(created.CaseId, CancellationToken.None));
        Assert.Contains(detail.History,
            entry => entry.EventType == "edit_lease_taken_over");
    }

    /// <summary>
    /// A takeover from a holder whose staff account name cannot be resolved records them as
    /// unnamed, never by their subject identifier (R520, 29 October 2026).
    /// </summary>
    [Fact]
    public async Task ATakeoverFromAnUnresolvableHolderNeverNamesItsIdentifier()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-TAKEOVER-UNNAMED");
        var leases = services.GetRequiredService<IEditScopeLeases>();
        var unresolvable = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var colleague = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        await leases.ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, created.Version, unresolvable,
                "triage-unnamed-first"),
            CancellationToken.None);

        await leases.ClaimAsync(
            new ClaimEditScopeRequest(
                EditScopeKind.Triage, created.CaseId, created.Version, colleague,
                "triage-unnamed-takeover")
            {
                TakeOver = true
            }, CancellationToken.None);

        var detail = Assert.IsType<TriageDetail>(
            await services.GetRequiredService<ITriageQueries>()
                .GetAsync(created.CaseId, CancellationToken.None));
        var history = Assert.Single(detail.History,
            entry => entry.EventType == "edit_lease_taken_over");
        Assert.Equal("Edit lease taken over from another member of staff.", history.Reason);
    }

    /// <summary>
    /// A one-off claim is refused while the holder's own scope is live, so it never ends their
    /// open window. Claiming their own scope again resumes it: the same token comes back, the
    /// earlier window keeps heartbeating, and no takeover of themselves is recorded (R518,
    /// 29 October 2026).
    /// </summary>
    [Fact]
    public async Task TheHoldersOwnScopeResumesWithTheSameTokenWithoutATakeover()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-OWN-RECLAIM");
        var leases = services.GetRequiredService<IEditScopeLeases>();
        var holder = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var held = await leases.ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, created.Version, holder, "triage-own-first"),
            CancellationToken.None);

        await Assert.ThrowsAsync<EditScopeConflictException>(() => leases.ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, created.Version, holder, "triage-own-one-off"),
            CancellationToken.None));
        var again = await leases.ClaimAsync(
            new ClaimEditScopeRequest(
                EditScopeKind.Triage, created.CaseId, created.Version, holder, "triage-own-second")
            {
                TakeOver = true
            },
            CancellationToken.None);

        Assert.Equal(held.Token, again.Token);
        Assert.Equal(held.Generation, again.Generation);
        Assert.Equal(holder.SubjectId, again.Holder);
        var beat = await leases.HeartbeatAsync(
            new(EditScopeKind.Triage, created.CaseId, holder, held.Token), CancellationToken.None);
        Assert.Equal(held.Token, beat.Token);
        var detail = Assert.IsType<TriageDetail>(
            await services.GetRequiredService<ITriageQueries>()
                .GetAsync(created.CaseId, CancellationToken.None));
        Assert.DoesNotContain(detail.History,
            entry => entry.EventType == "edit_lease_taken_over");
    }

    /// <summary>
    /// An Automation session never takes over a Triage scope another Automation session holds,
    /// as for a Case lease; a member of staff may (R517, 29 October 2026).
    /// </summary>
    [Fact]
    public async Task AutomationNeverTakesOverAnAutomationHeldTriageScope()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-AUTOMATION-TAKEOVER");
        var leases = services.GetRequiredService<IEditScopeLeases>();
        var automation = ActionActor.Automation($"grant:{Guid.NewGuid():N}");
        var otherAutomation = ActionActor.Automation($"grant:{Guid.NewGuid():N}");
        var staff = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var held = await leases.ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, created.Version, automation, "mcp:triage-first"),
            CancellationToken.None);

        await Assert.ThrowsAsync<EditScopeConflictException>(() => leases.ClaimAsync(
            new ClaimEditScopeRequest(
                EditScopeKind.Triage, created.CaseId, created.Version, otherAutomation, "mcp:triage-other")
            {
                TakeOver = true
            },
            CancellationToken.None));
        var taken = await leases.ClaimAsync(
            new ClaimEditScopeRequest(
                EditScopeKind.Triage, created.CaseId, created.Version, staff, "triage-staff-takeover")
            {
                TakeOver = true
            },
            CancellationToken.None);

        Assert.NotEqual(held.Token, taken.Token);
        var detail = Assert.IsType<TriageDetail>(
            await services.GetRequiredService<ITriageQueries>()
                .GetAsync(created.CaseId, CancellationToken.None));
        var history = Assert.Single(detail.History,
            entry => entry.EventType == "edit_lease_taken_over");
        Assert.Equal("Edit lease taken over from AI.", history.Reason);
    }

    /// <summary>
    /// A claim replayed with its operation key returns the token it was issued, as a Case
    /// claim's replay does, and records nothing more; the same key with other details is a new
    /// claim, refused while the scope is live (R519, 29 October 2026).
    /// </summary>
    [Fact]
    public async Task AReplayedTriageClaimReturnsTheSameToken()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-CLAIM-REPLAY");
        var leases = services.GetRequiredService<IEditScopeLeases>();
        var automation = ActionActor.Automation($"grant:{Guid.NewGuid():N}");
        var request = new ClaimEditScopeRequest(
            EditScopeKind.Triage, created.CaseId, created.Version, automation, "mcp:triage-replay");

        var first = await leases.ClaimAsync(request, CancellationToken.None);
        var replayed = await leases.ClaimAsync(request, CancellationToken.None);

        Assert.Equal(first.Token, replayed.Token);
        Assert.Equal(first.Generation, replayed.Generation);
        await Assert.ThrowsAsync<EditScopeConflictException>(() => leases.ClaimAsync(
            request with { TakeOver = true }, CancellationToken.None));
    }

    /// <summary>
    /// A scope is claimed at the record's current version: a claim from a page that read an
    /// older version is issued at the version the record now stands at, and only a version from
    /// the future is refused (R516, 29 October 2026).
    /// </summary>
    [Fact]
    public async Task ATriageScopeIsClaimedAtTheCurrentVersionAndRefusesOnlyANewerOne()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-CLAIM-VERSION");
        var leases = services.GetRequiredService<IEditScopeLeases>();
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var lease = await leases.ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, created.Version, actor,
                $"claim-version-await-edit:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var awaited = await services.GetRequiredService<IAwaitTriageInformation>().ExecuteAsync(
            new TriageTransitionRequest(
                created.CaseId,
                created.Version,
                actor,
                $"claim-version-await:{Guid.NewGuid():N}")
            {
                EditLeaseToken = lease.Token
            },
            CancellationToken.None);
        Assert.True(awaited.Version > created.Version);

        var older = await leases.ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, created.Version, actor, "claim-version-older"),
            CancellationToken.None);
        Assert.Equal(awaited.Version, older.RecordVersion);
        await leases.ReleaseAsync(
            new(EditScopeKind.Triage, created.CaseId, actor, "claim-version-older-release", older.Token),
            CancellationToken.None);

        var newer = await Assert.ThrowsAsync<EditScopeVersionConflictException>(() => leases.ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, awaited.Version + 1, actor, "claim-version-newer"),
            CancellationToken.None));
        Assert.Equal(awaited.Version, newer.ActualVersion);
    }

    /// <summary>
    /// A holder whose scope lapsed, with nobody having claimed the record since, carries on with
    /// the same token, as a Case lease does: the heartbeat picks it up again (R515, 29 October
    /// 2026).
    /// </summary>
    [Fact]
    public async Task ALapsedTriageScopeWhoseTokenStillMatchesCarriesOn()
    {
        var clock = new CaseWorkflowPersistenceTests.MutableTimeProvider(IntakeWebApplicationFactory.FixedUtcNow);
        using var factory = new IntakeWebApplicationFactory(clock);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-LAPSED-CARRIES-ON");
        var leases = services.GetRequiredService<IEditScopeLeases>();
        var automation = ActionActor.Automation($"grant:{Guid.NewGuid():N}");
        var held = await leases.ClaimAsync(
            new(EditScopeKind.Triage, created.CaseId, created.Version, automation, "mcp:triage-lapse"),
            CancellationToken.None);

        clock.Advance(EditScopeAuthority.Duration + TimeSpan.FromMinutes(1));
        Assert.Null(await leases.GetActiveAsync(
            EditScopeKind.Triage, created.CaseId, automation, CancellationToken.None));
        var picked = await leases.HeartbeatAsync(
            new(EditScopeKind.Triage, created.CaseId, automation, held.Token), CancellationToken.None);

        Assert.Equal(held.Token, picked.Token);
        Assert.True(picked.ExpiresAtUtc > clock.GetUtcNow());
    }

    /// <summary>
    /// One receipt with its retained accepted-match evidence and its
    /// evaluation revision — everything Triage creation requires, and nothing
    /// that races.
    /// </summary>
    private sealed record PreparedTriage(
        Guid ReceiptId,
        Guid EvaluationRevisionId,
        IntakeSourceIdentity SourceIdentity,
        string SourceHash,
        string Registration,
        IntakeEvidence AcceptedMatch);

    private static async Task<PreparedTriage> PrepareAsync(
        IServiceProvider services,
        string registration,
        string claimNumber)
    {
        var sourceIdentity = new IntakeSourceIdentity(
            IntakeSourceChannel.ManualUpload,
            Guid.NewGuid().ToString("N"));
        var sourceHash = new string('a', 64);
        var acceptedMatch = new IntakeEvidence(
            IntakeEvidenceSource.SystemDefault,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            registration,
            "Accepted Triage match for the reference-allocation test.",
            MatcherKey: "triage-allocation-test",
            MatcherVersion: 1);
        var receiptId = await TriageQueuesWebTests.StoreMinimalReceiptAsync(
            services,
            $"triage-allocation-{claimNumber.ToLowerInvariant()}.pdf",
            new InstructionDraft(
                SuggestedPrincipalCode: "QDOS",
                ClaimantName: null,
                ClaimNumber: claimNumber,
                VehicleRegistration: registration,
                VehicleMake: null,
                VehicleModel: null,
                VehicleMileage: null,
                AccidentCircumstances: null,
                DateOfIncident: null,
                InspectionAddress: null),
            [acceptedMatch],
            sourceIdentity,
            sourceHash);
        var evaluationRevisionId = await TriageQueuesWebTests.StageAndCompleteEvaluationAsync(
            services,
            receiptId);
        return new(
            receiptId,
            evaluationRevisionId,
            sourceIdentity,
            sourceHash,
            registration,
            acceptedMatch);
    }

    private static async Task<TriageRecord> CreateAsync(
        IServiceProvider services,
        PreparedTriage prepared,
        string operationKey)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICreateTriageFromIntake>().ExecuteAsync(
            new(
                new TriageOrigin(
                    prepared.ReceiptId,
                    prepared.SourceIdentity,
                    prepared.SourceHash,
                    prepared.EvaluationRevisionId),
                prepared.Registration,
                prepared.AcceptedMatch,
                // Creation is the intake worker's route, the one that admits
                // the system worker.
                ActionActor.SystemWorker("test-worker"),
                operationKey),
            CancellationToken.None);
    }

    private static async Task<TriageRecord> OpenTriageAsync(
        IServiceProvider services,
        string registration,
        string claimNumber)
    {
        var prepared = await PrepareAsync(services, registration, claimNumber);
        return await CreateAsync(
            services,
            prepared,
            $"triage-alloc-create:{prepared.ReceiptId:N}");
    }

    private static async Task<IReadOnlyList<TriageSummary>> ListTriageAsync(IServiceProvider services) =>
        await services.GetRequiredService<ITriageQueries>()
            .ListAsync(null, CancellationToken.None);
}
