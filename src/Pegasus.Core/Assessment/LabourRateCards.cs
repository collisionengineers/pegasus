using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public sealed record LabourRateCard(Guid Id, string Name, decimal HourlyRate, long Version);

/// <summary>
/// The rate a new imported spec starts on: a card, with or without the
/// regional uplift, or a Custom typed rate when no card's rate is the
/// document's.
/// </summary>
public sealed record ImportedSpecificationRate(LabourRateCard? Card, bool RegionalUplift, decimal? CustomRate);

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
    /// The card a repair spec started with <b>New repair spec</b> starts on:
    /// Standard (FRD-25).
    /// </summary>
    public static LabourRateCard? ForNewSpecification(IReadOnlyList<LabourRateCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        return cards.FirstOrDefault(card => card.Name == Standard);
    }

    /// <summary>
    /// The rate an imported spec starts on, from the panel or labour rate its
    /// document prints (FRD-25, R1328): the card whose rate it is, Standard
    /// before Prestige/Aluminium; else the card whose rate plus the 15 %
    /// regional uplift it is to the penny, with the uplift on; else Custom at
    /// the printed rate. A document that prints no rate, or one no card could
    /// hold (not above zero, beyond 999999 or finer than a penny), starts on
    /// Standard.
    /// </summary>
    public static ImportedSpecificationRate ForImportedSpecification(
        IReadOnlyList<LabourRateCard> cards, decimal? documentRate)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (documentRate is not { } rate || rate <= 0 || rate > 999999m || decimal.Round(rate, 2) != rate)
            return new(ForNewSpecification(cards), RegionalUplift: false, CustomRate: null);
        var offered = Names
            .Select(name => cards.FirstOrDefault(card => card.Name == name))
            .OfType<LabourRateCard>()
            .ToArray();
        if (offered.FirstOrDefault(card => card.HourlyRate == rate) is { } exact)
            return new(exact, RegionalUplift: false, CustomRate: null);
        if (offered.FirstOrDefault(card => decimal.Round(
                card.HourlyRate * EstimateDetails.RegionalUpliftFactor, 2, MidpointRounding.AwayFromZero) == rate)
            is { } uplifted)
            return new(uplifted, RegionalUplift: true, CustomRate: null);
        return new(Card: null, RegionalUplift: false, CustomRate: rate);
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
