using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>How a frozen repair specification version came about (v28 P43; BeforeImport since v38 P3).</summary>
public enum RepairSpecificationSnapshotKind
{
    Imported,
    BeforeScaling,
    Scaled,
    ScalingRemoved,
    BeforeRestore,
    Restored,
    Sent,
    BeforeImport,
}

/// <summary>
/// One frozen version of a repair specification (v28 P43): the header and
/// lines as they stood, numbered per specification, with how it came about.
/// A version the sent report used carries <see cref="SentOnReport"/>.
/// </summary>
public sealed record RepairSpecificationSnapshot(
    Guid Id,
    Guid CaseId,
    Guid SpecificationId,
    int Number,
    RepairSpecificationSnapshotKind Kind,
    string Origin,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    EstimateDetails Details,
    IReadOnlyList<CaseEstimateLineRecord> Lines,
    decimal Gross,
    bool SentOnReport,
    RepairSpecificationSupplementary? Supplementary = null)
{
    /// <summary>
    /// The frozen version as a specification, so Compare, the Supplementary
    /// statement and the totals read it as they read a live spec (v38 P2, P3).
    /// Its source is the version's own record; it carries no document route.
    /// </summary>
    public RepairSpecificationVersion AsSpecification() => new(
        SpecificationId, CaseId, Number, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        Lines, CreatedBy, CreatedAtUtc, Details, Supplementary: Supplementary);
}

public sealed record FreezeRepairSpecificationRequest(
    Guid CaseId,
    Guid SpecificationId,
    ActionActor Actor,
    RepairSpecificationSnapshotKind Kind,
    string Origin);

public interface IRepairSpecificationSnapshotStore
{
    /// <summary>
    /// Freezes the specification as it stands now. An unchanged draft reuses
    /// the last version rather than repeating it, except for the acts that
    /// must leave their own mark (<see cref="RepairSpecificationSnapshotKind.BeforeScaling"/>,
    /// <see cref="RepairSpecificationSnapshotKind.Scaled"/>,
    /// <see cref="RepairSpecificationSnapshotKind.ScalingRemoved"/>,
    /// <see cref="RepairSpecificationSnapshotKind.BeforeRestore"/>,
    /// <see cref="RepairSpecificationSnapshotKind.Restored"/>).
    /// A Sent mark is recorded only by the observed mail transaction.
    /// </summary>
    Task<RepairSpecificationSnapshot> FreezeAsync(FreezeRepairSpecificationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepairSpecificationSnapshot>> ListAsync(Guid caseId, Guid specificationId, CancellationToken cancellationToken);
}

/// <summary>The floors scaling never goes below (v28 P34): a labour rate per hour and a share of each price.</summary>
public sealed record ScalingFloors(decimal LabourRatePerHour, decimal PricePercent)
{
    /// <summary>The reference file's figures: £50 an hour and 65 % of price.</summary>
    public static ScalingFloors Default { get; } = new(50m, 65m);

    public static ScalingFloors Validate(ScalingFloors floors)
    {
        ArgumentNullException.ThrowIfNull(floors);
        if (floors.LabourRatePerHour < 0 || floors.PricePercent is < 0 or > 100)
        {
            throw new ArgumentException("The scaling floors must be a non-negative rate and a percentage between 0 and 100.", nameof(floors));
        }
        return floors;
    }
}

/// <summary>
/// Target % of value (v28 P34, ruled 20 September 2026: an Engineer's act on
/// the record). One factor lowers every part price, every materials figure
/// and the labour rate, each down to its floor; hours are never touched. The
/// factor is found by bisection so the printed total inc VAT meets the
/// target as closely as the floors allow. Moving the slider previews the
/// same result (issue 897); only Apply records it.
/// </summary>
public static class RepairSpecificationScaling
{
    /// <summary>
    /// The scaled header and lines, and the scaled spec's totals, which a
    /// preview shows and Apply records. <see cref="LabourAtFloor"/>: the
    /// labour rate was lowered as far as its floor allows (v28 P34).
    /// </summary>
    public sealed record Result(
        EstimateDetails Details,
        IReadOnlyList<EstimateLineInput> Lines,
        decimal Factor,
        decimal PriceFactor,
        decimal GrossBefore,
        EstimateTotals Totals,
        bool LabourAtFloor)
    {
        public decimal GrossAfter => Totals.Printed.Gross;
    }

