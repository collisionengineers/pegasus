using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The single-file upload page's small status call at
/// <c>/Upload/Status/{id}?handler=Status</c>, the same answer the group page
/// gives its script: whether the file is still moving, how long to wait before
/// asking again, and its queue state. The page owns the delay; the call
/// repeats it from the status read alone, and the receipt only when the delay
/// needs it.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class UploadFileStatusPollingWebTests
{
    [Fact]
    public async Task AReceivedFileAnswersMovingFromOneStatusRead()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            localIntakeEnabled: true,
            commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        var (upload, stagedReceiptId) = await UploadEmailAsync(client);

        counter.Reset();
        using var response = await client.GetAsync(StatusAddress(stagedReceiptId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var answer = json.RootElement;
        Assert.True(answer.GetProperty("moving").GetBoolean());
        Assert.Equal(2000, answer.GetProperty("nextDelayMs").GetInt32());
        var member = Assert.Single(answer.GetProperty("members").EnumerateArray());
        Assert.Equal(stagedReceiptId, member.GetProperty("id").GetGuid());
        Assert.Equal("received", member.GetProperty("state").GetString());
        Assert.Equal(0, counter.WholeReceiptReads());

        var page = await IntakeWebDriver.GetHtmlAsync(client, upload.Location!.OriginalString);
        var surface = Regex.Match(page, "<[^>]*\\bdata-auto-refresh=\"[^\"]*\"[^>]*>");
        Assert.True(surface.Success);
        Assert.Contains("data-auto-refresh=\"2000\"", surface.Value, StringComparison.Ordinal);
        Assert.Contains(
            $"data-status-url=\"{StatusAddress(stagedReceiptId)}\"",
            surface.Value,
            StringComparison.Ordinal);
        Assert.Contains("data-refresh-form", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProcessedFileAnswersNotMovingAndNoDelay()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            localIntakeEnabled: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var (upload, stagedReceiptId) = await UploadEmailAsync(client);
        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);

        using var response = await client.GetAsync(StatusAddress(stagedReceiptId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var answer = json.RootElement;
        Assert.False(answer.GetProperty("moving").GetBoolean());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("nextDelayMs").ValueKind);
        var member = Assert.Single(answer.GetProperty("members").EnumerateArray());
        Assert.Equal("complete", member.GetProperty("state").GetString());
        var page = await IntakeWebDriver.GetHtmlAsync(client, upload.Location!.OriginalString);
        Assert.DoesNotMatch("data-auto-refresh=\"[1-9]", page);
    }

    [Fact]
    public async Task ARetryDueFileAnswersItsDueTime()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            localIntakeEnabled: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var (upload, stagedReceiptId) = await UploadEmailAsync(client);

        using var retryDue = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IQueuedIntakeStatusQueries>();
                services.AddScoped<IQueuedIntakeStatusQueries>(provider =>
                    new UploadGroupStatusPollingWebTests.OneMemberRetryDue(
                        new EfQueuedIntakeStatusQueries(
                            provider.GetRequiredService<IDbContextFactory<PegasusDbContext>>()),
                        stagedReceiptId,
                        IntakeWebApplicationFactory.FixedUtcNow.AddSeconds(30)));
            }));
        using var retryDueClient = IntakeWebDriver.CreateClient(retryDue);

        using var response = await retryDueClient.GetAsync(StatusAddress(stagedReceiptId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var answer = json.RootElement;
        Assert.True(answer.GetProperty("moving").GetBoolean());
        Assert.Equal(30000, answer.GetProperty("nextDelayMs").GetInt32());
        var page = await IntakeWebDriver.GetHtmlAsync(retryDueClient, upload.Location!.OriginalString);
        Assert.Contains("data-auto-refresh=\"30000\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFileStatusCallIsStaffOnlyAndAnUnknownIdIsNotFound()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var address = StatusAddress(Guid.NewGuid());

        using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, address);
        anonymousRequest.Headers.Add("X-Test-Anonymous", "1");
        using var anonymous = await client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Contains(
            "/Account/SignIn",
            anonymous.Headers.Location!.OriginalString,
            StringComparison.Ordinal);

        using var rolelessRequest = new HttpRequestMessage(HttpMethod.Get, address);
        rolelessRequest.Headers.Add("X-Test-Roleless", "1");
        using var roleless = await client.SendAsync(rolelessRequest);
        Assert.Equal(HttpStatusCode.Forbidden, roleless.StatusCode);

        using var unknown = await client.GetAsync(address);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    private static string StatusAddress(Guid stagedReceiptId) =>
        $"/Upload/Status/{stagedReceiptId:D}?handler=Status";

    private static async Task<(UploadResult Upload, Guid StagedReceiptId)> UploadEmailAsync(HttpClient client)
    {
        var email = IntakeTestEvidence.CreateEmail(
            "ordinary-correspondence.eml",
            "Please review this ordinary correspondence.",
            "sender@example.test");
        var upload = await IntakeWebDriver.UploadAsync(
            client,
            email.FileName,
            email.MediaType,
            email.Content,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        return (upload, Assert.IsType<Guid>(IntakeWebDriver.Landing(upload).StagedReceiptId));
    }
}
