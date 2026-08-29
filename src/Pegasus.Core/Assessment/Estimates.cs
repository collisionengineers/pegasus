using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The editable header of one named estimate on a Case (EPIC-011 §1.9,
/// FRD-11 § Estimate VAT on the rendered report). Money is in pounds to two
/// places; rates are per hour; the VAT percentage is free per estimate (D9).
/// </summary>
public sealed record EstimateDetails(
    string Name,
    int? RepairDays,
    decimal? LabourRate,
    decimal? PaintLabourRate,
    decimal? PaintMaterials,
    decimal? OtherCosts,
    decimal VatPercent,
    string? Notes);

/// <summary>
/// The estimate editor's line operations. The persisted vocabulary stays
/// <see cref="EstimateLineCodes.Types"/>; this is the one mapping between
/// the two, so neither the screen nor a parser invents its own.
/// </summary>
public enum EstimateOperation
{
    Replace,
    Repair,
    RemoveAndRefit,
    Paint,
    Other,
}

public static class EstimateOperations
{
    /// <summary>The line type an operation lands as when nothing finer is known.</summary>
    public static string ToLineType(EstimateOperation operation) => operation switch
    {
        EstimateOperation.Replace => "new_part",
        EstimateOperation.Repair => "repair",
        EstimateOperation.RemoveAndRefit => "rnr",
        EstimateOperation.Paint => "paint_repair",
        EstimateOperation.Other => "specialist_fixed",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    public static EstimateOperation FromLineType(string lineType) => lineType switch
    {
        "new_part" => EstimateOperation.Replace,
        "repair" => EstimateOperation.Repair,
        "rnr" => EstimateOperation.RemoveAndRefit,
        "paint_new" or "paint_repair" or "paint_blend" or "paint_prep" => EstimateOperation.Paint,
        "check_labour" or "specialist_fixed" or "specialist_wu" => EstimateOperation.Other,
        _ => throw new InvalidOperationException($"Unknown estimate line type '{lineType}'."),
    };

    public static bool TryParse(string? value, out EstimateOperation operation)
    {
        switch (value?.Trim())
        {
            case "Replace": operation = EstimateOperation.Replace; return true;
            case "Repair": operation = EstimateOperation.Repair; return true;
            case "R&I" or "RemoveAndRefit": operation = EstimateOperation.RemoveAndRefit; return true;
            case "Paint": operation = EstimateOperation.Paint; return true;
            case "Other": operation = EstimateOperation.Other; return true;
            default: operation = default; return false;
        }
    }
}

/// <summary>
/// The single owner of estimate money (FRD-11 § Estimate VAT on the
/// rendered report). Parts = Σ price × quantity; Labour = Σ labour hours ×
/// labour rate; Paint = Σ paint hours × paint labour rate + paint
/// materials; Other = other costs; Subtotal = the four; VAT = Subtotal ×
/// VAT % rounded to pence; Total = Subtotal + VAT. Nothing else in the
/// application adds up an estimate.
/// </summary>
public sealed record EstimateTotals(
    decimal Parts,
    decimal Labour,
    decimal Paint,
    decimal Other,
    decimal Subtotal,
    decimal VatPercent,
    decimal Vat,
    decimal Total)
{
    public static EstimateTotals Compute(RepairSpecificationVersion estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        var details = estimate.Details;
        var parts = estimate.Lines.Sum(line => (line.Price ?? 0m) * (line.Quantity ?? 1));
        var labourHours = estimate.Lines.Sum(line => line.WorkUnits ?? 0m);
        var paintHours = estimate.Lines.Sum(line => line.PaintWorkUnits ?? 0m);
        var labour = labourHours * (details.LabourRate ?? 0m);
        var paint = paintHours * (details.PaintLabourRate ?? 0m) + (details.PaintMaterials ?? 0m);
        var other = details.OtherCosts ?? 0m;
        var subtotal = parts + labour + paint + other;
        var vat = decimal.Round(subtotal * details.VatPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new(parts, labour, paint, other, subtotal, details.VatPercent, vat, subtotal + vat);
    }
}

/// <summary>
/// Validation and actor rules for named estimates. Staff work is a staff
/// Engineer act (<see cref="RepairSpecificationPolicy.RequireEngineer"/>);
/// the Automation actor may only create or update <c>AiDraft</c> estimates
/// that cite the Estimate job they fulfil (FRD-10 § AI job and estimate
/// tools), and only a Draft is editable — an accepted estimate is duplicated,
/// never changed.
/// </summary>
public static class EstimatePolicy
{
    public const int MaximumNameLength = 100;
    public const int MaximumNotesLength = 4000;
    public const decimal DefaultVatPercent = 20m;
    public const string CopySuffix = " copy";

    public static EstimateDetails ValidateDetails(EstimateDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var name = details.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("An estimate name is required.", nameof(details));
        }
        if (name.Length > MaximumNameLength || name.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"An estimate name cannot exceed {MaximumNameLength} characters or contain control characters.",
                nameof(details));
        }
        if (details.RepairDays is < 0)
        {
            throw new ArgumentException("Repair days cannot be negative.", nameof(details));
        }
        Money(details.LabourRate, "labour rate");
        Money(details.PaintLabourRate, "paint labour rate");
        Money(details.PaintMaterials, "paint materials");
        Money(details.OtherCosts, "other costs");
        if (details.VatPercent is < 0 or > 100 || decimal.Round(details.VatPercent, 2) != details.VatPercent)
        {
            throw new ArgumentException(
                "The VAT percentage must be between 0 and 100 with at most two decimal places.",
                nameof(details));
        }
        var notes = string.IsNullOrWhiteSpace(details.Notes) ? null : details.Notes.Trim();
        if (notes is { Length: > MaximumNotesLength })
        {
            throw new ArgumentException(
                $"Estimate notes cannot exceed {MaximumNotesLength} characters.",
                nameof(details));
        }
        return details with { Name = name, Notes = notes };
    }

    public static SaveEstimateRequest ValidateSave(SaveEstimateRequest request)
    {
        CaseLifecycleRules.ValidateMutation(request);
        ArgumentNullException.ThrowIfNull(request.Lines);
        if (request.EstimateId == Guid.Empty || request.AiJobId == Guid.Empty)
        {
            throw new ArgumentException("An identifier cannot be empty when supplied.", nameof(request));
        }
        var source = RepairSpecificationPolicy.ValidateSource(request.Source);
        switch (request.Actor.Kind)
        {
            case ActorKind.Automation when source.Route != RepairSpecificationSourceRoute.AiDraft:
                throw new InvalidOperationException(
                    "The Automation actor can only save AI-draft estimates.");
            case ActorKind.Automation when request.AiJobId is null:
                throw new InvalidOperationException(
                    "An AI-draft estimate must cite the Estimate job it fulfils.");
            case ActorKind.Automation:
                break;
            default:
                RepairSpecificationPolicy.RequireEngineer(request.Actor);
                break;
        }
        return request with
        {
            Details = ValidateDetails(request.Details),
            Lines = AssessmentPolicy.NormalizeRepairSpecificationLines(request.Lines),
            Source = source,
        };
    }

    /// <summary>
    /// The job an AI draft cites must be an Estimate job on this case that
    /// the saving client currently holds (Taken under an unexpired lease).
    /// A staff Engineer editing an AI draft keeps its job reference; the
    /// job then only has to be an Estimate job on this case.
    /// </summary>
    public static void ValidateCitedJob(AiJobRecord? job, SaveEstimateRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (job is null)
        {
            throw new InvalidOperationException("The cited AI job was not found.");
        }
        if (job.Kind != AiJobKind.Estimate || job.SubjectId != request.CaseId)
        {
            throw new InvalidOperationException("The cited AI job is not an Estimate job on this case.");
        }
        if (request.Actor.Kind != ActorKind.Automation)
        {
            return;
        }
        var state = AiJobPolicy.EffectiveState(job.State, job.ExpiresAtUtc, job.LeaseExpiresAtUtc, now);
        if (state != AiJobState.Taken
            || !string.Equals(job.TakenBy, request.Actor.SubjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The cited AI job is not taken by this client.");
        }
    }

    public static void ValidateEditable(RepairSpecificationVersion estimate, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(actor);
        if (estimate.State != RepairSpecificationState.Draft)
        {
            throw new InvalidOperationException(
                "Only a draft estimate can be changed; duplicate an accepted estimate to revise it.");
        }
        if (actor.Kind == ActorKind.Automation
            && estimate.Source.Route != RepairSpecificationSourceRoute.AiDraft)
        {
            throw new InvalidOperationException(
                "The Automation actor can only change AI-draft estimates.");
        }
    }

    public static void ValidateDuplicate(RepairSpecificationVersion estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (estimate.State == RepairSpecificationState.Discarded)
        {
            throw new InvalidOperationException("A discarded estimate cannot be duplicated.");
        }
    }

    public static void ValidateDiscard(RepairSpecificationVersion estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (estimate.State == RepairSpecificationState.Accepted || estimate.IsCurrent)
        {
            throw new InvalidOperationException("An accepted estimate cannot be discarded.");
        }
        if (estimate.State == RepairSpecificationState.Discarded)
        {
            throw new InvalidOperationException("The estimate is already discarded.");
        }
    }

    /// <summary>
    /// Making an estimate Current is the Engineer's acceptance (FRD-11 § AI
    /// Job List: "Use estimate"). A Draft passes
    /// <see cref="RepairSpecificationPolicy.ValidateAcceptance"/> with the
    /// basis derived by <see cref="EstimateTotals"/>; an already accepted
    /// estimate is simply switched to.
    /// </summary>
    public static RepairCalculationBasis BasisFor(RepairSpecificationVersion estimate)
    {
        var totals = EstimateTotals.Compute(estimate);
        return new(
            totals.Labour,
            totals.Parts,
            totals.Paint,
            totals.Other,
            totals.VatPercent > 0,
            totals.Vat,
            totals.Total,
            $"{RepairSpecificationPolicy.PolicyKey}/v{RepairSpecificationPolicy.PolicyVersion}");
    }

    public static void ValidateSetCurrent(RepairSpecificationVersion estimate, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        RepairSpecificationPolicy.RequireEngineer(actor);
        switch (estimate.State)
        {
            case RepairSpecificationState.Draft:
                RepairSpecificationPolicy.ValidateAcceptance(
                    estimate with { CalculationBasis = BasisFor(estimate) },
                    actor);
                break;
            case RepairSpecificationState.Accepted:
                break;
            default:
                throw new InvalidOperationException(
                    $"A {estimate.State.ToString().ToLowerInvariant()} estimate cannot be made current.");
        }
    }

    private static void Money(decimal? value, string description)
    {
        if (value is { } amount && (amount < 0 || decimal.Round(amount, 2) != amount))
        {
            throw new ArgumentException(
                $"The {description} must be a non-negative amount with at most two decimal places.",
                nameof(value));
        }
    }
}

/// <summary>
/// Create (<see cref="EstimateId"/> null) or replace the whole content of a
/// Draft estimate: header, ordered lines and source provenance, under the
/// same actor, lease, version and operation-key guards as every case
/// mutation. <see cref="AiJobId"/> is required for an AI draft.
/// </summary>
public sealed record SaveEstimateRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid? EstimateId,
    EstimateDetails Details,
    IReadOnlyList<EstimateLineInput> Lines,
    RepairSpecificationSource Source,
    Guid? AiJobId = null)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record DuplicateEstimateRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EstimateId)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

