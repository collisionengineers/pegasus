using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore;
using Pegasus.Infrastructure;
using Pegasus.Core;
using Pegasus.Core.Address;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Intake;
using Pegasus.Web.Health;
using Pegasus.Web.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Pegasus.Core.Identity;
using Pegasus.Web.Pages.Requests;
using Pegasus.Web.Pages.Connect;

const string OriginalIssueClaim = "pegasus:original-issued-at";
const string DevelopmentOfflineProfile = "DevelopmentOffline";
const string DevelopmentOfflineAuthenticationScheme = "DevelopmentOffline";
const string AuthenticationRoutingScheme = "Pegasus";
const string StaffSignInRateLimitPolicy = "StaffSignIn";
const string StaffMcpOAuthRateLimitPolicy = "StaffMcpOAuth";
const string StaffMcpRequestRateLimitPolicy = "StaffMcpRequest";
const string RegisterDevelopmentMcpClientArgument = "--register-development-mcp-client";
const string RevokeDevelopmentMcpClientArgument = "--revoke-development-mcp-client";
const string DevelopmentMcpClientId = "pegasus-development-mcp";
const string DevelopmentMcpRedirectUri = "http://127.0.0.1:7890/callback";
var migrateDevelopment = args.Contains("--migrate-development", StringComparer.Ordinal);
var registerDevelopmentMcpClient =
    args.Contains(RegisterDevelopmentMcpClientArgument, StringComparer.Ordinal);
var revokeDevelopmentMcpClient =
    args.Contains(RevokeDevelopmentMcpClientArgument, StringComparer.Ordinal);
if ((migrateDevelopment ? 1 : 0)
    + (registerDevelopmentMcpClient ? 1 : 0)
    + (revokeDevelopmentMcpClient ? 1 : 0) > 1)
{
    throw new InvalidOperationException(
        "Development migration and MCP client commands must be run separately.");
}

var applicationArgs = args
    .Where(argument =>
        !argument.Equals("--migrate-development", StringComparison.Ordinal)
        && !argument.Equals(RegisterDevelopmentMcpClientArgument, StringComparison.Ordinal)
        && !argument.Equals(RevokeDevelopmentMcpClientArgument, StringComparison.Ordinal))
    .ToArray();
var builder = WebApplication.CreateBuilder(applicationArgs);
var configuredRuntimeProfile = builder.Configuration["Runtime:Profile"]
    ?? throw new InvalidOperationException("Runtime:Profile is required.");
var developmentOfflineOAuth = builder.Environment.IsDevelopment()
    && configuredRuntimeProfile.Equals(DevelopmentOfflineProfile, StringComparison.Ordinal);
var requestedStaffMcpOAuth =
    builder.Configuration.GetValue<bool>("Features:StaffMcpOAuth");
if (requestedStaffMcpOAuth && !developmentOfflineOAuth)
{
    throw new InvalidOperationException(
        "Production staff MCP OAuth activation is blocked until the exact issuer/resource, " +
        "approved public client metadata, and Web-only signing/encryption certificate custody " +
        "have separately approved target evidence.");
}

var staffMcpOAuthEnabled = developmentOfflineOAuth || requestedStaffMcpOAuth;
StaffMcpOAuthOptions? staffMcpOAuth = null;
if (staffMcpOAuthEnabled)
{
    var issuer = ParseAbsoluteHttpsUri(
        builder.Configuration["OpenIddict:Issuer"] ?? "https://localhost:7139/",
        "OpenIddict:Issuer");
    var resource = ParseAbsoluteHttpsUri(
        builder.Configuration["OpenIddict:StaffMcpResource"]
            ?? new Uri(issuer, "/mcp").AbsoluteUri,
        "OpenIddict:StaffMcpResource");
    if (!resource.AbsolutePath.Equals("/mcp", StringComparison.Ordinal)
        || !resource.GetLeftPart(UriPartial.Authority)
            .Equals(issuer.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "OpenIddict:StaffMcpResource must be the /mcp endpoint on the configured issuer.");
    }

    staffMcpOAuth = new(issuer, resource);
    builder.Services.AddSingleton(staffMcpOAuth);
}
var localDocumentCustodyConfigured =
    builder.Configuration.GetValue<bool>("Features:LocalDocumentCustody");
