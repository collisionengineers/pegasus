using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

/// <summary>
/// Reproduces the operator-reported production defect: dropping files onto
/// the Upload dropzone does nothing, while the native picker (the "Choose
/// files" button, which fires the same underlying <c>change</c> event) works.
///
/// Two faithful drops directly on the dropzone (a page-level synthetic
/// <c>DragEvent</c> and a CDP-level native drop with real file paths and real
/// hit-tested coordinates) both succeed on this code, so they were not what
/// broke in production. The actual cause: site.js bound drag/drop only to the
/// small dashed rectangle. Any drop that lands anywhere else in the window —
/// the heading, a panel border, released a beat early — was unhandled, so
/// Chrome's default action navigated the whole tab to the dropped file. The
/// last two tests below reproduce and guard exactly that.
/// </summary>
[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class UploadDropzoneBrowserTests
{
    [Fact]
    public async Task DroppingMultipleFilesPopulatesTheInputAndReadout()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);

        await support.GoToAsync("/Upload");

        var result = await support.Page.EvaluateAsync<DropResult>(
            """
            async () => {
                function makeFile(name, content, type) {
                    return new File([content], name, { type });
                }
                const dt = new DataTransfer();
                dt.items.add(makeFile('one.jpg', 'aaaa', 'image/jpeg'));
                dt.items.add(makeFile('two.pdf', 'bbbb', 'application/pdf'));

                const zone = document.querySelector('[data-dropzone]');
                const input = zone.querySelector('input[type="file"]');
                const readout = zone.querySelector('[data-dropzone-file]');

                function fire(type) {
                    const event = new DragEvent(type, {
                        bubbles: true,
                        cancelable: true,
                        dataTransfer: dt
                    });
                    zone.dispatchEvent(event);
                }

                fire('dragenter');
                fire('dragover');
                fire('drop');

                await new Promise(resolve => setTimeout(resolve, 50));

                return {
                    inputFileCount: input.files ? input.files.length : -1,
                    readoutHidden: readout.hidden,
                    readoutText: readout.textContent ?? '',
                    hasFileClass: zone.classList.contains('has-file')
                };
            }
            """);

        Assert.Equal(2, result.InputFileCount);
        Assert.False(result.ReadoutHidden);
        Assert.Contains("one.jpg", result.ReadoutText, StringComparison.Ordinal);
        Assert.Contains("two.pdf", result.ReadoutText, StringComparison.Ordinal);
        Assert.True(result.HasFileClass);
    }

    /// <summary>
    /// The same reproduction, but at the CDP/native level: a real OS-style
    /// drop delivers real file paths at real viewport coordinates rather than
    /// a page-level synthetic <c>DragEvent</c> dispatched directly on the
    /// element. This is closer to how a genuine drag from Explorer/Finder
    /// reaches the page, including the browser's own drop-target hit test.
    /// </summary>
    [Fact]
    public async Task NativeCdpDropOnTheDashedZonePopulatesTheInputAndReadout()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        await support.GoToAsync("/Upload");

        var result = await DropOnAsync(support, "[data-dropzone]");

        Assert.Equal(2, result.InputFileCount);
        Assert.False(result.ReadoutHidden);
        Assert.Contains("one.jpg", result.ReadoutText, StringComparison.Ordinal);
        Assert.Contains("two.pdf", result.ReadoutText, StringComparison.Ordinal);
        Assert.True(result.HasFileClass);
    }

    /// <summary>
    /// The dashed rectangle is a small target on a real drag. The effective
    /// target is the whole panel it sits in: a drop on the panel's own button
    /// row — inside the panel, outside the dashed area — must still land the
    /// files, not silently do nothing.
    /// </summary>
    [Fact]
    public async Task NativeCdpDropOnThePanelOutsideTheDashedZoneStillPopulatesTheInput()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        await support.GoToAsync("/Upload");

        var result = await DropOnAsync(support, ".button-row");

        Assert.Equal(2, result.InputFileCount);
        Assert.False(result.ReadoutHidden);
        Assert.True(result.HasFileClass);
    }

    /// <summary>
    /// The operator-reported failure: a drop that lands off the panel
    /// entirely (the page heading, above the form) must not be left to the
    /// browser's default action, which navigates the tab to the dropped file
    /// and loses the page. The drop is swallowed; nothing is stored.
    /// </summary>
    [Fact]
    public async Task NativeCdpDropOffThePanelDoesNotNavigateAwayFromUpload()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        await support.GoToAsync("/Upload");
        var uploadUrl = support.Page.Url;

        var result = await DropOnAsync(support, ".page-header");

        Assert.Equal(uploadUrl, support.Page.Url);
        Assert.True(await support.Page.Locator("[data-dropzone]").IsVisibleAsync());
        Assert.Equal(0, result.InputFileCount);
    }

    private static async Task<DropResult> DropOnAsync(BrowserTestSupport support, string targetSelector)
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "Pegasus.DropzoneBrowserTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var first = Path.Combine(directory, "one.jpg");
        var second = Path.Combine(directory, "two.pdf");
        await File.WriteAllTextAsync(first, "aaaa");
        await File.WriteAllTextAsync(second, "bbbb");

        try
        {
            var cdp = await support.Context.NewCDPSessionAsync(support.Page);
            var box = await support.Page.Locator(targetSelector).BoundingBoxAsync()
                ?? throw new InvalidOperationException($"'{targetSelector}' has no bounding box.");
            var x = box.X + (box.Width / 2);
            var y = box.Y + (box.Height / 2);

            async Task DispatchAsync(string type)
            {
                await cdp.SendAsync("Input.dispatchDragEvent", new Dictionary<string, object>
                {
                    ["type"] = type,
                    ["x"] = x,
                    ["y"] = y,
                    ["data"] = new Dictionary<string, object>
                    {
                        ["items"] = Array.Empty<object>(),
                        ["files"] = new[] { first, second },
                        ["dragOperationsMask"] = 1
                    }
                });
            }

            await DispatchAsync("dragEnter");
            await DispatchAsync("dragOver");
            await DispatchAsync("drop");
            await support.Page.WaitForTimeoutAsync(100);

            return await support.Page.EvaluateAsync<DropResult>(
                """
                () => {
                    const zone = document.querySelector('[data-dropzone]');
                    const input = zone.querySelector('input[type="file"]');
                    const readout = zone.querySelector('[data-dropzone-file]');
                    return {
                        inputFileCount: input.files ? input.files.length : -1,
                        readoutHidden: readout.hidden,
                        readoutText: readout.textContent ?? '',
                        hasFileClass: zone.classList.contains('has-file')
                    };
                }
                """);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class DropResult
    {
        public int InputFileCount { get; set; }
        public bool ReadoutHidden { get; set; }
        public string ReadoutText { get; set; } = string.Empty;
        public bool HasFileClass { get; set; }
    }
}
