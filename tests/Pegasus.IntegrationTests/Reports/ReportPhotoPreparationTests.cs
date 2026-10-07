using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Reports;
using SkiaSharp;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// Issue 850: the report prints each image through one canvas transform
/// (EXIF orientation, the Engineer's rotation, the crop and the fit to its
/// slot), a large PNG is read a row at a time, and every image opens only
/// when it prints. These tests pin that the print is what the two-stage
/// rotated-copy print was, on both decode routes, and that the renderer and
/// its gate hold one image at a time and let an abandoned render go.
/// </summary>
public sealed class ReportPhotoPreparationTests
{
    private static readonly CaseAssetRotation[] Rotations =
    [
        CaseAssetRotation.None,
        CaseAssetRotation.Clockwise90,
        CaseAssetRotation.Half,
        CaseAssetRotation.Clockwise270,
    ];

    /// <summary>
    /// The four EXIF orientations that change the picture, each with the
    /// colour that lands in each displayed quadrant of a stored image whose
    /// quadrants are red, green (top row), blue and yellow (bottom row).
    /// </summary>
    [Theory]
    [InlineData((ushort)3, "Y", "B", "G", "R")]
    [InlineData((ushort)6, "B", "R", "Y", "G")]
    [InlineData((ushort)8, "G", "Y", "R", "B")]
    [InlineData((ushort)1, "R", "G", "B", "Y")]
    public void ExifOrientationIsAppliedInTheSameTransform(
        ushort orientation, string topLeft, string topRight, string bottomLeft, string bottomRight)
    {
        var source = JpegWithOrientation(960, 640, orientation);

        using var printed = Print(source, CaseAssetRotation.None, CaseAssetCrop.Full, slot: null);

        AssertQuadrants(printed, topLeft, topRight, bottomLeft, bottomRight);
    }

    [Fact]
    public void TheEngineersRotationTurnsTheOrientedImageAgain()
    {
        // Orientation 6 shows blue, red over yellow, green; a clockwise
        // quarter turn of that shows yellow, blue over green, red.
        var source = JpegWithOrientation(960, 640, 6);

        using var printed = Print(source, CaseAssetRotation.Clockwise90, CaseAssetCrop.Full, slot: null);

        Assert.Equal((960, 640), (printed.Width, printed.Height));
        AssertQuadrants(printed, "Y", "B", "G", "R");
    }

    [Theory]
    [InlineData(CaseAssetRotation.None, "R", "G", "B", "Y")]
    [InlineData(CaseAssetRotation.Clockwise90, "B", "R", "Y", "G")]
    [InlineData(CaseAssetRotation.Half, "Y", "B", "G", "R")]
    [InlineData(CaseAssetRotation.Clockwise270, "G", "Y", "R", "B")]
    public void ACropAcrossTheCentreOfTheRotatedSourceKeepsItsQuadrants(
        CaseAssetRotation rotation, string topLeft, string topRight, string bottomLeft, string bottomRight)
    {
        var source = Quadrants(800, 600, SKEncodedImageFormat.Png);

        using var printed = Print(source, rotation, new CaseAssetCrop(0.25m, 0.25m, 0.5m, 0.5m), slot: null);

        var quarterTurn = rotation is CaseAssetRotation.Clockwise90 or CaseAssetRotation.Clockwise270;
        Assert.Equal(quarterTurn ? (300, 400) : (400, 300), (printed.Width, printed.Height));
        AssertQuadrants(printed, topLeft, topRight, bottomLeft, bottomRight);
    }

    [Fact]
    public void ACropOfOneCornerOfTheRotatedSourcePrintsOnlyThatCorner()
    {
        var source = Quadrants(800, 600, SKEncodedImageFormat.Png);

        // Rotated a quarter turn the top right is the stored top left: red.
        using var printed = Print(
            source, CaseAssetRotation.Clockwise90, new CaseAssetCrop(0.5m, 0m, 0.5m, 0.5m), slot: null);

        Assert.Equal((300, 400), (printed.Width, printed.Height));
        foreach (var (x, y) in new[] { (30, 30), (270, 30), (150, 200), (30, 370), (270, 370) })
        {
            AssertClose(printed.GetPixel(x, y), SKColors.Red);
        }
    }

