namespace Pegasus.Core.Assessment;

/// <summary>What a build did, or does not do, with one line of the outside estimate.</summary>
public enum GlassEstimateBuildOutcome
{
    /// <summary>An act to try that has not been tried yet.</summary>
    Pending,

    /// <summary>Posted, and the editor answered that it took it.</summary>
    Added,

    /// <summary>
    /// Posted, and the answer was lost or was not the one expected: the
    /// position may or may not be on the estimate, and Glass's export decides
    /// what the repair spec holds.
    /// </summary>
    NotConfirmed,

    /// <summary>Not on the built estimate; the reason code says why.</summary>
    LeftOff,

    /// <summary>Not sent: Glass's adds it itself.</summary>
    AddedByGlass,
}

/// <summary>
/// The kind of position a line of the outside estimate asks Glass's for, one
/// per row of the build's table (operator, 18 November 2026; R1172).
/// </summary>
public enum GlassEstimateBuildKind
{
    /// <summary>A Specialist line: a special operation, chosen by its exact name.</summary>
    SpecialOperation,

    /// <summary>A new part.</summary>
    Part,

    /// <summary>A paint line on its own.</summary>
    Paint,

    /// <summary>A repair line.</summary>
    Repair,

    /// <summary>A remove-and-refit line.</summary>
    RemoveAndRefit,

    /// <summary>An Other line that is not an included operation.</summary>
    Operation,

    /// <summary>An operation the source says is included in another.</summary>
    IncludedOperation,

    /// <summary>Paint preparation.</summary>
    PaintPreparation,
}

/// <summary>What an act's <see cref="GlassEstimateBuildAct.Key"/> is, and so how Glass's is searched by it.</summary>
public enum GlassEstimateBuildKeyKind
{
    /// <summary>No key: the line names nothing Glass's could be searched by.</summary>
    None,

    /// <summary>A special operation's exact name.</summary>
    Name,

    /// <summary>Glass's own part code (a Glass's export's <c>MCode</c>), searched with the line's side.</summary>
    PartCode,

    /// <summary>A manufacturer's part number (an Audatex sheet's).</summary>
    PartNumber,
}

/// <summary>A price and hours, either as the outside estimate gave them or as Glass's served them.</summary>
public sealed record GlassEstimateBuildFigures(decimal? Price, decimal? Hours);

/// <summary>
/// One line of the outside estimate and what the build does with it. No
/// figure is ever typed into Glass's (operator, 18 November 2026; R1168): a
/// position is posted as its dialog serves it, and <see cref="Served"/> keeps
/// what the dialog served beside <see cref="Source"/>, so the list after the
/// build can say where Glass's differs.
/// </summary>
/// <param name="SourcePositions">The positions of the outside estimate's lines this act stands for.</param>
/// <param name="Kind">The kind of position, by the line's type.</param>
/// <param name="Description">The outside line's description, as the list shows it.</param>
/// <param name="Key">What Glass's is searched by: a special operation's name, a part's code or number; null when the line has none.</param>
/// <param name="Side">The side a trailing "(L)" or "(R)" names, or null.</param>
/// <param name="Sends">Whether the plan has the build try it.</param>
/// <param name="Outcome">What became of it.</param>
/// <param name="ReasonCode">Why it was left off or not confirmed; null otherwise.</param>
/// <param name="Source">The outside estimate's own figures.</param>
/// <param name="Served">The figures Glass's dialog served, once it was posted.</param>
/// <param name="KeyKind">What <paramref name="Key"/> is: a name, Glass's part code or a part number.</param>
public sealed record GlassEstimateBuildAct(
    IReadOnlyList<int> SourcePositions,
    GlassEstimateBuildKind Kind,
    string Description,
    string? Key,
    string? Side,
    bool Sends,
    GlassEstimateBuildOutcome Outcome,
    string? ReasonCode,
    GlassEstimateBuildFigures Source,
    GlassEstimateBuildFigures? Served = null,
    GlassEstimateBuildKeyKind KeyKind = GlassEstimateBuildKeyKind.None);

