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
using Pegasus.Core.Eva;
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
using Pegasus.Web.AiWork;
using Pegasus.Web.Mcp;
using Pegasus.Web.ProviderApi;
using Pegasus.Web.Pages.Uploads;
using Pegasus.Web;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Eva;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Transport;
using Microsoft.ApplicationInsights.Extensibility;

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
var configuredRuntimeProfile = builder.Configuration["Runtime:Profile"]
    ?? throw new InvalidOperationException("Runtime:Profile is required.");
var developmentOfflineProfile = builder.Environment.IsDevelopment()
    && configuredRuntimeProfile.Equals(DevelopmentOfflineProfile, StringComparison.Ordinal);
var productionProfile = configuredRuntimeProfile.Equals("Production", StringComparison.Ordinal);
QueueClient? intakeWorkQueue = null;
var allowLocalQueueCreation = false;
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
        "Box:ConfigJson",
        "Box:ClientSecret",
        // EXT-04. Listed here so a deployment missing EVA's credentials fails
        // at startup naming the key, rather than at the first submission with
        // a case in front of an operator.
        "Eva:BaseUri",
        "Eva:ClientId",
        "Eva:ClientSecret",
        "Eva:RequestFrom",
        "Eva:InspectionType",
        "Eva:InstructionEmail"
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
    // PLAT-034: the deployed container has carried
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
        builder.Services.AddSingleton<ITelemetryInitializer, PublicUploadTelemetryInitializer>();
        builder.Services.Configure<TelemetryConfiguration>(
            telemetry => telemetry.SetAzureTokenCredential(credential));
    }
}
else
{
    var queueConnectionString = builder.Configuration["AzureWebJobsStorage"]
        ?? throw new InvalidOperationException(
            "AzureWebJobsStorage is required for DevelopmentOffline queue transport.");
    intakeWorkQueue = new QueueClient(queueConnectionString, "intake-work");
    allowLocalQueueCreation = true;
}
builder.Services.AddSingleton<ICursorProtector, DataProtectionCursorProtector>();
var localDocumentCustodyConfigured =
    builder.Configuration.GetValue<bool>("Features:LocalDocumentCustody");
Func<IServiceProvider, RequestUploadLimits>? requestUploadLimitsFactory = null;
var acceptedRequestLimitsVersion =
    builder.Configuration["DocumentRequests:AcceptedLimitsVersion"];
// INT-31 upload links stay inactive until their limits are accepted
// (docs/open-decisions.md). Production composes document custody but sets no
// accepted limits version, so the upload-link services stay unavailable there.
if ((localDocumentCustodyConfigured || productionProfile)
    && !string.IsNullOrWhiteSpace(acceptedRequestLimitsVersion))
{
    requestUploadLimitsFactory = serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var section = configuration.GetRequiredSection("DocumentRequests");
        var limitsVersion = section["LimitsVersion"]
            ?? throw new InvalidOperationException("DocumentRequests:LimitsVersion is required.");
        if (!string.Equals(
                limitsVersion,
                acceptedRequestLimitsVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "DocumentRequests:LimitsVersion must exactly match DocumentRequests:AcceptedLimitsVersion.");
        }

        var allowedMediaTypes = section.GetSection("AllowedMediaTypes").Get<string[]>()
            ?? throw new InvalidOperationException(
                "DocumentRequests:AllowedMediaTypes is required when accepted request limits are enabled.");
        return new(
            limitsVersion,
            TimeSpan.FromHours(section.GetValue<double>("LifetimeHours")),
            section.GetValue<int>("MaximumFileCount"),
            section.GetValue<long>("MaximumFileBytes"),
            section.GetValue<long>("MaximumRequestBytes"),
            allowedMediaTypes,
            section.GetValue<int>("RateLimit"),
            TimeSpan.FromMinutes(section.GetValue<double>("RateLimitWindowMinutes")));
    };
}


// The Automation MCP ingress is composition-gated off by default: when the
// flag is absent nothing below registers and no /mcp or /connect/token route
// exists. An explicitly configured deployment may enable it in Production.
var automationMcpOptions = AutomationMcpOptions.TryCreate(builder.Configuration);

// The Provider API (API-01) is gated the same way: off by default, and
// without the flag no /api/provider route, scheme or policy exists.
var providerApiEnabled = builder.Configuration.GetValue<bool>(ProviderApi.FeatureFlag);

