using System.Globalization;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>How a frozen repair specification version came about (v28 P43).</summary>
public enum RepairSpecificationSnapshotKind
{
    Imported,
    BeforeScaling,
    Scaled,
    BeforeRestore,
    Sent,
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
    bool SentOnReport);

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
    /// must leave their own mark (<see cref="RepairSpecificationSnapshotKind.Scaled"/>,
    /// <see cref="RepairSpecificationSnapshotKind.Sent"/>).
    /// </summary>
    Task<RepairSpecificationSnapshot> FreezeAsync(FreezeRepairSpecificationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepairSpecificationSnapshot>> ListAsync(Guid caseId, Guid specificationId, CancellationToken cancellationToken);

    Task<RepairSpecificationSnapshot?> GetAsync(Guid caseId, Guid snapshotId, CancellationToken cancellationToken);
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
/// target as closely as the floors allow.
/// </summary>
public static class RepairSpecificationScaling
{
    public sealed record Result(
        EstimateDetails Details,
        IReadOnlyList<EstimateLineInput> Lines,
        decimal Factor,
        decimal PriceFactor,
        decimal GrossBefore,
        decimal GrossAfter);

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
        var factor = (low + high) / 2m;
        var scaled = Scaled(specification, factor, floors);
        return new(
            scaled.Details,
            scaled.Lines.Select(ToInput).ToArray(),
            decimal.Round(factor, 6),
            decimal.Round(Math.Max(floors.PricePercent / 100m, factor), 6),
            top,
            EstimateTotals.Compute(scaled).Printed.Gross);
    }

    private static RepairSpecificationVersion Scaled(RepairSpecificationVersion specification, decimal factor, ScalingFloors floors)
    {
        var priceFactor = Math.Max(floors.PricePercent / 100m, factor);
        var baseRate = specification.Details.BaseHourlyRate;
        var rate = Math.Max(Math.Min(floors.LabourRatePerHour, baseRate), baseRate * factor);
        var details = specification.Details with
        {
            LabourRate = Pence(rate),
            Rate = specification.Details.Rate is { } snapshot ? snapshot with { HourlyRate = Pence(rate) } : null,
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
            line.PartNumber, line.Betterment, line.Status, line.EvidenceLabel, line.Justification,
            line.PaintWorkUnits, line.Quantity, line.Materials, line.Origin,
            line.SourceDocumentIdentity, line.SourceDocumentVersionId, line.SourceDocumentSha256,
            line.SourceRowIdentity, line.AmendedBy, line.AmendedAtUtc);
    }
}

public sealed record ScaleRepairSpecificationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    Guid SpecificationId,
    decimal TargetGross,
    ScalingFloors Floors,
    decimal? TargetPercentOfValue = null);

public sealed record RemoveRepairSpecificationScalingRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    Guid SpecificationId);

public sealed record RestoreRepairSpecificationSnapshotRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    Guid SpecificationId,
    Guid SnapshotId);

public interface IScaleRepairSpecification
{
    Task<RepairSpecificationVersion> ExecuteAsync(ScaleRepairSpecificationRequest request, CancellationToken cancellationToken);
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
/// Apply (v28 P34): freezes the outgoing draft, saves the scaled header and
/// lines, and freezes the scaled version with how it came about. An
/// Engineer's act on a Draft only.
/// </summary>
public sealed class ScaleRepairSpecification(
    IRepairSpecificationStore store,
    IRepairSpecificationSnapshotStore snapshots) : IScaleRepairSpecification
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        ScaleRepairSpecificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        var specification = await store.GetVersionAsync(request.CaseId, request.SpecificationId, cancellationToken)
            ?? throw new KeyNotFoundException("The repair specification was not found.");
        EstimatePolicy.ValidateEditable(specification, request.Actor);
        var result = RepairSpecificationScaling.Scale(specification, request.TargetGross, request.Floors);
        await snapshots.FreezeAsync(
            new(request.CaseId, request.SpecificationId, request.Actor,
                RepairSpecificationSnapshotKind.BeforeScaling, "Before scaling"),
            cancellationToken);
        var reason = RepairSpecificationWording.Scaled(result, request.TargetPercentOfValue);
        var saved = await store.SaveEstimateAsync(
            new(request.CaseId, request.ExpectedVersion, request.Actor, request.OperationKey, reason,
                request.EditLeaseToken, request.SpecificationId, result.Details, result.Lines,
                specification.Source, specification.AiJobId,
                ExistingLineIds: [.. specification.Lines.OrderBy(line => line.Position).Select(line => (Guid?)line.Id)])
            {
                EventType = "estimate_scaled",
                Supplementary = specification.Supplementary,
            },
            cancellationToken);
        await snapshots.FreezeAsync(
            new(request.CaseId, request.SpecificationId, request.Actor,
                RepairSpecificationSnapshotKind.Scaled, reason),
            cancellationToken);
        return saved;
    }
}