    /// <summary>
    /// The total inc VAT a percentage of the Engineer's Value asks for. The
    /// Engineer's Value must be recorded and the percentage at least 1. There
    /// is no upper bound here: the spec's own total is the ceiling, and
    /// <see cref="Scale"/> never raises a spec above it (operator, 8 October 2026).
    /// </summary>
    public static decimal TargetGross(decimal? engineerValue, decimal targetPercent)
    {
        if (engineerValue is not { } value || value <= 0m)
        {
            throw new InvalidOperationException("An Engineer's Value is required before scaling.");
        }
        if (targetPercent < 1m)
        {
            throw new ArgumentException(
                "The target must be at least 1 percent of the Engineer's Value.", nameof(targetPercent));
        }
        return value * targetPercent / 100m;
    }

    /// <summary>
    /// The spec's own share of the Engineer's Value, rounded up to a tenth of
    /// a percent: where the Target % of value slider starts and the highest it
    /// goes (operator, 8 October 2026). Rounded up, so a target at it leaves
    /// the spec as estimated.
    /// </summary>
    public static decimal PercentOfValue(decimal engineerValue, decimal gross)
    {
        if (engineerValue <= 0m)
        {
            throw new InvalidOperationException("An Engineer's Value is required before scaling.");
        }
        return Math.Ceiling(gross / engineerValue * 1_000m) / 10m;
    }

    /// <summary>
    /// The lowest share of the Engineer's Value the floors allow, rounded up
    /// to a whole percent and never below one: where the slider's range
    /// begins (v38 P6, the reference's scaleBounds; operator, 12 October 2026).
    /// </summary>
    public static decimal FloorPercentOfValue(decimal engineerValue, RepairSpecificationVersion specification, ScalingFloors floors)
    {
        if (engineerValue <= 0m)
        {
            throw new InvalidOperationException("An Engineer's Value is required before scaling.");
        }
        return Math.Max(1m, Math.Ceiling(Range(specification, floors).Floor / engineerValue * 100m));
    }

    public static decimal GrossAt(RepairSpecificationVersion specification, decimal factor, ScalingFloors floors) =>
        EstimateTotals.Compute(Scaled(specification, factor, floors)).Printed.Gross;

    /// <summary>The lowest total the floors allow, and the total as estimated.</summary>
    public static (decimal Floor, decimal AsEstimated) Range(RepairSpecificationVersion specification, ScalingFloors floors)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ScalingFloors.Validate(floors);
        return (GrossAt(specification, 0m, floors), GrossAt(specification, 1m, floors));
    }

    public static Result Scale(RepairSpecificationVersion specification, decimal targetGross, ScalingFloors floors)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ScalingFloors.Validate(floors);
        if (specification.Lines.Count == 0)
        {
            throw new InvalidOperationException("A repair specification with no lines cannot be scaled.");
        }
        var (floor, top) = Range(specification, floors);
        var target = Math.Clamp(targetGross, floor, top);
        decimal low = 0m, high = 1m;
        for (var step = 0; step < 40; step++)
        {
            var mid = (low + high) / 2m;
            if (GrossAt(specification, mid, floors) > target)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }
        var factor = low;
        var scaled = Scaled(specification, factor, floors);
        var scaledRate = scaled.Details.HourlyRate;
        return new(
            scaled.Details,
            scaled.Lines.Select(ToInput).ToArray(),
            decimal.Round(factor, 6),
            decimal.Round(Math.Max(floors.PricePercent / 100m, factor), 6),
            top,
            EstimateTotals.Compute(scaled),
            scaledRate < specification.Details.HourlyRate && scaledRate <= floors.LabourRatePerHour);
    }

    private static RepairSpecificationVersion Scaled(RepairSpecificationVersion specification, decimal factor, ScalingFloors floors)
    {
        var priceFactor = Math.Max(floors.PricePercent / 100m, factor);
        // The rate scaling lowers is the one the spec prices at, uplift
        // included: the scaled spec stands on a typed figure with no card
        // and no uplift, as the reference lands it on Custom (v38 P5, P6).
        var baseRate = specification.Details.HourlyRate;
        // A scaled rate lands on a whole pound or 50p, never below its floor
        // and never above the rate it started from (the reference's
        // scaledRate; operator, 12 October 2026). At factor one the rate is
        // the estimate's own.
        var rate = factor >= 1m
            ? baseRate
            : Math.Min(baseRate, Math.Max(Math.Min(floors.LabourRatePerHour, baseRate), Math.Round(baseRate * factor * 2m, MidpointRounding.AwayFromZero) / 2m));
        var details = specification.Details with
        {
            LabourRate = Pence(rate),
            // Scaling changes the estimate's typed rate. The pre-scale frozen
            // snapshot retains the card evidence; the scaled draft does not
            // claim that its derived rate came from the card, nor that the
            // uplift still applies on top of it.
            Rate = null,
            RegionalUplift = false,
        };
        var lines = specification.Lines
            .Select(line => line with
            {
                Price = line.Price is { } price ? Pence(price * priceFactor) : null,
                Materials = line.Materials is { } materials ? Pence(materials * priceFactor) : null,
            })
            .ToArray();
        return specification with { Details = details, Lines = lines };
    }

    private static decimal Pence(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>A recorded line as the save command takes it, every recorded fact carried.</summary>
    public static EstimateLineInput ToInput(CaseEstimateLineRecord line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new(
            line.Type, line.GuideCode, line.Description, line.WorkUnits, line.Price, line.Unpriced,
            line.PartNumber, line.Betterment, line.EvidenceLabel, line.Justification,
            line.PaintWorkUnits, line.Quantity, line.Materials, line.Origin,
            line.SourceDocumentIdentity, line.SourceDocumentVersionId, line.SourceDocumentSha256,
            line.SourceRowIdentity, line.AmendedBy, line.AmendedAtUtc);
    }
}

