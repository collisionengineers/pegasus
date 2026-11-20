using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// What a Glass's session the Automation owns has done, kept beside the
/// session so the Automation and the Case page read it without the protected
/// state (design 07 section 2). Nothing in it is secret.
/// </summary>
public sealed record GlassAutomationWork
{
    /// <summary>
    /// The operation key of the finish that claimed the session. The same key
    /// again answers the session as it stands and sends nothing to Glass's.
    /// </summary>
    public string? FinishOperationKey { get; init; }

    /// <summary>The repair spec the finish landed as: a Draft, which only staff put in use.</summary>
    public Guid? DraftSpecificationId { get; init; }

    /// <summary>
    /// Each position act, in the order it was asked for, by its operation
    /// key (design 07 section 3.4). A stored record written before acts
    /// existed reads as none.
    /// </summary>
    public IReadOnlyList<GlassAutomationAct> Acts { get; init; } = [];
}

/// <summary>Whether a position act adds a position or removes one.</summary>
public enum GlassAutomationActAction
{
    Add,
    Remove,
}

/// <summary>What became of a position act.</summary>
public enum GlassAutomationActOutcome
{
    /// <summary>Claimed; its work has not recorded an outcome yet.</summary>
    Pending,

    /// <summary>Posted, and the editor answered that it took it.</summary>
    Added,

    /// <summary>Removed, and the estimate read afterwards no longer lists it.</summary>
    Removed,

    /// <summary>
    /// Posted, and the answer was lost or was not the one expected: the
    /// estimate may or may not hold the change. The next read of the estimate
    /// settles it.
    /// </summary>
    NotConfirmed,

    /// <summary>Nothing was changed at Glass's; the code says why (R1169).</summary>
    Refused,
}

/// <summary>What an added position is: a part of the vehicle's part list, or a special operation.</summary>
public enum GlassAutomationPositionKind
{
    Part,
    SpecialOperation,
}

/// <summary>The operation a part is added with.</summary>
public enum GlassAutomationPartOperation
{
    /// <summary>A new part, as served (Replace).</summary>
    Part,

    /// <summary>A new part and its paint.</summary>
    PartAndPaint,

    /// <summary>Paint on its own.</summary>
    Paint,

    /// <summary>A repair, with whatever hours Glass's serves.</summary>
    Repair,

    /// <summary>A remove-and-refit, with whatever hours Glass's serves.</summary>
    RemoveAndRefit,
}

/// <summary>One part the editor's "Mandatory parts:" page listed, and whether Glass's ticked it itself.</summary>
public sealed record GlassAutomationListedPart(string Code, string Text, bool Ticked);

/// <summary>
/// One position act of the Automation's session: what was asked, what became
/// of it and what Glass's served. Only the figures Glass's own dialog served
/// are kept; none is ever typed (R1168).
/// </summary>
/// <param name="OperationKey">The caller's key: the same key answers this act and never sends twice.</param>
/// <param name="Action">Add or remove.</param>
/// <param name="Outcome">What became of it.</param>
/// <param name="RequestedAtUtc">When it was claimed.</param>
public sealed record GlassAutomationAct(
    string OperationKey,
    GlassAutomationActAction Action,
    GlassAutomationActOutcome Outcome,
    DateTimeOffset RequestedAtUtc)
{
    /// <summary>Why it was refused or not confirmed: a <c>glass.*</c> code; null otherwise.</summary>
    public string? Code { get; init; }

    /// <summary>An add's kind of position.</summary>
    public GlassAutomationPositionKind? Kind { get; init; }

    /// <summary>An add's row as the catalogue search answered it: a special operation's code, or a part's list row.</summary>
    public string? Row { get; init; }

    /// <summary>A part add's operation.</summary>
    public GlassAutomationPartOperation? Operation { get; init; }

    /// <summary>The side an add asked for, when it named one.</summary>
    public string? Side { get; init; }

    /// <summary>A remove's position id.</summary>
    public string? PositionId { get; init; }

    /// <summary>
    /// The code an add's position carries on the estimate (<c>A73</c>,
    /// <c>C2020001</c>), recorded before its post, by which a lost answer is
    /// settled. Null while the post has not begun.
    /// </summary>
    public string? ExpectedCode { get; init; }

    /// <summary>The positions that carried <see cref="ExpectedCode"/> just before the post.</summary>
    public IReadOnlyList<string> Before { get; init; } = [];

    /// <summary>The positions an add put on the estimate.</summary>
    public IReadOnlyList<string> PositionIds { get; init; } = [];

    /// <summary>The description Glass's dialog served.</summary>
    public string? Description { get; init; }

    /// <summary>The price and hours Glass's dialog served, a zero read as none.</summary>
    public GlassEstimateBuildFigures? Served { get; init; }

    /// <summary>The parts a "Mandatory parts:" page listed; only those Glass's ticked itself were confirmed.</summary>
    public IReadOnlyList<GlassAutomationListedPart> Listed { get; init; } = [];
}

