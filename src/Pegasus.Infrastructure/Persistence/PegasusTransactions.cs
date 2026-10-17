using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one owner of how a store opens its transaction (ADR-0074): read
/// committed by default, serializable only where the store names it.
/// </summary>
internal static class PegasusTransactions
{
    /// <summary>
    /// Opens the default transaction. The level is always named, so a
    /// transaction never takes whatever level its session last set.
    /// </summary>
    public static Task<IDbContextTransaction> BeginReadCommittedAsync(
        this DatabaseFacade database,
        CancellationToken cancellationToken) =>
        database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

    /// <summary>
    /// Opens a serializable transaction: the override of the default, so its
    /// callers are the list of stores that take it.
    /// </summary>
    public static Task<IDbContextTransaction> BeginSerializableAsync(
        this DatabaseFacade database,
        CancellationToken cancellationToken) =>
        database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
}
