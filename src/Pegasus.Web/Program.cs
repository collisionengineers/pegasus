using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore;
using Pegasus.Infrastructure;
using Pegasus.Core;
using Pegasus.Core.Address;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Intake;
using Pegasus.Web.Health;
using Pegasus.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Web.Mcp;
using Pegasus.Web.PrincipalApi;
using Pegasus.Web;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Glass;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Transport;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;

const string OriginalIssueClaim = "pegasus:original-issued-at";
const string DevelopmentOfflineProfile = "DevelopmentOffline";
const string DevelopmentOfflineAuthenticationScheme = "DevelopmentOffline";
const string AuthenticationRoutingScheme = "Pegasus";
const string StaffSignInRateLimitPolicy = "StaffSignIn";
const string GlassCallbackRateLimitPolicy = "GlassCallback";
const int GlassCallbackRequestsPerClientPerMinute = 30;
const string InitializeDevelopmentArgument = "--initialize-development";
const string BootstrapProductionAdministratorArgument = "--bootstrap-production-administrator";
const string BuildDiagnosticsArgument = "--diagnostics-version";
var informationalVersion = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    ?? throw new InvalidOperationException("Assembly informational version is required.");
var buildMetadataSeparator = informationalVersion.IndexOf('+', StringComparison.Ordinal);
if (buildMetadataSeparator <= 0 || buildMetadataSeparator == informationalVersion.Length - 1)
{
    throw new InvalidOperationException(
        "Assembly informational version must contain the product version and source SHA.");
}

var productVersion = informationalVersion[..buildMetadataSeparator];
var sourceSha = informationalVersion[(buildMetadataSeparator + 1)..].ToLowerInvariant();
if (sourceSha.Length != 40 || sourceSha.Any(character => !char.IsAsciiHexDigit(character)))
{
    throw new InvalidOperationException(
        "Assembly informational version must contain a 40-character hexadecimal source SHA.");
}

if (args.Contains(BuildDiagnosticsArgument, StringComparer.Ordinal))
{
    if (args.Length != 1)
    {
        throw new InvalidOperationException(
            $"{BuildDiagnosticsArgument} must be run without application or maintenance arguments.");
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        version = productVersion,
        sourceSha
    }));
    return;
}
// Phase timings go to stdout so a slow start shows where the time went.
var startupTimeline = StartupTimeline.Current;
startupTimeline.Mark("Main entered (time so far is runtime start)");
var initializeDevelopment =
    args.Contains(InitializeDevelopmentArgument, StringComparer.Ordinal);
var migrateDevelopment = args.Contains("--migrate-development", StringComparer.Ordinal);
var bootstrapProductionAdministrator =
    args.Contains(BootstrapProductionAdministratorArgument, StringComparer.Ordinal);
if ((initializeDevelopment ? 1 : 0)
    + (migrateDevelopment ? 1 : 0)
    + (bootstrapProductionAdministrator ? 1 : 0) > 1)
{
    throw new InvalidOperationException(
        "Development initialization, migration, and production bootstrap commands must be run separately.");
}

var applicationArgs = args
    .Where(argument =>
        !argument.Equals(InitializeDevelopmentArgument, StringComparison.Ordinal)
        && !argument.Equals(BootstrapProductionAdministratorArgument, StringComparison.Ordinal)
        && !argument.Equals("--migrate-development", StringComparison.Ordinal))
    .ToArray();
var builder = WebApplication.CreateBuilder(applicationArgs);
startupTimeline.Mark("configuration loaded");
var configuredRuntimeProfile = builder.Configuration["Runtime:Profile"]
    ?? throw new InvalidOperationException("Runtime:Profile is required.");
var developmentOfflineProfile = builder.Environment.IsDevelopment()
    && configuredRuntimeProfile.Equals(DevelopmentOfflineProfile, StringComparison.Ordinal);