    /// <summary>
    /// A slot keeps the whole image (operator, 7 October 2026): it is sized
    /// to fit inside the slot, 1000 by 597 pixels, at its own shape, and
    /// never enlarged.
    /// </summary>
    [Fact]
    public void ASlotKeepsTheWholeImageAndNeverEnlargesIt()
    {
        var large = Quadrants(1600, 1200, SKEncodedImageFormat.Png);
        var portrait = Quadrants(1200, 1600, SKEncodedImageFormat.Png);
        var small = Quadrants(160, 120, SKEncodedImageFormat.Png);

        using var fitted = Print(large, CaseAssetRotation.None, CaseAssetCrop.Full, ReportChrome.GridSlotHeight);
        using var upright = Print(portrait, CaseAssetRotation.None, CaseAssetCrop.Full, ReportChrome.GridSlotHeight);
        using var kept = Print(small, CaseAssetRotation.None, CaseAssetCrop.Full, ReportChrome.GridSlotHeight);

        Assert.Equal((796, 597), (fitted.Width, fitted.Height));
        AssertQuadrants(fitted, "R", "G", "B", "Y");
        Assert.Equal((448, 597), (upright.Width, upright.Height));
        AssertQuadrants(upright, "R", "G", "B", "Y");
        Assert.Equal((160, 120), (kept.Width, kept.Height));
    }

    [Fact]
    public void TransparentPixelsPrintOnWhite()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(400, 300, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var source = encoded.ToArray();

        using var direct = Print(source, CaseAssetRotation.None, CaseAssetCrop.Full, slot: null, scanlineFrom: long.MaxValue);
        using var scanned = Print(source, CaseAssetRotation.None, CaseAssetCrop.Full, slot: null, scanlineFrom: 0);

        foreach (var printed in new[] { direct, scanned })
        {
            var pixel = printed.GetPixel(200, 150);
            Assert.InRange(pixel.Red, 250, byte.MaxValue);
            Assert.InRange(pixel.Green, 250, byte.MaxValue);
            Assert.InRange(pixel.Blue, 250, byte.MaxValue);
            Assert.Equal(byte.MaxValue, pixel.Alpha);
        }
    }

    /// <summary>
    /// The direct transform prints what the rotated full-size copy printed:
    /// each rotation, crop and page shape against a reference that builds
    /// that copy, on an image whose colour changes everywhere so a shifted
    /// or mirrored print cannot pass.
    /// </summary>
    [Theory]
    [InlineData(0, 0.0, 0.0, 1.0, 1.0, true)]
    [InlineData(1, 0.0, 0.0, 1.0, 1.0, true)]
    [InlineData(2, 0.1, 0.2, 0.6, 0.7, true)]
    [InlineData(3, 0.3, 0.1, 0.5, 0.8, true)]
    [InlineData(0, 0.1, 0.2, 0.6, 0.7, false)]
    [InlineData(1, 0.25, 0.0, 0.7, 1.0, false)]
    [InlineData(2, 0.0, 0.3, 1.0, 0.6, false)]
    [InlineData(3, 0.2, 0.2, 0.6, 0.6, false)]
    public void TheDirectPrintMatchesTheRotatedCopyPrint(
        int rotationIndex, double left, double top, double width, double height, bool inSlot)
    {
        var rotation = Rotations[rotationIndex];
        var crop = new CaseAssetCrop((decimal)left, (decimal)top, (decimal)width, (decimal)height);
        var source = Pattern(1600, 1200);
        float? slot = inSlot ? ReportChrome.GridSlotHeight : null;

        using var printed = Print(source, rotation, crop, slot, scanlineFrom: long.MaxValue);
        using var reference = RotatedCopyPrint(source, rotation, crop, slot);

        AssertSameShape(reference, printed);
        AssertSimilar(reference, printed, mean: 9);
    }

