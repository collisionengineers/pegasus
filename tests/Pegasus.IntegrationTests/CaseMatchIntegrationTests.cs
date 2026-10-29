using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CaseMatchIntegrationTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly FixtureInspectionDate = new(2031, 5, 20);

    [Fact]
    public async Task AcceptanceWritesTheCaseMatchIndexInTheSameTransaction()
    {
        await using var harness = await Harness.CreateAsync();

        var outcome = await harness.AcceptAsync("case-match-accept-1");

        var row = await harness.SingleIndexRowAsync(outcome.Identity.CaseId);
        Assert.Equal("QDOS", row.PrincipalCode);
        Assert.Equal("12345/1", row.DurableClaimToken);
        Assert.Equal("AB12CDE", row.NormalizedVrm);
        Assert.Equal("EXAMPLE", row.NormalizedSurname);
        Assert.Equal("J", row.NormalizedFirstInitial);
        Assert.Equal(new DateOnly(2031, 4, 1), row.IncidentDate);
        Assert.Equal("principal_case_match", row.MatchPolicyKey);
        Assert.Equal(1, row.MatchPolicyVersion);
    }

    /// <summary>
    /// A manual upload has no accepted mail route and no credential binding, so
    /// the Principal is the one the accepting staff member allocated. The case
    /// must still carry it as its work provider, or the case export writes an
    /// empty Work Provider and no index row exists for images to match against.
    /// </summary>
    [Fact]
    public async Task ManualUploadAcceptanceCarriesTheStaffAllocatedPrincipalIntoTheIndex()
    {
        await using var harness = await Harness.CreateAsync();
        var receiptId = await harness.SeedManualUploadReceiptAsync("case-match-manual-upload-1");

        var outcome = await harness.AcceptAsync("case-match-accept-manual-1", receiptId);

        var data = await harness.GetRequiredDataAsync(outcome.Identity.CaseId);
        var provider = data.Principal.PrincipalCode.Current;
        Assert.NotNull(provider);
        Assert.Equal("QDOS", provider.Value);
        Assert.Equal(CaseDataValueKind.Confirmed, provider.Kind);
        Assert.Equal(CaseDataSourceKind.CaseAcceptance, provider.Source.Kind);
        var row = await harness.SingleIndexRowAsync(outcome.Identity.CaseId);
        Assert.Equal("QDOS", row.PrincipalCode);
        Assert.Equal("12345/1", row.DurableClaimToken);
        Assert.Equal("AB12CDE", row.NormalizedVrm);
    }

    [Fact]
    public async Task CandidateQueryFindsTheCaseByEachKeyAndCarriesLifecycleState()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-2");
        var queries = new EfCaseMatchIndex(harness.Factory);

        foreach (var keys in new CaseMatchKeys[]
        {
            new("12345/1", null, null, null, null),
            new(null, "AB12CDE", null, null, null),
            new(null, null, "EXAMPLE", "J", null)
        })
        {
            var candidates = await queries.FindByAnyKeyAsync("QDOS", keys, CancellationToken.None);
            var candidate = Assert.Single(candidates);
            Assert.Equal(outcome.Identity.CaseId, candidate.CaseId);
            Assert.True(Enum.IsDefined(candidate.State));
            Assert.Null(candidate.ReplacementCaseId);
        }

        Assert.Empty(await queries.FindByAnyKeyAsync(
            "QDOS",
            new("99999/9", null, null, null, null),
            CancellationToken.None));
        Assert.Empty(await queries.FindByAnyKeyAsync(
            "PCH",
            new("12345/1", null, null, null, null),
            CancellationToken.None));
    }

    /// <summary>
    /// The incident date is an excluding rule, not a matching rule (operator,
    /// 15 October 2026): the Case's stored date reaches the eliminator through
    /// the real index query, a different date excludes the Case its claim
    /// found, and the query never selects a Case on its date.
    /// </summary>
    [Fact]
    public async Task IncidentDateExcludesAgainstTheSqlIndexButNeverMatches()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("case-match-accept-incident-date")).Identity.CaseId;
        var sut = new EvaluateIntakeCaseMatch(
            [new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy())],
            new EfCaseMatchIndex(harness.Factory));
        async Task<CaseMatchEvaluationResult> EvaluateAsync(string claim, DateOnly? incidentDate) =>
            (await sut.ExecuteDeclaredAsync(
                "QDOS",
                new(claim, null, null, incidentDate),
                CancellationToken.None))!;

        var sameDate = await EvaluateAsync("12345/1", new DateOnly(2031, 4, 1));
        Assert.Equal(CaseMatchOutcome.UniqueMatch, sameDate.Outcome);
        Assert.Equal(caseId, sameDate.MatchedCaseId);

        var otherDate = await EvaluateAsync("12345/1", new DateOnly(2031, 4, 2));
        Assert.Equal(CaseMatchOutcome.NoMatch, otherDate.Outcome);
        var excluded = Assert.Single(otherDate.Candidates);
        Assert.Equal(caseId, excluded.CaseId);
        Assert.Equal([EvaluateIntakeCaseMatch.ClaimReferenceKey], excluded.HitKeys);
        Assert.Single(excluded.Eliminations);

        var noDate = await EvaluateAsync("12345/1", null);
        Assert.Equal(CaseMatchOutcome.UniqueMatch, noDate.Outcome);
        Assert.Equal(caseId, noDate.MatchedCaseId);

        var dateOnlyAgreement = await EvaluateAsync("99999/9", new DateOnly(2031, 4, 1));
        Assert.Equal(CaseMatchOutcome.NoMatch, dateOnlyAgreement.Outcome);
        Assert.Empty(dateOnlyAgreement.Candidates);
    }

    [Fact]
    public async Task StaffSaveUpdatesTheCaseMatchIndexThroughTheSameGrammar()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-3");
        var caseId = outcome.Identity.CaseId;
        var projection = await harness.GetRequiredDataAsync(caseId);

        var lease = await harness.AcquireLeaseAsync(caseId, projection.Version, "case-match-lease-1");
        await harness.WorkspaceStore.SaveAsync(
            new SaveCaseWorkspaceRequest(
                caseId,
                projection.Version,
                harness.StaffActor,
                "case-match-save-1",
                "Registration corrected from the client's V5C",
                lease.Token)
            {
                Vehicle = new("XY65 ZZZ", null, null, null, null)
            },
            CancellationToken.None);

        var row = await harness.SingleIndexRowAsync(caseId);
        Assert.Equal("XY65ZZZ", row.NormalizedVrm);
        Assert.Equal("12345/1", row.DurableClaimToken);
    }

    [Fact]
    public async Task AutomaticAssociationWritesOnceAndReplaysAsNoOp()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-4");
        var caseId = outcome.Identity.CaseId;
        var chaserReceiptId = await harness.SeedAdditionalReceiptAsync("chaser-token-1");
        var store = new EfIntakeMutationStore(harness.Factory);
        var request = new AutomaticCaseAssociationRequest(
            chaserReceiptId,
            caseId,
            "principal_case_match",
            1,
            "system-worker:intake-processing",
            "case-match-association:test-op-1",
            "Automatic association from the recorded case-match decision.");

        var first = await store.AssociateFromMatchAsync(request, StartUtc, CancellationToken.None);
        var replay = await store.AssociateFromMatchAsync(request, StartUtc, CancellationToken.None);
        var alreadyActive = await store.AssociateFromMatchAsync(
            request with { OperationKey = "case-match-association:test-op-2" },
            StartUtc,
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.Associated, first);
        Assert.Equal(AutomaticCaseAssociationOutcome.AlreadyAssociated, replay);
        Assert.Equal(AutomaticCaseAssociationOutcome.AlreadyAssociated, alreadyActive);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var association = Assert.Single(
            await context.IntakeManualAssociations
                .AsNoTracking()
                .Where(item => item.IntakeReceiptId == chaserReceiptId)
                .ToListAsync());
        Assert.True(association.IsActive);
        Assert.Equal(caseId, association.CaseId);
        Assert.Equal(nameof(ActorKind.SystemWorker), association.ActorKind);
        Assert.Equal("system-worker:intake-processing", association.ActorSubjectId);
        Assert.Equal("principal_case_match", association.MatchPolicyKey);
        Assert.Equal(1, association.MatchPolicyVersion);
        Assert.Equal(1, await context.IntakeMutationHistory
            .CountAsync(item => item.IntakeReceiptId == chaserReceiptId
                && item.EventType == "intake_case_linked_automatic"));
    }

    /// <summary>
    /// A retained message whose classification opens a query, linked to a
    /// Report sent Case, opens one query. The actor is the query-criteria
    /// worker, not the association actor. The state stays Report sent.
    /// </summary>
    [Fact]
    public async Task AutomaticLinkOpensAQueryWhenTheMessageOpensQueryAndTheReportIsSent()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-automatic-query");
        var receiptId = await harness.SeedRetainedMailReceiptAsync(
            "automatic-post-report-query",
            registration: null,
            conversationIdentity: "automatic-post-report-query");
        await harness.SetOpensQueryAsync(receiptId);
        await using (var seed = await harness.Factory.CreateDbContextAsync())
        {
            var seededWorkflow = await seed.CaseWorkflows.SingleAsync(item => item.CaseId == outcome.Identity.CaseId);
            seededWorkflow.State = nameof(CaseLifecycleState.ReportSent);
            await seed.SaveChangesAsync();
        }

        var result = await new EfIntakeMutationStore(harness.Factory).AssociateFromMatchAsync(
            new(
                receiptId,
                outcome.Identity.CaseId,
                "principal_case_match",
                1,
                "system-worker:intake-processing",
                "case-match-association:automatic-query",
                "Automatically linked retained post-report query."),
            StartUtc,
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.Associated, result);
        await using var verify = await harness.Factory.CreateDbContextAsync();
        var workflow = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == outcome.Identity.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), workflow.State);
        Assert.True(workflow.HasOpenQuery);
        Assert.Equal(1, workflow.Version);
        var query = Assert.Single(await verify.CaseQueries
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .ToListAsync());
        Assert.Equal(nameof(CaseQuerySource.Mail), query.Source);
        Assert.Equal(receiptId, query.IntakeReceiptId);
        Assert.Null(query.ClosedAtUtc);
        Assert.Equal(nameof(ActorKind.SystemWorker), query.RaisedByKind);
        Assert.Equal("system-worker:query-criteria", query.RaisedBySubjectId);
        var queryEvent = Assert.Single(await verify.CaseWorkflowEvents
            .Where(item => item.CaseId == outcome.Identity.CaseId && item.EventType == "case_query_received")
            .ToListAsync());
        Assert.Equal("system-worker:query-criteria", queryEvent.ActorSubjectId);
        Assert.Equal("Matched query criteria", queryEvent.Reason);
        var history = Assert.Single(await verify.IntakeMutationHistory
            .Where(item => item.IntakeReceiptId == receiptId
                && item.EventType == "intake_case_linked_automatic")
            .ToListAsync());
        Assert.Equal(0, history.BeforeCaseVersion);
        Assert.Equal(1, history.AfterCaseVersion);
    }

    /// <summary>The same link before the report is sent opens no query.</summary>
    [Fact]
    public async Task AutomaticLinkOpensNothingBeforeTheReportIsSent()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-automatic-early-query");
        var receiptId = await harness.SeedRetainedMailReceiptAsync(
            "automatic-early-post-report-query",
            registration: null,
            conversationIdentity: "automatic-early-post-report-query");
        await harness.SetOpensQueryAsync(receiptId);
        await using (var seed = await harness.Factory.CreateDbContextAsync())
        {
            var seededWorkflow = await seed.CaseWorkflows.SingleAsync(item => item.CaseId == outcome.Identity.CaseId);
            seededWorkflow.State = nameof(CaseLifecycleState.ReportPreparation);
            await seed.SaveChangesAsync();
        }

        var result = await new EfIntakeMutationStore(harness.Factory).AssociateFromMatchAsync(
            new(
                receiptId,
                outcome.Identity.CaseId,
                "principal_case_match",
                1,
                "system-worker:intake-processing",
                "case-match-association:automatic-early-query",
                "Automatically linked retained post-report query."),
            StartUtc,
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.Associated, result);
        await using var verify = await harness.Factory.CreateDbContextAsync();
        var workflow = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == outcome.Identity.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportPreparation), workflow.State);
        Assert.False(workflow.HasOpenQuery);
        Assert.Equal(0, workflow.Version);
        Assert.Empty(await verify.CaseQueries
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .ToListAsync());
    }
    [Fact]
    public async Task AutomaticLinkWithoutOpensQueryWritesNoCategoryAndOpensNoQuery()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-state-query");
        var receiptId = await harness.SeedRetainedMailReceiptAsync(
            "state-classified-query",
            registration: null,
            conversationIdentity: "state-classified-query");
        await harness.SetWorkflowStateAsync(outcome.Identity.CaseId, CaseLifecycleState.ReportSent);

        var result = await new EfIntakeMutationStore(harness.Factory).AssociateFromMatchAsync(
            new(
                receiptId,
                outcome.Identity.CaseId,
                "principal_case_match",
                1,
                "system-worker:intake-processing",
                "case-match-association:state-query",
                "Automatic association from the recorded case-match decision."),
            StartUtc,
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.Associated, result);
        await using var verify = await harness.Factory.CreateDbContextAsync();
        var workflow = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == outcome.Identity.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), workflow.State);
        Assert.False(workflow.HasOpenQuery);
        Assert.False(await verify.Set<IntakeMailClassificationDecisionEntity>()
            .AnyAsync(item => item.IntakeReceiptId == receiptId));
        Assert.Empty(await verify.CaseQueries.Where(item => item.IntakeReceiptId == receiptId).ToListAsync());
    }

    [Fact]
    public async Task AutomaticLinkLeavesAClassifiedDecisionAndOpensNoQueryWhenItDoesNot()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-state-keep");
        var receiptId = await harness.SeedRetainedMailReceiptAsync(
            "state-classified-keep",
            registration: null,
            conversationIdentity: "state-classified-keep");
        await harness.SetReceivedClassificationAsync(receiptId, "cancellation", version: 2);
        await harness.SetWorkflowStateAsync(outcome.Identity.CaseId, CaseLifecycleState.ReportSent);

        await new EfIntakeMutationStore(harness.Factory).AssociateFromMatchAsync(
            new(
                receiptId,
                outcome.Identity.CaseId,
                "principal_case_match",
                1,
                "system-worker:intake-processing",
                "case-match-association:state-keep",
                "Automatic association from the recorded case-match decision."),
            StartUtc,
            CancellationToken.None);

        await using var verify = await harness.Factory.CreateDbContextAsync();
        var workflow = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == outcome.Identity.CaseId);
        Assert.False(workflow.HasOpenQuery);
        var decision = await verify.Set<IntakeMailClassificationDecisionEntity>()
            .SingleAsync(item => item.IntakeReceiptId == receiptId);
        Assert.Equal("cancellation", decision.Category);
        Assert.False(decision.OpensQuery);
        Assert.Equal(2, decision.Version);
    }

    /// <summary>
    /// Ordinary automatic association does not wait for an editor to finish. It writes
    /// receipt rows only — no case row, no case version — so there is nothing for a staff edit to lose.
    /// and the yield it used to perform was one-shot, silently costing the association for the
    /// length of any editing session now that a lease is held for as long as the editor is open.
    /// </summary>
    [Fact]
    public async Task AutomaticAssociationWritesWhileAStaffEditLeaseIsLiveAndLeavesTheCaseUntouched()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-lease");
        var caseId = outcome.Identity.CaseId;
        var projection = await harness.GetRequiredDataAsync(caseId);
        var chaserReceiptId = await harness.SeedAdditionalReceiptAsync("chaser-token-lease");
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            projection.Version,
            "case-match-lease-live");

        var store = new EfIntakeMutationStore(harness.Factory);
        var associated = await store.AssociateFromMatchAsync(
            new(
                chaserReceiptId,
                caseId,
                "principal_case_match",
                1,
                "system-worker:intake-processing",
                "case-match-association:while-leased",
                "Automatic association while a staff editor holds the case."),
            StartUtc,
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.Associated, associated);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .SingleAsync(item => item.CaseId == caseId);
        // The editor keeps the case exactly as they loaded it: same version, same lease.
        Assert.Equal(projection.Version, workflow.Version);
        Assert.Equal(lease.Holder, workflow.EditLeaseHolder);
        Assert.Equal(lease.ExpiresAtUtc, workflow.EditLeaseExpiresAtUtc);
        var history = Assert.Single(await context.IntakeMutationHistory
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == chaserReceiptId
                && item.EventType == "intake_case_linked_automatic")
            .ToListAsync());
        Assert.Null(history.ExpectedCaseVersion);
        Assert.Null(history.BeforeCaseVersion);
        Assert.Null(history.AfterCaseVersion);
    }

    [Fact]
    public async Task StaffReversedAssociationIsNeverSilentlyRelinkedByAutomaticMatching()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-5");
        var caseId = outcome.Identity.CaseId;
        var chaserReceiptId = await harness.SeedAdditionalReceiptAsync("chaser-token-2");

        await using (var seed = await harness.Factory.CreateDbContextAsync())
        {
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeManualAssociations (IntakeReceiptId, CaseId, IsActive, Version, LinkedAtUtc, UnlinkedAtUtc, ActorKind, ActorSubjectId, ActorRolesJson, Reason, LastOperationKey, MatchPolicyKey, MatchPolicyVersion) VALUES ({chaserReceiptId}, {caseId}, {false}, {1L}, {StartUtc}, {StartUtc.AddMinutes(5)}, {"Staff"}, {Guid.NewGuid().ToString()}, {"[]"}, {"Staff reversed a mistaken automatic match"}, {"case-match-association:reversed-op"}, {"principal_case_match"}, {1})");
        }

        var store = new EfIntakeMutationStore(harness.Factory);
        var outcomeAfterReversal = await store.AssociateFromMatchAsync(
            new(
                chaserReceiptId,
                caseId,
                "principal_case_match",
                1,
                "system-worker:intake-processing",
                "case-match-association:new-evaluation-op",
                "Automatic association from a later evaluation."),
            StartUtc.AddMinutes(10),
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.AlreadyAssociated, outcomeAfterReversal);
        await using var context = await harness.Factory.CreateDbContextAsync();
        var association = Assert.Single(
            await context.IntakeManualAssociations
                .AsNoTracking()
                .Where(item => item.IntakeReceiptId == chaserReceiptId)
                .ToListAsync());
        Assert.False(association.IsActive);
        Assert.Equal("case-match-association:reversed-op", association.LastOperationKey);
    }

    [Fact]
    public async Task RetainedMailAssociationRejectsStaleEvidenceThenWritesAndReplaysOnce()
    {
        await using var harness = await Harness.CreateAsync();
        var accepted = await harness.AcceptAsync("mail-case-association-accept");
        var receiptId = await harness.SeedRetainedMailReceiptAsync(
            "mail-case-association-token",
            "AB12 CDE",
            "thread-1");
        var store = new EfIntakeMutationStore(harness.Factory);
        var evidence = await store.GetAsync(receiptId, null, CancellationToken.None);
        Assert.Equal(
            [accepted.Identity.CaseId],
            Assert.IsType<AutomaticMailCaseAssociationEvidence>(evidence)
                .RegistrationCandidates
                .Select(candidate => candidate.CaseId));

        await harness.SetReceiptRegistrationAsync(receiptId, "XY99 ZZZ");
        var staleRequest = new AutomaticCaseAssociationRequest(
            receiptId,
            accepted.Identity.CaseId,
            AssociateRetainedMailWithCase.PolicyKey,
            AssociateRetainedMailWithCase.PolicyVersion,
            "system-worker:intake-processing",
            $"mail-case-association:{receiptId:N}",
            "Automatic association from retained mail evidence.",
            evidence.Fingerprint);
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() =>
            store.AssociateFromMatchAsync(staleRequest, StartUtc, CancellationToken.None));

        await harness.SetReceiptRegistrationAsync(receiptId, "AB12 CDE");
        var useCase = new AssociateRetainedMailWithCase(store, store, TimeProvider.System);
        Assert.Equal(
            AutomaticCaseAssociationOutcome.Associated,
            await useCase.ExecuteAsync(receiptId));
        Assert.Equal(
            AutomaticCaseAssociationOutcome.AlreadyAssociated,
            await useCase.ExecuteAsync(receiptId));

        await using var context = await harness.Factory.CreateDbContextAsync();
        var association = Assert.Single(await context.IntakeManualAssociations
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == receiptId)
            .ToListAsync());
        Assert.Equal(accepted.Identity.CaseId, association.CaseId);
        Assert.Equal(AssociateRetainedMailWithCase.PolicyKey, association.MatchPolicyKey);
        Assert.Equal(1, await context.IntakeMutationHistory.CountAsync(item =>
            item.IntakeReceiptId == receiptId
            && item.EventType == "intake_case_linked_automatic"));
        var retainedMessageId = await context.RetainedMailboxMessages
            .Where(item => item.ExternalReceiptToken == "mail-case-association-token")
            .Select(item => item.Id)
            .SingleAsync();
        var retainedDetail = await new EfRetainedMailboxMessageStore(harness.Factory, new MemoryCache(new MemoryCacheOptions()))
            .GetAsync(retainedMessageId, CancellationToken.None);
        Assert.Equal(accepted.Identity.CaseId, retainedDetail?.Summary.CaseId);

        var sameThreadId = await harness.SeedRetainedMailReceiptAsync(
            "same-thread-token",
            null,
            "thread-1");
        var otherMailboxId = await harness.SeedRetainedMailReceiptAsync(
            "other-mailbox-token",
            null,
            "thread-1",
            "mailbox-2");
        Assert.Equal(
            [accepted.Identity.CaseId],
            (await store.GetAsync(sameThreadId, null, CancellationToken.None))!.ThreadCaseIds);
        Assert.Empty((await store.GetAsync(otherMailboxId, null, CancellationToken.None))!.ThreadCaseIds);
    }

    /// <summary>
    /// A reply answers the message it quotes: its In-Reply-To and References
    /// headers reach the Case of the retained message they point at (D13). The
    /// header value carries no angle brackets, while the retained identity was
    /// stored from the transport value with them.
    /// </summary>
    [Fact]
    public async Task ReplyHeadersLinkToTheCaseOfTheRetainedMessageTheyAnswer()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("mail-reply-retained-accept")).Identity.CaseId;
        var store = new EfIntakeMutationStore(harness.Factory);
        var original = await harness.SeedRetainedMailReceiptAsync(
            "reply-retained-original",
            null,
            null,
            messageIdentity: "<original@example.test>");
        await AssociateAsync(store, original, caseId);

        var reply = await harness.SeedRetainedMailReceiptAsync(
            "reply-retained-reply",
            null,
            null,
            messageIdentity: "<reply@example.test>",
            replyIdentities: ["original@example.test"]);

        var evidence = await store.GetAsync(reply, null, CancellationToken.None);
        Assert.Equal([caseId], evidence!.ReplyCaseIds);
        Assert.Equal(
            AutomaticCaseAssociationOutcome.Associated,
            await new AssociateRetainedMailWithCase(store, store, TimeProvider.System).ExecuteAsync(reply));
    }

    /// <summary>
    /// The message a reply answers may be a Pegasus Sent item from another
    /// approved mailbox; its authoritative Case identities answer the reply
    /// headers (D13). The Sent item keeps the transport Message-ID with its
    /// angle brackets, the reply header does not, and both forms are looked for.
    /// </summary>
    [Fact]
    public async Task ReplyHeadersLinkToASentItemInAnotherApprovedMailbox()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("mail-reply-sent-accept")).Identity.CaseId;
        var store = new EfIntakeMutationStore(harness.Factory);
        await harness.SeedSentPollOutcomeAsync("mailbox-2", "<sent-item@example.test>", caseId);

        var reply = await harness.SeedRetainedMailReceiptAsync(
            "reply-sent-item",
            null,
            null,
            "mailbox-1",
            messageIdentity: "<sent-reply@example.test>",
            replyIdentities: ["sent-item@example.test"]);

        var evidence = await store.GetAsync(reply, null, CancellationToken.None);
        Assert.Equal([caseId], evidence!.ReplyCaseIds);
        Assert.Equal(
            AutomaticCaseAssociationOutcome.Associated,
            await new AssociateRetainedMailWithCase(store, store, TimeProvider.System).ExecuteAsync(reply));
    }

    /// <summary>
    /// A reply to the Case's report answers the report's own Message-ID, which
    /// the report-sent evidence records against the Case (D13). The evidence row
    /// keeps the transport Message-ID with its angle brackets, the reply header
    /// does not, and both forms are looked for.
    /// </summary>
    [Fact]
    public async Task ReplyHeadersLinkToTheCaseOfTheReportTheyAnswer()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("mail-reply-report-accept")).Identity.CaseId;
        var store = new EfIntakeMutationStore(harness.Factory);
        await harness.SeedReportSentEvidenceAsync(caseId, "<report-sent@example.test>");

        var reply = await harness.SeedRetainedMailReceiptAsync(
            "reply-report-sent",
            null,
            null,
            messageIdentity: "<report-reply@example.test>",
            replyIdentities: ["report-sent@example.test"]);

        var evidence = await store.GetAsync(reply, null, CancellationToken.None);
        Assert.Equal([caseId], evidence!.ReplyCaseIds);
    }

    /// <summary>
    /// A claim reference the subject or body names links without the Principal's
    /// own label grammar, by matching the Case's indexed durable claim token
    /// across every Principal (D14).
    /// </summary>
    [Theory]
    [InlineData("subject", "Re: Claim 12345/1", null)]
    [InlineData("body", null, "The claim reference is 12345/1.")]
    public async Task ClaimReferenceTokensInTheSubjectOrBodyLinkToTheIndexedCase(
        string where,
        string? subject,
        string? body)
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync($"mail-claim-{where}")).Identity.CaseId;
        var store = new EfIntakeMutationStore(harness.Factory);
        var receipt = await harness.SeedRetainedMailReceiptAsync(
            $"claim-{where}-token",
            null,
            null,
            subject: subject,
            body: body);

        var evidence = await store.GetAsync(receipt, null, CancellationToken.None);
        Assert.Equal([caseId], evidence!.ClaimReferenceCaseIds);
        Assert.Empty(evidence.ThreadCaseIds);
        Assert.Empty(evidence.ReplyCaseIds);
        Assert.Empty(evidence.RegistrationCandidates);
        Assert.Equal(
            AutomaticCaseAssociationOutcome.Associated,
            await new AssociateRetainedMailWithCase(store, store, TimeProvider.System).ExecuteAsync(receipt));
    }

    /// <summary>
    /// A message that names no registration of its own has the one the
    /// photographs inside its PDF read (operator, 28 October 2026, R474): it
    /// links to the Case carrying that registration, and the write re-reads
    /// the evidence with the same read. With no read it links nothing and
    /// waits in No Case Found.
    /// </summary>
    [Fact]
    public async Task APdfPhotographReadLinksAMessageThatNamesNoRegistration()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("mail-photograph-accept")).Identity.CaseId;
        var store = new EfIntakeMutationStore(harness.Factory);
        var receipt = await harness.SeedRetainedMailReceiptAsync("photograph-read-token", null, null);
        var useCase = new AssociateRetainedMailWithCase(store, store, TimeProvider.System);

        var unread = await store.GetAsync(receipt, null, CancellationToken.None);
        Assert.Null(unread!.NormalizedVehicleRegistration);
        Assert.Empty(unread.RegistrationCandidates);
        Assert.Null(await useCase.ExecuteAsync(receipt));

        var evidence = await store.GetAsync(receipt, "AB12CDE", CancellationToken.None);
        Assert.Equal("AB12CDE", evidence!.NormalizedVehicleRegistration);
        Assert.Equal(caseId, Assert.Single(evidence.RegistrationCandidates).CaseId);
        Assert.Equal(
            AutomaticCaseAssociationOutcome.Associated,
            await useCase.ExecuteAsync(receipt, photographRegistration: "AB12CDE"));
    }

    /// <summary>
    /// A registration several Cases share is narrowed by the message's incident
    /// date, and the narrowing belongs to the policy: the evidence carries both
    /// candidates with their own dates, and <c>SelectTarget</c> keeps the Case
    /// carrying the message's date (D15).
    /// </summary>
    [Fact]
    public async Task VehicleRegistrationIsNarrowedByTheMessageIncidentDate()
    {
        await using var harness = await Harness.CreateAsync();
        var earlierCaseId = (await harness.AcceptAsync("mail-vrm-earlier")).Identity.CaseId;
        var laterReceiptId = await harness.SeedManualUploadReceiptAsync("mail-vrm-later-upload");
        var laterCaseId = (await harness.AcceptAsync("mail-vrm-later", laterReceiptId)).Identity.CaseId;
        await harness.SetIndexIncidentDateAsync(laterCaseId, new DateOnly(2031, 5, 1));
        var store = new EfIntakeMutationStore(harness.Factory);

        var receipt = await harness.SeedRetainedMailReceiptAsync(
            "vrm-narrowed-token",
            "AB12 CDE",
            null,
            incidentDate: new DateOnly(2031, 5, 1));

        var evidence = await store.GetAsync(receipt, null, CancellationToken.None);
        Assert.Equal(2, evidence!.RegistrationCandidates.Count);
        Assert.Equal(
            new DateOnly(2031, 4, 1),
            Assert.Single(evidence.RegistrationCandidates, item => item.CaseId == earlierCaseId).IncidentDate);
        Assert.Equal(
            new DateOnly(2031, 5, 1),
            Assert.Single(evidence.RegistrationCandidates, item => item.CaseId == laterCaseId).IncidentDate);

        var target = Assert.IsType<AssociateRetainedMailWithCase.MailAssociationTarget>(
            AssociateRetainedMailWithCase.SelectTarget(evidence, null));
        Assert.Equal(laterCaseId, target.CaseId);
        Assert.Equal(
            AutomaticCaseAssociationOutcome.Associated,
            await new AssociateRetainedMailWithCase(store, store, TimeProvider.System).ExecuteAsync(receipt));
    }

    /// <summary>
    /// An archived Case is dropped from every signal's candidate set before the
    /// Core policy sees it, so a reply chain or claim that points only at an
    /// archived Case links nothing (D16).
    /// </summary>
    [Fact]
    public async Task AnArchivedCaseIsNeverAnAssociationCandidate()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("mail-archived-accept")).Identity.CaseId;
        var store = new EfIntakeMutationStore(harness.Factory);
        var original = await harness.SeedRetainedMailReceiptAsync(
            "archived-original",
            null,
            null,
            messageIdentity: "<archived-original@example.test>");
        await AssociateAsync(store, original, caseId);
        await harness.ArchiveCaseAsync(caseId);

        var reply = await harness.SeedRetainedMailReceiptAsync(
            "archived-reply",
            null,
            null,
            messageIdentity: "<archived-reply@example.test>",
            replyIdentities: ["archived-original@example.test"]);

        var evidence = await store.GetAsync(reply, null, CancellationToken.None);
        Assert.Empty(evidence!.ReplyCaseIds);
        Assert.Null(await new AssociateRetainedMailWithCase(store, store, TimeProvider.System).ExecuteAsync(reply));
    }

    /// <summary>
    /// R469 (operator, 28 October 2026): a reply into a thread that holds a
    /// Triage links to the Triage as a reply links to a Case, whether the
    /// thread's earlier message opened the Triage or stands linked to it. The
    /// reply carries no registration, the shape that once reached the write and
    /// threw because a Triage Case has no workflow row (#1676, R468).
    /// </summary>
    [Theory]
    [InlineData("origin")]
    [InlineData("linked")]
    public async Task ARegistrationlessReplyInATriageThreadLinksToTheTriage(string threadHolds)
    {
        await using var harness = await Harness.CreateAsync();
        var store = new EfIntakeMutationStore(harness.Factory);
        var earlier = await harness.SeedRetainedMailReceiptAsync(
            $"triage-thread-earlier-{threadHolds}",
            null,
            "triage-thread");
        var triageCaseId = await harness.SeedTriageCaseAsync(
            "t.QDOS31901",
            threadHolds == "origin" ? earlier : null);
        if (threadHolds == "linked")
        {
            await harness.LinkReceiptToCaseAsync(earlier, triageCaseId);
        }

        var later = await harness.SeedRetainedMailReceiptAsync(
            $"triage-thread-later-{threadHolds}",
            null,
            "triage-thread");

        var evidence = await store.GetAsync(later, null, CancellationToken.None);
        Assert.Equal([triageCaseId], evidence!.ThreadCaseIds);
        Assert.Equal(
            AutomaticCaseAssociationOutcome.Associated,
            await new AssociateRetainedMailWithCase(store, store, TimeProvider.System).ExecuteAsync(later));

        await using var context = await harness.Factory.CreateDbContextAsync();
        var association = await context.IntakeManualAssociations
            .AsNoTracking()
            .SingleAsync(item => item.IntakeReceiptId == later);
        Assert.True(association.IsActive);
        Assert.Equal(triageCaseId, association.CaseId);
        Assert.Equal(nameof(ActorKind.SystemWorker), association.ActorKind);
    }

    [Fact]
    public async Task CaseMatchDecisionReloadsWithoutLosingAuditEvidence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var matchedCaseId = Guid.NewGuid();
        var decision = new CaseMatchEvaluationResult(
            CaseMatchOutcome.UniqueMatch,
            matchedCaseId,
            null,
            new("12345/1", "AB12CDE", "EXAMPLE", "J", new DateOnly(2026, 6, 18)),
            [
                new(
                    matchedCaseId,
                    ["claim-reference", "vehicle-registration"],
                    [])
            ],
            "Exactly one candidate case survived with no contradictory identity evidence.",
            "principal_case_match",
            1);

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        var stored = await store.StoreAsync(
            new(
                SourceFileName: "case-match-audit.eml",
                MediaType: "message/rfc822",
                SourceLength: 1,
                SourceHash: new string('D', 64),
                SourceIdentity: new(IntakeSourceChannel.Mailbox, "case-match-audit-token"),
                ReceivedAtUtc: StartUtc,
                ProcessedAtUtc: StartUtc,
                Actor: "system-worker:approved-inbox-poller",
                Decision: IntakeDecision.NeedsSorting,
                DecisionReason: "The accepted route did not contain a reviewable instruction.",
                Evidence: [],
                Fields: [],
                InstructionDraft: null,
                MissingFields: [],
                FailureCode: null,
                FailureReason: null,
                SourceReaderKey: "protocol_reader",
                SourceReaderVersion: "1",
                ExtractionPolicyKey: "protocol_policy",
                ExtractionPolicyVersion: 1,
                CaseMatchDecision: decision),
            CancellationToken.None);

        var reloaded = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(stored.Id, CancellationToken.None);
        var audit = Assert.IsType<CaseMatchEvaluationResult>(reloaded?.CaseMatchDecision);
        Assert.Equal(CaseMatchOutcome.UniqueMatch, audit.Outcome);
        Assert.Equal(matchedCaseId, audit.MatchedCaseId);
        Assert.Null(audit.RedirectedFromCaseId);
        Assert.Equal("12345/1", audit.Keys.DurableClaimToken);
        Assert.Equal(new DateOnly(2026, 6, 18), audit.Keys.IncidentDate);
        var candidate = Assert.Single(audit.Candidates);
        Assert.Equal(["claim-reference", "vehicle-registration"], candidate.HitKeys);
        Assert.Equal("principal_case_match", audit.PolicyKey);
        Assert.Equal(1, audit.PolicyVersion);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly LocalDbTestDatabase database;
        private readonly AcquireCaseEditLease acquireLease;
        private readonly AcceptIntake acceptIntake;
        private readonly EfCaseDataStore dataStore;

        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid receiptId,
            ActionActor staffActor,
            EfCaseDataStore dataStore,
            AcceptIntake acceptIntake,
            EfCaseWorkspaceStore workspaceStore,
            AcquireCaseEditLease acquireLease)
        {
            this.database = database;
            Factory = factory;
            ReceiptId = receiptId;
            StaffActor = staffActor;
            this.dataStore = dataStore;
            this.acceptIntake = acceptIntake;
            WorkspaceStore = workspaceStore;
            this.acquireLease = acquireLease;
        }

        public PooledDbContextFactory<PegasusDbContext> Factory { get; }
        public Guid ReceiptId { get; }
        public ActionActor StaffActor { get; }
        public EfCaseWorkspaceStore WorkspaceStore { get; }

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var timeProvider = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(StartUtc);
                var receiptId = Guid.NewGuid();
                var staffActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
                await SeedAsync(factory, receiptId);

                IPrincipalCaseMatchPolicy[] matchPolicies = [new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy())];
                var acceptanceStore = new EfCaseAcceptanceStore(factory, timeProvider, matchPolicies);
                var dataStore = new EfCaseDataStore(factory);
                var workflowStore = new EfCaseWorkflowStore(factory, timeProvider);
                return new(
                    database,
                    factory,
                    receiptId,
                    staffActor,
                    dataStore,
                    new AcceptIntake(
                        acceptanceStore,
                        new FixedConfiguration(),
                        new EfPrincipalInspectionModeStore(factory),
                        new DiscardingCommittedWorkPublisher(),
                        new TriageCasePairing(new EfTriageStore(factory,
                            [new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy())], timeProvider))),
                    new EfCaseWorkspaceStore(factory, timeProvider, matchPolicies),
                    new AcquireCaseEditLease(workflowStore));
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public Task<CaseAcceptanceOutcome> AcceptAsync(
            string operationKey,
            Guid? receiptId = null) =>
            acceptIntake.ExecuteAsync(
                new(
                    receiptId ?? ReceiptId,
                    0,
                    StaffActor,
                    operationKey,
                    CaseType.Inspection,
                    "QDOS",
                    new(true, true),
                    AcceptedInspectionDeadline: FixtureInspectionDate),
                CancellationToken.None);

        public Task<CaseEditLease> AcquireLeaseAsync(
            Guid caseId,
            long version,
            string operationKey) => acquireLease.ExecuteAsync(
            new(caseId, version, StaffActor, operationKey),
            CancellationToken.None);

        public async Task<CaseDataProjection> GetRequiredDataAsync(Guid caseId) =>
            await dataStore.GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None)
            ?? throw new InvalidOperationException("The case-data fixture was not persisted.");

        public async Task<CaseMatchIndexEntity> SingleIndexRowAsync(Guid caseId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.CaseMatchIndex
                .AsNoTracking()
                .SingleAsync(item => item.CaseId == caseId);
        }

        public async Task<Guid> SeedAdditionalReceiptAsync(string externalToken)
        {
            var id = Guid.NewGuid();
            var sourceHash = new string('c', 64);
            var emptyEnvelope = """{"version":1,"data":[]}""";
            await using var context = await Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({id}, {"chaser.eml"}, {"message/rfc822"}, {50L}, {sourceHash}, {"mailbox"}, {externalToken}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {0L}, {"needs_sorting"}, {"Chaser fixture"}, {emptyEnvelope}, {emptyEnvelope}, {emptyEnvelope})");
            return id;
        }

        /// <summary>
        /// The manual-upload shape: the same instruction evidence, but no mail
        /// route decision at all, so the accepted Principal can only be the one
        /// the staff member allocated.
        /// </summary>
        public async Task<Guid> SeedManualUploadReceiptAsync(string externalToken)
        {
            var id = Guid.NewGuid();
            var sourceHash = new string('e', 64);
            var emptyEnvelope = """{"version":1,"data":[]}""";
            await using var context = await Factory.CreateDbContextAsync();
            var fieldsJson = await context.IntakeReceipts
                .Where(item => item.Id == ReceiptId)
                .Select(item => item.FieldsJson)
                .SingleAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({id}, {"qdos-instruction.pdf"}, {"application/pdf"}, {100L}, {sourceHash}, {"manual_upload"}, {externalToken}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"case_created"}, {"Uploaded instruction fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration, DateOfIncident, InspectionAddress, InspectionDate) VALUES ({id}, {"QDOS"}, {"Mrs Jane Example"}, {"ABC/DEF/12345/1"}, {"AB12CDE"}, {new DateOnly(2031, 4, 1)}, {"1 Test Street, London"}, {FixtureInspectionDate})");
            return id;
        }

        public async Task SetOpensQueryAsync(Guid receiptId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var receipt = await context.IntakeReceipts.SingleAsync(item => item.Id == receiptId);
            receipt.MailClassificationDecision = new()
            {
                IntakeReceiptId = receiptId,
                Outcome = "unclassified",
                Category = null,
                OpensQuery = true,
                IsReplyContext = false,
                PredicatesJson = "{\"version\":1,\"data\":[]}",
                Reason = "Matched query criteria.",
                PolicyKey = "principal_mail_classification",
                PolicyVersion = 4,
                DecidedByActor = "test",
                DecidedAtUtc = StartUtc,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            };
            await context.SaveChangesAsync();
        }

        public async Task SetReceivedClassificationAsync(Guid receiptId, string category, int version)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var receipt = await context.IntakeReceipts.SingleAsync(item => item.Id == receiptId);
            receipt.MailClassificationDecision = new()
            {
                IntakeReceiptId = receiptId,
                Outcome = "classified",
                Category = category,
                IsReplyContext = false,
                PredicatesJson = "{\"version\":1,\"data\":[]}",
                Reason = "Test classification.",
                PolicyKey = "test-classification",
                PolicyVersion = 1,
                DecidedByActor = "test",
                DecidedAtUtc = StartUtc,
                Version = version,
                ConcurrencyToken = Guid.NewGuid()
            };
            await context.SaveChangesAsync();
        }

        public async Task SetWorkflowStateAsync(Guid caseId, CaseLifecycleState state)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
            workflow.State = state.ToString();
            await context.SaveChangesAsync();
        }
        public async Task<Guid> SeedRetainedMailReceiptAsync(
            string externalToken,
            string? registration,
            string? conversationIdentity,
            string mailboxId = "mailbox-1",
            DateOnly? incidentDate = null,
            string? messageIdentity = null,
            string[]? replyIdentities = null,
            string? subject = null,
            string? body = null)
        {
            var id = await SeedAdditionalReceiptAsync(externalToken);
            var messageId = Guid.NewGuid();
            var sourceHash = new string('d', 64);
            await using var context = await Factory.CreateDbContextAsync();
            if (registration is not null || incidentDate is not null)
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO InstructionDrafts (IntakeReceiptId, VehicleRegistration, DateOfIncident) VALUES ({id}, {registration}, {incidentDate})");
            }
            var approvedMailboxId = await TestMailboxId.EnsureApprovedAsync(
                context,
                mailboxId,
                $"{mailboxId}@example.test",
                StartUtc);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"IF NOT EXISTS (SELECT 1 FROM ApprovedInboxPollStates WHERE ApprovedMailboxId = {approvedMailboxId}) INSERT INTO ApprovedInboxPollStates (ApprovedMailboxId, MailboxAddress, ScopeFingerprint, ActivatedAtUtc, DueAtUtc) VALUES ({approvedMailboxId}, {$"{mailboxId}@example.test"}, {new string('0', 64)}, {StartUtc}, {StartUtc})");
            var canonicalIdentity = messageIdentity is null
                ? null
                : MailboxMessageIdentity.CanonicalizeInternetMessageIdentity(messageIdentity);
            var replyIdentitiesJson = replyIdentities is null
                ? null
                : JsonSerializer.Serialize(replyIdentities
                    .Select(MailboxMessageIdentity.CanonicalizeInternetMessageIdentity)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO RetainedMailboxMessages (Id, MailboxId, MailboxAddress, FolderScope, FolderIdentity, ImmutableMessageId, ConversationIdentity, InternetMessageIdentity, CanonicalInternetMessageIdentity, InReplyToIdentitiesJson, ExternalReceiptToken, ToAddressesJson, CcAddressesJson, Subject, BodyPlainText, SourceLength, SourceSha256, ReceivedAtUtc, RetainedAtUtc, IsRead) VALUES ({messageId}, {approvedMailboxId}, {"intake@example.test"}, {"inbox"}, {"inbox-folder"}, {$"immutable-{messageId:N}"}, {conversationIdentity}, {messageIdentity}, {canonicalIdentity}, {replyIdentitiesJson}, {externalToken}, {"[]"}, {"[]"}, {subject}, {body}, {50L}, {sourceHash}, {StartUtc}, {StartUtc}, {false})");
            return id;
        }

        /// <summary>
        /// A Pegasus Sent item discovered in another approved mailbox's Sent
        /// folder, carrying the Case its send was recorded against.
        /// </summary>
        public async Task SeedSentPollOutcomeAsync(
            string mailboxId,
            string internetMessageIdentity,
            Guid caseId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"IF NOT EXISTS (SELECT 1 FROM ApprovedSentPollStates WHERE MailboxId = {mailboxId}) INSERT INTO ApprovedSentPollStates (MailboxId, MailboxAddress, SentFolderIdentity, DueAtUtc) VALUES ({mailboxId}, {$"{mailboxId}@example.test"}, {$"sent-folder:{mailboxId}"}, {StartUtc})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO ApprovedSentPollOutcomes (Id, MailboxId, MailboxAddress, SourceOccurrenceIdentity, SourceSha256, ObservationKind, InternetMessageIdentity, AuthoritativeCaseIdentitiesJson, OutcomeKind, RecordedAtUtc, CursorAfterItem, OperationKey) VALUES ({Guid.NewGuid()}, {mailboxId}, {$"{mailboxId}@example.test"}, {$"occurrence:{Guid.NewGuid():N}"}, {new string('d', 64)}, {"Discovered"}, {internetMessageIdentity}, {JsonSerializer.Serialize(new[] { caseId })}, {"Ambiguous"}, {StartUtc}, {$"cursor:{Guid.NewGuid():N}"}, {$"outcome:{Guid.NewGuid():N}"})");
        }

        public async Task SeedReportSentEvidenceAsync(Guid caseId, string internetMessageIdentity)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var evidenceId = Guid.NewGuid();
            context.CaseReportSentEvidence.Add(new CaseReportSentEvidenceEntity
            {
                Id = evidenceId,
                CaseId = caseId,
                MailboxIdentity = "reports@example.test",
                SentFolderIdentity = "sent-items",
                ImmutableItemIdentity = $"report-item-{evidenceId:N}",
                InternetMessageIdentity = internetMessageIdentity,
                ConversationIdentity = $"report-conversation-{evidenceId:N}",
                ReplyChainIdentity = $"report-reply-chain-{evidenceId:N}",
                SourceOccurrenceIdentity = $"report-occurrence-{evidenceId:N}",
                SourceSha256 = new string('d', 64),
                MimeSha256 = new string('e', 64),
                SentAtUtc = StartUtc,
                DiscoveredAtUtc = StartUtc,
                DiscoveredByKind = nameof(ActorKind.SystemWorker),
                DiscoveredBySubjectId = "approved-mailbox-evidence-ingestion",
                RetentionOperationKey = $"retain-report-{evidenceId:N}",
                RetentionRequestHash = new string('f', 64)
            });
            await context.SaveChangesAsync();
        }

        public async Task SetIndexIncidentDateAsync(Guid caseId, DateOnly incidentDate)
        {
            await using var context = await Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseMatchIndex SET IncidentDate = {incidentDate} WHERE CaseId = {caseId}");
        }

        public async Task ArchiveCaseAsync(Guid caseId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET ArchivedAtUtc = {StartUtc}, ArchivedByKind = {"Staff"}, ArchivedBySubjectId = {"test-staff"}, ArchivedByRolesJson = {"[]"}, ArchiveReason = {"Test archive."} WHERE CaseId = {caseId}");
        }

        /// <summary>
        /// A Triage Case as intake writes it: a Case row of the seeded QDOS
        /// Principal with a <c>t.</c> reference and its open Triage row, and no
        /// CaseWorkflows row. With an origin receipt it is the Triage that
        /// receipt opened; without one, a Triage staff created directly.
        /// </summary>
        public async Task<Guid> SeedTriageCaseAsync(string reference, Guid? originReceiptId = null)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var principal = await SeededPrincipals.QdosAsync(context);
            var origin = originReceiptId is { } receiptId
                ? await context.IntakeReceipts
                    .Where(item => item.Id == receiptId)
                    .Select(item => new { item.SourceChannel, item.ExternalReceiptToken, item.SourceHash })
                    .SingleAsync()
                : null;
            var caseId = Guid.NewGuid();
            context.Cases.Add(new CaseEntity
            {
                Id = caseId,
                PrincipalId = principal.Id,
                SequenceLineageId = principal.SequenceLineageId,
                Year = StartUtc.Year,
                Sequence = 1 + (await context.Cases
                    .Where(item => item.SequenceLineageId == principal.SequenceLineageId
                        && item.Year == StartUtc.Year)
                    .MaxAsync(item => (int?)item.Sequence) ?? 0),
                Reference = reference,
                Type = CaseTypeCodes.Triage,
                InitialState = null,
                CustodyState = "pending",
                OriginIntakeReceiptId = originReceiptId,
                CreatedAtUtc = StartUtc,
                Version = 0,
                ConcurrencyToken = Guid.NewGuid()
            });
            context.Triage.Add(new TriageEntity
            {
                CaseId = caseId,
                OriginReceiptId = originReceiptId,
                SourceChannel = origin?.SourceChannel,
                ExternalReceiptToken = origin?.ExternalReceiptToken,
                SourceHash = origin?.SourceHash,
                EvaluationRevisionId = origin is null ? null : Guid.NewGuid(),
                NormalizedVehicleRegistration = "AB12CDE",
                State = "open",
                CreatedAtUtc = StartUtc,
                CreationOperationKey = $"triage:{reference}",
                Version = 0,
                ConcurrencyToken = Guid.NewGuid()
            });
            await context.SaveChangesAsync();
            return caseId;
        }

        /// <summary>A staff link from a receipt to a Case, as the Link to Case confirmation writes it.</summary>
        public async Task LinkReceiptToCaseAsync(Guid receiptId, Guid caseId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            context.IntakeManualAssociations.Add(new()
            {
                IntakeReceiptId = receiptId,
                CaseId = caseId,
                IsActive = true,
                Version = 1,
                LinkedAtUtc = StartUtc,
                ActorKind = nameof(ActorKind.Staff),
                ActorSubjectId = StaffActor.SubjectId,
                ActorRolesJson = "[\"User\"]",
                Reason = "Fixture thread association.",
                LastOperationKey = $"fixture-link:{Guid.NewGuid():N}"
            });
            await context.SaveChangesAsync();
        }

        public async Task SetReceiptRegistrationAsync(Guid receiptId, string registration)
        {
            await using var context = await Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InstructionDrafts SET VehicleRegistration = {registration} WHERE IntakeReceiptId = {receiptId}");
        }

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        private static async Task SeedAsync(
            IDbContextFactory<PegasusDbContext> factory,
            Guid receiptId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var principal = await SeededPrincipals.QdosAsync(context);
            var organizationId = principal.OrganizationId;
            var lineageId = principal.SequenceLineageId;
            var principalId = principal.Id;
            var sourceHash = new string('b', 64);
            var fieldsJson =
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Mrs Jane Example","candidates":[{"value":"Mrs Jane Example","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"ABC/DEF/12345/1","candidates":[{"value":"ABC/DEF/12345/1","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Date of incident","suggestedValue":"2031-04-01","candidates":[{"value":"2031-04-01","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection address","suggestedValue":"1 Test Street, London","candidates":[{"value":"1 Test Street, London","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection date","suggestedValue":"2031-05-20","candidates":[{"value":"2031-05-20","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false}]}""";
            var emptyEnvelope = """{"version":1,"data":[]}""";

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"qdos.eml"}, {"message/rfc822"}, {100L}, {sourceHash}, {"mailbox"}, {"case-match-item-1"}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"case_created"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration, DateOfIncident, InspectionAddress, InspectionDate) VALUES ({receiptId}, {"QDOS"}, {"Mrs Jane Example"}, {"ABC/DEF/12345/1"}, {"AB12CDE"}, {new DateOnly(2031, 4, 1)}, {"1 Test Street, London"}, {FixtureInspectionDate})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeMailRouteDecisions (IntakeReceiptId, Disposition, RouteOwnerCode, RouteKind, PrincipalCode, PredicatesJson, Reason, PolicyKey, PolicyVersion, TransportIdentitiesJson, OriginalIdentitiesJson) VALUES ({receiptId}, {"accepted"}, {"QDOS"}, {"direct_principal"}, {"QDOS"}, {emptyEnvelope}, {"Accepted QDOS route"}, {"qdos_mail_route"}, {3}, {emptyEnvelope}, {emptyEnvelope})");
        }
    }

    private static Task<AutomaticCaseAssociationOutcome> AssociateAsync(
        EfIntakeMutationStore store,
        Guid receiptId,
        Guid caseId) =>
        store.AssociateFromMatchAsync(
            new AutomaticCaseAssociationRequest(
                receiptId,
                caseId,
                AssociateRetainedMailWithCase.PolicyKey,
                AssociateRetainedMailWithCase.PolicyVersion,
                "system-worker:intake-processing",
                $"seed-association:{receiptId:N}",
                "Seeded association for the test.",
                null),
            StartUtc,
            CancellationToken.None);

    private sealed class FixedConfiguration : ICaseWorkflowConfiguration
    {
        private static readonly CaseWorkflowConfiguration Configuration = new(
            "case-workflow",
            1);

        public Task<CaseWorkflowConfiguration> GetCurrentAsync(
            CancellationToken cancellationToken) => Task.FromResult(Configuration);
    }
}