/// <summary>
/// A repair spec made outside Pegasus, rebuilt in Glass's: the acts its lines
/// became, the Glass's repair spec the build's return landed as, and, when the
/// build stopped before anything was saved and the member of staff was given
/// an empty estimate instead, the code it stopped at.
/// </summary>
public sealed record GlassEstimateBuild(
    Guid SourceSpecificationId,
    IReadOnlyList<GlassEstimateBuildAct> Acts,
    Guid? BuiltSpecificationId = null,
    string? StoppedAt = null)
{
    /// <summary>How many acts the build tries.</summary>
    public int Total => Acts.Count(act => act.Sends);

    /// <summary>How many of them it has tried.</summary>
    public int Done => Acts.Count(act => act.Sends && act.Outcome != GlassEstimateBuildOutcome.Pending);

    /// <summary>Whether anything may be on the estimate: an act was added, or posted and not confirmed.</summary>
    public bool Placed => Acts.Any(act =>
        act.Outcome is GlassEstimateBuildOutcome.Added or GlassEstimateBuildOutcome.NotConfirmed);

    /// <summary>Whether the build is still to run or running: nothing has stopped it and nothing has landed.</summary>
    public bool Running => StoppedAt is null && BuiltSpecificationId is null;
}

/// <summary>One row of Glass's special-operation list: its code and its name.</summary>
public sealed record GlassSpecialOperation(string Code, string Name);

/// <summary>
/// How a repair spec made outside Pegasus is rebuilt in Glass's (FRD-25,
/// operator, 18 November 2026). Every line of the spec becomes one act, in
/// the spec's order. A line Glass's has no row for is left off and listed
/// (R1169); a rebuilt line carries Glass's own figures (R1168). This version
/// builds special operations, new parts, and repair and remove-and-refit
/// lines (R1172): a part's line only where its row needs no equipment
/// answer, which the build decides from Glass's own part list, and a repair
/// or remove-and-refit line with whatever hours Glass's serves for it. Every
/// other line is left off as not built in this version, and later versions
/// change only the outcome a line type gets.
/// </summary>
public static class GlassEstimateBuildPolicy
{
    /// <summary>A line of a kind this version does not build.</summary>
    public const string NotBuiltCode = "glass.build.not_built";

    /// <summary>Glass's has no row for the line.</summary>
    public const string NoRowCode = "glass.build.no_row";

    /// <summary>Glass's has more than one row the line could be.</summary>
    public const string AmbiguousRowCode = "glass.build.ambiguous_row";

    /// <summary>
    /// A new part with nothing to find it by in Glass's: no Glass's part code
    /// (a Glass's export) and no part number (an Audatex sheet).
    /// </summary>
    public const string NoKeyCode = "glass.build.no_key";

    /// <summary>
    /// A part whose row in Glass's waits on an equipment answer Pegasus does
    /// not have (R1172): Glass's would ask which variant of the vehicle it is
    /// before it gives the row. For the Automation's add of a part as Part,
    /// the answer is the Automation's to give (R1206): an add that gives
    /// none is refused with this code and the question.
    /// </summary>
    public const string EquipmentCode = "glass.build.equipment";

    /// <summary>
    /// The note a Glass's PDF's line starts with when its printed operation
    /// is PR, Partial repair: a <c>repair</c> line of another operation
    /// than Repair, which this version does not build.
    /// </summary>
    public const string PartialRepairNote = "Printed operation: PR.";

    /// <summary>Not sent: an earlier line was posted and its answer was not confirmed, so the build stopped adding.</summary>
    public const string StoppedCode = "glass.build.stopped";

    /// <summary>Nothing was placed, so nothing was saved and the member of staff was given an empty estimate.</summary>
    public const string NothingPlacedCode = "glass.build.nothing_placed";

    /// <summary>
    /// A build a restart stopped part-way: the session is continued as a
    /// launch, and what the estimate it reopens holds is not known.
    /// </summary>
    public const string InterruptedCode = "glass.interrupted";

    /// <summary>
    /// Whether the session's build stopped before anything was saved, because
    /// Glass's editor could not be opened or nothing could be placed, and the
    /// session then opened the empty estimate a launch gives: it is open, or
    /// a return from it has been accepted since. The Case's Glass's line says
    /// so to the member of staff who ran it (operator, 18 November 2026;
    /// R1186). A build a restart interrupted is not one, as what its reopened
    /// estimate holds is not known, and nor is one whose estimate never opened.
    /// </summary>
    public static bool OpenedEmpty(GlassRepairEstimateSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Build is { BuiltSpecificationId: null, StoppedAt: { } code }
            && !string.Equals(code, InterruptedCode, StringComparison.Ordinal)
            && (session.State == GlassRepairEstimateSessionState.Active || session.CallbackConsumedAtUtc is not null);
    }