/// <summary>
/// One Engineer Apply: scale the saved Draft in one guarded operation. The
/// page saves the Case, the specification included, before it asks (the one
/// Save, 23 September 2026), so this carries intent only;
/// <see cref="EngineerValue"/> is filled by the Core act from the assessment
/// projection before the store opens its transaction.
/// </summary>
public sealed record ScaleRepairSpecificationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    Guid SpecificationId,
    decimal TargetPercentOfValue,
    ScalingFloors Floors,
    decimal? EngineerValue = null)
{
    /// <summary>The work whose specification is scaled, and whose Engineer's Value it is scaled to.</summary>
    public CaseWorkSelector Work { get; init; } = CaseWorkSelector.Current;
}

public sealed record RemoveRepairSpecificationScalingRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    Guid SpecificationId)
{
    public CaseWorkSelector Work { get; init; } = CaseWorkSelector.Current;
}

public sealed record RestoreRepairSpecificationSnapshotRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    Guid SpecificationId,
    Guid SnapshotId)
{
    public CaseWorkSelector Work { get; init; } = CaseWorkSelector.Current;
}

public interface IScaleRepairSpecification
{
    Task<RepairSpecificationVersion> ExecuteAsync(
        ScaleRepairSpecificationRequest request,
        CancellationToken cancellationToken);
}

public interface IRemoveRepairSpecificationScaling
{
    Task<RepairSpecificationVersion> ExecuteAsync(RemoveRepairSpecificationScalingRequest request, CancellationToken cancellationToken);
}

public interface IRestoreRepairSpecificationSnapshot
{
    Task<RepairSpecificationVersion> ExecuteAsync(RestoreRepairSpecificationSnapshotRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Apply (v28 P34): the target is derived from the Engineer's Value
/// in the existing assessment projection. Persistence owns the single
/// transaction that scales the saved draft and freezes both versions.
/// </summary>
public sealed class ScaleRepairSpecification(
    ICaseAssessmentStore assessment,
    IRepairSpecificationStore store) : IScaleRepairSpecification
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        ScaleRepairSpecificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        var projection = await assessment.GetAsync(request.CaseId, request.Work, cancellationToken);
        var field = projection?.Field(AssessmentVocabulary.ValueEngineer);
        decimal? engineerValue = field is not null
            && decimal.TryParse(field.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var recorded)
                ? recorded
                : null;
        // Refused here, before the store's transaction, on the store's own terms.
        RepairSpecificationScaling.TargetGross(engineerValue, request.TargetPercentOfValue);

        return await store.ScaleAsync(request with { EngineerValue = engineerValue }, cancellationToken);
    }
}

/// <summary>Remove scaling (v28 P34): the specification returns exactly to the version frozen before the last scaling.</summary>
public sealed class RemoveRepairSpecificationScaling(
    IRepairSpecificationStore store) : IRemoveRepairSpecificationScaling
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        RemoveRepairSpecificationScalingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        return await store.RemoveScalingAsync(request, cancellationToken);
    }
}

/// <summary>Restore (v28 P43): the store freezes and replaces the draft atomically.</summary>
public sealed class RestoreRepairSpecificationSnapshot(
    IRepairSpecificationStore store) : IRestoreRepairSpecificationSnapshot
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        RestoreRepairSpecificationSnapshotRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        return await store.RestoreSnapshotAsync(request, cancellationToken);
    }
}

