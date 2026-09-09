using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Actors;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class IntakeDestinationSelectionWebTests
{
    [Fact]
    public async Task ReceiptAssociationSearchesByCaseFactsAndRendersTheSelectedServerVersion()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "DESTINATION-CASE-01");
        var receipt = await StoreUnidentifiedReceiptAsync(factory);

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
        IntakeWebApplicationFactory factory)
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
                new(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
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
                null),
            CancellationToken.None);
    }
}
