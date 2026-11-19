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
/// and 6): it reads the estimate, searches its lists, previews a dialog,
/// adds a position, changes one (R1206) and removes one, on the editor key the Pegasus helper handed over
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
/// <c>glass.help.*</c> code before anything is sent. An add gives the
/// dialog what the Automation's own session gives it, its inputs included
/// (R1206); a remove takes any position of the estimate (R1185); nothing is
/// typed (R1168); a preview posts nothing. An add or a remove reads the estimate first. Afterwards the
/// window is asked to show its selected list again, and each act is a
/// history row on the session as the Automation's. No Case lease is taken
/// and the session's version does not move.
/// </para>
/// </remarks>
public sealed partial class GlassSessionHelp(
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
        if (string.IsNullOrWhiteSpace(query.Row))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query.Term);
        }

        Require(query.Actor, session);
        using var turn = Enter(session);
        var (rows, code, opened) = await Work.SearchAsync(
            session, turn.Editor, turn.Opened, query.Term.Trim(), query.Kind, query.Side,
            string.IsNullOrWhiteSpace(query.Row) ? null : query.Row.Trim(), cancellationToken);
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

    /// <summary>Changes any position of the estimate through its own dialog, as in the Automation's own session (R1206).</summary>
    public Task<GlassSessionHelpOutcome> ChangeAsync(
        GlassAutomationPositionRequest request, GlassRepairEstimateSession session, CancellationToken cancellationToken) =>
        ActAsync(request, session, GlassAutomationActAction.Change, cancellationToken);

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

        if (request.Action == GlassAutomationActAction.Preview)
        {
            throw new ArgumentException("A preview is not an act.", nameof(request));
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
            // Refused before anything is asked of Glass's: an operation the return cannot read back.
            LogRefusedBeforeSending(logger, session.Id, session.CaseId, act.Code ?? string.Empty, act.Inputs?.Operation ?? string.Empty);
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

        var done = action switch
        {
            GlassAutomationActAction.Remove => await Work.RemovePositionAsync(session, editor, opened, act, before, Record, cancellationToken),
            GlassAutomationActAction.Change => await Work.ChangePositionAsync(session, editor, opened, act, before, Record, cancellationToken),
            _ => await Work.AddPositionAsync(session, editor, opened, act, before, Record, cancellationToken),
        };
        var (settled, alsoNew) = Split(done, before);

        // The window is asked to show its selected list again whenever the
        // estimate may have changed, a change not confirmed included.
        var shown = false;
        if (settled.Outcome is GlassAutomationActOutcome.Added or GlassAutomationActOutcome.Removed
            or GlassAutomationActOutcome.Changed or GlassAutomationActOutcome.NotConfirmed)
        {
            shown = await keys.ShownAsync(session.Id, keys.Refresh(session.Id), CancellationToken.None);
        }

        return await RecordAsync(
            request.Actor, session, new(settled, alsoNew, done.After?.Positions, shown));
    }

    /// <summary>
    /// The act as asked (<see cref="GlassAutomationPositionPolicy.Draft"/>):
    /// Pending, or refused with its code when it chooses an operation the
    /// return cannot read back, as in the Automation's own session (R1206).
    /// </summary>
    private GlassAutomationAct Claimed(GlassAutomationPositionRequest request) =>
        GlassAutomationPositionPolicy.Draft(request, timeProvider.GetUtcNow());

    /// <summary>
    /// A preview in the Engineer's session (R1206), as the Automation's own
    /// session has it: the dialog an add would post, or a position on the
    /// estimate is shown by, read as a form. Nothing is posted, no act or
    /// history row is recorded, and the window is not asked to redraw.
    /// </summary>
    public async Task<GlassAutomationPreview> PreviewAsync(
        GlassAutomationPositionRequest request, GlassRepairEstimateSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        if (request.Action != GlassAutomationActAction.Preview || request.SessionId != session.Id)
        {
            throw new ArgumentException("The request is not a preview of this session.", nameof(request));
        }

        var draft = GlassAutomationPositionPolicy.Draft(request, timeProvider.GetUtcNow());
        Require(request.Actor, session);
        using var turn = Enter(session);
        var (dialog, code, opened, equipment) = await Work.PreviewAsync(session, turn.Editor, turn.Opened, draft, cancellationToken);
        Keep(session, turn.Editor, opened);
        return new(session, dialog, code) { Equipment = equipment?.Question, HiddenBy = equipment?.HiddenBy ?? [] };
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
        var own = act.Action == GlassAutomationActAction.Add && act.ExpectedCode is not null
            ? fresh.Where(id => GlassAutomationPositionPolicy.Carries(act, id)).ToList()
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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's help in session {SessionId} for case {CaseId} add refused before sending at {Code} for operation {Operation}")]
    private static partial void LogRefusedBeforeSending(
        ILogger logger, Guid sessionId, Guid caseId, string code, string operation);
}
