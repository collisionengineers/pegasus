using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// Reads receipts through the queries it wraps and counts the whole-receipt
/// reads (<see cref="GetAsync"/>), the read a processed item pays each time it
/// reloads the receipt. The other members keep their interface defaults.
/// </summary>
internal sealed class CountingReceiptQueries(IIntakeReceiptQueries inner) : IIntakeReceiptQueries
{
    private int getCount;

    public int GetCount => Volatile.Read(ref getCount);

    public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref getCount);
        return inner.GetAsync(id, cancellationToken);
    }
}