var productionProfile = configuredRuntimeProfile.Equals("Production", StringComparison.Ordinal);
QueueClient? intakeWorkQueue = null;
TokenCredential? automationMcpCredential = null;
var allowLocalQueueCreation = false;
var applicationInsightsConfigured = false;
if (configuredRuntimeProfile.Equals(DevelopmentOfflineProfile, StringComparison.Ordinal)
    && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "The DevelopmentOffline runtime profile is permitted only in the Development environment.");
}
if (builder.Configuration.GetValue<bool>("Features:LocalIntake")
    && !developmentOfflineProfile)
{
    throw new InvalidOperationException(
        "Features:LocalIntake requires the DevelopmentOffline runtime profile.");
}
// The local live-integration opt-ins. Each is a DevelopmentOffline feature in
// the same sense as Features:LocalIntake: Production composes its vendors from
// its own required keys and never reads these, so a Production host carrying
// one fails at start naming the key rather than composing a second truth.
var liveVehicleLookup = builder.Configuration.GetValue<bool>("Features:LiveVehicleLookup");
var liveBoxCustody = builder.Configuration.GetValue<bool>("Features:LiveBoxCustody");
var liveGlass = builder.Configuration.GetValue<bool>("Features:LiveGlass");
var passwordSignIn = builder.Configuration.GetValue<bool>("Features:PasswordSignIn");
foreach (var (featureKey, enabled) in new[]
{
    ("Features:LiveVehicleLookup", liveVehicleLookup),
    ("Features:LiveBoxCustody", liveBoxCustody),
    ("Features:LiveGlass", liveGlass),
    ("Features:PasswordSignIn", passwordSignIn),
})
{
    if (enabled && !developmentOfflineProfile)
    {
        throw new InvalidOperationException(
            $"{featureKey} requires the DevelopmentOffline runtime profile.");
    }
}
if (!developmentOfflineProfile && !productionProfile)
{
    throw new InvalidOperationException(
        $"Unsupported Runtime:Profile '{configuredRuntimeProfile}' for environment '{builder.Environment.EnvironmentName}'.");
}
if (productionProfile)
{
    if (!builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "Runtime:Profile Production requires ASPNETCORE_ENVIRONMENT=Production.");
    }
    foreach (var key in new[]
    {
        "ConnectionStrings:Pegasus",
        "AzureIdentity:WebClientId",
        "TransportStorage:AccountName",
        "IntakeQueue:ServiceUri",
        "CustodyStorage:AccountName",
        "CustodyStorage:ServiceUri",
        "Graph:BaseUri",
        "Graph:TenantId",
        "Graph:ChangeNotificationClientState",
        "Box:BaseUri",
        "Box:UploadUri",
        "Box:RootFolderId",
        "Box:HoldingFolderId",
        "Box:ConfigJson",
        "Box:ClientSecret",
        // The Glass's gateway is built from these on first use;
        // listed here so a deployment without them fails at startup naming the
        // key, rather than at the Engineer's Launch on a Case record.
        "Glass:MarketValueAssessorBaseUri",
        "Glass:EstimatorBaseUri",
        "Glass:CallbackBaseUri",
        "Glass:RepairProfileId",
        // Glass's valuation account (ADR-0060), read on each Get valuation.
        "Glass:ValuationAccount:Username",
        "Glass:ValuationAccount:Password",
        // Cazana's API key (ADR-0066), read on each Get valuation.
        "Cazana:ApiKey",
        "GitHub:ProblemReports:Token",
        "GitHub:ProblemReports:Repository"
    })
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
        {
            throw new InvalidOperationException($"{key} is required for the Production runtime profile.");
        }
    }
    var webClientId = Guid.Parse(builder.Configuration["AzureIdentity:WebClientId"]!);
    var custodyServiceUri = new Uri(builder.Configuration["CustodyStorage:ServiceUri"]!, UriKind.Absolute);
    if (custodyServiceUri.Scheme != Uri.UriSchemeHttps
        || !custodyServiceUri.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "CustodyStorage:ServiceUri must be an Azure Blob HTTPS service URI in Production.");
    }
    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ManagedIdentityClientId = webClientId.ToString("D"),
        ExcludeEnvironmentCredential = true,
        ExcludeWorkloadIdentityCredential = true,
        ExcludeManagedIdentityCredential = false,
        ExcludeVisualStudioCredential = true,
        ExcludeVisualStudioCodeCredential = true,
        ExcludeAzureCliCredential = true,
        ExcludeAzurePowerShellCredential = true,
        ExcludeAzureDeveloperCliCredential = true,
        ExcludeInteractiveBrowserCredential = true,
        ExcludeBrokerCredential = true
    });
    automationMcpCredential = credential;
    builder.Services.AddDataProtection()
        .SetApplicationName("Pegasus")
        .PersistKeysToAzureBlobStorage(
            new Uri(custodyServiceUri, "authentication-ring/keys.xml"),
            credential);
    builder.Services.AddSingleton(
        new BlobServiceClient(custodyServiceUri, credential)
            .GetBlobContainerClient("transient-intake"));
    var intakeQueueServiceUri = new Uri(
        builder.Configuration["IntakeQueue:ServiceUri"]!,
        UriKind.Absolute);
    if (intakeQueueServiceUri.Scheme != Uri.UriSchemeHttps
        || !intakeQueueServiceUri.Host.EndsWith(".queue.core.windows.net", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "IntakeQueue:ServiceUri must be an Azure Queue HTTPS service URI in Production.");
    }
    intakeWorkQueue = new QueueServiceClient(intakeQueueServiceUri, credential)
        .GetQueueClient("intake-work");
    // The mailbox-administration "add an address" resolve port alone (AddPegasusInfrastructure
    // below always composes ListApprovedMailboxes/UpdateApprovedMailbox; Web never composes
    // the Worker-only pollers that go with AddProductionExternalAdapters).
    builder.Services.AddSingleton<TokenCredential>(credential);
    builder.Services.AddProductionApprovedMailboxResolver(builder.Configuration["Graph:BaseUri"]);
    // The deployed container has carried
    // APPLICATIONINSIGHTS_CONNECTION_STRING since the estate was built, but
    // nothing in this application ever read it — the Web host was never
    // instrumented at all, so thirty days of production produced no traces,
    // no requests and no exceptions to diagnose from. The credential is
    // supplied explicitly because ingestion is configured for Entra
    // (APPLICATIONINSIGHTS_AUTHENTICATION_STRING names the runtime identity),
    // and a connection string alone would be rejected without it.
    if (!string.IsNullOrWhiteSpace(
            builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    {
        builder.Services.AddApplicationInsightsTelemetry();
        // Probe, ping and static-file rows carry nothing and spend the daily
        // cap; the SDK runs this before adaptive sampling.
        builder.Services.AddApplicationInsightsTelemetryProcessor<QuietRequestTelemetryFilter>();
        // The runtime and SqlClient counters, twelve and no others: metrics
        // are never sampled, so this list is the whole added volume.
        builder.Services.ConfigureTelemetryModule<EventCounterCollectionModule>(
            (module, _) => RuntimeCounters.Apply(module));
        builder.Services.AddSingleton<ITelemetryInitializer, GlassCallbackTelemetryInitializer>();
        builder.Services.AddSingleton<DocumentReadTelemetryBridge>();
        builder.Services.Configure<TelemetryConfiguration>(
            telemetry => telemetry.SetAzureTokenCredential(credential));
        applicationInsightsConfigured = true;
    }
}
else
{
    var queueConnectionString = builder.Configuration["AzureWebJobsStorage"]
        ?? throw new InvalidOperationException(
            "AzureWebJobsStorage is required for DevelopmentOffline queue transport.");
    // Pinned like the blob client below: the newest service version the
    // repository's Azurite pin (3.36.0) speaks.
    intakeWorkQueue = new QueueClient(
        queueConnectionString,
        "intake-work",
        new QueueClientOptions(QueueClientOptions.ServiceVersion.V2025_11_05));
    allowLocalQueueCreation = true;
    if (liveBoxCustody)
    {
        // Live Box custody keeps the production storage shape over the run's
        // Azurite account: the same container the Worker provisions and reads.
        // Pinned to the newest service version the repository's Azurite pin
        // (3.36.0) speaks; the SDK's default is ahead of it, and Azurite
        // refuses the request rather than downgrading.
        builder.Services.AddSingleton(
            new BlobContainerClient(
                queueConnectionString,
                "transient-intake",
                new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_11_05)));
    }
}
builder.Services.AddSingleton<ICursorProtector, DataProtectionCursorProtector>();
var localDocumentCustodyConfigured =
    builder.Configuration.GetValue<bool>("Features:LocalDocumentCustody");
if (localDocumentCustodyConfigured && liveBoxCustody)
{
    throw new InvalidOperationException(
        "Features:LocalDocumentCustody and Features:LiveBoxCustody name two custody stores; configure one.");
}

// The Automation MCP ingress is composition-gated off by default: when the
// flag is absent nothing below registers and no /mcp or /connect/token route
// exists. An explicitly configured deployment may enable it in Production.
var automationMcpOptions = AutomationMcpOptions.TryCreate(builder.Configuration);

