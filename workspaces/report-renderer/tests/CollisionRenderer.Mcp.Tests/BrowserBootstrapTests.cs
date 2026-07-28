using System;
using System.IO;
using CollisionRenderer.Mcp;
using Xunit;

namespace CollisionRenderer.Mcp.Tests;

/// <summary>
/// Guards the bundled-Chromium detection logic: the manifest's skip flag must be honoured,
/// and both directory-name shapes Playwright uses (full build and headless shell) must be
/// recognised, since the .mcpb ships only the shell. These tests mutate process-wide
/// environment variables, so they share one collection and restore state in finally blocks.
/// </summary>
[Collection("BrowserBootstrapEnv")]
public class BrowserBootstrapTests
{
    private const string SkipVar = "COLLISIONRENDERER_SKIP_BROWSER_INSTALL";
    private const string BrowsersPathVar = "PLAYWRIGHT_BROWSERS_PATH";

    [Fact]
    public void SkipInstall_TrueOnlyWhenEnvVarIsOne()
    {
        var saved = Environment.GetEnvironmentVariable(SkipVar);
        try
        {
            Environment.SetEnvironmentVariable(SkipVar, "1");
            Assert.True(BrowserBootstrap.SkipInstall());

            Environment.SetEnvironmentVariable(SkipVar, "0");
            Assert.False(BrowserBootstrap.SkipInstall());

            Environment.SetEnvironmentVariable(SkipVar, null);
            Assert.False(BrowserBootstrap.SkipInstall());
        }
        finally
        {
            Environment.SetEnvironmentVariable(SkipVar, saved);
        }
    }

    [Theory]
    [InlineData("chromium-1181", true)]
    [InlineData("chromium_headless_shell-1181", true)]
    [InlineData("firefox-1467", false)]
    public void ChromiumPresent_RecognisesBothChromiumDirectoryShapes(string dirName, bool expected)
    {
        var saved = Environment.GetEnvironmentVariable(BrowsersPathVar);
        var root = Path.Combine(Path.GetTempPath(), "cr-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, dirName));
            Environment.SetEnvironmentVariable(BrowsersPathVar, root);
            Assert.Equal(expected, BrowserBootstrap.ChromiumPresent());
        }
        finally
        {
            Environment.SetEnvironmentVariable(BrowsersPathVar, saved);
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void RunEnsureOnce_LatchesWithoutInstallWhenSkipFlagSet()
    {
        var installed = false;
        var latch = BrowserBootstrap.RunEnsureOnce(
            skipInstall: () => true,
            chromiumPresent: () => false,
            install: () => { installed = true; return 0; },
            log: _ => { });

        Assert.True(latch);
        Assert.False(installed);
    }

    [Fact]
    public void RunEnsureOnce_LatchesWhenAlreadyPresent()
    {
        var latch = BrowserBootstrap.RunEnsureOnce(
            skipInstall: () => false,
            chromiumPresent: () => true,
            install: () => throw new InvalidOperationException("must not install"),
            log: _ => { });

        Assert.True(latch);
    }

    [Fact]
    public void RunEnsureOnce_DoesNotLatchAFailedInstall()
    {
        // The regression: a failed install (offline first render) must stay retryable,
        // not fast-fail every later render in the session.
        var latch = BrowserBootstrap.RunEnsureOnce(
            skipInstall: () => false,
            chromiumPresent: () => false,
            install: () => 1,
            log: _ => { });

        Assert.False(latch);
    }

    [Fact]
    public void RunEnsureOnce_LatchesWhenInstallSucceeds()
    {
        var latch = BrowserBootstrap.RunEnsureOnce(
            skipInstall: () => false,
            chromiumPresent: () => false,
            install: () => 0,
            log: _ => { });

        Assert.True(latch);
    }

    [Fact]
    public void RunEnsureOnce_LatchesWhenInstallExitNonZeroButBrowserAppeared()
    {
        // Playwright's CLI can exit non-zero for cosmetic reasons after a good install;
        // presence on disk is the deciding signal.
        var present = false;
        var latch = BrowserBootstrap.RunEnsureOnce(
            skipInstall: () => false,
            chromiumPresent: () => present,
            install: () => { present = true; return 3; },
            log: _ => { });

        Assert.True(latch);
    }

    [Fact]
    public void ChromiumPresent_FalseWhenBrowsersPathMissingOrEmpty()
    {
        var saved = Environment.GetEnvironmentVariable(BrowsersPathVar);
        var root = Path.Combine(Path.GetTempPath(), "cr-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable(BrowsersPathVar, root);
            Assert.False(BrowserBootstrap.ChromiumPresent()); // directory does not exist

            Directory.CreateDirectory(root);
            Assert.False(BrowserBootstrap.ChromiumPresent()); // exists but empty
        }
        finally
        {
            Environment.SetEnvironmentVariable(BrowsersPathVar, saved);
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }
}
