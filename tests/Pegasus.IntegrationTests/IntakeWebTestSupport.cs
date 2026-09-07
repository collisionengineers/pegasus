using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MimeKit;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.IntegrationTests.Support;

namespace Pegasus.IntegrationTests;

public sealed class IntakeWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly DateTimeOffset FixedUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private readonly string environment;
    private readonly bool? localIntakeEnabled;
    private readonly TimeProvider timeProvider;
    private readonly IIntakeArtifactStore? artifactStore;
    private readonly IInstructionExtractionPolicy? extractionPolicy;
    private readonly IMailClassificationPolicy? mailClassificationPolicy;
    private readonly IVrmRecognitionEngine? recognitionEngine;
    private readonly IResolveApprovedMailboxIdentity? approvedMailboxIdentityResolver;
    private readonly bool useIntegrationTestAuthentication;
    private readonly bool initializeDevelopmentOffline;
    private readonly LocalDbTestDatabase database;
    private readonly string workingDirectory = Path.Combine(
        Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));

    public IntakeWebApplicationFactory()
        : this("Development", true, useIntegrationTestAuthentication: false)
    {
    }

    internal IntakeWebApplicationFactory(TimeProvider timeProvider)
        : this("Development", true, timeProvider, useIntegrationTestAuthentication: false)
    {
    }

    internal IntakeWebApplicationFactory(
        bool useIntegrationTestAuthentication = false,
        bool initializeDevelopmentOffline = true)
        : this(
            "Development",
            true,
            useIntegrationTestAuthentication: useIntegrationTestAuthentication,
            initializeDevelopmentOffline: initializeDevelopmentOffline)
    {
    }

    internal IntakeWebApplicationFactory(
        string environment,
        bool? localIntakeEnabled,
        TimeProvider? timeProvider = null,
        IIntakeArtifactStore? artifactStore = null,
        IInstructionExtractionPolicy? extractionPolicy = null,
        bool useIntegrationTestAuthentication = false,
        bool initializeDevelopmentOffline = true,
        IVrmRecognitionEngine? recognitionEngine = null,
        IMailClassificationPolicy? mailClassificationPolicy = null,
        IResolveApprovedMailboxIdentity? approvedMailboxIdentityResolver = null)
    {
        this.environment = environment;
        this.localIntakeEnabled = localIntakeEnabled;
        this.timeProvider = timeProvider ?? new TestTimeProvider(FixedUtcNow);
        this.artifactStore = artifactStore;
        this.extractionPolicy = extractionPolicy;
        this.recognitionEngine = recognitionEngine;
        this.mailClassificationPolicy = mailClassificationPolicy;
        this.approvedMailboxIdentityResolver = approvedMailboxIdentityResolver;
        this.useIntegrationTestAuthentication = useIntegrationTestAuthentication;
        this.initializeDevelopmentOffline = initializeDevelopmentOffline;
        // Restored from the per-run template rather than migrated here: this
        // constructor is the suite's most-repeated database lifecycle.
        // CreateHost still runs DevelopmentOfflineInitialization, whose own
        // MigrateAsync then finds nothing to apply.
        database = LocalDbTestDatabase.CreateAsync().GetAwaiter().GetResult();
    }

    internal LocalDbTestDatabase Database => database;

    internal string ArtifactDirectory => Path.Combine(workingDirectory, "intake-artifacts");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting(
            "Features:LocalIntake",
            (localIntakeEnabled ?? false).ToString());
        builder.UseSetting(
            "Features:LocalDocumentCustody",
            environment.Equals("Development", StringComparison.OrdinalIgnoreCase).ToString());
        builder.UseSetting(
            "DocumentRequests:AcceptedLimitsVersion",
            "integration-fixture-v1");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = environment.Equals(
                    "Development",
                    StringComparison.OrdinalIgnoreCase)
                    ? "DevelopmentOffline"
                    : "Production",
                ["ConnectionStrings:Pegasus"] = database.ConnectionString,
                ["Intake:LocalArtifactPath"] = ArtifactDirectory,
                ["Features:LocalIntake"] = (localIntakeEnabled ?? false).ToString(),
                ["Features:LocalDocumentCustody"] = environment.Equals(
                    "Development",
                    StringComparison.OrdinalIgnoreCase).ToString(),
                ["Graph:TenantId"] = "858cf5b3-aa0a-47a6-9b40-4851fd0afa94",
                ["Graph:ChangeNotificationClientState"] = "integration-client-state",
                ["DocumentRequests:AcceptedLimitsVersion"] = "integration-fixture-v1",
                ["DocumentRequests:LimitsVersion"] = "integration-fixture-v1",
                ["DocumentRequests:LifetimeHours"] = "1",
                ["DocumentRequests:MaximumFileCount"] = "5",
                ["DocumentRequests:MaximumFileBytes"] = "1048576",
                ["DocumentRequests:MaximumRequestBytes"] = "5242880",
                ["DocumentRequests:RateLimit"] = "10",
                ["DocumentRequests:RateLimitWindowMinutes"] = "1",
                ["DocumentRequests:AllowedMediaTypes:0"] = "application/pdf",
                ["DocumentRequests:AllowedMediaTypes:1"] = "text/plain",
                ["DocumentRequests:AllowedMediaTypes:2"] = "image/jpeg",
                ["DocumentRequests:AllowedMediaTypes:3"] = "image/png",
                ["DocumentRequests:AllowedMediaTypes:4"] =
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            };

            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureServices(services =>
        {
            // Program.cs configures data protection only on the Production
            // branch, so a Development host would otherwise fall back to the
            // machine-global key ring under
            // %LOCALAPPDATA%\ASP.NET\DataProtection-Keys under one
            // discriminator — the suite's only genuinely shared OS resource
            // once hosts are built concurrently. ConfiguredWebApplicationFactory
            // already does this.
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            if (useIntegrationTestAuthentication)
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "IntegrationTest";
                    options.DefaultChallengeScheme = "IntegrationTest";
                }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthenticationHandler>("IntegrationTest", _ => { });
            }
            services.RemoveAll<TimeProvider>();
            // Browser/TestServer hosts exercise Web mutations without an
            // Azurite process. Production must compose the required queue
            // publishers, but this host replaces transport only after the
            // Core route tests have proved its exact-ID call contract.
            services.RemoveAll<ICommittedIntakeWorkPublisher>();
            services.AddScoped<ICommittedIntakeWorkPublisher, CommittedWorkPublisherDouble>();
            services.RemoveAll<ICommittedExternalWorkPublisher>();
            services.AddScoped<ICommittedExternalWorkPublisher, CommittedWorkPublisherDouble>();
            services.AddSingleton(timeProvider);
            services.TryAddScoped<IReadLogicalDocumentVersion>(_ =>
                RecordingLogicalDocumentVersionReader.Refusing());
            services.TryAddScoped<ICaseArtifactCustody>(sp =>
                new RecordingCaseArtifactCustody(
                    sp.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                    sp.GetRequiredService<TimeProvider>()));
            services.TryAddScoped<ICaseArtifactCustodyStatus>(sp =>
                (RecordingCaseArtifactCustody)sp.GetRequiredService<ICaseArtifactCustody>());
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PEGASUS_TEST_UI_CAPTURE_DIR")))
            {
                services.AddTransient<IStartupFilter, TestUiResponseCaptureStartupFilter>();
            }
            if (artifactStore is not null)
            {
                services.RemoveAll<IIntakeArtifactStore>();
                services.AddSingleton(artifactStore);
            }
            if (extractionPolicy is not null)
            {
                services.RemoveAll<IInstructionExtractionPolicy>();
                services.AddSingleton(extractionPolicy);
            }
            if (recognitionEngine is not null)
            {
                services.RemoveAll<IVrmRecognitionEngine>();
                services.AddSingleton(recognitionEngine);
            }
            if (mailClassificationPolicy is not null)
            {
                services.RemoveAll<IMailClassificationPolicy>();
                services.AddSingleton(mailClassificationPolicy);
            }
            if (approvedMailboxIdentityResolver is not null)
            {
                services.RemoveAll<IResolveApprovedMailboxIdentity>();
                services.AddSingleton(approvedMailboxIdentityResolver);
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        if (initializeDevelopmentOffline)
        {
            DevelopmentOfflineInitialization.InitializeAsync(scope.ServiceProvider)
                .GetAwaiter()
                .GetResult();
        }
        else
        {
            DevelopmentOfflineInitialization.MigrateAsync(scope.ServiceProvider)
                .GetAwaiter()
                .GetResult();
        }
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            if (disposing)
            {
                try
                {
                    database.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                finally
                {
                    if (Directory.Exists(workingDirectory))
                    {
                        Directory.Delete(workingDirectory, recursive: true);
                    }
                }
            }
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

internal sealed class IntegrationTestAuthenticationHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                DevelopmentOfflineIdentity.AdministratorId.ToString("D")),
            new Claim(ClaimTypes.Name, "integration-user"),
            new Claim("display_name", "Integration User")
        };
        if (Request.Headers.TryGetValue("X-Test-Roles", out var requestedRoles))
        {
            // ENG-002: a test that needs a specific staff role (e.g. Engineer)
            // names it; the default identity stays Administrator-only.
            foreach (var role in requestedRoles.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }
        else if (!Request.Headers.ContainsKey("X-Test-Roleless"))
        {
            claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
        }
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Redirect("/Account/SignIn?ReturnUrl=" + Uri.EscapeDataString(Request.PathBase + Request.Path + Request.QueryString));
        return Task.CompletedTask;
    }
}

