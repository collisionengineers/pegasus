using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The connector acts for the Automation Actor or for a member of staff
/// signed in from Pegasus Desktop (R1304, ADR-0092), so no tool may assume
/// which. A tool reads the actor, and every label and AI mark that goes with
/// it (R1306), from the resolved context, never naming one itself; and each place in Core and Infrastructure that names
/// the Automation actor kind is listed here with what it does for a member of
/// staff, so a new one is settled when it is written and not found later.
/// </summary>
public sealed partial class ConnectorActorTableTests
{
    /// <summary>What a tool would write if it assumed the Automation Actor.</summary>
    private static readonly string[] AutomationAssumptions =
    [
        "ActionActor.Automation(",
        "DocumentSource.Automation",
        "RepairSpecificationSourceRoute.AiDraft",
        "context.ClientId",
        "context.GrantId"
    ];

    /// <summary>
    /// The files in Core and Infrastructure that name
    /// <c>ActorKind.Automation</c>, how many times, and what each does when
    /// the connector's actor is a member of staff. Compared on a no-growth
    /// basis: a count may fall, and a new or larger one is settled here.
    /// </summary>
    private static readonly (string File, int Count, string Disposition)[] ActorKindAutomation =
    [
        ("Pegasus.Core/Actors/ActorDisplayNames.cs", 1, "Label of the kind; a member of staff reads by their username."),
        ("Pegasus.Core/Address/InspectionAddressResolution.cs", 3, "The settler is recorded by kind; a member of staff settles as staff."),
        ("Pegasus.Core/AiWork/AiJobOperations.cs", 3, "An AI job is the Automation Actor's to take, progress, complete, fail and release; a member of staff is refused those and may cancel or confirm, as in Pegasus."),
        ("Pegasus.Core/Assessment/AssessmentPolicy.cs", 2, "Admits both kinds; the other names a system fill's recorder."),
        ("Pegasus.Core/Assessment/Estimates.cs", 1, "Holds the Automation Actor to an AI draft; the connector files an AI draft for a member of staff too, from the context."),
        ("Pegasus.Core/Assessment/RepairSpecifications.cs", 1, "The Case page's editor route by kind; the connector's estimate is an AI draft that waits for Use repair spec whoever it is recorded under."),
        ("Pegasus.Core/Cases/CaseWorkspace.cs", 1, "Admits both kinds to the Case save."),
        ("Pegasus.Core/Identity/IdentityContracts.cs", 1, "The Automation actor's factory."),
        ("Pegasus.Core/Identity/StaffAuthorization.cs", 1, "Casework for both kinds; the management rights are a staff Administrator's alone."),
        ("Pegasus.Core/Intake/AcceptIntake.cs", 1, "Admits both kinds."),
        ("Pegasus.Core/Intake/AnalyzeRetainedInstruction.cs", 1, "Names a system recorder, not a caller."),
        ("Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs", 1, "Names a system recorder, not a caller."),
        ("Pegasus.Core/Intake/RetainedMail.cs", 1, "The kind's prefix in a recorded actor string; a member of staff is written staff."),
        ("Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs", 2, "Admits both kinds."),
        ("Pegasus.Core/Operations/StaffMailSendEngine.cs", 2, "Admits both kinds; a member of staff's send re-reads their account, the Automation Actor's its kill switch."),
        ("Pegasus.Core/Reports/CaseReportDelivery.cs", 1, "Admits both kinds."),
        ("Pegasus.Core/Triage/TriageLifecycle.cs", 1, "Admits both kinds."),
        ("Pegasus.Core/Workflow/CaseEditAuthority.cs", 4, "The lease rules by holder kind; a member of staff follows the staff rules (FRD-14)."),
        ("Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs", 1, "Admits both kinds."),
        ("Pegasus.Infrastructure/Persistence/EfAiJobStore.cs", 1, "Only the holding client moves a taken job on; a member of staff never holds one."),
        ("Pegasus.Infrastructure/Persistence/EfAutomationActivityStore.cs", 1, "Automation activity is the Automation Actor's; a member of staff's calls are not counted."),
        ("Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs", 1, "Admits both kinds."),
        ("Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs", 1, "Reads a recorded actor back by kind."),
        ("Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs", 4, "Lease rules by holder kind, the Stop's release of Automation leases, and a recorded actor read back."),
        ("Pegasus.Infrastructure/Persistence/EfEditScopeStore.cs", 1, "A write keeps an Automation scope and ends a staff one."),
        ("Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs", 1, "Reads a recorded actor back by kind."),
        ("Pegasus.Infrastructure/Persistence/EfIntakeAllocationStore.cs", 1, "Reads a recorded actor back by kind."),
        ("Pegasus.Infrastructure/Persistence/EfManualCaseCreationStore.cs", 1, "The history reason by kind; a member of staff's Case is created by staff."),
        ("Pegasus.Infrastructure/Persistence/EfPhotoCaptureCaseWriter.cs", 2, "Names a system fill's recorder."),
        ("Pegasus.Infrastructure/Persistence/EfPhotoCaptureQueries.cs", 1, "Reads a recorded actor back by kind."),
        ("Pegasus.Infrastructure/Persistence/EfRecentCaseQueries.cs", 1, "How a Case arrived, from what was recorded."),
        ("Pegasus.Infrastructure/Persistence/EfSystemActionQueries.cs", 1, "System Actions are the system's and the Automation Actor's; a member of staff's are not listed."),
        ("Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs", 2, "A system recorder's resolutions, and a recorded actor read back."),
        ("Pegasus.Infrastructure/Persistence/EfVehicleLookupWorkStore.cs", 1, "Names a system fill's recorder."),
        ("Pegasus.Infrastructure/Persistence/GlassVinFillWriter.cs", 3, "Names a system fill's recorder."),
        ("Pegasus.Infrastructure/Persistence/InspectionAddressResolutionStore.cs", 2, "Reads a recorded settler back by kind."),
        ("Pegasus.Infrastructure/Persistence/NewCaseAssessmentDefaults.cs", 2, "Names a system fill's recorder."),
        ("Pegasus.Infrastructure/Persistence/OriginalReportPrefillWriter.cs", 1, "Names a system fill's recorder."),
        ("Pegasus.Infrastructure/Persistence/TriageFindingFillWriter.cs", 3, "Names a system fill's recorder."),
        ("Pegasus.Infrastructure/Persistence/VehicleCaptureFillWriter.cs", 3, "Names a system fill's recorder.")
    ];