// The Principal API (API-01) is gated the same way: off by default, and
// without the flag no /api/principal route, scheme or policy exists.
var principalApiEnabled = builder.Configuration.GetValue<bool>(PrincipalApi.FeatureFlag);

// RailCountsPageFilter supplies ViewData["RailCounts"] on authenticated full
// page results — the rail shipped with the badge
// mechanism but nothing populated it until now. RazorPagesOptions has no
// Filters collection of its own, so the global filter is added through the
// underlying MvcOptions instead.
builder.Services.AddRazorPages()
    .AddMvcOptions(options =>
    {
        options.Filters.Add<Pegasus.Web.Presentation.RailCountsPageFilter>();
        options.Filters.Add<Pegasus.Web.Presentation.WorkspaceRequestTimingFilter>();
    })
    .AddRazorPagesOptions(options =>
    {
        options.Conventions.AddPageApplicationModelConvention(
            "/Integrations/Glass/Callback",
            model => model.EndpointMetadata.Add(
                new EnableRateLimitingAttribute(GlassCallbackRateLimitPolicy)));
        // The Case record's section fragment answers on its own
        // path, `/Cases/{id}/Section`, rather than on the record's own URL, so
        // a fragment response is never mistaken for the page itself. The
        // constraint admits that one handler, and the route only matches — it
        // generates no links, so `/Cases/{id}` and every other handler's
        // query-string link are exactly as they were.
        options.Conventions.AddPageRouteModelConvention(
            "/Cases/Details",
            model => model.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel
                {
                    Template = "/Cases/{id:guid}/{handler:regex(^Section$)}",
                    SuppressLinkGeneration = true
                }
            }));
        // A Triage Case has no Case workflow, so the Case workflow sub-routes
        // are not found for one (the record itself dispatches in Details).
        options.Conventions.AddFolderApplicationModelConvention(
            "/Cases",
            model =>
            {
                if (Pegasus.Web.Pages.Cases.TriageCaseRouteFilter.GuardedPages.Contains(model.ViewEnginePath))
                {
                    model.Filters.Add(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(
                        typeof(Pegasus.Web.Pages.Cases.TriageCaseRouteFilter)));
                }
            });
    });
