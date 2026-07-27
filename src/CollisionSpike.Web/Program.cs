using CollisionSpike.Infrastructure;
using CollisionSpike.Infrastructure.Persistence;
using CollisionSpike.Web.Health;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

const string DevelopmentOfflineProfile = "DevelopmentOffline";
var migrateDevelopment = args.Contains("--migrate-development", StringComparer.Ordinal);
var applicationArgs = args
    .Where(argument => !argument.Equals("--migrate-development", StringComparison.Ordinal))
    .ToArray();
var builder = WebApplication.CreateBuilder(applicationArgs);

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = (10 * 1024 * 1024) + (64 * 1024);
});


builder.Services.AddCollisionSpikeInfrastructure((serviceProvider, options) =>
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
        var connectionName = configuration["Database:ConnectionStringName"] ?? "CollisionSpike";
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
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    if (context.Database.IsSqlite())
    {
        await DevelopmentSqliteBaselineGuard.ValidateAsync(context);
    }

    await context.Database.MigrateAsync();
    Console.WriteLine("Development database migrations applied.");
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

app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new()
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program;
