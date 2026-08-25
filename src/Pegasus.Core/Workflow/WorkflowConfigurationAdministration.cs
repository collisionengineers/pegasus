using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

public sealed record UpdateWorkflowConfigurationRequest(
    bool RequireStaffInstructionReviewBeforeEngineerAssignment,
    bool RequireStaffImageReviewBeforeEngineerAssignment,
    int ExpectedVersion,
    ActionActor Actor,
    string Reason,
    string OperationKey);

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
        if (request.ExpectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected workflow configuration version must be positive.");
        }

        return _store.UpdateAsync(
            request with
            {
                Reason = RequireText(
                    request.Reason,
                    1000,
                    "A configuration-change reason is required."),
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

public sealed class WorkflowConfigurationVersionConflictException(
    int expectedVersion,
    int currentVersion)
    : InvalidOperationException("The workflow configuration changed before this request was saved.")
{
    public int ExpectedVersion { get; } = expectedVersion;

    public int CurrentVersion { get; } = currentVersion;
}

public sealed class WorkflowConfigurationOperationConflictException()
    : InvalidOperationException("The operation key has already been used for another workflow configuration request.");
