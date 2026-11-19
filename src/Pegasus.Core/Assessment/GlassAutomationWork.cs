using System.Globalization;
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

/// <summary>
/// Whether a position act adds a position, removes one or changes one
/// already on the estimate (R1206). A preview asks Glass's for the dialog an
/// add would post, or a position is shown by, and is never an act: it posts
/// nothing and is never recorded (R1206).
/// </summary>
public enum GlassAutomationActAction
{
    Add,
    Remove,
    Preview,
    Change,
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

    /// <summary>A change posted, the editor answered that it took it, and the estimate read afterwards still lists the position (R1206).</summary>
    Changed,

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

/// <summary>
/// One part the editor's "Mandatory parts:" page listed, and whether it was
/// ticked when the page was confirmed: by Glass's itself, or, for an optional
/// one, because the add's <see cref="GlassAutomationInputs.OptionalParts"/>
/// named it (R1206).
/// </summary>
public sealed record GlassAutomationListedPart(string Code, string Text, bool Ticked)
{
    /// <summary>Whether the page let the part be ticked or left: an optional part.</summary>
    public bool Optional { get; init; }
}

/// <summary>The button an add presses on the dialog of a part that can be painted (Glass's <c>comp.php</c>).</summary>
public enum GlassAutomationButton
{
    /// <summary>Part (<c>BSS</c>): the part, not painted.</summary>
    Part,

    /// <summary>Paint (<c>BLL</c>): the part's paint on its own.</summary>
    Paint,

    /// <summary>Part and Paint (<c>BSL</c>).</summary>
    PartAndPaint,
}

/// <summary>
/// What a position act gives Glass's, as a person would give it in the
/// editor (operator, 19 November 2026; R1206). Each member is null when it is
/// not given; later versions add members, each built as its own change.
/// </summary>
/// <param name="Equipment">
/// A part's answer to Glass's equipment question: the option codes ticked,
/// the whole answer, so an option not named is unticked; empty ticks none.
/// </param>
/// <param name="Operation">
/// The operation chosen in a part's dialog, by Glass's own code as its list
/// offers it (<c>I</c>, <c>AE</c>, <c>E</c>, <c>AN</c> …): given with the
/// act's operation Part only.
/// </param>
/// <param name="Button">The button pressed on a dialog of a part that can be painted: given with the act's operation Part only.</param>
/// <param name="BothSides">Ticks "Both" on a dialog that offers it; the other side's own dialog is then posted as served.</param>
/// <param name="OptionalParts">The optional parts to tick on the "Mandatory parts:" page when Glass's shows one, by its codes.</param>
/// <param name="ConfirmNoFigures">
/// Answers OK to the question Glass's dialog asks before it sends a part
/// with no price and no hours. Repair (<c>I</c>) is answered OK without it
/// (R1188); any other operation is refused without it.
/// </param>
/// <param name="Hours">
/// The labour hours typed in the dialog's Time box, in place of Glass's own
/// (19 November 2026, R1206). Sent as <see cref="GlassAutomationPositionPolicy.TypedText"/>
/// gives it. Above Glass's own hours the dialog asks for a reason, which
/// <c>Reason</c> gives; Pegasus never writes one.
/// </param>
/// <param name="Price">The price typed in the dialog's Price box, sent as <c>Hours</c> is.</param>
/// <param name="OverlapHours">The overlap hours typed in the dialog's overlap box (<c>Ctime</c>), sent as <c>Hours</c> is.</param>
/// <param name="Reason">
/// The Automation's own reason for hours above Glass's own, as a person
/// types it in Glass's reason window: given only with <c>Hours</c>,
/// and sent only when the dialog asks for it.
/// </param>
public sealed record GlassAutomationInputs(
    IReadOnlyList<string>? Equipment = null,
    string? Operation = null,
    GlassAutomationButton? Button = null,
    bool? BothSides = null,
    IReadOnlyList<string>? OptionalParts = null,
    bool? ConfirmNoFigures = null,
    decimal? Hours = null,
    decimal? Price = null,
    decimal? OverlapHours = null,
    string? Reason = null)
{
    /// <summary>Whether no member is given.</summary>
    public bool IsEmpty =>
        Equipment is null && Operation is null && Button is null && BothSides is null && OptionalParts is null
        && ConfirmNoFigures is null && !Typed;

    /// <summary>Whether a typed figure or a reason is given (R1206).</summary>
    public bool Typed => Hours is not null || Price is not null || OverlapHours is not null || Reason is not null;
}

/// <summary>
/// The figures of one dialog (R1206): the price, labour hours and overlap
/// hours, as Glass's served them or as the post sent them. A figure the
/// dialog does not give is null; a zero is kept.
/// </summary>
public sealed record GlassAutomationFigures(decimal? Price, decimal? Hours, decimal? OverlapHours);

/// <summary>One option of Glass's equipment question for a part, as Glass's own dialog shows it.</summary>
/// <param name="Code">Glass's code for the option: the value an answer names.</param>
/// <param name="Name">Glass's own name for it.</param>
/// <param name="Ticked">Whether Glass's holds it as present on the vehicle now, so the dialog shows it ticked.</param>
/// <param name="Greyed">Whether the dialog greys it out, so an answer may not name it.</param>
/// <param name="Group">The option's group when the options of a group exclude each other; null otherwise.</param>
/// <param name="Note">Glass's own note when another position uses the option, as its dialog shows it; null otherwise.</param>
public sealed record GlassEquipmentOption(string Code, string Name, bool Ticked, bool Greyed, string? Group, string? Note);

/// <summary>
/// One answer to a part's equipment question and the part it leads to, as
/// Glass's part list gives it: the codes ticked, the row pair the dialog is
/// asked for, and the part's number, price and Replace hours. A map entry
/// given instead of answers uses the same shape: the codes it needs and its
/// one row, with the figures of that row.
/// </summary>
public sealed record GlassEquipmentAnswer(
    IReadOnlyList<string> Ticked, string Rows, string? PartNumber, decimal? Price, decimal? Hours);

/// <summary>
/// The equipment question Glass's asks before it adds a part whose block in
/// its part list has variants (R1206): the options its dialog shows. Asked
/// for one row, it also holds every answer that leads to a part; where those
/// are too many, it holds instead the part rows and Replace hours rows of the
/// row's equipment map, each with the options it needs.
/// </summary>
public sealed record GlassEquipmentQuestion(IReadOnlyList<GlassEquipmentOption> Options)
{
    public IReadOnlyList<GlassEquipmentAnswer>? Answers { get; init; }

