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
/// Nothing here throws at the caller: bytes that are not a decodable image,
/// or an image past the decode bound, mean "no rendering", and the caller
/// serves the metadata and the content URL instead.
/// </remarks>
internal sealed class ImageDeliveryRenderer : IRenderImageForDelivery
{
    /// <summary>The decoded-pixel bound, matching the thumbnail renderer's.</summary>
    private const long MaximumDecodedPixels = 40_000_000;

    /// <summary>Below this edge a photograph stops being evidence; give up instead.</summary>
    private const int MinimumEdge = 256;

    private static readonly int[] Qualities = [85, 75, 65, 55, 45];

    private static readonly SemaphoreSlim DecodeGate = new(2, 2);

    public async Task<DeliveryImage?> RenderAsync(
        ReadOnlyMemory<byte> content,
        int longestEdge,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.IsEmpty || longestEdge <= 0 || maximumBytes <= 0)
        {
            return null;
        }

        await DecodeGate.WaitAsync(cancellationToken);
        try
        {
            using var data = SKData.CreateCopy(content.Span);
            using var codec = SKCodec.Create(data);
            if (codec is null
                || (long)codec.Info.Width * codec.Info.Height is <= 0 or > MaximumDecodedPixels)
            {
                return null;
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
                return null;
            }

            for (var edge = firstEdge; edge >= MinimumEdge; edge /= 2)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scale = Math.Min(1d, (double)edge / displayedLongest);
                var targetWidth = Math.Max(1, (int)Math.Round(displayedWidth * scale));
                var targetHeight = Math.Max(1, (int)Math.Round(displayedHeight * scale));
                using var image = Draw(decoded, origin, transposed, targetWidth, targetHeight);
                if (image is null)
                {
                    return null;
                }

                foreach (var quality in Qualities)
                {
                    using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality);
                    if (encoded is null)
                    {
                        return null;
                    }
                    if (encoded.Size <= maximumBytes)
                    {
                        return new DeliveryImage(
                            encoded.ToArray(),
                            "image/jpeg",
                            targetWidth,
                            targetHeight,
                            Downscaled: targetWidth < displayedWidth || targetHeight < displayedHeight);
                    }
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
        finally
        {
            DecodeGate.Release();
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