    /// <summary>
    /// The build of a spec, or null when the spec has no lines and there is
    /// nothing to build. Whether the spec belongs to a Glass's estimate is the
    /// caller's to know: one that does reopens that estimate instead.
    /// </summary>
    public static GlassEstimateBuild? Plan(RepairSpecificationVersion specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (specification.Lines.Count == 0)
        {
            return null;
        }

        var route = specification.Source.Route;
        var acts = specification.Lines
            .OrderBy(line => line.Position)
            .Select(line => ActOf(route, line))
            .ToList();
        var build = new GlassEstimateBuild(specification.SpecificationId, acts);
        // A spec with nothing to send is not taken into Glass's editor at all:
        // the member of staff gets the empty estimate they would get anyway.
        return build.Total == 0 ? build with { StoppedAt = NothingPlacedCode } : build;
    }

    /// <summary>
    /// The special operation a line names: the one row of the list whose name
    /// is exactly the line's description. None, or more than one, is a reason
    /// the line is left off.
    /// </summary>
    public static (GlassSpecialOperation? Row, string? ReasonCode) ChooseSpecialOperation(
        IEnumerable<GlassSpecialOperation> list, string? name)
    {
        ArgumentNullException.ThrowIfNull(list);
        var wanted = name?.Trim();
        if (string.IsNullOrEmpty(wanted))
        {
            return (null, NoRowCode);
        }

        var matches = list
            .Where(row => string.Equals(row.Name.Trim(), wanted, StringComparison.Ordinal))
            .Take(2)
            .ToList();
        return matches.Count switch
        {
            0 => (null, NoRowCode),
            1 => (matches[0], null),
            _ => (null, AmbiguousRowCode),
        };
    }

