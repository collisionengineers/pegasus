using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// UIIMP-008: the Work Centre's five metrics are page-queried figures, each
/// an exact link to the Cases tab behind it. Blocked is the real Blocked
/// intake count and links to the Unidentified tab, where Blocked intake rows
/// carry their own chip (decision D14) — never a fixture number.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DashboardCountersWebTests
{
    [Fact]
    public async Task EveryMetricLinksToItsCasesTab()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The strip's five destinations, including the D14 rule: Blocked
        // shares the Unidentified tab because there is no Blocked tab.
        foreach (var (key, tab) in new[]
        {
            ("not_ready", "not_ready"),
            ("review", "review"),
            ("held", "held"),
            ("unidentified", "unidentified"),
            ("blocked", "unidentified")
        })
        {
            Assert.Contains($"data-value=\"{key}\" href=\"/Cases?tab={tab}\"", html);
        }
    }

    [Fact]
    public async Task BlockedMetricCountsBlockedIntakeReceipts()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        await StoreReceiptAsync(services, IntakeDecision.BlockedIntake, "blocked.eml", now);

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var match = Regex.Match(
            html,
            "data-value=\"blocked\"[^>]*>[\\s\\S]*?metric-value\">(\\d+)</span>");
        Assert.True(match.Success, "Blocked metric markup not found.");
        Assert.Equal(1, int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task StoreReceiptAsync(
        IServiceProvider services,
        IntakeDecision decision,
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
                new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, Guid.NewGuid().ToString("N")),
                receivedAtUtc,
                receivedAtUtc,
                "test-actor",
                decision,
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
