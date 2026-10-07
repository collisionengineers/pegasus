using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The Worker's timers are named in four places besides the source: the bicep app
/// settings, the release scripts' Disabled-setting census, the local example settings
/// and the local development script. These tests read each of them against the compiled
/// functions so a function added or retired cannot leave one behind.
/// </summary>
public sealed partial class WorkerTimerContractTests
{
    /// <summary>
    /// Every timer's schedule. Nothing here may get slower (roadmap decision N2): the
    /// staged sweep is 10 seconds, the other timers are one minute, and the due-work
    /// sweep and inbox recovery ride the one-minute recovery timer every fifth minute.
    /// </summary>
    private static readonly Dictionary<string, string> ExpectedSchedules = new(StringComparer.Ordinal)
    {
        ["PendingWorkRecoverySchedule"] = "0 * * * * *",
        ["SentEvidencePollSchedule"] = "0 * * * * *",
        ["IntakeStagedArtifactReconciliationSchedule"] = "*/10 * * * * *"
    };

    [Fact]
    public void EveryWorkerFunctionHasOneDisabledSettingInBicepAndNoOther()
    {
        var bicep = ReadRepositoryFile("infra", "modules", "platform.bicep");

        Assert.Equal(
            WorkerFunctionSet.ExpectedNames.Order(StringComparer.Ordinal),
            DisabledSettingNames(bicep).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("PegasusPlatform.ps1")]
    [InlineData("Test-PegasusPlatform.ps1")]
    public void TheReleaseScriptsCensusIsExactlyTheWorkerFunctions(string script)
    {
        var text = ReadRepositoryFile("scripts", script);

        Assert.Equal(
            WorkerFunctionSet.ExpectedNames.Order(StringComparer.Ordinal),
            DisabledSettingNames(text).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryTimerScheduleIsDeclaredEverywhereItIsNeededAndKeepsItsCadence()
    {
        var bicep = ReadRepositoryFile("infra", "modules", "platform.bicep");
        var localScript = ReadRepositoryFile("scripts", "Invoke-LocalDevelopment.ps1");
        using var example = JsonDocument.Parse(
            ReadRepositoryFile("src", "Pegasus.Worker", "local.settings.example.json"));
        var exampleValues = example.RootElement.GetProperty("Values");

        var usedSettings = TimerScheduleSettings().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(ExpectedSchedules.Keys.Order(StringComparer.Ordinal), usedSettings);

        foreach (var (setting, cadence) in ExpectedSchedules)
        {
            Assert.Contains($"{{ name: '{setting}', value: '{cadence}' }}", bicep, StringComparison.Ordinal);
            Assert.Equal(cadence, exampleValues.GetProperty(setting).GetString());
            Assert.Contains($"{setting} = '{cadence}'", localScript, StringComparison.Ordinal);
        }

        // A schedule setting no timer reads is a retired timer's leftover.
        var declaredSchedules = ScheduleSettingNames().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(usedSettings, declaredSchedules);
        Assert.Equal(
            usedSettings,
            exampleValues.EnumerateObject()
                .Select(property => property.Name)
                .Where(name => name.EndsWith("Schedule", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            usedSettings,
            LocalScriptScheduleNames(localScript).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A timer of one minute or longer writes no monitor blob to storage on each run: a
    /// missed firing after a restart is not replayed, which these idempotent recovery
    /// sweeps allow. The 10-second sweep keeps its monitor.
    /// </summary>
    [Fact]
    public void OnlyTheTenSecondSweepKeepsItsMonitorBlob()
    {
        var monitored = WorkerFunctionSet.Declarations()
            .SelectMany(function => function.Method.GetParameters()
                .Select(parameter => parameter.GetCustomAttribute<TimerTriggerAttribute>())
                .OfType<TimerTriggerAttribute>()
                .Select(trigger => (function.Name, trigger.UseMonitor)))
            .Where(timer => timer.UseMonitor)
            .Select(timer => timer.Name);

        Assert.Equal([nameof(StagedArtifactReconciliationFunction)], monitored);
    }

    /// <summary>
    /// A safety bound on the Worker's scale-out. Flex Consumption applies it to the on-demand
    /// instances of each function group, so it cannot starve a group. It is not a saving:
    /// the Worker has never run more than two instances at once.
    /// </summary>
    [Fact]
    public void WorkerScaleOutIsBoundedAtFiveOnDemandInstancesPerFunctionGroup()
    {
        var bicep = ReadRepositoryFile("infra", "modules", "platform.bicep");

        Assert.Single(Regex.Matches(bicep, @"maximumInstanceCount:\s*\d+"));
        Assert.Contains("maximumInstanceCount: 5", bicep, StringComparison.Ordinal);
    }

    private static IEnumerable<string> ScheduleSettingNames() =>
        ScheduleSettingPattern().Matches(ReadRepositoryFile("infra", "modules", "platform.bicep"))
            .Select(match => match.Groups[1].Value);

    private static IEnumerable<string> LocalScriptScheduleNames(string localScript) =>
        LocalScheduleAssignmentPattern().Matches(localScript).Select(match => match.Groups[1].Value);

    private static IEnumerable<string> DisabledSettingNames(string text) =>
        DisabledSettingPattern().Matches(text).Select(match => match.Groups[1].Value);

    /// <summary>The app setting each timer function reads its schedule from.</summary>
    private static IEnumerable<string> TimerScheduleSettings() =>
        WorkerFunctionSet.Declarations()
            .SelectMany(function => function.Method.GetParameters()
                .Select(parameter => parameter.GetCustomAttribute<TimerTriggerAttribute>())
                .OfType<TimerTriggerAttribute>())
            .Select(trigger => SettingNamePattern().Match(trigger.Schedule).Groups[1].Value);

    [GeneratedRegex(@"'AzureWebJobs\.([A-Za-z]+)\.Disabled'")]
    private static partial Regex DisabledSettingPattern();

    [GeneratedRegex(@"name:\s*'([A-Za-z]+Schedule)'")]
    private static partial Regex ScheduleSettingPattern();

    [GeneratedRegex(@"^\s+([A-Za-z]+Schedule) = '", RegexOptions.Multiline)]
    private static partial Regex LocalScheduleAssignmentPattern();

    [GeneratedRegex(@"^%([A-Za-z]+)%$")]
    private static partial Regex SettingNamePattern();

    private static string ReadRepositoryFile(params string[] relativePath) =>
        File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. relativePath]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Pegasus repository root.");
    }
}