    /// <summary>
    /// A PNG read a row at a time prints what the same PNG decoded whole
    /// prints, for every rotation and crop, both page shapes.
    /// </summary>
    [Theory]
    [InlineData(0, 0.0, 0.0, 1.0, 1.0, true)]
    [InlineData(1, 0.0, 0.0, 1.0, 1.0, true)]
    [InlineData(2, 0.1, 0.2, 0.6, 0.7, true)]
    [InlineData(3, 0.3, 0.1, 0.5, 0.8, true)]
    [InlineData(0, 0.1, 0.2, 0.6, 0.7, false)]
    [InlineData(1, 0.25, 0.0, 0.7, 1.0, false)]
    [InlineData(2, 0.0, 0.3, 1.0, 0.6, false)]
    [InlineData(3, 0.2, 0.2, 0.6, 0.6, false)]
    public void AScanlineReadPrintsWhatAWholeDecodePrints(
        int rotationIndex, double left, double top, double width, double height, bool inSlot)
    {
        var rotation = Rotations[rotationIndex];
        var crop = new CaseAssetCrop((decimal)left, (decimal)top, (decimal)width, (decimal)height);
        var source = Pattern(1600, 1200);
        float? slot = inSlot ? ReportChrome.GridSlotHeight : null;

        using var whole = Print(source, rotation, crop, slot, scanlineFrom: long.MaxValue);
        using var scanned = Print(source, rotation, crop, slot, scanlineFrom: 0);

        AssertSameShape(whole, scanned);
        AssertSimilar(whole, scanned, mean: 9);
    }

    [Theory]
    [InlineData((ushort)3)]
    [InlineData((ushort)6)]
    [InlineData((ushort)8)]
    public void AScanlineReadAppliesAnOrientationCarriedByThePng(ushort orientation)
    {
        // Where the PNG codec does not read the chunk the origin is the top
        // left on both routes, so this still pins that the routes agree.
        var source = WithExifOrientation(Pattern(1200, 800), orientation);
        var crop = new CaseAssetCrop(0.1m, 0.2m, 0.7m, 0.6m);

        using var whole = Print(source, CaseAssetRotation.Clockwise90, crop, slot: null, scanlineFrom: long.MaxValue);
        using var scanned = Print(source, CaseAssetRotation.Clockwise90, crop, slot: null, scanlineFrom: 0);

        AssertSameShape(whole, scanned);
        AssertSimilar(whole, scanned, mean: 9);
    }

    /// <summary>
    /// A PNG far past the whole-decode threshold is generated in memory and
    /// prints at the slot's 597 pixels high, the same as a whole decode of it.
    /// Nothing refuses it for its size.
    /// </summary>
    [Fact]
    public void ALargePngIsReadByScanlineAndPrintsLikeAWholeDecode()
    {
        var source = Quadrants(5000, 4000, SKEncodedImageFormat.Png);
        Assert.True(5000L * 4000 > ReportPhotoPreparation.ScanlineFromPixels);
        var crop = new CaseAssetCrop(0.05m, 0.05m, 0.9m, 0.9m);

        using var scanned = Print(source, CaseAssetRotation.Clockwise90, crop, ReportChrome.GridSlotHeight);
        using var whole = Print(
            source, CaseAssetRotation.Clockwise90, crop, ReportChrome.GridSlotHeight, scanlineFrom: long.MaxValue);

        Assert.Equal((478, 597), (scanned.Width, scanned.Height));
        AssertSameShape(whole, scanned);
        AssertSimilar(whole, scanned, mean: 4);
        // Turned a quarter turn: blue, red over yellow, green.
        AssertQuadrants(scanned, "B", "R", "Y", "G");
    }

