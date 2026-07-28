using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace CollisionDocNet.Core;

public enum BoundedInputReadStatus
{
    Complete = 0,
    ResourceLimitExceeded,
    Cancelled,
    TimedOut,
}

public readonly record struct BoundedInputReadResult(
    BoundedInputReadStatus Status,
    ImmutableArray<byte> Bytes);

public static class BoundedInputReader
{
    private const int BufferSize = 16 * 1024;

    public static async ValueTask<BoundedInputReadResult> ReadAllAsync(
        Stream source,
        ResourceBudget budget,
        ExtractionControl control)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(control);
        if (!source.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(source));
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var destination = new MemoryStream();
            while (true)
            {
                BoundedInputReadStatus? stopped = ToStoppedStatus(control.Check());
                if (stopped is not null)
                {
                    return new BoundedInputReadResult(stopped.Value, []);
                }

                int read;
                try
                {
                    using ExtractionInterruptScope interrupt = control.CreateInterruptScope();
                    read = await source.ReadAsync(
                        rented.AsMemory(0, BufferSize),
                        interrupt.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Check after the interrupted operation so simultaneous signals follow the
                    // public precedence rule: caller cancellation wins over the deadline.
                    BoundedInputReadStatus? interrupted = ToStoppedStatus(control.Check());
                    if (interrupted is not null)
                    {
                        return new BoundedInputReadResult(interrupted.Value, []);
                    }

                    throw;
                }
                if (read == 0)
                {
                    byte[] bytes = destination.ToArray();
                    return new BoundedInputReadResult(
                        BoundedInputReadStatus.Complete,
                        ImmutableCollectionsMarshal.AsImmutableArray(bytes));
                }

                if (!budget.TryCharge(ResourceKind.InputBytes, read))
                {
                    return new BoundedInputReadResult(
                        BoundedInputReadStatus.ResourceLimitExceeded,
                        []);
                }

                destination.Write(rented, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    private static BoundedInputReadStatus? ToStoppedStatus(ExtractionControlState state) =>
        state switch
        {
            ExtractionControlState.Continue => null,
            ExtractionControlState.Cancelled => BoundedInputReadStatus.Cancelled,
            ExtractionControlState.TimedOut => BoundedInputReadStatus.TimedOut,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
}
