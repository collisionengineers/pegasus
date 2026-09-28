using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The one place that says which case-command failures are designed refusals: the
/// edit lease is gone or held by someone else, the case or operation moved on, or
/// the case is closed. The operator is told and recovers; nothing is broken, so the
/// log records the refusal without an exception payload.
/// </summary>
internal static class DesignedCaseRefusal
{
    public static bool Is(Exception exception) =>
        exception is CaseEditLeaseExpiredException
            or CaseEditLeaseConflictException
            or CaseVersionConflictException
            or CaseOperationConflictException
            or CaseTerminalMutationException;
}
