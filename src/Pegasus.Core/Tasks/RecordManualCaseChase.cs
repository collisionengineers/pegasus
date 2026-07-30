using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tasks;

public sealed class RecordManualCaseChase(
    ICaseDueWorkStore store,
    ICaseWorkflowQueries workflowQueries,
    TimeProvider timeProvider) : IRecordManualCaseChase
{
    private readonly ICaseDueWorkStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseWorkflowQueries _workflowQueries = workflowQueries ?? throw new ArgumentNullException(nameof(workflowQueries));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<CaseDueWork> ExecuteAsync(ManualChaseRecord request, CancellationToken cancellationToken)
    {
        Validate(request, _timeProvider.GetUtcNow());
        var workflow = await _workflowQueries.GetAsync(request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        if (workflow.State != CaseLifecycleState.NotReady
            && !await _workflowQueries.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("A manual chase can be recorded only while a case is Not ready.");
        }

        return await _store.RecordManualChaseAsync(request, cancellationToken);
    }

    private static void Validate(ManualChaseRecord request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        if (request.ExpectedCaseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The expected case version cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        RequireText(request.EditLeaseToken, "An active edit lease token is required.", 128, nameof(request));
        RequireText(request.OperationKey, "An operation key is required.", 100, nameof(request));
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
        RequireText(request.Channel, "A chase channel is required.", 100, nameof(request));
        RequireText(request.TargetPartyOrAddress, "A chase target is required.", 500, nameof(request));
        RequireText(request.Outcome, "A chase outcome is required.", 500, nameof(request));
        if (request.Note is { Length: > 1000 })
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The chase note cannot exceed 1000 characters.");
        }

        if (request.AttemptedAtUtc == default
            || request.AttemptedAtUtc.Offset != TimeSpan.Zero
            || request.AttemptedAtUtc > now)
        {
            throw new ArgumentException(
                "The chase attempt time must be a non-future UTC instant.",
                nameof(request));
        }
    }

    private static void RequireText(string value, string message, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