/// <summary>How a specification came about, as the origin line and a frozen version say it.</summary>
public static class RepairSpecificationRouteWords
{
    public static string Of(RepairSpecificationSourceRoute route) => route switch
    {
        RepairSpecificationSourceRoute.Manual => "entered by hand",
        RepairSpecificationSourceRoute.Glasses => "from Glass's",
        RepairSpecificationSourceRoute.AudatexPdf => "from Audatex",
        RepairSpecificationSourceRoute.Json => "from a JSON estimate",
        RepairSpecificationSourceRoute.AiDraft => "drafted by AI",
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };
}

/// <summary>The words the acts record and the report prints; report wording, not screen copy.</summary>
public static class RepairSpecificationWording
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    public static string Money(decimal value) => "£" + value.ToString("N2", Gb);

    public static string Scaled(RepairSpecificationScaling.Result result, decimal? percentOfValue) =>
        "Repair spec scaled: " + ScaleReadout(result, percentOfValue);

    /// <summary>
    /// The Target % of value readout (v28 P34): what the spec totals before
    /// and after, the share of value and the price factor, and whether the
    /// labour rate stopped at its floor. The preview shows it while the slider
    /// moves and Apply records it.
    /// </summary>
    public static string ScaleReadout(RepairSpecificationScaling.Result result, decimal? percentOfValue)
    {
        ArgumentNullException.ThrowIfNull(result);
        var share = percentOfValue is { } percent
            ? " (" + percent.ToString("0.0", CultureInfo.InvariantCulture) + " % of value)"
            : string.Empty;
        var floor = result.LabourAtFloor ? " \u00b7 labour at floor" : string.Empty;
        return $"{Money(result.GrossBefore)} \u2192 {Money(result.GrossAfter)}{share} \u00b7 prices \u00d7{result.PriceFactor.ToString("0.00", CultureInfo.InvariantCulture)}{floor}";
    }
}

/// <summary>
/// What a repair specification says about the frozen version it supplements
/// (v28 P20; v38 P3, operator, 12 October 2026): an estimate imported onto
/// the spec changes it against the version the last sent report used, else
/// the grid the import replaced. <see cref="OfSnapshotId"/> names that
/// version; the statement is composed from the two and printed when the
/// Engineer says so.
/// </summary>
public sealed record RepairSpecificationSupplementary(
    Guid OfSnapshotId,
    string Reason,
    bool ExplainOnReport,
    string Statement);

/// <summary>
/// Richer Compare (v28 P19) and the Supplementary statement (P20): two
/// specifications line by line. A line is the same line when its part number
/// or the start of its description matches; lines that kept their type pair
/// first, then a line whose type changed pairs with one left over and says
/// what its type was (v38 P2). A frozen version compares through
/// <see cref="RepairSpecificationSnapshot.AsSpecification"/> (v38 P2).
/// </summary>
public static class RepairSpecificationComparison
{
    public sealed record LineChange(CaseEstimateLineRecord From, CaseEstimateLineRecord To, IReadOnlyList<string> ChangedFields);

    /// <summary>
    /// The lines added, changed and removed between two specifications, and
    /// the lines both hold unchanged (v38 P2: the side-by-side table lists
    /// every line), with the totals each side prices at.
    /// </summary>
    public sealed record Diff(
        IReadOnlyList<CaseEstimateLineRecord> Added,
        IReadOnlyList<LineChange> Changed,
        IReadOnlyList<CaseEstimateLineRecord> Removed,
        decimal GrossFrom,
        decimal GrossTo,
        IReadOnlyList<LineChange> Unchanged,
        decimal RateFrom,
        decimal RateTo)
    {
        public decimal GrossDelta => GrossTo - GrossFrom;
    }

    /// <summary>
    /// One line's value at a rate, as the side-by-side Compare prints it
    /// (v38 P2): what <see cref="EstimateTotals.Compute"/> prices on that line
    /// before discounts and VAT — a part's amount by its quantity, the hours
    /// the type prices at the rate, materials, and an amount a labour line
    /// carries off-pattern.
    /// </summary>
    public static decimal LineValue(CaseEstimateLineRecord line, decimal hourlyRate)
    {
        ArgumentNullException.ThrowIfNull(line);
        var quantity = line.Quantity is { } supplied && supplied > 0 ? supplied : 1;
        var amount = (line.Price ?? 0m) * quantity;
        var materials = line.Materials ?? 0m;
        var operation = EstimateOperations.FromLineType(line.Type);
        var hours = operation switch
        {
            EstimateOperation.Specialist => line.Type == "specialist_wu" ? line.WorkUnits ?? 0m : 0m,
            EstimateOperation.Paint or EstimateOperation.Blend => (line.WorkUnits ?? 0m) + (line.PaintWorkUnits ?? 0m),
            _ => line.WorkUnits ?? 0m,
        };
        return decimal.Round(amount + hours * hourlyRate + materials, 2, MidpointRounding.AwayFromZero);
    }

