using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public sealed record LabourRateCard(Guid Id, string Name, decimal HourlyRate, long Version);

public sealed class LabourRateCardConflictException(string message) : InvalidOperationException(message);

public sealed record SaveLabourRateCardRequest(
    Guid Id, decimal HourlyRate, long ExpectedVersion,
    ActionActor Actor, string? Reason, string OperationKey);

public interface ILabourRateCardStore
{
    Task<IReadOnlyList<LabourRateCard>> ListAsync(CancellationToken cancellationToken);
    Task<LabourRateCard> SaveAsync(SaveLabourRateCardRequest request, CancellationToken cancellationToken);
}

public sealed class LabourRateCardAdministration(ILabourRateCardStore store)
{
    public const string Standard = "Standard";
    public const string PrestigeAluminium = "Prestige/Aluminium";

    /// <summary>The two built-in cards, in the order they are offered (operator, 13 October 2026).</summary>
    public static readonly IReadOnlyList<string> Names = [Standard, PrestigeAluminium];

    public Task<IReadOnlyList<LabourRateCard>> ListAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return store.ListAsync(cancellationToken);
    }

    public Task<LabourRateCard> SaveAsync(SaveLabourRateCardRequest request, CancellationToken cancellationToken) =>
        store.SaveAsync(Validate(request), cancellationToken);

    /// <summary>
    /// The card a new repair spec starts on: Standard. A source document's
    /// own rate never picks a card (FRD-25).
    /// </summary>
    public static LabourRateCard? ForNewSpecification(IReadOnlyList<LabourRateCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        return cards.FirstOrDefault(card => card.Name == Standard);
    }

    public static SaveLabourRateCardRequest Validate(SaveLabourRateCardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageWorkflowConfiguration);
        if (request.Id == Guid.Empty || request.ExpectedVersion < 1)
            throw new ArgumentException("The labour-rate card has changed. Reload it and try again.");
        if (request.HourlyRate < 0 || request.HourlyRate > 999999m || decimal.Round(request.HourlyRate, 2) != request.HourlyRate)
            throw new ArgumentException("Enter an hourly rate from 0 to 999999 with at most two decimal places.");
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > 1000 })
            throw new ArgumentException("A reason cannot exceed 1000 characters.");
        if (string.IsNullOrWhiteSpace(request.OperationKey) || request.OperationKey.Length > 100)
            throw new ArgumentException("Submit the current form.");
        return request with { Reason = reason };
    }
}