    public IReadOnlyList<GlassEquipmentAnswer>? MapParts { get; init; }

    public IReadOnlyList<GlassEquipmentAnswer>? MapHours { get; init; }
}

/// <summary>A value Glass's held for the vehicle that an answer changed: the option and its new tick.</summary>
public sealed record GlassEquipmentChange(string Code, bool Ticked);

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

    /// <summary>A remove's or a change's position id.</summary>
    public string? PositionId { get; init; }

    /// <summary>
    /// A change's position as Glass's record holds it once changed
    /// (<c>mcode.php</c>, R1206); null when the record was not read, or Glass's
    /// answered it empty.
    /// </summary>
    public GlassPositionFigures? Position { get; init; }

    /// <summary>
    /// The code an add's position carries on the estimate (<c>A73</c>,
    /// <c>C2020001</c>), recorded before its post, by which a lost answer is
    /// settled. Null while the post has not begun.
    /// </summary>
    public string? ExpectedCode { get; init; }

    /// <summary>
    /// With "Both" ticked (R1206): the code the other side's position
    /// carries, recorded before the other side's own dialog is posted, by
    /// which a lost answer is settled as <see cref="ExpectedCode"/> is.
    /// </summary>
    public string? OtherSideCode { get; init; }

    /// <summary>The positions that carried <see cref="ExpectedCode"/> or <see cref="OtherSideCode"/> just before each post.</summary>
    public IReadOnlyList<string> Before { get; init; } = [];

    /// <summary>The positions an add put on the estimate.</summary>
    public IReadOnlyList<string> PositionIds { get; init; } = [];

    /// <summary>The description Glass's dialog served.</summary>
    public string? Description { get; init; }

    /// <summary>The price and hours Glass's dialog served, a zero read as none.</summary>
    public GlassEstimateBuildFigures? Served { get; init; }

    /// <summary>The parts a "Mandatory parts:" page listed; only those Glass's ticked itself were confirmed.</summary>
    public IReadOnlyList<GlassAutomationListedPart> Listed { get; init; } = [];

    /// <summary>What an add gave Glass's (R1206); null when it gave nothing.</summary>
    public GlassAutomationInputs? Inputs { get; init; }

    /// <summary>
    /// The figures the dialog served for the operation and side it sent,
    /// before anything was typed (R1206); recorded just before the post, and
    /// null while no dialog has been built.
    /// </summary>
    public GlassAutomationFigures? DialogServed { get; init; }

    /// <summary>
    /// The figures the post sent: Glass's served ones with the Automation's
    /// typed ones in their place (R1206). Recorded just before the post, so a
    /// lost answer still says what was sent; null while no dialog has been built.
    /// </summary>
    public GlassAutomationFigures? Sent { get; init; }

    /// <summary>
    /// The part's equipment question, kept on an add refused on it: one that
    /// gave no answer, or an answer that is not allowed or leads to no part.
    /// </summary>
    public GlassEquipmentQuestion? Equipment { get; init; }

    /// <summary>The row pair a part's dialog was asked for, when an equipment answer chose it.</summary>
    public string? RowPair { get; init; }

    /// <summary>The values Glass's held for the vehicle that the add's equipment answer changed.</summary>
    public IReadOnlyList<GlassEquipmentChange> EquipmentChanged { get; init; } = [];

    /// <summary>
    /// Whether the add's equipment answer was sent to Glass's, which keeps it
    /// for the vehicle as it keeps a person's: recorded just before it is
    /// sent, so an add refused afterwards still says so.
    /// </summary>
    public bool EquipmentStored { get; init; }
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