    /// <summary>The build with one act replaced by what became of it.</summary>
    public static GlassEstimateBuild Record(GlassEstimateBuild build, int index, GlassEstimateBuildAct act)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(act);
        var acts = build.Acts.ToList();
        acts[index] = act;
        return build with { Acts = acts };
    }

    /// <summary>
    /// Every act not yet tried, left off with the reason the build stopped
    /// trying: what a build does once a post's answer is lost.
    /// </summary>
    public static GlassEstimateBuild LeaveOffPending(GlassEstimateBuild build, string reasonCode)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return build with
        {
            Acts = build.Acts
                .Select(act => act.Outcome == GlassEstimateBuildOutcome.Pending
                    ? act with { Outcome = GlassEstimateBuildOutcome.LeftOff, ReasonCode = reasonCode }
                    : act)
                .ToList(),
        };
    }

    /// <summary>Whether Glass's served a price other than the outside estimate's (R1168).</summary>
    public static bool PriceDiffers(GlassEstimateBuildAct act)
    {
        ArgumentNullException.ThrowIfNull(act);
        return act.Served is { } served && served.Price != act.Source.Price;
    }

    /// <summary>Whether Glass's served hours other than the outside estimate's (R1168).</summary>
    public static bool HoursDiffers(GlassEstimateBuildAct act)
    {
        ArgumentNullException.ThrowIfNull(act);
        return act.Served is { } served && served.Hours != act.Source.Hours;
    }

    /// <summary>
    /// One line's act, by its type and the route the spec came from. A
    /// Specialist line is tried by its name, and a new part by Glass's part
    /// code and side (a Glass's export) or by its part number (an Audatex
    /// sheet); a repair or remove-and-refit line of a Glass's export is tried
    /// by Glass's part code and side, and on any other route it has no
    /// Glass's part code to be tried by; an included operation and paint
    /// preparation are Glass's own to add; every other line waits for a
    /// later version. A paint line is left off even beside its part's line
    /// (R1172): a Glass's export's paint row carries a paint code (1640 for a
    /// bonnet), not its part's code, so nothing on the spec ties the two
    /// (export of 5 October 2026). A Glass's PDF writes both Repair (R) and
    /// Partial repair (PR) as a repair line; a partial repair is left off,
    /// as it is not the Repair the build would send.
    /// </summary>
    private static GlassEstimateBuildAct ActOf(RepairSpecificationSourceRoute route, CaseEstimateLineRecord line)
    {
        var description = line.Description?.Trim() ?? string.Empty;
        // A zero is no figure, as Glass's serves none for a dialog without one.
        var figures = new GlassEstimateBuildFigures(NonZero(line.Price), NonZero(line.WorkUnits));
        (GlassEstimateBuildKind Kind, string? Key, GlassEstimateBuildKeyKind KeyKind) position = line.Type switch
        {
            "specialist_fixed" or "specialist_wu" =>
                (GlassEstimateBuildKind.SpecialOperation, description, GlassEstimateBuildKeyKind.Name),
            "new_part" when route == RepairSpecificationSourceRoute.Glasses =>
                (GlassEstimateBuildKind.Part, Present(line.GuideCode), GlassEstimateBuildKeyKind.PartCode),
            "new_part" when route == RepairSpecificationSourceRoute.AudatexPdf =>
                (GlassEstimateBuildKind.Part, Present(line.PartNumber), GlassEstimateBuildKeyKind.PartNumber),
            "new_part" => (GlassEstimateBuildKind.Part, null, GlassEstimateBuildKeyKind.None),
            "paint_new" or "paint_repair" or "paint_blend" => (GlassEstimateBuildKind.Paint,
                route == RepairSpecificationSourceRoute.Glasses ? line.GuideCode : null, GlassEstimateBuildKeyKind.None),
            "repair" when route == RepairSpecificationSourceRoute.Glasses
                && line.Justification?.StartsWith(PartialRepairNote, StringComparison.Ordinal) != true =>
                (GlassEstimateBuildKind.Repair, Present(line.GuideCode), GlassEstimateBuildKeyKind.PartCode),
            "rnr" when route == RepairSpecificationSourceRoute.Glasses =>
                (GlassEstimateBuildKind.RemoveAndRefit, Present(line.GuideCode), GlassEstimateBuildKeyKind.PartCode),
            "repair" => (GlassEstimateBuildKind.Repair, line.GuideCode, GlassEstimateBuildKeyKind.None),
            "rnr" => (GlassEstimateBuildKind.RemoveAndRefit, line.GuideCode, GlassEstimateBuildKeyKind.None),
            "paint_prep" => (GlassEstimateBuildKind.PaintPreparation, null, GlassEstimateBuildKeyKind.None),
            "check_labour" when IncludedOperations.IsIncluded(line.Type, line.WorkUnits, line.Price, line.Justification) =>
                (GlassEstimateBuildKind.IncludedOperation, line.GuideCode, GlassEstimateBuildKeyKind.None),
            _ => (GlassEstimateBuildKind.Operation, line.GuideCode, GlassEstimateBuildKeyKind.None),
        };

        (bool Sends, GlassEstimateBuildOutcome Outcome, string? Reason) plan = position.Kind switch
        {
            GlassEstimateBuildKind.SpecialOperation => (true, GlassEstimateBuildOutcome.Pending, null),
            GlassEstimateBuildKind.Part when position.Key is null => (false, GlassEstimateBuildOutcome.LeftOff, NoKeyCode),
            GlassEstimateBuildKind.Part => (true, GlassEstimateBuildOutcome.Pending, null),
            GlassEstimateBuildKind.Repair or GlassEstimateBuildKind.RemoveAndRefit
                when position.KeyKind == GlassEstimateBuildKeyKind.PartCode && position.Key is null =>
                (false, GlassEstimateBuildOutcome.LeftOff, NoKeyCode),
            GlassEstimateBuildKind.Repair or GlassEstimateBuildKind.RemoveAndRefit
                when position.KeyKind == GlassEstimateBuildKeyKind.PartCode =>
                (true, GlassEstimateBuildOutcome.Pending, null),
            GlassEstimateBuildKind.IncludedOperation or GlassEstimateBuildKind.PaintPreparation =>
                (false, GlassEstimateBuildOutcome.AddedByGlass, null),
            _ => (false, GlassEstimateBuildOutcome.LeftOff, NotBuiltCode),
        };

        return new GlassEstimateBuildAct(
            [line.Position], position.Kind, description, position.Key, SideOf(description),
            plan.Sends, plan.Outcome, plan.Reason, figures,
            KeyKind: position.Key is null ? GlassEstimateBuildKeyKind.None : position.KeyKind);
    }

    private static decimal? NonZero(decimal? value) => value is 0m ? null : value;

    private static string? Present(string? key) => string.IsNullOrWhiteSpace(key) ? null : key.Trim();

    /// <summary>The side a description's trailing "(L)" or "(R)" names; "(Both)" and anything else name none.</summary>
    private static string? SideOf(string description) =>
        description.EndsWith(" (L)", StringComparison.Ordinal) ? "L"
        : description.EndsWith(" (R)", StringComparison.Ordinal) ? "R"
        : null;
}
