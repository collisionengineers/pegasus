using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The upload group page's small status call at
/// <c>/Upload/Group/{id}?handler=Status</c>, which the page's script asks
/// between reloads until every file is settled: whether anything is still
/// moving, how long to wait before asking again, and each member's queue
/// state. The page itself owns the delay; the call only repeats it.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class UploadGroupStatusPollingWebTests
{
    [Fact]
    public async Task AGroupStillBeingReceivedAnswersMovingWithTheMinimumDelay()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (_, groupId) = await UploadTwoImagesAsync(client);

        using var response = await client.GetAsync(StatusAddress(groupId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var answer = json.RootElement;
        Assert.True(answer.GetProperty("moving").GetBoolean());
        Assert.Equal(2000, answer.GetProperty("nextDelayMs").GetInt32());
        var members = answer.GetProperty("members").EnumerateArray().ToArray();
        Assert.All(members, member => Assert.Equal("received", member.GetProperty("state").GetString()));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var group = await scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>()
                .GetAsync(groupId, CancellationToken.None);
            Assert.Equal(
                group!.Members.Select(member => member.StagedReceiptId),
                members.Select(member => member.GetProperty("id").GetGuid()));
        }

        var page = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Group/{groupId:D}");
        Assert.Contains("data-auto-refresh=\"2000\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGroupWithEveryMemberSettledAnswersNotMovingAndNoDelay()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var (upload, groupId) = await UploadTwoImagesAsync(client);
        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(scope.ServiceProvider);
        }

        var answer = await GetStatusAsync(client, groupId);

        Assert.False(answer.Moving);
        Assert.Null(answer.NextDelayMs);
        Assert.All(answer.States, state => Assert.Equal("complete", state));
        var page = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Group/{groupId:D}");
        Assert.Contains("data-upload-phase=\"decision\"", page, StringComparison.Ordinal);
        Assert.DoesNotMatch("data-auto-refresh=\"[1-9]", page);
    }

    [Fact]
    public async Task AWorkingMemberOrARetryDueMemberSetsTheDelayThePageWouldSet()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var (upload, groupId) = await UploadTwoImagesAsync(client);
        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        IntakeSubmissionGroup group;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(scope.ServiceProvider);
            group = (await scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>()
                .GetAsync(groupId, CancellationToken.None))!;
        }
        var member = group.Members[0].StagedReceiptId;

        using (var working = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUploadOutcomeQueries>();
                services.AddSingleton<IUploadOutcomeQueries>(new OneMemberWorking(member));
            })))
        {
            using var workingClient = IntakeWebDriver.CreateClient(working);
            await AssertStatusEqualsThePageAsync(workingClient, groupId, 2000);
        }

        using (var retryDue = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IQueuedIntakeStatusQueries>();
                services.AddScoped<IQueuedIntakeStatusQueries>(provider => new OneMemberRetryDue(
                    new EfQueuedIntakeStatusQueries(
                        provider.GetRequiredService<IDbContextFactory<PegasusDbContext>>()),
                    member,
                    IntakeWebApplicationFactory.FixedUtcNow.AddSeconds(30)));
            })))
        {
            using var retryDueClient = IntakeWebDriver.CreateClient(retryDue);
            await AssertStatusEqualsThePageAsync(retryDueClient, groupId, 30000);
        }
    }

    [Fact]
    public async Task ThePageOffersPollingAndKeepsItsRefreshButton()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (_, groupId) = await UploadTwoImagesAsync(client);

        var page = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Group/{groupId:D}");

        Assert.Contains("data-upload-phase=\"pending\"", page, StringComparison.Ordinal);
        var surface = Regex.Match(page, "<[^>]*\\bdata-auto-refresh=\"[^\"]*\"[^>]*>");
        Assert.True(surface.Success);
        Assert.Contains(
            $"data-status-url=\"/Upload/Group/{groupId:D}?handler=Status\"",
            surface.Value,
            StringComparison.Ordinal);
        Assert.Contains("data-refresh-form", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheStatusCallIsStaffOnlyAndAnUnknownGroupIsNotFound()
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

    private static string StatusAddress(Guid groupId) => $"/Upload/Group/{groupId:D}?handler=Status";

    private static async Task<(UploadResult Upload, Guid GroupId)> UploadTwoImagesAsync(HttpClient client)
    {
        var png = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", png),
                ("close-up.png", "image/png", png)
            ]);
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        return (upload, Guid.Parse(upload.Location!.OriginalString.Split('/').Last()));
    }

    private static async Task<StatusAnswer> GetStatusAsync(HttpClient client, Guid groupId)
    {
        using var response = await client.GetAsync(StatusAddress(groupId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var answer = json.RootElement;
        var delay = answer.GetProperty("nextDelayMs");
        return new(
            answer.GetProperty("moving").GetBoolean(),
            delay.ValueKind == JsonValueKind.Null ? null : delay.GetInt32(),
            answer.GetProperty("members").EnumerateArray()
                .Select(member => member.GetProperty("state").GetString()!)
                .ToArray());
    }

    private static async Task AssertStatusEqualsThePageAsync(HttpClient client, Guid groupId, int expectedDelay)
    {
        var answer = await GetStatusAsync(client, groupId);
        Assert.True(answer.Moving);
        Assert.Equal(expectedDelay, answer.NextDelayMs);
        var page = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Group/{groupId:D}");
        Assert.Contains($"data-auto-refresh=\"{expectedDelay}\"", page, StringComparison.Ordinal);
    }

    private sealed record StatusAnswer(bool Moving, int? NextDelayMs, IReadOnlyList<string> States);

    /// <summary>One member's group-level outcome is still resolving; the rest await a staff decision.</summary>
    private sealed class OneMemberWorking(Guid workingStagedReceiptId) : IUploadOutcomeQueries
    {
        public Task<UploadOutcomeView> BuildAsync(
            QueuedIntakeStatus status,
            IntakeReceipt? receipt,
            Guid? submissionGroupId,
            ActionActor actor,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UploadOutcomeView(
                status.StagedReceiptId == workingStagedReceiptId
                    ? UploadOutcomeKind.Working
                    : UploadOutcomeKind.NeedsReview,
                "State",
                "Message",
                null,
                null));
    }

    /// <summary>One member reads Processing with its next attempt already scheduled.</summary>
    private sealed class OneMemberRetryDue(
        IQueuedIntakeStatusQueries inner,
        Guid stagedReceiptId,
        DateTimeOffset dueAtUtc) : IQueuedIntakeStatusQueries
    {
        public async Task<QueuedIntakeStatus?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var status = await inner.GetAsync(id, cancellationToken);
            return status is not null && id == stagedReceiptId
                ? status with { Status = QueuedIntakeStatusKind.Processing, RetryDueAtUtc = dueAtUtc }
                : status;
        }

        public Task<QueuedIntakeStatus?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken = default) =>
            inner.FindBySourceIdentityAsync(sourceIdentity, cancellationToken);
    }
}
