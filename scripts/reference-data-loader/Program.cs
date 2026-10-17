using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.ReferenceDataLoader;

internal static class Program
{
    /// <summary>The connection string the Web and the Worker read as <c>ConnectionStrings:Pegasus</c>.</summary>
    private const string ConnectionStringVariable = "ConnectionStrings__Pegasus";

    public static Task<int> Main(string[] args) => LoaderCommand.RunAsync(
        args,
        Environment.GetEnvironmentVariable(ConnectionStringVariable),
        Console.Out,
        Console.Error,
        CancellationToken.None);
}

/// <summary>
/// The one-off, operator-run load of the reviewed register (operator, 15
/// October 2026: "One-off operator-run loader through
/// <c>IContactDirectoryAdministration.SaveAsync</c> from reviewed CSVs under
/// <c>artifacts/</c>"). It composes Core and Infrastructure, hosts nothing and
/// owns no policy.
/// </summary>
internal static class LoaderCommand
{
    internal const string Usage = """
        Pegasus reference-data loader (docs/runbook.md, Reference-data load)

        dry-run --data <folder> --actor-staff-id <id> --batch <id> --reviewed-at <yyyy-MM-dd> --report <file.json>
        apply   the dry-run's options and --target <server>/<database> --approved-report-sha256 <plan hash>

        The connection string is read from ConnectionStrings__Pegasus.
        """;

    internal static async Task<int> RunAsync(
        string[] args,
        string? connectionString,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = LoaderOptions.Parse(args);

            // A missing file or column stops the load before the database is read.
            var data = await ReferenceFiles.ReadAsync(options.Data, cancellationToken);
            string connection = string.IsNullOrWhiteSpace(connectionString)
                ? throw new InvalidOperationException("ConnectionStrings__Pegasus is not set.")
                : connectionString;
            var target = TargetOf(connection);
            if (options.Apply && !string.Equals(options.Target, target, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"--target {options.Target} is not the connection string's {target}; nothing was written.");
            }

            var services = new ServiceCollection();
            services.AddPegasusInfrastructure((_, database) => PegasusSqlServer.Configure(database, connection));
            await using var provider = services.BuildServiceProvider(validateScopes: true);
            await using var scope = provider.CreateAsyncScope();
            var actor = await RequireAdministratorAsync(
                scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>(), options.ActorStaffId, cancellationToken);
            var settings = new LoadSettings(target, actor, options.Batch, options.ReviewedAtUtc);
            var ports = new LoadPorts(
                scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>(),
                scope.ServiceProvider.GetRequiredService<IGetPrincipal>(),
                scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>(),
                scope.ServiceProvider.GetRequiredService<IInspectionLocationAdministration>());

            var planned = await new LoadPlanner(data, settings, ports).PlanAsync(cancellationToken);
            var planHash = LoadReport.Hash(planned.Plan);
            LoadOutcome? outcome = null;
            if (options.Apply)
            {
                // The plan is recomputed from the stored state and the files;
                // anything changed since the approved dry-run changes its hash.
                if (!string.Equals(planHash, options.ApprovedReportSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"The plan hash is now {planHash}, not the approved {options.ApprovedReportSha256}; nothing was written.");
                }

                outcome = await LoadRunner.ApplyAsync(planned, settings, ports, cancellationToken);
            }

            await LoadReport.WriteAsync(options.Report, planHash, planned.Plan, outcome, cancellationToken);
            output.WriteLine(LoadReport.Summary(planHash, options.Report, outcome));
            return outcome is { Rejects.Count: > 0 } ? 1 : 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>The <c>&lt;server&gt;/&lt;database&gt;</c> a connection string names.</summary>
    internal static string TargetOf(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return $"{builder.DataSource}/{builder.InitialCatalog}";
    }

    /// <summary>The actor every write names: an enabled Administrator, never Automation.</summary>
    private static async Task<ActionActor> RequireAdministratorAsync(
        IStaffAccountQueries staff,
        Guid staffId,
        CancellationToken cancellationToken)
    {
        var account = await staff.GetAsync(staffId, cancellationToken);
        if (account is not { IsEnabled: true, Role: StaffRole.Administrator })
        {
            throw new InvalidOperationException($"--actor-staff-id {staffId} is not an enabled Administrator.");
        }

        return ActionActor.Staff(staffId, [StaffRole.Administrator]);
    }
}

internal sealed record LoaderOptions(
    bool Apply,
    string Data,
    Guid ActorStaffId,
    string Batch,
    DateTimeOffset ReviewedAtUtc,
    string Report,
    string? Target,
    string? ApprovedReportSha256)
{
    private static readonly string[] DryRunOptions = ["data", "actor-staff-id", "batch", "reviewed-at", "report"];
    private static readonly string[] ApplyOptions = [.. DryRunOptions, "target", "approved-report-sha256"];

    internal static LoaderOptions Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("dry-run" or "apply"))
        {
            throw new ArgumentException(LoaderCommand.Usage);
        }

        var apply = args[0] == "apply";
        var allowed = apply ? ApplyOptions : DryRunOptions;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var name = args[index].StartsWith("--", StringComparison.Ordinal) ? args[index][2..] : string.Empty;
            if (!allowed.Contains(name, StringComparer.Ordinal) || index + 1 == args.Length)
            {
                throw new ArgumentException($"{args[index]} is not an option here.{Environment.NewLine}{LoaderCommand.Usage}");
            }

            values[name] = args[index + 1];
        }

        string Required(string name) =>
            values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"--{name} is required.");

        // The batch is part of every operation key, ref04:<batch>:<kind>:<row>:v<version>.
        var batch = Required("batch");
        if (batch.Length > 32 || !batch.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            throw new ArgumentException("--batch is at most 32 letters, digits and hyphens.");
        }

        if (!Guid.TryParse(Required("actor-staff-id"), out var actorStaffId))
        {
            throw new ArgumentException("--actor-staff-id is not a staff id.");
        }

        if (!DateOnly.TryParseExact(
            Required("reviewed-at"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var reviewedAt))
        {
            throw new ArgumentException("--reviewed-at is not a yyyy-MM-dd date.");
        }

        var report = ReferenceFiles.RequireArtifactsPath(Required("report"), "report");
        if (!report.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("--report names a .json file; the text report is written beside it.");
        }

        return new(
            apply,
            ReferenceFiles.RequireArtifactsPath(Required("data"), "data"),
            actorStaffId,
            batch,
            new DateTimeOffset(reviewedAt.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            report,
            apply ? Required("target") : null,
            apply ? Required("approved-report-sha256") : null);
    }
}
