using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Core.Tests;

/// <summary>
/// The one retry of a store write that loses a concurrency race: three
/// attempts, 25 ms × attempt apart, the last failure unchanged.
/// </summary>
public sealed class StoreConcurrencyRetryTests
{
    [Fact]
    public async Task TwoDeadlocksThenASuccessAnswersTheThirdAttempt()
    {
        // EF wraps a deadlock in its save exception; the first is wrapped.
        var failures = new Queue<Exception>([new DbUpdateException("save", Sql(1205)), Sql(1205)]);
        var calls = 0;

        var result = await StoreConcurrencyRetry.ExecuteAsync(
            _ =>
            {
                calls++;
                return failures.TryDequeue(out var failure)
                    ? Task.FromException<string>(failure)
                    : Task.FromResult("saved");
            },
            CancellationToken.None);

        Assert.Equal("saved", result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task TheThirdFailureSurfacesThatAttemptsExceptionUnchanged()
    {
        var thrown = new List<Exception>();

        var surfaced = await Assert.ThrowsAsync<SqlException>(() => StoreConcurrencyRetry.ExecuteAsync<string>(
            _ =>
            {
                var failure = Sql(1205);
                thrown.Add(failure);
                return Task.FromException<string>(failure);
            },
            CancellationToken.None));

        Assert.Equal(3, thrown.Count);
        Assert.Same(thrown[2], surfaced);
    }

    [Fact]
    public async Task AFailureOutsideTheSetSurfacesAfterOneCall()
    {
        var calls = 0;
        var failure = Sql(547);

        var surfaced = await Assert.ThrowsAsync<SqlException>(() => StoreConcurrencyRetry.ExecuteAsync<string>(
            _ =>
            {
                calls++;
                return Task.FromException<string>(failure);
            },
            CancellationToken.None));

        Assert.Same(failure, surfaced);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ACallersPredicateReplacesTheDefault()
    {
        var calls = 0;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => StoreConcurrencyRetry.ExecuteAsync<string>(
            _ =>
            {
                calls++;
                return Task.FromException<string>(new DbUpdateConcurrencyException("stale"));
            },
            CancellationToken.None,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ALostRaceAnswersFromTheCommittedResult()
    {
        var calls = 0;

        var result = await StoreConcurrencyRetry.ExecuteAsync(
            _ =>
            {
                calls++;
                return Task.FromException<string>(Sql(2601));
            },
            CancellationToken.None,
            findCommitted: _ => Task.FromResult<string?>("committed"));

        Assert.Equal("committed", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancellationDuringTheDelaySurfacesWithoutAnotherAttempt()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StoreConcurrencyRetry.ExecuteAsync<string>(
            _ =>
            {
                calls++;
                cancellation.Cancel();
                return Task.FromException<string>(Sql(1205));
            },
            cancellation.Token));

        Assert.Equal(1, calls);
    }

    /// <summary>
    /// A SqlException with the given error number, built through SqlClient's
    /// internal factory as <c>SqlDiagnosticsInterceptorTests</c> builds one.
    /// </summary>
    private static SqlException Sql(int number)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type[] errorParameters =
            [typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(Exception)];
        var error = typeof(SqlError).GetConstructor(all, binder: null, errorParameters, modifiers: null)!
            .Invoke([number, (byte)0, (byte)0, "server", "sample error", string.Empty, 1, null]);
        var errors = typeof(SqlErrorCollection).GetConstructor(all, binder: null, Type.EmptyTypes, modifiers: null)!
            .Invoke([]);
        typeof(SqlErrorCollection).GetMethod("Add", all, [typeof(SqlError)])!.Invoke(errors, [error]);
        return (SqlException)typeof(SqlException)
            .GetMethod("CreateException", all, [typeof(SqlErrorCollection), typeof(string), typeof(Guid), typeof(Exception)])!
            .Invoke(null, [errors, "16.0.0", Guid.NewGuid(), null])!;
    }
}