/// <summary>The reason is the discard reason and is recorded on the estimate.</summary>
public sealed record DiscardEstimateRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EstimateId)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record SetCurrentEstimateRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EstimateId)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public interface ISaveEstimate
{
    Task<RepairSpecificationVersion> ExecuteAsync(SaveEstimateRequest request, CancellationToken cancellationToken);
}

public interface IDuplicateEstimate
{
    Task<RepairSpecificationVersion> ExecuteAsync(DuplicateEstimateRequest request, CancellationToken cancellationToken);
}

public interface IDiscardEstimate
{
    Task<RepairSpecificationVersion> ExecuteAsync(DiscardEstimateRequest request, CancellationToken cancellationToken);
}

public interface ISetCurrentEstimate
{
    Task<RepairSpecificationVersion> ExecuteAsync(SetCurrentEstimateRequest request, CancellationToken cancellationToken);
}

public interface IListCaseEstimates
{
    Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(Guid caseId, CancellationToken cancellationToken);
}

public sealed class SaveEstimate(
    IRepairSpecificationStore store,
    IAiJobStore jobs,
    TimeProvider timeProvider) : ISaveEstimate
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        SaveEstimateRequest request,
        CancellationToken cancellationToken)
    {
        var validated = EstimatePolicy.ValidateSave(request);
        if (validated.AiJobId is { } jobId)
        {
            var job = await jobs.GetAsync(jobId, cancellationToken);
            EstimatePolicy.ValidateCitedJob(job, validated, timeProvider.GetUtcNow());
        }
        return await store.SaveEstimateAsync(validated, cancellationToken);
    }
}