    private static readonly string[] Projects = ["Pegasus.Core", "Pegasus.Infrastructure"];

    [Fact]
    public void NoToolAssumesTheAutomationActor()
    {
        var offenders = new List<string>();
        var routes = 0;
        foreach (var file in ToolFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (AutomationAssumptions.Any(assumption => line.Contains(assumption, StringComparison.Ordinal)))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{index + 1}: {line.Trim()}");
                }

                // The channel is the route a source arrived by, the connector,
                // whoever submitted it: the one submit tool names it, once.
                if (line.Contains("IntakeSourceChannel.Automation", StringComparison.Ordinal))
                {
                    routes++;
                    if (Path.GetFileName(file) != "IntakeMcpTools.cs")
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{index + 1}: {line.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A tool reads the actor and its labels from the resolved context:" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
        Assert.Equal(1, routes);
    }

    [Fact]
    public void EachPlaceThatNamesTheAutomationActorKindIsListedWithItsDisposition()
    {
        var source = Path.Combine(RepositoryRoot.Find(), "src");
        var pinned = ActorKindAutomation.ToDictionary(row => row.File, row => row.Count, StringComparer.Ordinal);
        Assert.All(ActorKindAutomation, row => Assert.False(string.IsNullOrWhiteSpace(row.Disposition), row.File));

        var grown = Projects
            .SelectMany(project => Directory.EnumerateFiles(
                Path.Combine(source, project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutputOrGenerated(source, path))
            .Select(path => (
                File: Path.GetRelativePath(source, path).Replace(Path.DirectorySeparatorChar, '/'),
                Count: ActorKindAutomationName().Count(File.ReadAllText(path))))
            .Where(found => found.Count > pinned.GetValueOrDefault(found.File))
            .Select(found => $"{found.File}: {found.Count} (listed {pinned.GetValueOrDefault(found.File)})")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            grown.Length == 0,
            "List each new place that names ActorKind.Automation with what it does for a member of staff:"
                + Environment.NewLine + string.Join(Environment.NewLine, grown));

        // A listed file that is gone is a row to take out, so the table stays true.
        Assert.All(pinned.Keys, file => Assert.True(File.Exists(Path.Combine(source, file)), file));
    }

    private static IEnumerable<string> ToolFiles() =>
        Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot.Find(), "src", "Pegasus.Web", "Mcp"), "*McpTools.cs");

    private static bool IsBuildOutputOrGenerated(string source, string path)
    {
        var segments = Path.GetRelativePath(source, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment is "bin" or "obj" or "Migrations");
    }

    [GeneratedRegex(@"ActorKind\.Automation\b", RegexOptions.CultureInvariant)]
    private static partial Regex ActorKindAutomationName();
}
