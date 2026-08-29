using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

/// <summary>
/// The Upload page's per-file rows (§1.10): one .file-row per selected file
/// (not the crammed single line this replaced), the drawn native progress bar
/// — indeterminate, because a single POST stores the whole batch — a shared
/// "uploading" state while that POST is in flight (the honest bound), and a
/// "stored" tick once the response proves the whole batch is durable, before
/// the page navigates on to the status surface.
/// </summary>
[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class UploadRowsBrowserTests
{
    [Fact]
    public async Task SelectingMultipleFilesRendersOneRowPerFile()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        await support.GoToAsync("/Upload");

        var result = await support.Page.EvaluateAsync<RowsResult>(
            """
            async () => {
                function makeFile(name, content, type) {
                    return new File([content], name, { type });
                }
                const dt = new DataTransfer();
                dt.items.add(makeFile('one.pdf', 'a'.repeat(2000), 'application/pdf'));
                dt.items.add(makeFile('two.jpg', 'b'.repeat(3000), 'image/jpeg'));

                const zone = document.querySelector('[data-dropzone]');
                const input = zone.querySelector('input[type="file"]');
                input.files = dt.files;
                input.dispatchEvent(new Event('change', { bubbles: true }));
                await new Promise(resolve => setTimeout(resolve, 20));

                const rows = Array.from(document.querySelectorAll('.file-row'));
                return {
                    rowCount: rows.length,
                    names: rows.map(row => row.querySelector('strong').textContent),
                    sizes: rows.map(row => row.querySelector('small').textContent),
                    progressCount: rows.filter(row => row.querySelector('progress.progress')).length,
                    determinateCount: rows.filter(
                        row => row.querySelector('progress.progress').hasAttribute('value')).length,
                    visibleProgressCount: rows.filter(
                        row => !row.querySelector('progress.progress').hidden).length,
                    readoutText: document.querySelector('[data-dropzone-file]').textContent
                };
            }
            """);

        Assert.Equal(2, result.RowCount);
        Assert.Equal(["one.pdf", "two.jpg"], result.Names);
        Assert.All(result.Sizes, size => Assert.Contains("KB", size, StringComparison.Ordinal));
        // The drawn progress bar is a real <progress>, one per row, and it is
        // indeterminate: a single POST stores the whole batch, so no row may
        // carry a fraction the page cannot know. Nothing shows until a
        // submission is actually under way.
        Assert.Equal(2, result.ProgressCount);
        Assert.Equal(0, result.DeterminateCount);
        Assert.Equal(0, result.VisibleProgressCount);
        // The old readout crammed "name (size)name (size)" onto one line —
        // this is the exact shape that reproduces, so its absence is the
        // regression guard for "the files leaking" (the operator's words).
        Assert.DoesNotContain("(2 KB)(3 KB)", result.ReadoutText.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
    }

    [Fact]
    public async Task SubmittingShowsEveryRowUploadingTogetherThenNavigatesOnSuccess()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        await support.GoToAsync("/Upload");

        // Hold the POST response back so the in-flight "uploading" state is
        // observable rather than racing straight past it.
        var release = new TaskCompletionSource();
        await support.Page.RouteAsync("**/Upload", async route =>
        {
            if (route.Request.Method != "POST")
            {
                await route.ContinueAsync();
                return;
            }

            await release.Task;
            await route.ContinueAsync();
        });

        await support.Page.EvaluateAsync(
            """
            () => {
                function makeFile(name, content, type) {
                    return new File([content], name, { type });
                }
                const dt = new DataTransfer();
                dt.items.add(makeFile('one.pdf', 'a'.repeat(2000), 'application/pdf'));
                dt.items.add(makeFile('two.jpg', 'b'.repeat(3000), 'image/jpeg'));
                const input = document.querySelector('[data-dropzone] input[type="file"]');
                input.files = dt.files;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
            """);
        await support.Page.GetByRole(AriaRole.Button, new() { Name = "Upload", Exact = true }).ClickAsync();

        await support.Page.Locator("[data-file-row-status][data-state='uploading']").First
            .WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var uploadingCount = await support.Page.Locator("[data-file-row-status][data-state='uploading']").CountAsync();
        Assert.Equal(2, uploadingCount);
        Assert.True(await support.Page.Locator("[data-dropzone-file].is-refreshing").IsVisibleAsync());
        // Every row shows the drawn bar while the one POST is in flight, and
        // every one of them is still indeterminate.
        Assert.Equal(2, await support.Page.Locator(".file-row progress.progress:visible").CountAsync());
        Assert.Equal(0, await support.Page.Locator(".file-row progress.progress[value]").CountAsync());

        release.SetResult();
        await support.Page.WaitForURLAsync(url => url.Contains("/Upload/Group/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/Upload/Status/", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class RowsResult
    {
        public int RowCount { get; set; }
        public string[] Names { get; set; } = [];
        public string[] Sizes { get; set; } = [];
        public int ProgressCount { get; set; }
        public int DeterminateCount { get; set; }
        public int VisibleProgressCount { get; set; }
        public string ReadoutText { get; set; } = string.Empty;
    }
}