// The Send to AI hand-off (AI-09) follows the same gate pattern: absent by
// default, DevelopmentOffline-only, and without it the assessment panel
// renders the unavailable state and no outbound transport exists.
var sendToAiOptions = SendToAiOptions.TryCreate(
    builder.Configuration,
    developmentOfflineProfile);

// RailCountsPageFilter supplies ViewData["RailCounts"] on every
// authenticated request (PLAT-003) — the rail (PLAT-001) shipped with the
// badge mechanism but nothing populated it until now. RazorPagesOptions has
// no Filters collection of its own, so the global filter is added through
// the underlying MvcOptions instead.
builder.Services.AddRazorPages()
    .AddMvcOptions(options => options.Filters.Add<Pegasus.Web.Presentation.RailCountsPageFilter>())
    // The anonymous upload link is the only Razor page reachable without a
    // session, so it is the only one that carries a transport-level bound.
    // Applying it here rather than on MapRazorPages() keeps every
    // authenticated page off the limiter.
    .AddRazorPagesOptions(options =>
    {
        options.Conventions.AddPageApplicationModelConvention(
            "/Uploads/Request",
            model => model.EndpointMetadata.Add(
                new EnableRateLimitingAttribute(PublicUploadLink.RateLimitPolicy)));
        options.Conventions.AddPageApplicationModelConvention(
            "/Integrations/Glass/Callback",
            model => model.EndpointMetadata.Add(
                new EnableRateLimitingAttribute(GlassCallbackRateLimitPolicy)));
        // CASE-038: the Case record's section fragment answers on its own
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
                : rejectedPath.StartsWithSegments(ProviderApi.BasePath)
                    ? "provider_api_rate_limited"
                    : rejectedPath.StartsWithSegments("/Integrations/Glass/Callback")
                        ? "glass_callback_rate_limited"
                    : rejectedPath.StartsWithSegments("/Uploads")
                        ? "upload_link_rate_limited"
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
    // The limiter runs before authentication, so a presented key id is a claim
    // and not an identity, and it cannot be the partition: naming another
    // provider's key id would spend that provider's budget with forged
    // secrets, and minting a fresh well-formed key id per request would hand
    // the caller a fresh budget each time and bound nothing at all. The
    // partition is the calling address, as it already is for staff sign-in and
    // the MCP ingress.
    // The anonymous upload link holds no credential and presents no identity,
    // and its per-token limiter cannot bound a caller who has no token, so the
    // partition is the calling address as it is for every other pre-auth
    // surface here.
    options.AddPolicy(
        PublicUploadLink.RateLimitPolicy,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = PublicUploadLink.RequestsPerClientPerMinute,
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
    options.AddPolicy(
        ProviderApi.RateLimitPolicy,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = ProviderApi.RequestsPerCallerPerMinute,
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
            return environment.IsDevelopment()
                && configuration["Runtime:Profile"]?.Equals(
                    DevelopmentOfflineProfile,
                    StringComparison.Ordinal) == true
                    ? DevelopmentOfflineAuthenticationScheme
                    : IdentityConstants.ApplicationScheme;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, DevelopmentOfflineAuthenticationHandler>(
        DevelopmentOfflineAuthenticationScheme,
        displayName: null,
        _ => { });
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
    options.OnRefreshingPrincipal = context =>
    {
        var originalIssue = context.CurrentPrincipal?.FindFirst(OriginalIssueClaim);
        var identity = context.NewPrincipal?.Identity as System.Security.Claims.ClaimsIdentity;
        if (originalIssue is not null
            && identity is not null
            && !identity.HasClaim(claim => claim.Type == OriginalIssueClaim))
        {
            identity.AddClaim(originalIssue);
        }

        return Task.CompletedTask;
    };
});
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
    options.Events.OnValidatePrincipal = async context =>
    {
        var subjectId = context.Principal?.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal is null)
        {
            await AppendSignInSecurityEventAsync(
                context.HttpContext,
                subjectId,
                SecurityEventOutcome.Denied,
                "invalid_security_stamp");
            return;
        }

        var nowSeconds = context.HttpContext.RequestServices
            .GetRequiredService<TimeProvider>()
            .GetUtcNow()
            .ToUnixTimeSeconds();
        var issuedValue = context.Principal.FindFirst(OriginalIssueClaim)?.Value;
        if (!long.TryParse(
                issuedValue,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var issuedSeconds)
            || issuedSeconds < 0
            || issuedSeconds > nowSeconds
            || nowSeconds - issuedSeconds >= (long)StaffSessionPolicy.AbsoluteLifetime.TotalSeconds)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await AppendSignInSecurityEventAsync(
                context.HttpContext,
                subjectId,
                SecurityEventOutcome.Denied,
                "absolute_session_expired");
            return;
        }

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = await userManager.GetUserAsync(context.Principal);
        if (user is null || !user.IsEnabled)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await AppendSignInSecurityEventAsync(
                context.HttpContext,
                subjectId,
                SecurityEventOutcome.Denied,
                "disabled_or_missing_staff");
        }
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
    return writer.AppendAsync(
        new SecurityEvent(
            Guid.NewGuid(),
            SecurityEventType.SignIn,
            outcome,
            subjectId,
            occurredAtUtc,
            context.TraceIdentifier,
            reasonCode),
        context.RequestAborted);
}

