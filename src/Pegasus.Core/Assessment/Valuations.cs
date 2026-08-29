using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

public enum ValuationSource
{
    Glasses,
    Cazana,
    EngineersValue,
}

public sealed record ValuationSourceDefinition(
    ValuationSource Source,
    string Name);

/// <summary>
/// The one valuation-source vocabulary. Persistence stores the enum name;
/// callers use this list for the operator-facing names.
/// </summary>
public static class ValuationSources
{
    public static IReadOnlyList<ValuationSourceDefinition> All { get; } =
    [
        new(ValuationSource.Glasses, "Glass's"),
        new(ValuationSource.Cazana, "Cazana"),
        new(ValuationSource.EngineersValue, "Engineer's Value"),
    ];

    public static bool IsSupported(ValuationSource source) =>
        All.Any(item => item.Source == source);
}

/// <summary>
/// The operator-entered local date and time are retained separately. When an
/// instant is needed, <see cref="LondonCalendar"/> remains the single
/// conversion owner.
/// </summary>
public sealed record ValuationDetails(
    ValuationSource Source,
    DateOnly Date,
    TimeOnly Time,
    long Mileage,
    decimal RetailValue,
    decimal TradeValue);

public sealed record CaseValuation(
    Guid ValuationId,
    Guid CaseId,
    ValuationDetails Details,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc,
    string? LastEditedBy = null,
    DateTimeOffset? LastEditedAtUtc = null);

public sealed record SaveValuationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    ValuationDetails Details)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record EditValuationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid ValuationId,
    ValuationDetails Details)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public static class ValuationPolicy
{
    public const string PolicyKey = "case-valuation";
    public const int PolicyVersion = 1;

    public static SaveValuationRequest ValidateSave(SaveValuationRequest request)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        return request with { Details = ValidateDetails(request.Details) };
    }

    public static EditValuationRequest ValidateEdit(EditValuationRequest request)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        if (request.ValuationId == Guid.Empty)
        {
            throw new ArgumentException("A valuation identifier is required.", nameof(request));
        }
        return request with { Details = ValidateDetails(request.Details) };
    }

    public static ValuationDetails ValidateDetails(ValuationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (!ValuationSources.IsSupported(details.Source))
        {
            throw new ArgumentOutOfRangeException(
                nameof(details),
                "The valuation source is not supported.");
        }
        if (details.Mileage < 0)
        {
            throw new ArgumentException("Mileage cannot be negative.", nameof(details));
        }
        Money(details.RetailValue, "retail value");
        Money(details.TradeValue, "trade value");
        return details;
    }

    public static DateTimeOffset ValuedAtUtc(ValuationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return LondonCalendar.ToUtc(details.Date.ToDateTime(details.Time));
    }

    /// <summary>
    /// The current Engineer value is the source's latest entered local date
    /// and time. Audit time and stable identity break exact ties.
    /// </summary>
    public static CaseValuation? CurrentEngineersValue(
        IEnumerable<CaseValuation> valuations)
    {
        ArgumentNullException.ThrowIfNull(valuations);
        return valuations
            .Where(item => item.Details.Source == ValuationSource.EngineersValue)
            .OrderByDescending(item => ValuedAtUtc(item.Details))
            .ThenByDescending(item => item.LastEditedAtUtc ?? item.RecordedAtUtc)
            .ThenByDescending(item => item.ValuationId)
            .FirstOrDefault();
    }

    private static void Money(decimal value, string description)
    {
        if (value < 0 || decimal.Round(value, 2) != value)
        {
            throw new ArgumentException(
                $"The {description} must be a non-negative amount with at most two decimal places.",
                nameof(value));
        }
    }
}

public interface IValuationStore
{
    Task<CaseValuation> SaveAsync(
        SaveValuationRequest request,
        CancellationToken cancellationToken);

    Task<CaseValuation> EditAsync(
        EditValuationRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CaseValuation>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

public interface ISaveValuation
{
    Task<CaseValuation> ExecuteAsync(
        SaveValuationRequest request,
        CancellationToken cancellationToken);
}

public interface IEditValuation
{
    Task<CaseValuation> ExecuteAsync(
        EditValuationRequest request,
        CancellationToken cancellationToken);
}

public interface IListCaseValuations
{
    Task<IReadOnlyList<CaseValuation>> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

public interface IGetCurrentEngineersValue
{
    Task<CaseValuation?> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

public sealed class SaveValuation(IValuationStore store) : ISaveValuation
{
    public Task<CaseValuation> ExecuteAsync(
        SaveValuationRequest request,
        CancellationToken cancellationToken) =>
        store.SaveAsync(ValuationPolicy.ValidateSave(request), cancellationToken);
}

public sealed class EditValuation(IValuationStore store) : IEditValuation
{
    public Task<CaseValuation> ExecuteAsync(
        EditValuationRequest request,
        CancellationToken cancellationToken) =>
        store.EditAsync(ValuationPolicy.ValidateEdit(request), cancellationToken);
}

public sealed class ListCaseValuations(IValuationStore store) : IListCaseValuations
{
    public Task<IReadOnlyList<CaseValuation>> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        RequireCaseId(caseId);
        return store.ListForCaseAsync(caseId, cancellationToken);
    }

    internal static void RequireCaseId(Guid caseId)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }
    }
}

public sealed class GetCurrentEngineersValue(IValuationStore store) : IGetCurrentEngineersValue
{
    public async Task<CaseValuation?> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ListCaseValuations.RequireCaseId(caseId);
        return ValuationPolicy.CurrentEngineersValue(
            await store.ListForCaseAsync(caseId, cancellationToken));
    }
}
