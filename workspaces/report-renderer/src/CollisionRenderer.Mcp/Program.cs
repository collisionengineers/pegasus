using CollisionRenderer.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Chromium is provisioned lazily on the first render (see BrowserBootstrap.EnsureChromium),
// not at startup — so the server answers initialize/tools/list immediately and never makes
// the MCP client wait on a ~150 MB download during connection. install_browser pre-warms it.

var builder = Host.CreateApplicationBuilder(args);

// stdout is the JSON-RPC channel for stdio MCP; every log line MUST go to stderr or
// it corrupts the protocol stream. Clear the default providers and pin console to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// One Chromium engine + one renderer for the process. The engine is registered on its own
// so render_health can probe it and report how the browser resolved (bundled shell vs the
// system Edge/Chrome fallback); the renderer wraps the SAME instance (ownsEngine: false —
// the DI container disposes the engine singleton on shutdown), so the probe's warm browser
// is the one every render reuses.
builder.Services.AddSingleton<CollisionRenderer.Core.Rendering.IPdfEngine>(
    _ => new CollisionRenderer.Core.Rendering.ChromiumPdfEngine());
builder.Services.AddSingleton<IDocumentRenderer>(
    sp => CollisionRendererFactory.CreateRenderer(
        sp.GetRequiredService<CollisionRenderer.Core.Rendering.IPdfEngine>()));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
