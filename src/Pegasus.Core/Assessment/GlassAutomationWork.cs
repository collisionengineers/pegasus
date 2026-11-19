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
/// What a position act gives Glass's, as a person would give it in the
/// editor (operator, 19 November 2026; R1206). Each member is null when it is
/// not given; later versions add members, each built as its own change.
/// </summary>
/// <param name="Equipment">
/// A part's answer to Glass's equipment question: the option codes ticked,
/// the whole answer, so an option not named is unticked; empty ticks none.
/// </param>
public sealed record GlassAutomationInputs(IReadOnlyList<string>? Equipment = null);

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

    /// <summary>What an add gave Glass's (R1206); null when it gave nothing.</summary>
    public GlassAutomationInputs? Inputs { get; init; }

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

    /// <summary>What an add gives Glass's (R1206): in this version a part's equipment answer, for a part added as Part.</summary>
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
