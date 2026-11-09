using Net.Codecrete.QrCodeGenerator;

namespace Pegasus.Web.Presentation;

/// <summary>
/// A standing photo link's QR code (R777): the one place the code is drawn,
/// as SVG markup generated from the link's own address. The markup carries no
/// operator text and no script, so the pages write it out as it is.
/// </summary>
public static class PhotoLinkQr
{
    private const int Border = 4;

    public static string Svg(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return QrCode.EncodeText(url, QrCode.Ecc.Medium).ToSvgString(Border);
    }

    /// <summary>The file a Download QR saves.</summary>
    public static string FileName(string slug) => $"photo-link-{slug}.svg";
}