builder.Services
    .AddIdentity<PegasusIdentityUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Lockout.AllowedForNewUsers = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<PegasusDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        var rejectedPath = context.HttpContext.Request.Path;
        var reasonCode = rejectedPath.Equals(
            "/Account/SignIn",
            StringComparison.OrdinalIgnoreCase)
            ? "sign_in_rate_limited"
            : rejectedPath.StartsWithSegments(AutomationMcp.McpEndpointPath)
                || rejectedPath.Equals(
                    AutomationMcp.TokenEndpointPath,
                    StringComparison.OrdinalIgnoreCase)
                ? "automation_rate_limited"
                : rejectedPath.StartsWithSegments(PrincipalApi.BasePath)
                    ? "principal_api_rate_limited"
                    : rejectedPath.StartsWithSegments("/Integrations/Glass/Callback")
                        ? "glass_callback_rate_limited"
                    : "authentication_rate_limited";
        return new ValueTask(AppendRateLimitedSecurityEventAsync(
            context.HttpContext,
            reasonCode,
            cancellationToken));
    };
    options.AddPolicy(
        StaffSignInRateLimitPolicy,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = StaffSessionPolicy.SignInAttemptsPerClientPerMinute,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy(
        AutomationMcp.RateLimitPolicy,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = AutomationMcp.RequestsPerClientPerMinute,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy(
        GlassCallbackRateLimitPolicy,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = GlassCallbackRequestsPerClientPerMinute,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    // The limiter runs before authentication, so a presented key id is a claim
    // and not an identity, and it cannot be the partition: naming another
    // principal's key id would spend that principal's budget with forged
    // secrets, and minting a fresh well-formed key id per request would hand
    // the caller a fresh budget each time and bound nothing at all. The
    // partition is the calling address, as it already is for staff sign-in and
    // the MCP ingress.
    options.AddPolicy(
        PrincipalApi.RateLimitPolicy,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = PrincipalApi.RequestsPerCallerPerMinute,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});
builder.Services.AddSingleton(_ => new FixedWindowRateLimiter(
    new FixedWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        PermitLimit = StaffSessionPolicy.SignInAttemptsGlobalPerMinute,
        QueueLimit = 0,
        Window = TimeSpan.FromMinutes(1)
    }));
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = AuthenticationRoutingScheme;
        options.DefaultChallengeScheme = AuthenticationRoutingScheme;
    })
    .AddPolicyScheme(AuthenticationRoutingScheme, displayName: null, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            // Features:PasswordSignIn hands the offline host back to the
            // Identity cookie so the real sign-in page and role matrix can be
            // exercised; the automatic scheme then never answers.
            return environment.IsDevelopment()
                && configuration["Runtime:Profile"]?.Equals(
                    DevelopmentOfflineProfile,
                    StringComparison.Ordinal) == true
                && !configuration.GetValue<bool>("Features:PasswordSignIn")
                    ? DevelopmentOfflineAuthenticationScheme
                    : IdentityConstants.ApplicationScheme;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, DevelopmentOfflineAuthenticationHandler>(
        DevelopmentOfflineAuthenticationScheme,
        displayName: null,
        _ => { });
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-Pegasus";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = StaffSessionPolicy.IdleLifetime;
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/SignIn";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnSigningIn = async context =>
    {
        var principal = context.Principal
            ?? throw new InvalidOperationException("A staff sign-in requires a principal.");
        var identity = principal.Identity as System.Security.Claims.ClaimsIdentity
            ?? throw new InvalidOperationException("A staff sign-in requires a claims identity.");
        if (!identity.HasClaim(claim => claim.Type == OriginalIssueClaim))
        {
            var clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
            identity.AddClaim(new(
                OriginalIssueClaim,
                clock.GetUtcNow().ToUnixTimeSeconds().ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
        }

        var subjectId = principal.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("A staff sign-in requires a subject identifier.");
        await AppendSignInSecurityEventAsync(
            context.HttpContext,
            subjectId,
            SecurityEventOutcome.Succeeded,
            reasonCode: null);
    };
    // The account is checked on every request; StaffPrincipalValidator says
    // why the principal is never rebuilt.
    options.Events.OnValidatePrincipal = async context =>
    {
        using var validation = DocumentReadTelemetry.Start("web.auth.validation");
        var subjectId = context.Principal?.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var refusal = await StaffPrincipalValidator.ValidateAsync(context, OriginalIssueClaim);
        if (refusal is null)
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await AppendSignInSecurityEventAsync(
            context.HttpContext,
            subjectId,
            SecurityEventOutcome.Denied,
            refusal);
    };
});

static Task AppendSignInSecurityEventAsync(
    HttpContext context,
    string subjectId,
    SecurityEventOutcome outcome,
    string? reasonCode)
{
    var writer = context.RequestServices.GetRequiredService<ISecurityEventWriter>();
    var occurredAtUtc = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    var securityEvent = new SecurityEvent(
        Guid.NewGuid(),
        SecurityEventType.SignIn,
        outcome,
        subjectId,
        occurredAtUtc,
        context.TraceIdentifier,
        reasonCode);
    // A sign-in decision is taken by the staff member signing in, so the acting
    // principal and the subject are the same account. A subject that is not a
    // staff identifier at all (the literal "unknown" a claim-less principal
    // leaves behind) is not attributable and stays unattributed rather than
    // being recorded as a staff actor.
    return writer.AppendAsync(
        Guid.TryParse(subjectId, out var staffId) && staffId != Guid.Empty
            ? securityEvent.By(ActorKind.Staff, subjectId)
            : securityEvent,
        context.RequestAborted);
}

static Task AppendAutomationDeniedSecurityEventAsync(
    HttpContext context,
    bool tokenEndpoint)
{
    var writer = context.RequestServices.GetRequiredService<ISecurityEventWriter>();
    var occurredAtUtc = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    var claimedSubject = context.User.FindFirst(
        System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    var securityEvent = new SecurityEvent(
        Guid.NewGuid(),
        SecurityEventType.Token,
        SecurityEventOutcome.Denied,
        claimedSubject ?? "anonymous",
        occurredAtUtc,
        context.TraceIdentifier,
        tokenEndpoint ? "automation_token_rejected" : "automation_access_denied");
    // The refused caller is the Automation client when it presented an identity
    // at all; a request that carried none is anonymous, which no actor kind
    // represents, so it stays unattributed.
    return writer.AppendAsync(
        claimedSubject is { Length: > 0 }
            ? securityEvent.By(ActorKind.Automation, claimedSubject)
            : securityEvent,
        CancellationToken.None);
}

static Task AppendRateLimitedSecurityEventAsync(
    HttpContext context,
    string reasonCode,
    CancellationToken cancellationToken)
{
    var writer = context.RequestServices.GetRequiredService<ISecurityEventWriter>();
    var occurredAtUtc = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    // A rate-limited request is refused before any principal is established:
    // there is no acting actor to record and none is invented.
    return writer.AppendAsync(
        new SecurityEvent(
            Guid.NewGuid(),
            SecurityEventType.RateLimited,
            SecurityEventOutcome.Denied,
            "anonymous",
            occurredAtUtc,
            context.TraceIdentifier,
            reasonCode),
        cancellationToken);
}
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy("Administrator", policy =>
        policy.RequireRole(StaffRoleNames.Administrator));
// Pages only, over HTTPS too; a decision record accepts the BREACH risk.
// Static assets are precompressed at build and files keep their own encoding.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ["text/html"];
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(
    options => options.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(
    options => options.Level = System.IO.Compression.CompressionLevel.Fastest);
// A new instance warms its hot reads before it reports ready (at most 45 s).
// /health/warm answers the platform's start-up ping from the warm-up alone, so
// a database outage never keeps a new instance from starting. The warm-up then
// repeats its reads every Startup:WarmupInterval (three minutes; 00:00:00 runs
// it once) so no staff request is the first to touch a cold path.
builder.Services.AddSingleton(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    return StartupWarmupState.FromSettings(
        configuration.GetValue("Startup:Warmup", true),
        configuration["Startup:WarmupInterval"]);
});
builder.Services.AddHostedService<StartupWarmup>();
if (applicationInsightsConfigured)
{
    // Its trace reaches Application Insights only where that is configured.
    builder.Services.AddHostedService<RuntimeHeartbeat>();
}
// The database check remembers a current schema, so it is one instance for the
// process and not one per probe.
builder.Services.AddSingleton<DatabaseReadinessHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"])
    .AddCheck<StartupWarmupHealthCheck>("warmup", tags: ["ready", "warm"]);
builder.Services.Configure<FormOptions>(options =>
{
    // Bounded for a whole Upload batch, not one file: IntakeEnvelopeLimits
    // enforces the per-file cap and the maximum file count independently.
    options.MultipartBodyLengthLimit = IntakeEnvelopeLimits.MaximumBatchContentLength;
});

// Live Box custody offline composes the production storage set instead of the
// local artifact root, so the two never resolve side by side.
var composesLocalArtifactRoot = developmentOfflineProfile && !liveBoxCustody;
Func<IServiceProvider, string>? localArtifactRootFactory = composesLocalArtifactRoot
    ? serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
        var configuredArtifactRoot = configuration["Intake:LocalArtifactPath"]
            ?? throw new InvalidOperationException(
                "Intake:LocalArtifactPath is required for the DevelopmentOffline runtime profile.");
        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredArtifactRoot));
    }
    : null;

builder.Services.AddPegasusInfrastructure((serviceProvider, options) =>
{
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
        .GetConnectionString("Pegasus")
        ?? throw new InvalidOperationException("Connection string 'Pegasus' is required.");
    PegasusSqlServer.Configure(options, connectionString);
}, localArtifactRootFactory,
documentStorage: composesLocalArtifactRoot
    ? null
    : (Action<IServiceCollection>)(registrations => registrations.AddProductionDocumentStorage(
        provider => provider.GetRequiredService<BlobContainerClient>(),
        // Web never provisions the production container; the Worker owns that.
        // The local run starts Web before the Worker, so offline it may create
        // the Azurite container it is about to write.
        _ => liveBoxCustody,
        // Deferred to first Box use: parsing this at host build aborted the
        // process whenever the platform handed over an unresolved Key Vault
        // reference.
        _ => BoxCustodyOptions.Create(
            builder.Configuration["Box:BaseUri"],
            builder.Configuration["Box:UploadUri"],
            builder.Configuration["Box:RootFolderId"],
            builder.Configuration["Box:ConfigJson"],
            builder.Configuration["Box:ClientSecret"],
            builder.Configuration["Box:HoldingFolderId"],
            liveBoxCustody
                ? BoxCustodyOptions.DevelopmentRootFolderId
                : BoxCustodyOptions.ProductionRootFolderId))));
