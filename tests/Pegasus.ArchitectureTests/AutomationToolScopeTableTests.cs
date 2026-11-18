using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using OpenIddict.Abstractions;
using Pegasus.Web.Mcp;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// ADR-0087 decision 3: <see cref="AutomationToolScopes"/>
/// is the one declaration of a tool's scope, read by the call gate and the
/// tools/list filter. Every registered tool has a row, every row is a registered tool,
/// every gate names the tool it guards, and a token lists exactly the tools
/// of the scopes it carries.
/// </summary>
public sealed partial class AutomationToolScopeTableTests
{
    private static readonly string[] GlassTools = ["pegasus_glass_session_act", "pegasus_glass_session_get"];

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

        // Every tool gates on its own name: at the resolver, through the auditor's
        // CallAsync, or through the Triage writes' MutateAsync, which passes it on.
        Assert.Equal(AutomationToolScopes.Tools.Count(), gated);
    }

    [Fact]
    public void ATokenListsExactlyTheToolsOfItsScopes()
    {
        string[] mailTools =
        [
            "pegasus_mail_action",
            "pegasus_mail_attachment_text",
            "pegasus_mail_correct_classification",
            "pegasus_mail_get",
            "pegasus_mail_link_case",
            "pegasus_mail_link_destinations",
            "pegasus_mail_list",
            "pegasus_mail_mailbox_search",
            "pegasus_mail_open_query",
            "pegasus_mail_photo_registrations",
            "pegasus_old_system_folder_search"
        ];
        Assert.Equal(mailTools, Listed(AutomationMcp.MailScope));
        var casesAndDocuments = Listed(AutomationMcp.CasesScope, AutomationMcp.DocumentsScope);
        Assert.Equal(27, casesAndDocuments.Length);
        Assert.Contains("pegasus_edit_begin", casesAndDocuments);
        var intakeTools = Listed(AutomationMcp.IntakeScope);
        Assert.Contains("pegasus_edit_begin", intakeTools);
        Assert.Equal(31, intakeTools.Length);
        Assert.Contains("pegasus_image_intake_action", intakeTools);
        Assert.Contains("pegasus_upload_group_action", intakeTools);
        Assert.Contains("pegasus_unidentified_action", intakeTools);
        Assert.Equal(10, Listed(AutomationMcp.AssessmentScope).Length);
        // The Automation's own Glass's sessions are their own scope, so a
        // grant can hold casework without acting in Glass's (R1179).
        Assert.Equal(GlassTools, Listed(AutomationMcp.GlassScope));
        Assert.DoesNotContain("pegasus_glass_session_act", Listed(AutomationMcp.AssessmentScope));
        Assert.Equal(AutomationToolScopes.Tools.Order(StringComparer.Ordinal).ToArray(), Listed([.. AutomationMcp.Scopes]));
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
        Assert.Equal(AutomationMcp.SendScope, AutomationToolScopes.RequiredScope("pegasus_photo_link_send"));
        Assert.Equal(AutomationMcp.CasesScope, AutomationToolScopes.RequiredScope("pegasus_photo_link_action"));
        Assert.Equal(AutomationMcp.GlassScope, AutomationToolScopes.RequiredScope("pegasus_glass_session_act"));
        Assert.Equal(AutomationMcp.GlassScope, AutomationToolScopes.RequiredScope("pegasus_glass_session_get"));
        Assert.Throws<InvalidOperationException>(
            () => AutomationToolScopes.RequiredScope("pegasus_no_such_tool"));
    }

    private static string[] Listed(params string[] scopes)
    {
        var held = AutomationToolScopes.Held(new ClaimsPrincipal(new ClaimsIdentity("test")).SetScopes(scopes));
        return [.. AutomationToolScopes.Tools
            .Where(tool => AutomationToolScopes.IsListed(tool, held))
            .Order(StringComparer.Ordinal)];
    }

    [GeneratedRegex(@"Name = ""(?<tool>pegasus_[a-z_]+)""|(?:RequireAsync|CallAsync|CallWithOptionalKeyAsync|MutateAsync)\(\s*""(?<gate>pegasus_[a-z_]+)""")]
    private static partial Regex ToolOrGate();
}