static Task AppendAutomationDeniedSecurityEventAsync(
    HttpContext context,
    bool tokenEndpoint)
{
    var writer = context.RequestServices.GetRequiredService<ISecurityEventWriter>();
    var occurredAtUtc = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    var subjectId = context.User.FindFirst(
        System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
    return writer.AppendAsync(
        new SecurityEvent(
            Guid.NewGuid(),
            SecurityEventType.Token,
            SecurityEventOutcome.Denied,
            subjectId,
            occurredAtUtc,
            context.TraceIdentifier,
            tokenEndpoint ? "automation_token_rejected" : "automation_access_denied"),
        CancellationToken.None);
}

static Task AppendRateLimitedSecurityEventAsync(
    HttpContext context,
    string reasonCode,
    CancellationToken cancellationToken)
{
    var writer = context.RequestServices.GetRequiredService<ISecurityEventWriter>();
    var occurredAtUtc = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
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
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);
builder.Services.Configure<FormOptions>(options =>
{
    // Bounded for a whole Upload batch, not one file: IntakeEnvelopeLimits
    // enforces the per-file cap and the maximum file count independently.
    options.MultipartBodyLengthLimit = IntakeEnvelopeLimits.MaximumBatchContentLength;
});

Func<IServiceProvider, string>? localArtifactRootFactory = developmentOfflineProfile
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
    options.UseSqlServer(connectionString);
}, localArtifactRootFactory, requestUploadLimitsFactory: requestUploadLimitsFactory,
documentStorage: !productionProfile
    ? null
    : (Action<IServiceCollection>)(registrations => registrations.AddProductionDocumentStorage(
        provider => provider.GetRequiredService<BlobContainerClient>(),
        // Web never provisions the container; the Worker owns that.
        static _ => false,
        // Deferred to first Box use: parsing this at host build aborted the
        // process whenever the platform handed over an unresolved Key Vault
        // reference (PLAT-013).
        _ => BoxCustodyOptions.Create(
            builder.Configuration["Box:BaseUri"],
            builder.Configuration["Box:UploadUri"],
            builder.Configuration["Box:RootFolderId"],
            builder.Configuration["Box:ConfigJson"],
            builder.Configuration["Box:ClientSecret"]))));
// EXT-04: the manual Send to EVA route. Production only — the offline
// profile reaches no vendor — and the options are read lazily for the same
// PLAT-013 reason as Box's.
if (productionProfile)
{
    builder.Services.AddEvaApiSubmission(
        _ => EvaApiOptions.Create(key => builder.Configuration[key]));
}

builder.Services.AddPegasusReportRendering();
if (developmentOfflineProfile)
{
    builder.Services.AddScoped<Pegasus.Core.Operations.IStaffReportSend, UnavailableStaffReportSend>();
    builder.Services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay);
    builder.Services.AddSingleton<IResolveApprovedMailboxIdentity, LocalApprovedMailboxIdentityResolver>();
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
builder.Services.AddScoped<IProviderInspectionModeStore, EfProviderInspectionModeStore>();
builder.Services.AddScoped<IInspectionAddressResolutionStore, InspectionAddressResolutionStore>();
if (requestUploadLimitsFactory is not null)
{
    builder.Services.AddSingleton<RequestUploadAttemptLimiter>();
}
builder.Services.AddScoped<EfIntakeWorkStore>();
builder.Services.AddScoped<IIntakeWorkStore>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIntakeWorkStore>());
builder.Services.AddScoped<IStagedArtifactAuthority>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIntakeWorkStore>());
builder.Services.AddScoped<IQueuedIntakeStatusQueries, EfQueuedIntakeStatusQueries>();
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
builder.Services.AddScoped<ReceiveIntake>();
builder.Services.AddScoped<IIntakeSubmission>(serviceProvider =>
    serviceProvider.GetRequiredService<ReceiveIntake>());
