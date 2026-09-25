using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

public sealed record UpdateWorkflowConfigurationRequest(
    int ExpectedVersion,
    ActionActor Actor,
    string OperationKey)
{
    public bool RequireInstructions { get; init; } = true;
    public bool RequireImages { get; init; } = true;
    public int ChaseIntervalDays { get; init; } = 7;
    public int UnidentifiedTargetDays { get; init; }
    public int TriageTargetDays { get; init; } = 1;
    public int HeldTargetDays { get; init; } = 7;
    public int ReviewTargetDays { get; init; } = 1;
    public int AiDraftTargetDays { get; init; } = 1;

    /// <summary>
    /// The five due targets with the name each validation message uses, so the
    /// shared 0–365 range rule is stated once.
    /// </summary>
    public IEnumerable<(string Name, int Days)> TargetDays()
    {
        yield return ("Unidentified target", UnidentifiedTargetDays);
        yield return ("Triage target", TriageTargetDays);
        yield return ("Held decision target", HeldTargetDays);
        yield return ("Review target", ReviewTargetDays);
        yield return ("AI draft target", AiDraftTargetDays);
    }
}

public interface IWorkflowConfigurationStore : ICaseWorkflowConfiguration
{
    Task<CaseWorkflowConfiguration> UpdateAsync(
        UpdateWorkflowConfigurationRequest request,
        CancellationToken cancellationToken);
}

public sealed class GetWorkflowConfiguration(IWorkflowConfigurationStore store)
{
    private readonly IWorkflowConfigurationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<CaseWorkflowConfiguration> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageWorkflowConfiguration);
        return _store.GetCurrentAsync(cancellationToken);
    }
}

public sealed class UpdateWorkflowConfiguration(IWorkflowConfigurationStore store)
{
    private readonly IWorkflowConfigurationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<CaseWorkflowConfiguration> ExecuteAsync(
        UpdateWorkflowConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(
            request.Actor,
            StaffAccessRight.ManageWorkflowConfiguration);
        if (request.ChaseIntervalDays is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(request), "Chase interval must be between 1 and 365 days.");
        foreach (var (name, days) in request.TargetDays())
        {
            if (days is < CaseWorkflowConfiguration.MinimumTargetDays or > CaseWorkflowConfiguration.MaximumTargetDays)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    $"{name} must be between {CaseWorkflowConfiguration.MinimumTargetDays} and {CaseWorkflowConfiguration.MaximumTargetDays} days.");
            }
        }

        if (request.ExpectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected workflow configuration version must be positive.");
        }

        return _store.UpdateAsync(
            request with
            {
                OperationKey = RequireText(
                    request.OperationKey,
                    100,
                    "An operation key is required.")
            },
            cancellationToken);
    }

    private static string RequireText(string value, int maximumLength, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }
}

public sealed class WorkflowConfigurationVersionConflictException()
    : InvalidOperationException("The workflow configuration changed before this request was saved.");

public sealed class WorkflowConfigurationOperationConflictException()
    : InvalidOperationException("The operation key has already been used for another workflow configuration request.");