internal static partial class IntakeWebDriver
{
    public static HttpClient CreateClient(IntakeWebApplicationFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

    public static async Task<UploadResult> UploadAsync(
        HttpClient client,
        GenuineCorpusSample sample,
        string? externalReceiptToken = null,
        CancellationToken cancellationToken = default) =>
        await UploadAsync(
            client,
            sample.UploadName,
            sample.MediaType,
            sample.Bytes,
            externalReceiptToken,
            cancellationToken);

    public static async Task<UploadResult> UploadAsync(
        HttpClient client,
        string uploadName,
        string mediaType,
        byte[] bytes,
        string? externalReceiptToken = null,
        CancellationToken cancellationToken = default)
    {
        var form = await GetUploadFormTokensAsync(client, cancellationToken);
        return await PostUploadAsync(
            client,
            form.AntiforgeryToken,
            uploadName,
            mediaType,
            bytes,
            externalReceiptToken ?? form.ExternalReceiptToken,
            cancellationToken);
    }

    public static async Task<UploadResult> UploadAndProcessAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        GenuineCorpusSample sample,
        string? externalReceiptToken = null,
        CancellationToken cancellationToken = default)
    {
        var upload = await UploadAsync(
            client,
            sample,
            externalReceiptToken,
            cancellationToken);
        return await ProcessQueuedAsync(factory, upload, cancellationToken);
    }

