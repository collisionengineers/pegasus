using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The operator's Stage 0 rule, end to end through the real intake pipeline —
/// no stub extraction policy, no injected evidence. A QDOS Triage request opens
/// a Triage when a vehicle registration is known and, when it is not, waits in
/// the Inbox's No Case Found scope — an e-mail never becomes an Unidentified
/// item (operator, 13 October 2026); neither outcome allocates a formal Case/PO.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class TriageFromIntakeIntegrationTests
{
    [Fact]
    public async Task PendingTriageSelectionSkipsUnknownAndContradictoryOriginsWithoutPermanentlyExcludingThem()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-recovery-order.eml");
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queries = services.GetRequiredService<IIntakeReceiptQueries>();
        var original = Assert.IsType<IntakeReceipt>(await queries.GetAsync(receiptId, CancellationToken.None));
        Assert.Single(await services.GetRequiredService<ITriageQueries>().ListAsync(null, CancellationToken.None));
        var store = services.GetRequiredService<ITriageStore>();
        var copies = new List<TriageRecord>();
        // Persisted-state probes reuse the original's evidence and typed values;
        // these are distinct test occurrences, not invented inbound messages.
        for (var index = 0; index < 2; index++)
        {
            var identity = new IntakeSourceIdentity(original.SourceIdentity.Channel, $"triage-recovery-copy:{index}");
            var copy = await services.GetRequiredService<IIntakeReceiptStore>().StoreAsync(new(
                original.SourceFileName, original.MediaType, original.SourceLength, original.SourceHash, identity,
                original.ReceivedAtUtc, original.ProcessedAtUtc, "triage-recovery-fixture", original.Decision,
                original.DecisionReason, original.Evidence, original.Fields, original.InstructionDraft,
                original.MissingFields, original.FailureCode, original.FailureReason, original.SourceReaderKey,
                original.SourceReaderVersion, original.ExtractionPolicyKey, original.ExtractionPolicyVersion), CancellationToken.None);
            var evaluation = await TriageQueuesWebTests.StageAndCompleteEvaluationAsync(services, copy.Id);
            copies.Add(await store.CreateAsync(new(new(copy.Id, identity, copy.SourceHash, evaluation),
                "VO75DFJ", Assert.Single(copy.Evidence, item => item.Finding == IntakeEvidenceFinding.AcceptedTriageMatch),
                ActionActor.SystemWorker("triage-recovery-fixture"), $"triage-recovery-create:{index}"), CancellationToken.None));
        }
        var caseId = await QdosTriageIntegrationTests.SeedMatchingFormalCaseAsync(services, copies[1].Origin!.ReceiptId);
        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        // A Triage Case always has its Principal; what can be unknown is the
        // origin's own Principal statement, which the match reads again.
        var firstReceiptId = receiptId;
        var originalPrincipalCode = (await context.InstructionDrafts.AsNoTracking()
            .SingleAsync(item => item.IntakeReceiptId == firstReceiptId)).SuggestedPrincipalCode;
        await context.InstructionDrafts.Where(item => item.IntakeReceiptId == firstReceiptId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.SuggestedPrincipalCode, (string?)null));
        await context.InstructionDrafts.Where(item => item.IntakeReceiptId == copies[0].Origin!.ReceiptId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.VehicleRegistration, "PG18BTY"));
        var eligible = Assert.Single(await store.ListAutomaticLinkCandidatesAsync(null, null, 1, CancellationToken.None));
        Assert.Equal(copies[1].CaseId, eligible.CaseId);
        Assert.Equal(caseId, eligible.InstructionCaseId);
        var pairing = services.GetRequiredService<ITriageCasePairing>();
        Assert.Equal(new TriageCasePairingResult(1, 1, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
        Assert.Equal(new TriageCasePairingResult(0, 0, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
        await context.InstructionDrafts.Where(item => item.IntakeReceiptId == firstReceiptId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.SuggestedPrincipalCode, originalPrincipalCode));
        Assert.Equal(new TriageCasePairingResult(1, 1, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
        Assert.Null((await context.Triage.AsNoTracking().SingleAsync(item => item.CaseId == copies[0].CaseId)).LinkedInstructionCaseId);
        // One instructed Case; the three Triage Cases are Cases too.
        Assert.Equal(1, await context.Cases.CountAsync(item => item.Type != CaseTypeCodes.Triage));
        Assert.Equal(3, await context.Cases.CountAsync(item => item.Type == CaseTypeCodes.Triage));
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ASubjectTemplateTriageRequestOpensATriageAndNoUnidentifiedItem()
    {
        // The subject template states the registration nowhere but the
        // subject; this is the exact shape of the message the operator
        // forwarded and watched disappear.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("engineer-triage.eml");

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(
            await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None));

        Assert.NotEqual(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.Null(receipt.CurrentCaseId);
        Assert.Equal("VO75DFJ", receipt.InstructionDraft?.VehicleRegistration);

        var triage = Assert.Single(
            await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
        var detail = Assert.IsType<TriageDetail>(
            await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .GetAsync(triage.CaseId, CancellationToken.None));

        Assert.Equal(receiptId, detail.Record.Origin?.ReceiptId);
        Assert.Equal("VO75DFJ", detail.Record.NormalizedVehicleRegistration);
        Assert.Equal(TriageState.Open, detail.Record.State);
        var created = Assert.Single(detail.History, item => item.EventType == "triage_created");
        Assert.Contains(
            PrincipalMailClassificationPolicy.Key,
            created.Reason,
            StringComparison.Ordinal);
        // The intake pipeline opened this Triage, so the history says so in the
        // one place that can be trusted: the recorded actor kind, not a prefix
        // read back out of the subject.
        Assert.Equal(nameof(ActorKind.SystemWorker), created.ActorKind);
        Assert.Equal("intake-processing", created.Actor);

        // The registration is known, so this is not Unidentified material.
        Assert.Null(
            await scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
                .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None));
    }

    /// <summary>
    /// R195 (operator, 22 October 2026): a reply into a thread that already
    /// holds a Triage, or a message linked to a Case, opens no second Triage;
    /// nor does a request in reply form that intake already linked to a Case
    /// outside its thread. A reply into a thread that holds a Triage links to
    /// the Triage as a reply links to a Case (operator, 28 October 2026;
    /// R469). The shape a reply actually arrives in is "FW: RE: ...": every
    /// QDOS message reaches us as a staff forward.
    /// </summary>
    [Theory]
    [InlineData("origin")]
    [InlineData("linked")]
    [InlineData("matched")]
    public async Task AForwardedReplyOnATriageThreadOpensNoSecondTriage(string threadHolds)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var conversation = $"conversation-{threadHolds}";
        Guid? matchedCaseId = null;
        Guid? threadTriageCaseId = null;
        if (threadHolds == "matched")
        {
            // An instructed Case for the reply's registration and claim
            // reference, outside its conversation: intake links the reply to
            // it before the Triage is considered.
            matchedCaseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
                factory, client, "VO75DFJ", "46246/1");
        }
        else
        {
            var firstToken = Guid.NewGuid().ToString("N");
            await RetainInConversationAsync(factory, firstToken, "Engineer Triage", conversation);
            var first = threadHolds == "origin"
                ? IntakeTestEvidence.CreateEngineerTriageRequest("engineer-triage.eml")
                : IntakeTestEvidence.CreateEmail(
                    "thread-correspondence.eml", "Further to our call this morning, please see the vehicle details below.");
            var firstReceiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, first, firstToken);
            if (threadHolds == "linked")
            {
                var caseId = await MailboxIntakeTestData.SeedTriageCaseAsync(factory.Services, null, "t.QDOS31900");
                await MailboxIntakeTestData.LinkReceiptAsync(factory.Services, firstReceiptId, caseId);
                threadTriageCaseId = caseId;
            }
            else
            {
                await using var originScope = factory.Services.CreateAsyncScope();
                threadTriageCaseId = (await originScope.ServiceProvider.GetRequiredService<ITriageQueries>()
                    .GetByOriginReceiptAsync(firstReceiptId, CancellationToken.None))?.CaseId;
                Assert.NotNull(threadTriageCaseId);
            }
        }

        var replyToken = Guid.NewGuid().ToString("N");
        const string replySubject = "FW: RE: Engineer Triage - Our Claim Reference : 46246/1 - "
            + "Vehicle Registration : VO75DFJ";
        await RetainInConversationAsync(factory, replyToken, replySubject, conversation);
        var reply = IntakeTestEvidence.CreateEmail(
            "engineer-triage-reply.eml",
            "Thanks -- confirming the vehicle is roadworthy as discussed.",
            subject: replySubject);
        var replyReceiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, reply, replyToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(
            await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(replyReceiptId, CancellationToken.None));
        Assert.NotEqual(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.Single(receipt.Evidence, item => item.Finding == IntakeEvidenceFinding.AcceptedTriageMatch);
        Assert.Equal(matchedCaseId ?? threadTriageCaseId, receipt.CurrentCaseId);
        Assert.Null(
            await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .GetByOriginReceiptAsync(replyReceiptId, CancellationToken.None));
    }

    /// <summary>
    /// R195 (operator, 22 October 2026): a fresh request in reply form, in a
    /// thread that holds no Case or Triage, opens the Triage and links the
    /// message, so it leaves No Case Found; a redelivery replays it (#1482).
    /// </summary>
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task AFreshTriageRequestInReplyFormOpensItsTriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        const string subject = "RE: (EREF19) RTA on 06/09/2026 : Mr Andrew Lennox "
            + "(Our Ref: SCL//48672/1, Vehicle: SC10TCJ)";
        var email = IntakeTestEvidence.CreateEmail(
            "triage-request-reply-form.eml",
            "Our Client:  Mr Andrew Lennox\r\n"
            + "Registration:  SC10TCJ\r\n"
            + "Date of Accident: 6 September 2026\r\n\r\n"
            + "Triage Only Request\r\n\r\n"
            + "Please find attached our client's images.",
            subject: subject);
        var token = Guid.NewGuid().ToString("N");
        await RetainInConversationAsync(factory, token, subject, "conversation-fresh");

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email, token);
        Assert.Equal(receiptId, await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email, token));

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(
            await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None));
        Assert.True(receipt.MailClassificationDecision?.IsReplyContext);
        var triage = Assert.Single(
            await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
        Assert.Equal(triage.CaseId, (await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None))?.CaseId);
        Assert.Equal("SC10TCJ", triage.NormalizedVehicleRegistration);
        Assert.StartsWith("t.QDOS", triage.Reference, StringComparison.Ordinal);

        // It leaves No Case Found: the message reads the Triage it opened,
        // the Case that scope reads too (RetainedMailPersistenceTests
        // NoCaseFoundListsReceivedMailWhoseReceiptStandsLinkedToNoCase).
        var incoming = await scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>().ListAsync(
            new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None);
        Assert.Equal(triage.CaseId, Assert.Single(incoming.Items, item => item.IntakeReceiptId == receiptId).CaseId);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ABodyTemplateTriageRequestOpensATriageFromTheLettersRegistration()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-only-request.eml",
            "Our Client:  Miss Nicola Granger\r\n"
            + "Our Client's Vehicle: MERCEDES-BENZ E250 CDI AMG LINE AUTO\r\n"
            + "Registration:  VN64WNG\r\n"
            + "Date of Accident: 30 June 2026\r\n\r\n"
            + "Triage Only Request\r\n\r\n"
            + "Please find attached our client's images.");

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

        await using var scope = factory.Services.CreateAsyncScope();
        var triage = Assert.Single(
            await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));

        Assert.Equal("VN64WNG", triage.NormalizedVehicleRegistration);
        Assert.Null(
            await scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
                .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ATriageRequestWithNoRegistrationWaitsWithNoCaseAndOpensNoTriage()
    {
        // The operator's other branch: keep it "until a vehicle registration
        // is known, then open the Triage". Only images become Unidentified
        // (operator, 13 October 2026), so the e-mail waits with no Case.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-without-registration.eml",
            "Triage Only Request\r\n\r\nPlease find attached our client's images.");

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(
            await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None));

        Assert.Null(receipt.InstructionDraft?.VehicleRegistration);
        Assert.NotEqual(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.Empty(
            await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));

        Assert.True(ProcessIntake.IsTriageRequest(receipt));
        Assert.Null(receipt.CurrentCaseId);
        Assert.Null(
            await scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
                .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None));
    }

    [Fact]
    public async Task StaffCorrectingAMessageToTriageRequestOpensTheTriageFromTheMessage()
    {
        // FRD-03's third way, from the message itself: the route read nothing
        // in this message, staff correct it to Triage request, and the message
        // record then offers Open the Triage against the same receipt.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "corrected-to-triage.eml",
            "Further to our call this morning, please see the vehicle details below.");

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

        var messageId = await RetainAsThePollWouldAsync(factory, receiptId, "corrected-to-triage", "Further to our call");
        await using (var lookup = factory.Services.CreateAsyncScope())
        {
            Assert.Empty(await lookup.ServiceProvider.GetRequiredService<ITriageQueries>().ListAsync(null, CancellationToken.None));
        }

        // Before the correction the message offers nothing.
        using var beforeResponse = await client.GetAsync($"/Inbox/{messageId:D}");
        if (beforeResponse.StatusCode != HttpStatusCode.OK)
        {
            await using var probe = factory.Services.CreateAsyncScope();
            var probed = await probe.ServiceProvider.GetRequiredService<GetRetainedMail>().ExecuteAsync(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), messageId, null, CancellationToken.None);
            Assert.Fail($"Message page {beforeResponse.StatusCode}; detail {(probed is null ? "null" : "present, folder " + probed.Folder + ", receipt " + probed.Summary.IntakeReceiptId)}");
        }
        var before = await beforeResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("data-offered-action", before, StringComparison.Ordinal);

        using var correction = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["ExpectedClassificationVersion"] = "1",
            ["ClassificationKey"] = "triage-request",
            ["CorrectionReason"] = "The Principal asked for a Triage on the call."
        });
        using var corrected = await client.PostAsync($"/Inbox/{messageId:D}?handler=CorrectClassification", correction);
        Assert.Equal(HttpStatusCode.Redirect, corrected.StatusCode);

        var offered = await IntakeWebDriver.GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.Contains("data-offered-action=\"OpenTriage\"", offered, StringComparison.Ordinal);
        Assert.Contains("handler=OpenTriage", offered, StringComparison.Ordinal);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["vehicleRegistration"] = "vn64 wng",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });
        using var opened = await client.PostAsync($"/Inbox/{messageId:D}?handler=OpenTriage", form);
        Assert.Equal(HttpStatusCode.Redirect, opened.StatusCode);

        await using var after = factory.Services.CreateAsyncScope();
        var triage = Assert.Single(
            await after.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
        var detail = Assert.IsType<TriageDetail>(
            await after.ServiceProvider.GetRequiredService<ITriageQueries>()
                .GetAsync(triage.CaseId, CancellationToken.None));
        Assert.Equal(receiptId, detail.Record.Origin?.ReceiptId);
        Assert.Equal("VN64WNG", detail.Record.NormalizedVehicleRegistration);

        // Opened once: the offer is gone and the Case tab names the Triage.
        using var settledResponse = await client.GetAsync($"/Inbox/{messageId:D}");
        if (settledResponse.StatusCode != HttpStatusCode.OK)
        {
            // Surface the detail query's own refusal, which the page turns into 404.
            await after.ServiceProvider.GetRequiredService<GetRetainedMail>().ExecuteAsync(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), messageId, null, CancellationToken.None);
        }
        Assert.Equal(HttpStatusCode.OK, settledResponse.StatusCode);
        var settled = await settledResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("data-offered-action", settled, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task StaffSupplyingTheRegistrationOnTheMessageOpensTheTriage()
    {
        // The rest of the operator's sentence: a Triage request that never
        // carried a readable registration waits "until a vehicle registration
        // is known, then open the Triage" — and the only thing that can know
        // it here is a member of staff, on the message itself.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-registration-supplied.eml",
            "Triage Only Request\r\n\r\nPlease find attached our client's images.");

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        var messageId = await RetainAsThePollWouldAsync(factory, receiptId, "registration-supplied", "Triage Only Request");

        // Stranded exactly as the branch above leaves it.
        await using (var before = factory.Services.CreateAsyncScope())
        {
            Assert.Empty(
                await before.ServiceProvider.GetRequiredService<ITriageQueries>()
                    .ListAsync(null, CancellationToken.None));
        }

        var offered = await IntakeWebDriver.GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.Contains("data-offered-action=\"OpenTriage\"", offered, StringComparison.Ordinal);
        Assert.Contains("handler=OpenTriage", offered, StringComparison.Ordinal);

        // Typed the way a person types it: lower case, with the separator.
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["vehicleRegistration"] = "vn64 wng",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });
        using var opened = await client.PostAsync($"/Inbox/{messageId:D}?handler=OpenTriage", form);
        Assert.Equal(HttpStatusCode.Redirect, opened.StatusCode);

        await using var after = factory.Services.CreateAsyncScope();
        var triage = Assert.Single(
            await after.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
        var detail = Assert.IsType<TriageDetail>(
            await after.ServiceProvider.GetRequiredService<ITriageQueries>()
                .GetAsync(triage.CaseId, CancellationToken.None));

        Assert.Equal(receiptId, detail.Record.Origin?.ReceiptId);
        Assert.Equal("VN64WNG", detail.Record.NormalizedVehicleRegistration);
        Assert.Equal(TriageState.Open, detail.Record.State);
        Assert.Null(
            await after.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
                .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None));

        // The receipt now has its destination, so the action is not offered a
        // second time and no second Triage can be opened from the screen.
        var settled = await IntakeWebDriver.GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.DoesNotContain("handler=OpenTriage", settled, StringComparison.Ordinal);
    }

    /// <summary>
    /// Processing here does not run the mailbox poll, which is what retains
    /// the message record staff open; retain it as the poll would.
    /// </summary>
    internal static async Task<Guid> RetainAsThePollWouldAsync(
        IntakeWebApplicationFactory factory,
        Guid receiptId,
        string key,
        string subject)
    {
        await using var lookup = factory.Services.CreateAsyncScope();
        var contextFactory = lookup.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var token = await context.IntakeReceipts.Where(item => item.Id == receiptId).Select(item => item.ExternalReceiptToken).SingleAsync();
        var retained = await context.RetainedMailboxMessages
            .Where(item => item.ExternalReceiptToken == token)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync();
        retained ??= await AddRetainedMessageAsync(context, key, token, $"{key}-{receiptId:N}", subject, null);
        // Where the route read nothing in the message, processing recorded no
        // classification. The record staff correct is the abstention the
        // classifier would have recorded for it.
        if (!await context.IntakeMailClassificationDecisions.AnyAsync(item => item.IntakeReceiptId == receiptId))
        {
            context.IntakeMailClassificationDecisions.Add(new IntakeMailClassificationDecisionEntity
            {
                IntakeReceiptId = receiptId,
                Outcome = "unclassified",
                IsReplyContext = false,
                PredicatesJson = "{\"version\":1,\"data\":[]}",
                Reason = "No supported category matched.",
                PolicyKey = "principal_mail_classification",
                PolicyVersion = 2,
                DecidedByActor = "system-worker:intake-processing",
                DecidedAtUtc = DateTimeOffset.UtcNow,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
            await context.SaveChangesAsync();
        }
        return retained.Value;
    }

    /// <summary>
    /// The poll retains a message, with its mailbox conversation, before it
    /// stages the source; retain it so before processing, under the token the
    /// submission then uses.
    /// </summary>
    private static async Task RetainInConversationAsync(
        IntakeWebApplicationFactory factory,
        string token,
        string subject,
        string conversationIdentity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        _ = await AddRetainedMessageAsync(context, "thread", token, $"thread-{token}", subject, conversationIdentity);
    }

    private static async Task<Guid> AddRetainedMessageAsync(
        PegasusDbContext context,
        string key,
        string token,
        string immutableMessageId,
        string subject,
        string? conversationIdentity)
    {
        var now = DateTimeOffset.UtcNow;
        var approvedMailboxId = await TestMailboxId.EnsureApprovedAsync(
            context, $"{key}-mailbox", "intake@example.test", now.AddDays(-1));
        if (!await context.ApprovedInboxPollStates.AnyAsync(item => item.ApprovedMailboxId == approvedMailboxId))
        {
            context.ApprovedInboxPollStates.Add(new()
            {
                ApprovedMailboxId = approvedMailboxId,
                MailboxAddress = "intake@example.test",
                ScopeFingerprint = new string('A', 64),
                ActivatedAtUtc = now.AddDays(-1),
                DueAtUtc = now,
                LastCompletedAtUtc = now.AddMinutes(-1)
            });
        }
        var message = new RetainedMailboxMessageEntity
        {
            Id = Guid.NewGuid(),
            MailboxId = approvedMailboxId,
            MailboxAddress = "intake@example.test",
            FolderScope = "inbox",
            FolderIdentity = "inbox",
            ImmutableMessageId = immutableMessageId,
            ConversationIdentity = conversationIdentity,
            ExternalReceiptToken = token,
            SenderAddress = "instructions@qdosassist.co.uk",
            SenderDisplayName = "Synthetic sender",
            ToAddressesJson = "[]",
            CcAddressesJson = "[]",
            Subject = subject,
            SourceLength = 1,
            SourceSha256 = new string('A', 64),
            ReceivedAtUtc = now,
            RetainedAtUtc = now
        };
        context.RetainedMailboxMessages.Add(message);
        await context.SaveChangesAsync();
        return message.Id;
    }
}