/// <summary>Remove scaling (v28 P34): the specification returns exactly to the version frozen before the last scaling.</summary>
public sealed class RemoveRepairSpecificationScaling(
    IRepairSpecificationStore store,
    IRepairSpecificationSnapshotStore snapshots) : IRemoveRepairSpecificationScaling
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        RemoveRepairSpecificationScalingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        var specification = await store.GetVersionAsync(request.CaseId, request.SpecificationId, cancellationToken)
            ?? throw new KeyNotFoundException("The repair specification was not found.");
        EstimatePolicy.ValidateEditable(specification, request.Actor);
        var before = (await snapshots.ListAsync(request.CaseId, request.SpecificationId, cancellationToken))
            .Where(snapshot => snapshot.Kind == RepairSpecificationSnapshotKind.BeforeScaling)
            .MaxBy(snapshot => snapshot.Number)
            ?? throw new InvalidOperationException("The repair specification has not been scaled.");
        return await store.SaveEstimateAsync(
            new(request.CaseId, request.ExpectedVersion, request.Actor, request.OperationKey, "Scaling removed",
                request.EditLeaseToken, request.SpecificationId, before.Details,
                before.Lines.Select(RepairSpecificationScaling.ToInput).ToArray(),
                specification.Source, specification.AiJobId,
                ExistingLineIds: RepairSpecificationWording.MatchingIds(specification, before))
            {
                EventType = "estimate_scaling_removed",
                Supplementary = specification.Supplementary,
            },
            cancellationToken);
    }
}

/// <summary>Restore (v28 P43): the outgoing draft is frozen first, then the chosen version's header and lines become the draft.</summary>
public sealed class RestoreRepairSpecificationSnapshot(
    IRepairSpecificationStore store,
    IRepairSpecificationSnapshotStore snapshots) : IRestoreRepairSpecificationSnapshot
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        RestoreRepairSpecificationSnapshotRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        var specification = await store.GetVersionAsync(request.CaseId, request.SpecificationId, cancellationToken)
            ?? throw new KeyNotFoundException("The repair specification was not found.");
        EstimatePolicy.ValidateEditable(specification, request.Actor);
        var version = await snapshots.GetAsync(request.CaseId, request.SnapshotId, cancellationToken)
            ?? throw new KeyNotFoundException("The version was not found.");
        if (version.SpecificationId != request.SpecificationId)
        {
            throw new InvalidOperationException("The version belongs to another repair specification.");
        }
        await snapshots.FreezeAsync(
            new(request.CaseId, request.SpecificationId, request.Actor,
                RepairSpecificationSnapshotKind.BeforeRestore, $"Before v{version.Number} was restored"),
            cancellationToken);
        return await store.SaveEstimateAsync(
            new(request.CaseId, request.ExpectedVersion, request.Actor, request.OperationKey,
                $"Restored from v{version.Number} ({version.Origin})",
                request.EditLeaseToken, request.SpecificationId, version.Details,
                version.Lines.Select(RepairSpecificationScaling.ToInput).ToArray(),
                specification.Source, specification.AiJobId,
                ExistingLineIds: RepairSpecificationWording.MatchingIds(specification, version))
            {
                EventType = "estimate_restored",
                Supplementary = specification.Supplementary,
            },
            cancellationToken);
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
        RepairSpecificationSourceRoute.ApprovedAiProposal => "from an approved AI proposal",
        RepairSpecificationSourceRoute.AiDraft => "drafted by AI",
        _ => "recorded before source tracking"
    };
}

