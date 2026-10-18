using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

public sealed partial class QdosTriageIntegrationTests
{
    private const string GenuineFormalInstructionHash =
        "3063ff9ecb31878f582fb439047d999a41a7c6fe5b978cfbee5c7e7f277553b4";

    [Fact]
    public async Task AutomaticPairingRechecksCurrentIdentityLeaseVersionAndManualIntent()
    {
        using var factory = new IntakeWebApplicationFactory();
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-link-guards.eml");
        _ = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        var triage = (await GetOnlyTriageAsync(factory.Services)).Record;
        var caseId = await SeedMatchingFormalCaseAsync(factory.Services, triage.Origin!.ReceiptId);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<ITriageStore>();
        var pairing = services.GetRequiredService<ITriageCasePairing>();
        var worker = ActionActor.SystemWorker(TriageCasePairing.ActorId);
        var staff = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var candidate = Assert.Single(await store.ListAutomaticLinkCandidatesAsync(null, null, 1, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            store.LinkAutomaticallyAsync(candidate, staff, CancellationToken.None));
        var contexts = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contexts.CreateDbContextAsync();
        var initialExternalWorkCount = await context.ExternalWorkItems.CountAsync();

        // Change the CURRENT index after candidate discovery. The accepted
        // Triage VRM must not be overwritten or replaced by its target's VRM.
        await context.CaseMatchIndex.Where(item => item.CaseId == caseId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.NormalizedVrm, "PG18BTY"));
        Assert.False(await store.LinkAutomaticallyAsync(candidate, worker, CancellationToken.None));
        await context.CaseMatchIndex.Where(item => item.CaseId == caseId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.NormalizedVrm, triage.NormalizedVehicleRegistration));
        await context.CaseWorkflows.Where(item => item.CaseId == caseId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Version, 1L));
        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            store.LinkAutomaticallyAsync(candidate, worker, CancellationToken.None));
        candidate = Assert.Single(await store.ListAutomaticLinkCandidatesAsync(null, null, 1, CancellationToken.None));
        var competitorId = await SeedMatchingFormalCaseAsync(factory.Services, triage.Origin!.ReceiptId, 2);
        Assert.False(await store.LinkAutomaticallyAsync(candidate, worker, CancellationToken.None));
        Assert.Equal(new TriageCasePairingResult(0, 0, 0), await pairing.ReconcileAsync(1, CancellationToken.None));

        // A redirect may have matching keys, but cannot change this Triage's
        // known customer. Both candidate queries must use the final transaction.
        var otherPrincipal = await context.Principals.AsNoTracking().SingleAsync(item => item.Code == "ALS");
        await context.Cases.Where(item => item.Id == competitorId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.PrincipalId, otherPrincipal.Id));
        await context.CaseMatchIndex.Where(item => item.CaseId == competitorId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.PrincipalCode, otherPrincipal.Code));
        await context.CaseWorkflows.Where(item => item.CaseId == caseId).ExecuteUpdateAsync(update =>
            update.SetProperty(item => item.State, nameof(CaseLifecycleState.CreatedInError))
                .SetProperty(item => item.ReplacementCaseId, competitorId));
        Assert.False(await store.LinkAutomaticallyAsync(candidate, worker, CancellationToken.None));
        Assert.Empty(await store.ListAutomaticLinkCandidatesAsync(null, null, 1, CancellationToken.None));
        await context.CaseMatchIndex.Where(item => item.CaseId == competitorId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.NormalizedVrm, "PG18BTY"));
        await context.CaseWorkflows.Where(item => item.CaseId == caseId).ExecuteUpdateAsync(update =>
            update.SetProperty(item => item.State, nameof(CaseLifecycleState.Review))
                .SetProperty(item => item.ReplacementCaseId, (Guid?)null));

        var lease = await ClaimCaseLeaseAsync(factory.Services, caseId, 1, staff, "automatic-link-live-lease");
        Assert.False(await store.LinkAutomaticallyAsync(candidate, worker, CancellationToken.None));
        Assert.Equal(new TriageCasePairingResult(0, 0, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
        await context.CaseWorkflows.Where(item => item.CaseId == caseId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EditLeaseExpiresAtUtc, DateTimeOffset.UnixEpoch));
        await context.Triage.Where(item => item.CaseId == triage.CaseId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, "cancelled"));
        Assert.False(await store.LinkAutomaticallyAsync(candidate, worker, CancellationToken.None));
        await context.Triage.Where(item => item.CaseId == triage.CaseId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, "open"));
        Assert.Equal(new TriageCasePairingResult(1, 1, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
        Assert.False(await store.LinkAutomaticallyAsync(candidate, worker, CancellationToken.None));

        var linked = await GetTriageAsync(factory.Services, triage.CaseId);
        Assert.Equal(triage.Reference, linked.Record.Reference);
        Assert.Equal(triage.State, linked.Record.State);
        Assert.Equal(caseId, linked.Record.LinkedInstructionCaseId);
        var unlinkLease = await ClaimCaseLeaseAsync(factory.Services, caseId, 2, staff, "automatic-link-manual-unlink");
        var unlinkTriageLease = await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.Triage, triage.CaseId, linked.Record.Version, staff,
                "automatic-link-manual-unlink-edit"),
            CancellationToken.None);
        await services.GetRequiredService<IUnlinkTriageCase>().ExecuteAsync(new(
            triage.CaseId, caseId, linked.Record.Version, 2, staff, "deliberate-unlink",
            "Keep this Triage separate.", unlinkLease.Token)
        {
            EditLeaseToken = unlinkTriageLease.Token
        }, CancellationToken.None);
        Assert.Equal(new TriageCasePairingResult(0, 0, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
        var unlinked = await GetTriageAsync(factory.Services, triage.CaseId);
        Assert.Null(unlinked.Record.LinkedInstructionCaseId);
        Assert.Single(unlinked.History, item => item.EventType == "triage_case_linked" && item.ActorKind == nameof(ActorKind.SystemWorker));
        Assert.Single(unlinked.History, item => item.EventType == "triage_case_unlinked");
        // The two seeded instructed Cases; the Triage Case is a Case too.
        Assert.Equal(2, await context.Cases.CountAsync(item => item.Type != CaseTypeCodes.Triage));
        Assert.Equal(initialExternalWorkCount, await context.ExternalWorkItems.CountAsync());
        Assert.False(string.IsNullOrWhiteSpace(lease.Token));
    }

    [Fact]
    public async Task AutomaticLinkCandidatesForAnAcceptedCaseAreScopedToItsPrincipal()
    {
        using var factory = new IntakeWebApplicationFactory();
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-link-principal-scope.eml");
        _ = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        var triage = (await GetOnlyTriageAsync(factory.Services)).Record;
        var caseId = await SeedMatchingFormalCaseAsync(factory.Services, triage.Origin!.ReceiptId);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ITriageStore>();

        var scoped = Assert.Single(await store.ListAutomaticLinkCandidatesAsync(null, caseId, 5, CancellationToken.None));
        Assert.Equal(caseId, scoped.InstructionCaseId);
        Assert.Empty(await store.ListAutomaticLinkCandidatesAsync(null, Guid.NewGuid(), 5, CancellationToken.None));

        var contexts = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contexts.CreateDbContextAsync())
        {
            var instruction = await context.Cases.SingleAsync(item => item.Id == caseId);
            var otherPrincipal = await context.Principals.AsNoTracking()
                .FirstAsync(item => item.Id != instruction.PrincipalId);
            instruction.PrincipalId = otherPrincipal.Id;
            await context.SaveChangesAsync();
        }

        Assert.Empty(await store.ListAutomaticLinkCandidatesAsync(null, caseId, 5, CancellationToken.None));
    }

    /// <summary>
    /// A listing sends the statements of its pages and of one read of the
    /// match index and one of the matched Cases, whatever the page holds: here
    /// the page read, the read that finds the end of the listing, the match
    /// index and the Cases. An empty queue costs one.
    /// </summary>
    [Fact]
    public async Task ListingAutomaticLinkCandidatesCostsTheSameStatementsForThreeAndSixTriages()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory("Development", true, commandInterceptor: counter);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<ITriageStore>();

        counter.Reset();
        Assert.Empty(await store.ListAutomaticLinkCandidatesAsync(null, null, 50, CancellationToken.None));
        var emptyQueue = counter.Count;

        var original = await SubmitTriageOriginAsync(factory.Services, "triage-link-statement-count.eml");
        await SeedMatchingFormalCaseAsync(factory.Services, original.Id);
        await CreateTriageCopiesAsync(services, original, 0, 2);
        counter.Reset();
        Assert.Equal(3, (await store.ListAutomaticLinkCandidatesAsync(null, null, 50, CancellationToken.None)).Count);
        var withThree = counter.Count;

        await CreateTriageCopiesAsync(services, original, 2, 3);
        counter.Reset();
        Assert.Equal(6, (await store.ListAutomaticLinkCandidatesAsync(null, null, 50, CancellationToken.None)).Count);
        var withSix = counter.Count;

        const int ceiling = 4;
        Assert.True(emptyQueue == 1, $"An empty queue sent {emptyQueue} statements; one is allowed.");
        Assert.True(withThree > 0, "The interceptor observed no statements at all.");
        Assert.True(
            withThree == withSix,
            $"The listing's statements grew with the Triage Cases: {withThree} for three and {withSix} for six.");
        Assert.True(withSix <= ceiling, $"The listing sent {withSix} statements; at most {ceiling} are allowed.");
    }

    /// <summary>
    /// A page of Triage Cases that none passes the gate for costs only its
    /// page read: no match index is read for it, and the next page is.
    /// </summary>
    [Fact]
    public async Task ListingAutomaticLinkCandidatesReadsASecondPageWhenTheFirstHoldsNoCandidate()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory("Development", true, commandInterceptor: counter);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var original = await SubmitTriageOriginAsync(factory.Services, "triage-link-second-page.eml");
        await SeedMatchingFormalCaseAsync(factory.Services, original.Id);
        await CreateTriageCopiesAsync(services, original, 0, 50);
        var contexts = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contexts.CreateDbContextAsync();
        // Triage Cases share the factory's clock, so the order is the
        // database's (created time, then Case id), not the creation order.
        var order = await context.Triage.AsNoTracking()
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.CaseId)
            .Select(item => new { item.CaseId, ReceiptId = item.OriginReceiptId!.Value })
            .ToListAsync();
        Assert.Equal(51, order.Count);
        var firstPage = order.Take(50).Select(item => item.ReceiptId).ToList();
        await context.InstructionDrafts.Where(item => firstPage.Contains(item.IntakeReceiptId))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.SuggestedPrincipalCode, (string?)null));

        counter.Reset();
        var candidate = Assert.Single(await services.GetRequiredService<ITriageStore>()
            .ListAutomaticLinkCandidatesAsync(null, null, 1, CancellationToken.None));

        Assert.Equal(order[50].CaseId, candidate.CaseId);
        var matchIndexReads = counter.CountContaining("CaseMatchIndex");
        Assert.True(
            matchIndexReads == 1,
            $"The match index was read {matchIndexReads} times; once, for the second page, is allowed.");
    }

    /// <summary>
    /// The Worker's sweep lists from where its last listing stopped and, at
    /// the end of the listing, starts again at the oldest Triage Case; a call
    /// scoped to an accepted Case lists from the oldest and leaves it alone.
    /// </summary>
    [Fact]
    public async Task TheSweepContinuesFromWhereItStoppedAndStartsAgainAtTheEndOfTheListing()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var original = await SubmitTriageOriginAsync(factory.Services, "triage-link-sweep-cursor.eml");
        var caseId = await SeedMatchingFormalCaseAsync(factory.Services, original.Id);
        await CreateTriageCopiesAsync(services, original, 0, 50);
        var contexts = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contexts.CreateDbContextAsync();
        var order = await context.Triage.AsNoTracking()
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.CaseId)
            .Select(item => new { item.CaseId, item.CreatedAtUtc, ReceiptId = item.OriginReceiptId!.Value })
            .ToListAsync();
        Assert.Equal(51, order.Count);
        // Only the 50th and 51st can link: the first page ends on a candidate
        // and the second holds the other.
        var gatedOut = order.Take(49).Select(item => item.ReceiptId).ToList();
        await context.InstructionDrafts.Where(item => gatedOut.Contains(item.IntakeReceiptId))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.SuggestedPrincipalCode, (string?)null));
        var cursor = new TriagePairingSweepCursor();
        var sweep = new EfTriageStore(
            contexts, services.GetRequiredService<IEnumerable<IPrincipalCaseMatchPolicy>>(), null, cursor);

        var first = Assert.Single(await sweep.ListAutomaticLinkCandidatesAsync(null, null, 1, CancellationToken.None));
        Assert.Equal(order[49].CaseId, first.CaseId);
        Assert.Equal(new TriagePairingPosition(order[49].CreatedAtUtc, order[49].CaseId), cursor.After);

        var second = Assert.Single(await sweep.ListAutomaticLinkCandidatesAsync(null, null, 1, CancellationToken.None));
        Assert.Equal(order[50].CaseId, second.CaseId);
        Assert.Equal(new TriagePairingPosition(order[50].CreatedAtUtc, order[50].CaseId), cursor.After);

        var scoped = await sweep.ListAutomaticLinkCandidatesAsync(null, caseId, 5, CancellationToken.None);
        Assert.Equal(new[] { order[49].CaseId, order[50].CaseId }, scoped.Select(item => item.CaseId));
        Assert.Equal(new TriagePairingPosition(order[50].CreatedAtUtc, order[50].CaseId), cursor.After);

        Assert.Empty(await sweep.ListAutomaticLinkCandidatesAsync(null, null, 1, CancellationToken.None));
        Assert.Null(cursor.After);
        var again = Assert.Single(await sweep.ListAutomaticLinkCandidatesAsync(null, null, 1, CancellationToken.None));
        Assert.Equal(order[49].CaseId, again.CaseId);
    }

    private static async Task<IntakeReceipt> SubmitTriageOriginAsync(IServiceProvider services, string fileName)
    {
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(
            services, IntakeTestEvidence.CreateEngineerTriageRequest(fileName));
        await using var scope = services.CreateAsyncScope();
        return Assert.IsType<IntakeReceipt>(
            await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None));
    }

    /// <summary>
    /// Opens <paramref name="count"/> more Triage Cases from copies of the
    /// original's receipt, as the pending-selection test does: distinct test
    /// occurrences of the original's evidence and typed values.
    /// </summary>
    private static async Task CreateTriageCopiesAsync(
        IServiceProvider services, IntakeReceipt original, int firstIndex, int count)
    {
        for (var index = firstIndex; index < firstIndex + count; index++)
        {
            var identity = new IntakeSourceIdentity(original.SourceIdentity.Channel, $"triage-pairing-copy:{index}");
            var copy = await services.GetRequiredService<IIntakeReceiptStore>().StoreAsync(new(
                original.SourceFileName, original.MediaType, original.SourceLength, original.SourceHash, identity,
                original.ReceivedAtUtc, original.ProcessedAtUtc, "triage-pairing-fixture", original.Decision,
                original.DecisionReason, original.Evidence, original.Fields, original.InstructionDraft,
                original.MissingFields, original.FailureCode, original.FailureReason, original.SourceReaderKey,
                original.SourceReaderVersion, original.ExtractionPolicyKey, original.ExtractionPolicyVersion), CancellationToken.None);
            var evaluation = await TriageQueuesWebTests.StageAndCompleteEvaluationAsync(services, copy.Id);
            await services.GetRequiredService<ITriageStore>().CreateAsync(new(new(copy.Id, identity, copy.SourceHash, evaluation),
                "VO75DFJ", Assert.Single(copy.Evidence, item => item.Finding == IntakeEvidenceFinding.AcceptedTriageMatch),
                ActionActor.SystemWorker("triage-pairing-fixture"), $"triage-pairing-create:{index}"), CancellationToken.None);
        }
    }

    internal static async Task<Guid> SeedMatchingFormalCaseAsync(
        IServiceProvider services, Guid originReceiptId, int sequence = 1)
    {
        // Existing persisted-Case fixture using this real Triage's accepted
        // customer/typed identity; not a fabricated formal instruction.
        await using var scope = services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var triage = await context.Triage.AsNoTracking().SingleAsync(item => item.OriginReceiptId == originReceiptId);
        var principalId = await context.Cases.AsNoTracking()
            .Where(item => item.Id == triage.CaseId)
            .Select(item => item.PrincipalId)
            .SingleAsync();
        var principal = await context.Principals.AsNoTracking().SingleAsync(item => item.Id == principalId);
        var draft = await context.InstructionDrafts.AsNoTracking().SingleAsync(item => item.IntakeReceiptId == originReceiptId);
        var policy = new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy());
        var keys = policy.DeriveIndexKeys(new(draft.ClaimNumber, triage.NormalizedVehicleRegistration,
            draft.ClaimantName, draft.DateOfIncident));
        var caseId = Guid.NewGuid();
        // The Triage Case already took its number from this Principal's 2031
        // sequence, so the fixture Case stands well clear of the allocator.
        var fixtureSequence = 900 + sequence;
        context.Cases.Add(new()
        {
            Id = caseId, PrincipalId = principal.Id, SequenceLineageId = principal.SequenceLineageId,
            Year = 2031, Sequence = fixtureSequence, Reference = $"QDOS31{fixtureSequence:D3}",
            Type = "inspection", InitialState = "review", CustodyState = "pending",
            OriginIntakeReceiptId = originReceiptId, InstructionComplete = true, ImagesComplete = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        context.CaseWorkflows.Add(new() { CaseId = caseId, State = nameof(CaseLifecycleState.Review) });
        context.CaseMatchIndex.Add(new()
        {
            CaseId = caseId, PrincipalCode = principal.Code, DurableClaimToken = keys.DurableClaimToken,
            NormalizedVrm = keys.NormalizedVrm, NormalizedSurname = keys.NormalizedSurname,
            NormalizedFirstInitial = keys.NormalizedFirstInitial, IncidentDate = keys.IncidentDate,
            MatchPolicyKey = policy.PolicyKey, MatchPolicyVersion = policy.PolicyVersion, UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return caseId;
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task CaseAssociationUsesCanonicalWorkflowVersionAndActiveCaseLease()
    {
        using var factory = new IntakeWebApplicationFactory();
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-case-association.eml");
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        var initial = await GetOnlyTriageAsync(factory.Services);
        var triageId = initial.Record.CaseId;
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var caseId = await SeedCaseAsync(factory.Services, receiptId);
        var firstCaseLease = await ClaimCaseLeaseAsync(
            factory.Services,
            caseId,
            0,
            actor,
            "claim-first-case-association-lease");
        var link = services.GetRequiredService<ILinkTriageCase>();
        var unlink = services.GetRequiredService<IUnlinkTriageCase>();
        var firstTriageLease = await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.Triage, triageId, 0, actor, "claim-first-triage-association-edit"),
            CancellationToken.None);

        var hiddenVersionRequest = new TriageCaseLinkRequest(
            triageId,
            caseId,
            0,
            SeededCaseEntityVersion,
            actor,
            "link-case-with-hidden-version",
            "The hidden case-row version must not authorize association",
            firstCaseLease.Token)
        {
            EditLeaseToken = firstTriageLease.Token
        };
        var hiddenVersionConflict = await Assert.ThrowsAsync<CaseVersionConflictException>(
            () => link.ExecuteAsync(hiddenVersionRequest, CancellationToken.None));
        Assert.Equal(SeededCaseEntityVersion, hiddenVersionConflict.ExpectedVersion);
        Assert.Equal(0, hiddenVersionConflict.ActualVersion);

        var linkRequest = hiddenVersionRequest with
        {
            ExpectedCaseVersion = 0,
            OperationKey = "link-case-canonical-version",
            Reason = "Associate retained Triage evidence with the case"
        };
        await link.ExecuteAsync(linkRequest, CancellationToken.None);
        await link.ExecuteAsync(linkRequest, CancellationToken.None);
        await Assert.ThrowsAsync<TriageOperationConflictException>(
            () => link.ExecuteAsync(
                linkRequest with { Reason = "Altered association request" },
                CancellationToken.None));

        var linked = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(caseId, linked.Record.LinkedInstructionCaseId);
        Assert.Equal(1, linked.Record.Version);
        var afterLink = await ReadCaseAssociationPersistenceAsync(factory.Database, caseId);
        Assert.Equal(SeededCaseEntityVersion, afterLink.CaseEntityVersion);
        Assert.Equal(1, afterLink.WorkflowVersion);
        Assert.False(afterLink.HasActiveLease);
        Assert.Collection(
            afterLink.Events,
            item =>
            {
                Assert.Equal("triage_case_linked", item.EventType);
                Assert.Equal(linkRequest.OperationKey, item.OperationKey);
                Assert.Equal(0, item.BeforeVersion);
                Assert.Equal(1, item.AfterVersion);
            });

        var consumedLeaseTriageLease = await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.Triage, triageId, 1, actor, "claim-consumed-case-lease-triage-edit"),
            CancellationToken.None);
        var consumedLeaseRequest = new TriageCaseLinkRequest(
            triageId,
            caseId,
            1,
            1,
            actor,
            "unlink-case-with-consumed-lease",
            "A consumed case lease must not authorize another mutation",
            firstCaseLease.Token)
        {
            EditLeaseToken = consumedLeaseTriageLease.Token
        };
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(
            () => unlink.ExecuteAsync(consumedLeaseRequest, CancellationToken.None));

        var secondCaseLease = await ClaimCaseLeaseAsync(
            factory.Services,
            caseId,
            1,
            actor,
            "claim-second-case-association-lease");
        // Under a proven lease an older version is system work having moved
        // the Case; only a version the Case has not reached is refused.
        var staleCaseVersionRequest = consumedLeaseRequest with
        {
            ExpectedCaseVersion = 2,
            OperationKey = "unlink-case-with-stale-version",
            Reason = "A canonical workflow version the case has not reached must fail",
            CaseEditLeaseToken = secondCaseLease.Token
        };
        var staleCaseVersion = await Assert.ThrowsAsync<CaseVersionConflictException>(
            () => unlink.ExecuteAsync(staleCaseVersionRequest, CancellationToken.None));
        Assert.Equal(2, staleCaseVersion.ExpectedVersion);
        Assert.Equal(1, staleCaseVersion.ActualVersion);

        var wrongCaseTokenRequest = staleCaseVersionRequest with
        {
            ExpectedCaseVersion = 1,
            OperationKey = "unlink-case-with-wrong-token",
            Reason = "A different case lease token must fail",
            CaseEditLeaseToken = new string('e', 64)
        };
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(
            () => unlink.ExecuteAsync(wrongCaseTokenRequest, CancellationToken.None));

        var unlinkRequest = wrongCaseTokenRequest with
        {
            OperationKey = "unlink-case-canonical-version",
            Reason = "Correct the retained Triage-to-case association",
            CaseEditLeaseToken = secondCaseLease.Token
        };
        await unlink.ExecuteAsync(unlinkRequest, CancellationToken.None);
        await unlink.ExecuteAsync(unlinkRequest, CancellationToken.None);
        await Assert.ThrowsAsync<TriageOperationConflictException>(
            () => unlink.ExecuteAsync(
                unlinkRequest with { Reason = "Altered disassociation request" },
                CancellationToken.None));

        var final = await GetTriageAsync(factory.Services, triageId);
        Assert.Null(final.Record.LinkedInstructionCaseId);
        Assert.Equal(2, final.Record.Version);
        Assert.Collection(
            final.History,
            item => Assert.Equal("triage_created", item.EventType),
            item => Assert.Equal("triage_case_linked", item.EventType),
            item => Assert.Equal("triage_case_unlinked", item.EventType));

        var persistence = await ReadCaseAssociationPersistenceAsync(factory.Database, caseId);
        Assert.Equal(SeededCaseEntityVersion, persistence.CaseEntityVersion);
        Assert.Equal(2, persistence.WorkflowVersion);
        Assert.False(persistence.HasActiveLease);
        Assert.Collection(
            persistence.Events,
            item =>
            {
                Assert.Equal("triage_case_linked", item.EventType);
                Assert.Equal(linkRequest.OperationKey, item.OperationKey);
                Assert.Equal(0, item.BeforeVersion);
                Assert.Equal(1, item.AfterVersion);
            },
            item =>
            {
                Assert.Equal("triage_case_unlinked", item.EventType);
                Assert.Equal(unlinkRequest.OperationKey, item.OperationKey);
                Assert.Equal(1, item.BeforeVersion);
                Assert.Equal(2, item.AfterVersion);
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Corpus")]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task GenuineFormalInstructionLinksTriageInEitherArrivalOrderWithoutChangingItsWorkflow(bool caseFirst)
    {
        var mappingRoot = Path.Combine(QdosCorpus.Root, "qdosmapping");
        var instructionFileName =
            "(EREF10) RTA on 14_08_2026  Mr Paul Larcombe (Our Ref AMA_47857_1, Vehicle PG18 BTY).eml";
        var instructionBytes = await File.ReadAllBytesAsync(
            Path.Combine(mappingRoot, instructionFileName));
        Assert.Equal(
            GenuineFormalInstructionHash,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(instructionBytes)));
        var formalInstruction = new GenuineCorpusSample(
            GenuineFormalInstructionHash,
            instructionFileName,
            "message/rfc822",
            instructionBytes);

        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        Guid? instructionReceiptId = null;
        if (caseFirst)
        {
            instructionReceiptId = IntakeWebDriver.ReceiptId(
                await IntakeWebDriver.UploadAndProcessAsync(factory, client, formalInstruction));
        }

        // Arrange the pre-existing Triage at its real Core creation boundary.
        // Its VRM, principal and source identity come from the same genuine
        // instruction that the production intake path processes below; only
        // the already-open Triage state is test setup.
        var receivedAtUtc = new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        var sourceIdentity = new IntakeSourceIdentity(
            IntakeSourceChannel.Mailbox,
            $"triage-association-arrangement:{Guid.NewGuid():N}");
        var readResult = await new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System)
            .ReadAsync(
                new(
                    instructionFileName,
                    "message/rfc822",
                    instructionBytes,
                    receivedAtUtc,
                    "qdos-triage-association",
                    sourceIdentity),
                CancellationToken.None);
        Assert.Equal(IntakeSourceReadStatus.Readable, readResult.Status);
        var route = new PrincipalMailRoutePolicy().Evaluate(readResult);
        Assert.Equal(MailRouteDisposition.Accepted, route.Disposition);
        var extracted = new QdosInstructionExtractionPolicy().Extract(
            readResult,
            receivedAtUtc,
            new(
                QdosInstructionExtractionPolicy.SupportedPrincipalCode,
                PrincipalMailRoutePolicy.Key,
                PrincipalMailRoutePolicy.Version));
        var draft = Assert.IsType<InstructionDraft>(extracted.InstructionDraft);
        var normalizedVrm = Assert.IsType<string>(draft.VehicleRegistration);
        Assert.Equal(QdosInstructionExtractionPolicy.SupportedPrincipalCode, draft.SuggestedPrincipalCode);
        Assert.Equal(
            CaseType.InspectionAndAudit,
            new PrincipalMailClassificationPolicy("QDOS").Classify(readResult).CaseType);
        var acceptedMatch = new IntakeEvidence(
            IntakeEvidenceSource.SystemDefault,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            normalizedVrm,
            "The pre-existing Triage state is arranged at the production Core boundary.",
            "triage-association-arrangement",
            1);
        Guid triageId;
        long initialVersion;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var receipt = await services.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
                new(
                    instructionFileName,
                    "message/rfc822",
                    instructionBytes.LongLength,
                    GenuineFormalInstructionHash,
                    sourceIdentity,
                    receivedAtUtc,
                    receivedAtUtc,
                    "qdos-triage-association",
                    IntakeDecision.NeedsSorting,
                    "Pre-existing Triage arrangement.",
                    [acceptedMatch],
                    [],
                    draft,
                    [],
                    null,
                    null,
                    readResult.ReaderKey,
                    readResult.ReaderVersion,
                    QdosInstructionExtractionPolicy.Key,
                    QdosInstructionExtractionPolicy.Version),
                CancellationToken.None);
            var evaluationId = await TriageQueuesWebTests.StageAndCompleteEvaluationAsync(
                services,
                receipt.Id);
            var triage = await services.GetRequiredService<ICreateTriageFromIntake>().ExecuteAsync(
                new(
                    new(receipt.Id, sourceIdentity, GenuineFormalInstructionHash, evaluationId),
                    normalizedVrm,
                    acceptedMatch,
                    ActionActor.SystemWorker("test-worker"),
                    $"triage-association-arrangement:{receipt.Id:N}"),
                CancellationToken.None);
            triageId = triage.CaseId;
            initialVersion = triage.Version;
        }

        var initialTriage = await GetTriageAsync(factory.Services, triageId);

        Assert.Equal(TriageState.Open, initialTriage.Record.State);
        Assert.Equal(caseFirst, initialTriage.Record.LinkedInstructionCaseId is not null);
        Assert.Equal(normalizedVrm, initialTriage.Record.NormalizedVehicleRegistration);

        instructionReceiptId ??= IntakeWebDriver.ReceiptId(
            await IntakeWebDriver.UploadAndProcessAsync(factory, client, formalInstruction));

        Guid formalCaseId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
            var instructionReceipt = Assert.IsType<IntakeReceipt>(
                await receipts.GetAsync(instructionReceiptId.Value, CancellationToken.None));

            Assert.Equal(IntakeDecision.CaseCreated, instructionReceipt.Decision);
            Assert.NotNull(instructionReceipt.CurrentCaseId);
            formalCaseId = instructionReceipt.CurrentCaseId.Value;
            Assert.Equal(normalizedVrm, instructionReceipt.InstructionDraft?.VehicleRegistration);
        }

        var remainingTriage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.Open, remainingTriage.Record.State);
        Assert.Equal(formalCaseId, remainingTriage.Record.LinkedInstructionCaseId);
        Assert.Equal(initialVersion + 1, remainingTriage.Record.Version);
        Assert.Equal(initialTriage.Record.Reference, remainingTriage.Record.Reference);
        Assert.Equal(initialTriage.Findings, remainingTriage.Findings);
        Assert.Single(remainingTriage.History, entry => entry.EventType == "triage_case_linked" && entry.ActorKind == nameof(ActorKind.SystemWorker));
        await using var replayScope = factory.Services.CreateAsyncScope();
        var pairing = replayScope.ServiceProvider.GetRequiredService<ITriageCasePairing>();
        await using var acceptedContext = await replayScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await acceptedContext.Cases.CountAsync(item => item.Type != CaseTypeCodes.Triage));
        Assert.Equal(new TriageCasePairingResult(0, 0, 0),
            await pairing.PairTriageAsync(triageId, CancellationToken.None));
        Assert.Equal(new TriageCasePairingResult(0, 0, 0),
            await pairing.PairAcceptedCaseAsync(formalCaseId, CancellationToken.None));
        Assert.DoesNotContain(
            remainingTriage.History,
            entry => entry.EventType == "triage_state_changed"
                || (entry.EventType == "triage_case_linked" && entry.ActorKind != nameof(ActorKind.SystemWorker)));
    }

    private static async Task<CaseEditLease> ClaimCaseLeaseAsync(
        IServiceProvider services,
        Guid caseId,
        long expectedVersion,
        ActionActor actor,
        string operationKey)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new ClaimCaseEditLeaseRequest(
                caseId,
                expectedVersion,
                actor,
                operationKey),
            CancellationToken.None);
    }

    private static async Task<CaseAssociationPersistence> ReadCaseAssociationPersistenceAsync(
        LocalDbTestDatabase database,
        Guid caseId)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        long caseEntityVersion;
        long workflowVersion;
        bool hasActiveLease;
        await using (var versionCommand = connection.CreateCommand())
        {
            versionCommand.CommandText = """
                SELECT c.Version,
                       w.Version,
                       CASE WHEN w.EditLeaseTokenHash IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END
                FROM Cases AS c
                INNER JOIN CaseWorkflows AS w ON w.CaseId = c.Id
                WHERE c.Id = @caseId
                """;
            versionCommand.Parameters.AddWithValue("@caseId", caseId);
            await using var reader = await versionCommand.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            caseEntityVersion = reader.GetInt64(0);
            workflowVersion = reader.GetInt64(1);
            hasActiveLease = reader.GetBoolean(2);
            Assert.False(await reader.ReadAsync());
        }

        var events = new List<CaseAssociationEvent>();
        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.CommandText = """
                SELECT EventType, OperationKey, BeforeVersion, AfterVersion
                FROM CaseWorkflowEvents
                WHERE CaseId = @caseId
                ORDER BY AfterVersion
                """;
            eventCommand.Parameters.AddWithValue("@caseId", caseId);
            await using var reader = await eventCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                events.Add(new(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3)));
            }
        }

        return new(caseEntityVersion, workflowVersion, hasActiveLease, events);
    }

    private sealed record CaseAssociationPersistence(
        long CaseEntityVersion,
        long WorkflowVersion,
        bool HasActiveLease,
        IReadOnlyList<CaseAssociationEvent> Events);

    private sealed record CaseAssociationEvent(
        string EventType,
        string OperationKey,
        long BeforeVersion,
        long AfterVersion);
}