// The staff-identity surfaces need this host's Identity and key ring.
builder.Services.AddPegasusStaffIdentity();
// Glass's (ADR-0060) and Cazana (ADR-0066) valuation: Production only — the
// offline profile reaches no vendor — and each Key Vault-held credential is
// read on each valuation for the same unresolved-Key-Vault-reference reason
// as Box's.
if (productionProfile)
{
    builder.Services.AddGlassGuideValuation(
        _ => Pegasus.Infrastructure.Glass.GlassValuationAccount.Create(key => builder.Configuration[key]));
    builder.Services.AddCazanaGuideValuation(
        _ => Pegasus.Infrastructure.Cazana.CazanaApiKey.Create(key => builder.Configuration[key]));
}

builder.Services.AddPegasusReportRendering();
// EXT-06: Glass's is composed only by a host that reaches the provider;
// every other host offers no control and refuses the command (fail closed).
if (productionProfile || liveGlass)
{
    builder.Services.AddGlassRepairEstimates(
        GlassRepairEstimateOptions.Create(key => builder.Configuration[key]));
}
else
{
    builder.Services.AddUnavailableGlassRepairEstimates();
}
// Glass's provider work runs in this host after the staff member's request has
// answered. It stays here, not in the Worker: the session state and per-staff
// credentials it reads are protected by this host's key ring (ADR-0058).
builder.Services.AddSingleton<Pegasus.Web.Background.ProviderWorkQueue>();
builder.Services.AddHostedService<Pegasus.Web.Background.ProviderWorkService>();
builder.Services.AddScoped<Pegasus.Web.Pages.Integrations.Glass.GlassSessionWork>();
// A guide valuation's report is fetched over the provider session this host
// holds, so it is filed on the same queue after the figures have answered.
builder.Services.AddScoped<
    Pegasus.Core.Assessment.IScheduleGuideValuationReport,
    Pegasus.Web.Background.GuideValuationReportScheduler>();
// Get valuation files its report through that scheduler, so the fetch use
// case is composed here, beside it.
builder.Services.AddScoped<
    Pegasus.Core.Assessment.IFetchGuideValuation,
    Pegasus.Core.Assessment.FetchGuideValuation>();
builder.Services.AddScoped<IStaffMailAttachmentResolver, StaffMailAttachmentResolver>();
builder.Services.AddScoped<Pegasus.Web.Intake.StaffIntakeActions>();
if (developmentOfflineProfile)
{
    builder.Services.AddScoped<Pegasus.Core.Operations.IStaffMailSend, UnavailableStaffMailSend>();
    builder.Services.AddScoped<Pegasus.Core.Operations.IStaffReportSend, UnavailableStaffReportSend>();
    // The Web only records the request either way; the mode names which
    // adapter the offline Worker answers it with.
    builder.Services.AddSingleton(liveVehicleLookup
        ? VehicleLookupAvailability.ProductionLive
        : VehicleLookupAvailability.DevelopmentOfflineReplay);
    builder.Services.AddSingleton<LocalApprovedMailboxIdentityResolver>();
    builder.Services.AddSingleton<IResolveApprovedMailboxIdentity>(provider =>
        provider.GetRequiredService<LocalApprovedMailboxIdentityResolver>());
    builder.Services.AddSingleton<ICheckApprovedMailboxAccess>(provider =>
        provider.GetRequiredService<LocalApprovedMailboxIdentityResolver>());
}
else
{
    // The production profile enables staff vehicle lookup requests. The Web only
    // records the request; the production Worker owns the live DVLA/DVSA adapter
    // and executes it from the recorded work item.
    builder.Services.AddSingleton(VehicleLookupAvailability.ProductionLive);
}
builder.Services.AddScoped<EfIdentityAuditStore>();
builder.Services.AddScoped<ISecurityEventWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIdentityAuditStore>());
builder.Services.AddScoped<IActionHistoryWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIdentityAuditStore>());
builder.Services.AddScoped<ICaseAcceptanceStore, EfCaseAcceptanceStore>();
builder.Services.AddScoped<IPrincipalInspectionModeStore, EfPrincipalInspectionModeStore>();
builder.Services.AddScoped<IInspectionAddressResolutionStore, InspectionAddressResolutionStore>();
builder.Services.AddScoped<EfIntakeWorkStore>();
builder.Services.AddScoped<IIntakeWorkStore>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIntakeWorkStore>());
builder.Services.AddScoped<IStagedArtifactAuthority>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIntakeWorkStore>());
builder.Services.AddSingleton<IIntakeWorkEnqueuer>(
    new AzureQueueIntakeWorkEnqueuer(
        intakeWorkQueue ?? throw new InvalidOperationException("The intake queue is not configured."),
        allowLocalQueueCreation));
builder.Services.AddSingleton<IExternalWorkEnqueuer>(
    new AzureQueueExternalWorkEnqueuer(
        intakeWorkQueue ?? throw new InvalidOperationException("The unified work queue is not configured."),
        allowLocalQueueCreation));
builder.Services.AddSingleton<IMailboxWakeEnqueuer>(
    new AzureQueueMailboxWakeEnqueuer(
        intakeWorkQueue ?? throw new InvalidOperationException("The unified work queue is not configured."),
        allowLocalQueueCreation));
builder.Services.AddScoped<DispatchPendingIntakeWork>();
builder.Services.AddScoped<ICommittedIntakeWorkPublisher>(serviceProvider =>
    serviceProvider.GetRequiredService<DispatchPendingIntakeWork>());
builder.Services.AddScoped<DispatchPendingExternalWork>();
builder.Services.AddScoped<ICommittedExternalWorkPublisher>(serviceProvider =>
    serviceProvider.GetRequiredService<DispatchPendingExternalWork>());
// Presentation-layer read model for the Upload confirmation surface: composes
// existing Core read ports only, and every action it offers routes to the
// existing page that performs it (see Pegasus.Web.Presentation.UploadOutcome).
builder.Services.AddScoped<Pegasus.Web.Presentation.IUploadOutcomeQueries,
    Pegasus.Web.Presentation.UploadOutcomeQueries>();
// The confirmation surface's one staff decision: the case search behind the
// autocomplete, and add-to-case through the existing leased link path
// (see Pegasus.Web.Presentation.UploadCaseDecision).
builder.Services.AddScoped<Pegasus.Web.Presentation.IUploadCaseDecision,
    Pegasus.Web.Presentation.UploadCaseDecision>();
