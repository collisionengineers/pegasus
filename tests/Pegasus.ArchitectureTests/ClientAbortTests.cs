using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Pegasus.Web;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// A request whose caller hung up is not an application error: one classifier
/// says which exceptions are only that, and the handler ends such a request as
/// a quiet 499 instead of letting the error page re-execute under a cancelled
/// token and log it as an unhandled exception.
/// </summary>
public sealed class ClientAbortTests
{
    private const string SqlClientWording = "Operation cancelled by user.";

    [Fact]
    public void ACancellationOrSqlClientsCancelledCommandWordingIsAClientAbort()
    {
        Exception[] cancellations =
        [
            new OperationCanceledException(),
            new TaskCanceledException(),
            new InvalidOperationException(SqlClientWording),
            new DatabaseException(
                "A severe error occurred on the current command.  The results, if any, should be discarded.\r\n"
                + SqlClientWording)
        ];

        Assert.All(cancellations, exception => Assert.True(ClientAbort.IsCancellation(exception), exception.Message));
    }

    [Fact]
    public void NothingElseIsAClientAbort()
    {
        Exception[] others =
        [
            new KeyNotFoundException(),
            new IOException("The client reset the request stream."),
            new InvalidOperationException("The sequence contains no elements."),
            new DatabaseException("Login failed for user."),
            new ArgumentException(SqlClientWording),
            // Only the exception itself is asked, never one it wraps.
            new InvalidOperationException("The read failed.", new InvalidOperationException(SqlClientWording)),
            new AggregateException(new OperationCanceledException())
        ];

        Assert.All(others, exception => Assert.False(ClientAbort.IsCancellation(exception), exception.ToString()));
    }

    [Fact]
    public async Task AnAbortedRequestIsEndedAsAQuiet499WithNoStatusPage()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        var statusPages = new StatusPages();
        context.Features.Set<IStatusCodePagesFeature>(statusPages);

        var handled = await new ClientAbortExceptionHandler().TryHandleAsync(
            context, new InvalidOperationException(SqlClientWording), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(499, context.Response.StatusCode);
        Assert.False(statusPages.Enabled);
    }

    [Fact]
    public async Task AResponseThatHasStartedKeepsItsStatus()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Features.Set<IHttpResponseFeature>(new StartedResponse());

        var handled = await new ClientAbortExceptionHandler().TryHandleAsync(
            context, new OperationCanceledException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task ALiveRequestOrAnotherExceptionIsLeftToTheErrorPage()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var live = new DefaultHttpContext();
        var liveStatusPages = new StatusPages();
        live.Features.Set<IStatusCodePagesFeature>(liveStatusPages);
        var other = new DefaultHttpContext { RequestAborted = aborted.Token };
        var otherStatusPages = new StatusPages();
        other.Features.Set<IStatusCodePagesFeature>(otherStatusPages);
        var handler = new ClientAbortExceptionHandler();

        Assert.False(await handler.TryHandleAsync(
            live, new InvalidOperationException(SqlClientWording), CancellationToken.None));
        Assert.False(await handler.TryHandleAsync(
            other, new KeyNotFoundException(), CancellationToken.None));

        Assert.Equal(200, live.Response.StatusCode);
        Assert.Equal(200, other.Response.StatusCode);
        Assert.True(liveStatusPages.Enabled);
        Assert.True(otherStatusPages.Enabled);
    }

    /// <summary>A database error carrying the message SqlClient gives it.</summary>
    private sealed class DatabaseException(string message) : DbException(message);

    private sealed class StatusPages : IStatusCodePagesFeature
    {
        public bool Enabled { get; set; } = true;
    }

    private sealed class StartedResponse : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