/// <summary>
/// The Automation's position acts in its own Glass's session (design 07
/// section 3.4): which positions it may add, how an act is recorded by its
/// key, and how an act whose answer was lost is settled by the next read of
/// the estimate.
/// </summary>
public static class GlassAutomationPositionPolicy
{
    /// <summary>A row neither of the session's lists holds, or a position the estimate does not hold.</summary>
    public const string RowUnknownCode = "glass.row.unknown";

    /// <summary>A part row that serves both sides: the dialog's side tick box is not built.</summary>
    public const string BothSidesCode = "glass.row.both_sides";

    /// <summary>
    /// Whether an add of this kind and operation is sent at all: the same set
    /// a rebuild sends (R1172), special operations, and a part as Part,
    /// Repair or remove-and-refit, each posted as Glass's dialog serves it
    /// and only where its row is certain. Paint on its own and Part and
    /// Paint are refused with <see cref="GlassEstimateBuildPolicy.NotBuiltCode"/>
    /// and nothing is sent. One predicate, so the set is narrowed in one place.
    /// </summary>
    /// <remarks>Operator, 18 November 2026: R1182.</remarks>
    public static bool Sends(GlassAutomationPositionKind kind, GlassAutomationPartOperation? operation) =>
        kind == GlassAutomationPositionKind.SpecialOperation
        || operation is GlassAutomationPartOperation.Part
            or GlassAutomationPartOperation.Repair
            or GlassAutomationPartOperation.RemoveAndRefit;

    /// <summary>The act recorded under the key, or null.</summary>
    public static GlassAutomationAct? Find(GlassAutomationWork? work, string operationKey) =>
        work?.Acts.FirstOrDefault(act => string.Equals(act.OperationKey, operationKey, StringComparison.Ordinal));

    /// <summary>The work with the act recorded: in place of the act of the same key, or added last.</summary>
    public static GlassAutomationWork Record(GlassAutomationWork? work, GlassAutomationAct act)
    {
        ArgumentNullException.ThrowIfNull(act);
        var current = work ?? new GlassAutomationWork();
        var acts = current.Acts.ToList();
        var index = acts.FindIndex(item => string.Equals(item.OperationKey, act.OperationKey, StringComparison.Ordinal));
        if (index < 0)
        {
            acts.Add(act);
        }
        else
        {
            acts[index] = act;
        }

        return current with { Acts = acts };
    }

    /// <summary>
    /// The code a position id carries: its third part
    /// (<c>C350987.35.C2020001.4467</c> carries <c>C2020001</c>), or none.
    /// </summary>
    public static string? CodeOf(string positionId)
    {
        ArgumentNullException.ThrowIfNull(positionId);
        var parts = positionId.Split('.');
        return parts.Length > 2 ? parts[2] : null;
    }

    /// <summary>
    /// Every earlier act still open, settled by the estimate as now read:
    /// an add whose expected code is on a position that was not there before
    /// its post is Added with those positions, otherwise Refused with its
    /// code; a remove whose position is gone is Removed, otherwise Refused.
    /// An add claimed and interrupted before its post began is Refused. The
    /// act of <paramref name="exceptOperationKey"/>, the one now running, is
    /// left as it is.
    /// </summary>
    public static GlassAutomationWork? Settle(
        GlassAutomationWork? work, IReadOnlyList<string> positions, string? exceptOperationKey = null)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (work is null || !work.Acts.Any(act => Open(act, exceptOperationKey)))
        {
            return work;
        }

