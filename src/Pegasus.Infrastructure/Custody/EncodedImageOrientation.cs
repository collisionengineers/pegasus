using SkiaSharp;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The one owner of EXIF display orientation for decoded case images: the
/// gallery thumbnail and the rendered report both show a photo the way a
/// browser and the crop editor show it, so the stored orientation is applied
/// here and nowhere else.
/// </summary>
internal static class EncodedImageOrientation
{
    /// <summary>
    /// Whether the EXIF origin exchanges the image's width and height.
    /// </summary>
    internal static bool IsTransposed(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// Turns the stored pixels into the displayed image on a canvas whose own
    /// size — <paramref name="width"/> by <paramref name="height"/> — is
    /// already the displayed one.
    /// </summary>
    internal static void Apply(
        SKCanvas canvas,
        SKEncodedOrigin origin,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(width, height);
                canvas.Scale(-1, -1);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(width, height);
                canvas.Scale(-1, -1);
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(-90);
                break;
            default:
                break;
        }
    }
}
