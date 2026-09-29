using System.Runtime.InteropServices;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Custody;
using SkiaSharp;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// What the page prints for one report image: the confirmed bytes decoded
/// once, shown the way the crop editor showed them (EXIF orientation first,
/// then the Engineer's whole-turn rotation), and the persisted crop taken as
/// fractions of that rotated source. An image for a slot is then trimmed
/// about its centre to the slot's shape, so it fills the slot; an image for a
/// page of its own keeps its whole crop. Neither is ever enlarged. Bytes that
/// do not decode fail the render closed: the report never prints a
/// placeholder.
/// </summary>
/// <remarks>
/// Memory is bounded by the print, not the source. The decoded bitmap is
/// drawn straight into the output square through one canvas transform
/// (orientation, quarter-turn, crop and fit together), so no rotated copy of
/// the source is ever made. A PNG cannot be scale-decoded, so a large one
/// is read a row at a time and averaged down into a bitmap of about the
/// output's size; nothing refuses an image for its size.
/// </remarks>
internal static class ReportPhotoPreparation
{
    /// <summary>
    /// A slot's width in pixels: 80.4 mm at 300 dpi needs 950, so neither
    /// the grid nor page 1 prints a soft image, and each embeds at most 1000
    /// px across whatever the source size.
    /// </summary>
    internal const int SlotPixels = 1000;

    /// <summary>The longest edge of an image that prints on a page of its own.</summary>
    internal const int FullPagePixels = 2000;

    /// <summary>
    /// The pixel count above which a PNG is read by scanline instead of
    /// decoded whole: a square of the full-page class, 2000 by 2000, so a PNG
    /// larger than any print square never decodes whole. A smaller PNG decodes
    /// whole, which is quicker and at most 16 MB of RGBA.
    /// </summary>
    internal const long ScanlineFromPixels = (long)FullPagePixels * FullPagePixels;

    private const int PhotoJpegQuality = 85;
    private const int CancellationRowInterval = 64;

    /// <summary>The prepared JPEG for one image, ready to embed.</summary>
    /// <param name="content">The verified source bytes.</param>
    /// <param name="reference">The image's name, for the refusal that names it.</param>
    /// <param name="rotation">The Engineer's whole-turn rotation.</param>
    /// <param name="crop">The Engineer's crop, as fractions of the rotated source.</param>
    /// <param name="slotHeight">The slot's height in millimetres, or null for a page of its own.</param>
    /// <param name="cancellationToken">Checked while a large image is read.</param>
    /// <param name="scanlineFromPixels">Where a PNG starts being read by scanline.</param>
    internal static byte[] Prepare(
        byte[] content,
        string reference,
        CaseAssetRotation rotation,
        CaseAssetCrop crop,
        float? slotHeight,
        CancellationToken cancellationToken,
        long scanlineFromPixels = ScanlineFromPixels)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(crop);
        using var data = Wrap(content, reference);
        using var codec = SKCodec.Create(data)
            ?? throw new ReportRenderRejectedException($"Report image '{reference}' could not be decoded.");
        var info = codec.Info;
        var origin = codec.EncodedOrigin;
        var transposed = EncodedImageOrientation.IsTransposed(origin);
        var quarterTurn = rotation is CaseAssetRotation.Clockwise90 or CaseAssetRotation.Clockwise270;
        var shape = slotHeight is { } height ? ReportChrome.SlotWidth / height : (float?)null;

        // The whole geometry is in the source's own pixels: the displayed
        // source, the rotated source the crop is a fraction of, the part of
        // it the print keeps about the crop's centre, and the output it fits.
        var shownWidth = transposed ? info.Height : info.Width;
        var shownHeight = transposed ? info.Width : info.Height;
        var rotatedWidth = quarterTurn ? shownHeight : shownWidth;
        var rotatedHeight = quarterTurn ? shownWidth : shownHeight;
        var cropWidth = Math.Max(1f, (float)crop.Width * rotatedWidth);
        var cropHeight = Math.Max(1f, (float)crop.Height * rotatedHeight);
        var kept = Kept(cropWidth, cropHeight, shape);
        var left = (float)crop.Left * rotatedWidth + (cropWidth - kept.Width) / 2f;
        var top = (float)crop.Top * rotatedHeight + (cropHeight - kept.Height) / 2f;
        var scale = Scale(kept, shape);
        var outputWidth = Math.Max(1, (int)Math.Round(kept.Width * scale));
        var outputHeight = Math.Max(1, (int)Math.Round(kept.Height * scale));