builder.Services.AddScoped<SubmitGroupedIntake>();
builder.Services.AddScoped<IGroupedIntakeSubmission>(serviceProvider =>
    serviceProvider.GetRequiredService<SubmitGroupedIntake>());
// The consolidated Automation activity read model backs the Administration
// view in every profile; the ingress itself stays behind the composition gate.
builder.Services.AddScoped<IAutomationActivityQueries, EfAutomationActivityStore>();
builder.Services.AddScoped<IListAutomationActivity, ListAutomationActivity>();
if (automationMcpOptions is not null)
{
    builder.Services.AddPegasusAutomationMcp(automationMcpOptions, productVersion);
}
if (providerApiEnabled)
{
    builder.Services.AddPegasusProviderApi();
}
if (sendToAiOptions is not null)
{
    builder.Services.AddPegasusSendToAi(sendToAiOptions);
}

var app = builder.Build();
var runtimeProfile = app.Configuration["Runtime:Profile"]
    ?? throw new InvalidOperationException("Runtime:Profile is required.");
var developmentOffline = runtimeProfile.Equals(
    DevelopmentOfflineProfile,
    StringComparison.Ordinal);
var localIntakeConfigured = app.Configuration.GetValue<bool>("Features:LocalIntake");
// The document surface follows composed custody, not the Development-only feature
// flag: Production composes Box-backed custody and must serve the staff pages.
var documentCustodyEnabled =
    (developmentOffline && localDocumentCustodyConfigured) || productionProfile;

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

// A named, disposable Administrator used to drive the deployed application
// through a browser during UI verification. Present only while
// `Bootstrap:VerificationAccount:UserName` and `:Password` are both configured;
// with the settings removed the account is deleted on the next start, so
// retiring it is a configuration change rather than a database chore.
//
// Production profile only. DevelopmentOffline authenticates every request as
// its own local identity, so a password account there would be an unused row
// that every test fixture then has to know about.
if (productionProfile
    && (builder.Configuration["Bootstrap:VerificationAccount:UserName"] is { Length: > 0 }
        || builder.Configuration["Bootstrap:VerificationAccount:Removed"] is { Length: > 0 }))
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        await ReconcileVerificationAccountAsync(scope.ServiceProvider, builder.Configuration);
    }
    catch (Exception exception)
    {
        // A temporary verification account is never worth refusing to start
        // over. The database may be unreachable or unmigrated at this point in
        // startup — both are the deployment's problem to report, not this
        // block's to escalate into an outage.
        BootstrapLog.VerificationAccountSkipped(
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Pegasus.Bootstrap"),
            exception);
    }
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
    // Container Apps ingress terminates TLS and forwards the original scheme in
    // X-Forwarded-Proto. Without this, Kestrel sees http, UseHttpsRedirection
    // loops, and every generated redirect and sign-in callback emits http://.
    // It must run before UseHsts and UseHttpsRedirection.
    // The ingress is not on a known network, so the proxy allow-lists are cleared.
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
    };
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

// Every status code that reaches a browser gets the designed page. Before this,
// an unknown record URL, a dead public upload link, an oversized upload and a
// rate-limited sign-in all rendered the browser's own error page — including on
// the one screen whose audience is outside Collision Engineers.
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
        // PDF in a same-origin iframe, and 'none' refuses that too -- the
        // browser blocks the frame and the operator gets a blank stage. The
        // clickjacking protection this header exists for is unchanged, because
        // 'self' still refuses every other origin. Development does not set the
        // header at all, so the tests could not have caught it (DOCS-011).
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'self'";
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

// The Provider API joins the same absence gates. Answering 404 before routing
// matters: the static-assets fallback owns a GET/HEAD-only catch-all over
// every file-shaped path, so an uncomposed POST here would otherwise surface
// as a 405 that discloses the route's shape instead of its absence.
if (!providerApiEnabled)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments(ProviderApi.BasePath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
}

