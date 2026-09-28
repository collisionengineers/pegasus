using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Presentation;

/// <summary>
/// Releases the Case edit lease a page claimed for one action of its own — a
/// Work Centre command, or Generate report outside edit mode — once that
/// action has run. A failed release is logged, never shown: the action's own
/// outcome is what the operator sees, and an unreleased lease expires.
/// </summary>
public static partial class CaseEditLeaseRelease
{
    public static async Task ReleaseQuietlyAsync(
        IReleaseCaseEditLease releaseLease,
        ILogger logger,
        Guid caseId,
        ActionActor actor,
        CaseEditLease? lease)
    {
        ArgumentNullException.ThrowIfNull(releaseLease);
        ArgumentNullException.ThrowIfNull(logger);
        if (lease is null)
        {
            return;
        }

        try
        {
            await releaseLease.ExecuteAsync(
                new ReleaseCaseEditLeaseRequest(caseId, actor, Guid.NewGuid().ToString("N"), lease.Token),
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogReleaseFailed(logger, caseId, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Case command release_lease failed for case {CaseId}.")]
    private static partial void LogReleaseFailed(ILogger logger, Guid caseId, Exception exception);
}