    // The refusals of an equipment answer (R1206), each made before anything
    // is sent to Glass's. A part whose row asks and that is given no answer
    // is GlassEstimateBuildPolicy.EquipmentCode, as a rebuild's line is.

    /// <summary>An answer names a code that is not an option of the part's question.</summary>
    public const string EquipmentUnknownCode = "glass.build.equipment_unknown";

    /// <summary>An answer names an option Glass's dialog greys out.</summary>
    public const string EquipmentGreyedCode = "glass.build.equipment_greyed";

    /// <summary>An answer names one code twice.</summary>
    public const string EquipmentRepeatedCode = "glass.build.equipment_repeated";

    /// <summary>An answer names two options of one group, which exclude each other.</summary>
    public const string EquipmentSameGroupCode = "glass.build.equipment_same_group";

    /// <summary>An answer that leads Glass's own rule to no part, or to no Replace hours for it.</summary>
    public const string EquipmentNoPartCode = "glass.build.equipment_no_part";

    /// <summary>An answer given for a row whose part Glass's adds without asking any equipment question.</summary>
    public const string EquipmentNotAskedCode = "glass.build.equipment_not_asked";

    /// <summary>
    /// Whether an add may give an equipment answer: a part added as Part
    /// (Replace) only. Repair and remove-and-refit on a part whose row asks
    /// stay refused (R1172, R1206).
    /// </summary>
    public static bool TakesEquipment(GlassAutomationPositionKind? kind, GlassAutomationPartOperation? operation) =>
        kind == GlassAutomationPositionKind.Part && operation == GlassAutomationPartOperation.Part;