app.UseHttpsRedirection();

app.UseRouting();
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
        var isDocumentUi = path.StartsWithSegments("/uploads")
            || path.StartsWithSegments("/requests")
            || (path.StartsWithSegments("/cases")
                && path.Value?.EndsWith("/documents", StringComparison.OrdinalIgnoreCase) == true);
        if (isDocumentUi)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
}
else if (requestUploadLimitsFactory is null)
{
    // Without accepted limits the upload-link services are the unavailable store,
    // whose staff commands throw. INT-31 is off the alpha path, so keep the whole
    // request surface absent in Production rather than offering a failing action.
    // DevelopmentOffline keeps its existing narrower gate.
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/uploads")
            || (productionProfile && path.StartsWithSegments("/requests")))
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

app.MapStaticAssets()
    .AllowAnonymous();
app.MapGet("/diagnostics/version", () => Results.Ok(new
{
    version = productVersion,
    sourceSha
})).AllowAnonymous();
app.MapPost("/hooks/microsoft-graph/mail", GraphMailWebhook.HandleAsync)
    .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(64 * 1024))
    .AllowAnonymous();
app.MapRazorPages()
   .WithStaticAssets();
if (automationMcpOptions is not null)
{
    app.MapPegasusAutomationMcp();
}
if (providerApiEnabled)
{
    app.MapPegasusProviderApi();
}

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
    || path.StartsWithSegments(ProviderApi.BasePath);

/// <summary>
/// Creates, updates, or removes the disposable UI-verification Administrator.
/// </summary>
/// <remarks>
/// This is not a second bootstrap path: it refuses to run unless an
/// Administrator already exists, so it can never be the route by which the
/// first privileged account in a deployment appears. It exists because
/// verifying the deployed interface means driving it as a real signed-in
/// operator, and doing that as `alex` would put the owner's own credentials
/// through an automated browser.
///
/// Setting `Bootstrap:VerificationAccount:Removed` (with no username) deletes
/// the account, so retirement is one configuration change and needs no
/// database surgery.
/// </remarks>
static async Task ReconcileVerificationAccountAsync(
    IServiceProvider services,
    IConfiguration configuration)
{
    var userName = configuration["Bootstrap:VerificationAccount:UserName"];
    var password = configuration["Bootstrap:VerificationAccount:Password"];
    var removed = configuration["Bootstrap:VerificationAccount:Removed"];

    var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    if (!string.IsNullOrWhiteSpace(removed))
    {
        var retired = await userManager.FindByNameAsync(removed.Trim());
        if (retired is not null)
        {
            await userManager.DeleteAsync(retired);
        }

        return;
    }

    if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
    {
        return;
    }

    // Fail closed rather than minting the first privileged account.
    if (!await userManager.Users.AnyAsync())
    {
        return;
    }

    if (!await roleManager.RoleExistsAsync(StaffRoleNames.Administrator))
    {
        return;
    }

    var trimmedUserName = userName.Trim();
    var existing = await userManager.FindByNameAsync(trimmedUserName);
    if (existing is null)
    {
        var created = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = trimmedUserName,
            IsEnabled = true,
            MustChangePassword = false,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        var createResult = await userManager.CreateAsync(created, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Verification account creation failed: "
                + string.Join(',', createResult.Errors.Select(error => error.Code)));
        }

        await userManager.AddToRoleAsync(created, StaffRoleNames.Administrator);
        return;
    }

    // Converge an existing account on the configured password and role, so a
    // rotated password in configuration is enough to restore access.
    existing.IsEnabled = true;
    existing.MustChangePassword = false;
    await userManager.UpdateAsync(existing);
    var resetToken = await userManager.GeneratePasswordResetTokenAsync(existing);
    await userManager.ResetPasswordAsync(existing, resetToken, password);
    if (!await userManager.IsInRoleAsync(existing, StaffRoleNames.Administrator))
    {
        await userManager.AddToRoleAsync(existing, StaffRoleNames.Administrator);
    }
}

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

internal static class BootstrapLog
{
    private static readonly Action<ILogger, Exception?> VerificationAccountSkippedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1, nameof(VerificationAccountSkipped)),
            "The verification account could not be reconciled.");

    public static void VerificationAccountSkipped(ILogger logger, Exception exception) =>
        VerificationAccountSkippedMessage(logger, exception);
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
