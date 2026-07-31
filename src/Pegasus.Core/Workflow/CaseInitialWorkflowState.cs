using Pegasus.Core.Cases;

namespace Pegasus.Core.Workflow;

/// <summary>
/// The only translation from the immutable acceptance outcome to the editable lifecycle.
/// Intake persistence must create this initial lifecycle state in the same transaction as
/// identity allocation, the accepted source link, custody work, and permanent history.
/// </summary>
public static class CaseInitialWorkflowState
{
    public static CaseLifecycleState From(CaseInitialState initialState) => initialState switch
    {
        CaseInitialState.NotReady => CaseLifecycleState.NotReady,
        CaseInitialState.Review => CaseLifecycleState.Review,
        _ => throw new ArgumentOutOfRangeException(nameof(initialState), initialState, "The case initial state is invalid.")
    };
}