    public static readonly IReadOnlyList<(string Code, string Label, string Lead)> SupplementaryReasons =
    [
        ("estimate", "Supplementary estimate received", "Following receipt of a supplementary estimate"),
        ("dismantle", "Further damage found on dismantling", "Following dismantling of the vehicle, further damage was identified and"),
        ("inspect", "Further inspection", "Following a further inspection of the vehicle"),
        ("images", "Further images received", "Following receipt of further images"),
    ];

    public static Diff Compare(RepairSpecificationVersion from, RepairSpecificationVersion to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var fromLines = from.Lines.OrderBy(line => line.Position).ToList();
        var toLines = to.Lines.OrderBy(line => line.Position).ToList();
        // A frozen version's lines keep the ids they had when frozen, which the
        // live spec's replaced lines no longer share; a line is matched once.
        // Lines that kept their type pair first, so a line whose type changed
        // takes only a line no same-type line claimed (one panel's Replace,
        // Paint and R&I lines share a description).
        var used = new HashSet<int>();
        var matches = new int[toLines.Count];
        Array.Fill(matches, -1);
        foreach (var sameType in new[] { true, false })
        {
            for (var position = 0; position < toLines.Count; position++)
            {
                if (matches[position] >= 0)
                {
                    continue;
                }
                var line = toLines[position];
                var index = fromLines.FindIndex(candidate => SameLine(candidate, line, sameType));
                while (index >= 0 && used.Contains(index))
                {
                    index = fromLines.FindIndex(index + 1, candidate => SameLine(candidate, line, sameType));
                }
                if (index >= 0)
                {
                    used.Add(index);
                    matches[position] = index;
                }
            }
        }
        var added = new List<CaseEstimateLineRecord>();
        var changed = new List<LineChange>();
        var unchanged = new List<LineChange>();
        for (var position = 0; position < toLines.Count; position++)
        {
            var line = toLines[position];
            if (matches[position] < 0)
            {
                added.Add(line);
                continue;
            }
            var match = fromLines[matches[position]];
            var fields = ChangedFields(match, line);
            if (fields.Count > 0)
            {
                changed.Add(new(match, line, fields));
            }
            else
            {
                unchanged.Add(new(match, line, []));
            }
        }
        var removed = fromLines.Where((_, index) => !used.Contains(index)).ToArray();
        return new(
            added, changed, removed,
            EstimateTotals.Compute(from).Printed.Gross,
            EstimateTotals.Compute(to).Printed.Gross,
            unchanged,
            from.Details.HourlyRate,
            to.Details.HourlyRate);
    }

    /// <summary>A frozen version against a live specification (v38 P2, P3).</summary>
    public static Diff Compare(RepairSpecificationSnapshot from, RepairSpecificationVersion to)
    {
        ArgumentNullException.ThrowIfNull(from);
        return Compare(from.AsSpecification(), to);
    }

    /// <summary>The longest statement the specification's record holds.</summary>
    public const int MaximumStatementLength = 4000;

    /// <summary>
    /// What an import onto a spec supplements (v38 P3): the import against
    /// the version it is measured from, under the first reason, Explain
    /// unticked. None when that version held no lines: an import onto an
    /// empty spec adds to nothing.
    /// </summary>
    public static RepairSpecificationSupplementary? ForImport(
        Guid ofSnapshotId, RepairSpecificationVersion from, RepairSpecificationVersion imported)
    {
        ArgumentNullException.ThrowIfNull(from);
        if (from.Lines.Count == 0)
        {
            return null;
        }
        var reason = SupplementaryReasons[0].Code;
        return new(ofSnapshotId, reason, false, SupplementaryStatement(Compare(from, imported), reason));
    }

