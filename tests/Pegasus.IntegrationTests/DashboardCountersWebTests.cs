using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// PLAT-012: the Dashboard's "Received today" tile sits under the E-mail
/// activity section, so it must count mailbox-channel intake only. A manual
/// upload is a different intake channel entirely and must not move it.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DashboardCountersWebTests
{
    [Fact]
    public async Task ReceivedTodayCountsMailboxChannelOnlyNotManualUploads()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        // One mailbox receipt (should count) and one manual-upload receipt
        // (must not) — both received "now", well inside the office day the
        // fixed test clock reports.
        await StoreReceiptAsync(services, IntakeSourceChannel.Mailbox, "instruction.eml", now);
        await StoreReceiptAsync(services, IntakeSourceChannel.ManualUpload, "photo.jpg", now);

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var match = Regex.Match(
            html,
            "Received today[\\s\\S]*?metric__value\">(\\d+)</strong>");
        Assert.True(match.Success, "Received today tile markup not found.");
        Assert.Equal(1, int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task StoreReceiptAsync(
        IServiceProvider services,
        IntakeSourceChannel channel,
        string sourceFileName,
        DateTimeOffset receivedAtUtc)
    {
        var receiptStore = services.GetRequiredService<IIntakeReceiptStore>();
        await receiptStore.StoreAsync(
            new IntakeReceiptDraft(
                sourceFileName,
                "application/octet-stream",
                1024,
                Guid.NewGuid().ToString("N"),
                new IntakeSourceIdentity(channel, Guid.NewGuid().ToString("N")),
                receivedAtUtc,
                receivedAtUtc,
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
