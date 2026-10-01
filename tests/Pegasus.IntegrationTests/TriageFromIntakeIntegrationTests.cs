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
/// a Triage when a vehicle registration is known and waits in Unidentified when
/// it is not; neither outcome allocates a formal Case/PO.
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

    [Fact]
    public async Task AForwardedReplyOnATriageThreadOpensNoSecondTriage()
    {
        // The shape a reply actually arrives in. The classifier's own note
        // records that every QDOS message reaches us as a staff forward, so an
        // ordinary reply is "FW: RE: ..." and never a bare "RE: ...". While
        // reply detection only recognised a leading "RE:", this subject read as
        // a brand-new request and opened a duplicate Triage for ordinary thread
        // correspondence -- the exact duplicate the reply-context gate exists
        // to prevent.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "engineer-triage-reply.eml",
            "Thanks -- confirming the vehicle is roadworthy as discussed.",
            subject: "FW: RE: Engineer Triage - Our Claim Reference : 46246/1 - "
                + "Vehicle Registration : VO75DFJ");

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(
            await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None));

        // Still not an instruction, and still no case -- that part never
        // depended on reply detection.
        Assert.NotEqual(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.Null(receipt.CurrentCaseId);

        // The registration is right there in the subject, so this is the case
        // that would have opened a Triage on the strength of it.
        Assert.Empty(
            await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
        Assert.DoesNotContain(
            receipt.Evidence,
            item => item.Finding == IntakeEvidenceFinding.AcceptedTriageMatch);
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
    public async Task ATriageRequestWithNoRegistrationWaitsInUnidentifiedAndOpensNoTriage()
    {
        // The operator's other branch, verbatim: "keep it as Unidentified …
        // until a vehicle registration is known, then open the Triage".
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

        var item = Assert.IsType<UnidentifiedItem>(
            await scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
                .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None));
        Assert.Equal(UnidentifiedState.Open, item.State);
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

        Guid messageId;
        await using (var lookup = factory.Services.CreateAsyncScope())
        {
            var contextFactory = lookup.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var token = await context.IntakeReceipts.Where(item => item.Id == receiptId).Select(item => item.ExternalReceiptToken).SingleAsync();
            messageId = await context.RetainedMailboxMessages.Where(item => item.ExternalReceiptToken == token).Select(item => item.Id).SingleAsync();
            Assert.Equal(1, await context.IntakeMailClassificationDecisions.Where(item => item.IntakeReceiptId == receiptId).Select(item => item.Version).SingleAsync());
            Assert.Empty(await lookup.ServiceProvider.GetRequiredService<ITriageQueries>().ListAsync(null, CancellationToken.None));
        }

        // Before the correction the message offers nothing.
        var before = await IntakeWebDriver.GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.DoesNotContain("data-offered-action", before, StringComparison.Ordinal);

        using var correction = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["ExpectedClassificationVersion"] = "1",
            ["ClassificationKey"] = "received:PreInstructionEmails:triage-request",
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
        var settled = await IntakeWebDriver.GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.DoesNotContain("data-offered-action", settled, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task StaffSupplyingTheRegistrationOpensTheTriageAndClosesTheUnidentifiedItem()
    {
        // The rest of the operator's sentence: a Triage request that never
        // carried a readable registration waits in Unidentified "until a
        // vehicle registration is known, then open the Triage" — and the only
        // thing that can know it here is a member of staff.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-registration-supplied.eml",
            "Triage Only Request\r\n\r\nPlease find attached our client's images.");

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

        // Stranded exactly as the branch above leaves it.
        await using (var before = factory.Services.CreateAsyncScope())
        {
            Assert.Empty(
                await before.ServiceProvider.GetRequiredService<ITriageQueries>()
                    .ListAsync(null, CancellationToken.None));
            var open = Assert.IsType<UnidentifiedItem>(
                await before.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
                    .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None));
            Assert.Equal(UnidentifiedState.Open, open.State);
        }

        // The action is offered on the Unidentified record (received file D2).
        Guid unidentifiedId;
        await using (var lookup = factory.Services.CreateAsyncScope())
        {
            unidentifiedId = Assert.IsType<UnidentifiedItem>(
                await lookup.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
                    .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None)).Id;
        }
        var offered = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{unidentifiedId:D}");
        Assert.Contains("handler=OpenTriage", offered, StringComparison.Ordinal);
        Assert.Contains("Open the Triage", offered, StringComparison.Ordinal);

        // Typed the way a person types it: lower case, with the separator.
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["vehicleRegistration"] = "vn64 wng",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });
        using var opened = await client.PostAsync($"/Unidentified/{unidentifiedId:D}?handler=OpenTriage", form);
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

        // The Unidentified item is stale the moment the Triage exists, and the
        // resolution names the destination permanently.
        var unidentifiedStore = after.ServiceProvider.GetRequiredService<IUnidentifiedStore>();
        var resolved = Assert.IsType<UnidentifiedItem>(
            await unidentifiedStore.GetByOriginAsync(
                UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None));
        Assert.Equal(UnidentifiedState.Resolved, resolved.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.Triage, resolved.ResolutionTargetKind);
        Assert.Equal(triage.CaseId.ToString("N"), resolved.ResolutionTargetId);
        Assert.Equal("VN64WNG", resolved.ResolutionTargetReference);
        Assert.Contains(
            await unidentifiedStore.HistoryAsync(resolved.Id, CancellationToken.None),
            entry => entry.NewState == UnidentifiedState.Resolved
                && entry.TargetKind == UnidentifiedResolutionTargetKind.Triage);

        // The receipt now has its destination, so the action is not offered a
        // second time and no second Triage can be opened from the screen.
        var settled = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{unidentifiedId:D}");
        Assert.DoesNotContain("handler=OpenTriage", settled, StringComparison.Ordinal);
    }
}
