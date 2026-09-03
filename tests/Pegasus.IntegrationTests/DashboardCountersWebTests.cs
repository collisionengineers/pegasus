using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.IntegrationTests;

/// <summary>
/// UIIMP-008: the Work Centre's five metrics are page-queried figures, each
/// an exact link to the Cases tab behind it. Blocked is the real Blocked
/// intake count and links to the Unidentified tab, where Blocked intake rows
/// carry their own chip (decision D14) — never a fixture number. The file
/// also keeps PLAT-012's mailbox-channel guard, whose rule outlived the tile
/// it was written against.
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

    /// <summary>
    /// UIIMP-008: a link whose `asp-page` names a page that does not exist
    /// renders `href=""` — valid HTML, so nothing failed. That shipped three
    /// dead controls on this page, and the committed snapshot recorded it
    /// verbatim without any gate objecting.
    ///
    /// This is the class guard: whatever the Work Centre draws, no anchor may
    /// carry an empty href. The leading space matters — it matches a real
    /// `href` attribute and not `data-workspace-href` or `data-download-href`,
    /// whose names end in "href" and which are legitimately empty when the page
    /// is not a record.
    /// </summary>
    [Fact]
    public async Task TheWorkCentreRendersNoEmptyLink()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(" href=\"\"", html, StringComparison.Ordinal);
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

    /// <summary>
    /// PLAT-012: "Received today" counts mailbox-channel intake only — a
    /// manual upload is a different intake channel entirely and must not move
    /// it. The Work Centre port removed the E-mail activity tile this guard
    /// used to scrape, but <see cref="IDashboardQueries"/> still runs the
    /// channel filter on every load, so the guard reads the query where the
    /// rule actually lives instead of being dropped with the tile. It retires
    /// with the property under PLAT-058.
    /// </summary>
    [Fact]
    public async Task ReceivedTodayCountsMailboxChannelOnlyNotManualUploads()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        // One mailbox receipt (should count) and one manual-upload receipt
        // (must not) — both received "now", well inside the day the count is
        // taken from.
        await StoreReceiptAsync(services, IntakeDecision.CaseCreated, "instruction.eml", now);
        await StoreReceiptAsync(
            services,
            IntakeDecision.CaseCreated,
            "photo.jpg",
            now,
            IntakeSourceChannel.ManualUpload);

        var counts = await services.GetRequiredService<IDashboardQueries>()
            .GetMailActivityCountsAsync(now.AddHours(-1), CancellationToken.None);

        Assert.Equal(1, counts.ReceivedToday);
    }

    private static async Task StoreReceiptAsync(
        IServiceProvider services,
        IntakeDecision decision,
        string sourceFileName,
        DateTimeOffset receivedAtUtc,
        IntakeSourceChannel channel = IntakeSourceChannel.Mailbox)
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
