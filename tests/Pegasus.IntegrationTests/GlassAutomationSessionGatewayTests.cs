using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Glass;
using static Pegasus.IntegrationTests.GlassProviderFixture;

namespace Pegasus.IntegrationTests;

// The Automation's own Glass's sessions (design 07 sections 1 to 3; operator,
// 18 November 2026, R1177 to R1180), through the gateway against the scripted
// portal and the scripted editor of GlassProviderFixture: start under the
// login the deployment holds for it, the editor kept open between calls,
// finish as a Draft, and the one live slot a login holds.
public sealed partial class GlassRepairEstimateGatewayTests
{
    /// <summary>
    /// start makes a new stock vehicle and a new estimate, as any first press
    /// does, opens the editor and keeps it in the protected state; the
    /// session names no member of staff and holds the Automation's own login.
    /// </summary>
    [Fact]
    public async Task AnAutomationStartMakesANewEstimateAndKeepsItsEditorOpen()
    {
        var harness = Harness.Create();

        var session = await AutomationStartAsync(harness);

        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Null(session.FailureCode);
        Assert.Equal(ActorKind.Automation, session.OwnerKind);
        Assert.Equal(Guid.Empty, session.PegasusUserId);
        Assert.Equal(0, session.CredentialGeneration);
        Assert.Equal(GlassAccountKey.Of(Harness.AutomationAccount), session.NormalizedExternalAccountKey);
        Assert.Equal(VehicleId, session.ProviderVehicleId);
        Assert.Equal(1, harness.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Equal(1, harness.Mva.Count("POST /ere/start-ere"));
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/aco2.php"));
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/selected.php"));

        var state = await harness.ProviderStateAsync(session.Id);
        Assert.False(string.IsNullOrEmpty(state.GetProperty("editor").GetProperty("key").GetString()));
        Assert.Equal(
            System.Text.Json.JsonValueKind.String,
            state.GetProperty("editorLastRequestAtUtc").ValueKind);
        // Nothing protected is on the session the Automation and the page read.
        Assert.Null(session.Automation);
    }

    /// <summary>
    /// The editor a start opened is the one the next call uses: reading the
    /// estimate asks Glass's once, with no second opening, and keeps the
    /// editor's cookies and pace.
    /// </summary>
    [Fact]
    public async Task TheEditorAStartOpenedSurvivesToTheNextCall()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var read = await harness.Gateway.ReadAutomationEstimateAsync(
            harness.Automation, session.Id, CancellationToken.None);

        Assert.NotNull(read);
        Assert.NotNull(read.PositionIds);
        Assert.Empty(read.PositionIds);
        Assert.Equal(session.Version + 1, read.Session.Version);
        Assert.Equal(GlassRepairEstimateSessionState.Active, read.Session.State);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/aco2.php"));
        Assert.Equal(2, harness.Mva.Count("GET /ere/aco/selected.php"));
        // A member of staff reads nothing of it: the session is the Automation's.
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Gateway.ReadAutomationEstimateAsync(
            harness.Engineer, session.Id, CancellationToken.None));
    }

    /// <summary>
    /// One live session per login, whoever holds it: while a member of staff
    /// holds the login the Automation is configured with, its start is
    /// refused before Glass's is asked, naming that session, and the other
    /// way round.
    /// </summary>
    [Fact]
    public async Task AStaffSessionAndAnAutomationSessionOnOneLoginBlockEachOther()
    {
        var harness = Harness.Create();
        harness.Credentials.Give(harness.Engineer, harness.EngineerId, account: Harness.AutomationAccount);
        var staff = await harness.LaunchAsync();
        var requests = harness.Mva.Requests.Count;

        var refused = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => AutomationStartAsync(harness));

        Assert.Equal(GlassRepairEstimateSessionConflict.ActiveAccount, refused.Conflict);
        Assert.Equal(staff.Id, refused.SessionId);
        Assert.Equal(requests, harness.Mva.Requests.Count);

        await harness.Gateway.CloseAsync(
            new(harness.Engineer, staff.Id, (await harness.Sessions.GetAsync(staff.Id, CancellationToken.None))!.Session.Version,
                true, "Closed in Glass's."),
            CancellationToken.None);
        var automation = await AutomationStartAsync(harness, "mcp:start-2");
        Assert.Equal(GlassRepairEstimateSessionState.Active, automation.State);

        var blocked = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.LaunchAsync(operationKey: "glass-launch-2"));
        Assert.Equal(GlassRepairEstimateSessionConflict.ActiveAccount, blocked.Conflict);
        Assert.Equal(automation.Id, blocked.SessionId);
    }

    /// <summary>
    /// A login of its own (R1177) is its own slot: a member of staff on
    /// another login works beside the Automation.
    /// </summary>
    [Fact]
    public async Task AnAutomationOnItsOwnLoginWorksBesideStaff()
    {
        var harness = Harness.Create();
        var staff = await harness.LaunchAsync();

        var automation = await AutomationStartAsync(harness);

        Assert.Equal(GlassRepairEstimateSessionState.Active, staff.State);
        Assert.Equal(GlassRepairEstimateSessionState.Active, automation.State);
        Assert.NotEqual(staff.NormalizedExternalAccountKey, automation.NormalizedExternalAccountKey);
    }

    /// <summary>
    /// A second start on the Case where the Automation's own session is live
    /// continues it: the estimate it already started is reopened on its
    /// vehicle and a new editor opened, and nothing new is made. The same
    /// operation key again is a replay that sends nothing.
    /// </summary>
    [Fact]
    public async Task ASecondStartContinuesTheAutomationsOwnSession()
    {
        var harness = Harness.Create();
        var first = await AutomationStartAsync(harness);
        var requests = harness.Mva.Requests.Count;

        var replayed = await AutomationStartAsync(harness);
        Assert.Equal(first.Id, replayed.Id);
        Assert.Equal(requests, harness.Mva.Requests.Count);

        var continued = await AutomationStartAsync(harness, "mcp:start-2");

        Assert.Equal(first.Id, continued.Id);
        Assert.Equal(GlassRepairEstimateSessionState.Active, continued.State);
        Assert.Equal(1, harness.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Equal(2, harness.Mva.Count("POST /ere/start-ere"));
        Assert.Equal(2, harness.Mva.Count("GET /ere/aco/aco2.php"));
        Assert.Single(harness.Store.Sessions);
    }

    /// <summary>
    /// finish saves the estimate in Glass's and brings it back through the
    /// session's own return as an import whose actor is the Automation: an
    /// import the Automation makes is a Draft, never put in use. The session
    /// names it, and the account is free.
    /// </summary>
    [Fact]
    public async Task AFinishLandsTheEstimateAsADraftAndNamesIt()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        // In this slice a test puts the positions on the estimate.
        harness.Mva.Respond("GET /ere/aco/selected.php", _ => EditorPage("selected.html", null));

        var finished = await AutomationFinishAsync(harness, session);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, finished.State);
        Assert.Equal(harness.Import.EstimateId, finished.Automation?.DraftSpecificationId);
        Assert.Equal("mcp:finish-1", finished.Automation?.FinishOperationKey);
        var import = Assert.Single(harness.Import.Requests);
        Assert.Equal(ActorKind.Automation, import.Actor.Kind);
        Assert.False(RepairSpecificationPolicy.BecomesCurrentWhenCreated(import.Actor));
        Assert.True(import.KeepsLease);
        Assert.Equal(VehicleId, import.GlassEstimate?.VehicleId);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/close.php"));
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
        Assert.Null(await harness.Sessions.FindLiveForAccountAsync(
            GlassAccountKey.Of(Harness.AutomationAccount), CancellationToken.None));
        // The editor is closed: none is kept once the save is made.
        Assert.False((await harness.ProviderStateAsync(session.Id)).TryGetProperty("editor", out _));

        // The same finish again answers the session as it stands and sends nothing.
        var requests = harness.Mva.Requests.Count;
        var replayed = await AutomationFinishAsync(harness, session);
        Assert.Equal(finished.Version, replayed.Version);
        Assert.Equal(requests, harness.Mva.Requests.Count);
    }

    /// <summary>
    /// An estimate with no position is not saved (design 07 section 3.2): the
    /// session is open again, with the code, and can be built on or closed.
    /// </summary>
    [Fact]
    public async Task AFinishOfAnEmptyEstimateSavesNothingAndLeavesTheSessionOpen()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var refused = await AutomationFinishAsync(harness, session);

        Assert.Equal(GlassRepairEstimateSessionState.Active, refused.State);
        Assert.Equal(GlassRepairEstimateSessionPolicy.AutomationEmptyFailureCode, refused.FailureCode);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/close.php"));
        Assert.Empty(harness.Import.Requests);
        Assert.True((await harness.ProviderStateAsync(session.Id)).TryGetProperty("editor", out _));

        var closed = await harness.Gateway.CloseAsync(
            new(harness.Automation, refused.Id, refused.Version, true, "Nothing to save."),
            CancellationToken.None);
        Assert.Equal(GlassRepairEstimateSessionState.Cancelled, closed.State);
    }

    /// <summary>
    /// When the Case moved on, or a member of staff holds it, the return is
    /// held, as for staff; a later finish lands it under the authority that
    /// call proves, and sends nothing more to the editor.
    /// </summary>
    [Fact]
    public async Task AHeldFinishIsLandedByTheNextFinish()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        harness.Mva.Respond("GET /ere/aco/selected.php", _ => EditorPage("selected.html", null));
        harness.Import.Refusal = new CaseEditLeaseExpiredException(harness.CaseId, Harness.CaseVersion);

        var held = await AutomationFinishAsync(harness, session);

        Assert.Equal(GlassRepairEstimateSessionState.AwaitingImport, held.State);
        Assert.Null(held.Automation?.DraftSpecificationId);
        harness.Import.Refusal = null;
        var saves = harness.Mva.Count("GET /ere/aco/service.php");

        var landed = await AutomationFinishAsync(harness, held, "mcp:finish-2");

        Assert.Equal(GlassRepairEstimateSessionState.Completed, landed.State);
        Assert.Equal(harness.Import.EstimateId, landed.Automation?.DraftSpecificationId);
        Assert.Equal(saves, harness.Mva.Count("GET /ere/aco/service.php"));
        Assert.Equal(2, harness.Import.Requests.Count);
    }

    /// <summary>
    /// Interrupted work settles as a staff session's does: a start that never
    /// reached Glass's stays Prepared and a later start runs it; a finish
    /// that may have acted becomes Unknown and keeps the login.
    /// </summary>
    [Fact]
    public async Task InterruptedStartAndFinishSettleAsForStaff()
    {
        var harness = Harness.Create();
        var step = await harness.Gateway.PrepareAutomationLaunchAsync(
            StartRequest(harness, "mcp:start-1"), CancellationToken.None);
        Assert.Equal(GlassRepairEstimateContinuation.AutomationStart, step.Continuation);

        var prepared = await harness.Gateway.SettleInterruptedAsync(
            harness.Automation, step.Session.Id, CancellationToken.None);
        Assert.Equal(GlassRepairEstimateSessionState.Prepared, prepared.State);
        Assert.Equal(GlassFailure.Interrupted, prepared.FailureCode);

        var started = await AutomationStartAsync(harness, "mcp:start-2");
        Assert.Equal(step.Session.Id, started.Id);
        Assert.Equal(GlassRepairEstimateSessionState.Active, started.State);

        harness.Mva.Respond("GET /ere/aco/selected.php", _ => EditorPage("selected.html", null));
        var finishing = await harness.Gateway.PrepareAutomationFinishAsync(
            FinishRequest(harness, started, "mcp:finish-1"), CancellationToken.None);
        Assert.Equal(GlassRepairEstimateContinuation.AutomationFinish, finishing.Continuation);
        Assert.Equal(GlassRepairEstimateSessionState.Launching, finishing.Session.State);

        var unknown = await harness.Gateway.SettleInterruptedAsync(
            harness.Automation, finishing.Session.Id, CancellationToken.None);
        Assert.Equal(GlassRepairEstimateSessionState.Unknown, unknown.State);
        Assert.Equal(GlassFailure.Interrupted, unknown.FailureCode);
        Assert.NotNull(await harness.Sessions.FindLiveForAccountAsync(
            GlassAccountKey.Of(Harness.AutomationAccount), CancellationToken.None));
    }

    /// <summary>
    /// The login the session was launched with is the one it continues on: a
    /// deployment login changed under a live session is refused, as a staff
    /// session's replaced credential is.
    /// </summary>
    [Fact]
    public async Task AChangedAutomationLoginDoesNotContinueTheSession()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        harness.AutomationLogin.Username = "another.automation.login";
        harness.Mva.Respond("GET /ere/aco/selected.php", _ => EditorPage("selected.html", null));

        var refused = await Assert.ThrowsAsync<GlassRepairEstimateRefusalException>(
            () => AutomationFinishAsync(harness, session));

        Assert.Equal("This Glass's session requires the account credentials it was launched with.", refused.Message);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/service.php"));
    }

    /// <summary>A member of staff never runs the Automation's acts, and the Automation never runs a staff launch.</summary>
    [Fact]
    public async Task TheAutomationsActsAreTheAutomationsAndTheStaffLaunchIsStaffs()
    {
        var harness = Harness.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Gateway.PrepareAutomationLaunchAsync(
            StartRequest(harness, "mcp:start-1") with { Actor = harness.Engineer }, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Gateway.PrepareLaunchAsync(
            StartRequest(harness, "glass-launch-1"), CancellationToken.None));
        Assert.Empty(harness.Mva.Requests);
    }

    private static GlassRepairEstimateLaunchRequest StartRequest(Harness harness, string operationKey) =>
        new(harness.Automation, harness.CaseId, Harness.CaseVersion, Harness.LeaseToken, operationKey,
            AutomationSessionId(operationKey));

    private static GlassAutomationFinishRequest FinishRequest(
        Harness harness, GlassRepairEstimateSession session, string operationKey) =>
        new(harness.Automation, session.Id, session.Version, Harness.CaseVersion, Harness.LeaseToken, operationKey);

    /// <summary>The id a start's new session takes, from its key, as the tool derives it.</summary>
    private static Guid AutomationSessionId(string operationKey) =>
        new(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("glass-start:" + operationKey)).AsSpan(0, 16));

    /// <summary>The Automation's start: the prepare half, then its provider work inline.</summary>
    private static async Task<GlassRepairEstimateSession> AutomationStartAsync(
        Harness harness, string operationKey = "mcp:start-1")
    {
        var step = await harness.Gateway.PrepareAutomationLaunchAsync(
            StartRequest(harness, operationKey), CancellationToken.None);
        return await harness.ContinueAsync(harness.Automation, step, CancellationToken.None);
    }

    /// <summary>The Automation's finish of the session as last read: the prepare half, then its work inline.</summary>
    private static async Task<GlassRepairEstimateSession> AutomationFinishAsync(
        Harness harness, GlassRepairEstimateSession session, string operationKey = "mcp:finish-1")
    {
        var step = await harness.Gateway.PrepareAutomationFinishAsync(
            FinishRequest(harness, session, operationKey), CancellationToken.None);
        return await harness.ContinueAsync(harness.Automation, step, CancellationToken.None);
    }
}