if (staffMcpOAuthEnabled && !localDocumentCustodyConfigured)
{
    throw new InvalidOperationException(
        "Staff MCP requires the approved document-custody boundary; it cannot expose an incomplete tool map.");
}
Func<IServiceProvider, RequestUploadLimits>? requestUploadLimitsFactory = null;
if (localDocumentCustodyConfigured)
{
    requestUploadLimitsFactory = serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var section = configuration.GetRequiredSection("DocumentRequests");
        var allowedMediaTypes = section.GetSection("AllowedMediaTypes").Get<string[]>()
            ?? throw new InvalidOperationException(
                "DocumentRequests:AllowedMediaTypes is required when local document custody is enabled.");
        return new(
            section["LimitsVersion"]
                ?? throw new InvalidOperationException("DocumentRequests:LimitsVersion is required."),
            TimeSpan.FromHours(section.GetValue<double>("LifetimeHours")),
            section.GetValue<int>("MaximumFileCount"),
            section.GetValue<long>("MaximumFileBytes"),
            section.GetValue<long>("MaximumRequestBytes"),
            allowedMediaTypes,
            section.GetValue<int>("RateLimit"),
            TimeSpan.FromMinutes(section.GetValue<double>("RateLimitWindowMinutes")));
    };
}
var informationalVersion = typeof(Program).Assembly
    .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    ?? throw new InvalidOperationException("Assembly informational version is required.");
var buildMetadataSeparator = informationalVersion.IndexOf('+', StringComparison.Ordinal);
if (buildMetadataSeparator <= 0 || buildMetadataSeparator == informationalVersion.Length - 1)
{
    throw new InvalidOperationException(
        "Assembly informational version must contain the product version and source SHA.");
}

var productVersion = informationalVersion[..buildMetadataSeparator];
var sourceSha = informationalVersion[(buildMetadataSeparator + 1)..];
if (sourceSha.Length != 40 || sourceSha.Any(character => !char.IsAsciiHexDigit(character)))
{
    throw new InvalidOperationException(
        "Assembly informational version must contain a 40-character hexadecimal source SHA.");
}


builder.Services.AddRazorPages(options =>
    options.Conventions.AuthorizePage("/Intake/EmailEvaluation", "Administrator"));
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
        var reasonCode = context.HttpContext.Request.Path.Equals(
            "/Account/SignIn",
            StringComparison.OrdinalIgnoreCase)
            ? "sign_in_rate_limited"
            : context.HttpContext.Request.Path.StartsWithSegments("/mcp")
                ? "mcp_rate_limited"
                : "oauth_rate_limited";
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
        StaffMcpOAuthRateLimitPolicy,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy(
        StaffMcpRequestRateLimitPolicy,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 60,
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
var openIddict = builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<PegasusDbContext>();
    });
if (staffMcpOAuth is not null)
{
    openIddict
        .AddServer(options =>
        {
            options.SetIssuer(staffMcpOAuth.Issuer);
            options.SetAuthorizationEndpointUris("/connect/authorize")
                .SetRevocationEndpointUris("/connect/revoke")
                .SetTokenEndpointUris("/connect/token");
            options.AllowAuthorizationCodeFlow()
                .AllowRefreshTokenFlow()
                .RequireProofKeyForCodeExchange();
            options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
            options.SetRefreshTokenLifetime(StaffSessionPolicy.AbsoluteLifetime);
            options.DisableSlidingRefreshTokenExpiration();
            options.RegisterScopes(
                OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.OfflineAccess,
                StaffMcpOAuthOptions.ReadScope,
                StaffMcpOAuthOptions.WriteScope);
            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
            options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough();
        })
        .AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();
        });
    builder.Services.AddPegasusStaffMcp(staffMcpOAuth);
}

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = (10 * 1024 * 1024) + (64 * 1024);
});


