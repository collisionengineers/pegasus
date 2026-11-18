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
