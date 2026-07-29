using System.Reflection;
using Pegasus.Infrastructure;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Health;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;

const string OriginalIssueClaim = "pegasus:original-issued-at";
const string DevelopmentOfflineProfile = "DevelopmentOffline";
const string DevelopmentOfflineAuthenticationScheme = "DevelopmentOffline";
const string AuthenticationRoutingScheme = "Pegasus";
var migrateDevelopment = args.Contains("--migrate-development", StringComparer.Ordinal);
var applicationArgs = args
    .Where(argument => !argument.Equals("--migrate-development", StringComparison.Ordinal))
    .ToArray();
var builder = WebApplication.CreateBuilder(applicationArgs);
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
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/SignIn";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnSigningIn = context =>
    {
        var identity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
        if (identity is not null && !identity.HasClaim(claim => claim.Type == OriginalIssueClaim))
        {
            var clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
            identity.AddClaim(new(
                OriginalIssueClaim,
                clock.GetUtcNow().ToUnixTimeSeconds().ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
        }

        return Task.CompletedTask;
    };
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal is null)
        {
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
            || nowSeconds - issuedSeconds > (long)TimeSpan.FromHours(8).TotalSeconds)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = await userManager.GetUserAsync(context.Principal);
        if (user is null || !user.IsEnabled)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy("Administrator", policy =>
        policy.RequireRole(StaffRoleNames.Administrator));
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<PegasusDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetEndSessionEndpointUris("/connect/logout")
            .SetTokenEndpointUris("/connect/token");
        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange();
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromHours(8));
        options.DisableSlidingRefreshTokenExpiration();
        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.OfflineAccess,
            "pegasus.mcp.read",
            "pegasus.mcp.write");
        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
            options.UseAspNetCore();
        }
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        if (builder.Environment.IsDevelopment())
        {
            options.UseAspNetCore();
        }
    });

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = (10 * 1024 * 1024) + (64 * 1024);
});


builder.Services.AddPegasusInfrastructure((serviceProvider, options) =>
{
    options.UseOpenIddict();
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
});
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



if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
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

app.UseAuthentication();
app.UseAuthorization();

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
app.MapRazorPages()
   .WithStaticAssets();

app.Run();


public partial class Program;

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
