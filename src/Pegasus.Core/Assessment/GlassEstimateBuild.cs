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
    GlassEstimateBuildFigures? Served = null);

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
/// builds special operations; every other line is left off as not built in
/// this version, and later versions change only the outcome a line type gets.
/// </summary>
public static class GlassEstimateBuildPolicy
{
    /// <summary>A line of a kind this version does not build.</summary>
    public const string NotBuiltCode = "glass.build.not_built";

    /// <summary>Glass's has no row for the line.</summary>
    public const string NoRowCode = "glass.build.no_row";

    /// <summary>Glass's has more than one row the line could be.</summary>
    public const string AmbiguousRowCode = "glass.build.ambiguous_row";

    /// <summary>Not sent: an earlier line was posted and its answer was not confirmed, so the build stopped adding.</summary>
    public const string StoppedCode = "glass.build.stopped";

    /// <summary>Nothing was placed, so nothing was saved and the member of staff was given an empty estimate.</summary>
    public const string NothingPlacedCode = "glass.build.nothing_placed";

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
    /// One line's act, by its type and the route the spec came from. Only a
    /// Specialist line is tried; an included operation and paint preparation
    /// are Glass's own to add; every other line waits for a later version.
    /// </summary>
    private static GlassEstimateBuildAct ActOf(RepairSpecificationSourceRoute route, CaseEstimateLineRecord line)
    {
        var description = line.Description?.Trim() ?? string.Empty;
        // A zero is no figure, as Glass's serves none for a dialog without one.
        var figures = new GlassEstimateBuildFigures(NonZero(line.Price), NonZero(line.WorkUnits));
        (GlassEstimateBuildKind Kind, string? Key) position = line.Type switch
        {
            "specialist_fixed" or "specialist_wu" => (GlassEstimateBuildKind.SpecialOperation, description),
            "new_part" => (GlassEstimateBuildKind.Part, route switch
            {
                RepairSpecificationSourceRoute.Glasses => line.GuideCode,
                RepairSpecificationSourceRoute.AudatexPdf => line.PartNumber,
                _ => null,
            }),
            "paint_new" or "paint_repair" or "paint_blend" => (GlassEstimateBuildKind.Paint,
                route == RepairSpecificationSourceRoute.Glasses ? line.GuideCode : null),
            "repair" => (GlassEstimateBuildKind.Repair, line.GuideCode),
            "rnr" => (GlassEstimateBuildKind.RemoveAndRefit, line.GuideCode),
            "paint_prep" => (GlassEstimateBuildKind.PaintPreparation, null),
            "check_labour" when IncludedOperations.IsIncluded(line.Type, line.WorkUnits, line.Price, line.Justification) =>
                (GlassEstimateBuildKind.IncludedOperation, line.GuideCode),
            _ => (GlassEstimateBuildKind.Operation, line.GuideCode),
        };

        (bool Sends, GlassEstimateBuildOutcome Outcome, string? Reason) plan = position.Kind switch
        {
            GlassEstimateBuildKind.SpecialOperation => (true, GlassEstimateBuildOutcome.Pending, null),
            GlassEstimateBuildKind.IncludedOperation or GlassEstimateBuildKind.PaintPreparation =>
                (false, GlassEstimateBuildOutcome.AddedByGlass, null),
            _ => (false, GlassEstimateBuildOutcome.LeftOff, NotBuiltCode),
        };

        return new GlassEstimateBuildAct(
            [line.Position], position.Kind, description, position.Key, SideOf(description),
            plan.Sends, plan.Outcome, plan.Reason, figures);
    }

    private static decimal? NonZero(decimal? value) => value is 0m ? null : value;

    /// <summary>The side a description's trailing "(L)" or "(R)" names; "(Both)" and anything else name none.</summary>
    private static string? SideOf(string description) =>
        description.EndsWith(" (L)", StringComparison.Ordinal) ? "L"
        : description.EndsWith(" (R)", StringComparison.Ordinal) ? "R"
        : null;
}
