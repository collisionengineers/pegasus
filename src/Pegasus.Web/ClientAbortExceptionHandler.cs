using Microsoft.AspNetCore.Diagnostics;

namespace Pegasus.Web;

/// <summary>
/// Ends a request whose caller hung up when it surfaces as a cancellation the
/// framework does not already recognise (see <see cref="ClientAbort"/>). The
/// answer is a 499 nobody reads: the error page does not re-execute under the
/// cancelled request token, so nothing is logged as an unhandled exception and
/// the status pages middleware, which wraps this handler, writes no page.
/// Anything else, a live request included, is left to the error page.
/// </summary>
internal sealed class ClientAbortExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!httpContext.RequestAborted.IsCancellationRequested || !ClientAbort.IsCancellation(exception))
        {
            return ValueTask.FromResult(false);
        }

        if (!httpContext.Response.HasStarted)
        {
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }

        if (httpContext.Features.Get<IStatusCodePagesFeature>() is { } statusPages)
        {
            statusPages.Enabled = false;
        }

        return ValueTask.FromResult(true);
    }
}
