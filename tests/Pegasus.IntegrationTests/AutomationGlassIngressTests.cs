using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Automation's own Glass's session tools over real HTTP against the one
/// server and LocalDB (design 07 sections 3 and 4; FRD-10): each tool's
/// success, its scope refusal, its validation refusals and its history line.
/// The provider work of start and finish is proved against the scripted
/// provider in the gateway suite; here no request reaches Glass's.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationGlassIngressTests
{
    /// <summary>The Automation's own login on this host (R1177): a made-up username.</summary>
    private const string AutomationLogin = "automation-ingress-login";

    /// <summary>The lining's three options (glasses7 #356, row 0), all ticked.</summary>
    private static readonly string[] LiningOptions = ["12000", "24119", "90109"];

    private static readonly string[] FogLamp = ["12000"];

    /// <summary>The door's optional trim support kit, as its "Mandatory parts:" page lists it.</summary>
    private static readonly string[] TrimSupportKit = ["8099801.ER."];

    private static readonly string[] SessionGetParameters = ["caseId", "positionId", "read", "sessionId"];

    [Fact]
    public async Task TheGlassToolsNeedTheGlassScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithGlass(WithAutomationMcp(factory));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var casework = await RequestTokenAsync(client, AllScopes);

        using (var get = await PostMcpAsync(client, casework, ToolCallPayload(
            1, "pegasus_glass_session_get", new { caseId })))
        {
            await AssertScopeDeniedAsync(get, AutomationMcp.GlassScope);
        }
        using (var act = await PostMcpAsync(client, casework, ToolCallPayload(
            2, "pegasus_glass_session_act", new { caseId, action = "close", operationKey = "mcp:close-denied", expectedSessionVersion = 0, reason = "No scope." })))
        {
            await AssertScopeDeniedAsync(act, AutomationMcp.GlassScope);
        }
        using (var search = await PostMcpAsync(client, casework, ToolCallPayload(
            3, "pegasus_glass_catalogue_search", new { caseId, term = "door" })))
        {
            await AssertScopeDeniedAsync(search, AutomationMcp.GlassScope);
        }
        using (var position = await PostMcpAsync(client, casework, ToolCallPayload(
            4, "pegasus_glass_position_act", new { caseId, action = "remove", operationKey = "mcp:remove-denied", expectedSessionVersion = 0, positionId = "C1.2.C3.4" })))
        {
            await AssertScopeDeniedAsync(position, AutomationMcp.GlassScope);
        }

        Assert.Equal(4, await CountScopeDenialsAsync(factory));
    }

    /// <summary>
    /// session_get on a Case with no Automation session answers none and a
    /// free account; with one, its state and version, and the account held
    /// by the Automation on that Case. Nothing names a member of staff.
    /// </summary>
    [Fact]
    public async Task SessionGetReadsTheAutomationsSessionAndWhoHoldsTheAccount()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithGlass(WithAutomationMcp(factory));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);

        using (var none = await PostMcpAsync(client, token, ToolCallPayload(
            1, "pegasus_glass_session_get", new { caseId })))
        {
            var structured = await ReadStructuredContentAsync(none);
            Assert.True(IsAbsent(structured, "session"));
            Assert.True(structured.GetProperty("account").GetProperty("free").GetBoolean());
        }

        var sessionId = await SeedAutomationSessionAsync(mcpFactory, caseId);
        using (var read = await PostMcpAsync(client, token, ToolCallPayload(
            2, "pegasus_glass_session_get", new { caseId })))
        {
            var structured = await ReadStructuredContentAsync(read);
            var session = structured.GetProperty("session");
            Assert.Equal(sessionId, session.GetProperty("sessionId").GetGuid());
            Assert.Equal("Active", session.GetProperty("state").GetString());
            Assert.Equal(0, session.GetProperty("version").GetInt64());
            Assert.False(session.GetProperty("working").GetBoolean());
            var account = structured.GetProperty("account");
            Assert.False(account.GetProperty("free").GetBoolean());
            Assert.Equal("automation", account.GetProperty("heldBy").GetString());
            Assert.Equal(caseId, account.GetProperty("caseId").GetGuid());
            Assert.DoesNotContain(AutomationLogin, structured.ToString(), StringComparison.Ordinal);
        }

        Assert.Equal(2, await CountHistoryAsync(factory, "pegasus_glass_session_get"));
    }

    /// <summary>
    /// close ends the Automation's session with a reason: nothing is sent to
    /// Glass's, no Case lease is taken, and the account is free. A stale
    /// session version, a missing reason and a session already closed are
    /// refused with their sentences.
    /// </summary>
    [Fact]
    public async Task CloseEndsTheSessionWithAReasonAndRefusesWhatItMust()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithGlass(WithAutomationMcp(factory));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);
        await SeedAutomationSessionAsync(mcpFactory, caseId);

        using (var stale = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_session_act", new
        {
            caseId, action = "close", operationKey = "mcp:close-stale", expectedSessionVersion = 7, reason = "Stopped.",
        })))
        {
            Assert.Contains("changed before it could be closed", await RefusalAsync(stale), StringComparison.Ordinal);
        }
        using (var unreasoned = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_session_act", new
        {
            caseId, action = "close", operationKey = "mcp:close-unreasoned", expectedSessionVersion = 0, reason = " ",
        })))
        {
            Assert.Contains("a reason", await RefusalAsync(unreasoned), StringComparison.Ordinal);
        }

        using (var closed = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_glass_session_act", new
        {
            caseId, action = "close", operationKey = "mcp:close-1", expectedSessionVersion = 0, reason = "The work is done elsewhere.",
        })))
        {
            var structured = await ReadStructuredContentAsync(closed);
            Assert.Equal("Cancelled", structured.GetProperty("session").GetProperty("state").GetString());
            Assert.Equal("mcp:close-1", structured.GetProperty("operationKey").GetString());
        }

        using (var again = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_glass_session_act", new
        {
            caseId, action = "close", operationKey = "mcp:close-2", expectedSessionVersion = 1, reason = "Again.",
        })))
        {
            Assert.Contains(AutomationToolText.GlassNoSession, await RefusalAsync(again), StringComparison.Ordinal);
        }

        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_mcp'
              AND EventKind = N'pegasus_glass_session_act'
              AND ActorKind = N'Automation'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'mcp:close-1'
            """));
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'glass-repair-estimate-session'
              AND EventKind = N'glass_session_closed'
              AND ActorKind = N'Automation'
              AND Reason = N'The work is done elsewhere.'
            """));
    }

    /// <summary>
    /// The act's own refusals, before anything is asked of Glass's: an action
    /// it does not have, a start without the Case version, and a finish with
    /// no open session on the Case.
    /// </summary>
    [Fact]
    public async Task TheActRefusesAnUnknownActionAMissingVersionAndAFinishWithNoSession()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithGlass(WithAutomationMcp(factory));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);

        using (var unknown = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_session_act", new
        {
            caseId, action = "build", operationKey = "mcp:build-1",
        })))
        {
            Assert.Contains("The action must be one of: start, finish, close.", await RefusalAsync(unknown), StringComparison.Ordinal);
        }
        using (var versionless = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_session_act", new
        {
            caseId, action = "start", operationKey = "mcp:start-1",
        })))
        {
            Assert.Contains("expectedVersion", await RefusalAsync(versionless), StringComparison.Ordinal);
        }
        using (var finish = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_glass_session_act", new
        {
            caseId, action = "finish", operationKey = "mcp:finish-1", expectedVersion = 0, expectedSessionVersion = 0,
        })))
        {
            Assert.Contains(AutomationToolText.GlassNoSession, await RefusalAsync(finish), StringComparison.Ordinal);
        }
        using (var keyless = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_glass_session_act", new
        {
            caseId, action = "close", operationKey = "close-1", expectedSessionVersion = 0, reason = "No key.",
        })))
        {
            Assert.Contains("mcp:", await RefusalAsync(keyless), StringComparison.Ordinal);
        }
    }

    /// <summary>A host that does not reach Glass's refuses both tools with the page's own sentence.</summary>
    [Fact]
    public async Task AHostWithoutGlassRefusesTheTools()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);

        using var refused = await PostMcpAsync(client, token, ToolCallPayload(
            1, "pegasus_glass_session_get", new { caseId }));

        Assert.Contains(Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession.Unavailable, await RefusalAsync(refused), StringComparison.Ordinal);
    }

    /// <summary>
    /// The catalogue search reads the open estimate's lists and pages its
    /// rows by a cursor bound to the session and the term; the position act
    /// adds the row it returned, posted as Glass's dialog serves it, and
    /// answers the act with Glass's own figures and the session's new
    /// version. Each call is a history line. The scripted editor stands in
    /// for Glass's on the host's own named client.
    /// </summary>
    [Fact]
    public async Task SearchPagesTheRowsAndPositionActAddsOne()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var glass = new ScriptedGlass();
        GlassProviderFixture.ScriptEditor(glass);
        using var mcpFactory = WithEditor(WithGlass(WithAutomationMcp(factory)), glass);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);
        await SeedAutomationSessionAsync(mcpFactory, caseId, withEditor: true);

        string? next;
        using (var first = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_catalogue_search", new
        {
            caseId, term = "door shell", kind = "part", limit = 1,
        })))
        {
            var structured = await ReadStructuredContentAsync(first);
            var row = Assert.Single(structured.GetProperty("items").EnumerateArray());
            Assert.Equal("771", row.GetProperty("row").GetString());
            Assert.Equal("L", row.GetProperty("side").GetString());
            Assert.True(row.GetProperty("paintable").GetBoolean());
            next = structured.GetProperty("nextCursor").GetString();
            Assert.False(string.IsNullOrEmpty(next));
        }
        using (var second = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_catalogue_search", new
        {
            caseId, term = "door shell", kind = "part", limit = 1, cursor = next,
        })))
        {
            var structured = await ReadStructuredContentAsync(second);
            Assert.Equal("790", Assert.Single(structured.GetProperty("items").EnumerateArray()).GetProperty("row").GetString());
            Assert.True(IsAbsent(structured, "nextCursor"));
        }

        long version;
        using (var special = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_glass_catalogue_search", new
        {
            caseId, term = "appraisal", kind = "special_operation",
        })))
        {
            var structured = await ReadStructuredContentAsync(special);
            var row = Assert.Single(structured.GetProperty("items").EnumerateArray());
            Assert.Equal("AC0000073", row.GetProperty("row").GetString());
            Assert.Equal("special_operation", row.GetProperty("kind").GetString());
            version = structured.GetProperty("session").GetProperty("version").GetInt64();
            Assert.Equal(3, version);
        }

        using (var added = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:add-1", expectedSessionVersion = version,
            kind = "special_operation", row = "AC0000073",
        })))
        {
            var structured = await ReadStructuredContentAsync(added);
            var act = structured.GetProperty("act");
            Assert.Equal("added", act.GetProperty("outcome").GetString());
            Assert.Equal(176.96m, act.GetProperty("price").GetDecimal());
            Assert.Contains("A350880.1.A73.A73", act.GetProperty("positionIds").EnumerateArray().Select(id => id.GetString()));
            Assert.Equal(version + 3, structured.GetProperty("session").GetProperty("version").GetInt64());
            Assert.Equal("mcp:add-1", structured.GetProperty("operationKey").GetString());
        }
        Assert.Equal(1, glass.Count("POST /ere/aco/compproc.php"));

        using (var read = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_glass_session_get", new { caseId })))
        {
            var structured = await ReadStructuredContentAsync(read);
            var act = Assert.Single(structured.GetProperty("acts").EnumerateArray());
            Assert.Equal("mcp:add-1", act.GetProperty("operationKey").GetString());
            Assert.Equal("added", act.GetProperty("outcome").GetString());
        }

        Assert.Equal(3, await CountHistoryAsync(factory, "pegasus_glass_catalogue_search"));
        Assert.Equal(1, await CountHistoryAsync(factory, "pegasus_glass_position_act"));
    }

    /// <summary>
    /// A part's equipment question through the tools (R1206): an answer on a
    /// remove is refused before anything is asked of Glass's; the search
    /// answers a row with its question, as Glass's names the options, and
    /// every answer that leads to a part; an add with no answer is refused
    /// with the question; an add with inputs.equipment stores the answer and
    /// adds the part as served, and its act keeps the answer, the rows and
    /// the values it changed.
    /// </summary>
    [Fact]
    public async Task APartsEquipmentQuestionIsAnsweredThroughTheTools()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var glass = new ScriptedGlass();
        GlassProviderFixture.ScriptEditor(glass);
        using var mcpFactory = WithEditor(WithGlass(WithAutomationMcp(factory)), glass);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);
        await SeedAutomationSessionAsync(mcpFactory, caseId, withEditor: true);

        using (var remove = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_position_act", new
        {
            caseId, action = "remove", operationKey = "mcp:remove-1", expectedSessionVersion = 0, positionId = "C1.2.C3.4",
            inputs = new { equipment = FogLamp },
        })))
        {
            Assert.Contains(AutomationToolText.GlassInputsEquipmentRefused, await RefusalAsync(remove), StringComparison.Ordinal);
        }
        Assert.Empty(glass.Requests);

        long version;
        using (var found = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_catalogue_search", new { caseId, row = "0" })))
        {
            var structured = await ReadStructuredContentAsync(found);
            var equipment = Assert.Single(structured.GetProperty("items").EnumerateArray()).GetProperty("equipment");
            var options = equipment.GetProperty("options").EnumerateArray().ToList();
            Assert.Equal(LiningOptions, options.Select(option => option.GetProperty("code").GetString()));
            Assert.Equal("Fog Lamp", options[0].GetProperty("name").GetString());
            Assert.Equal("144", options[1].GetProperty("group").GetString());
            Assert.Equal(8, equipment.GetProperty("answers").GetArrayLength());
            version = structured.GetProperty("session").GetProperty("version").GetInt64();
        }

        using (var unanswered = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:lining-1", expectedSessionVersion = version,
            kind = "part", row = "0", operation = "part",
        })))
        {
            var structured = await ReadStructuredContentAsync(unanswered);
            var act = structured.GetProperty("act");
            Assert.Equal("refused", act.GetProperty("outcome").GetString());
            Assert.Equal(GlassEstimateBuildPolicy.EquipmentCode, act.GetProperty("code").GetString());
            Assert.Equal(3, act.GetProperty("equipment").GetProperty("options").GetArrayLength());
            version = structured.GetProperty("session").GetProperty("version").GetInt64();
        }
        Assert.DoesNotContain(glass.Requests, request => request.Query.Contains("globcrit=", StringComparison.Ordinal));

        using (var added = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:lining-2", expectedSessionVersion = version,
            kind = "part", row = "0", operation = "part", inputs = new { equipment = LiningOptions },
        })))
        {
            var act = (await ReadStructuredContentAsync(added)).GetProperty("act");
            Assert.Equal("added", act.GetProperty("outcome").GetString());
            Assert.Equal("0|5", act.GetProperty("rowPair").GetString());
            Assert.True(act.GetProperty("equipmentStored").GetBoolean());
            Assert.Equal(3, act.GetProperty("inputs").GetProperty("equipment").GetArrayLength());
            Assert.Equal(3, act.GetProperty("equipmentChanged").GetArrayLength());
            Assert.Equal(379.00m, act.GetProperty("price").GetDecimal());
        }
        Assert.Single(glass.Requests, request => request.Query.Contains("globcrit=", StringComparison.Ordinal));
        Assert.Equal(2, glass.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// preview and the dialog's own inputs through the tools (R1206): a
    /// preview answers the door's dialog as a form with Glass's own texts,
    /// what an add with its inputs would send and the question it would meet,
    /// and posts nothing; the add with the question answered, another button
    /// and an optional part ticked is posted, and its act keeps every input
    /// beside Glass's figures. Inputs the act does not take are refused
    /// before anything is asked of Glass's.
    /// </summary>
    [Fact]
    public async Task APreviewAndTheDialogsInputsGoThroughTheTools()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var glass = new ScriptedGlass();
        GlassProviderFixture.ScriptEditor(glass);
        using var mcpFactory = WithEditor(WithGlass(WithAutomationMcp(factory)), glass);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);
        await SeedAutomationSessionAsync(mcpFactory, caseId, withEditor: true);

        long version;
        using (var preview = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_position_act", new
        {
            caseId, action = "preview", operationKey = "mcp:preview-1",
            kind = "part", row = "771", operation = "part", inputs = new { operation = "AD" },
        })))
        {
            var structured = await ReadStructuredContentAsync(preview);
            Assert.True(IsAbsent(structured, "act"));
            var dialog = structured.GetProperty("dialog");
            Assert.Equal("part_paintable", dialog.GetProperty("kind").GetString());
            var operations = dialog.GetProperty("operations").EnumerateArray().ToList();
            var sealing = operations.Single(item => item.GetProperty("code").GetString() == "AD");
            Assert.Equal("S (Sealing)", sealing.GetProperty("text").GetString());
            Assert.True(sealing.GetProperty("changeable").GetBoolean());
            var dismantle = operations.Single(item => item.GetProperty("code").GetString() == "Z");
            Assert.False(dismantle.GetProperty("changeable").GetBoolean());
            Assert.Equal(
                GlassAutomationPositionPolicy.OperationNotReadBackCode, dismantle.GetProperty("refusedCode").GetString());
            var sends = dialog.GetProperty("sends");
            Assert.Equal(GlassFailure.EditorRefused, sends.GetProperty("code").GetString());
            var question = sends.GetProperty("questions").EnumerateArray()
                .Single(item => item.GetProperty("kind").GetString() == "no_figures");
            Assert.False(question.GetProperty("answered").GetBoolean());
            version = structured.GetProperty("session").GetProperty("version").GetInt64();
        }
        Assert.Equal(0, glass.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(1, glass.Count("GET /ere/aco/comp.php"));

        using (var added = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:add-z", expectedSessionVersion = version,
            kind = "part", row = "771", operation = "part",
            inputs = new { operation = "AD", button = "part_and_paint", confirmNoFigures = true, optionalParts = TrimSupportKit },
        })))
        {
            var structured = await ReadStructuredContentAsync(added);
            version = structured.GetProperty("session").GetProperty("version").GetInt64();
            var act = structured.GetProperty("act");
            Assert.Equal("added", act.GetProperty("outcome").GetString());
            var inputs = act.GetProperty("inputs");
            Assert.Equal("AD", inputs.GetProperty("operation").GetString());
            Assert.Equal("part_and_paint", inputs.GetProperty("button").GetString());
            Assert.True(inputs.GetProperty("confirmNoFigures").GetBoolean());
            var kit = act.GetProperty("mandatoryParts").EnumerateArray()
                .Single(item => item.GetProperty("code").GetString() == "8099801.ER.");
            Assert.True(kit.GetProperty("ticked").GetBoolean());
            Assert.True(kit.GetProperty("optional").GetBoolean());
        }
        Assert.Equal(2, glass.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(
            GlassEditorFixture.Text("door_left_ad_part_and_paint.post.txt"),
            glass.Requests.First(request => request.Method == "POST").Body);

        using (var renew = await PostMcpAsync(client, token, ToolCallPayload(8, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:add-renew", expectedSessionVersion = version,
            kind = "part", row = "771", operation = "part", inputs = new { operation = "AN" },
        })))
        {
            var act = (await ReadStructuredContentAsync(renew)).GetProperty("act");
            Assert.Equal("refused", act.GetProperty("outcome").GetString());
            Assert.Equal(GlassAutomationPositionPolicy.OperationNotReadBackCode, act.GetProperty("code").GetString());
            Assert.Equal("AN", act.GetProperty("inputs").GetProperty("operation").GetString());
        }
        Assert.Equal(2, glass.Count("POST /ere/aco/compproc.php"));

        var requests = glass.Requests.Count;
        using (var button = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:add-x", expectedSessionVersion = 0,
            kind = "part", row = "771", operation = "part", inputs = new { button = "glitter" },
        })))
        {
            Assert.Contains(AutomationToolText.GlassInputsButtonRefused, await RefusalAsync(button), StringComparison.Ordinal);
        }
        using (var side = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_glass_position_act", new
        {
            caseId, action = "preview", operationKey = "mcp:preview-x",
            kind = "part", row = "771", operation = "part", inputs = new { side = "L" },
        })))
        {
            Assert.Contains(AutomationToolText.GlassInputsSideRefused, await RefusalAsync(side), StringComparison.Ordinal);
        }
        using (var chosen = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:add-y", expectedSessionVersion = 0,
            kind = "part", row = "771", operation = "repair", inputs = new { operation = "AE" },
        })))
        {
            Assert.Contains(AutomationToolText.GlassInputsOperationRefused, await RefusalAsync(chosen), StringComparison.Ordinal);
        }
        using (var special = await PostMcpAsync(client, token, ToolCallPayload(6, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:add-w", expectedSessionVersion = 0,
            kind = "special_operation", row = "AC0000073", inputs = new { confirmNoFigures = true },
        })))
        {
            Assert.Contains(AutomationToolText.GlassInputsConfirmNoFiguresRefused, await RefusalAsync(special), StringComparison.Ordinal);
        }
        using (var position = await PostMcpAsync(client, token, ToolCallPayload(7, "pegasus_glass_position_act", new
        {
            caseId, action = "preview", operationKey = "mcp:preview-y", positionId = "C1.2.C3.4", kind = "part",
        })))
        {
            Assert.Contains(AutomationToolText.GlassPreviewPositionAlone, await RefusalAsync(position), StringComparison.Ordinal);
        }
        Assert.Equal(requests, glass.Requests.Count);
    }

    /// <summary>
    /// session_get's read in the Automation's own session (R1206), one
    /// request to Glass's each and nothing posted: the positions with
    /// Glass's running totals, one position's figures with its paint, and
    /// Glass's calculation. A position Glass's does not answer for answers
    /// its code and nothing read, and leaves the session open as it was.
    /// </summary>
    [Fact]
    public async Task SessionGetReadsTotalsAPositionAndTheCalculation()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var glass = new ScriptedGlass();
        GlassProviderFixture.ScriptEditor(glass);
        using var mcpFactory = WithEditor(WithGlass(WithAutomationMcp(factory)), glass);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);
        await SeedAutomationSessionAsync(mcpFactory, caseId, withEditor: true);

        using (var positions = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_session_get", new
        {
            caseId, read = "positions",
        })))
        {
            var structured = await ReadStructuredContentAsync(positions);
            var estimate = structured.GetProperty("estimate");
            Assert.Empty(estimate.GetProperty("positionIds").EnumerateArray());
            var due = Assert.Single(estimate.GetProperty("totals").EnumerateArray());
            Assert.Equal("Amount due (£)", due.GetProperty("label").GetString());
            Assert.Equal(0m, due.GetProperty("amount").GetDecimal());
            Assert.True(IsAbsent(structured, "readCode"));
            Assert.Equal(1, structured.GetProperty("session").GetProperty("version").GetInt64());
        }
        using (var position = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_session_get", new
        {
            caseId, read = "position", positionId = "C350898.20.C1060000.2464",
        })))
        {
            var structured = await ReadStructuredContentAsync(position);
            var figures = structured.GetProperty("position");
            Assert.Equal("Bonnet", figures.GetProperty("description").GetString());
            Assert.Equal("ER", figures.GetProperty("operation").GetString());
            Assert.Equal(412.12m, figures.GetProperty("price").GetDecimal());
            Assert.Equal(0.80m, figures.GetProperty("hours").GetDecimal());
            Assert.Equal("41007290942", figures.GetProperty("partNumber").GetString());
            Assert.True(IsAbsent(figures, "side"));
            Assert.Equal(697.10m, figures.GetProperty("paint").GetProperty("price").GetDecimal());
            Assert.Equal(2.60m, figures.GetProperty("paint").GetProperty("labourTime").GetDecimal());
            Assert.True(IsAbsent(structured, "estimate"));
        }
        using (var calculation = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_glass_session_get", new
        {
            caseId, read = "calculation",
        })))
        {
            var summary = (await ReadStructuredContentAsync(calculation)).GetProperty("calculation");
            Assert.Equal(4, summary.GetProperty("trades").GetArrayLength());
            var body = summary.GetProperty("trades")[0];
            Assert.Equal("Body", body.GetProperty("trade").GetString());
            Assert.Equal(1952.00m, body.GetProperty("labourCosts").GetDecimal());
            Assert.Equal(7336.24m, summary.GetProperty("totalMaterial").GetDecimal());
            Assert.Equal(11008.24m, summary.GetProperty("repairCostsExclVat").GetDecimal());
            Assert.Equal("VAT (20.00 %)", summary.GetProperty("vatLabel").GetString());
            Assert.Equal(13209.89m, summary.GetProperty("repairCostsInclVat").GetDecimal());
        }
        using (var unknown = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_glass_session_get", new
        {
            caseId, read = "position", positionId = "C1.2.C3.4",
        })))
        {
            var structured = await ReadStructuredContentAsync(unknown);
            Assert.Equal(GlassFailure.EditorRequest, structured.GetProperty("readCode").GetString());
            Assert.True(IsAbsent(structured, "position"));
            var session = structured.GetProperty("session");
            Assert.Equal("Active", session.GetProperty("state").GetString());
            Assert.True(IsAbsent(session, "failureCode"));
        }

        Assert.Equal(4, glass.Requests.Count);
        Assert.Equal(1, glass.Count("GET /ere/aco/selected.php"));
        Assert.Equal(2, glass.Count("GET /ere/aco/mcode.php"));
        Assert.Equal(1, glass.Count("GET /ere/aco/report.php"));
        Assert.Contains("&cml=C&calcid=0&dummy=", glass.Requests.Single(request => request.Path.EndsWith("/report.php", StringComparison.Ordinal)).Query, StringComparison.Ordinal);
        Assert.Equal(4, await CountHistoryAsync(factory, "pegasus_glass_session_get"));
    }

    /// <summary>
    /// The read's own refusals, before anything is asked of Glass's: a kind
    /// it does not have, a position read with no position id or one that is
    /// not as Glass's lists them, and a position id with another read. The
    /// tool takes read and positionId, and readEstimate no longer.
    /// </summary>
    [Fact]
    public async Task SessionGetRefusesAReadItDoesNotHave()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var glass = new ScriptedGlass();
        GlassProviderFixture.ScriptEditor(glass);
        using var mcpFactory = WithEditor(WithGlass(WithAutomationMcp(factory)), glass);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);
        await SeedAutomationSessionAsync(mcpFactory, caseId, withEditor: true);

        using (var unknown = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_session_get", new { caseId, read = "everything" })))
        {
            Assert.Contains(AutomationToolText.GlassReadRefused, await RefusalAsync(unknown), StringComparison.Ordinal);
        }
        using (var unnamed = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_session_get", new { caseId, read = "position" })))
        {
            Assert.Contains(AutomationToolText.GlassReadPositionIdRefused, await RefusalAsync(unnamed), StringComparison.Ordinal);
        }
        using (var injected = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_glass_session_get", new
        {
            caseId, read = "position", positionId = "C1.2.C3.4&cml=C",
        })))
        {
            Assert.Contains(AutomationToolText.GlassReadPositionIdRefused, await RefusalAsync(injected), StringComparison.Ordinal);
        }
        using (var stray = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_glass_session_get", new
        {
            caseId, read = "positions", positionId = "C1.2.C3.4",
        })))
        {
            Assert.Contains(AutomationToolText.GlassReadPositionIdNotForRead, await RefusalAsync(stray), StringComparison.Ordinal);
        }
        Assert.Empty(glass.Requests);

        using var tools = await PostMcpAsync(client, token, ToolsListPayload(5));
        using var document = await ReadJsonRpcAsync(tools);
        var get = document.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "pegasus_glass_session_get");
        Assert.Equal(
            SessionGetParameters,
            get.GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The two tools' own refusals, before anything is asked of Glass's: a
    /// blank term, a kind or an action they do not have, a part without its
    /// operation, a Case with no Automation session; then, on an open
    /// session, a foreign cursor and a stale session version.
    /// </summary>
    [Fact]
    public async Task SearchAndPositionActRefuseWhatTheyMust()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var glass = new ScriptedGlass();
        GlassProviderFixture.ScriptEditor(glass);
        using var mcpFactory = WithEditor(WithGlass(WithAutomationMcp(factory)), glass);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);

        using (var blank = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_catalogue_search", new { caseId, term = " " })))
        {
            Assert.Contains(AutomationToolText.GlassSearchTermRefused, await RefusalAsync(blank), StringComparison.Ordinal);
        }
        using (var kind = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_catalogue_search", new { caseId, term = "door", kind = "wheel" })))
        {
            Assert.Contains(AutomationToolText.GlassSearchKindRefused, await RefusalAsync(kind), StringComparison.Ordinal);
        }
        using (var none = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_glass_catalogue_search", new { caseId, term = "door" })))
        {
            Assert.Contains(AutomationToolText.GlassNoSession, await RefusalAsync(none), StringComparison.Ordinal);
        }
        using (var move = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_glass_position_act", new
        {
            caseId, action = "move", operationKey = "mcp:move-1", expectedSessionVersion = 0,
        })))
        {
            Assert.Contains(AutomationToolText.GlassPositionActionRefused, await RefusalAsync(move), StringComparison.Ordinal);
        }
        using (var unoperated = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:add-1", expectedSessionVersion = 0, kind = "part", row = "771",
        })))
        {
            Assert.Contains(AutomationToolText.GlassAddOperationMissing, await RefusalAsync(unoperated), StringComparison.Ordinal);
        }
        using (var sessionless = await PostMcpAsync(client, token, ToolCallPayload(6, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:add-2", expectedSessionVersion = 0,
            kind = "special_operation", row = "AC0000073",
        })))
        {
            Assert.Contains(AutomationToolText.GlassNoSession, await RefusalAsync(sessionless), StringComparison.Ordinal);
        }

        await SeedAutomationSessionAsync(mcpFactory, caseId, withEditor: true);
        using (var foreign = await PostMcpAsync(client, token, ToolCallPayload(7, "pegasus_glass_catalogue_search", new
        {
            caseId, term = "door", cursor = "not-a-cursor",
        })))
        {
            Assert.Contains("The cursor is invalid", await RefusalAsync(foreign), StringComparison.Ordinal);
        }
        using (var stale = await PostMcpAsync(client, token, ToolCallPayload(8, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:add-3", expectedSessionVersion = 7,
            kind = "special_operation", row = "AC0000073",
        })))
        {
            Assert.Contains("is at version 0 and not 7", await RefusalAsync(stale), StringComparison.Ordinal);
        }

        Assert.Empty(glass.Requests);
    }

    /// <summary>
    /// On an Engineer's sessions the tools refuse with the glass.help.* codes
    /// before anything is asked of Glass's (design 08 section 5): help off,
    /// several sessions (session_get lists them and a named one is chosen),
    /// no window connected, a session no longer Active; and session_act will
    /// not finish or close an Engineer's session.
    /// </summary>
    [Fact]
    public async Task OnAnEngineersSessionTheToolsRefuseWithTheHelpCodes()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var glass = new ScriptedGlass();
        GlassProviderFixture.ScriptEditor(glass);
        using var mcpFactory = WithEditor(WithGlass(WithAutomationMcp(factory)), glass);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);

        var first = await SeedEngineerSessionAsync(mcpFactory, caseId, "engineer-one", helpOn: false);
        using (var off = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_catalogue_search", new { caseId, term = "door" })))
        {
            Assert.Contains(GlassSessionHelpPolicy.OffCode, await RefusalAsync(off), StringComparison.Ordinal);
        }
        using (var read = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_session_get", new { caseId })))
        {
            var structured = await ReadStructuredContentAsync(read);
            Assert.True(IsAbsent(structured, "session"));
            Assert.Equal(GlassSessionHelpPolicy.OffCode, structured.GetProperty("code").GetString());
        }

        await TurnHelpOnAsync(mcpFactory, first);
        using (var unconnected = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:help-add-0", expectedSessionVersion = 0,
            kind = "special_operation", row = "AC0000073",
        })))
        {
            Assert.Contains(GlassSessionHelpPolicy.NoWindowCode, await RefusalAsync(unconnected), StringComparison.Ordinal);
        }
        using (var read = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_glass_session_get", new { caseId })))
        {
            var session = (await ReadStructuredContentAsync(read)).GetProperty("session");
            Assert.Equal(first, session.GetProperty("sessionId").GetGuid());
            Assert.Equal("staff", session.GetProperty("owner").GetString());
            Assert.True(session.GetProperty("helpOn").GetBoolean());
            Assert.False(session.GetProperty("windowConnected").GetBoolean());
        }
        using (var finish = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_glass_session_act", new
        {
            caseId, action = "finish", operationKey = "mcp:finish-engineer", expectedVersion = 0, expectedSessionVersion = 0,
        })))
        {
            Assert.Contains(GlassSessionHelpPolicy.NotAutomationSessionCode, await RefusalAsync(finish), StringComparison.Ordinal);
        }
        using (var close = await PostMcpAsync(client, token, ToolCallPayload(6, "pegasus_glass_session_act", new
        {
            caseId, action = "close", operationKey = "mcp:close-engineer", expectedSessionVersion = 0, reason = "Not mine.",
        })))
        {
            Assert.Contains(GlassSessionHelpPolicy.NotAutomationSessionCode, await RefusalAsync(close), StringComparison.Ordinal);
        }

        var second = await SeedEngineerSessionAsync(mcpFactory, caseId, "engineer-two", helpOn: true);
        using (var several = await PostMcpAsync(client, token, ToolCallPayload(7, "pegasus_glass_catalogue_search", new { caseId, term = "door" })))
        {
            Assert.Contains(GlassSessionHelpPolicy.SeveralCode, await RefusalAsync(several), StringComparison.Ordinal);
        }
        using (var read = await PostMcpAsync(client, token, ToolCallPayload(8, "pegasus_glass_session_get", new { caseId })))
        {
            var structured = await ReadStructuredContentAsync(read);
            Assert.Equal(GlassSessionHelpPolicy.SeveralCode, structured.GetProperty("code").GetString());
            Assert.Equal(2, structured.GetProperty("sessions").GetArrayLength());
        }
        using (var named = await PostMcpAsync(client, token, ToolCallPayload(9, "pegasus_glass_catalogue_search", new
        {
            caseId, term = "door", sessionId = second,
        })))
        {
            Assert.Contains(GlassSessionHelpPolicy.NoWindowCode, await RefusalAsync(named), StringComparison.Ordinal);
        }

        await MoveAsync(mcpFactory, second, GlassRepairEstimateSessionState.Importing);
        using (var ended = await PostMcpAsync(client, token, ToolCallPayload(10, "pegasus_glass_catalogue_search", new
        {
            caseId, term = "door", sessionId = second,
        })))
        {
            Assert.Contains(GlassSessionHelpPolicy.NotActiveCode, await RefusalAsync(ended), StringComparison.Ordinal);
        }

        Assert.Empty(glass.Requests);
    }

    /// <summary>
    /// With help on and the window's key held, position_act adds in the
    /// Engineer's session through help alone: posted as Glass's dialog
    /// serves it, answered with the positions the Engineer added meanwhile
    /// and whether the window shows it, recorded as the Automation's history
    /// row, and the same key again answers that row and sends nothing. The
    /// session's version does not move and no Case lease is taken.
    /// </summary>
    [Fact]
    public async Task PositionActAddsInAnEngineersSessionWithHelpOnAndAKeyHeld()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var glass = new ScriptedGlass();
        GlassProviderFixture.ScriptEditor(glass);
        using var mcpFactory = WithEditor(WithGlass(WithAutomationMcp(factory)), glass);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);
        var sessionId = await SeedEngineerSessionAsync(mcpFactory, caseId, "engineer-one", helpOn: true);
        var keys = mcpFactory.Services.GetRequiredService<GlassHelpKeys>();
        var options = mcpFactory.Services.GetRequiredService<GlassRepairEstimateOptions>();
        keys.Hold(sessionId, keys.Read(GlassHelpFrameTests.FrameAddress(
            GlassHelpFrameTests.Url0(options.CallbackFor(GlassHelpFrameTests.Token).AbsoluteUri)))
            ?? throw new InvalidOperationException("The frame was not read."));
        var database = await factory.DatabaseAsync;
        const string HeldLeases = "SELECT COUNT(*) FROM CaseWorkflows WHERE EditLeaseHolder IS NOT NULL";
        var leasesBefore = await database.ScalarAsync<int>(HeldLeases);

        using (var added = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:help-add-1", expectedSessionVersion = 0,
            kind = "special_operation", row = "AC0000073",
        })))
        {
            var structured = await ReadStructuredContentAsync(added);
            var act = structured.GetProperty("act");
            Assert.Equal("added", act.GetProperty("outcome").GetString());
            Assert.Equal("A350880.1.A73.A73", Assert.Single(act.GetProperty("positionIds").EnumerateArray()).GetString());
            Assert.Contains(
                "C350926.27.C2027021.4646",
                structured.GetProperty("alsoNew").EnumerateArray().Select(id => id.GetString()));
            Assert.False(structured.GetProperty("shownInWindow").GetBoolean());
            var session = structured.GetProperty("session");
            Assert.Equal("staff", session.GetProperty("owner").GetString());
            Assert.Equal(0, session.GetProperty("version").GetInt64());
        }
        Assert.Equal(1, glass.Count("POST /ere/aco/compproc.php"));

        using (var replayed = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_position_act", new
        {
            caseId, action = "add", operationKey = "mcp:help-add-1", expectedSessionVersion = 0,
            kind = "special_operation", row = "AC0000073",
        })))
        {
            var structured = await ReadStructuredContentAsync(replayed);
            Assert.Equal("added", structured.GetProperty("act").GetProperty("outcome").GetString());
        }
        Assert.Equal(1, glass.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(0, glass.Count("GET /ere/aco/close.php"));

        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'glass-repair-estimate-session'
              AND EventKind = N'glass_session_help_act'
              AND ActorKind = N'Automation'
              AND CorrelationId = N'mcp:help-add-1'
            """));
        // No Case lease was taken: the Engineer keeps the edit.
        Assert.Equal(leasesBefore, await database.ScalarAsync<int>(HeldLeases));
    }

    /// <summary>
    /// In an Engineer's session with help on and the window's key held, the
    /// read is asked through help alone (R1206): the calculation is answered,
    /// a position Glass's does not answer for answers its code, nothing is
    /// posted, the session's version does not move and no Case lease is
    /// taken.
    /// </summary>
    [Fact]
    public async Task SessionGetReadsInAnEngineersSessionWithHelpOnAndAKeyHeld()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var glass = new ScriptedGlass();
        GlassProviderFixture.ScriptEditor(glass);
        using var mcpFactory = WithEditor(WithGlass(WithAutomationMcp(factory)), glass);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.GlassScope);
        var sessionId = await SeedEngineerSessionAsync(mcpFactory, caseId, "engineer-one", helpOn: true);
        var keys = mcpFactory.Services.GetRequiredService<GlassHelpKeys>();
        var options = mcpFactory.Services.GetRequiredService<GlassRepairEstimateOptions>();
        keys.Hold(sessionId, keys.Read(GlassHelpFrameTests.FrameAddress(
            GlassHelpFrameTests.Url0(options.CallbackFor(GlassHelpFrameTests.Token).AbsoluteUri)))
            ?? throw new InvalidOperationException("The frame was not read."));
        var database = await factory.DatabaseAsync;
        const string HeldLeases = "SELECT COUNT(*) FROM CaseWorkflows WHERE EditLeaseHolder IS NOT NULL";
        var leasesBefore = await database.ScalarAsync<int>(HeldLeases);

        using (var calculation = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_glass_session_get", new
        {
            caseId, read = "calculation",
        })))
        {
            var structured = await ReadStructuredContentAsync(calculation);
            Assert.Equal(13209.89m, structured.GetProperty("calculation").GetProperty("repairCostsInclVat").GetDecimal());
            Assert.True(IsAbsent(structured, "code"));
            Assert.True(IsAbsent(structured, "readCode"));
            var session = structured.GetProperty("session");
            Assert.Equal("staff", session.GetProperty("owner").GetString());
            Assert.Equal(0, session.GetProperty("version").GetInt64());
        }
        using (var unknown = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_glass_session_get", new
        {
            caseId, read = "position", positionId = "C1.2.C3.4",
        })))
        {
            var structured = await ReadStructuredContentAsync(unknown);
            Assert.Equal(GlassFailure.EditorRequest, structured.GetProperty("readCode").GetString());
            Assert.True(IsAbsent(structured, "code"));
            Assert.True(IsAbsent(structured, "position"));
        }

        Assert.Equal(2, glass.Requests.Count);
        Assert.Equal(0, glass.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(leasesBefore, await database.ScalarAsync<int>(HeldLeases));
    }

    /// <summary>
    /// The host composes Glass's (DevelopmentOffline with Features:LiveGlass)
    /// with reserved test origins and the Automation's made-up login. Host
    /// settings are the one channel Program reads this early.
    /// </summary>
    private static WebApplicationFactory<Program> WithGlass(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:LiveGlass", "true");
            builder.UseSetting("Glass:MarketValueAssessorBaseUri", GlassProviderFixture.MvaBase.AbsoluteUri);
            builder.UseSetting("Glass:EstimatorBaseUri", GlassProviderFixture.EstimatorBase.AbsoluteUri);
            builder.UseSetting("Glass:CallbackBaseUri", GlassProviderFixture.CallbackBase.AbsoluteUri);
            builder.UseSetting("Glass:RepairProfileId", GlassProviderFixture.ProfileId);
            builder.UseSetting("Glass:AutomationAccount:Username", AutomationLogin);
            builder.UseSetting("Glass:AutomationAccount:Password", "automation-ingress-synthetic-password");
        });

    /// <summary>
    /// The scripted editor on the host's own named Glass's client, so the
    /// gateway's requests reach it and nothing else.
    /// </summary>
    private static WebApplicationFactory<Program> WithEditor(WebApplicationFactory<Program> factory, ScriptedGlass glass) =>
        factory.WithWebHostBuilder(builder =>
        {
            // The editor's own pace is a browser's; here it waits for nothing.
            builder.UseSetting("Glass:EditorPauseMilliseconds", "0");
            builder.ConfigureTestServices(services => services.Configure<HttpClientFactoryOptions>(
                GlassRepairEstimateOptions.HttpClientName,
                options => options.HttpMessageHandlerBuilderActions.Add(
                    handlerBuilder => handlerBuilder.PrimaryHandler = glass)));
        });

    /// <summary>
    /// The protected state of an Automation session whose editor is open on
    /// the scripted estimator, in the shape the gateway keeps it, protected
    /// by the host's own key ring. Every value is made up.
    /// </summary>
    private static string OpenEditorState(IServiceProvider services) =>
        services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(GlassRepairEstimateGateway.ProtectionPurpose)
            .Protect(JsonSerializer.Serialize(new
            {
                registration = "AB12CDE",
                mileageMiles = 33000,
                editor = new
                {
                    baseAddress = GlassProviderFixture.EstimatorBase.AbsoluteUri + "ere/aco/",
                    url0 = "made-up-url0",
                    key = "made-up-session-key",
                    cookies = new Dictionary<string, string>(),
                },
            }));

    /// <summary>
    /// The Automation's open session on the Case, as its start records it,
    /// on its own login: written through the real store. With its editor,
    /// its protected state holds an open editor on the scripted estimator.
    /// </summary>
    private static async Task<Guid> SeedAutomationSessionAsync(
        WebApplicationFactory<Program> factory, Guid caseId, bool withEditor = false)
    {
        var sessionId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IGlassRepairEstimateSessionStore>().CreateAsync(
            new GlassRepairEstimateSessionMaterial(
                new GlassRepairEstimateSession(
                    sessionId,
                    caseId,
                    Guid.Empty,
                    CredentialGeneration: 0,
                    GlassAccountKey.Of(AutomationLogin),
                    GlassRepairEstimateSessionState.Active,
                    Version: 0,
                    OperationKey: "mcp:seeded-start",
                    SeedUtcNow,
                    ProviderVehicleId: null,
                    ProviderEstimateId: null,
                    FailureCode: null)
                {
                    OwnerKind = ActorKind.Automation,
                },
                protectedProviderState: withEditor
                    ? OpenEditorState(scope.ServiceProvider)
                    : "protected:fixture:not-a-secret",
                GlassRepairEstimateGateway.CallbackDigestOf(GlassRepairEstimateCallbackWebTests.NewCorrelation())),
            CancellationToken.None);
        return sessionId;
    }

    /// <summary>
    /// An Engineer's own Active session on the Case, as their launch records
    /// it on their own made-up login, with help on or off.
    /// </summary>
    private static async Task<Guid> SeedEngineerSessionAsync(
        WebApplicationFactory<Program> factory, Guid caseId, string login, bool helpOn)
    {
        var sessionId = Guid.NewGuid();
        var engineerId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IGlassRepairEstimateSessionStore>().CreateAsync(
            new GlassRepairEstimateSessionMaterial(
                new GlassRepairEstimateSession(
                    sessionId,
                    caseId,
                    engineerId,
                    CredentialGeneration: 1,
                    GlassAccountKey.Of(login),
                    GlassRepairEstimateSessionState.Active,
                    Version: 0,
                    OperationKey: "launch-" + login,
                    SeedUtcNow,
                    ProviderVehicleId: null,
                    ProviderEstimateId: null,
                    FailureCode: null),
                protectedProviderState: "protected:fixture:not-a-secret",
                GlassRepairEstimateGateway.CallbackDigestOf(GlassRepairEstimateCallbackWebTests.NewCorrelation())),
            CancellationToken.None);
        if (helpOn)
        {
            await TurnHelpOnAsync(factory, sessionId);
        }

        return sessionId;
    }

    /// <summary>The Engineer turns help on, as the Case page's post does.</summary>
    private static async Task TurnHelpOnAsync(WebApplicationFactory<Program> factory, Guid sessionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var material = await scope.ServiceProvider.GetRequiredService<IGlassRepairEstimateSessionStore>()
            .GetAsync(sessionId, CancellationToken.None)
            ?? throw new InvalidOperationException("The session was not seeded.");
        Assert.True(await scope.ServiceProvider.GetRequiredService<IGlassSessionHelpStore>().SetHelpAsync(
            sessionId,
            ActionActor.Staff(material.Session.PegasusUserId, [StaffRole.Engineer]),
            true,
            CancellationToken.None));
    }

    /// <summary>The session moved on, as the Engineer's own Save &amp; Exit moves it.</summary>
    private static async Task MoveAsync(
        WebApplicationFactory<Program> factory, Guid sessionId, GlassRepairEstimateSessionState state)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGlassRepairEstimateSessionStore>();
        var material = await store.GetAsync(sessionId, CancellationToken.None)
            ?? throw new InvalidOperationException("The session was not seeded.");
        await store.SaveAsync(
            new GlassRepairEstimateSessionMaterial(
                material.Session with { State = state },
                material.ProtectedProviderState,
                material.CallbackDigest,
                material.ResultArtifactsJson),
            material.Session.Version,
            CancellationToken.None);
    }

    private static async Task<int> CountHistoryAsync(IntakeWebApplicationFactory factory, string tool) =>
        await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_mcp'
              AND EventKind = N'{tool}'
              AND ActorKind = N'Automation'
              AND Outcome = N'Succeeded'
            """);

    /// <summary>A refused call's own sentence, as the content blocks carry it.</summary>
    private static async Task<string> RefusalAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonRpcAsync(response);
        var result = document.RootElement.GetProperty("result");
        Assert.True(
            result.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
            "Expected a refused tool call: " + result);
        return string.Join(
            ' ',
            result.GetProperty("content").EnumerateArray()
                .Select(block => block.TryGetProperty("text", out var text) ? text.GetString() : null)
                .OfType<string>());
    }

    private static bool IsAbsent(JsonElement element, string name) =>
        !element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;
}