        // Decode no larger than the print needs from the part it keeps. The
        // codec picks the nearest scale it supports, and the next one up when
        // that would leave the print short of its pixels.
        var decodedSize = codec.GetScaledDimensions(scale);
        if (decodedSize.Width < info.Width * scale)
        {
            decodedSize = codec.GetScaledDimensions(Math.Min(1f, scale + 0.125f));
        }

        cancellationToken.ThrowIfCancellationRequested();
        SKBitmap? decoded = null;
        try
        {
            // The part of the stored image the decoded bitmap stands for.
            var region = new SKRectI(0, 0, info.Width, info.Height);
            if (decodedSize.Width == info.Width
                && decodedSize.Height == info.Height
                && (long)info.Width * info.Height > scanlineFromPixels
                && CanReadByScanline(codec, content))
            {
                var wanted = StoredRegion(
                    origin, rotation, shownWidth, shownHeight, rotatedWidth, rotatedHeight,
                    new SKRect(left, top, left + kept.Width, top + kept.Height), info.Width, info.Height);
                decoded = ReadRegion(codec, wanted, scale, reference, cancellationToken);
                if (decoded is not null)
                {
                    region = wanted;
                }
            }
            decoded ??= SKBitmap.Decode(
                codec,
                new SKImageInfo(decodedSize.Width, decodedSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul))
                ?? throw new ReportRenderRejectedException($"Report image '{reference}' could not be decoded.");

            cancellationToken.ThrowIfCancellationRequested();
            return Print(
                decoded, region, origin, rotation, shownWidth, shownHeight, rotatedWidth, rotatedHeight,
                left, top, kept, outputWidth, outputHeight, reference);
        }
        finally
        {
            decoded?.Dispose();
        }
    }

    /// <summary>
    /// Draws the decoded bitmap into the output through one transform that
    /// composes the crop editor's own: the stored pixels turned to the
    /// displayed image, the Engineer's rotation, then the kept part of the
    /// rotated source fitted to the output. The bitmap stands for
    /// <paramref name="region"/> of the stored image, which is all of it
    /// unless a large PNG was read by scanline.
    /// </summary>
    private static byte[] Print(
        SKBitmap decoded,
        SKRectI region,
        SKEncodedOrigin origin,
        CaseAssetRotation rotation,
        int shownWidth,
        int shownHeight,
        int rotatedWidth,
        int rotatedHeight,
        float left,
        float top,
        SKSize kept,
        int outputWidth,
        int outputHeight,
        string reference)
    {
        // A read-only bitmap is shared with its image rather than copied.
        decoded.SetImmutable();
        using var source = SKImage.FromBitmap(decoded);
        using var output = new SKBitmap(new SKImageInfo(outputWidth, outputHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(output))
        {
            canvas.Clear(SKColors.White);
            canvas.Scale(outputWidth / kept.Width, outputHeight / kept.Height);
            canvas.Translate(-left, -top);
            canvas.Translate(rotatedWidth / 2f, rotatedHeight / 2f);
            canvas.RotateDegrees((int)rotation);
            canvas.Translate(-shownWidth / 2f, -shownHeight / 2f);
            EncodedImageOrientation.Apply(canvas, origin, shownWidth, shownHeight);
            canvas.Translate(region.Left, region.Top);
            canvas.Scale(region.Width / (float)decoded.Width, region.Height / (float)decoded.Height);
            canvas.DrawImage(source, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        using var image = SKImage.FromBitmap(output);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, PhotoJpegQuality)
            ?? throw new ReportRenderRejectedException($"Report image '{reference}' could not be prepared for print.");
        return encoded.ToArray();
    }

    /// <summary>
    /// The part of a crop the print keeps: all of it, or for a slot the
    /// largest part of the slot's shape, width over height.
    /// </summary>
    private static SKSize Kept(float cropWidth, float cropHeight, float? shape)
    {
        if (shape is not { } widthOverHeight)
        {
            return new(cropWidth, cropHeight);
        }
        return cropWidth / cropHeight > widthOverHeight
            ? new(cropHeight * widthOverHeight, cropHeight)
            : new(cropWidth, cropWidth / widthOverHeight);
    }

    /// <summary>What brings the kept part within its pixel budget, never more than one.</summary>
    private static float Scale(SKSize kept, float? shape) => shape is null
        ? Math.Min(1f, FullPagePixels / Math.Max(1f, Math.Max(kept.Width, kept.Height)))
        : Math.Min(1f, SlotPixels / Math.Max(1f, kept.Width));

    /// <summary>
    /// The bytes as native data with no second copy. The array stays pinned
    /// until the data is released.
    /// </summary>
    private static SKData Wrap(byte[] content, string reference)
    {
        var pin = GCHandle.Alloc(content, GCHandleType.Pinned);
        SKData? data;
        try
        {
            data = SKData.Create(pin.AddrOfPinnedObject(), content.Length, (_, _) => pin.Free());
        }
        catch
        {
            pin.Free();
            throw;
        }
        if (data is null)
        {
            pin.Free();
            throw new ReportRenderRejectedException($"Report image '{reference}' could not be decoded.");
        }
        return data;
    }

    /// <summary>
    /// Whether the image can be read a row at a time without holding it
    /// whole: a PNG whose rows come top to bottom. An interlaced PNG (the
    /// IHDR interlace byte) is only ever delivered whole, so it decodes whole.
    /// </summary>
    private static bool CanReadByScanline(SKCodec codec, byte[] content)
    {
        const int InterlaceMethodOffset = 28;
        return codec.EncodedFormat == SKEncodedImageFormat.Png
            && content.Length > InterlaceMethodOffset
            && content[InterlaceMethodOffset] == 0
            && codec.ScanlineOrder == SKCodecScanlineOrder.TopDown;
    }

    /// <summary>
    /// The pixels of the stored image the kept part of the rotated source
    /// covers, found by carrying that part back through the same transform
    /// the print uses.
    /// </summary>
    private static SKRectI StoredRegion(
        SKEncodedOrigin origin,
        CaseAssetRotation rotation,
        int shownWidth,
        int shownHeight,
        int rotatedWidth,
        int rotatedHeight,
        SKRect kept,
        int storedWidth,
        int storedHeight)
    {
        using var probe = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(probe);
        canvas.Translate(rotatedWidth / 2f, rotatedHeight / 2f);
        canvas.RotateDegrees((int)rotation);
        canvas.Translate(-shownWidth / 2f, -shownHeight / 2f);
        EncodedImageOrientation.Apply(canvas, origin, shownWidth, shownHeight);
        if (!canvas.TotalMatrix.TryInvert(out var inverse))
        {
            return new SKRectI(0, 0, storedWidth, storedHeight);
        }
        var stored = inverse.MapRect(kept);
        var leftEdge = Math.Clamp((int)Math.Floor(stored.Left), 0, storedWidth - 1);
        var topEdge = Math.Clamp((int)Math.Floor(stored.Top), 0, storedHeight - 1);
        var rightEdge = Math.Clamp((int)Math.Ceiling(stored.Right), leftEdge + 1, storedWidth);
        var bottomEdge = Math.Clamp((int)Math.Ceiling(stored.Bottom), topEdge + 1, storedHeight);
        return new SKRectI(leftEdge, topEdge, rightEdge, bottomEdge);
    }

    /// <summary>
    /// Reads only <paramref name="region"/> of a non-interlaced PNG, a row at
    /// a time, and averages it down by <paramref name="scale"/> into a bitmap
    /// of about the output's size. Returns null when the codec will not start
    /// a scanline decode, so the caller decodes whole; a decode that fails
    /// part way fails the render.
    /// </summary>
    private static SKBitmap? ReadRegion(
        SKCodec codec, SKRectI region, float scale, string reference, CancellationToken cancellationToken)
    {
        var info = codec.Info;
        var rowInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        if (codec.StartScanlineDecode(rowInfo) != SKCodecResult.Success)
        {
            return null;
        }
        var targetWidth = Math.Clamp((int)Math.Ceiling(region.Width * scale), 1, region.Width);
        var targetHeight = Math.Clamp((int)Math.Ceiling(region.Height * scale), 1, region.Height);
        using var averager = new AreaAverager(region.Width, region.Height, targetWidth, targetHeight);
        if (region.Top > 0 && !codec.SkipScanlines(region.Top))
        {
            throw new ReportRenderRejectedException($"Report image '{reference}' could not be decoded.");
        }

        var row = new byte[rowInfo.RowBytes];
        var pin = GCHandle.Alloc(row, GCHandleType.Pinned);
        try
        {
            var address = pin.AddrOfPinnedObject();
            for (var y = 0; y < region.Height; y++)
            {
                if (y % CancellationRowInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (codec.GetScanlines(address, 1, rowInfo.RowBytes) != 1)
                {
                    throw new ReportRenderRejectedException($"Report image '{reference}' could not be decoded.");
                }
                averager.AddRow(row.AsSpan(region.Left * 4, region.Width * 4));
            }
        }
        finally
        {
            pin.Free();
        }
        return averager.Complete();
    }

    /// <summary>
    /// Averages rows of premultiplied RGBA down into a smaller bitmap, each
    /// target pixel the mean of the source area it covers, holding only the
    /// rows in progress. Source rows arrive top to bottom, and the target
    /// is never larger than the source.
    /// </summary>
    private sealed class AreaAverager : IDisposable
    {
        private readonly int sourceWidth;
        private readonly int targetWidth;
        private readonly int targetHeight;
        private readonly int[] columnFirst;
        private readonly float[] columnFirstWeight;
        private readonly float[] columnNextWeight;
        private readonly double rowsPerSourceRow;
        private readonly float[] reduced;
        private readonly SKBitmap target;
        private float[] current;
        private float[] following;
        private int currentRow;
        private int sourceRow;
        private bool completed;

        internal AreaAverager(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            this.sourceWidth = sourceWidth;
            this.targetWidth = targetWidth;
            this.targetHeight = targetHeight;
            var columnsPerSourceColumn = targetWidth / (double)sourceWidth;
            rowsPerSourceRow = targetHeight / (double)sourceHeight;
            columnFirst = new int[sourceWidth];
            columnFirstWeight = new float[sourceWidth];
            columnNextWeight = new float[sourceWidth];
            for (var column = 0; column < sourceWidth; column++)
            {
                var start = column * columnsPerSourceColumn;
                var end = start + columnsPerSourceColumn;
                var first = Math.Min((int)Math.Floor(start), targetWidth - 1);
                columnFirst[column] = first;
                columnFirstWeight[column] = (float)Math.Max(0d, Math.Min(end, first + 1) - start);
                columnNextWeight[column] = first + 1 < targetWidth && end > first + 1
                    ? (float)(end - (first + 1))
                    : 0f;
            }
            reduced = new float[targetWidth * 4];
            current = new float[targetWidth * 4];
            following = new float[targetWidth * 4];
            target = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        }

        internal void AddRow(ReadOnlySpan<byte> pixels)
        {
            Array.Clear(reduced);
            for (var column = 0; column < sourceWidth; column++)
            {
                var at = column * 4;
                float red = pixels[at];
                float green = pixels[at + 1];
                float blue = pixels[at + 2];
                float alpha = pixels[at + 3];
                var into = columnFirst[column] * 4;
                var weight = columnFirstWeight[column];
                reduced[into] += red * weight;
                reduced[into + 1] += green * weight;
                reduced[into + 2] += blue * weight;
                reduced[into + 3] += alpha * weight;
                var next = columnNextWeight[column];
                if (next > 0f)
                {
                    into += 4;
                    reduced[into] += red * next;
                    reduced[into + 1] += green * next;
                    reduced[into + 2] += blue * next;
                    reduced[into + 3] += alpha * next;
                }
            }

            var start = sourceRow * rowsPerSourceRow;
            var end = start + rowsPerSourceRow;
            var first = Math.Min((int)Math.Floor(start), targetHeight - 1);
            while (currentRow < first)
            {
                FlushCurrent();
            }
            Accumulate(current, (float)Math.Max(0d, Math.Min(end, first + 1) - start));
            if (first + 1 < targetHeight && end > first + 1)
            {
                Accumulate(following, (float)(end - (first + 1)));
            }
            sourceRow++;
        }

        internal SKBitmap Complete()
        {
            if (!completed)
            {
                while (currentRow < targetHeight)
                {
                    FlushCurrent();
                }
                completed = true;
            }
            return target;
        }

        public void Dispose()
        {
            // The finished bitmap belongs to the caller; an unfinished one is dropped.
            if (!completed)
            {
                target.Dispose();
            }
        }

        private void Accumulate(float[] into, float weight)
        {
            for (var index = 0; index < reduced.Length; index++)
            {
                into[index] += reduced[index] * weight;
            }
        }

        private void FlushCurrent()
        {
            var pixels = target.GetPixelSpan();
            var offset = currentRow * target.RowBytes;
            for (var column = 0; column < targetWidth; column++)
            {
                var at = column * 4;
                var alpha = ToByte(current[at + 3]);
                pixels[offset + at] = Math.Min(ToByte(current[at]), alpha);
                pixels[offset + at + 1] = Math.Min(ToByte(current[at + 1]), alpha);
                pixels[offset + at + 2] = Math.Min(ToByte(current[at + 2]), alpha);
                pixels[offset + at + 3] = alpha;
            }
            (current, following) = (following, current);
            Array.Clear(following);
            currentRow++;
        }

        private static byte ToByte(float sum) => (byte)Math.Clamp((int)(sum + 0.5f), 0, 255);
    }
}
