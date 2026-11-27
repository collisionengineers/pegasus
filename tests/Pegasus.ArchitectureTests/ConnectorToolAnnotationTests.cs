using System.Reflection;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Pegasus.Web.Mcp;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// Pegasus Desktop's assistant treats a tool as one that only looks when its
/// MCP annotation says read-only, and puts every other tool on a card the
/// member of staff confirms before the change is made (R1304). A wrong mark
/// is therefore a change with no card. The marks are held here to what can
/// be checked without running a tool: the tools marked read-only are the
/// listed reads and no others, a read takes nothing a write takes, and its
/// body claims no edit lease and derives no operation key.
/// </summary>
public sealed partial class ConnectorToolAnnotationTests
{
    /// <summary>
    /// Every tool that only looks. A tool joins or leaves this list only with
    /// a reading of what it calls: each of these reads, and writes at most
    /// the record that it was read (a download's <c>document_downloaded</c>
    /// row, the call's own Action logs row).
    /// </summary>
    private static readonly string[] ReadOnlyTools =
    [
        "pegasus_ai_job_list",
        "pegasus_assessment_get",
        "pegasus_case_get",
        "pegasus_case_search",
        "pegasus_directory_search",
        "pegasus_document_download",
        "pegasus_document_download_batch",
        "pegasus_estimate_get",
        "pegasus_estimate_list",
        "pegasus_image_intake_get",
        "pegasus_image_intake_list",
        "pegasus_image_preparation_get",
        "pegasus_intake_get",
        "pegasus_intake_queue_list",
        "pegasus_mail_attachment_text",
        "pegasus_mail_get",
        "pegasus_mail_link_destinations",
        "pegasus_mail_list",
        "pegasus_mail_mailbox_search",
        "pegasus_mail_photo_registrations",
        "pegasus_old_system_folder_search",
        "pegasus_photo_link_get",
        "pegasus_photo_link_list",
        "pegasus_report_list",
        "pegasus_report_wording_get",
        "pegasus_session_get",
        "pegasus_triage_get",
        "pegasus_triage_list",
        "pegasus_triage_source_download",
        "pegasus_unidentified_get",
        "pegasus_unidentified_list",
        "pegasus_unidentified_source_download",
        "pegasus_upload_group_get",
        "pegasus_valuation_list",
        "pegasus_vocabulary_get"
    ];

    /// <summary>What only a tool that changes something does in its own body.</summary>
    private static readonly string[] WriteMechanics =
    [
        "leases.",
        "RequireOperationKey(",
        "DeriveOperationGuid(",
        "CallWithOptionalKeyAsync(",
        "MutateAsync("
    ];

    [Fact]
    public void TheToolsMarkedReadOnlyAreTheListedReadsAndNoOthers()
    {
        Assert.Equal(
            ReadOnlyTools,
            Tools().Where(tool => tool.Attribute.ReadOnly)
                .Select(tool => tool.Attribute.Name!)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void AToolMarkedReadOnlyTakesNoOperationKeyVersionOrLeaseAndIsNotDestructive()
    {
        var offenders = Tools()
            .Where(tool => tool.Attribute.ReadOnly)
            .SelectMany(tool => tool.Method.GetParameters()
                .Where(parameter => TakenByAWrite(parameter.Name!))
                .Select(parameter => $"{tool.Attribute.Name} takes {parameter.Name}")
                .Concat(tool.Attribute.Destructive
                    ? new[] { $"{tool.Attribute.Name} is marked destructive" }
                    : Array.Empty<string>()))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "A tool marked read-only takes nothing a write takes:" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));

        // The rule the other way round: no tool that takes one is marked read-only,
        // and the tools that take one exist, so neither check is vacuous.
        var writes = Tools()
            .Where(tool => tool.Method.GetParameters().Any(parameter => TakenByAWrite(parameter.Name!)))
            .ToArray();
        Assert.NotEmpty(writes);
        Assert.All(writes, tool => Assert.False(tool.Attribute.ReadOnly, tool.Attribute.Name));
    }

    [Fact]
    public void AToolMarkedReadOnlyClaimsNoLeaseAndDerivesNoOperationKeyInItsBody()
    {
        var bodies = ToolBodies();
        Assert.Equal(
            Tools().Select(tool => tool.Attribute.Name!).Order(StringComparer.Ordinal).ToArray(),
            bodies.Keys.Order(StringComparer.Ordinal).ToArray());

        var offenders = ReadOnlyTools
            .SelectMany(name => WriteMechanics
                .Where(mechanic => bodies[name].Contains(mechanic, StringComparison.Ordinal))
                .Select(mechanic => $"{name} calls {mechanic}"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "A tool marked read-only does nothing only a write does:" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));

        // The mechanics are what the writes are made of, so the scan is not vacuous.
        Assert.Contains("leases.", bodies["pegasus_document_add"], StringComparison.Ordinal);
        Assert.Contains("RequireOperationKey(", bodies["pegasus_upload_begin"], StringComparison.Ordinal);
    }

    /// <summary>
    /// The inputs only a change carries: the caller's idempotency key, an
    /// edit lease token, and the version the caller observed.
    /// </summary>
    private static bool TakenByAWrite(string parameterName) =>
        parameterName is "operationKey" or "editLeaseToken"
        || parameterName.StartsWith("expected", StringComparison.Ordinal);

    private static IEnumerable<(MethodInfo Method, McpServerToolAttribute Attribute)> Tools() =>
        typeof(AutomationMcp).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(tool => tool.Attribute?.Name is not null)
            .Select(tool => (tool.Method, tool.Attribute!));

    /// <summary>
    /// Each tool's own method as source: from its attribute to the line that
    /// closes the method, so a helper that follows it is not read as part of it.
    /// </summary>
    private static Dictionary<string, string> ToolBodies()
    {
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot.Find(), "src", "Pegasus.Web", "Mcp"), "*McpTools.cs"))
        {
            var source = File.ReadAllText(file).ReplaceLineEndings("\n");
            foreach (Match tool in ToolAttribute().Matches(source))
            {
                var end = source.IndexOf("\n    }\n", tool.Index, StringComparison.Ordinal);
                Assert.True(end > tool.Index, $"{tool.Groups["name"].Value} has no method body in {Path.GetFileName(file)}.");
                bodies.Add(tool.Groups["name"].Value, source[tool.Index..end]);
            }
        }

        return bodies;
    }

    [GeneratedRegex(@"\[McpServerTool\(\s*Name = ""(?<name>pegasus_[a-z_]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ToolAttribute();
}
