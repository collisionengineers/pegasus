using System.Net;

namespace Pegasus.IntegrationTests;

public sealed class WorkflowWorkspaceWebTests
{
    [Fact]
    public async Task AuthenticatedWorkflowListsRenderConcreteDestinationsAndEmptyStates()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var triageResponse = await client.GetAsync("/Triage");
        using var casesResponse = await client.GetAsync("/Cases");
        Assert.Equal(HttpStatusCode.OK, triageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, casesResponse.StatusCode);

        var triageHtml = await triageResponse.Content.ReadAsStringAsync();
        var casesHtml = await casesResponse.Content.ReadAsStringAsync();
        Assert.Contains("Triage", triageHtml, StringComparison.Ordinal);
        Assert.Contains("Registration", triageHtml, StringComparison.Ordinal);
        Assert.Contains("No Triage records", triageHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"/Triage\"", triageHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"\"", triageHtml, StringComparison.Ordinal);
        Assert.Contains("Cases", casesHtml, StringComparison.Ordinal);
        Assert.Contains("Case / PO", casesHtml, StringComparison.Ordinal);
        Assert.Contains("No Cases", casesHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"/Cases\"", casesHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"\"", casesHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingWorkflowDetailsRemainNotFound()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var missingId = Guid.NewGuid();

        using var triageResponse = await client.GetAsync($"/Triage/{missingId}");
        using var caseResponse = await client.GetAsync($"/Cases/{missingId}");

        Assert.Equal(HttpStatusCode.NotFound, triageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, caseResponse.StatusCode);
    }
}
