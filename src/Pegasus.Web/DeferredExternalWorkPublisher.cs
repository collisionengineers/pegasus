using Pegasus.Core.Custody;

namespace Pegasus.Web;

/// <summary>
/// The Web host's post-commit external-work publication: the work is already
/// committed, so the claim, the queue send and the mark follow the response in
/// the request's own scope instead of holding the page (R315). A publication
/// that fails is logged and left to the recovery sweep, which claims anything
/// still pending; it never reaches the page. Outside a request (a background
/// piece of Web work) there is no response to follow, so the publication is
/// immediate. The Worker composes the immediate publisher.
/// </summary>
public sealed partial class DeferredExternalWorkPublisher(
    DispatchPendingExternalWork immediate,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DeferredExternalWorkPublisher> logger) : ICommittedExternalWorkPublisher
{
    private readonly List<Guid> pending = [];

    public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (httpContextAccessor.HttpContext is not { } httpContext)
        {
            return immediate.PublishAsync(workItemId, cancellationToken);
        }

        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("An external work item identifier is required.", nameof(workItemId));
        }

        if (pending.Count == 0)
        {
            httpContext.Response.OnCompleted(PublishPendingAsync);
        }

        pending.Add(workItemId);
        return Task.CompletedTask;
    }

    private async Task PublishPendingAsync()
    {
        foreach (var workItemId in pending)
        {
            try
            {
                await immediate.PublishAsync(workItemId, CancellationToken.None);
            }
            catch (Exception exception)
            {
                LogDeferredPublishFailed(logger, workItemId, exception);
            }
        }
    }

    [LoggerMessage(
        EventId = 1242,
        Level = LogLevel.Warning,
        Message = "Publishing external work {WorkItemId} after the response failed; the recovery sweep will publish it")]
    private static partial void LogDeferredPublishFailed(ILogger logger, Guid workItemId, Exception exception);
}
