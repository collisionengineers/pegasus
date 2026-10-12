namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The external-work row the router read to choose a handler, kept for the
/// custody processor in the same scope so the row is read once by id. The
/// processor's claim still compares the state, lease token and lease expiry it
/// observed, so a row that has since moved on is re-read, never trusted.
/// </summary>
internal sealed class PrefetchedExternalWork
{
    private ExternalWorkItemEntity? _row;

    public void Offer(ExternalWorkItemEntity row) => _row = row;

    /// <summary>The offered row if it is the one asked for; it is handed over once.</summary>
    public ExternalWorkItemEntity? Take(Guid workItemId)
    {
        var row = _row;
        _row = null;
        return row?.Id == workItemId ? row : null;
    }
}