    /// <summary>
    /// Why an equipment answer is not one Glass's dialog would take, before
    /// anything is sent (R1206), or null: a code named twice, a code that is
    /// not an option or is greyed out, or two options of one group.
    /// </summary>
    public static string? EquipmentRefusal(GlassEquipmentQuestion question, IReadOnlyList<string> ticked)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(ticked);
        if (ticked.Distinct(StringComparer.Ordinal).Count() != ticked.Count)
        {
            return EquipmentRepeatedCode;
        }

        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in ticked)
        {
            var option = question.Options.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.Ordinal));
            if (option is null)
            {
                return EquipmentUnknownCode;
            }
            if (option.Greyed)
            {
                return EquipmentGreyedCode;
            }
        }
        foreach (var code in ticked)
        {
            var group = question.Options.First(item => string.Equals(item.Code, code, StringComparison.Ordinal)).Group;
            if (group is not null && !groups.Add(group))
            {
                return EquipmentSameGroupCode;
            }
        }

        return null;
    }

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
        if (act.Action == GlassAutomationActAction.Change)
        {
            // A change keeps its position's id, so the list cannot say whether it took: one whose
            // post may have begun stays not confirmed, and the position's own read settles it.
            return act.ExpectedCode is null
                ? act with { Outcome = GlassAutomationActOutcome.Refused, Code = code }
                : act with { Outcome = GlassAutomationActOutcome.NotConfirmed, Code = code };
        }
        if (act.Action == GlassAutomationActAction.Remove)
        {
            return act.PositionId is { } removed && !positions.Contains(removed, StringComparer.Ordinal)
                ? act with { Outcome = GlassAutomationActOutcome.Removed, Code = null }
                : act with { Outcome = GlassAutomationActOutcome.Refused, Code = code };
        }

        if (act.ExpectedCode is null)
        {
            return act with { Outcome = GlassAutomationActOutcome.Refused, Code = code };
        }

        var added = positions
            .Where(id => Carries(act, id) && !act.Before.Contains(id, StringComparer.Ordinal))
            .ToList();
        return added.Count > 0
            ? act with { Outcome = GlassAutomationActOutcome.Added, Code = null, PositionIds = added }
            : act with { Outcome = GlassAutomationActOutcome.Refused, Code = code };
    }

    /// <summary>The code of an act whose work stopped before it recorded an outcome.</summary>
    public const string InterruptedCode = "glass.interrupted";

    /// <summary>
    /// Whether a position carries the code an add expects: its own side's
    /// (<see cref="GlassAutomationAct.ExpectedCode"/>) or, with "Both"
    /// ticked, the other side's (<see cref="GlassAutomationAct.OtherSideCode"/>).
    /// </summary>
    public static bool Carries(GlassAutomationAct act, string positionId)
    {
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(positionId);
        var code = CodeOf(positionId);
        return code is not null
            && (string.Equals(code, act.ExpectedCode, StringComparison.Ordinal)
                || string.Equals(code, act.OtherSideCode, StringComparison.Ordinal));
    }

    /// <summary>A part's button or a dialog's choice was asked for a part that cannot be painted: its dialog has no Paint or Part and Paint button.</summary>
    public const string NotPaintableCode = "glass.row.not_paintable";

    /// <summary>
    /// "Both" was ticked and the first side was added, but the editor did not
    /// go on to the other side's own dialog as its list does, or that dialog
    /// was not the one expected: the other side was not added.
    /// </summary>
    public const string OtherSideNotAddedCode = "glass.row.other_side_not_added";

    /// <summary>
    /// The input of an add or a preview that its kind and operation do not
    /// take, by its tool name, or null when every input given is taken
    /// (R1206). A special operation takes none. A part takes
    /// <c>inputs.equipment</c> with the operation Part (R1172);
    /// <c>inputs.operation</c> and <c>inputs.button</c> with the operation
    /// Part, and an operation not with the Paint button, which sends paint
    /// alone; <c>inputs.side</c> ("Both") with the operation Part, the dialog's
    /// own operation and the Part button; <c>inputs.optionalParts</c> as
    /// codes, none blank or repeated. A remove takes none. Typed figures and
    /// a reason (R1206) are taken by a special operation and by a part
    /// pressed with Part, one side at a time, as <see cref="TypedRefused"/>
    /// allows them; a special operation takes nothing else.
    /// </summary>
    public static string? InputRefused(
        GlassAutomationActAction action, GlassAutomationPositionKind? kind, GlassAutomationPartOperation? operation,
        GlassAutomationInputs? inputs)
    {
        if (inputs is null || inputs.IsEmpty)
        {
            return null;
        }
        if (action == GlassAutomationActAction.Remove)
        {
            return FirstGiven(inputs);
        }
        if (TypedRefused(inputs) is { } typed)
        {
            return typed;
        }
        if (action == GlassAutomationActAction.Change
            || (action == GlassAutomationActAction.Preview && kind is null))
        {
            // A position already on the estimate: what its dialog lets a person change (R1206).
            return ChangeRefused(inputs);
        }
        if (kind != GlassAutomationPositionKind.Part)
        {
            // A special operation takes typed figures and a reason alone.
            return FirstGiven(inputs with { Hours = null, Price = null, OverlapHours = null, Reason = null });
        }
        if (inputs.Typed
            && (operation is GlassAutomationPartOperation.Paint or GlassAutomationPartOperation.PartAndPaint
                || inputs.Button is GlassAutomationButton.Paint or GlassAutomationButton.PartAndPaint
                || inputs.BothSides == true))
        {
            // Typed figures go on a dialog of a part that cannot be painted, one side at a time.
            return FirstTyped(inputs);
        }
        if (inputs.Equipment is { } equipment
            && (!TakesEquipment(kind, operation) || equipment.Any(string.IsNullOrWhiteSpace)))
        {
            return "inputs.equipment";
        }

        var asServed = operation == GlassAutomationPartOperation.Part;
        if (inputs.Operation is { } chosen
            && (!asServed || string.IsNullOrWhiteSpace(chosen) || inputs.Button == GlassAutomationButton.Paint))
        {
            return "inputs.operation";
        }
        if (inputs.Button is not null && !asServed)
        {
            return "inputs.button";
        }
        if (inputs.BothSides == true
            && (!asServed || inputs.Operation is not null || inputs.Button is GlassAutomationButton.Paint or GlassAutomationButton.PartAndPaint))
        {
            return "inputs.side";
        }
        if (inputs.OptionalParts is { } codes
            && (codes.Any(string.IsNullOrWhiteSpace) || codes.Distinct(StringComparer.Ordinal).Count() != codes.Count))
        {
            return "inputs.optionalParts";
        }

        return null;
    }

    private static string? FirstGiven(GlassAutomationInputs inputs) =>
        inputs.Equipment is not null ? "inputs.equipment"
        : inputs.Operation is not null ? "inputs.operation"
        : inputs.Button is not null ? "inputs.button"
        : inputs.BothSides is not null ? "inputs.side"
        : inputs.OptionalParts is not null ? "inputs.optionalParts"
        : inputs.ConfirmNoFigures is not null ? "inputs.confirmNoFigures"
        : FirstTyped(inputs);

    /// <summary>
    /// The input a change of a position already on the estimate does not
    /// take (R1206), or null: its equipment, its operation and Both stay as
    /// the position has them; optional parts are codes, none blank or repeated.
    /// </summary>
    private static string? ChangeRefused(GlassAutomationInputs inputs) =>
        inputs.Equipment is not null ? "inputs.equipment"
        : inputs.Operation is not null ? "inputs.operation"
        : inputs.BothSides is not null ? "inputs.side"
        : inputs.OptionalParts is { } codes
            && (codes.Any(string.IsNullOrWhiteSpace) || codes.Distinct(StringComparer.Ordinal).Count() != codes.Count)
            ? "inputs.optionalParts"
        : null;

    private static string? FirstTyped(GlassAutomationInputs inputs) =>
        inputs.Hours is not null ? "inputs.hours"
        : inputs.Price is not null ? "inputs.price"
        : inputs.OverlapHours is not null ? "inputs.overlapHours"
        : inputs.Reason is not null ? "inputs.reason"
        : null;

    /// <summary>
    /// A typed figure that is not one Pegasus sends, or a reason not given as
    /// one is (R1206), by its tool name: a negative figure, one with more than
    /// two decimal places, hours or overlap hours above
    /// <see cref="MaximumTypedHours"/>, a price above <see cref="MaximumTypedPrice"/>;
    /// a reason given without hours, blank, or longer than
    /// <see cref="MaximumReasonLength"/>. Null when none is.
    /// </summary>
    private static string? TypedRefused(GlassAutomationInputs inputs) =>
        inputs.Hours is { } hours && (TypedText(hours) is null || hours > MaximumTypedHours) ? "inputs.hours"
        : inputs.Price is { } price && (TypedText(price) is null || price > MaximumTypedPrice) ? "inputs.price"
        : inputs.OverlapHours is { } overlap && (TypedText(overlap) is null || overlap > MaximumTypedHours) ? "inputs.overlapHours"
        : inputs.Reason is { } reason
            && (inputs.Hours is null || reason.Trim(' ').Length == 0 || reason.Length > MaximumReasonLength)
            ? "inputs.reason"
        : null;

    /// <summary>
    /// A figure as the Automation types it in a dialog (R1206): two decimal
    /// places after a full stop and no thousands separator, as Glass's serves
    /// its own figures (<c>2.80</c>, <c>1234.00</c>). The dialog posts the text
    /// typed unchanged. Null for a negative figure or one with more than two
    /// decimal places, which is refused.
    /// </summary>
    public static string? TypedText(decimal value) =>
        value < 0 || decimal.Round(value, 2) != value ? null : value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// The most hours or overlap hours typed: the return's reader refuses a
    /// whole export with a time above 1,000 (<c>EstimatePolicy.MaximumLineWorkUnits</c>).
    /// </summary>
    public const decimal MaximumTypedHours = 1000m;

    /// <summary>The highest price typed: nine characters, the length of the dialog's price box.</summary>
    public const decimal MaximumTypedPrice = 999999.99m;

    /// <summary>
    /// The longest reason. Glass's keeps it on the position and its export
    /// carries it into the line's note, which the return's reader refuses
    /// beyond 500 characters with the line's other notes; 150 leaves them room.
    /// </summary>
    public const int MaximumReasonLength = 150;

    /// <summary>Typed hours are above Glass's own and the dialog asks for a reason the act did not give: nothing is posted (R1206).</summary>
    public const string ReasonRequiredCode = "glass.build.reason_required";

    /// <summary>A reason was given, but the dialog asks for none: the hours sent are not above Glass's own. Nothing is posted.</summary>
    public const string ReasonNotAskedCode = "glass.build.reason_not_asked";

    /// <summary>
    /// The overlap hours sent are more than the hours sent. The return's reader
    /// refuses a whole export with such a position, so nothing is posted.
    /// </summary>
    public const string OverlapAboveHoursCode = "glass.build.overlap_above_hours";

    /// <summary>The inputs as an act keeps them: null when none is given.</summary>
    public static GlassAutomationInputs? Kept(GlassAutomationInputs? inputs) =>
        inputs is null || inputs.IsEmpty ? null : inputs;

    /// <summary>
    /// The act a request asks for, Pending and not yet recorded, or an
    /// <see cref="ArgumentException"/> when the request is not one the tools
    /// make: a remove names its position; an add or a preview of a new
    /// position names its kind and row, and an operation for a part and only
    /// for a part; a preview of a position on the estimate names that
    /// position alone, or with the inputs a change would give; a change names
    /// its position and the inputs it gives (R1206); and no input is given
    /// that the kind and operation do not take (<see cref="InputRefused"/>).
    /// </summary>
    public static GlassAutomationAct Draft(GlassAutomationPositionRequest request, DateTimeOffset requestedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        var act = new GlassAutomationAct(
            request.OperationKey.Trim(), request.Action, GlassAutomationActOutcome.Pending, requestedAtUtc);
        if (InputRefused(request.Action, request.Kind, request.Operation, request.Inputs) is { } refused)
        {
            throw new ArgumentException("The act gives " + refused + ", which it does not take.", nameof(request));
        }
        if (request.Action == GlassAutomationActAction.Change)
        {
            // A change names its position and what it changes (R1206).
            if (string.IsNullOrWhiteSpace(request.PositionId) || request.Kind is not null || request.Row is not null
                || request.Operation is not null || request.Inputs is null || request.Inputs.IsEmpty)
            {
                throw new ArgumentException("A change names its position and what it changes, and nothing else.", nameof(request));
            }

            return act with { PositionId = request.PositionId.Trim(), Inputs = Kept(request.Inputs) };
        }
        if (request.Action == GlassAutomationActAction.Remove
            || (request.Action == GlassAutomationActAction.Preview && request.Kind is null))
        {
            if (string.IsNullOrWhiteSpace(request.PositionId)
                || (request.Action == GlassAutomationActAction.Remove && request.Inputs is not null && !request.Inputs.IsEmpty))
            {
                throw new ArgumentException("A remove names its position alone, and a preview of a position its position and any change.", nameof(request));
            }

            // A preview of a position may give what a change would (R1206).
            return act with { PositionId = request.PositionId.Trim(), Inputs = Kept(request.Inputs) };
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
            Inputs = Kept(request.Inputs),
        };

        // An add of an operation the return cannot read back is refused
        // before anything is asked of Glass's; a preview still shows its dialog.
        return request.Action == GlassAutomationActAction.Add && OperationRefusal(act.Inputs?.Operation) is { } unread
            ? act with { Outcome = GlassAutomationActOutcome.Refused, Code = unread }
            : act;
    }

    /// <summary>
    /// The operation chosen in a part's dialog names a position the return
    /// cannot read back: the add is refused before anything is sent (R1206,
    /// R1208; issue 2155).
    /// </summary>
    public const string OperationNotReadBackCode = "glass.build.operation_not_read_back";

    /// <summary>
    /// The operations the Automation may choose in a part's dialog, by Glass's
    /// own code: those whose position comes back in Glass's export with a
    /// repair kind the return's reader reads
    /// (<c>GlassEstimateXmlParser.Operation</c>; change the two together).
    /// ER, I and AE are Replace, Repair and Uninstall and install. AD
    /// ("S (Sealing)") and P ("CA (Control and adjustment)") are Sealing and
    /// Control: the MP23KTV sheet of 5 September 2026 prints S beside the
    /// position its export calls Sealing and CA beside the one it calls
    /// Control, and lists "S Sealing" and "CA Control and adjustment". Every
    /// other operation (AN Renew, ET, HO, IT, KM, KS, UN, V, Z, E, H and any
    /// not judged) would make the Engineer's Save &amp; Exit fail to import, or
    /// is not shown to come back as a kind the reader reads; which line each
    /// becomes is the operator's decision under issue 2155.
    /// </summary>
    public static IReadOnlySet<string> ReadBackOperations { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "ER", "I", "AE", "AD", "P" };

    /// <summary>
    /// <see cref="OperationNotReadBackCode"/> when the operation chosen is not
    /// one of <see cref="ReadBackOperations"/>; null when none is chosen or
    /// the return reads it back.
    /// </summary>
    public static string? OperationRefusal(string? operation) =>
        operation is null || ReadBackOperations.Contains(operation.Trim()) ? null : OperationNotReadBackCode;
}

