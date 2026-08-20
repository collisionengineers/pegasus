using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// MCP-07: the Administration-entered connector settings — base URL, timeout,
/// token entry/rotation — stored beside the Send to AI switch, overriding the
/// composed configuration from the next hand-off, with the token write-only
/// and every change attributed. Second part of the Send to AI suite so the
/// fake channel and form helpers stay in one place.
/// </summary>
public sealed partial class SendToAiIntegrationTests
{
    private const string RotatedToken = "administration-rotated-channel-token-9876543210";

    [Fact]
    public async Task AdministrationConnectorValuesOverrideConfigurationFromTheNextHandOff()
    {
        await using var configuredReceiver = new FakeChannelReceiver();
        await using var overrideReceiver = new FakeChannelReceiver();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithSendToAi(baseFactory, configuredReceiver.BaseUrl);
        var caseId = await SeedAcceptedCaseAsync(factory);
        using var client = CreateClient(factory);

        // Before any administration entry the section states the fallback.
        var adminHtml = await GetHtmlAsync(client, "/Administration/Automation");
        Assert.Contains("Send to AI connector", adminHtml, StringComparison.Ordinal);
        Assert.Contains("From deployment configuration", adminHtml, StringComparison.Ordinal);

        using (var response = await client.PostAsync(
            "/Administration/Automation?handler=UpdateConnector",
            Form(
                AntiforgeryValue(adminHtml),
                ("ChannelAddress", overrideReceiver.BaseUrl),
                ("ChannelTimeoutSeconds", "5"),
                ("Reason", "Point the connector at the replacement channel."),
                ("OperationKey", InputValue(adminHtml, "OperationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var rotateHtml = await GetHtmlAsync(client, "/Administration/Automation");
        using (var response = await client.PostAsync(
            "/Administration/Automation?handler=RotateChannelToken",
            Form(
                AntiforgeryValue(rotateHtml),
                ("NewChannelToken", RotatedToken),
                ("Reason", "Enter the replacement channel token."),
                ("OperationKey", InputValue(rotateHtml, "OperationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        // The page states that a token is held and never shows the value.
        var heldHtml = await GetHtmlAsync(client, "/Administration/Automation");
        Assert.Contains("Entered from Administration", heldHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(RotatedToken, heldHtml, StringComparison.Ordinal);

        // The next hand-off reaches the overridden channel with the rotated
        // token; the composed channel receives nothing.
        var assessmentHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        using (var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=Send",
            Form(
                AntiforgeryValue(assessmentHtml),
                ("operationKey", InputValue(assessmentHtml, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var request = Assert.Single(
            overrideReceiver.Requests,
            item => item.Path.StartsWith("/send", StringComparison.Ordinal));
        Assert.Equal($"Bearer {RotatedToken}", request.Authorization);
        Assert.DoesNotContain(
            configuredReceiver.Requests,
            item => item.Path.StartsWith("/send", StringComparison.Ordinal));

        // Both changes are attributed permanent history; neither carries the
        // token value, and the stored token is protected at rest.
        using var scope = factory.Services.CreateScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "send_to_ai")
            .ToArrayAsync();
        Assert.Contains(history, entry =>
            entry.EventKind == "send_to_ai_connector_updated"
            && entry.ActorKind == "Staff"
            && entry.Outcome == "Succeeded");
        Assert.Contains(history, entry =>
            entry.EventKind == "send_to_ai_channel_token_rotated"
            && entry.ActorKind == "Staff"
            && entry.Outcome == "Succeeded");
        Assert.All(history, entry =>
            Assert.DoesNotContain(RotatedToken, entry.Reason ?? string.Empty, StringComparison.Ordinal));
        var stored = await context.Database
            .SqlQuery<string?>($"SELECT ChannelTokenProtected AS Value FROM SendToAiControl")
            .SingleAsync();
        Assert.NotNull(stored);
        Assert.DoesNotContain(RotatedToken, stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectorSettingsFailClosedOnActorBoundsAndClearing()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithSendToAi(baseFactory, "http://127.0.0.1:9");
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAiChannelConnectorStore>();
        var administrator = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);

        // Only an Administrator may change connector settings.
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => store.UpdateAsync(
            new(ActionActor.Automation("pegasus-automation"), "reason", "key", null, null),
            CancellationToken.None));

        // The Core-owned bounds refuse a non-loopback origin, an out-of-range
        // timeout, and a short token.
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateAsync(
            new(administrator, "reason", "key", "https://example.com/", null),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateAsync(
            new(administrator, "reason", "key", null, 120),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => store.RotateTokenAsync(
            new(administrator, "reason", "key", "short"),
            CancellationToken.None));

        // Rotation stores a protected token the runtime view can read back;
        // clearing returns the connector to configuration.
        await store.RotateTokenAsync(
            new(administrator, "Enter the token.", "connector-test-rotate", RotatedToken),
            CancellationToken.None);
        var settings = await store.GetAsync(CancellationToken.None);
        Assert.True(settings.TokenHeld);
        Assert.NotNull(settings.TokenRotatedAtUtc);
        var runtime = await store.GetRuntimeAsync(CancellationToken.None);
        Assert.Equal(RotatedToken, runtime.ChannelToken);

        await store.RotateTokenAsync(
            new(administrator, "Remove the token.", "connector-test-clear", NewToken: null),
            CancellationToken.None);
        var cleared = await store.GetAsync(CancellationToken.None);
        Assert.False(cleared.TokenHeld);
        Assert.Null(cleared.TokenRotatedAtUtc);
        Assert.Null((await store.GetRuntimeAsync(CancellationToken.None)).ChannelToken);
    }
}
