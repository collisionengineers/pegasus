using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The operator's Stage 0 rule, end to end through the real intake pipeline —
/// no stub extraction policy, no injected evidence. A QDOS Triage request opens
/// a Triage when a vehicle registration is known and waits in Unidentified when
/// it is not, and it never becomes a case either way (INTK-033).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class TriageFromIntakeIntegrationTests
{
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ASubjectTemplateTriageRequestOpensATriageAndNoUnidentifiedItem()
    {
        // The subject template states the registration nowhere but the
        // subject; this is the exact shape of the message the operator
        // forwarded and watched disappear.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "engineer-triage.eml",
            "Good morning\r\n\r\nPlease see the attached images to determine if the vehicle is "
            + "repairable or a total loss. We have noted the vehicle as roadworthy.",
            subject: "Engineer Triage - Our Claim Reference : 46246/1 - Vehicle Registration : VO75DFJ");

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

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
                .GetAsync(triage.Id, CancellationToken.None));

        Assert.Equal(receiptId, detail.Record.Origin.ReceiptId);
        Assert.Equal("VO75DFJ", detail.Record.NormalizedVehicleRegistration);
        Assert.Equal(TriageState.Open, detail.Record.State);
        var created = Assert.Single(detail.History, item => item.EventType == "triage_created");
        Assert.Contains(
            QdosMailClassificationPolicy.Key,
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
        // to prevent (INTK-033 review).
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "engineer-triage-reply.eml",
            "Thanks -- confirming the vehicle is roadworthy as discussed.",
            subject: "FW: RE: Engineer Triage - Our Claim Reference : 46246/1 - "
                + "Vehicle Registration : VO75DFJ");

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

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

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

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

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

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
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task StaffSupplyingTheRegistrationOpensTheTriageAndClosesTheUnidentifiedItem()
    {
        // The rest of the operator's sentence: a Triage request that never
        // carried a readable registration waits in Unidentified "until a
        // vehicle registration is known, then open the Triage" — and the only
        // thing that can know it here is a member of staff (INTK-035).
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-registration-supplied.eml",
            "Triage Only Request\r\n\r\nPlease find attached our client's images.");

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

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

        // The action is offered on the receipt the Unidentified item links to.
        var offered = await IntakeWebDriver.GetHtmlAsync(client, $"/Received/{receiptId}");
        Assert.Contains("handler=OpenTriage", offered, StringComparison.Ordinal);
        Assert.Contains("Open the Triage", offered, StringComparison.Ordinal);

        // Typed the way a person types it: lower case, with the separator.
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["vehicleRegistration"] = "vn64 wng",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });
        using var opened = await client.PostAsync($"/Received/{receiptId}?handler=OpenTriage", form);
        Assert.Equal(HttpStatusCode.Redirect, opened.StatusCode);

        await using var after = factory.Services.CreateAsyncScope();
        var triage = Assert.Single(
            await after.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
        var detail = Assert.IsType<TriageDetail>(
            await after.ServiceProvider.GetRequiredService<ITriageQueries>()
                .GetAsync(triage.Id, CancellationToken.None));

        Assert.Equal(receiptId, detail.Record.Origin.ReceiptId);
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
        Assert.Equal(triage.Id.ToString("N"), resolved.ResolutionTargetId);
        Assert.Equal("VN64WNG", resolved.ResolutionTargetReference);
        Assert.Contains(
            await unidentifiedStore.HistoryAsync(resolved.Id, CancellationToken.None),
            entry => entry.NewState == UnidentifiedState.Resolved
                && entry.TargetKind == UnidentifiedResolutionTargetKind.Triage);

        // The receipt now has its destination, so the action is not offered a
        // second time and no second Triage can be opened from the screen.
        var settled = await IntakeWebDriver.GetHtmlAsync(client, $"/Received/{receiptId}");
        Assert.DoesNotContain("handler=OpenTriage", settled, StringComparison.Ordinal);
    }
}
