using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Background;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record GlassSessionToolItem(
    Guid SessionId,
    long Version,
    [property: Description("Prepared, Launching, Active, Importing, AwaitingImport, Completed, Failed, Unknown, Expired or Cancelled.")] string State,
    [property: Description("The glass.* code of what Glass's answered, when it stopped or refused.")] string? FailureCode,
    DateTimeOffset CreatedAtUtc,
    [property: Description("Work for the session is queued or running now; read it again later.")] bool Working,
    [property: Description("The Draft repair spec a finished session landed as.")] Guid? DraftSpecificationId);

internal sealed record GlassAccountToolItem(
    [property: Description("No session holds the Glass's account.")] bool Free,
    [property: Description("staff or automation, when a session holds the account.")] string? HeldBy,
    Guid? CaseId,
    string? CaseReference);

internal sealed record GlassEstimateToolItem(
    [property: Description("Each position's id on the estimate.")] IReadOnlyList<string> PositionIds,
    [property: Description("The codes on the estimate, as Glass's lists them.")] IReadOnlyList<string> Codes);

internal sealed record GlassListedPartToolItem(string Code, string Text, bool Ticked);

internal sealed record GlassActToolItem(
    string OperationKey,
    [property: Description(AutomationToolText.GlassPositionAction)] string Action,
    [property: Description(AutomationToolText.GlassActOutcome)] string Outcome,
    [property: Description(AutomationToolText.GlassActCode)] string? Code,
    [property: Description(AutomationToolText.GlassCatalogueKind)] string? Kind,
    string? Row,
    string? Operation,
    string? Side,
    string? PositionId,
    [property: Description(AutomationToolText.GlassActPositionIds)] IReadOnlyList<string> PositionIds,
    [property: Description(AutomationToolText.GlassActDescription)] string? Description,
    [property: Description(AutomationToolText.GlassActPrice)] decimal? Price,
    [property: Description(AutomationToolText.GlassActHours)] decimal? Hours,
    [property: Description(AutomationToolText.GlassActMandatoryParts)] IReadOnlyList<GlassListedPartToolItem> MandatoryParts,
    DateTimeOffset RequestedAtUtc);

internal sealed record GlassSessionGetToolResult(
    Guid CaseId,
    GlassSessionToolItem? Session,
    GlassAccountToolItem Account,
    [property: Description("Present only when readEstimate was asked and Glass's answered.")] GlassEstimateToolItem? Estimate,
    [property: Description(AutomationToolText.GlassActs)] IReadOnlyList<GlassActToolItem> Acts,
    string CorrelationId);

internal sealed record GlassCatalogueRowToolItem(
    [property: Description(AutomationToolText.GlassCatalogueRow)] string Row,
    [property: Description(AutomationToolText.GlassCatalogueKind)] string Kind,
    string Description,
    string? PartCode,
    string? PartNumber,
    [property: Description(AutomationToolText.GlassCatalogueSide)] string? Side,
    [property: Description(AutomationToolText.GlassCataloguePaintable)] bool Paintable);

internal sealed record GlassCatalogueSearchToolResult(
    Guid CaseId,
    GlassSessionToolItem? Session,
    IReadOnlyList<GlassCatalogueRowToolItem> Items,
    string? NextCursor,
    [property: Description(AutomationToolText.GlassCatalogueCode)] string? Code,
    string CorrelationId);

internal sealed record GlassPositionActToolResult(
    Guid CaseId,
    GlassSessionToolItem? Session,
    GlassActToolItem Act,
    string OperationKey);

internal sealed record GlassSessionActToolResult(
    Guid CaseId,
    GlassSessionToolItem? Session,
    string OperationKey);