    /// <summary>
    /// A flat colour stays that colour when the scanline read averages it
    /// down, so the averaging cannot brighten or darken a print.
    /// </summary>
    [Fact]
    public void AScanlineReadKeepsAFlatColourTheSame()
    {
        var colour = new SKColor(200, 100, 50);
        using var bitmap = new SKBitmap(new SKImageInfo(3000, 2000, SKColorType.Rgba8888, SKAlphaType.Opaque));
        bitmap.Erase(colour);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var source = encoded.ToArray();

        using var scanned = Print(source, CaseAssetRotation.None, CaseAssetCrop.Full, slot: null, scanlineFrom: 0);

        Assert.Equal((2000, 1333), (scanned.Width, scanned.Height));
        foreach (var (x, y) in new[] { (5, 5), (1000, 660), (1990, 1320) })
        {
            var pixel = scanned.GetPixel(x, y);
            Assert.InRange((int)pixel.Red, colour.Red - 3, colour.Red + 3);
            Assert.InRange((int)pixel.Green, colour.Green - 3, colour.Green + 3);
            Assert.InRange((int)pixel.Blue, colour.Blue - 3, colour.Blue + 3);
        }
    }

    [Fact]
    public void ACropOfAHugeImagePrintsOnlyTheKeptPart()
    {
        // A small crop of a large PNG has nothing to scale down, so the
        // whole kept part prints at its own pixels, read without the rest.
        var source = Quadrants(5000, 4000, SKEncodedImageFormat.Png);
        var crop = new CaseAssetCrop(0.45m, 0.45m, 0.1m, 0.1m);

        using var printed = Print(source, CaseAssetRotation.None, crop, slot: null);

        Assert.Equal((500, 400), (printed.Width, printed.Height));
        AssertQuadrants(printed, "R", "G", "B", "Y");
    }

    [Fact]
    public void AnUndecodableImageIsRefusedByName()
    {
        var garbage = new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 };

        var refusal = Assert.Throws<ReportRenderRejectedException>(() => ReportPhotoPreparation.Prepare(
            garbage, "broken.png", CaseAssetRotation.None, CaseAssetCrop.Full, null, CancellationToken.None, 0));

        Assert.Contains("broken.png", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACancelledPrintStopsBeforeDecoding()
    {
        var source = Quadrants(400, 300, SKEncodedImageFormat.Png);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() => ReportPhotoPreparation.Prepare(
            source, "site.png", CaseAssetRotation.None, CaseAssetCrop.Full, null, cancelled.Token, 0));
    }

