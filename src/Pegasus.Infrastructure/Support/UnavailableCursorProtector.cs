using Pegasus.Core;

namespace Pegasus.Infrastructure.Support;

/// <summary>
/// The cursor protector for a host that serves no paged query. The Worker
/// composes the shared use cases that take one but never calls them; this
/// satisfies the composition honestly and refuses any actual use, rather
/// than giving a background host a key ring it would never need.
/// </summary>
public sealed class UnavailableCursorProtector : ICursorProtector
{
    public string Protect(string scope, string sortKey, Guid id) =>
        throw new InvalidOperationException("Cursor paging is not composed on this host.");

    public (string SortKey, Guid Id) Unprotect(string cursor, string scope) =>
        throw new InvalidOperationException("Cursor paging is not composed on this host.");
}