/// <summary>
/// A position act of the Automation on its own session (design 07 section
/// 3.4): the session version it read and its operation key, and for an add
/// the kind, the row the catalogue search returned, a part's operation and
/// the side asked for; for a remove the position id; for a change (R1206)
/// the position id and the inputs. A preview (R1206) names a new position
/// as an add does, or a position on the estimate by its id with any change,
/// and is never recorded.
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

    /// <summary>What an add or a preview gives Glass's dialog (R1206), as <see cref="GlassAutomationPositionPolicy.InputRefused"/> allows.</summary>
    public GlassAutomationInputs? Inputs { get; init; }
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
/// number, the kind of row (null for any) and, for parts, the side. With
/// <see cref="Row"/> it answers that one part row instead, with every answer
/// to its equipment question that leads to a part (R1206), and the term is
/// not read.
/// </summary>
public sealed record GlassAutomationCatalogueQuery(
    ActionActor Actor, Guid SessionId, string Term, GlassAutomationPositionKind? Kind, string? Side)
{
    public string? Row { get; init; }
}

/// <summary>
/// One row a catalogue search found: <see cref="Row"/> is the value
/// <c>pegasus_glass_position_act</c> adds it by (a special operation's code,
/// or a part's list row). <see cref="Side"/> is <c>L</c>, <c>R</c>,
/// <c>both</c> or null. No price or hours: the list carries none for parts,
/// and the add answers with the figures the dialog serves.
/// <see cref="Equipment"/> is the equipment question Glass's asks before it
/// adds the part, present only when its block asks one (R1206).
/// </summary>
public sealed record GlassAutomationCatalogueRow(
    string Row, GlassAutomationPositionKind Kind, string Description,
    string? PartCode, string? PartNumber, string? Side, bool Paintable)
{
    public GlassEquipmentQuestion? Equipment { get; init; }
}