builder.Services.AddPegasusInfrastructure((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var databaseProvider = configuration["Database:Provider"]
        ?? throw new InvalidOperationException("Database:Provider is required.");

    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var localPath = configuration["Database:LocalPath"]
            ?? throw new InvalidOperationException("Database:LocalPath is required for SQLite.");
        var fullPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, localPath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        options.UseSqlite(new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            ForeignKeys = true
        }.ToString());
        // The canonical migration snapshot targets SQL Server. Local SQLite uses the
        // same provider-aware migrations, while SQL Server integration tests retain
        // the pending-model guard for the release schema.
        options.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        return;
    }

    if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        var connectionName = configuration["Database:ConnectionStringName"] ?? "Pegasus";
        var connectionString = configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"Connection string '{connectionName}' is required.");
        options.UseSqlServer(connectionString);
        return;
    }

    throw new InvalidOperationException($"Unsupported database provider '{databaseProvider}'.");
}, serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var profile = configuration["Runtime:Profile"]
        ?? throw new InvalidOperationException("Runtime:Profile is required.");
    if (!profile.Equals(DevelopmentOfflineProfile, StringComparison.Ordinal)
        || !environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "The local intake artifact store is available only in the DevelopmentOffline runtime profile.");
    }

    var configuredArtifactRoot = configuration["Intake:LocalArtifactPath"]
        ?? throw new InvalidOperationException(
            "Intake:LocalArtifactPath is required for the DevelopmentOffline runtime profile.");
    return Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredArtifactRoot));
}, requestUploadLimitsFactory: requestUploadLimitsFactory);
builder.Services.AddSingleton<IIntakeEvaluationReportStore>(serviceProvider =>
    serviceProvider.GetRequiredService<IIntakeArtifactStore>() as IIntakeEvaluationReportStore
    ?? throw new InvalidOperationException(
        "The configured local intake artifact store must support evaluation reports."));
builder.Services.AddSingleton<QdosAlphaAcceptanceGate>();
builder.Services.AddScoped<EfIdentityAuditStore>();
builder.Services.AddScoped<ISecurityEventWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIdentityAuditStore>());
builder.Services.AddScoped<IActionHistoryWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIdentityAuditStore>());
builder.Services.AddScoped<IStaffAccountAdministration, EfStaffAccountAdministration>();
builder.Services.AddScoped<EfTriageStore>();
builder.Services.AddScoped<ITriageStore>(serviceProvider =>
    serviceProvider.GetRequiredService<EfTriageStore>());
builder.Services.AddScoped<ITriageQueries>(serviceProvider =>
    serviceProvider.GetRequiredService<EfTriageStore>());
builder.Services.AddScoped<ICaseAcceptanceStore, EfCaseAcceptanceStore>();
builder.Services.AddScoped<IAcceptIntake, AcceptIntake>();
builder.Services.AddScoped<IInspectionAddressResolutionStore, InspectionAddressResolutionStore>();
builder.Services.AddScoped<IEvaHandoffStore, EvaHandoffStore>();
builder.Services.AddSingleton<RequestUploadAttemptLimiter>();
builder.Services.AddScoped<IMailRoutePolicy>(serviceProvider =>
    serviceProvider.GetRequiredService<IInstructionExtractionPolicy>() as IMailRoutePolicy
    ?? throw new InvalidOperationException(
        "The configured instruction extraction policy must implement the mail-route policy contract."));
builder.Services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
builder.Services.AddScoped<ReceiveIntake>();
builder.Services.AddScoped<ProcessIntakeSubmission>();
builder.Services.AddScoped<IIntakeSubmission>(serviceProvider =>
{
    var profile = serviceProvider.GetRequiredService<IConfiguration>()["Runtime:Profile"]
        ?? throw new InvalidOperationException("Runtime:Profile is required.");
    if (profile.Equals(DevelopmentOfflineProfile, StringComparison.Ordinal))
    {
        return serviceProvider.GetRequiredService<ProcessIntakeSubmission>();
    }

    return serviceProvider.GetRequiredService<ReceiveIntake>();
});

var app = builder.Build();
var runtimeProfile = app.Configuration["Runtime:Profile"]
    ?? throw new InvalidOperationException("Runtime:Profile is required.");
var developmentOffline = runtimeProfile.Equals(
    DevelopmentOfflineProfile,
    StringComparison.Ordinal);
