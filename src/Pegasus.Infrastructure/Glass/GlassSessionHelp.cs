using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// What a help act did: the act as it stands (its positions only those that
/// carry the code its dialog served), the positions that came onto the
/// estimate meanwhile from the member of staff's own window, the estimate's
/// positions as read after the act when they were, and whether the window
/// says it has shown the change.
/// </summary>
public sealed record GlassSessionHelpOutcome(
    GlassAutomationAct Act, IReadOnlyList<string> AlsoNew, IReadOnlyList<string>? Positions, bool ShownInWindow);

/// <summary>
/// The Automation's help in a member of staff's open Glass's session
/// (operator, 18 November 2026; R1170, R1182, R1185; design 08 sections 5
/// and 6): it reads the estimate, searches its lists, adds a position and
/// removes one, on the editor key the Pegasus helper handed over
/// (<see cref="GlassHelpKeys"/>), and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// It has no path to save, exit or close the editor, to the import, to the
/// session store's writes or to any Glass's login: it holds the keys, an
/// editor client and the help store's history rows, and
/// <c>GlassSessionHelpBoundaryTests</c> pins that. The member of staff's own
/// Save &amp; Exit brings the estimate back through the ordinary return (R1184).
/// </para>
/// <para>
/// Every call needs the session <c>Active</c> with help on, no other help
/// act holding it, and a key held; each is refused with its
/// <c>glass.help.*</c> code before anything is sent. What may be added is
/// <see cref="GlassAutomationPositionPolicy.Sends"/>, unchanged (R1182);
/// a remove takes any position of the estimate (R1185); nothing is typed
/// (R1168). An add or a remove reads the estimate first. Afterwards the
/// window is asked to show its selected list again, and each act is a
/// history row on the session as the Automation's. No Case lease is taken
/// and the session's version does not move.
/// </para>
/// </remarks>
public sealed class GlassSessionHelp(
    GlassHelpKeys keys,
    IHttpClientFactory httpClientFactory,
    GlassRepairEstimateOptions options,
    IGlassSessionHelpStore store,
    TimeProvider timeProvider,
    ILogger<GlassSessionHelp> logger)
{
    private GlassEditorPositionWork Work => work ??= new(logger);

    private GlassEditorPositionWork? work;

    /// <summary>
    /// One read of the estimate, asked of Glass's with one request, as the
    /// Automation's own session has it (R1206): its positions and Glass's
    /// running totals, one position's figures, or Glass's calculation.
    /// Nothing changes at Glass's; a read it did not answer answers its code.
    /// </summary>
    public async Task<GlassAutomationEstimate> ReadAsync(
        ActionActor actor, GlassRepairEstimateSession session, GlassAutomationRead read,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        Require(actor, session);
        using var turn = Enter(session);
        var done = await Work.ReadAsync(session, turn.Editor, turn.Opened, read, cancellationToken);
        Keep(session, turn.Editor, done.Opened);
        return done.For(session);
    }

    /// <summary>The catalogue search of the estimate's lists, as the Automation's own session has it; nothing changes at Glass's.</summary>
    public async Task<GlassAutomationCatalogue> SearchAsync(
        GlassAutomationCatalogueQuery query, GlassRepairEstimateSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Term);
        Require(query.Actor, session);
        using var turn = Enter(session);
        var (rows, code, opened) = await Work.SearchAsync(
            session, turn.Editor, turn.Opened, query.Term.Trim(), query.Kind, query.Side, cancellationToken);
        Keep(session, turn.Editor, opened);
        return new(session, rows, code);
    }

    /// <summary>Adds a position from a row the search answered, posted as Glass's dialog serves it.</summary>
    public Task<GlassSessionHelpOutcome> AddAsync(
        GlassAutomationPositionRequest request, GlassRepairEstimateSession session, CancellationToken cancellationToken) =>
        ActAsync(request, session, GlassAutomationActAction.Add, cancellationToken);

    /// <summary>Removes any position of the estimate (R1185).</summary>
    public Task<GlassSessionHelpOutcome> RemoveAsync(
        GlassAutomationPositionRequest request, GlassRepairEstimateSession session, CancellationToken cancellationToken) =>
        ActAsync(request, session, GlassAutomationActAction.Remove, cancellationToken);

    private async Task<GlassSessionHelpOutcome> ActAsync(
        GlassAutomationPositionRequest request, GlassRepairEstimateSession session, GlassAutomationActAction action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        if (request.Action != action || request.SessionId != session.Id)
        {
            throw new ArgumentException("The act names another action or another session.", nameof(request));
        }

        Require(request.Actor, session);
        if (session.Version != request.ExpectedVersion)
        {
            throw new GlassRepairEstimateSessionConflictException(
                GlassRepairEstimateSessionConflict.Version,
                session.Id,
                $"The Glass's session is at version {session.Version} and not {request.ExpectedVersion}.");
        }

        var act = Claimed(request);
        if (act.Outcome != GlassAutomationActOutcome.Pending)
        {
            // Refused before anything is asked of Glass's (R1182).
            return await RecordAsync(request.Actor, session, new(act, [], null, false));
        }

        using var turn = Enter(session);
        var editor = turn.Editor;
        GlassEditorEstimate before;
        var opened = turn.Opened;
        try
        {
            var read = await Work.StageAsync(session, "EditorRead", () => editor.ReadEstimateAsync(
                opened, cancellationToken));
            opened = read.Session;
            before = read.Value;
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException
                || GlassEditorPositionWork.IsTransportFailure(failure, cancellationToken))
        {
            // Reading changes nothing: nothing was sent for the act.
            Keep(session, editor, opened);
            return await RecordAsync(request.Actor, session, new(
                act with { Outcome = GlassAutomationActOutcome.Refused, Code = GlassEditorPositionWork.CodeOf(failure) },
                [], null, false));
        }

        Keep(session, editor, opened);
        if (!before.Found)
        {
            // The key no longer opens this estimate's list: nothing is sent.
            return await RecordAsync(request.Actor, session, new(
                act with { Outcome = GlassAutomationActOutcome.Refused, Code = GlassFailure.EditorAnswer },
                [], null, false));
        }

        // The editor's cookies and pace are kept on the held key before the
        // post and once the act has an outcome.
        Task Record(GlassAutomationAct recorded, GlassEditorSession at)
        {
            Keep(session, editor, at);
            return Task.CompletedTask;
        }

        var done = action == GlassAutomationActAction.Remove
            ? await Work.RemovePositionAsync(session, editor, opened, act, before, Record, cancellationToken)
            : await Work.AddPositionAsync(session, editor, opened, act, before, Record, cancellationToken);
        var (settled, alsoNew) = Split(done, before);

        // The window is asked to show its selected list again whenever the
        // estimate may have changed, a change not confirmed included.
        var shown = false;
        if (settled.Outcome is GlassAutomationActOutcome.Added or GlassAutomationActOutcome.Removed
            or GlassAutomationActOutcome.NotConfirmed)
        {
            shown = await keys.ShownAsync(session.Id, keys.Refresh(session.Id), CancellationToken.None);
        }

        return await RecordAsync(
            request.Actor, session, new(settled, alsoNew, done.After?.Positions, shown));
    }

    /// <summary>
    /// The act as asked, Pending; or, for an add the Automation may not send
    /// (R1182), Refused with its reason, and nothing is sent.
    /// </summary>
    private GlassAutomationAct Claimed(GlassAutomationPositionRequest request)
    {
        var act = new GlassAutomationAct(
            request.OperationKey.Trim(), request.Action, GlassAutomationActOutcome.Pending, timeProvider.GetUtcNow());
        if (request.Action == GlassAutomationActAction.Remove)
        {
            if (string.IsNullOrWhiteSpace(request.PositionId))
            {
                throw new ArgumentException("A remove names a position.", nameof(request));
            }

            return act with { PositionId = request.PositionId.Trim() };
        }

        var kind = request.Kind ?? throw new ArgumentException("An add names its kind of position.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Row)
            || (kind == GlassAutomationPositionKind.Part) != request.Operation.HasValue)
        {
            throw new ArgumentException(
                "An add names its row, and an operation for a part and only for a part.", nameof(request));
        }

        act = act with
        {
            Kind = kind,
            Row = request.Row.Trim(),
            Operation = request.Operation,
            Side = string.IsNullOrWhiteSpace(request.Side) ? null : request.Side.Trim(),
        };
        return GlassAutomationPositionPolicy.Sends(kind, request.Operation)
            ? act
            : act with { Outcome = GlassAutomationActOutcome.Refused, Code = GlassEstimateBuildPolicy.NotBuiltCode };
    }

    /// <summary>
    /// The positions the act's estimate read gained since the read before it:
    /// an add's own are those carrying the code its dialog served, and the
    /// rest came from the member of staff's window meanwhile. An add Glass's
    /// answered as done whose code is on no new position is not confirmed.
    /// The Automation never removes a position to tidy up.
    /// </summary>
    private static (GlassAutomationAct Act, IReadOnlyList<string> AlsoNew) Split(
        GlassEditorPositionDone done, GlassEditorEstimate before)
    {
        var act = done.Act;
        if (done.After is not { } after)
        {
            return (act, Array.Empty<string>());
        }

        var fresh = after.Positions.Where(id => !before.Positions.Contains(id, StringComparer.Ordinal)).ToList();
        var own = act.Action == GlassAutomationActAction.Add && act.ExpectedCode is { } expected
            ? fresh.Where(id => string.Equals(GlassAutomationPositionPolicy.CodeOf(id), expected, StringComparison.Ordinal)).ToList()
            : new List<string>();
        IReadOnlyList<string> alsoNew = fresh.Where(id => !own.Contains(id, StringComparer.Ordinal)).ToList();
        if (act.Action != GlassAutomationActAction.Add || act.Outcome != GlassAutomationActOutcome.Added)
        {
            return (act, alsoNew);
        }

        return own.Count > 0
            ? (act with { PositionIds = own }, alsoNew)
            : (act with { Outcome = GlassAutomationActOutcome.NotConfirmed, Code = GlassFailure.EditorAnswer, PositionIds = [] },
                alsoNew);
    }

    /// <summary>The act's history row, written whatever the caller does, so what was sent is never forgotten.</summary>
    private async Task<GlassSessionHelpOutcome> RecordAsync(
        ActionActor actor, GlassRepairEstimateSession session, GlassSessionHelpOutcome outcome)
    {
        await store.RecordHelpActAsync(session.Id, session.CaseId, actor, outcome.Act, CancellationToken.None);
        return outcome;
    }

    /// <summary>
    /// The Automation, in a member of staff's session that is <c>Active</c>
    /// with help on; a session found ended forgets its key.
    /// </summary>
    private void Require(ActionActor actor, GlassRepairEstimateSession session)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(session);
        if (actor.Kind != ActorKind.Automation)
        {
            throw new InvalidOperationException("Only the Automation helps in a member of staff's Glass's session.");
        }
        if (session.OwnerKind != ActorKind.Staff)
        {
            throw new InvalidOperationException("The Automation's own Glass's session is not helped in; it is its own.");
        }
        if (GlassSessionHelpPolicy.RefusalOf(session) is { } refused)
        {
            if (refused == GlassSessionHelpPolicy.NotActiveCode)
            {
                keys.Drop(session.Id);
            }

            throw new GlassSessionHelpRefusalException(refused);
        }
    }

    /// <summary>The session held for this one act, the key's editor session and a client at its pace; or the refusal.</summary>
    private Turn Enter(GlassRepairEstimateSession session)
    {
        var gate = keys.TryEnter(session.Id)
            ?? throw new GlassSessionHelpRefusalException(GlassSessionHelpPolicy.BusyCode);
        if (keys.Session(session.Id) is not { } opened)
        {
            gate.Dispose();
            throw new GlassSessionHelpRefusalException(GlassSessionHelpPolicy.NoWindowCode);
        }

        var editor = new GlassEditorClient(
            httpClientFactory.CreateClient(GlassRepairEstimateOptions.HttpClientName),
            options,
            timeProvider,
            options.EditorPause)
        {
            LastRequestAtUtc = keys.LastRequestAtUtc(session.Id),
        };
        return new Turn(gate, opened, editor);
    }

    private void Keep(GlassRepairEstimateSession session, GlassEditorClient editor, GlassEditorSession opened) =>
        keys.Keep(session.Id, opened, editor.LastRequestAtUtc);

    /// <summary>One help act's hold on the session, with the editor session and client it acts with.</summary>
    private sealed class Turn(IDisposable gate, GlassEditorSession opened, GlassEditorClient editor) : IDisposable
    {
        public GlassEditorSession Opened { get; } = opened;

        public GlassEditorClient Editor { get; } = editor;

        public void Dispose() => gate.Dispose();
    }
}
