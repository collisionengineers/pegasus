using System.Reflection;
using Microsoft.Playwright;

namespace CollisionRenderer.Core.Rendering;

/// <summary>Turns a fully composed HTML document into PDF bytes.</summary>
public interface IPdfEngine : IAsyncDisposable
{
    Task<byte[]> RenderHtmlToPdfAsync(string html, PdfPageSettings settings, CancellationToken ct = default);
    int CountPages(byte[] pdf);
    string EngineVersion { get; }
}

/// <summary>
/// Headless-Chromium PDF engine (via Playwright). Chosen because it renders the
/// brand's existing CSS design system faithfully, supports CSS paged-media
/// (repeating table headers, break-inside, running header/footer), and runs the
/// same on a Windows desktop and a Linux container. The browser is launched once
/// and reused across renders.
/// </summary>
public sealed class ChromiumPdfEngine : IPdfEngine
{
    private static readonly string CoreVersion =
        typeof(ChromiumPdfEngine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ChromiumPdfEngine).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string? _channelPin;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private BrowserResolution? _resolution;
    private bool _disposed;

    /// <param name="channelPin">
    /// Optional browser pin (<c>bundled</c>/<c>msedge</c>/<c>chrome</c>); when null the
    /// <c>COLLISIONRENDERER_BROWSER_CHANNEL</c> env var is consulted at launch time.
    /// </param>
    public ChromiumPdfEngine(string? channelPin = null)
    {
        _channelPin = channelPin;
    }

    public string EngineVersion => $"CollisionRenderer/{CoreVersion}; engine=chromium-playwright";

    /// <summary>How the last launch attempt resolved (null until a launch has been tried).</summary>
    public BrowserResolution? Resolution => _resolution;

    public async Task<byte[]> RenderHtmlToPdfAsync(string html, PdfPageSettings s, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var browser = await GetBrowserAsync().ConfigureAwait(false);
        var page = await browser.NewPageAsync().ConfigureAwait(false);

        // Closing the page aborts an in-flight SetContent/Pdf call so the token is honoured.
        using var registration = ct.Register(() =>
        {
            try { _ = page.CloseAsync(); } catch { /* page already closing */ }
        });

        try
        {
            await page.SetContentAsync(html, new PageSetContentOptions
            {
                WaitUntil = WaitUntilState.Load,
            }).ConfigureAwait(false);

            var hasFurniture = !(string.IsNullOrEmpty(s.HeaderHtml) && string.IsNullOrEmpty(s.FooterHtml));

            return await page.PdfAsync(new PagePdfOptions
            {
                Format = s.Format,
                PrintBackground = s.PrintBackground,
                PreferCSSPageSize = false,
                DisplayHeaderFooter = hasFurniture,
                HeaderTemplate = string.IsNullOrEmpty(s.HeaderHtml) ? "<span></span>" : s.HeaderHtml,
                FooterTemplate = string.IsNullOrEmpty(s.FooterHtml) ? "<span></span>" : s.FooterHtml,
                Margin = new Margin
                {
                    Top = s.MarginTop,
                    Right = s.MarginRight,
                    Bottom = s.MarginBottom,
                    Left = s.MarginLeft,
                },
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        finally
        {
            try { await page.CloseAsync().ConfigureAwait(false); } catch { /* already closed */ }
        }
    }

    public int CountPages(byte[] pdf) => PdfPageCounter.Count(pdf);

    private async Task<IBrowser> GetBrowserAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_browser is { IsConnected: true })
        {
            return _browser;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Drop a crashed/disconnected browser so a transient failure can recover.
            if (_browser is { IsConnected: false })
            {
                try { await _browser.CloseAsync().ConfigureAwait(false); } catch { /* already gone */ }
                _browser = null;
            }

            if (_browser is null)
            {
                try
                {
                    _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Driver acquisition (not browser launch): the '.playwright' node driver
                    // that ships next to the executable is missing or unreadable.
                    throw new InvalidOperationException(
                        "The Playwright driver could not be started — the '.playwright' folder that ships "
                        + "next to the renderer executable is missing or incomplete. Update or reinstall the "
                        + "Document Renderer extension (dev machines: rebuild, the driver is copied on build). "
                        + $"Underlying error: {FirstLine(ex.Message)}", ex);
                }

                // Try the bundled headless shell first, then the system Edge/Chrome channels,
                // so a broken or missing bundled browser degrades to a browser that is already
                // on the machine instead of failing the render outright.
                var attempts = new List<string>();
                var pin = _channelPin ?? Environment.GetEnvironmentVariable("COLLISIONRENDERER_BROWSER_CHANNEL");
                foreach (var candidate in BrowserLaunchPlan.Build(pin))
                {
                    try
                    {
                        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                        {
                            Headless = true,
                            Channel = candidate.Channel,
                            Args = new[] { "--no-sandbox", "--font-render-hinting=none" },
                        }).ConfigureAwait(false);
                        _resolution = new BrowserResolution(candidate.Kind, candidate.Channel, attempts.ToArray());
                        break;
                    }
                    catch (PlaywrightException ex)
                    {
                        attempts.Add($"{candidate.Kind}: {FirstLine(ex.Message)}");
                    }
                }

                if (_browser is null)
                {
                    _resolution = new BrowserResolution("missing", null, attempts.ToArray());
                    throw new InvalidOperationException(BuildLaunchFailureMessage(attempts));
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        return _browser!;
    }

    /// <summary>First line of a (typically multi-line) Playwright error, for compact diagnostics.</summary>
    private static string FirstLine(string message)
    {
        var idx = message.IndexOf('\n');
        return (idx < 0 ? message : message[..idx]).TrimEnd('\r').Trim();
    }

    private static string BuildLaunchFailureMessage(IReadOnlyList<string> attempts)
    {
        var browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        var pathNote = string.IsNullOrWhiteSpace(browsersPath)
            ? "(not set — Playwright default cache)"
            : browsersPath;

        return
            "No Chromium-based browser could be launched for PDF rendering. Attempts: "
            + (attempts.Count > 0 ? string.Join(" | ", attempts) : "(none)") + ". "
            + $"Bundled browser directory (PLAYWRIGHT_BROWSERS_PATH): {pathNote}. "
            + "The headless shell normally ships inside the Document Renderer extension — update or "
            + "reinstall the extension, or run the install_browser tool once. Dev machines: run "
            + "'playwright install chromium' from a built project folder. "
            + "Set COLLISIONRENDERER_BROWSER_CHANNEL=msedge|chrome to pin the system browser.";
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_browser is not null)
        {
            try { await _browser.CloseAsync().ConfigureAwait(false); } catch { /* already closing */ }
            _browser = null;
        }

        _playwright?.Dispose();
        _gate.Dispose();
    }
}