// A Triage Case's record on /Cases/{id}: its ports, taken by the Case record's
// Triage handlers, and the filter that keeps the Case workflow sub-routes from
// answering for it.
builder.Services.AddScoped<Pegasus.Web.Pages.Cases.TriageCasePorts>();
builder.Services.AddScoped<Pegasus.Web.Pages.Cases.TriageCaseRouteFilter>();
builder.Services.AddScoped<ReceiveIntake>();
builder.Services.AddScoped<DiscardIntakeSubmissionGroup>();
builder.Services.AddScoped<IDiscardIntakeSubmissionGroup>(serviceProvider =>
    serviceProvider.GetRequiredService<DiscardIntakeSubmissionGroup>());
builder.Services.AddScoped<IIntakeSubmission>(serviceProvider =>
    serviceProvider.GetRequiredService<ReceiveIntake>());
builder.Services.AddScoped<SubmitGroupedIntake>();
builder.Services.AddScoped<IGroupedIntakeSubmission>(serviceProvider =>
    serviceProvider.GetRequiredService<SubmitGroupedIntake>());
// No Administration view reads the Automation activity use case: automation
// activity is read in Action logs (FRD-04). GetServiceHealth reads the newest
// activity through this port in every profile; the ingress itself stays
// behind the composition gate.
builder.Services.AddScoped<IAutomationActivityQueries, EfAutomationActivityStore>();
// Administration health remains available when the Automation ingress is disabled.
builder.Services.AddScoped<Pegasus.Core.Operations.IAutomationIngressStatusQueries, AutomationIngressStatusQueries>();
builder.Services.AddScoped<Pegasus.Core.Operations.GetServiceHealth>();
builder.Services.AddSingleton(new Pegasus.Core.ReleaseNotes.ApplicationBuild(productVersion, sourceSha));
builder.Services.AddScoped<Pegasus.Core.ReleaseNotes.ReleaseNoteAdministration>();
builder.Services.AddScoped<Pegasus.Core.Support.ReportProblem>();
builder.Services.AddScoped<Pegasus.Core.Support.RetryProblemReport>();
builder.Services.AddScoped<Pegasus.Core.Support.ReconcileProblemReport>();
// Report a problem (ADR-0055): reports become issues on one repository when
// the token and repository are configured; otherwise every report is kept
// as Not sent with the reason, and an Administrator retries once connected.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient(Pegasus.Infrastructure.Support.GitHubIssueProblemReportSink.HttpClientName, client =>
    client.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<Pegasus.Core.Support.IProblemReportSink>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var options = Pegasus.Infrastructure.Support.GitHubProblemReportOptions.FromConfiguration(
        configuration[Pegasus.Infrastructure.Support.GitHubProblemReportOptions.TokenKey],
        configuration[Pegasus.Infrastructure.Support.GitHubProblemReportOptions.RepositoryKey],
        configuration[Pegasus.Infrastructure.Support.GitHubProblemReportOptions.LabelsKey]);
    return options is null
        ? new Pegasus.Infrastructure.Support.UnconfiguredProblemReportSink()
        : new Pegasus.Infrastructure.Support.GitHubIssueProblemReportSink(
            options,
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(
                Pegasus.Infrastructure.Support.GitHubIssueProblemReportSink.HttpClientName));
});
if (automationMcpOptions is not null)
{
    builder.Services.AddPegasusAutomationMcp(
        automationMcpOptions,
        productVersion,
        automationMcpCredential);
}
if (principalApiEnabled)
{
    builder.Services.AddPegasusPrincipalApi();
}

startupTimeline.Mark("services composed");
// Last, after every AddDataProtection: the framework's start-up load of the key
// ring would hold the port behind a managed-identity token (see the extension).
builder.Services.DeferDataProtectionKeyRingLoad();
var app = builder.Build();
startupTimeline.Mark("host built");
if (applicationInsightsConfigured)
{
    // This singleton owns the listener for the application's lifetime. Resolving
    // it here makes timing active only in the configured AI composition.
    _ = app.Services.GetRequiredService<DocumentReadTelemetryBridge>();
}
startupTimeline.Mark("telemetry bridge resolved");
var runtimeProfile = app.Configuration["Runtime:Profile"]
    ?? throw new InvalidOperationException("Runtime:Profile is required.");
var developmentOffline = runtimeProfile.Equals(
    DevelopmentOfflineProfile,
    StringComparison.Ordinal);
var localIntakeConfigured = app.Configuration.GetValue<bool>("Features:LocalIntake");
// The document surface follows composed custody, not the Development-only feature
// flag: Production composes Box-backed custody and must serve the staff pages.
var documentCustodyEnabled =
    (developmentOffline && (localDocumentCustodyConfigured || liveBoxCustody)) || productionProfile;

if (developmentOffline && !app.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "The DevelopmentOffline runtime profile is permitted only in the Development environment.");
}

if (localIntakeConfigured && !developmentOffline)
{
    throw new InvalidOperationException(
        "Features:LocalIntake requires the DevelopmentOffline runtime profile.");
}
if (localDocumentCustodyConfigured && !developmentOffline)
{
    throw new InvalidOperationException(
        "Features:LocalDocumentCustody requires the DevelopmentOffline runtime profile.");
}

if (bootstrapProductionAdministrator)
{
    if (!app.Environment.IsProduction()
        || !runtimeProfile.Equals("Production", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "The first-Administrator bootstrap is available only in the Production runtime profile and environment.");
    }
    await BootstrapProductionAdministratorAsync(app.Services);
    Console.WriteLine("Production Administrator bootstrap completed; first password change is required.");
    return;
}

var localIntakeEnabled = developmentOffline && localIntakeConfigured;
var intakeSurfaceEnabled = localIntakeEnabled || productionProfile;
if (migrateDevelopment)
{
    await using var scope = app.Services.CreateAsyncScope();
    await DevelopmentOfflineInitialization.MigrateAsync(scope.ServiceProvider);
    Console.WriteLine("Development database migrations applied.");
    return;
}
if (initializeDevelopment)
{
    await using var scope = app.Services.CreateAsyncScope();
    await DevelopmentOfflineInitialization.InitializeAsync(scope.ServiceProvider);
    Console.WriteLine("DevelopmentOffline database, local test identity, and roles initialized.");
    return;
}




