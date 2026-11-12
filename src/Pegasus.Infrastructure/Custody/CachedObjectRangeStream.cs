using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// A verified cache object read from the position it is sought to, one ranged
/// download pinned to the object's ETag at a time, so a byte-range request
/// costs its slice rather than the whole object. A download that ends before
/// <see cref="Length"/>, or a 412 because the object changed, fails the read.
/// </summary>
internal sealed class CachedObjectRangeStream(BlobClient blob, ETag etag, long length) : Stream
{
    private Stream? body;
    private long bodyPosition = -1;
    private long position;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position
    {
        get => position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            SeekOrigin.End => length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        ArgumentOutOfRangeException.ThrowIfNegative(target, nameof(offset));
        position = target;
        return position;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty || position >= length)
        {
            return 0;
        }
        if (body is null || bodyPosition != position)
        {
            await CloseBodyAsync();
            var response = await blob.DownloadStreamingAsync(
                new BlobDownloadOptions
                {
                    Range = new HttpRange(position),
                    Conditions = new BlobRequestConditions { IfMatch = etag }
                },
                cancellationToken);
            body = response.Value.Content;
            bodyPosition = position;
        }
        var wanted = (int)Math.Min(buffer.Length, length - position);
        var read = await body.ReadAsync(buffer[..wanted], cancellationToken);
        if (read == 0)
        {
            throw new InvalidDataException("The cached object ended before its verified length.");
        }
        position += read;
        bodyPosition = position;
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        await CloseBodyAsync();
        await base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            body?.Dispose();
            body = null;
        }
        base.Dispose(disposing);
    }

    private async ValueTask CloseBodyAsync()
    {
        if (body is not null)
        {
            await body.DisposeAsync();
            body = null;
        }
        bodyPosition = -1;
    }
}
