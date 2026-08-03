using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MimeKit;
using Pegasus.Core.Intake;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

public sealed class IntakeWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly DateTimeOffset FixedUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private readonly string environment;
    private readonly bool? localIntakeEnabled;
    private readonly TimeProvider timeProvider;
    private readonly IIntakeArtifactStore? artifactStore;
    private readonly IInstructionExtractionPolicy? extractionPolicy;
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
        bool initializeDevelopmentOffline = true)
    {
        this.environment = environment;
        this.localIntakeEnabled = localIntakeEnabled;
        this.timeProvider = timeProvider ?? new TestTimeProvider(FixedUtcNow);
        this.artifactStore = artifactStore;
        this.extractionPolicy = extractionPolicy;
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
            if (useIntegrationTestAuthentication)
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "IntegrationTest";
                    options.DefaultChallengeScheme = "IntegrationTest";
                }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthenticationHandler>("IntegrationTest", _ => { });
            }
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(timeProvider);
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

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                DevelopmentOfflineIdentity.AdministratorId.ToString("D")),
            new Claim(ClaimTypes.Name, "integration-user"),
            new Claim("display_name", "Integration User"),
            new Claim(ClaimTypes.Role, "Administrator")
        };
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
        IntakeWebApplicationFactory factory,
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
        IntakeWebApplicationFactory factory,
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

    public static async Task<UploadResult> ProcessQueuedAsync(
        IntakeWebApplicationFactory factory,
        UploadResult upload,
        CancellationToken cancellationToken = default)
    {
        var stagedReceiptId = QueuedReceiptId(upload);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var processor = services.GetRequiredService<ProcessQueuedIntake>();
        var dispatcher = new DispatchPendingIntakeWork(
            workStore,
            new ImmediateIntakeWorkEnqueuer(processor),
            services.GetRequiredService<TimeProvider>());
        var evaluation = await workStore.GetCompletedEvaluationAsync(
            stagedReceiptId,
            cancellationToken);
        while (evaluation is null)
        {
            var dispatched = await dispatcher.ExecuteAsync(1, cancellationToken);
            Assert.Equal(1, dispatched);
            evaluation = await workStore.GetCompletedEvaluationAsync(
                stagedReceiptId,
                cancellationToken);
        }

        var query = ParseLocationQuery(upload);
        var duplicate = query.TryGetValue("duplicate", out var values)
            && bool.TryParse(values.SingleOrDefault(), out var parsed)
            && parsed;
        var detailLocation = $"/Intake/{evaluation.ProcessedReceiptId:D}"
            + (duplicate ? "?duplicate=true" : string.Empty);
        return upload with
        {
            Location = new Uri(detailLocation, UriKind.Relative),
            ProcessedReceiptId = evaluation.ProcessedReceiptId
        };
    }

    public static Guid QueuedReceiptId(UploadResult result)
    {
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var query = ParseLocationQuery(result);
        Assert.True(query.TryGetValue("queuedReceiptId", out var values));
        Assert.True(Guid.TryParse(values.SingleOrDefault(), out var id));
        return id;
    }

    public static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        CancellationToken cancellationToken = default) =>
        (await GetUploadFormTokensAsync(client, cancellationToken)).AntiforgeryToken;

    public static async Task<UploadFormTokens> GetUploadFormTokensAsync(
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        using var formPage = await client.GetAsync("/Intake", cancellationToken);
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

        using var response = await client.PostAsync("/Intake?handler=ReceiveIntake", multipart, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return new(response.StatusCode, response.Headers.Location, responseBody);
    }

    public static Guid ReceiptId(UploadResult result)
    {
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.NotNull(result.Location);
        if (result.ProcessedReceiptId is { } processedReceiptId)
        {
            return processedReceiptId;
        }

        var path = result.Location!.OriginalString.Split('?', 2)[0];
        Assert.True(Guid.TryParse(path.Split('/', StringSplitOptions.RemoveEmptyEntries).Last(), out var id));
        return id;
    }

    private static Dictionary<string, Microsoft.Extensions.Primitives.StringValues> ParseLocationQuery(
        UploadResult result)
    {
        Assert.NotNull(result.Location);
        var location = result.Location!.OriginalString;
        var queryIndex = location.IndexOf('?', StringComparison.Ordinal);
        Assert.True(queryIndex >= 0);
        return QueryHelpers.ParseQuery(location[queryIndex..]);
    }

    private sealed class ImmediateIntakeWorkEnqueuer(ProcessQueuedIntake processor)
        : IIntakeWorkEnqueuer
    {
        public Task EnqueueAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            processor.ExecuteAsync(stagedReceiptId, cancellationToken);
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
    Guid? ProcessedReceiptId = null);

internal sealed record UploadFormTokens(string AntiforgeryToken, string ExternalReceiptToken);

internal static class IntakeTestEvidence
{
    public static TestEmail CreateEmail(string fileName, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("QDOS Alpha", "engineers@qdosassist.co.uk"));
        message.To.Add(new MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS test instruction";
        message.Body = new TextPart("plain") { Text = body };
        using var output = new MemoryStream();
        message.WriteTo(output);
        return new(fileName, "message/rfc822", output.ToArray());
    }

    public static async Task AssertNoDurableIntakeReceiptsAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        Assert.Empty(await receipts.ListAsync(null, CancellationToken.None));
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
