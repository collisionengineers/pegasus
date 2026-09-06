using System.Globalization;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

public enum ValuationSource
{
    Glasses,
    Cazana,
    EngineersValue,
    AiMarketResearch,
    Brego,
    SuperCap,
}

/// <summary>
/// The one question asked of the valuation-source vocabulary. The enum above
/// is that vocabulary: persistence stores its member name and generates the
/// table's check constraint from it, so there is no second list. The
/// operator-facing name of a source belongs to the presentation layer, beside
/// every other source label.
/// </summary>
public static class ValuationSources
{
    public static bool IsSupported(ValuationSource source) => Enum.IsDefined(source);
}

/// <summary>
/// One recorded valuation. The operator-entered local date and time are
/// retained as entered, and are the order the Valuations table is read in.
/// An <see cref="ValuationSource.EngineersValue"/> row additionally writes
/// the confirmed <c>assessment.values.engineer</c> field, which stays the one
/// owner of the Engineer's Value the product consumes.
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
        RequireActor(request.Actor, request.Details);
        return request with { Details = ValidateDetails(request.Details) };
    }

    public static EditValuationRequest ValidateEdit(EditValuationRequest request)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RequireActor(request.Actor, request.Details);
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

        // An Engineer's Value row is the entry surface of
        // assessment.values.engineer, so a row that cannot be written to that
        // field is refused here rather than persisted and silently dropped.
        EngineersValueField(details);
        return details;
    }

    public static ValuationDetails ValidateAutomationMarketResearch(ValuationDetails details)
    {
        details = ValidateDetails(details);
        if (details.Source != ValuationSource.AiMarketResearch)
        {
            throw new InvalidOperationException(
                "Automation may record only an AI market research valuation.");
        }

        return details;
    }

    /// <summary>
    /// Recording or correcting a valuation is ordinary casework. An
    /// Engineer's Value row is not: it carries the confirmed
    /// <c>assessment.values.engineer</c> professional finding, so it takes
    /// that field's own authority rule from its single owner.
    /// </summary>
    private static void RequireActor(ActionActor actor, ValuationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (actor.Kind != ActorKind.Staff)
        {
            throw new InvalidOperationException(
                "Valuations entered through the staff save and edit actions require a staff actor.");
        }
        if (details.Source == ValuationSource.EngineersValue)
        {
            AssessmentPolicy.RequireFindingConfirmationAuthority(actor);
        }
    }

    /// <summary>
    /// The confirmed <c>assessment.values.engineer</c> value an Engineer's
    /// Value row carries: its retail figure, which is the pre-accident value
    /// a settlement is measured from (FRD-11 total-loss report). Null for
    /// every other source, which writes no assessment field.
    /// </summary>
    public static string? EngineersValueField(ValuationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return details.Source == ValuationSource.EngineersValue
            ? AssessmentPolicy.NormalizeFieldValue(
                AssessmentVocabulary.ValueEngineer,
                details.RetailValue.ToString(CultureInfo.InvariantCulture))
            : null;
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
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return store.ListForCaseAsync(caseId, cancellationToken);
    }
}