var localIntakeConfigured = app.Configuration.GetValue<bool>("Features:LocalIntake");
var localDocumentCustodyEnabled =
    developmentOffline && localDocumentCustodyConfigured;

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

var localIntakeEnabled = developmentOffline && localIntakeConfigured;
if (migrateDevelopment)
{
    if (!developmentOffline)
    {
        throw new InvalidOperationException(
            "--migrate-development requires the DevelopmentOffline runtime profile.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    if (context.Database.IsSqlite())
    {
        await DevelopmentSqliteBaselineGuard.ValidateAsync(context);
    }

    await context.Database.MigrateAsync();
    Console.WriteLine("Development database migrations applied.");
    return;
}
if (registerDevelopmentMcpClient || revokeDevelopmentMcpClient)
{
    if (!developmentOfflineOAuth)
    {
        throw new InvalidOperationException(
            "Development MCP client commands require the DevelopmentOffline runtime profile " +
            "and Development environment.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var applicationManager =
        scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    var securityEvents = scope.ServiceProvider.GetRequiredService<ISecurityEventWriter>();
    var application = await applicationManager.FindByClientIdAsync(DevelopmentMcpClientId);
    if (revokeDevelopmentMcpClient)
    {
        if (application is not null)
        {
            await applicationManager.DeleteAsync(application);
            await securityEvents.AppendAsync(
                new(
                    Guid.NewGuid(),
                    SecurityEventType.Client,
                    SecurityEventOutcome.Succeeded,
                    DevelopmentMcpClientId,
                    scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
                    "development-mcp-client-command",
                    "development_mcp_client_revoked"),
                CancellationToken.None);
        }

        Console.WriteLine("The deterministic DevelopmentOffline MCP client is revoked.");
        return;
    }

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = DevelopmentMcpClientId,
        ClientType = OpenIddictConstants.ClientTypes.Public,
        ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
        DisplayName = "Pegasus Development MCP client"
    };
    descriptor.RedirectUris.Add(new Uri(DevelopmentMcpRedirectUri));
    descriptor.Permissions.UnionWith(
    [
        OpenIddictConstants.Permissions.Endpoints.Authorization,
        OpenIddictConstants.Permissions.Endpoints.Revocation,
        OpenIddictConstants.Permissions.Endpoints.Token,
        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
        OpenIddictConstants.Permissions.ResponseTypes.Code,
        OpenIddictConstants.Permissions.Scopes.Profile,
        OpenIddictConstants.Permissions.Prefixes.Scope + StaffMcpOAuthOptions.ReadScope,
        OpenIddictConstants.Permissions.Prefixes.Scope + StaffMcpOAuthOptions.WriteScope
    ]);
    descriptor.Requirements.Add(
        OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

    if (application is null)
    {
        await applicationManager.CreateAsync(descriptor);
    }
    else
    {
        await applicationManager.UpdateAsync(application, descriptor);
    }
    await securityEvents.AppendAsync(
        new(
            Guid.NewGuid(),
            SecurityEventType.Client,
            SecurityEventOutcome.Succeeded,
            DevelopmentMcpClientId,
            scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
            "development-mcp-client-command",
            "development_mcp_client_registered"),
        CancellationToken.None);

    Console.WriteLine(
        "The deterministic DevelopmentOffline public PKCE MCP client is registered.");
    return;
}




if (!staffMcpOAuthEnabled)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/connect")
            || context.Request.Path.StartsWithSegments(
                "/.well-known/oauth-protected-resource"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.Use(async (context, next) =>
    {
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        await next(context);
    });
}

if (!localIntakeEnabled)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/Intake"))
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
            path.StartsWithSegments("/Account/ChangePassword")
            || path.StartsWithSegments("/Account/SignOut")
            || path.StartsWithSegments("/css")
            || path.StartsWithSegments("/js")
            || path.StartsWithSegments("/lib")
            || path.StartsWithSegments("/favicon.ico");
        if (user?.MustChangePassword == true && !allowedWhilePasswordChangeRequired)
        {
            context.Response.Redirect("/Account/ChangePassword");
            return;
        }
    }

    await next(context);
});
app.UseAuthorization();
if (!localDocumentCustodyEnabled)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        var isDocumentUi = path.StartsWithSegments("/requests")
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

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();

app.MapStaticAssets()
    .AllowAnonymous();
app.MapGet("/diagnostics/version", () => Results.Ok(new
{
    version = productVersion,
    sourceSha
})).AllowAnonymous();
if (staffMcpOAuth is not null)
{
    app.MapPegasusStaffMcp(StaffMcpRequestRateLimitPolicy);
    app.MapGet(
        "/.well-known/oauth-protected-resource",
        () => Results.Json(new
        {
            resource = staffMcpOAuth.Resource.AbsoluteUri,
            authorization_servers = new[] { staffMcpOAuth.Issuer.AbsoluteUri },
            scopes_supported = new[]
            {
                StaffMcpOAuthOptions.ReadScope,
                StaffMcpOAuthOptions.WriteScope
            },
            bearer_methods_supported = Program.BearerMethodsSupported
        }))
        .AllowAnonymous();
    app.MapPost(
        "/connect/token",
        async (
            HttpContext context,
            UserManager<PegasusIdentityUser> userManager,
            SignInManager<PegasusIdentityUser> signInManager) =>
        {
            var request = context.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException(
                    "The OpenIddict token request is unavailable.");
            var requestedResources = request.GetResources();
            if (requestedResources.Length != 1
                || !requestedResources[0].Equals(
                    staffMcpOAuth.Resource.AbsoluteUri,
                    StringComparison.Ordinal))
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.InvalidTarget,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The exact Pegasus staff MCP resource is required."
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            if (!request.IsAuthorizationCodeGrantType()
                && !request.IsRefreshTokenGrantType())
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.UnsupportedGrantType,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Only authorization-code and refresh-token grants are supported."
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            var authentication = await context.AuthenticateAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var authenticationPrincipal = authentication.Principal;
            var subject = authenticationPrincipal?.GetClaim(
                OpenIddictConstants.Claims.Subject);
            var user = string.IsNullOrWhiteSpace(subject)
                ? null
                : await userManager.FindByIdAsync(subject);
            if (authenticationPrincipal is null
                || user is null
                || !user.IsEnabled
                || user.MustChangePassword)
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The staff authorization is no longer valid."
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            var principal = await StaffMcpTokenPrincipal.CreateAsync(
                user,
                userManager,
                signInManager,
                authenticationPrincipal.GetScopes(),
                [staffMcpOAuth.Resource.AbsoluteUri]);
            principal.SetAuthorizationId(authenticationPrincipal.GetAuthorizationId());
            return Results.SignIn(
                principal,
                authentication.Properties,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        })
        .AllowAnonymous()
        .RequireRateLimiting(StaffMcpOAuthRateLimitPolicy);
}

app.MapRazorPages()
   .WithStaticAssets();

app.Run();


static Uri ParseAbsoluteHttpsUri(string value, string configurationKey)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
        || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrEmpty(uri.UserInfo)
        || !string.IsNullOrEmpty(uri.Query)
        || !string.IsNullOrEmpty(uri.Fragment))
    {
        throw new InvalidOperationException(
            $"{configurationKey} must be an absolute HTTPS URI without user information, query, or fragment.");
    }

    return uri;
}

public partial class Program
{
    internal static readonly string[] BearerMethodsSupported = ["header"];
}

internal sealed class DevelopmentOfflineAuthenticationHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    IConfiguration configuration,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string AdministratorId = "d47fbbae-ea22-4ca6-b983-01e2ed1fbd13";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment()
            || configuration["Runtime:Profile"]?.Equals(
                "DevelopmentOffline",
                StringComparison.Ordinal) != true)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new System.Security.Claims.ClaimsIdentity(
            [
                new(System.Security.Claims.ClaimTypes.NameIdentifier, AdministratorId),
                new(System.Security.Claims.ClaimTypes.Name, "DevelopmentOffline Administrator"),
                new(System.Security.Claims.ClaimTypes.Role, StaffRoleNames.Administrator)
            ],
            Scheme.Name);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