/// <summary>
/// What a catalogue search read: the session as the read left it, every
/// row found in list order, and the <c>glass.*</c> code when a list was not
/// read (no rows are then answered).
/// </summary>
public sealed record GlassAutomationCatalogue(
    GlassRepairEstimateSession Session, IReadOnlyList<GlassAutomationCatalogueRow> Rows, string? Code);

/// <summary>What the Automation reads of an open Glass's estimate (R1206).</summary>
public enum GlassAutomationReadKind
{
    /// <summary>The estimate's positions and Glass's running totals, from one <c>selected.php</c>.</summary>
    Positions,

    /// <summary>One position's figures, from one <c>mcode.php</c>.</summary>
    Position,

    /// <summary>Glass's calculation, from one <c>report.php?calcid=0</c>.</summary>
    Calculation,
}

/// <summary>
/// One read of an open Glass's estimate (R1206): its kind and, for
/// <see cref="GlassAutomationReadKind.Position"/>, the position's id as
/// Glass's lists it (such as <c>C350898.20.C1060000.2464</c>). Reading
/// changes nothing at Glass's.
/// </summary>
public sealed record GlassAutomationRead(GlassAutomationReadKind Kind, string? PositionId = null)
{
    /// <summary>The longest position id read; Glass's own are a few dozen characters.</summary>
    public const int MaximumPositionIdLength = 100;