        var acts = work.Acts
            .Select(act => Open(act, exceptOperationKey) ? Settled(act, positions) : act)
            .ToList();
        return work with { Acts = acts };
    }

    private static bool Open(GlassAutomationAct act, string? exceptOperationKey) =>
        act.Outcome is GlassAutomationActOutcome.Pending or GlassAutomationActOutcome.NotConfirmed
        && !string.Equals(act.OperationKey, exceptOperationKey, StringComparison.Ordinal);

    private static GlassAutomationAct Settled(GlassAutomationAct act, IReadOnlyList<string> positions)
    {
        // An interrupted claim keeps its own code only when it has none.
        var code = act.Code ?? InterruptedCode;
        if (act.Action == GlassAutomationActAction.Remove)
        {
            return act.PositionId is { } removed && !positions.Contains(removed, StringComparer.Ordinal)
                ? act with { Outcome = GlassAutomationActOutcome.Removed, Code = null }
                : act with { Outcome = GlassAutomationActOutcome.Refused, Code = code };
        }

        if (act.ExpectedCode is not { } expected)
        {
            return act with { Outcome = GlassAutomationActOutcome.Refused, Code = code };
        }

        var added = positions
            .Where(id => string.Equals(CodeOf(id), expected, StringComparison.Ordinal)
                && !act.Before.Contains(id, StringComparer.Ordinal))
            .ToList();
        return added.Count > 0
            ? act with { Outcome = GlassAutomationActOutcome.Added, Code = null, PositionIds = added }
            : act with { Outcome = GlassAutomationActOutcome.Refused, Code = code };
    }

    /// <summary>The code of an act whose work stopped before it recorded an outcome.</summary>
    public const string InterruptedCode = "glass.interrupted";
}

/// <summary>
/// A position act of the Automation on its own session (design 07 section
/// 3.4): the session version it read and its operation key, and for an add
/// the kind, the row the catalogue search returned, a part's operation and
/// the side asked for; for a remove the position id.
/// </summary>
public sealed record GlassAutomationPositionRequest(
    ActionActor Actor, Guid SessionId, long ExpectedVersion, string OperationKey,
    GlassAutomationActAction Action)
{
    public GlassAutomationPositionKind? Kind { get; init; }

    public string? Row { get; init; }

    public GlassAutomationPartOperation? Operation { get; init; }

    public string? Side { get; init; }

    public string? PositionId { get; init; }
}

/// <summary>
/// A position act as its prepare half or its work left it: the session, the
/// act, and whether provider work is still owed to it.
/// </summary>
public sealed record GlassAutomationPositionStep(
    GlassRepairEstimateSession Session, GlassAutomationAct Act, bool Owed);

/// <summary>The work a claimed position act owes, run for the Automation by its key.</summary>
public sealed record GlassAutomationPositionContinueRequest(ActionActor Actor, Guid SessionId, string OperationKey);

/// <summary>
/// A catalogue search of the Automation's open estimate (design 07 section
/// 3.3): the term matched against a row's description, part code or part
/// number, the kind of row (null for any) and, for parts, the side.
/// </summary>
public sealed record GlassAutomationCatalogueQuery(
    ActionActor Actor, Guid SessionId, string Term, GlassAutomationPositionKind? Kind, string? Side);

/// <summary>
/// One row a catalogue search found: <see cref="Row"/> is the value
/// <c>pegasus_glass_position_act</c> adds it by (a special operation's code,
/// or a part's list row). <see cref="Side"/> is <c>L</c>, <c>R</c>,
/// <c>both</c> or null. No price or hours: the list carries none for parts,
/// and the add answers with the figures the dialog serves.
/// </summary>
public sealed record GlassAutomationCatalogueRow(
    string Row, GlassAutomationPositionKind Kind, string Description,
    string? PartCode, string? PartNumber, string? Side, bool Paintable);

/// <summary>
/// What a catalogue search read: the session as the read left it, every
/// row found in list order, and the <c>glass.*</c> code when a list was not
/// read (no rows are then answered).
/// </summary>
public sealed record GlassAutomationCatalogue(
    GlassRepairEstimateSession Session, IReadOnlyList<GlassAutomationCatalogueRow> Rows, string? Code);