    /// <summary>
    /// The composed paragraph: the reason's lead, then what was added, revised,
    /// no longer required and the movement of the cost. A change list too long
    /// for <see cref="MaximumStatementLength"/> is cut short with an ellipsis; the
    /// movement of the cost always stands.
    /// </summary>
    public static string SupplementaryStatement(Diff diff, string reasonCode)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var reason = SupplementaryReasons.FirstOrDefault(item => item.Code == reasonCode);
        if (reason.Code is null)
        {
            throw new ArgumentException("The supplementary reason is not one of the four.", nameof(reasonCode));
        }
        var hours = diff.Changed
            .Where(change => change.ChangedFields.Contains("hours") || change.ChangedFields.Contains("paint hours"))
            .ToArray();
        var shown = Math.Max(diff.Added.Count, Math.Max(hours.Length, diff.Removed.Count));
        var text = Statement(diff, reason.Lead, hours, shown);
        while (text.Length > MaximumStatementLength && shown > 0)
        {
            shown--;
            text = Statement(diff, reason.Lead, hours, shown);
        }
        return text;
    }

    private static string Statement(Diff diff, string lead, LineChange[] hours, int shown)
    {
        var text = lead + (diff.Added.Count > 0
            ? " the following additional items are now required: " + Names(diff.Added, shown, Name) + "."
            : " the repair specification has been revised.");
        if (hours.Length > 0)
        {
            text += " The repair time for " + Names(hours, shown, change =>
                $"{Name(change.To)} ({Hours(change.From)} h \u2192 {Hours(change.To)} h)") + " has been revised.";
        }
        if (diff.Removed.Count > 0)
        {
            text += " " + Names(diff.Removed, shown, Name) + (diff.Removed.Count > 1 ? " are" : " is") + " no longer required.";
        }
        text += $" The estimated repair cost has {(diff.GrossTo >= diff.GrossFrom ? "increased" : "reduced")} from {RepairSpecificationWording.Money(diff.GrossFrom)} to {RepairSpecificationWording.Money(diff.GrossTo)}.";
        return text;
    }

    /// <summary>The first <paramref name="shown"/> items, and an ellipsis for any more.</summary>
    private static string Names<T>(IReadOnlyList<T> items, int shown, Func<T, string> name)
    {
        var names = items.Take(shown).Select(name).ToList();
        if (items.Count > shown)
        {
            names.Add("\u2026");
        }
        return string.Join("; ", names);
    }

    private static bool SameLine(CaseEstimateLineRecord a, CaseEstimateLineRecord b, bool sameType)
    {
        if (a.Id != Guid.Empty && a.Id == b.Id)
        {
            return true;
        }
        if (sameType && a.Type != b.Type)
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(a.PartNumber) && string.Equals(a.PartNumber, b.PartNumber, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        var left = (a.Description ?? string.Empty).Trim();
        var right = (b.Description ?? string.Empty).Trim();
        return left.Length > 0 && right.Length > 0
            && (left.StartsWith(right, StringComparison.OrdinalIgnoreCase) || right.StartsWith(left, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ChangedFields(CaseEstimateLineRecord from, CaseEstimateLineRecord to)
    {
        var fields = new List<string>();
        // In the editor's vocabulary, as an amendment is judged.
        if (EstimateOperations.FromLineType(from.Type) != EstimateOperations.FromLineType(to.Type)) fields.Add("type");
        if (!string.Equals(from.Description?.Trim(), to.Description?.Trim(), StringComparison.Ordinal)) fields.Add("description");
        if (!string.Equals(from.PartNumber?.Trim(), to.PartNumber?.Trim(), StringComparison.OrdinalIgnoreCase)) fields.Add("part number");
        if ((from.Quantity ?? 1) != (to.Quantity ?? 1)) fields.Add("quantity");
        if ((from.Price ?? 0m) != (to.Price ?? 0m)) fields.Add("unit amount");
        if ((from.WorkUnits ?? 0m) != (to.WorkUnits ?? 0m)) fields.Add("hours");
        if ((from.PaintWorkUnits ?? 0m) != (to.PaintWorkUnits ?? 0m)) fields.Add("paint hours");
        if ((from.Materials ?? 0m) != (to.Materials ?? 0m)) fields.Add("materials");
        return fields;
    }

    private static string Name(CaseEstimateLineRecord line) =>
        !string.IsNullOrWhiteSpace(line.Description) ? line.Description.Trim()
        : !string.IsNullOrWhiteSpace(line.GuideCode) ? line.GuideCode.Trim()
        : line.Type;

    private static string Hours(CaseEstimateLineRecord line) =>
        ((line.WorkUnits ?? 0m) + (line.PaintWorkUnits ?? 0m)).ToString("0.#", CultureInfo.InvariantCulture);
}