    public static GlassAutomationRead Positions { get; } = new(GlassAutomationReadKind.Positions);

    public static GlassAutomationRead Calculation { get; } = new(GlassAutomationReadKind.Calculation);

    public static GlassAutomationRead Position(string positionId) => new(GlassAutomationReadKind.Position, positionId);

    /// <summary>
    /// Whether the text can be a position id as Glass's lists them: letters,
    /// digits and dots, nothing else, so it goes into Glass's address as it is.
    /// </summary>
    public static bool IsPositionId(string? text) =>
        !string.IsNullOrEmpty(text)
        && text.Length <= MaximumPositionIdLength
        && text.All(character => char.IsAsciiLetterOrDigit(character) || character == '.');
}

/// <summary>
/// One of Glass's running totals as the estimate's list holds them
/// (<c>selected.php</c>, <c>CalcToolTipTexts</c> and <c>CalcToolTipNums</c>):
/// Glass's own label, such as <c>Parts</c>, <c>Paint material</c>,
/// <c>Body</c>, <c>Total (£)</c>, <c>VAT (20.00 %)</c> or <c>Amount due (£)</c>,
/// and its amount. Glass's lists only the totals the estimate has.
/// </summary>
public sealed record GlassEstimateTotal(string Label, decimal Amount);

/// <summary>
/// One position's figures as Glass's own record of it holds them
/// (<c>mcode.php</c>): its part code, description, operation (Glass's code,
/// such as <c>ER</c>), and the price, hours and part number for that
/// operation and its side (<c>L</c> or <c>R</c>, or none). A paint position
/// on its own (an id beginning <c>L</c>) has no operation, price or hours of
/// its own, only <see cref="Paint"/>. A figure the record does not give is
/// null, never zero.
/// </summary>
public sealed record GlassPositionFigures(
    string PositionId,
    string? PartCode,
    string? Description,
    string? Operation,
    decimal? Price,
    decimal? Hours,
    string? Side,
    string? PartNumber,
    GlassPositionPaint? Paint);

/// <summary>
/// The paint of a position as Glass's record holds it: the paint position's
/// id, its description, and its price and labour time, the figures the
/// part's dialog shows under "Price" and "Labour time" in its paint section.
/// </summary>
public sealed record GlassPositionPaint(string PositionId, string? Description, decimal? Price, decimal? LabourTime);

/// <summary>
/// One trade's line of Glass's calculation summary (<c>report.php?calcid=0</c>,
/// the table headed "Summary"): Glass's own name for the trade (such as
/// <c>Body</c>, <c>Mechanical</c>, <c>Auxiliary work</c>, <c>Paint</c>) and
/// its labour rate, labour time, labour costs and material costs.
/// </summary>
public sealed record GlassCalculationTrade(
    string Trade, decimal? LabourRate, decimal? LabourTime, decimal? LabourCosts, decimal? MaterialCosts);

/// <summary>
/// Glass's calculation as its summary states it: each trade's line, then
/// "Total Labour" (time and costs), "Total Material", "Repair costs excl.
/// VAT", the VAT line with Glass's own label (such as <c>VAT (20.00 %)</c>)
/// and "Repair costs incl. VAT". A figure the page does not give is null.
/// </summary>
public sealed record GlassCalculation(
    IReadOnlyList<GlassCalculationTrade> Trades,
    decimal? TotalLabourTime,
    decimal? TotalLabourCosts,
    decimal? TotalMaterial,
    decimal? RepairCostsExclVat,
    string? VatLabel,
    decimal? Vat,
    decimal? RepairCostsInclVat);

/// <summary>
/// The dialog a new position would be added by, or a position on the
/// estimate is shown by, as a person sees it, answered as a form (R1206). A
/// preview asks Glass's for the dialog and posts nothing: pressing Cancel in
/// the editor sends no request. Glass's own labels and option texts are kept
/// as Glass's serves them.
/// </summary>
/// <param name="Kind">
/// <c>part_paintable</c> (Glass's <c>comp.php</c>, with a paint section and
/// the Part, Paint and Part and Paint buttons), <c>part</c> (<c>part.php</c>)
/// or <c>special_operation</c> (<c>part.php</c> for a special operation).
/// </param>
/// <param name="Description">The part's or special operation's description, as served.</param>
/// <param name="PartNumber">The part number for the served side, as served.</param>
/// <param name="PartCode">The part code for the served side.</param>
/// <param name="Side"><c>L</c>, <c>R</c>, or null for none.</param>
/// <param name="Operations">Each operation the dialog's list offers, in its order.</param>
/// <param name="Fields">Each field a person sees, in the form's order.</param>
/// <param name="Paint">The paint section as served; null where the dialog has none.</param>
/// <param name="Buttons">Each button the dialog offers, by its name and Glass's own text.</param>
public sealed record GlassDialogPreview(
    string Kind,
    string? Description,
    string? PartNumber,
    string? PartCode,
    string? Side,
    IReadOnlyList<GlassDialogOperation> Operations,
    IReadOnlyList<GlassDialogField> Fields,
    GlassDialogPaint? Paint,
    IReadOnlyList<GlassDialogChoice> Buttons)
{
    /// <summary>For the inputs a preview gave: what an add with them would send, and what the dialog would ask.</summary>
    public GlassDialogSend? Sends { get; init; }
}

