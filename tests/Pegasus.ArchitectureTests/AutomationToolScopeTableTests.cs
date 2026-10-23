using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using OpenIddict.Abstractions;
using Pegasus.Web.Mcp;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// ADR-0080 decision 3: <see cref="AutomationToolScopes"/> is the one
/// declaration of a tool's scope, read by the call gate and the tools/list
/// filter. Every registered tool has a row, every row is a registered tool,
/// every gate names the tool it guards, and a token lists exactly the tools
/// of the scopes it carries.
/// </summary>
public sealed partial class AutomationToolScopeTableTests
{
    [Fact]
    public void EveryRegisteredToolHasOneRowAndEveryRowIsATool()
    {
        var registered = typeof(AutomationMcp).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(67, registered.Length);
        Assert.Equal(registered, AutomationToolScopes.Tools.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void EachGateNamesTheToolItGuards()
    {
        var gated = 0;
        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot.Find(), "src", "Pegasus.Web", "Mcp"), "*McpTools.cs"))
        {
            string? tool = null;
            foreach (Match match in ToolOrGate().Matches(File.ReadAllText(file)))
            {
                if (match.Groups["tool"].Success)
                {
                    tool = match.Groups["tool"].Value;
                    continue;
                }

                Assert.Equal(
                    $"{Path.GetFileName(file)} {tool}",
                    $"{Path.GetFileName(file)} {match.Groups["gate"].Value}");
                gated++;
            }
        }

        // The nine Triage writes share one gate, which takes the tool's name.
        Assert.Equal(67 - 9, gated);
    }

    [Fact]
    public void ATokenListsExactlyTheToolsOfItsScopes()
    {
        string[] mailTools =
        [
            "pegasus_mail_action",
            "pegasus_mail_correct_classification",
            "pegasus_mail_get",
            "pegasus_mail_link_case",
            "pegasus_mail_link_destinations",
            "pegasus_mail_list"
        ];
        Assert.Equal(mailTools, Listed(AutomationMcp.MailScope));
        var casesAndDocuments = Listed(AutomationMcp.CasesScope, AutomationMcp.DocumentsScope);
        Assert.Equal(23, casesAndDocuments.Length);
        Assert.Contains("pegasus_edit_begin", casesAndDocuments);
        Assert.Contains("pegasus_edit_begin", Listed(AutomationMcp.IntakeScope));
        Assert.Equal(10, Listed(AutomationMcp.AssessmentScope).Length);
        Assert.Equal(67, Listed([.. AutomationMcp.Scopes]).Length);
        Assert.Empty(Listed());
    }

    [Fact]
    public void AnEditLeaseToolTakesTheScopeOfItsRecordKind()
    {
        Assert.Equal(
            AutomationMcp.CasesScope,
            AutomationToolScopes.RequiredScope("pegasus_edit_begin", AutomationMcp.CasesScope));
        Assert.Equal(
            AutomationMcp.IntakeScope,
            AutomationToolScopes.RequiredScope("pegasus_edit_end", AutomationMcp.IntakeScope));
        Assert.Throws<InvalidOperationException>(
            () => AutomationToolScopes.RequiredScope("pegasus_edit_renew", AutomationMcp.MailScope));
        Assert.Throws<InvalidOperationException>(
            () => AutomationToolScopes.RequiredScope("pegasus_edit_begin"));
        Assert.Equal(AutomationMcp.SendScope, AutomationToolScopes.RequiredScope("pegasus_report_send"));
        Assert.Throws<InvalidOperationException>(
            () => AutomationToolScopes.RequiredScope("pegasus_no_such_tool"));
    }

    [Fact]
    public void EachServerHoldsTheToolsOfItsOwnScope()
    {
        // ADR-0080 decision 1: one server per scope, named and pathed by its word.
        string[] words = ["cases", "intake", "documents", "assessment", "mail", "jobs", "send"];
        int[] toolCounts = [11, 25, 12, 10, 6, 4, 2];
        Assert.Equal(words, AutomationMcp.Servers.Select(server => server.Word).ToArray());
        foreach (var server in AutomationMcp.Servers)
        {
            Assert.Equal("pegasus-" + server.Word, server.Name);
            Assert.Equal("/mcp/" + server.Word, server.Path);
            Assert.Equal("automation." + server.Word, Assert.Single(server.Scopes));
            Assert.Equal(Listed([.. server.Scopes]), OnServer(server));
        }

        Assert.Equal(toolCounts, AutomationMcp.Servers.Select(server => OnServer(server).Length).ToArray());
        Assert.All(AutomationToolScopes.Tools, tool => Assert.Contains(
            AutomationMcp.Servers, server => AutomationToolScopes.IsOn(tool, server)));

        // The shared reads are not duplicated; the edit trio is on cases and intake.
        string[] cases = ["cases"];
        string[] casesAndIntake = ["cases", "intake"];
        Assert.Equal(cases, Servers("pegasus_case_get"));
        Assert.Equal(cases, Servers("pegasus_vocabulary_get"));
        Assert.Equal(casesAndIntake, Servers("pegasus_edit_begin"));
    }

    private static string[] OnServer(AutomationMcpServer server) =>
        [.. AutomationToolScopes.Tools
            .Where(tool => AutomationToolScopes.IsOn(tool, server))
            .Order(StringComparer.Ordinal)];

    private static string[] Servers(string tool) =>
        [.. AutomationMcp.Servers
            .Where(server => AutomationToolScopes.IsOn(tool, server))
            .Select(server => server.Word)];

    private static string[] Listed(params string[] scopes)
    {
        var held = AutomationToolScopes.Held(new ClaimsPrincipal(new ClaimsIdentity("test")).SetScopes(scopes));
        return [.. AutomationToolScopes.Tools
            .Where(tool => AutomationToolScopes.IsListed(tool, held))
            .Order(StringComparer.Ordinal)];
    }

    [GeneratedRegex(@"Name = ""(?<tool>pegasus_[a-z_]+)""|RequireAsync\(\s*""(?<gate>pegasus_[a-z_]+)""")]
    private static partial Regex ToolOrGate();
}
