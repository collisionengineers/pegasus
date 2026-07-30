using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Health;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Pegasus.Core.Access;
using Pegasus.Web.Auth;
const string DevelopmentOfflineProfile = "DevelopmentOffline";

var migrateDevelopment = args.Contains("--migrate-development", StringComparer.Ordinal);
var bootstrapStaff = args.Contains("--bootstrap-staff", StringComparer.Ordinal);
var applicationArgs = args
    .Where(argument => !argument.Equals("--migrate-development", StringComparison.Ordinal)
        && !argument.Equals("--bootstrap-staff", StringComparison.Ordinal))
    .ToArray();
var builder = WebApplication.CreateBuilder(applicationArgs);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IStaffActorAccessor, HttpStaffActorAccessor>();
builder.Services.AddIdentityCore<StaffAccount>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
    .AddRoles<StaffRoleEntity>()
    .AddEntityFrameworkStores<PegasusDbContext>()
    .AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    options.LoginPath = "/Account/SignIn";
    options.AccessDeniedPath = "/Account/Denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Events.OnValidatePrincipal = async context =>
    {
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<StaffAccount>>();
        var id = userManager.GetUserId(context.Principal!);
        var user = id is null ? null : await userManager.FindByIdAsync(id);
        if (user is null || user.DisabledAtUtc is not null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser().Build());
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
static void EnsureIdentity(IdentityResult result)
{
    if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
}
if (bootstrapStaff)
{
    await using var scope = app.Services.CreateAsyncScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<StaffAccount>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<StaffRoleEntity>>();
    if (await userManager.Users.AnyAsync())
        throw new InvalidOperationException("Staff bootstrap refuses to run after the first account exists.");
    var username = app.Configuration["StaffBootstrap:UserName"];
    var displayName = app.Configuration["StaffBootstrap:DisplayName"];
    var password = app.Configuration["StaffBootstrap:Password"];
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(password))
        throw new InvalidOperationException("StaffBootstrap UserName, DisplayName, and Password are required.");
    foreach (var role in new[] { "Administrator", "Engineer", "User" })
        if (!await roleManager.RoleExistsAsync(role))
            EnsureIdentity(await roleManager.CreateAsync(new StaffRoleEntity(role)));
    var account = new StaffAccount { UserName = username, DisplayName = displayName, ForcePasswordChange = true };
    EnsureIdentity(await userManager.CreateAsync(account, password));
    EnsureIdentity(await userManager.AddToRoleAsync(account, "Administrator"));
    Console.WriteLine("Administrator bootstrap completed.");
    return;
}

var localIntakeEnabled = developmentOffline && localIntakeConfigured;

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

app.MapStaticAssets();
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new()
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program;