public sealed class DuplicateEstimate(IRepairSpecificationStore store) : IDuplicateEstimate
{
    public Task<RepairSpecificationVersion> ExecuteAsync(
        DuplicateEstimateRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        return store.DuplicateEstimateAsync(request, cancellationToken);
    }
}

public sealed class DiscardEstimate(IRepairSpecificationStore store) : IDiscardEstimate
{
    public Task<RepairSpecificationVersion> ExecuteAsync(
        DiscardEstimateRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        return store.DiscardEstimateAsync(request, cancellationToken);
    }
}

/// <summary>
/// The staff act that consumes an Estimate job's result: once the AI draft
/// is Current, the Draft-ready job it cites is confirmed Completed
/// (FRD-11 § AI Job List). A job in any other state is left as it is — the
/// Engineer's choice of estimate never depends on the ledger.
/// </summary>
public sealed class SetCurrentEstimate(
    IRepairSpecificationStore store,
    IAiJobStore jobs,
    IConfirmAiJob confirmJob,
    TimeProvider timeProvider) : ISetCurrentEstimate
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        SetCurrentEstimateRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        var current = await store.SetCurrentEstimateAsync(request, cancellationToken);
        if (current.AiJobId is not { } jobId)
        {
            return current;
        }
        var job = await jobs.GetAsync(jobId, cancellationToken);
        if (job is not null
            && AiJobPolicy.EffectiveState(job.State, job.ExpiresAtUtc, job.LeaseExpiresAtUtc, timeProvider.GetUtcNow())
                == AiJobState.DraftReady)
        {
            // Derived from the case operation key so a replayed set-current
            // replays the confirmation too; kept inside the ledger's own
            // 100-character key limit.
            var jobOperationKey = string.Concat(
                request.OperationKey.AsSpan(0, Math.Min(request.OperationKey.Length, 96)),
                ":job");
            await confirmJob.ExecuteAsync(
                new(job.JobId, job.Version, request.Actor, jobOperationKey),
                cancellationToken);
        }
        return current;
    }
}

public sealed class ListCaseEstimates(IRepairSpecificationStore store) : IListCaseEstimates
{
    public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }
        return store.ListEstimatesAsync(caseId, cancellationToken);
    }
}
