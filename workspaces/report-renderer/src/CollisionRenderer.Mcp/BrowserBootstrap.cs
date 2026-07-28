namespace CollisionRenderer.Mcp;

/// <summary>
/// Chromium provisioning for the stdio host. The render engine drives headless
/// Chromium through Playwright (<see cref="Microsoft.Playwright"/>) for <c>PdfAsync</c>.
///
/// The Windows <c>.mcpb</c> now BUNDLES Chromium's headless shell
/// (<c>chromium_headless_shell-&lt;rev&gt;</c>, the smallest browser that still supports
/// <c>PdfAsync</c>) under the <c>PLAYWRIGHT_BROWSERS_PATH</c> the manifest points at,
/// and the manifest sets <c>COLLISIONRENDERER_SKIP_BROWSER_INSTALL=1</c> — so rendering
/// works straight from the bundle with no runtime download. The on-demand
/// <c>--only-shell</c> install below is a DORMANT fallback for non-bundled/dev runs;
/// <c>install_browser</c> remains a manual escape hatch.
/// </summary>
public static class BrowserBootstrap
{
    private static readonly object Gate = new();
    private static bool _ensured;

    /// <summary>
    /// Install Chromium only when it is not already present, at most once per process.
    /// Called lazily on the first render so the cost lands there, not at startup. Best-effort:
    /// a transient failure (e.g. offline) is logged to stderr and the render then surfaces a
    /// clearer Playwright error; <c>install_browser</c> can retry explicitly.
    /// </summary>
    public static void EnsureChromium()
    {
        if (_ensured)
        {
            return;
        }

        lock (Gate)
        {
            if (_ensured)
            {
                return;
            }

            try
            {
                _ensured = RunEnsureOnce(SkipInstall, ChromiumPresent, Install, Console.Error.WriteLine);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[collisionrenderer-mcp] Chromium bootstrap skipped: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// One bootstrap attempt; returns whether the "ensured" latch may be set. Latching is
    /// only correct when no install is needed (skip flag / already present) or the install
    /// verifiably succeeded — a FAILED install must stay retryable, otherwise every later
    /// render in the session fails instantly even after connectivity returns.
    /// Extracted (with injected deps) so the latch policy is unit-testable without running
    /// a real <c>playwright install</c>.
    /// </summary>
    internal static bool RunEnsureOnce(
        Func<bool> skipInstall,
        Func<bool> chromiumPresent,
        Func<int> install,
        Action<string> log)
    {
        // The .mcpb bundles the headless shell and sets this opt-out, so the
        // download path is inert. It also guarantees no SILENT network fetch if
        // the bundle is incomplete: the render then fails fast with a clear
        // Playwright error instead of stalling on a ~150 MB download.
        // (install_browser still calls Install() directly as a manual override.)
        if (skipInstall())
        {
            return true;
        }

        if (!chromiumPresent())
        {
            log("[collisionrenderer-mcp] Chromium not found; installing headless shell (first run)…");
            var code = install();
            log($"[collisionrenderer-mcp] Chromium install exited with code {code}.");

            if (code != 0 && !chromiumPresent())
            {
                log("[collisionrenderer-mcp] Chromium install did not complete; will retry on the next render "
                    + "(or run install_browser manually).");
                return false;
            }
        }

        return true;
    }

    /// <summary>Run <c>playwright install chromium --only-shell</c>; returns the CLI exit code.</summary>
    /// <remarks>
    /// Playwright's install spawns the bundled node driver with INHERITED stdio and streams a
    /// download progress bar (<c>Chromium … |■■■|</c>) to stdout. For an stdio MCP server stdout
    /// IS the JSON-RPC channel, so that output corrupts the protocol (the host logs
    /// <c>Unexpected token '|'/'C' … is not valid JSON</c>). We point the process's stdout handle
    /// at stderr for the duration of the install so the child inherits stderr; the MCP SDK's
    /// JSON-RPC writer holds the ORIGINAL stdout stream (opened at startup) and is unaffected.
    /// Restored in <c>finally</c>. Win32-only (the connector ships win32) via SetStdHandle.
    /// </remarks>
    public static int Install()
    {
        // The stdout-handle swap below is process-global state; serialise every caller
        // (EnsureChromium's locked path AND the install_browser tool, which calls in
        // directly) so two overlapping installs can't interleave their save/restore
        // sequences and leak installer output into the JSON-RPC channel. Monitor is
        // re-entrant, so the EnsureChromium -> Install path taking Gate twice is fine.
        lock (Gate)
        {
            IntPtr savedStdout = GetStdHandle(StdOutputHandle);
            try
            {
                // Redirect native stdout -> stderr so the install child's progress bar can never
                // reach the JSON-RPC channel. GetStdHandle/SetStdHandle are no-ops we tolerate
                // failing (best-effort) on non-Windows / detached-console hosts.
                IntPtr stderrHandle = GetStdHandle(StdErrorHandle);
                if (stderrHandle != IntPtr.Zero && stderrHandle != InvalidHandle)
                {
                    SetStdHandle(StdOutputHandle, stderrHandle);
                }

                return Microsoft.Playwright.Program.Main(new[] { "install", "chromium", "--only-shell" });
            }
            finally
            {
                if (savedStdout != IntPtr.Zero && savedStdout != InvalidHandle)
                {
                    SetStdHandle(StdOutputHandle, savedStdout);
                }
            }
        }
    }

    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private static readonly IntPtr InvalidHandle = new(-1);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

    internal static bool SkipInstall() =>
        Environment.GetEnvironmentVariable("COLLISIONRENDERER_SKIP_BROWSER_INSTALL") == "1";

    internal static bool ChromiumPresent()
    {
        var root = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (string.IsNullOrWhiteSpace(root))
        {
            // Playwright's default cache: %LOCALAPPDATA%\ms-playwright on Windows.
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ms-playwright");
        }

        if (!Directory.Exists(root))
        {
            return false;
        }

        // Accept a full Chromium build OR the bundled headless shell
        // (chromium_headless_shell-<rev>) — the latter is what the .mcpb ships and what
        // a headless PdfAsync launch resolves to. The directory NAME is the stable
        // signal (the shell binary's filename varies across Playwright versions).
        return Directory.EnumerateDirectories(root).Any(d =>
        {
            var name = Path.GetFileName(d);
            return name.StartsWith("chromium-", StringComparison.Ordinal)
                || name.StartsWith("chromium_headless_shell-", StringComparison.Ordinal);
        });
    }
}
