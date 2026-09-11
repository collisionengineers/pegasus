using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public sealed record LabourRateCard(Guid Id, string Name, decimal HourlyRate, bool Enabled, long Version);

public sealed class LabourRateCardConflictException(string message) : InvalidOperationException(message);

public sealed record SaveLabourRateCardRequest(
    Guid Id, string Name, decimal HourlyRate, bool Enabled, long ExpectedVersion,
    ActionActor Actor, string? Reason, string OperationKey, string EditLeaseToken);

public interface ILabourRateCardStore
{
    Task<IReadOnlyList<LabourRateCard>> ListAsync(CancellationToken cancellationToken);
    Task<LabourRateCard> SaveAsync(SaveLabourRateCardRequest request, CancellationToken cancellationToken);
}

public sealed class LabourRateCardAdministration(ILabourRateCardStore store)
{
    public Task<IReadOnlyList<LabourRateCard>> ListAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return store.ListAsync(cancellationToken);
    }

    public Task<LabourRateCard> SaveAsync(SaveLabourRateCardRequest request, CancellationToken cancellationToken) =>
        store.SaveAsync(Validate(request), cancellationToken);

    public static SaveLabourRateCardRequest Validate(SaveLabourRateCardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageWorkflowConfiguration);
        if (request.Id == Guid.Empty || request.ExpectedVersion < 0)
            throw new ArgumentException("The labour-rate card has changed. Reload it and try again.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            throw new ArgumentException("Enter a name of up to 200 characters.");
        if (request.HourlyRate < 0 || request.HourlyRate > 999999m || decimal.Round(request.HourlyRate, 2) != request.HourlyRate)
            throw new ArgumentException("Enter an hourly rate from 0 to 999999 with at most two decimal places.");
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > 1000 })
            throw new ArgumentException("A reason cannot exceed 1000 characters.");
        if (string.IsNullOrWhiteSpace(request.OperationKey) || request.OperationKey.Length > 100)
            throw new ArgumentException("Submit the current form.");
        return request with { Name = request.Name.Trim(), Reason = reason };
    }
}