    /// <summary>
    /// Every image opens when the renderer prints it, one at a time, and once:
    /// nothing is read while the snapshot is composed or the render queues.
    /// </summary>
    [Fact]
    public async Task EachImageOpensOnceWhenItPrintsAndOnlyOneIsOpenAtATime()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var opens = new List<string>();
        var running = 0;
        var most = 0;
        ReportImageEvidence Photo(int index)
        {
            var bytes = Quadrants(320, 240, SKEncodedImageFormat.Jpeg);
            return new ReportImageEvidence(
                $"site-{index}.jpg",
                "image/jpeg",
                ReportImageContent.Opened(async _ =>
                {
                    opens.Add($"site-{index}.jpg");
                    most = Math.Max(most, Interlocked.Increment(ref running));
                    await Task.Delay(20, CancellationToken.None);
                    Interlocked.Decrement(ref running);
                    return bytes;
                }),
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                CaseAssetReportRole.Supporting,
                index);
        }
        var snapshot = AssessmentReportRendererTests.ReadySnapshot() with
        {
            Photos = [Photo(3), Photo(1), Photo(2)],
        };
        Assert.Empty(opens);

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);

        Assert.True(artifact.PageCount > 0);
        // In the Engineer's order, each exactly once.
        Assert.Equal(["site-1.jpg", "site-2.jpg", "site-3.jpg"], opens);
        Assert.Equal(1, most);
    }

    /// <summary>
    /// A caller that stops waiting stops the render: the token the gate hands
    /// the render is cancelled and the render does not open its next image.
    /// </summary>
    [Fact]
    public async Task ACancelledRenderStopsBeforeItOpensTheNextImage()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var gate = provider.GetRequiredService<ReportRenderGate>();
        using var caller = new CancellationTokenSource();
        var opened = new List<int>();
        ReportImageEvidence Photo(int index)
        {
            var bytes = Quadrants(320, 240, SKEncodedImageFormat.Jpeg);
            return new ReportImageEvidence(
                $"site-{index}.jpg",
                "image/jpeg",
                ReportImageContent.Opened(_ =>
                {
                    opened.Add(index);
                    caller.Cancel();
                    return Task.FromResult(bytes);
                }),
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                CaseAssetReportRole.Supporting,
                index);
        }
        var snapshot = AssessmentReportRendererTests.ReadySnapshot() with { Photos = [Photo(1), Photo(2)] };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => renderer.RenderAsync(snapshot, CaseReportArtifactKind.AssessmentReport, caller.Token));
        await SettledAsync(gate);

        Assert.Equal([1], opened);
    }

    /// <summary>
    /// A render whose caller has gone keeps its place in admission until it
    /// has stopped, so abandoned renders cannot let the queue hold more.
    /// </summary>
    [Fact]
    public async Task AnAbandonedRenderStillCountsAgainstAdmissionUntilItStops()
    {
        using var gate = new ReportRenderGate();
        using var caller = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var abandoned = gate.RunAsync(async _ =>
        {
            started.SetResult();
            await release.Task;
            return new byte[] { 1 };
        }, caller.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);

        // Its caller is gone, but the render is still running and admitted.
        Assert.Equal(1, gate.InFlight);
        var waiting = Enumerable.Range(0, 8)
            .Select(index => gate.RunAsync(_ => Task.FromResult(new byte[] { (byte)index }), CancellationToken.None))
            .ToArray();
        Assert.Equal(9, gate.InFlight);
        var busy = await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => gate.RunAsync(_ => Task.FromResult(new byte[] { 3 }), CancellationToken.None));
        Assert.Equal("The document renderer is busy.", busy.Message);

        release.SetResult();
        await Task.WhenAll(waiting).WaitAsync(TimeSpan.FromSeconds(30));
        await SettledAsync(gate);
        Assert.Equal(0, gate.InFlight);
    }

    [Fact]
    public async Task AnAbandonedRenderThatChecksItsTokenStopsAndFreesTheSlot()
    {
        using var gate = new ReportRenderGate();
        using var caller = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var abandoned = gate.RunAsync(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new byte[] { 1 };
        }, caller.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
        await SettledAsync(gate);

        Assert.Equal(0, gate.InFlight);
        Assert.Equal(
            new byte[] { 9 }, await gate.RunAsync(_ => Task.FromResult(new byte[] { 9 }), CancellationToken.None));
    }

    private static async Task SettledAsync(ReportRenderGate gate)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (gate.InFlight > 0 && DateTime.UtcNow < until)
        {
            await Task.Delay(20);
        }
    }

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }

    private static SKBitmap Print(
        byte[] source,
        CaseAssetRotation rotation,
        CaseAssetCrop crop,
        float? slot,
        long scanlineFrom = ReportPhotoPreparation.ScanlineFromPixels)
    {
        var jpeg = ReportPhotoPreparation.Prepare(
            source, "site", rotation, crop, slot, CancellationToken.None, scanlineFrom);
        return Assert.IsType<SKBitmap>(SKBitmap.Decode(jpeg));
    }

    /// <summary>
    /// The print as it was made before the direct transform: the whole image
    /// decoded, a full-size rotated copy drawn as the crop editor draws it,
    /// then the kept part of that copy scaled into the output.
    /// </summary>
    private static SKBitmap RotatedCopyPrint(
        byte[] source, CaseAssetRotation rotation, CaseAssetCrop crop, float? slot)
    {
        using var decoded = Assert.IsType<SKBitmap>(SKBitmap.Decode(source));
        var quarterTurn = rotation is CaseAssetRotation.Clockwise90 or CaseAssetRotation.Clockwise270;
        var rotatedWidth = quarterTurn ? decoded.Height : decoded.Width;
        var rotatedHeight = quarterTurn ? decoded.Width : decoded.Height;
        using var rotated = new SKBitmap(new SKImageInfo(rotatedWidth, rotatedHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Clear(SKColors.White);
            canvas.Translate(rotatedWidth / 2f, rotatedHeight / 2f);
            canvas.RotateDegrees((int)rotation);
            canvas.Translate(-decoded.Width / 2f, -decoded.Height / 2f);
            canvas.DrawBitmap(decoded, 0, 0);
        }

        var cropWidth = Math.Max(1f, (float)crop.Width * rotatedWidth);
        var cropHeight = Math.Max(1f, (float)crop.Height * rotatedHeight);
        var shape = slot is { } slotHeight ? ReportChrome.SlotWidth / slotHeight : (float?)null;
        var keptWidth = cropWidth;
        var keptHeight = cropHeight;
        var left = (float)crop.Left * rotatedWidth;
        var top = (float)crop.Top * rotatedHeight;
        var scale = shape is not { } widthOverHeight
            ? Math.Min(1f, ReportPhotoPreparation.FullPagePixels / Math.Max(1f, Math.Max(keptWidth, keptHeight)))
            : Math.Min(1f, Math.Min(
                ReportPhotoPreparation.SlotPixels / Math.Max(1f, keptWidth),
                ReportPhotoPreparation.SlotPixels / widthOverHeight / Math.Max(1f, keptHeight)));
        var outputWidth = Math.Max(1, (int)Math.Round(keptWidth * scale));
        var outputHeight = Math.Max(1, (int)Math.Round(keptHeight * scale));
        var output = new SKBitmap(new SKImageInfo(outputWidth, outputHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(output))
        using (var image = SKImage.FromBitmap(rotated))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(
                image,
                new SKRect(left, top, left + keptWidth, top + keptHeight),
                new SKRect(0, 0, outputWidth, outputHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        return output;
    }

    private static void AssertSameShape(SKBitmap expected, SKBitmap actual)
    {
        // A pixel of rounding either way at the edge is not a different print.
        Assert.InRange(actual.Width, expected.Width - 1, expected.Width + 1);
        Assert.InRange(actual.Height, expected.Height - 1, expected.Height + 1);
    }

    private static void AssertSimilar(SKBitmap expected, SKBitmap actual, double mean)
    {
        var width = Math.Min(expected.Width, actual.Width);
        var height = Math.Min(expected.Height, actual.Height);
        long total = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var a = expected.GetPixel(x, y);
                var b = actual.GetPixel(x, y);
                total += Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue);
            }
        }
        var average = total / (3d * width * height);
        Assert.True(average <= mean, $"The prints differ by {average:F2} per channel on average; at most {mean} is allowed.");
    }

    private static void AssertQuadrants(
        SKBitmap printed, string topLeft, string topRight, string bottomLeft, string bottomRight)
    {
        var width = printed.Width;
        var height = printed.Height;
        AssertClose(printed.GetPixel(width / 4, height / 4), Named(topLeft));
        AssertClose(printed.GetPixel(width * 3 / 4, height / 4), Named(topRight));
        AssertClose(printed.GetPixel(width / 4, height * 3 / 4), Named(bottomLeft));
        AssertClose(printed.GetPixel(width * 3 / 4, height * 3 / 4), Named(bottomRight));
    }

    private static SKColor Named(string code) => code switch
    {
        "R" => SKColors.Red,
        "G" => SKColors.Green,
        "B" => SKColors.Blue,
        "Y" => SKColors.Yellow,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static void AssertClose(SKColor actual, SKColor expected)
    {
        const int tolerance = 40;
        Assert.InRange((int)actual.Red, Math.Max(0, expected.Red - tolerance), Math.Min(255, expected.Red + tolerance));
        Assert.InRange((int)actual.Green, Math.Max(0, expected.Green - tolerance), Math.Min(255, expected.Green + tolerance));
        Assert.InRange((int)actual.Blue, Math.Max(0, expected.Blue - tolerance), Math.Min(255, expected.Blue + tolerance));
    }

    /// <summary>Red, green over blue, yellow, split at the middle.</summary>
    private static byte[] Quadrants(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint())
        {
            var halfWidth = width / 2f;
            var halfHeight = height / 2f;
            paint.Color = SKColors.Red;
            canvas.DrawRect(0, 0, halfWidth, halfHeight, paint);
            paint.Color = SKColors.Green;
            canvas.DrawRect(halfWidth, 0, width - halfWidth, halfHeight, paint);
            paint.Color = SKColors.Blue;
            canvas.DrawRect(0, halfHeight, halfWidth, height - halfHeight, paint);
            paint.Color = SKColors.Yellow;
            canvas.DrawRect(halfWidth, halfHeight, width - halfWidth, height - halfHeight, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 100);
        return encoded.ToArray();
    }

    /// <summary>
    /// A PNG whose colour changes at every pixel and whose blocks change
    /// again every hundred, so a shifted, mirrored or wrongly cropped print
    /// differs from the right one everywhere.
    /// </summary>
    private static byte[] Pattern(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var pixels = bitmap.GetPixelSpan();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = (y * width + x) * 4;
                pixels[at] = (byte)(x * 255 / (width - 1));
                pixels[at + 1] = (byte)(y * 255 / (height - 1));
                pixels[at + 2] = (x / 100 + y / 100) % 2 == 0 ? (byte)200 : (byte)40;
                pixels[at + 3] = 255;
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static byte[] JpegWithOrientation(int width, int height, ushort orientation)
    {
        var encoded = Quadrants(width, height, SKEncodedImageFormat.Jpeg);
        byte[] exif =
        [
            0xff, 0xe1, 0x00, 0x22,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            0x49, 0x49, 0x2a, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,
            (byte)orientation, (byte)(orientation >> 8), 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        var oriented = new byte[encoded.Length + exif.Length];
        Buffer.BlockCopy(encoded, 0, oriented, 0, 2);
        Buffer.BlockCopy(exif, 0, oriented, 2, exif.Length);
        Buffer.BlockCopy(encoded, 2, oriented, exif.Length + 2, encoded.Length - 2);
        return oriented;
    }

    /// <summary>The PNG with an eXIf chunk naming its orientation, straight after IHDR.</summary>
    private static byte[] WithExifOrientation(byte[] png, ushort orientation)
    {
        byte[] tiff =
        [
            0x49, 0x49, 0x2a, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,
            (byte)orientation, (byte)(orientation >> 8), 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        var typeAndData = new byte[4 + tiff.Length];
        "eXIf"u8.CopyTo(typeAndData);
        tiff.CopyTo(typeAndData, 4);
        var chunk = new byte[4 + typeAndData.Length + 4];
        WriteBigEndian(chunk, 0, (uint)tiff.Length);
        typeAndData.CopyTo(chunk, 4);
        WriteBigEndian(chunk, 4 + typeAndData.Length, Crc32(typeAndData));

        // The signature is 8 bytes and IHDR is 25: length, type, 13 of data, CRC.
        const int afterHeader = 8 + 25;
        var result = new byte[png.Length + chunk.Length];
        Buffer.BlockCopy(png, 0, result, 0, afterHeader);
        Buffer.BlockCopy(chunk, 0, result, afterHeader, chunk.Length);
        Buffer.BlockCopy(png, afterHeader, result, afterHeader + chunk.Length, png.Length - afterHeader);
        return result;
    }

    private static void WriteBigEndian(byte[] into, int at, uint value)
    {
        into[at] = (byte)(value >> 24);
        into[at + 1] = (byte)(value >> 16);
        into[at + 2] = (byte)(value >> 8);
        into[at + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return ~crc;
    }
}
