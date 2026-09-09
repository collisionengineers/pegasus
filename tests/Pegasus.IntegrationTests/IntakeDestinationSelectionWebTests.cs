using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Workflow;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class IntakeDestinationSelectionWebTests
{
    [Theory]
    [InlineData(IntakeSourceChannel.ManualUpload)]
    [InlineData(IntakeSourceChannel.Mailbox)]
    [InlineData(IntakeSourceChannel.ProviderApi)]
    [InlineData(IntakeSourceChannel.Automation)]
    public async Task ReceiptAssociationSearchesSelectsLeasesAndAttachesTheRetainedSource(IntakeSourceChannel channel)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "DESTINATION-CASE-01");
        var receipt = await StoreUnidentifiedReceiptAsync(factory, channel);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var unidentified = await RegisterUnidentifiedAsync(services, receipt.Id);

        using var searched = await client.GetAsync(
            $"/Received/{receipt.Id:D}?caseQuery=DESTINATION-CASE-01");
        var searchHtml = await searched.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, searched.StatusCode);
        Assert.Contains("DESTINATION-CASE-01", searchHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Case identifier", searchHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Expected case version", searchHtml, StringComparison.Ordinal);

        using var selected = await client.GetAsync($"/Received/{receipt.Id:D}?targetCaseId={caseId:D}");
        var selectedHtml = await selected.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        Assert.Contains("Selected case", selectedHtml, StringComparison.Ordinal);
        Assert.Contains($"name=\"caseId\" value=\"{caseId:D}\"", selectedHtml, StringComparison.Ordinal);
        Assert.Matches("name=\"expectedCaseVersion\" value=\"[0-9]+\"", selectedHtml);

        using var claimed = await client.PostAsync($"/Received/{receipt.Id:D}?handler=ClaimCaseLease",
            new FormUrlEncodedContent(HiddenFormValues(selectedHtml, "ClaimCaseLease")));
        Assert.Equal(HttpStatusCode.Redirect, claimed.StatusCode);
        Assert.Contains($"targetCaseId={caseId:D}", claimed.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
        var leasedHtml = await client.GetStringAsync(claimed.Headers.Location);
        var notices = Regex.Matches(leasedHtml, "<div class=\"notice[^\"]*\"[^>]*>(.*?)</div>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .Select(match => WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "<[^>]+>", " ",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))).Trim());
        Assert.True(leasedHtml.Contains("Case edit mode is active until", StringComparison.Ordinal),
            "The claim must succeed before linking. Visible notices: " + string.Join(" | ", notices));
        var linkForm = HiddenFormValues(leasedHtml, "LinkCase");
        Assert.Contains("DESTINATION-CASE-01", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("AB12CDE", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Fixture Claimant", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Review", leasedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Enter case edit mode", leasedHtml, StringComparison.Ordinal);

        var staleLinkForm = new Dictionary<string, string>(linkForm)
        {
            ["expectedIntakeVersion"] = "-1",
            ["reason"] = "The stale link must retain the selected target for retry."
        };
        using var staleLink = await client.PostAsync($"/Received/{receipt.Id:D}?handler=LinkCase",
            new FormUrlEncodedContent(staleLinkForm));
        Assert.Equal(HttpStatusCode.Redirect, staleLink.StatusCode);
        var retryHtml = await client.GetStringAsync(staleLink.Headers.Location);
        Assert.Contains("The intake command could not be applied", retryHtml, StringComparison.Ordinal);
        Assert.Contains("DESTINATION-CASE-01", retryHtml, StringComparison.Ordinal);
        Assert.Contains("AB12CDE", retryHtml, StringComparison.Ordinal);
        Assert.Contains("Fixture Claimant", retryHtml, StringComparison.Ordinal);
        Assert.Contains("Review", retryHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Enter case edit mode", retryHtml, StringComparison.Ordinal);
        linkForm = HiddenFormValues(retryHtml, "LinkCase");
        linkForm["reason"] = "Staff identified the Case from the retained source.";
        using var linked = await client.PostAsync($"/Received/{receipt.Id:D}?handler=LinkCase", new FormUrlEncodedContent(linkForm));
        Assert.Equal(HttpStatusCode.Redirect, linked.StatusCode);
        var persisted = await services.GetRequiredService<IIntakeReceiptQueries>().GetAsync(receipt.Id, CancellationToken.None);
        Assert.Equal(caseId, persisted!.CurrentCaseId);

        await services.GetRequiredService<ReconcileUnidentifiedDestinations>().ExecuteAsync(50);
        var resolved = await services.GetRequiredService<IUnidentifiedStore>().GetAsync(unidentified.Id);
        Assert.Equal(UnidentifiedState.Resolved, resolved!.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolved.ResolutionTargetKind);
        Assert.Equal(caseId, Guid.Parse(resolved.ResolutionTargetId!));
    }

    [Fact]
    public async Task ManualUnidentifiedPdfDoesNotRenderAnEmptyInspectionAddressSection()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await StoreUnidentifiedReceiptAsync(factory);

        using var response = await client.GetAsync($"/Received/{receipt.Id:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Inspection-address confirmation", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStaleCloseRetainsItsVersionAndCannotResolveOnRetry()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await StoreUnidentifiedReceiptAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var item = await RegisterUnidentifiedAsync(services, receipt.Id);
        var html = await client.GetStringAsync($"/Unidentified/{item.Id:D}?action=close");
        var form = HiddenFormValues(html, "Resolve");
        form["ResolutionReason"] = "The original reviewed reason.";
        await using (var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE UnidentifiedItems SET Version = Version + 1 WHERE Id = {item.Id}");
        }
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await client.PostAsync($"/Unidentified/{item.Id:D}?handler=Resolve", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var refusedHtml = await response.Content.ReadAsStringAsync();
            Assert.Contains("Reload it before resolving", refusedHtml, StringComparison.Ordinal);
            Assert.Contains("data-dialog-open-on-load=\"true\"", refusedHtml, StringComparison.Ordinal);
            var retry = HiddenFormValues(refusedHtml, "Resolve");
            Assert.Equal(form["ExpectedVersion"], retry["ExpectedVersion"]);
            Assert.Equal(form["OperationKey"], retry["OperationKey"]);
            Assert.Contains("The original reviewed reason.", refusedHtml, StringComparison.Ordinal);
            retry["ResolutionReason"] = form["ResolutionReason"];
            form = retry;
        }
        var unchanged = await services.GetRequiredService<IUnidentifiedStore>().GetAsync(item.Id);
        Assert.Equal(UnidentifiedState.Open, unchanged!.State);
        Assert.Equal(item.Version + 1, unchanged.Version);
        Assert.Null(unchanged.ResolutionTargetId);
    }

    [Theory]
    [InlineData(IntakeSourceChannel.Mailbox)]
    [InlineData(IntakeSourceChannel.ProviderApi)]
    [InlineData(IntakeSourceChannel.Automation)]
    public async Task NonManualImageWritesCannotBypassPostReportDestinationEligibility(IntakeSourceChannel channel)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(factory, client, "AB12 CDE", "POST-REPORT-DESTINATION");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using (var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.PostReport)} WHERE CaseId = {caseId}");
        }
        var image = new IntakeAssetRecord(Guid.NewGuid(), "retained source", "vehicle.jpg", "image/jpeg",
            IntakeAssetKind.Source, IntakeAssetDisposition.Source, 2048, new string('a', 64), "fixture/vehicle.jpg",
            null, null, null, null);
        var receipt = await StoreUnidentifiedReceiptAsync(factory, channel, [image]);
        Assert.True(ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt));
        var actor = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var workflow = await services.GetRequiredService<ICaseWorkflowQueries>().GetAsync(caseId, CancellationToken.None);
        var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new ClaimCaseEditLeaseRequest(caseId, workflow!.Version, actor, $"destination-lease:{Guid.NewGuid():N}"), CancellationToken.None);
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => services.GetRequiredService<ILinkIntake>().ExecuteAsync(new(
            receipt.Id, caseId, receipt.Version, workflow.Version, lease.Token, actor,
            $"forged-destination:{Guid.NewGuid():N}", "A retained image must not attach after reporting.")));
        var unchanged = await services.GetRequiredService<IIntakeReceiptQueries>().GetAsync(receipt.Id, CancellationToken.None);
        Assert.Null(unchanged!.CurrentCaseId);
        Assert.Equal(receipt.Version, unchanged.Version);
    }

    [Fact]
    public async Task MalformedDestinationSelectorsShowAnErrorWithoutChangingTheReceipt()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await StoreUnidentifiedReceiptAsync(factory);
        foreach (var query in new[] { "caseQuery=" + new string('x', 301), "targetCaseId=" + Guid.Empty, "targetCaseId=invalid" })
        {
            using var response = await client.GetAsync($"/Received/{receipt.Id:D}?{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Enter case edit mode", html, StringComparison.Ordinal);
            Assert.Contains("Please correct the following errors:", html, StringComparison.Ordinal);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var unchanged = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>().GetAsync(receipt.Id, CancellationToken.None);
        Assert.Equal(receipt.Version, unchanged!.Version);
        Assert.Null(unchanged.CurrentCaseId);
    }

    [Fact]
    public async Task UnidentifiedCloseUsesOnlyTheRequiredReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await StoreUnidentifiedReceiptAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var item = (await services.GetRequiredService<IRegisterUnidentified>().ExecuteAsync(
            new(
                UnidentifiedOrigin.Receipt(receipt.Id),
                UnidentifiedReasonCode.NoUsableIdentification,
                "The document did not establish a destination.",
                ActionActor.SystemWorker("destination-selection-test"),
                $"destination-selection-test:{Guid.NewGuid():N}",
                services.GetRequiredService<TimeProvider>().GetUtcNow()),
            CancellationToken.None)).Item;

        using var response = await client.GetAsync(
            $"/Unidentified/{item.Id:D}?action=close");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Close with reason", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ResolutionReason\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Destination identifier", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Destination reference", html, StringComparison.Ordinal);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["ExpectedVersion"] = item.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["OperationKey"] = $"close-unidentified-test:{Guid.NewGuid():N}",
            ["action"] = "close",
            ["ResolutionReason"] = "This source does not require further work."
        });
        using var closed = await client.PostAsync($"/Unidentified/{item.Id:D}?handler=Resolve", form);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        var resolved = await services.GetRequiredService<IUnidentifiedStore>()
            .GetAsync(item.Id, CancellationToken.None);
        Assert.Equal(UnidentifiedState.Resolved, resolved!.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.ExternalReference, resolved.ResolutionTargetKind);
    }

    private static async Task<IntakeReceipt> StoreUnidentifiedReceiptAsync(
        IntakeWebApplicationFactory factory,
        IntakeSourceChannel channel = IntakeSourceChannel.ManualUpload,
        IReadOnlyList<IntakeAssetRecord>? assets = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receivedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
        return await services.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new(
                "destination-selection.pdf",
                "application/pdf",
                2048,
                Guid.NewGuid().ToString("N"),
                new(channel, Guid.NewGuid().ToString("N")),
                receivedAt,
                receivedAt,
                "test-actor",
                IntakeDecision.NeedsSorting,
                "test decision reason",
                [],
                [],
                null,
                [],
                null,
                null,
                "test-reader",
                "1",
                null,
                null,
                Assets: assets),
            CancellationToken.None);
    }

    private static async Task<UnidentifiedItem> RegisterUnidentifiedAsync(IServiceProvider services, Guid receiptId) =>
        (await services.GetRequiredService<IRegisterUnidentified>().ExecuteAsync(new(
            UnidentifiedOrigin.Receipt(receiptId), UnidentifiedReasonCode.NoUsableIdentification,
            "The retained source needs a staff destination.", ActionActor.SystemWorker("destination-selection-test"),
            $"destination-selection-test:{Guid.NewGuid():N}", services.GetRequiredService<TimeProvider>().GetUtcNow()))).Item;

    private static Dictionary<string, string> HiddenFormValues(string html, string handler)
    {
        var form = Regex.Match(html, $"<form\\b[^>]*action=\"[^\"]*handler={Regex.Escape(handler)}[^\"]*\"[^>]*>(.*?)</form>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.True(form.Success, $"The {handler} form must be present.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(form.Groups[1].Value, "<input\\b[^>]*>", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            var name = Regex.Match(input.Value, "\\bname=\"([^\"]*)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            var value = Regex.Match(input.Value, "\\bvalue=\"([^\"]*)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (name.Success) values[WebUtility.HtmlDecode(name.Groups[1].Value)] = WebUtility.HtmlDecode(value.Groups[1].Value);
        }
        return values;
    }
}
