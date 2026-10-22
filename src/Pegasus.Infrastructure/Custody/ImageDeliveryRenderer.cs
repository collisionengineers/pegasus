using System.Runtime.InteropServices;
using Pegasus.Core.Documents;
using SkiaSharp;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// Renders one image for an MCP client with SkiaSharp: EXIF orientation
/// applied, the longest displayed edge bounded, encoded as JPEG at the highest
/// quality that fits the byte budget. When no quality fits, the edge is halved
/// and the qualities tried again, down to a floor, so a photograph always
/// arrives as a picture the client can show rather than as a refusal.
/// </summary>
/// <remarks>
/// The source is decoded once, at the scale the first edge needs, and every
/// smaller attempt resizes that bitmap, so a 6 MB photograph is read once.
/// The smallest rendering is tried first, so an image that no rendering fits
/// is refused after one draw and one encode rather than every one.
/// The codec reads the caller's bytes where they lie rather than a copy.
/// Nothing here throws at the caller: bytes that are not a decodable image,
/// an image past the decode bound, or a budget no quality fits mean "no
/// rendering" with that reason, and the caller serves the metadata and the
/// content URL instead.
/// </remarks>
internal sealed class ImageDeliveryRenderer : IRenderImageForDelivery
{
    /// <summary>
    /// Below this edge a photograph stops being evidence; give up instead. An
    /// image already smaller is tried at its own size.
    /// </summary>
    private const int MinimumEdge = 256;

    private static readonly int[] Qualities = [85, 75, 65, 55, 45];

    private static readonly SemaphoreSlim DecodeGate = new(2, 2);

    /// <summary>
    /// The smallest JPEG this encoder writes, measured once: one mid-grey
    /// pixel at the lowest quality, so every table holds a single symbol. Any
    /// other rendering carries the same markers and at least as many coded
    /// bits.
    /// </summary>
    private static readonly Lazy<int> SmallestJpeg = new(MeasureSmallestJpeg);

    private static readonly DeliveryImageRendering Undecodable =
        DeliveryImageRendering.Refused(DeliveryImageRefusal.Undecodable);

    private static readonly DeliveryImageRendering Oversized =
        DeliveryImageRendering.Refused(DeliveryImageRefusal.Oversized);

    private static readonly DeliveryImageRendering NoQualityFits =
        DeliveryImageRendering.Refused(DeliveryImageRefusal.NoQualityFits);

    public int MinimumBytes => SmallestJpeg.Value;

    public async Task<DeliveryImageRendering> RenderAsync(
        ReadOnlyMemory<byte> content,
        int longestEdge,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.IsEmpty)
        {
            return Undecodable;
        }
        if (longestEdge <= 0 || maximumBytes < MinimumBytes)
        {
            return NoQualityFits;
        }