/// <summary>The words the acts record and the report prints; report wording, not screen copy.</summary>
public static class RepairSpecificationWording
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    public static string Money(decimal value) => "£" + value.ToString("N2", Gb);

    public static string Scaled(RepairSpecificationScaling.Result result, decimal? percentOfValue)
    {
        ArgumentNullException.ThrowIfNull(result);
        var share = percentOfValue is { } percent
            ? " (" + percent.ToString("0.0", CultureInfo.InvariantCulture) + " % of value)"
            : string.Empty;
        return $"Repair spec scaled: {Money(result.GrossBefore)} \u2192 {Money(result.GrossAfter)}{share} \u00b7 prices \u00d7{result.PriceFactor.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// The contract repair sentence (v28 P35) the report prints once a sum is
    /// agreed.
    /// </summary>
    public static string ContractRepair(decimal agreedSum) =>
        $"A contract repair has been agreed for the total sum of {Money(agreedSum)}. Costs cannot increase above this figure.";

    /// <summary>
    /// The line identities a restored version keeps: a restored line matches
    /// the draft's line of the same position when it is the same line, so
    /// its evidence and amendment stamps travel with it.
    /// </summary>
    public static IReadOnlyList<Guid?> MatchingIds(RepairSpecificationVersion draft, RepairSpecificationSnapshot version)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(version);
        var live = draft.Lines.ToDictionary(line => line.Id);
        return version.Lines.Select(line => live.ContainsKey(line.Id) ? (Guid?)line.Id : null).ToArray();
    }
}

/// <summary>What a repair specification says about the one it supplements (v28 P20).</summary>
public sealed record RepairSpecificationSupplementary(
    Guid OfSpecificationId,
    string Reason,
    bool ExplainOnReport,
    string Statement);

/// <summary>
/// Richer Compare (v28 P19) and the Supplementary statement (P20): two
/// specifications line by line. A line is the same line when its type
/// matches and either its part number or the start of its description does.
/// </summary>
public static class RepairSpecificationComparison
{
    public sealed record LineChange(CaseEstimateLineRecord From, CaseEstimateLineRecord To, IReadOnlyList<string> ChangedFields);

    public sealed record Diff(
        IReadOnlyList<CaseEstimateLineRecord> Added,
        IReadOnlyList<LineChange> Changed,
        IReadOnlyList<CaseEstimateLineRecord> Removed,
        decimal GrossFrom,
        decimal GrossTo)
    {
        public decimal GrossDelta => GrossTo - GrossFrom;
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
        var used = new HashSet<Guid>();
        var added = new List<CaseEstimateLineRecord>();
        var changed = new List<LineChange>();
        foreach (var line in to.Lines.OrderBy(line => line.Position))
        {
            var match = fromLines.FirstOrDefault(candidate => !used.Contains(candidate.Id) && SameLine(candidate, line));
            if (match is null)
            {
                added.Add(line);
                continue;
            }
            used.Add(match.Id);
            var fields = ChangedFields(match, line);
            if (fields.Count > 0)
            {
                changed.Add(new(match, line, fields));
            }
        }
        var removed = fromLines.Where(line => !used.Contains(line.Id)).ToArray();
        return new(
            added, changed, removed,
            EstimateTotals.ForProjection(from).Printed.Gross,
            EstimateTotals.ForProjection(to).Printed.Gross);
    }

    /// <summary>The composed paragraph: the reason's lead, then what was added, revised, no longer required and the movement of the cost.</summary>
    public static string SupplementaryStatement(Diff diff, string reasonCode)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var reason = SupplementaryReasons.FirstOrDefault(item => item.Code == reasonCode);
        if (reason.Code is null)
        {
            throw new ArgumentException("The supplementary reason is not one of the four.", nameof(reasonCode));
        }
        var text = reason.Lead + (diff.Added.Count > 0
            ? " the following additional items are now required: " + string.Join("; ", diff.Added.Select(Name)) + "."
            : " the repair specification has been revised.");
        var hours = diff.Changed
            .Where(change => change.ChangedFields.Contains("hours") || change.ChangedFields.Contains("paint hours"))
            .ToArray();
        if (hours.Length > 0)
        {
            text += " The repair time for " + string.Join("; ", hours.Select(change =>
                $"{Name(change.To)} ({Hours(change.From)} h \u2192 {Hours(change.To)} h)")) + " has been revised.";
        }
        if (diff.Removed.Count > 0)
        {
            text += " " + string.Join("; ", diff.Removed.Select(Name)) + (diff.Removed.Count > 1 ? " are" : " is") + " no longer required.";
        }
        text += $" The estimated repair cost has {(diff.GrossTo >= diff.GrossFrom ? "increased" : "reduced")} from {RepairSpecificationWording.Money(diff.GrossFrom)} to {RepairSpecificationWording.Money(diff.GrossTo)}.";
        return text;
    }

    private static bool SameLine(CaseEstimateLineRecord a, CaseEstimateLineRecord b)
    {
        if (a.Id != Guid.Empty && a.Id == b.Id)
        {
            return true;
        }
        if (a.Type != b.Type)
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