/// <summary>
/// One operation a dialog's list offers: Glass's code (the value
/// <c>inputs.operation</c> names), Glass's own text, the price and hours the
/// dialog serves for it on the served side and Glass's own hours for it, a
/// figure the dialog does not give being null; whether the dialog serves it
/// selected, and whether Pegasus may choose it.
/// </summary>
public sealed record GlassDialogOperation(
    string Code, string Text, decimal? Price, decimal? Hours, decimal? GlassHours, bool Served, bool Changeable)
{
    /// <summary>
    /// The <c>glass.*</c> code an add choosing it is refused with although
    /// the dialog offers it: <see cref="GlassAutomationPositionPolicy.OperationNotReadBackCode"/>
    /// for one the return cannot read back; null otherwise.
    /// </summary>
    public string? RefusedCode { get; init; }
}

/// <summary>A value of a dialog's list or a button: the value Glass's posts and Glass's own text for it.</summary>
public sealed record GlassDialogChoice(string Value, string Text);

/// <summary>
/// One field of a dialog a person sees: its name in Glass's form, Glass's own
/// label beside it where the dialog has one, its served value (a tick box's
/// value is whether it is ticked), its choices where it is a list, whether
/// the dialog serves it greyed out, and whether Pegasus may change it.
/// </summary>
public sealed record GlassDialogField(
    string Name, string? Label, string? Value, IReadOnlyList<GlassDialogChoice>? Choices, bool Disabled, bool Changeable)
{
    /// <summary>The member of the act's inputs that changes the field (R1206), such as <c>inputs.hours</c>; null for one Pegasus does not change.</summary>
    public string? Input { get; init; }

    /// <summary>
    /// The labour hours box: Glass's own hours for the served operation,
    /// above which the dialog asks for a reason (R1206); null where it asks
    /// none, because Glass's has no hours of its own for the operation.
    /// </summary>
    public decimal? ReasonAbove { get; init; }
}

/// <summary>
/// The paint section of a part's dialog as served: the paint area, the paint
/// type (coats) and the paint level, each with Glass's own text, and the
/// paint price and labour time.
/// </summary>
public sealed record GlassDialogPaint(
    GlassDialogChoice? Area, GlassDialogChoice? Coats, GlassDialogChoice? Level, decimal? Price, decimal? Hours);

/// <summary>
/// What an add with a preview's inputs would send, and each question the
/// dialog would ask: the button, the operation, the price and hours, and on a
/// part that can be painted the paint level, price and labour time, as the
/// dialog's own script sets them for that operation; whether "Both" is
/// ticked; and <see cref="Code"/>, the <c>glass.*</c> code the add would be
/// refused with before anything is posted, or null.
/// </summary>
public sealed record GlassDialogSend(
    string? Button,
    string? Operation,
    decimal? Price,
    decimal? Hours,
    string? PaintLevel,
    decimal? PaintPrice,
    decimal? PaintHours,
    bool BothSides,
    IReadOnlyList<GlassDialogQuestion> Questions,
    string? Code)
{
    /// <summary>The overlap hours the post would send (R1206); null where the dialog has none.</summary>
    public decimal? OverlapHours { get; init; }
}

/// <summary>
/// A question the dialog or the editor would ask on the way: <c>no_figures</c>
/// (price and hours are both zero; <see cref="Text"/> is Glass's own),
/// <c>reason</c> (more hours than Glass's own; answered by <c>inputs.reason</c>),
/// <c>other_side</c> ("Both": the editor then opens the other side's own
/// dialog, posted as served) or <c>mandatory_parts</c> (Glass's may list
/// mandatory parts after the post; which, if any, cannot be known before it).
/// <see cref="Answered"/> says whether the inputs given answer it.
/// </summary>
public sealed record GlassDialogQuestion(string Kind, string? Text, bool Answered);

/// <summary>
/// What a preview read (R1206): the session as the read left it, the dialog
/// as a form, and the <c>glass.*</c> code when no dialog was read: a row or
/// position Glass's lists do not hold, a row that is not certain, or a page
/// Glass's did not answer. Nothing was posted either way.
/// </summary>
public sealed record GlassAutomationPreview(GlassRepairEstimateSession Session, GlassDialogPreview? Dialog, string? Code);
