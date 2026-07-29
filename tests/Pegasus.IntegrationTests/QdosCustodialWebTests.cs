using System.Net;
using Pegasus.Core.Documents;

namespace Pegasus.IntegrationTests;

public sealed class QdosCustodialWebTests
{
    [Fact]
    public async Task PublicRequestUploadWithNoMatchingTokenReturnsNoRequestOrCaseDisclosure()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = RequestUploadToken.Create().Secret.Token;

        using var response = await client.GetAsync($"/Requests/Upload?token={Uri.EscapeDataString(token)}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
        Assert.DoesNotContain("case", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("request", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustodialDocumentControlsUseTheAuthenticatedOfflineStaffSession()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"/cases/{Guid.NewGuid():D}/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Case documents", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
