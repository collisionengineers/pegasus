using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class UploadStatusRefreshBrowserTests
{
    [Fact]
    public async Task ReturningToAHiddenStatusPageReloadsImmediately()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        var loads = 0;
        var reloaded = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await support.Page.AddInitScriptAsync(
            """
            window.__pageHidden = false;
            Object.defineProperty(document, 'hidden', {
                configurable: true,
                get: () => window.__pageHidden
            });
            window.__setPageHidden = hidden => {
                window.__pageHidden = hidden;
                document.dispatchEvent(new Event('visibilitychange'));
            };
            """);
        await support.Page.RouteAsync("**/refresh-probe", async route =>
        {
            var load = Interlocked.Increment(ref loads);

            await route.FulfillAsync(new()
            {
                ContentType = "text/html",
                Body =
                    "<main data-auto-refresh=\"60000\"></main>" +
                    "<script src=\"/js/site.js\"></script>"
            });
            if (load > 1)
            {
                reloaded.TrySetResult();
            }
        });

        await support.GoToAsync("/refresh-probe");
        await support.Page.EvaluateAsync("window.__setPageHidden(true)");
        await support.Page.WaitForTimeoutAsync(100);

        Assert.Equal(1, loads);

        await support.Page.EvaluateAsync(
            "setTimeout(() => window.__setPageHidden(false), 0)");
        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, loads);
    }
}
