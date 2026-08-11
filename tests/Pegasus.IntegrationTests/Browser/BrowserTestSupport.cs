using System.Net;
using Deque.AxeCore.Playwright;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

internal sealed class BrowserTestSupport : IAsyncDisposable
{
    private readonly IntakeWebApplicationFactory baseFactory;
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient applicationClient;
    private readonly WebApplication loopbackHost;
    private readonly IPlaywright playwright;
    private readonly IBrowser browser;

    private BrowserTestSupport(
        IntakeWebApplicationFactory baseFactory,
        WebApplicationFactory<Program> factory,
        HttpClient applicationClient,
        WebApplication loopbackHost,
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext context,
        IPage page,
        Uri baseAddress)
    {
        this.baseFactory = baseFactory;
        this.factory = factory;
        this.applicationClient = applicationClient;
        this.loopbackHost = loopbackHost;
        this.playwright = playwright;
        this.browser = browser;
        Context = context;
        Page = page;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

    public IBrowserContext Context { get; }

    public IPage Page { get; }

    public IServiceProvider Services => factory.Services;

    public static async Task<BrowserTestSupport> StartAsync(
        int width = 1280,
        int height = 720,
        ForcedColors forcedColors = ForcedColors.None,
        bool javaScriptEnabled = true,
        bool useIntegrationTestAuthentication = false,
        Action<IWebHostBuilder>? configureWebHost = null,
        CancellationToken cancellationToken = default)
    {
        var baseFactory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication,
            initializeDevelopmentOffline: true);
        WebApplicationFactory<Program> factory = configureWebHost is null
            ? baseFactory
            : baseFactory.WithWebHostBuilder(configureWebHost);
        var applicationClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var loopbackHost = builder.Build();
        loopbackHost.Run(context => ForwardAsync(applicationClient, context));
        await loopbackHost.StartAsync(cancellationToken);

        var addresses = loopbackHost.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The loopback browser host did not publish an address.");
        var baseAddress = new Uri(addresses.Addresses.Single(), UriKind.Absolute);

        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = true
            });
        var context = await browser.NewContextAsync(
            new BrowserNewContextOptions
            {
                BaseURL = baseAddress.AbsoluteUri,
                ColorScheme = ColorScheme.Light,
                ForcedColors = forcedColors,
                ReducedMotion = ReducedMotion.Reduce,
                JavaScriptEnabled = javaScriptEnabled,
                ViewportSize = new ViewportSize
                {
                    Width = width,
                    Height = height
                }
            });
        var page = await context.NewPageAsync();

        return new BrowserTestSupport(
            baseFactory,
            factory,
            applicationClient,
            loopbackHost,
            playwright,
            browser,
            context,
            page,
            baseAddress);
    }

    public async Task<IResponse> GoToAsync(string relativePath)
    {
        var response = await Page.GotoAsync(
            new Uri(BaseAddress, relativePath).AbsoluteUri,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

        return response ?? throw new InvalidOperationException($"The browser returned no response for '{relativePath}'.");
    }

    public async Task<IReadOnlyList<string>> FindAccessibilityViolationIdsAsync()
    {
        var result = await Page.RunAxe();
        return result.Violations?
            .Select(violation => violation.Id)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await browser.DisposeAsync();
        playwright.Dispose();
        await loopbackHost.StopAsync();
        await loopbackHost.DisposeAsync();
        applicationClient.Dispose();
        if (!ReferenceEquals(factory, baseFactory))
        {
            factory.Dispose();
        }
        baseFactory.Dispose();
    }

    private static async Task ForwardAsync(HttpClient applicationClient, HttpContext context)
    {
        var requestTarget = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        using var forwardedRequest = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            requestTarget);

        if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            forwardedRequest.Content = new StreamContent(context.Request.Body);
        }

        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!forwardedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                forwardedRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        using var forwardedResponse = await applicationClient.SendAsync(
            forwardedRequest,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);
        context.Response.StatusCode = (int)forwardedResponse.StatusCode;

        CopyHeaders(forwardedResponse.Headers, context.Response);
        CopyHeaders(forwardedResponse.Content.Headers, context.Response);
        context.Response.Headers.Remove("transfer-encoding");
        await forwardedResponse.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private static void CopyHeaders(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> source,
        HttpResponse destination)
    {
        foreach (var header in source)
        {
            destination.Headers[header.Key] = new StringValues(header.Value.ToArray());
        }
    }
}