if (productionProfile)
{
    // The App Service front end terminates TLS and forwards the original scheme
    // in X-Forwarded-Proto (and the caller in X-Forwarded-For). Without this,
    // Kestrel sees http, UseHttpsRedirection loops, and every generated redirect
    // and sign-in callback emits http://. It must run before UseHsts and
    // UseHttpsRedirection. The front end is not on a known network, so the
    // proxy allow-lists are cleared.
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
    };
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

// While the Automation OAuth certificates load (after the port binds), every
// request but a health or version probe gets 503 and a retry hint. Those probes
// short-circuit after routing, so they never reach authentication, which builds
// the token server's options. Absent when the certificates are not read from
// Key Vault.
if (app.Services.GetService<OAuthCertificateStore>() is { } oauthCertificates)
{
    app.Use(oauthCertificates.Gate);
}
startupTimeline.Mark("OAuth certificate store resolved");

// Every status code that reaches a browser gets the designed page. Before this,
// an unknown record URL, an oversized staff upload and a rate-limited sign-in
// all rendered the browser's own error page.
//
// Scoped away from the machine surfaces: health probes, the version endpoint
// and the automation ingress answer callers that want a status code and a body
// they can parse, not a card.
app.UseWhen(
    context => !IsMachineSurface(context.Request.Path),
    branch => branch.UseStatusCodePagesWithReExecute("/status/{0}"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.Use(async (context, next) =>
    {
        // frame-ancestors is 'self', not 'none': the evidence viewer previews a
        // PDF in a same-origin iframe. frame-src admits only that existing
        // source and the in-page Blob URLs used by saved report and estimate
        // previews. img-src admits the Blob URLs the Upload page draws its
        // chosen images from before they are sent. The clickjacking protection
        // this header exists for is unchanged, because frame-ancestors still
        // refuses every other origin. Development does not set the header at
        // all, so this policy is tested through the Production profile.
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; object-src 'none'; base-uri 'self'; " +
            "img-src 'self' blob:; frame-src 'self' blob:; frame-ancestors 'self'";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        await next(context);
    });
}

// The whole received-item surface — the list, an item, and its retained source
// — is present only where intake is composed, and returns 404 everywhere else.
// The mail workspace at /Inbox joins it: retained mail exists only where polling
// is composed, so a deployment without it has no messages to show and says 404
// rather than rendering a permanently empty screen.
//
// The second gate that used to sit here refused POST /Intake?handler=ReceiveIntake
// when local intake was off. That handler stopped existing when manual upload
// moved to its own /Upload page, and no screen has produced that query string
// since, so the branch matched nothing. Creating a case now happens at
// /Cases/Create, outside these routes, which is deliberate: it is a staff action
// in every runtime profile and must not inherit a development-only gate.
if (!intakeSurfaceEnabled)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/Received")
            || context.Request.Path.StartsWithSegments("/Inbox"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
}

// The Principal API joins the same absence gates. Answering 404 before routing
// matters: the static-assets fallback owns a GET/HEAD-only catch-all over
// every file-shaped path, so an uncomposed POST here would otherwise surface
// as a 405 that discloses the route's shape instead of its absence.
if (!principalApiEnabled)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments(PrincipalApi.BasePath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
}

// The platform's probes reach the instance over plain HTTP on its own port and
// want a status code, so the health endpoints are never redirected to HTTPS.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseHttpsRedirection());
app.UseResponseCompression();

app.UseRouting();
if (applicationInsightsConfigured)
{
    app.Use(RequestRuntimeStamps.InvokeAsync);
}
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.Equals("/Account/SignIn", StringComparison.OrdinalIgnoreCase))
    {
        var limiter = context.RequestServices.GetRequiredService<FixedWindowRateLimiter>();
        using var lease = await limiter.AcquireAsync(1, context.RequestAborted);
        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            await AppendRateLimitedSecurityEventAsync(
                context,
                "sign_in_rate_limited",
                context.RequestAborted);
            return;
        }
    }

    await next(context);
});

app.UseRateLimiter();
if (automationMcpOptions is not null)
{
    app.Use(async (context, next) =>
    {
        var automationPath = context.Request.Path;
        var isTokenEndpoint = automationPath.Equals(
            AutomationMcp.TokenEndpointPath,
            StringComparison.OrdinalIgnoreCase);
        var isMcpEndpoint = automationPath.StartsWithSegments(
            AutomationMcp.McpEndpointPath);
        var isAuthorizationEndpoint = automationPath.Equals(
            AutomationMcp.AuthorizationEndpointPath,
            StringComparison.OrdinalIgnoreCase);
        if (!isTokenEndpoint && !isMcpEndpoint && !isAuthorizationEndpoint)
        {
            await next(context);
            return;
        }

        if ((isTokenEndpoint && HttpMethods.IsPost(context.Request.Method))
            || isAuthorizationEndpoint)
        {
            // Seed/reconcile the single Automation client registration before
            // OpenIddict validates the caller (token) or the connector's
            // authorization request against it.
            await context.RequestServices
                .GetRequiredService<AutomationClientRegistry>()
                .EnsureRegisteredAsync(context.RequestAborted);
        }

        await next(context);
        if (isAuthorizationEndpoint)
        {
            // The consent page is a staff surface; its refusals are recorded
            // by the page itself, not as transport denials.
            return;
        }

        // Transport-level denials on the automation surface are material and
        // become attributable security events. Tool-level denials (scope,
        // kill switch) are written by the actor resolver instead.
        var status = context.Response.StatusCode;
        var isDenied = isTokenEndpoint
            ? status is StatusCodes.Status400BadRequest
                or StatusCodes.Status401Unauthorized
                or StatusCodes.Status403Forbidden
            : status is StatusCodes.Status401Unauthorized
                or StatusCodes.Status403Forbidden;
        if (isDenied)
        {
            await AppendAutomationDeniedSecurityEventAsync(context, isTokenEndpoint);
        }
    });
}
app.UseAuthentication();
app.Use(async (context, next) =>
{
    // Identity's validation-driven cookie reissue used to incidentally attach
    // no-store to most protected Razor responses. Preserve that requirement at
    // the response owner without overwriting the document preview's explicit
    // private cache policy or static-asset cache headers.
    if (context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.RazorPages.PageActionDescriptor>() is not null
        && context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null
        && !context.Response.Headers.ContainsKey("Cache-Control"))
    {
        context.Response.Headers.CacheControl = "private, no-store";
    }

    await next(context);
});
app.Use(async (context, next) =>
{
    if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null
        && context.User.Identity?.IsAuthenticated == true)
    {
        var userManager = context.RequestServices
            .GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = await userManager.GetUserAsync(context.User);
        var path = context.Request.Path;
        var allowedWhilePasswordChangeRequired =
            path.StartsWithSegments("/Account/PasswordChange")
            || path.StartsWithSegments("/Account/SignOut")
            || path.StartsWithSegments("/css")
            || path.StartsWithSegments("/js")
            || path.StartsWithSegments("/lib")
            || path.StartsWithSegments("/favicon.ico");
        if (user?.MustChangePassword == true && !allowedWhilePasswordChangeRequired)
        {
            context.Response.Redirect("/Account/PasswordChange");
            return;
        }
    }

    await next(context);
});
app.UseAuthorization();
if (!documentCustodyEnabled)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        var isDocumentUi = path.StartsWithSegments("/cases")
            && path.Value?.EndsWith("/documents", StringComparison.OrdinalIgnoreCase) == true;
        if (isDocumentUi)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
}
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
})
    .AllowAnonymous()
    .ShortCircuit();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
})
    .AllowAnonymous()
    .ShortCircuit();
