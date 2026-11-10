using Pegasus.Core.Documents;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Web.PhotoCapture;

/// <summary>
/// A staged upload's parts read back in order as one forward-only stream,
/// each part streamed from staging as it is read, never held whole. Each part
/// must have the length <see cref="CaptureLimits.PartLength"/> gives it; a
/// missing or misfit part is the same file sent differently, an item conflict.
/// </summary>
public sealed class ConcatenatedPartsStream : Stream
{
    private readonly IDocumentUploadStaging staging;
    private readonly Guid uploadId;
    private readonly long totalBytes;
    private readonly int partCount;
    private int nextPart = 1;
    private Stream? current;
    private long currentRemaining;
    private long position;

    public ConcatenatedPartsStream(IDocumentUploadStaging staging, Guid uploadId, long totalBytes)
    {
        ArgumentNullException.ThrowIfNull(staging);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalBytes);
        this.staging = staging;
        this.uploadId = uploadId;
        this.totalBytes = totalBytes;
        partCount = CaptureLimits.PartCount(totalBytes);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => totalBytes;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (true)
        {
            if (current is null)
            {
                if (nextPart > partCount)
                {
                    return 0;
                }

                current = await staging.OpenPartAsync(uploadId, nextPart, cancellationToken) ?? throw Misfit();
                currentRemaining = CaptureLimits.PartLength(totalBytes, nextPart);
                nextPart++;
            }

            if (currentRemaining == 0)
            {
                // The part must end exactly where its length says.
                if (await current.ReadAsync(new byte[1], cancellationToken) != 0)
                {
                    throw Misfit();
                }

                await CloseCurrentAsync();
                continue;
            }

            var count = await current.ReadAsync(buffer[..(int)Math.Min(buffer.Length, currentRemaining)], cancellationToken);
            if (count == 0)
            {
                throw Misfit();
            }

            currentRemaining -= count;
            position += count;
            return count;
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            current?.Dispose();
            current = null;
        }

        base.Dispose(disposing);
    }

    private async ValueTask CloseCurrentAsync()
    {
        if (current is not null)
        {
            await current.DisposeAsync();
            current = null;
        }
    }

    private static PhotoCaptureRefusalException Misfit() =>
        new(PhotoCaptureRefusals.ItemConflict, DocumentUploadRefusals.LengthMismatch);
}
