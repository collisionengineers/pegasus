using System.Reflection;
using System.Text.Json;
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
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Intake;
using Pegasus.Web.Health;
using Pegasus.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Web.Pages.Uploads;

const string OriginalIssueClaim = "pegasus:original-issued-at";
const string DevelopmentOfflineProfile = "DevelopmentOffline";
const string DevelopmentOfflineAuthenticationScheme = "DevelopmentOffline";
const string AuthenticationRoutingScheme = "Pegasus";
const string StaffSignInRateLimitPolicy = "StaffSignIn";
const string InitializeDevelopmentArgument = "--initialize-development";
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
if ((initializeDevelopment ? 1 : 0)
    + (migrateDevelopment ? 1 : 0) > 1)
{
    throw new InvalidOperationException(
        "Development initialization and migration commands must be run separately.");
}

var applicationArgs = args
    .Where(argument =>
        !argument.Equals(InitializeDevelopmentArgument, StringComparison.Ordinal)
        && !argument.Equals("--migrate-development", StringComparison.Ordinal))
    .ToArray();
var builder = WebApplication.CreateBuilder(applicationArgs);
var configuredRuntimeProfile = builder.Configuration["Runtime:Profile"]
    ?? throw new InvalidOperationException("Runtime:Profile is required.");
var developmentOfflineProfile = builder.Environment.IsDevelopment()
    && configuredRuntimeProfile.Equals(DevelopmentOfflineProfile, StringComparison.Ordinal);
var localDocumentCustodyConfigured =
    builder.Configuration.GetValue<bool>("Features:LocalDocumentCustody");
Func<IServiceProvider, RequestUploadLimits>? requestUploadLimitsFactory = null;
var acceptedRequestLimitsVersion =
    builder.Configuration["DocumentRequests:AcceptedLimitsVersion"];
if (localDocumentCustodyConfigured
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


builder.Services.AddRazorPages();
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
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = (10 * 1024 * 1024) + (64 * 1024);
});


builder.Services.AddPegasusInfrastructure((serviceProvider, options) =>
{
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
        .GetConnectionString("Pegasus")
        ?? throw new InvalidOperationException("Connection string 'Pegasus' is required.");
    options.UseSqlServer(connectionString);
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
}, requestUploadLimitsFactory: requestUploadLimitsFactory,
evaMappingAcceptanceFactory: serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    return new EvaMappingAcceptance(
        configuration["Eva:AcceptedMapping:Key"],
        configuration.GetValue<int?>("Eva:AcceptedMapping:Version"),
        configuration["Eva:AcceptedMapping:EvidenceReference"]);
});
if (developmentOfflineProfile)
{
    builder.Services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay);
}
builder.Services.AddSingleton<QdosAlphaAcceptanceGate>();
builder.Services.AddScoped<EfIdentityAuditStore>();
builder.Services.AddScoped<ISecurityEventWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIdentityAuditStore>());
builder.Services.AddScoped<IActionHistoryWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIdentityAuditStore>());
builder.Services.AddScoped<ICaseAcceptanceStore, EfCaseAcceptanceStore>();
builder.Services.AddScoped<IAcceptIntake, AcceptIntake>();
builder.Services.AddScoped<IInspectionAddressResolutionStore, InspectionAddressResolutionStore>();
if (requestUploadLimitsFactory is not null)
{
    builder.Services.AddSingleton<RequestUploadAttemptLimiter>();
}
builder.Services.AddScoped<IMailRoutePolicy>(serviceProvider =>
    serviceProvider.GetRequiredService<IInstructionExtractionPolicy>() as IMailRoutePolicy
    ?? throw new InvalidOperationException(
        "The configured instruction extraction policy must implement the mail-route policy contract."));
builder.Services.AddScoped<EfIntakeWorkStore>();
builder.Services.AddScoped<IIntakeWorkStore>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIntakeWorkStore>());
builder.Services.AddScoped<IStagedArtifactAuthority>(serviceProvider =>
    serviceProvider.GetRequiredService<EfIntakeWorkStore>());
builder.Services.AddScoped<ReceiveIntake>();
builder.Services.AddScoped<ProcessQueuedIntake>();
builder.Services.AddScoped<IIntakeSubmission>(serviceProvider =>
    serviceProvider.GetRequiredService<ReceiveIntake>());

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
if (!localDocumentCustodyEnabled)
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
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/uploads"))
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
app.MapRazorPages()
   .WithStaticAssets();

app.Run();


public partial class Program
{
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