        await DecodeGate.WaitAsync(cancellationToken);
        var pinned = MemoryMarshal.TryGetArray(content, out var segment)
            ? GCHandle.Alloc(segment.Array, GCHandleType.Pinned)
            : default;
        try
        {
            // The pinned array outlives the data and the codec, which are
            // disposed at the end of this block, before it is released.
            using var data = pinned.IsAllocated
                ? SKData.Create(pinned.AddrOfPinnedObject() + segment.Offset, segment.Count)
                : SKData.CreateCopy(content.Span);
            using var codec = SKCodec.Create(data);
            if (codec is null || (long)codec.Info.Width * codec.Info.Height <= 0)
            {
                return Undecodable;
            }
            // The decoded-pixel bound, matching the thumbnail renderer's.
            if ((long)codec.Info.Width * codec.Info.Height > DeliveryImageRendering.MaximumPixels)
            {
                return Oversized;
            }

            var origin = codec.EncodedOrigin;
            var transposed = EncodedImageOrientation.IsTransposed(origin);
            var displayedWidth = transposed ? codec.Info.Height : codec.Info.Width;
            var displayedHeight = transposed ? codec.Info.Width : codec.Info.Height;
            var displayedLongest = Math.Max(displayedWidth, displayedHeight);
            var firstEdge = Math.Min(longestEdge, displayedLongest);
            var decodedSize = codec.GetScaledDimensions(
                (float)Math.Min(1d, (double)firstEdge / displayedLongest));
            using var decoded = SKBitmap.Decode(
                codec,
                new SKImageInfo(decodedSize.Width, decodedSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (decoded is null)
            {
                return Undecodable;
            }

            // The edges halve from the first down to the floor; the last one
            // tried is the smallest.
            var lastEdge = Math.Min(MinimumEdge, firstEdge);
            var smallestEdge = firstEdge;
            while (smallestEdge / 2 >= lastEdge)
            {
                smallestEdge /= 2;
            }

            // The smallest rendering first, the smallest edge at the lowest
            // quality: when it does not fit, no rendering does, so the image
            // is refused after one draw and one encode. When it fits, the
            // search runs from the largest, and the smallest edge reuses this
            // drawing and this encoding.
            cancellationToken.ThrowIfCancellationRequested();
            var (smallestWidth, smallestHeight) = TargetSize(
                displayedWidth, displayedHeight, displayedLongest, smallestEdge);
            using var smallest = Draw(decoded, origin, transposed, smallestWidth, smallestHeight);
            if (smallest is null)
            {
                return Undecodable;
            }
            using var smallestEncoded = smallest.Encode(SKEncodedImageFormat.Jpeg, Qualities[^1]);
            if (smallestEncoded is null)
            {
                return Undecodable;
            }
            if (smallestEncoded.Size > maximumBytes)
            {
                return NoQualityFits;
            }

            for (var edge = firstEdge; edge > smallestEdge; edge /= 2)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (targetWidth, targetHeight) = TargetSize(displayedWidth, displayedHeight, displayedLongest, edge);
                using var image = Draw(decoded, origin, transposed, targetWidth, targetHeight);
                if (image is null)
                {
                    return Undecodable;
                }
                if (FirstFit(image, Qualities, maximumBytes, targetWidth, targetHeight, displayedWidth, displayedHeight)
                    is { } rendering)
                {
                    return rendering;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return FirstFit(
                    smallest,
                    Qualities.AsSpan(..^1),
                    maximumBytes,
                    smallestWidth,
                    smallestHeight,
                    displayedWidth,
                    displayedHeight)
                ?? Rendered(smallestEncoded, smallestWidth, smallestHeight, displayedWidth, displayedHeight);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Undecodable;
        }
        finally
        {
            if (pinned.IsAllocated)
            {
                pinned.Free();
            }
            DecodeGate.Release();
        }
    }

    /// <summary>The displayed size at <paramref name="edge"/>, never enlarged.</summary>
    private static (int Width, int Height) TargetSize(
        int displayedWidth,
        int displayedHeight,
        int displayedLongest,
        int edge)
    {
        var scale = Math.Min(1d, (double)edge / displayedLongest);
        return (
            Math.Max(1, (int)Math.Round(displayedWidth * scale)),
            Math.Max(1, (int)Math.Round(displayedHeight * scale)));
    }

    /// <summary>
    /// The drawing encoded at the first of <paramref name="qualities"/> that
    /// fits, undecodable when an encode fails, or <c>null</c> when none fits.
    /// </summary>
    private static DeliveryImageRendering? FirstFit(
        SKImage image,
        ReadOnlySpan<int> qualities,
        int maximumBytes,
        int width,
        int height,
        int displayedWidth,
        int displayedHeight)
    {
        foreach (var quality in qualities)
        {
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            if (encoded is null)
            {
                return Undecodable;
            }
            if (encoded.Size <= maximumBytes)
            {
                return Rendered(encoded, width, height, displayedWidth, displayedHeight);
            }
        }
        return null;
    }

    private static DeliveryImageRendering Rendered(
        SKData encoded,
        int width,
        int height,
        int displayedWidth,
        int displayedHeight) =>
        DeliveryImageRendering.Rendered(new DeliveryImage(
            encoded.ToArray(),
            "image/jpeg",
            width,
            height,
            Downscaled: width < displayedWidth || height < displayedHeight));

    private static int MeasureSmallestJpeg()
    {
        try
        {
            using var pixel = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Opaque));
            pixel.Erase(new SKColor(128, 128, 128));
            using var image = SKImage.FromBitmap(pixel);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, Qualities[^1]);
            return encoded is null ? 1 : (int)Math.Max(1, encoded.Size);
        }
        catch (Exception)
        {
            // No floor rather than a throw: a render then answers for itself.
            return 1;
        }
    }

    /// <summary>
    /// The decoded pixels, in their stored orientation, resized and then drawn
    /// through the EXIF orientation onto one opaque white surface, so JPEG
    /// output never composes a transparent source onto black.
    /// </summary>
    private static SKImage? Draw(
        SKBitmap decoded,
        SKEncodedOrigin origin,
        bool transposed,
        int targetWidth,
        int targetHeight)
    {
        using var scaled = decoded.Resize(
            new SKImageInfo(
                transposed ? targetHeight : targetWidth,
                transposed ? targetWidth : targetHeight,
                SKColorType.Rgba8888,
                SKAlphaType.Premul),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (scaled is null)
        {
            return null;
        }

        using var target = new SKBitmap(
            new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.White);
            EncodedImageOrientation.Apply(canvas, origin, targetWidth, targetHeight);
            canvas.DrawBitmap(scaled, 0, 0);
        }
        return SKImage.FromBitmap(target);
    }
}
