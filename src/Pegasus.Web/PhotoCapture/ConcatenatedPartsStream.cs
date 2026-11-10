using Pegasus.Core.Documents;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Web.PhotoCapture;

/// <summary>
/// A staged upload's parts read back in order as one forward-only stream,
/// one part in memory at a time. Each part must have the length
/// <see cref="CaptureLimits.PartLength"/> gives it; a missing or misfit part
/// is the same file sent differently, an item conflict.
/// </summary>
public sealed class ConcatenatedPartsStream : Stream
{
    private readonly IDocumentUploadStaging staging;
    private readonly Guid uploadId;
    private readonly long totalBytes;
    private readonly int partCount;
    private int nextPart = 1;
    private ReadOnlyMemory<byte> current = ReadOnlyMemory<byte>.Empty;
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

        while (current.IsEmpty)
        {
            if (nextPart > partCount)
            {
                return 0;
            }

            var part = await staging.ReadPartAsync(uploadId, nextPart, cancellationToken);
            if (part is not { } bytes || bytes.Length != CaptureLimits.PartLength(totalBytes, nextPart))
            {
                throw new PhotoCaptureRefusalException(
                    PhotoCaptureRefusals.ItemConflict,
                    DocumentUploadRefusals.LengthMismatch);
            }

            current = bytes;
            nextPart++;
        }

        var count = Math.Min(buffer.Length, current.Length);
        current[..count].CopyTo(buffer);
        current = current[count..];
        position += count;
        return count;
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
}