app.MapHealthChecks("/health/warm", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("warm")
})
    .AllowAnonymous()
    .ShortCircuit();

app.MapStaticAssets()
    .AllowAnonymous()
    .ShortCircuit();
startupTimeline.Mark("static assets mapped");
app.MapGet("/diagnostics/version", () => Results.Ok(new
{
    version = productVersion,
    sourceSha
})).AllowAnonymous()
    .ShortCircuit();
app.MapPost("/hooks/microsoft-graph/mail", GraphMailWebhook.HandleAsync)
    .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(64 * 1024))
    .AllowAnonymous();
app.MapRazorPages()
   .WithStaticAssets();
if (automationMcpOptions is not null)
{
    app.MapPegasusAutomationMcp();
}
startupTimeline.Mark("Razor Pages and MCP mapped");
if (principalApiEnabled)
{
    app.MapPegasusPrincipalApi();
}

startupTimeline.Mark("pipeline built, starting to listen");
app.Lifetime.ApplicationStarted.Register(() =>
{
    startupTimeline.Mark("listening");
    // Information reaches Application Insights only for this category (see
    // appsettings.json), so the next slow start can be read from telemetry.
    StartupLog.Phases(
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(StartupTimeline.Category),
        startupTimeline.Summary());
});
app.Run();


/// <summary>
/// Paths whose callers are programs, not people: they want a status code and a
/// parsable body, and a re-executed HTML card would break them.
/// </summary>
static bool IsMachineSurface(PathString path) =>
    path.StartsWithSegments("/health")
    || path.StartsWithSegments("/diagnostics")
    || path.StartsWithSegments(AutomationMcp.McpEndpointPath)
    || path.Equals(AutomationMcp.TokenEndpointPath, StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments(PrincipalApi.BasePath);

static async Task BootstrapProductionAdministratorAsync(IServiceProvider services)
{
    if (Console.IsInputRedirected)
    {
        throw new InvalidOperationException(
            "Production Administrator bootstrap requires an interactive terminal.");
    }

    Console.Write("Username (must be alex): ");
    var username = Console.ReadLine();
    if (!string.Equals(username, "alex", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The first production Administrator must be exactly alex.");
    }
    Console.Write("Temporary password: ");
    var password = ReadSecret();
    Console.Write("Confirm temporary password: ");
    var confirmation = ReadSecret();
    if (!string.Equals(password, confirmation, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The temporary passwords did not match.");
    }

    await using var scope = services.CreateAsyncScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    if (await userManager.Users.AnyAsync())
    {
        throw new InvalidOperationException(
            "Production Administrator bootstrap refuses to run after any application user exists.");
    }
    if (!await roleManager.RoleExistsAsync(StaffRoleNames.Administrator))
    {
        var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(StaffRoleNames.Administrator));
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Administrator role creation failed: {string.Join(',', roleResult.Errors.Select(error => error.Code))}");
        }
    }

    var user = new PegasusIdentityUser
    {
        Id = Guid.NewGuid(),
        UserName = "alex",
        IsEnabled = true,
        MustChangePassword = true,
        SecurityStamp = Guid.NewGuid().ToString("N")
    };
    var createResult = await userManager.CreateAsync(user, password);
    if (!createResult.Succeeded)
    {
        throw new InvalidOperationException(
            $"Administrator creation failed: {string.Join(',', createResult.Errors.Select(error => error.Code))}");
    }
    var addRoleResult = await userManager.AddToRoleAsync(user, StaffRoleNames.Administrator);
    if (!addRoleResult.Succeeded)
    {
        throw new InvalidOperationException(
            $"Administrator role assignment failed: {string.Join(',', addRoleResult.Errors.Select(error => error.Code))}");
    }
}

static string ReadSecret()
{
    var characters = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return new string(characters.ToArray());
        }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (characters.Count > 0)
            {
                characters.RemoveAt(characters.Count - 1);
            }
            continue;
        }
        if (!char.IsControl(key.KeyChar))
        {
            characters.Add(key.KeyChar);
        }
    }
}

public partial class Program
{
}

internal static class StartupLog
{
    private static readonly Action<ILogger, string, Exception?> PhasesMessage =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(Phases)),
            "Web is listening. Startup phases: {Phases}");

    public static void Phases(ILogger logger, string phases) =>
        PhasesMessage(logger, phases, null);
}

internal sealed class DevelopmentOfflineAuthenticationHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    IConfiguration configuration,
    IHostEnvironment environment,
    UserManager<PegasusIdentityUser> userManager,
    IUserClaimsPrincipalFactory<PegasusIdentityUser> claimsPrincipalFactory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment()
            || configuration["Runtime:Profile"]?.Equals(
                "DevelopmentOffline",
                StringComparison.Ordinal) != true)
        {
            return AuthenticateResult.NoResult();
        }

        var user = await userManager.FindByIdAsync(
            DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
        if (user is null
            || !user.IsEnabled
            || user.MustChangePassword
            || user.PasswordHash is not null
            || !string.Equals(
                user.UserName,
                DevelopmentOfflineIdentity.UserName,
                StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var principal = await claimsPrincipalFactory.CreateAsync(user);
        if (!principal.IsInRole(StaffRoleNames.Administrator))
        {
            return AuthenticateResult.NoResult();
        }

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