    public static async Task<UploadResult> UploadAndProcessAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        string uploadName,
        string mediaType,
        byte[] bytes,
        string? externalReceiptToken = null,
        CancellationToken cancellationToken = default)
    {
        var upload = await UploadAsync(
            client,
            uploadName,
            mediaType,
            bytes,
            externalReceiptToken,
            cancellationToken);
        return await ProcessQueuedAsync(factory, upload, cancellationToken);
    }

    /// <summary>
    /// Drains the upload's staged work through the Worker's own use cases and
    /// points the result at the received-item screen.
    /// </summary>
    /// <remarks>
    /// Every ingress stages pending work; the Web host never processes it. This
    /// stands in for the Worker timer and queue trigger with an immediate
    /// enqueuer. The result is pointed at <c>/Received/{id}</c> because that is
    /// what callers want next: the retained record of what arrived. Where the
    /// upload itself landed is asked with <see cref="Landing"/>.
    /// </remarks>
    public static async Task<UploadResult> ProcessQueuedAsync(
        WebApplicationFactory<Program> factory,
        UploadResult upload,
        CancellationToken cancellationToken = default)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        if (TryGetUploadGroupId(upload.Location, out var groupId))
        {
            var groups = services.GetRequiredService<IIntakeSubmissionGroupStore>();
            var group = await groups.GetAsync(groupId, cancellationToken)
                ?? throw new InvalidOperationException("The upload group was not persisted.");
            var first = Guid.Empty;
            foreach (var member in group.Members.OrderBy(item => item.Ordinal))
            {
                var memberEvaluation = await DrainStagedAsync(services, member.StagedReceiptId, cancellationToken);
                first = first == Guid.Empty ? memberEvaluation.ProcessedReceiptId : first;
            }

            return upload with
            {
                Location = new Uri($"/Received/{first:D}", UriKind.Relative),
                ProcessedReceiptId = first
            };
        }

        // The token the upload was posted under identifies its receipt exactly,
        // whatever the page did next. Where the redirect names a case it cannot
        // be trusted for this: an image set that joins an existing case lands on
        // that case, whose origin receipt is the instruction's, not the image's.
        var byToken = upload.ExternalReceiptToken is null
            ? null
            : await TryResolveByTokenAsync(services, upload.ExternalReceiptToken, cancellationToken);
        if (byToken is { } tokenReceiptId)
        {
            return upload with
            {
                Location = new Uri(
                    $"/Received/{tokenReceiptId:D}" + (IsDuplicateLanding(upload) ? "?duplicate=true" : string.Empty),
                    UriKind.Relative),
                ProcessedReceiptId = tokenReceiptId
            };
        }

        var landing = Landing(upload);
        var stagedReceiptId = landing.StagedReceiptId
            ?? throw new InvalidOperationException(
                $"The upload landed on '{upload.Location}', which names nothing that can be processed.");
        var evaluation = await DrainStagedAsync(services, stagedReceiptId, cancellationToken);
        var processedReceiptId = evaluation.ProcessedReceiptId;

        var detailLocation = $"/Received/{processedReceiptId:D}"
            + (landing.IsDuplicate ? "?duplicate=true" : string.Empty);
        return upload with
        {
            Location = new Uri(detailLocation, UriKind.Relative),
            ProcessedReceiptId = processedReceiptId
        };
    }

    private static bool TryGetUploadGroupId(Uri? location, out Guid id)
    {
        id = Guid.Empty;
        if (location is null)
        {
            return false;
        }

        var segments = location.OriginalString.Split('?', 2)[0]
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 3
            && string.Equals(segments[0], "Upload", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[1], "Group", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[2], out id);
    }

    /// <summary>
    /// The processed receipt an upload produced, found by the token it was
    /// posted under, or null where processing has not produced one yet.
    /// </summary>
    private static async Task<Guid?> TryResolveByTokenAsync(
        IServiceProvider services,
        string externalReceiptToken,
        CancellationToken cancellationToken)
    {
        // The page canonicalises the token, so a caller that posted it in a
        // different case is looking for the canonical form.
        var token = Guid.TryParseExact(externalReceiptToken, "N", out var parsed)
            ? parsed.ToString("N")
            : externalReceiptToken;
        var store = services.GetRequiredService<IIntakeReceiptStore>();
        var receipt = await store.FindBySourceIdentityAsync(
            new(IntakeSourceChannel.ManualUpload, token),
            cancellationToken);
        return receipt?.Id;
    }

    private static bool IsDuplicateLanding(UploadResult upload) =>
        upload.StatusCode == HttpStatusCode.Redirect
        && upload.Location is not null
        && Landing(upload).IsDuplicate;

    /// <summary>
    /// What the upload's redirect names: the staged receipt on
    /// <c>/Upload/Status/{id}</c>, and whether the page was told it is a replay.
    /// </summary>
    public static UploadLanding Landing(UploadResult result)
    {
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.NotNull(result.Location);
        var location = result.Location!.OriginalString;
        var path = location.Split('?', 2)[0];
        var query = ParseLocationQuery(result);
        var isDuplicate = query.TryGetValue("duplicate", out var duplicateValues)
            && bool.TryParse(duplicateValues.SingleOrDefault(), out var parsedDuplicate)
            && parsedDuplicate;
        var lastSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        var stagedReceiptId =
            path.StartsWith("/Upload/Status/", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(lastSegment, out var pathId)
                ? pathId
                : (Guid?)null;
        return new(stagedReceiptId, isDuplicate);
    }

    /// <summary>
    /// The receipt an upload produced, read from where the upload lands.
    /// </summary>
    public static Guid QueuedReceiptId(UploadResult result) => ReceiptId(result);

    /// <summary>
    /// The one receipt in the database.
    /// </summary>
    /// <remarks>
    /// A file that could not be read has no next screen to go to, so the
    /// upload reports the failure on the page the operator is still looking at
    /// and the redirect that used to carry the identifier is gone. The receipt
    /// is still retained, and for a single-upload test this is how to find it.
    /// </remarks>
    public static async Task<Guid> SoleReceiptIdAsync(
        IntakeWebApplicationFactory factory,
        CancellationToken cancellationToken = default)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var all = await receipts.ListAsync(null, 1, 100, cancellationToken);
        return Assert.Single(all.Items).Id;
    }

    /// <summary>GETs a page, asserts 200, and returns its HTML.</summary>
    public static async Task<string> GetHtmlAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        await CaptureReceiptImagesAsync(client, html, cancellationToken);
        return html;
    }

    /// <summary>
    /// Under a Test UI capture, a page's receipt images are fetched with the
    /// page so its offline snapshot shows those receipts and never another's.
    /// </summary>
    private static async Task CaptureReceiptImagesAsync(
        HttpClient client,
        string html,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PEGASUS_TEST_UI_CAPTURE_DIR")))
        {
            return;
        }

        var urls = ReceiptImageUrlRegex().Matches(html)
            .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var url in urls)
        {
            using var image = await client.GetAsync(url, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        }
    }

    /// <summary>The inline receipt-image sources a rendered page shows.</summary>
    [GeneratedRegex("<img[^>]+src=\"(/Received/[^\"/]+/Image)\"", RegexOptions.IgnoreCase)]
    internal static partial Regex ReceiptImageUrlRegex();

    public static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        CancellationToken cancellationToken = default) =>
        (await GetUploadFormTokensAsync(client, cancellationToken)).AntiforgeryToken;

    public static async Task<UploadFormTokens> GetUploadFormTokensAsync(
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        using var formPage = await client.GetAsync("/Upload", cancellationToken);
        formPage.EnsureSuccessStatusCode();
        var html = await formPage.Content.ReadAsStringAsync(cancellationToken);
        var tokenTag = AntiforgeryTagRegex().Match(html);
        Assert.True(tokenTag.Success, "The real upload page must render an antiforgery token.");
        var tokenValue = AntiforgeryValueRegex().Match(tokenTag.Value);
        Assert.True(tokenValue.Success, "The antiforgery token must have a value.");
        var receiptTokenTag = ExternalReceiptTokenTagRegex().Match(html);
        Assert.True(receiptTokenTag.Success, "The real upload page must render an external receipt token.");
        var receiptTokenValue = AntiforgeryValueRegex().Match(receiptTokenTag.Value);
        Assert.True(receiptTokenValue.Success, "The external receipt token must have a value.");
        return new(
            WebUtility.HtmlDecode(tokenValue.Groups["value"].Value),
            WebUtility.HtmlDecode(receiptTokenValue.Groups["value"].Value));
    }

    public static async Task<UploadResult> PostUploadAsync(
        HttpClient client,
        string? antiforgeryToken,
        string? uploadName,
        string mediaType,
        byte[]? bytes,
        string? externalReceiptToken = null,
        CancellationToken cancellationToken = default)
    {

        using var multipart = new MultipartFormDataContent();
        if (antiforgeryToken is not null)
        {
            multipart.Add(new StringContent(antiforgeryToken), "__RequestVerificationToken");
        }

        if (externalReceiptToken is not null)
        {
            multipart.Add(new StringContent(externalReceiptToken), "ExternalReceiptToken");
        }

        if (uploadName is not null && bytes is not null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
            multipart.Add(file, "Upload", uploadName);
        }

        using var response = await client.PostAsync("/Upload", multipart, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return new(
            response.StatusCode,
            response.Headers.Location,
            responseBody,
            ExternalReceiptToken: externalReceiptToken);
    }

    public static async Task<UploadResult> PostUploadManyAsync(
        HttpClient client,
        string antiforgeryToken,
        string externalReceiptToken,
        IReadOnlyList<(string Name, string MediaType, byte[] Bytes)> files,
        CancellationToken cancellationToken = default)
    {
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(antiforgeryToken), "__RequestVerificationToken");
        multipart.Add(new StringContent(externalReceiptToken), "ExternalReceiptToken");
        foreach (var item in files)
        {
            var content = new ByteArrayContent(item.Bytes);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(item.MediaType);
            multipart.Add(content, "Upload", item.Name);
        }

        using var response = await client.PostAsync("/Upload", multipart, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return new(
            response.StatusCode,
            response.Headers.Location,
            responseBody,
            ExternalReceiptToken: externalReceiptToken);
    }

    public static Guid ReceiptId(UploadResult result)
    {
        if (result.ProcessedReceiptId is { } processedReceiptId)
        {
            return processedReceiptId;
        }

        var id = Landing(result).StagedReceiptId;
        Assert.True(
            id is not null,
            $"The upload should land on the item it created; it landed on '{result.Location}'.");
        return id!.Value;
    }

    private static Dictionary<string, Microsoft.Extensions.Primitives.StringValues> ParseLocationQuery(
        UploadResult result)
    {
        Assert.NotNull(result.Location);
        var location = result.Location!.OriginalString;
        var queryIndex = location.IndexOf('?', StringComparison.Ordinal);
        return queryIndex < 0
            ? []
            : QueryHelpers.ParseQuery(location[queryIndex..]);
    }

    /// <summary>
    /// The Worker's processor, built by hand because the Web host deliberately
    /// does not register it: tests that need to drain work must say so.
    ///
    /// Processing now requires <see cref="IReadLogicalDocumentVersion"/> to
    /// re-read a retained source after its staged copy is deleted. A04's
    /// concrete readers are Stream A's and are supplied by the combined host,
    /// so where a host composes one it is used; where none is composed — which
    /// is every standalone C host — the processor is built on a double that
    /// refuses, so a pass that unexpectedly re-reads fails by name instead of
    /// on an unresolved service. A test whose scenario does re-read registers
    /// the reader it means to exercise and gets that one.
    /// </summary>
    internal static ProcessQueuedIntake CreateProcessor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetService<IReadLogicalDocumentVersion>() is null
            ? ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(
                services,
                RecordingLogicalDocumentVersionReader.Refusing())
            : ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(services);
    }

    /// <summary>
    /// Runs the Worker's grouped-image reconcile sweep once, standing in for
    /// its timer. A grouped upload's members can drain in an order that
    /// leaves a member's group outcome pending for this sweep (the ordinal-
    /// zero member's group lookup resolves only through it), so a test about
    /// a group's settled state must run it just as production does.
    /// </summary>
    internal static async Task ReconcileGroupedImageIntakeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var reconcile = ActivatorUtilities.CreateInstance<ReconcileGroupedImageIntake>(
            services,
            (IProcessQueuedIntake)CreateProcessor(services));
        _ = await reconcile.ExecuteAsync(50, cancellationToken);
    }

    /// <summary>
    /// Dispatches and processes one staged receipt to its completed evaluation,
    /// standing in for the Worker timer and queue trigger. A replay may already
    /// name completed work, so it reads before dispatching.
    /// </summary>
    internal static async Task<IntakeEvaluationRevision> DrainStagedAsync(
        IServiceProvider services,
        Guid stagedReceiptId,
        CancellationToken cancellationToken = default)
    {
        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var dispatcher = new DispatchPendingIntakeWork(
            workStore,
            new ImmediateIntakeWorkEnqueuer(CreateProcessor(services)),
            services.GetRequiredService<TimeProvider>());
        var evaluation = await workStore.GetCompletedEvaluationAsync(stagedReceiptId, cancellationToken);
        while (evaluation is null)
        {
            var dispatched = await dispatcher.ExecuteAsync(1, cancellationToken);
            if (dispatched == 0)
            {
                // A recoverable failure under load reschedules the item with
                // a retry backoff the frozen test clock never reaches.
                // Dispatch once from a clock past any backoff so the retry
                // runs now — the worker timer would have done the same.
                var lateDispatcher = new DispatchPendingIntakeWork(
                    workStore,
                    new ImmediateIntakeWorkEnqueuer(CreateProcessor(services)),
                    new OffsetTimeProvider(
                        services.GetRequiredService<TimeProvider>(),
                        TimeSpan.FromMinutes(10)));
                dispatched = await lateDispatcher.ExecuteAsync(1, cancellationToken);
            }
            Assert.Equal(1, dispatched);
            evaluation = await workStore.GetCompletedEvaluationAsync(stagedReceiptId, cancellationToken);
        }

        return evaluation;
    }

    private sealed class OffsetTimeProvider(TimeProvider inner, TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow() + offset;
    }

    internal sealed class ImmediateIntakeWorkEnqueuer(ProcessQueuedIntake processor)
        : IIntakeWorkEnqueuer
    {
        public Task EnqueueAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            processor.ExecuteAsync(stagedReceiptId, cancellationToken);
    }

    /// <summary>
    /// Advances a work item from pending to dispatched without processing it,
    /// so a test can choose exactly which member's processing pass runs when
    /// rather than folding dispatch and processing into one call.
    /// </summary>
    internal sealed class NoOpIntakeWorkEnqueuer : IIntakeWorkEnqueuer
    {
        public Task EnqueueAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("<input[^>]*name=\"ExternalReceiptToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalReceiptTokenTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryValueRegex();
}

internal sealed record UploadResult(
    HttpStatusCode StatusCode,
    Uri? Location,
    string ResponseBody,
    Guid? ProcessedReceiptId = null,
    string? ExternalReceiptToken = null);

/// <summary>
/// What an upload's redirect names.
/// </summary>
/// <param name="StagedReceiptId">
/// The staged receipt the status page shows, which still has to be dispatched
/// before there is anything to read.
/// </param>
/// <param name="IsDuplicate">Whether the page was told the file is a replay.</param>
internal sealed record UploadLanding(Guid? StagedReceiptId, bool IsDuplicate);

internal sealed record UploadFormTokens(string AntiforgeryToken, string ExternalReceiptToken);

internal static class IntakeTestEvidence
{
    public static TestEmail CreateEmail(
        string fileName,
        string body,
        string senderAddress = "instructions@qdosassist.co.uk",
        string subject = "QDOS test instruction",
        IReadOnlyList<(string FileName, string MediaType, byte[] Content)>? attachments = null)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Synthetic sender", senderAddress));
        message.To.Add(new MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = subject;
        var text = new TextPart("plain") { Text = body };
        if (attachments is { Count: > 0 })
        {
            var multipart = new Multipart("mixed") { text };
            foreach (var (attachmentName, attachmentMediaType, content) in attachments)
            {
                var type = ContentType.Parse(attachmentMediaType);
                multipart.Add(new MimePart(type.MediaType, type.MediaSubtype)
                {
                    Content = new MimeContent(new MemoryStream(content)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = attachmentName
                });
            }

            message.Body = multipart;
        }
        else
        {
            message.Body = text;
        }
        using var output = new MemoryStream();
        message.WriteTo(output);
        return new(fileName, "message/rfc822", output.ToArray());
    }

    public static async Task AssertNoDurableIntakeReceiptsAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        Assert.Empty((await receipts.ListAsync(null, 1, 100, CancellationToken.None)).Items);
    }
}

internal sealed record TestEmail(string FileName, string MediaType, byte[] Content);

internal sealed record GenuineCorpusSample(string Hash, string UploadName, string MediaType, byte[] Bytes);

internal static class GenuineQdosCorpus
{
    private static readonly Lazy<Dictionary<string, string>> PathsByHash = new(BuildPathsByHash);

    public static bool IsPresent => Directory.Exists(CorpusRoot);

    public static bool Contains(string expectedHash) =>
        IsPresent && PathsByHash.Value.ContainsKey(expectedHash);

    public static GenuineCorpusSample Read(string expectedHash)
    {
        Assert.True(PathsByHash.Value.TryGetValue(expectedHash, out var path),
            $"The frozen genuine-corpus item {expectedHash[..12]}... is absent.");
        var bytes = File.ReadAllBytes(path!);
        var actualHash = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Equal(expectedHash, actualHash);
        var extension = Path.GetExtension(path);
        return new(
            expectedHash,
            expectedHash[..12] + extension,
            extension.Equals(".eml", StringComparison.OrdinalIgnoreCase) ? "message/rfc822" : "application/pdf",
            bytes);
    }

    private static Dictionary<string, string> BuildPathsByHash()
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(CorpusRoot, "*.*", SearchOption.AllDirectories)
                     .Where(path => Path.GetExtension(path).Equals(".eml", StringComparison.OrdinalIgnoreCase)
                                    || Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = File.OpenRead(path);
            paths[Convert.ToHexString(SHA256.HashData(stream))] = path;
        }

        return paths;
    }

    private static string CorpusRoot => Path.Combine(
        FindRepositoryRoot(),
        "corpus",
        "emailevals",
        "qdos-email-corpus");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

internal sealed class GenuineQdosCorpusFactAttribute : FactAttribute
{
    public GenuineQdosCorpusFactAttribute(params string[] requiredHashes)
    {
        if (!GenuineQdosCorpus.IsPresent)
        {
            Skip = "The ignored local corpus/emailevals/qdos-email-corpus is absent; genuine-input evidence was not run.";
            return;
        }

        var missing = requiredHashes.FirstOrDefault(hash => !GenuineQdosCorpus.Contains(hash));
        if (missing is not null)
        {
            Skip = $"This machine's qdos-email-corpus lacks the frozen item {missing[..12]}...; corpora differ per system.";
        }
    }
}
