using CollisionSpike.Infrastructure;
using CollisionSpike.Infrastructure.Persistence;
using CollisionSpike.Web.Health;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = (10 * 1024 * 1024) + (64 * 1024);
});

var localArtifactRoot = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["Intake:LocalArtifactPath"] ?? "../../artifacts/intake"));

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
}, localArtifactRoot);

var app = builder.Build();
var localIntakeEnabled = app.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Features:LocalIntake");

if (app.Environment.IsDevelopment()
    && app.Configuration["Database:Provider"]?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
{
    await using var scope = app.Services.CreateAsyncScope();
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await DevelopmentSqliteBaselineGuard.ValidateAsync(context);
    await context.Database.MigrateAsync();
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