/// <summary>
/// The Automation actor's own Glass's sessions (design 07 section 3; operator,
/// 18 November 2026, R1177 to R1180): it starts a session under the login the
/// deployment holds for it, finishes it as a Draft repair spec, or closes it.
/// Every tool finds the Automation's session by its Case. A refusal Pegasus
/// makes before Glass's is asked is an error; what Glass's answered is the
/// session's state and code. Work that asks Glass's runs as provider work
/// keyed on the session, one piece at a time, and a call waits for it for up
/// to <see cref="AnswerWindow"/>.
/// </summary>
[McpServerToolType]
internal sealed class GlassMcpTools(
    IGlassRepairEstimateGateway gateway,
    IGlassRepairEstimateSessionReader sessions,
    IGlassRepairEstimateSessionStore store,
    GlassRepairEstimateAvailability availability,
    ProviderWorkQueue queue,
    ListCaseReferences listCaseReferences,
    IServiceProvider services,
    TimeProvider timeProvider,
    ICursorProtector cursors,
    AutomationMcpAuditor auditor,
    OneCommandEdit leases)
{
    /// <summary>
    /// How long a call waits for its work: the wait <c>pegasus_edit_begin</c>
    /// already makes inside a call (design 07 section 6). An engineering
    /// bound, not a business rule.
    /// </summary>
    private static readonly TimeSpan AnswerWindow = TimeSpan.FromSeconds(45);

    [McpServerTool(
        Name = "pegasus_glass_session_get",
        Title = "Get the Automation's Glass's session",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(AutomationToolText.GlassSessionGet)]
    public async Task<GlassSessionGetToolResult> GetSessionAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description("Ask Glass's once for the positions the open estimate holds.")] bool readEstimate = false,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_glass_session_get",
            AutomationMcpAuditor.Resource(caseId),
            async context =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                RequireAvailable();
                var session = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
                // A session waiting on work that nothing runs was interrupted,
                // and is settled as the Glass's window's poll settles it.
                if (session is not null
                    && GlassRepairEstimateSessionPolicy.AwaitsProviderWork(session.State)
                    && !queue.IsInFlight(session.Id))
                {
                    session = await gateway.SettleInterruptedAsync(context.Actor, session.Id, cancellationToken);
                }

                GlassEstimateToolItem? estimate = null;
                if (readEstimate
                    && session is { State: GlassRepairEstimateSessionState.Active }
                    && !queue.IsInFlight(session.Id))
                {
                    var sessionId = session.Id;
                    var read = await RunHereAsync(
                        sessionId,
                        "GlassAutomationRead",
                        token => gateway.ReadAutomationEstimateAsync(context.Actor, sessionId, token),
                        cancellationToken);
                    if (read is not null)
                    {
                        session = read.Session;
                        estimate = read.PositionIds is { } positions
                            ? new GlassEstimateToolItem(positions, read.Codes ?? [])
                            : null;
                    }
                }

                return new GlassSessionGetToolResult(
                    caseId,
                    Map(session),
                    await AccountAsync(context.Actor, cancellationToken),
                    estimate,
                    session?.Automation?.Acts.Select(MapAct).ToList() ?? [],
                    context.TraceIdentifier);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_glass_session_act",
        Title = "Start, finish or close the Automation's Glass's session",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(AutomationToolText.GlassSessionAct)]
    public async Task<GlassSessionActToolResult> ActOnSessionAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description("start, finish or close.")] string action,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("start and finish: " + AutomationToolText.ExpectedCaseVersion)] long? expectedVersion = null,
        [Description("finish and close: the session version from pegasus_glass_session_get; a stale value fails closed.")] long? expectedSessionVersion = null,
        [Description("close: why the session is ended without saving, 1 to 2,000 characters.")] string? reason = null,
        [Description(AutomationToolText.EditLeaseToken)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_glass_session_act",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                RequireAvailable();
                var session = action?.Trim() switch
                {
                    "start" => await StartAsync(
                        context.Actor, caseId, RequireValue(expectedVersion, "expectedVersion"), key,
                        editLeaseToken, cancellationToken),
                    "finish" => await FinishAsync(
                        context.Actor, caseId, RequireValue(expectedVersion, "expectedVersion"),
                        RequireValue(expectedSessionVersion, "expectedSessionVersion"), key, editLeaseToken,
                        cancellationToken),
                    "close" => await CloseAsync(
                        context.Actor, caseId, RequireValue(expectedSessionVersion, "expectedSessionVersion"),
                        reason, cancellationToken),
                    _ => throw new McpException("The action must be one of: start, finish, close."),
                };
                return new GlassSessionActToolResult(caseId, Map(session), key);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_glass_catalogue_search",
        Title = "Search the Automation's open Glass's estimate",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(AutomationToolText.GlassCatalogueSearch)]
    public async Task<GlassCatalogueSearchToolResult> SearchCatalogueAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.GlassSearchTerm)] string term,
        [Description(AutomationToolText.GlassSearchKind)] string? kind = null,
        [Description(AutomationToolText.GlassSearchSide)] string? side = null,
        [Description(AutomationToolText.Cursor)] string? cursor = null,
        [Description(AutomationToolText.PageSize)] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_glass_catalogue_search",
            AutomationMcpAuditor.Resource(caseId),
            async context =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                RequireAvailable();
                var wantedTerm = term?.Trim() ?? string.Empty;
                if (wantedTerm.Length is 0 or > MaximumTermLength)
                {
                    throw new McpException(AutomationToolText.GlassSearchTermRefused);
                }

                GlassAutomationPositionKind? wantedKind = kind?.Trim() switch
                {
                    null or "" or "any" => null,
                    "part" => GlassAutomationPositionKind.Part,
                    "special_operation" => GlassAutomationPositionKind.SpecialOperation,
                    _ => throw new McpException(AutomationToolText.GlassSearchKindRefused),
                };
                var wantedSide = SideOf(side);
                var pageSize = CursorPaging.NormalizeLimit(limit);
                var session = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
                if (session is not { State: GlassRepairEstimateSessionState.Active })
                {
                    throw new McpException(AutomationToolText.GlassNoSession);
                }

                // The cursor is bound to the caller, the session, the term,
                // the kind and the side (FRD-10): an offset into the rows
                // found, which each page asks Glass's for again (no cache).
                var sessionId = session.Id;
                var scope = CursorPaging.CreateScope(
                    "pegasus_glass_catalogue_search", context.Actor,
                    sessionId.ToString("D"), wantedTerm, wantedKind is { } scoped ? KindName(scoped) : "any", wantedSide);
                var offset = 0;
                if (!string.IsNullOrEmpty(cursor))
                {
                    var (sortKey, id) = cursors.Unprotect(cursor, scope);
                    if (id != sessionId
                        || !int.TryParse(sortKey, NumberStyles.None, CultureInfo.InvariantCulture, out offset))
                    {
                        throw new CursorRejectedException();
                    }
                }

                var found = await RunHereAsync(
                    sessionId,
                    "GlassAutomationSearch",
                    token => gateway.SearchAutomationCatalogueAsync(
                        new GlassAutomationCatalogueQuery(context.Actor, sessionId, wantedTerm, wantedKind, wantedSide),
                        token),
                    cancellationToken)
                    ?? throw new McpException(AutomationToolText.GlassNoSession);
                var items = found.Rows
                    .Skip(offset)
                    .Take(pageSize)
                    .Select(row => new GlassCatalogueRowToolItem(
                        row.Row, KindName(row.Kind), row.Description, row.PartCode, row.PartNumber, row.Side, row.Paintable))
                    .ToList();
                var next = offset + pageSize < found.Rows.Count
                    ? cursors.Protect(scope, (offset + pageSize).ToString(CultureInfo.InvariantCulture), sessionId)
                    : null;
                return new GlassCatalogueSearchToolResult(
                    caseId, Map(found.Session), items, next, found.Code, context.TraceIdentifier);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_glass_position_act",
        Title = "Add or remove a position on the Automation's Glass's estimate",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(AutomationToolText.GlassPositionAct)]
    public async Task<GlassPositionActToolResult> ActOnPositionAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.GlassPositionAction)] string action,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description(AutomationToolText.GlassExpectedSessionVersion)] long? expectedSessionVersion = null,
        [Description(AutomationToolText.GlassAddKind)] string? kind = null,
        [Description(AutomationToolText.GlassAddRow)] string? row = null,
        [Description(AutomationToolText.GlassAddOperation)] string? operation = null,
        [Description(AutomationToolText.GlassAddSide)] string? side = null,
        [Description(AutomationToolText.GlassRemovePositionId)] string? positionId = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_glass_position_act",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                RequireAvailable();
                var asked = PositionRequest(
                    context.Actor, action, key, RequireValue(expectedSessionVersion, "expectedSessionVersion"),
                    kind, row, operation, side, positionId);
                // The same key again answers the act as recorded and sends
                // nothing, whichever of the Case's Automation sessions holds it.
                if (await store.FindAutomationActSessionAsync(caseId, key, cancellationToken) is { } replayed
                    && GlassAutomationPositionPolicy.Find(replayed.Automation, key) is { } recorded)
                {
                    return new GlassPositionActToolResult(caseId, Map(replayed), MapAct(recorded), key);
                }

                var session = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
                if (session is not { State: GlassRepairEstimateSessionState.Active })
                {
                    throw new McpException(AutomationToolText.GlassNoSession);
                }
                if (queue.IsInFlight(session.Id))
                {
                    throw new McpException(AutomationToolText.GlassSessionWorking);
                }

                var id = session.Id;
                using var reservation = queue.Reserve(id);
                var step = await gateway.PrepareAutomationPositionAsync(asked with { SessionId = id }, cancellationToken)
                    ?? throw new McpException(AutomationToolText.GlassNoSession);
                if (!step.Owed)
                {
                    return new GlassPositionActToolResult(caseId, Map(step.Session), MapAct(step.Act), key);
                }

                await RunAsync(
                    reservation,
                    "GlassAutomationPosition",
                    (work, workToken) => work.GetRequiredService<IGlassRepairEstimateGateway>()
                        .ContinueAutomationPositionAsync(new(context.Actor, id, key), workToken),
                    cancellationToken);
                var after = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
                var act = GlassAutomationPositionPolicy.Find(after?.Automation, key) ?? step.Act;
                return new GlassPositionActToolResult(caseId, Map(after), MapAct(act), key);
            },
            cancellationToken);
    }

    /// <summary>How long a catalogue search's term may be (design 07 section 3.3).</summary>
    private const int MaximumTermLength = 200;

    /// <summary>
    /// A position act as asked, refused before anything is read when its
    /// action, kind, operation or side is not one the tool takes, or when
    /// what the action needs is missing. The session is named once found.
    /// </summary>
    private static GlassAutomationPositionRequest PositionRequest(
        ActionActor actor, string? action, string key, long expectedSessionVersion,
        string? kind, string? row, string? operation, string? side, string? positionId)
    {
        switch (action?.Trim())
        {
            case "remove":
                if (string.IsNullOrWhiteSpace(positionId))
                {
                    throw new McpException(AutomationToolText.GlassRemovePositionIdRefused);
                }

                return new(actor, Guid.Empty, expectedSessionVersion, key, GlassAutomationActAction.Remove)
                {
                    PositionId = positionId.Trim(),
                };
            case "add":
                var wantedKind = kind?.Trim() switch
                {
                    "part" => GlassAutomationPositionKind.Part,
                    "special_operation" => GlassAutomationPositionKind.SpecialOperation,
                    _ => throw new McpException(AutomationToolText.GlassAddKindRefused),
                };
                if (string.IsNullOrWhiteSpace(row))
                {
                    throw new McpException(AutomationToolText.GlassAddRowRefused);
                }

                GlassAutomationPartOperation? wantedOperation = operation?.Trim() switch
                {
                    null or "" => null,
                    "part" => GlassAutomationPartOperation.Part,
                    "part_and_paint" => GlassAutomationPartOperation.PartAndPaint,
                    "paint" => GlassAutomationPartOperation.Paint,
                    "repair" => GlassAutomationPartOperation.Repair,
                    "remove_and_refit" => GlassAutomationPartOperation.RemoveAndRefit,
                    _ => throw new McpException(AutomationToolText.GlassAddOperationRefused),
                };
                if (wantedKind == GlassAutomationPositionKind.Part && wantedOperation is null)
                {
                    throw new McpException(AutomationToolText.GlassAddOperationMissing);
                }
                if (wantedKind == GlassAutomationPositionKind.SpecialOperation && wantedOperation is not null)
                {
                    throw new McpException(AutomationToolText.GlassAddOperationNotForSpecial);
                }

                return new(actor, Guid.Empty, expectedSessionVersion, key, GlassAutomationActAction.Add)
                {
                    Kind = wantedKind,
                    Row = row.Trim(),
                    Operation = wantedOperation,
                    Side = SideOf(side),
                };
            default:
                throw new McpException(AutomationToolText.GlassPositionActionRefused);
        }
    }

    /// <summary>A side as a tool takes it: L or R, or none when omitted.</summary>
    private static string? SideOf(string? side) => side?.Trim() switch
    {
        null or "" => null,
        "L" => "L",
        "R" => "R",
        _ => throw new McpException(AutomationToolText.GlassSideRefused),
    };

    private static string KindName(GlassAutomationPositionKind kind) =>
        kind == GlassAutomationPositionKind.SpecialOperation ? "special_operation" : "part";

    private static string OperationName(GlassAutomationPartOperation operation) => operation switch
    {
        GlassAutomationPartOperation.PartAndPaint => "part_and_paint",
        GlassAutomationPartOperation.Paint => "paint",
        GlassAutomationPartOperation.Repair => "repair",
        GlassAutomationPartOperation.RemoveAndRefit => "remove_and_refit",
        _ => "part",
    };

    private static string OutcomeName(GlassAutomationActOutcome outcome) => outcome switch
    {
        GlassAutomationActOutcome.Added => "added",
        GlassAutomationActOutcome.Removed => "removed",
        GlassAutomationActOutcome.NotConfirmed => "not_confirmed",
        GlassAutomationActOutcome.Refused => "refused",
        _ => "pending",
    };

    private static GlassActToolItem MapAct(GlassAutomationAct act) =>
        new(
            act.OperationKey,
            act.Action == GlassAutomationActAction.Remove ? "remove" : "add",
            OutcomeName(act.Outcome),
            act.Code,
            act.Kind is { } kind ? KindName(kind) : null,
            act.Row,
            act.Operation is { } operation ? OperationName(operation) : null,
            act.Side,
            act.PositionId,
            act.PositionIds,
            act.Description,
            act.Served?.Price,
            act.Served?.Hours,
            act.Listed.Select(listed => new GlassListedPartToolItem(listed.Code, listed.Text, listed.Ticked)).ToList(),
            act.RequestedAtUtc);

    /// <summary>
    /// start: under the Case's one-command lease, the prepare half proves and
    /// claims, then the launch runs as provider work the call waits for. The
    /// new session's id comes from the operation key, so a replay names the
    /// same one.
    /// </summary>
    private async Task<GlassRepairEstimateSession?> StartAsync(
        ActionActor actor, Guid caseId, long expectedVersion, string key, string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        // Work already in hand for the Automation's session here is not
        // overtaken: the session is answered as it stands.
        var current = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
        if (current is not null && queue.IsInFlight(current.Id))
        {
            return current;
        }

        var sessionId = AutomationMcpErrors.DeriveOperationGuid("glass-start", actor, key);
        // The Automation's live session here is the one a start continues.
        var live = current is not null
            && current.Id != sessionId
            && GlassRepairEstimateSessionPolicy.OccupiesAccount(current.State)
                ? current.Id
                : (Guid?)null;
        return await leases.RunCaseAsync(
            caseId,
            expectedVersion,
            editLeaseToken,
            actor,
            key,
            async token =>
            {
                // Both sessions a start may act on are held busy from before
                // its claim, so nothing settles either between the claim and
                // its work.
                using var reservation = queue.Reserve(sessionId);
                using var continued = live is { } liveId ? queue.Reserve(liveId) : null;
                GlassRepairEstimateStep step;
                try
                {
                    step = await gateway.PrepareAutomationLaunchAsync(
                        new GlassRepairEstimateLaunchRequest(actor, caseId, expectedVersion, token, key, sessionId),
                        cancellationToken);
                }
                catch (GlassRepairEstimateSessionConflictException conflict)
                    when (conflict.Conflict == GlassRepairEstimateSessionConflict.ActiveAccount)
                {
                    throw new McpException(await HeldAsync(actor, conflict, cancellationToken));
                }

                if (step.Continuation == GlassRepairEstimateContinuation.None)
                {
                    return step.Session;
                }

                var id = step.Session.Id;
                using var late = id == sessionId || id == live ? null : queue.Reserve(id);
                await RunAsync(
                    id == sessionId ? reservation : late ?? continued!,
                    "GlassAutomationStart",
                    (work, workToken) => work.GetRequiredService<IGlassRepairEstimateGateway>()
                        .ContinueAutomationStartAsync(new(actor, id), workToken),
                    cancellationToken);
                return await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>
    /// finish: under the Case's one-command lease, so the import lands under
    /// the authority this call proves; the save and the return run as
    /// provider work the call waits for. A held estimate is landed the same
    /// way. An estimate with no position is refused and nothing is saved.
    /// </summary>
    private async Task<GlassRepairEstimateSession?> FinishAsync(
        ActionActor actor, Guid caseId, long expectedVersion, long expectedSessionVersion, string key,
        string? editLeaseToken, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
        if (session is not null
            && string.Equals(session.Automation?.FinishOperationKey, key, StringComparison.Ordinal)
            && session.State != GlassRepairEstimateSessionState.AwaitingImport)
        {
            // The same finish again: the session as it stands.
            return session;
        }
        if (session is null || !GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State))
        {
            throw new McpException(AutomationToolText.GlassNoSession);
        }
        if (queue.IsInFlight(session.Id))
        {
            return session;
        }

        var id = session.Id;
        var finished = await leases.RunCaseAsync(
            caseId,
            expectedVersion,
            editLeaseToken,
            actor,
            key,
            async token =>
            {
                using var reservation = queue.Reserve(id);
                var step = await gateway.PrepareAutomationFinishAsync(
                    new GlassAutomationFinishRequest(actor, id, expectedSessionVersion, expectedVersion, token, key),
                    cancellationToken);
                if (step.Continuation == GlassRepairEstimateContinuation.None)
                {
                    return step.Session;
                }

                await RunAsync(
                    reservation,
                    step.Continuation == GlassRepairEstimateContinuation.Import ? "GlassImport" : "GlassAutomationFinish",
                    (work, workToken) =>
                    {
                        var glass = work.GetRequiredService<IGlassRepairEstimateGateway>();
                        return step.Continuation == GlassRepairEstimateContinuation.Import
                            ? glass.ContinueImportAsync(new(actor, id), workToken)
                            : glass.ContinueAutomationFinishAsync(new(actor, id), workToken);
                    },
                    cancellationToken);
                return await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
            },
            cancellationToken);
        if (finished is { State: GlassRepairEstimateSessionState.Active, FailureCode: GlassRepairEstimateSessionPolicy.AutomationEmptyFailureCode }
            && finished.Id == id
            && !queue.IsInFlight(id))
        {
            throw new McpException(AutomationToolText.GlassEstimateEmpty);
        }

        return finished;
    }

    /// <summary>
    /// close: the call is the confirmation of external closure, as a member
    /// of staff's Close is. Nothing is sent to Glass's and no Case lease is
    /// taken. Refused while the session's work runs, as the staff Close is.
    /// </summary>
    private async Task<GlassRepairEstimateSession> CloseAsync(
        ActionActor actor, Guid caseId, long expectedSessionVersion, string? reason,
        CancellationToken cancellationToken)
    {
        var session = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
        if (session is null || !GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State))
        {
            throw new McpException(AutomationToolText.GlassNoSession);
        }
        if (queue.IsInFlight(session.Id))
        {
            throw new McpException(CaseWorkspaceLabels.GlassSession.CloseWhileWorking);
        }

        return await gateway.CloseAsync(
            new GlassRepairEstimateCloseRequest(
                actor, session.Id, expectedSessionVersion, ExternalSessionClosed: true, reason ?? string.Empty),
            cancellationToken);
    }

    /// <summary>
    /// Queues the work under the reservation and waits for it for up to
    /// <see cref="AnswerWindow"/>. Work in the queue runs to its own cap
    /// whatever the caller does, so a client that gives up never cancels a
    /// request to Glass's halfway. A full queue runs the work here, under its
    /// own cap rather than the call's.
    /// </summary>
    private async Task RunAsync(
        ProviderWorkReservation reservation,
        string kind,
        Func<IServiceProvider, CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = new ProviderWork(
            reservation.Key,
            kind,
            async (provider, token) =>
            {
                try
                {
                    await run(provider, token);
                }
                finally
                {
                    done.TrySetResult();
                }
            });
        if (reservation.Admit(work) == ProviderWorkAdmission.Full)
        {
            using var cap = new CancellationTokenSource(ProviderWorkService.TimeCap, timeProvider);
            await reservation.RunHereAsync(work, services, cap.Token);
            return;
        }

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await Task.WhenAny(done.Task, Task.Delay(AnswerWindow, timeProvider, wait.Token));
        await wait.CancelAsync();
    }

    /// <summary>One read that asks Glass's, run here under the session's turn in the queue.</summary>
    private async Task<T> RunHereAsync<T>(
        Guid sessionId, string kind, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        using var reservation = queue.Reserve(sessionId);
        T result = default!;
        await reservation.RunHereAsync(
            new ProviderWork(sessionId, kind, async (_, token) => result = await read(token)),
            services,
            cancellationToken);
        return result;
    }

    /// <summary>
    /// The refusal that names who holds the Glass's account, and on which
    /// Case. The conflict names the session in the way, or, when the
    /// account's index decided a race, the session that was not made; the
    /// holder is then read off the account. One freed meanwhile leaves the
    /// conflict's own sentence.
    /// </summary>
    private async Task<string> HeldAsync(
        ActionActor actor, GlassRepairEstimateSessionConflictException conflict, CancellationToken cancellationToken)
    {
        var holder = (await store.GetAsync(conflict.SessionId, cancellationToken))?.Session;
        if (holder is null || !GlassRepairEstimateSessionPolicy.OccupiesAccount(holder.State))
        {
            holder = await sessions.FindLiveForAccountAsync(
                services.GetRequiredService<GlassAutomationAccountSource>().Read().AccountKey, cancellationToken);
        }
        if (holder is null)
        {
            return conflict.Message;
        }

        var reference = await ReferenceAsync(actor, holder.CaseId, cancellationToken);
        return holder.OwnerKind == ActorKind.Automation
            ? AutomationToolText.GlassAccountHeldByAutomation(reference)
            : AutomationToolText.GlassAccountHeldByStaff(reference);
    }

    /// <summary>
    /// Whether the Automation's Glass's account is free, or which Case holds
    /// it and whether staff or the Automation do. No member of staff is named.
    /// </summary>
    private async Task<GlassAccountToolItem> AccountAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var account = services.GetRequiredService<GlassAutomationAccountSource>().Read();
        var live = await sessions.FindLiveForAccountAsync(account.AccountKey, cancellationToken);
        return live is null
            ? new GlassAccountToolItem(true, null, null, null)
            : new GlassAccountToolItem(
                false,
                live.OwnerKind == ActorKind.Automation ? "automation" : "staff",
                live.CaseId,
                await ReferenceAsync(actor, live.CaseId, cancellationToken));
    }

    private async Task<string> ReferenceAsync(ActionActor actor, Guid caseId, CancellationToken cancellationToken)
    {
        var references = await listCaseReferences.ExecuteAsync(new(actor, [caseId]), cancellationToken);
        return references.GetValueOrDefault(caseId, caseId.ToString("D"));
    }

    private void RequireAvailable()
    {
        if (!availability.Enabled)
        {
            throw new McpException(CaseWorkspaceLabels.GlassSession.Unavailable);
        }
    }

    private static long RequireValue(long? value, string name) =>
        value ?? throw new McpException($"{name} is required for this action.");

    private GlassSessionToolItem? Map(GlassRepairEstimateSession? session) =>
        session is null
            ? null
            : new GlassSessionToolItem(
                session.Id,
                session.Version,
                session.State.ToString(),
                session.FailureCode,
                session.CreatedAtUtc,
                queue.IsInFlight(session.Id),
                session.Automation?.DraftSpecificationId);
}
