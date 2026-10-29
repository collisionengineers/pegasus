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
    /// The chase interval and the five due targets, each with its value, so the
    /// range rule is stated once and checked once.
    /// </summary>
    public IReadOnlyList<(WorkflowDaySetting Setting, int Days)> DaySettings() =>
    [
        (WorkflowDaySetting.ChaseInterval, ChaseIntervalDays),
        (WorkflowDaySetting.UnidentifiedTarget, UnidentifiedTargetDays),
        (WorkflowDaySetting.TriageTarget, TriageTargetDays),
        (WorkflowDaySetting.HeldTarget, HeldTargetDays),
        (WorkflowDaySetting.ReviewTarget, ReviewTargetDays),
        (WorkflowDaySetting.AiDraftTarget, AiDraftTargetDays)
    ];
}

/// <summary>
/// One whole-day workflow setting: the request field that carries it, the name
/// its refusal uses and its allowed range (FRD-13 chase interval, FRD-17 due targets).
/// </summary>
public sealed record WorkflowDaySetting(string Field, string Label, int Minimum, int Maximum)
{
    public static WorkflowDaySetting ChaseInterval { get; } = new(
        nameof(UpdateWorkflowConfigurationRequest.ChaseIntervalDays), "Chase interval",
        CaseWorkflowConfiguration.MinimumChaseIntervalDays, CaseWorkflowConfiguration.MaximumChaseIntervalDays);

    public static WorkflowDaySetting UnidentifiedTarget { get; } = Target(
        nameof(UpdateWorkflowConfigurationRequest.UnidentifiedTargetDays), "Unidentified target");

    public static WorkflowDaySetting TriageTarget { get; } = Target(
        nameof(UpdateWorkflowConfigurationRequest.TriageTargetDays), "Triage target");

    public static WorkflowDaySetting HeldTarget { get; } = Target(
        nameof(UpdateWorkflowConfigurationRequest.HeldTargetDays), "Held decision target");

    public static WorkflowDaySetting ReviewTarget { get; } = Target(
        nameof(UpdateWorkflowConfigurationRequest.ReviewTargetDays), "Review target");

    public static WorkflowDaySetting AiDraftTarget { get; } = Target(
        nameof(UpdateWorkflowConfigurationRequest.AiDraftTargetDays), "AI draft target");

    /// <summary>The refusal for a value outside the range, or for no value at all.</summary>
    public string RangeMessage => $"{Label} must be between {Minimum} and {Maximum} days.";

    /// <summary>Whether a value is present and inside the range.</summary>
    public bool Accepts(int? days) => days >= Minimum && days <= Maximum;

    private static WorkflowDaySetting Target(string field, string label) => new(
        field, label, CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays);
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
        foreach (var (setting, days) in request.DaySettings())
        {
            if (!setting.Accepts(days))
                throw new ArgumentException(setting.RangeMessage);
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
