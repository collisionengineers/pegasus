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
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;
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
    private readonly IMailClassificationPolicy? mailClassificationPolicy;
    private readonly IVrmRecognitionEngine? recognitionEngine;
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
        IMailClassificationPolicy? mailClassificationPolicy = null)
    {
        this.environment = environment;
        this.localIntakeEnabled = localIntakeEnabled;
        this.timeProvider = timeProvider ?? new TestTimeProvider(FixedUtcNow);
        this.artifactStore = artifactStore;
        this.extractionPolicy = extractionPolicy;
        this.recognitionEngine = recognitionEngine;
        this.mailClassificationPolicy = mailClassificationPolicy;
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
        if (!Request.Headers.ContainsKey("X-Test-Roleless"))
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

    /// <summary>
    /// Reads the upload to the point where the receipt it produced can be
    /// opened, and points the result at the received-item screen.
    /// </summary>
    /// <remarks>
    /// This used to be the only way an upload ever got processed: the page
    /// staged the bytes and returned, and this helper drove
    /// <c>DispatchPendingIntakeWork</c> by hand to stand in for the Worker
    /// timer. Manual upload now reads the file while the operator waits, so
    /// for that caller the evaluation is already complete before the redirect
    /// arrives and there is nothing to dispatch — asserting that a dispatch
    /// happened would fail on work that has already been done.
    ///
    /// The dispatch loop stays for the mailbox and automation callers, which
    /// genuinely still queue.
    ///
    /// Either way the result is pointed at <c>/Received/{id}</c>, because that is
    /// what the callers of this helper want next: the retained record of what
    /// arrived. Where the upload itself landed is a separate question, asked
    /// with <see cref="Landing"/>, <see cref="CreateScreenReceiptId"/> or
    /// <see cref="CaseId"/>.
    /// </remarks>
    public static async Task<UploadResult> ProcessQueuedAsync(
        IntakeWebApplicationFactory factory,
        UploadResult upload,
        CancellationToken cancellationToken = default)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

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

        Guid processedReceiptId;
        var landing = Landing(upload);
        if (landing.ReceiptId is { } inlineReceiptId)
        {
            processedReceiptId = inlineReceiptId;
        }
        else if (landing.CaseId is { } allocatedCaseId)
        {
            var contextFactory = services
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            processedReceiptId = await context.CaseIntakeLinks
                .AsNoTracking()
                .Where(link => link.CaseId == allocatedCaseId)
                .Select(link => link.IntakeReceiptId)
                .SingleAsync(cancellationToken);
        }
        else
        {
            var stagedReceiptId = landing.StagedReceiptId
                ?? throw new InvalidOperationException(
                    $"The upload landed on '{upload.Location}', which names nothing that can be processed.");
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

            processedReceiptId = evaluation.ProcessedReceiptId;
        }

        var detailLocation = $"/Received/{processedReceiptId:D}"
            + (landing.IsDuplicate ? "?duplicate=true" : string.Empty);
        return upload with
        {
            Location = new Uri(detailLocation, UriKind.Relative),
            ProcessedReceiptId = processedReceiptId
        };
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
    /// What the upload's redirect names.
    /// </summary>
    /// <remarks>
    /// There are three landing places now, and they mean different things: the
    /// case the file allocated, the create screen for readable material that
    /// did not allocate one, and the received item for everything else. The
    /// legacy <c>received</c> and <c>queuedReceiptId</c> query keys are still
    /// read for callers that stage rather than process.
    /// </remarks>
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

        if (query.TryGetValue("receiptId", out var createValues)
            && Guid.TryParse(createValues.SingleOrDefault(), out var createReceiptId))
        {
            return new(createReceiptId, null, null, IsCreateScreen: true, isDuplicate);
        }

        if (query.TryGetValue("received", out var receivedValues)
            && Guid.TryParse(receivedValues.SingleOrDefault(), out var receivedId))
        {
            return new(null, null, receivedId, IsCreateScreen: false, isDuplicate);
        }

        if (query.TryGetValue("queuedReceiptId", out var queuedValues)
            && Guid.TryParse(queuedValues.SingleOrDefault(), out var queuedId))
        {
            return new(null, null, queuedId, IsCreateScreen: false, isDuplicate);
        }

        var lastSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (!Guid.TryParse(lastSegment, out var pathId))
        {
            return new(null, null, null, IsCreateScreen: false, isDuplicate);
        }

        return path.StartsWith("/Cases/", StringComparison.OrdinalIgnoreCase)
            ? new(null, pathId, null, IsCreateScreen: false, isDuplicate)
            : new(pathId, null, null, IsCreateScreen: false, isDuplicate);
    }

    /// <summary>
    /// The receipt an upload produced, read from where the upload lands.
    /// </summary>
    public static Guid QueuedReceiptId(UploadResult result) => ReceiptId(result);

    /// <summary>
    /// The receipt id from a landing on the create screen, asserting that the
    /// upload did in fact open it.
    /// </summary>
    public static Guid CreateScreenReceiptId(UploadResult result)
    {
        var landing = Landing(result);
        Assert.True(
            landing.IsCreateScreen && landing.ReceiptId is not null,
            $"The upload should have opened the create screen; it landed on '{result.Location}'.");
        return landing.ReceiptId!.Value;
    }

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

    /// <summary>
    /// The case an upload allocated on its own, asserting that it allocated
    /// one.
    /// </summary>
    public static Guid CaseId(UploadResult result)
    {
        var landing = Landing(result);
        Assert.True(
            landing.CaseId is not null,
            $"The upload should have landed on the case it created; it landed on '{result.Location}'.");
        return landing.CaseId!.Value;
    }

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

    public static Guid ReceiptId(UploadResult result)
    {
        if (result.ProcessedReceiptId is { } processedReceiptId)
        {
            return processedReceiptId;
        }

        var landing = Landing(result);
        Assert.True(
            landing.CaseId is null,
            $"'{result.Location}' names a case, not a receipt; use CaseId or ProcessQueuedAsync.");
        var id = landing.ReceiptId ?? landing.StagedReceiptId;
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
    Guid? ProcessedReceiptId = null,
    string? ExternalReceiptToken = null);

/// <summary>
/// What an upload's redirect names.
/// </summary>
/// <param name="ReceiptId">The processed receipt, where the redirect names one.</param>
/// <param name="CaseId">The case the file allocated, where it allocated one.</param>
/// <param name="StagedReceiptId">
/// The staged receipt of a caller that queued rather than processed, which
/// still has to be dispatched before there is anything to read.
/// </param>
internal sealed record UploadLanding(
    Guid? ReceiptId,
    Guid? CaseId,
    Guid? StagedReceiptId,
    bool IsCreateScreen,
    bool IsDuplicate);

internal sealed record UploadFormTokens(string AntiforgeryToken, string ExternalReceiptToken);

internal static class IntakeTestEvidence
{
    public static TestEmail CreateEmail(string fileName, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("QDOS Alpha", "instructions@qdosassist.co.uk"));
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
