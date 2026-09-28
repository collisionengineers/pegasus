using System.Globalization;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The vehicle drawing as the report prints it: the Case page's plan
/// (<see cref="DamagePlanGeometry"/>), drawn in the Case page's colours and
/// turned on its side so that it fits the slot on page 1, with the front of
/// the vehicle at the left. It is turned and nothing else, so left stays
/// left. One disc marks each recorded damage that names a plan area, clipped
/// to the body as the Case page clips it; the plan prints with no marks when
/// none does. It carries no words.
/// </summary>
internal static class DamagePlanDrawing
{
    // The Case page's colours (case-workspace.css, #section-damage; the
    // tokens are site.css's), written flat.
    private const string Outline = "#9da9ae";
    private const string Body = "#ffffff";
    private const string Glass = "#cfd8de";
    private const string Seam = "#aeb8bd";
    private const string SoftSeam = "#d5dce0";
    private const string Lamp = "#eef2f4";
    private const string RearLamp = "#f3d4d6";
    private const string RearLampOutline = "#d39aa0";
    private const string Wheel = "#2f3538";
    private const string Mirror = "#dfe4e7";
    private const string Dot = "#202629";
    private static readonly (int Red, int Green, int Blue) MarkRed = (0xc9, 0x22, 0x2b);
    private const string MarkRedDark = "#9e1720";

    /// <summary>
    /// How much of the Case page's red a disc's fill and outline carry, by
    /// the rank of the severity. The heaviest is drawn in the darker red.
    /// </summary>
    private static readonly (double Fill, double Outline)[] Shades =
    [
        (0.22, 0.40),
        (0.40, 0.55),
        (0.58, 0.70),
        (0.78, 0.85),
    ];

    private const string ClipId = "plan-body";

    /// <summary>
    /// The drawing. Each damage's disc is shaded by that damage's own
    /// severity, as the Case page shades it.
    /// </summary>
    internal static string Svg(IReadOnlyList<ReportImpact> impacts)
    {
        ArgumentNullException.ThrowIfNull(impacts);
        var (width, height) = PlanSize();
        var discs = impacts
            .Select(impact => (Disc: DamagePlanGeometry.Disc(impact.Codes, impact.Disc), impact.Severity))
            .Where(mark => mark.Disc is not null)
            .Select(mark => (Disc: mark.Disc!, mark.Severity))
            .ToArray();

        var svg = new StringBuilder();
        Append(svg, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {height} {width}\">");
        Append(svg, $"<defs><clipPath id=\"{ClipId}\"><path d=\"{DamagePlanGeometry.BodyPath}\"/></clipPath></defs>");
        // A quarter turn anticlockwise: the front, at the top of the plan,
        // comes to the left.
        Append(svg, $"<g transform=\"translate(0 {width}) rotate(-90)\">");
        foreach (var wheel in DamagePlanGeometry.Wheels)
        {
            Append(svg, $"<rect fill=\"{Wheel}\" x=\"{wheel.CentreX - 8}\" y=\"{wheel.CentreY - 24}\" width=\"16\" height=\"48\" rx=\"5\"/>");
        }
        foreach (var mirror in DamagePlanGeometry.Mirrors)
        {
            Append(svg, $"<rect fill=\"{Mirror}\" stroke=\"{Outline}\" stroke-width=\"0.8\" x=\"{mirror.X}\" y=\"{mirror.Y}\" width=\"{mirror.Width}\" height=\"{mirror.Height}\" rx=\"4\"/>");
        }
        Append(svg, $"<path fill=\"{Body}\" stroke=\"{Outline}\" stroke-width=\"1.2\" stroke-linejoin=\"round\" d=\"{DamagePlanGeometry.BodyPath}\"/>");
        Append(svg, $"<path fill=\"{Glass}\" stroke=\"{Outline}\" stroke-width=\"0.6\" d=\"{DamagePlanGeometry.FrontGlassPath}\"/>");
        Append(svg, $"<path fill=\"{Glass}\" stroke=\"{Outline}\" stroke-width=\"0.6\" d=\"{DamagePlanGeometry.RearGlassPath}\"/>");
        Append(svg, $"<path fill=\"none\" stroke=\"{Seam}\" stroke-width=\"0.8\" d=\"{DamagePlanGeometry.SeamPath}\"/>");
        Append(svg, $"<path fill=\"none\" stroke=\"{SoftSeam}\" stroke-width=\"0.7\" d=\"{DamagePlanGeometry.SoftSeamPath}\"/>");
        foreach (var lamp in DamagePlanGeometry.FrontLampPaths)
        {
            Append(svg, $"<path fill=\"{Lamp}\" stroke=\"{Seam}\" stroke-width=\"0.6\" d=\"{lamp}\"/>");
        }
        foreach (var lamp in DamagePlanGeometry.RearLampPaths)
        {
            Append(svg, $"<path fill=\"{RearLamp}\" stroke=\"{RearLampOutline}\" stroke-width=\"0.6\" d=\"{lamp}\"/>");
        }
        foreach (var (disc, severity) in discs)
        {
            var (fill, outline) = Shade(severity);
            Append(svg, $"<circle clip-path=\"url(#{ClipId})\" fill=\"{fill}\" fill-opacity=\"0.32\" stroke=\"{outline}\" stroke-width=\"1.6\" cx=\"{disc.CentreX:0.#}\" cy=\"{disc.CentreY:0.#}\" r=\"{disc.Radius:0.#}\"/>");
            Append(svg, $"<circle fill=\"{Dot}\" stroke=\"{Body}\" stroke-width=\"1.5\" cx=\"{disc.CentreX:0.#}\" cy=\"{disc.CentreY:0.#}\" r=\"8\"/>");
        }
        svg.Append("</g></svg>");
        return svg.ToString();
    }

    private static (string Fill, string Outline) Shade(string severity)
    {
        if (!AssessmentVocabulary.DamageSeverities.TryGetValue(severity, out var recorded))
        {
            throw new ReportRenderRejectedException(
                "The report cannot draw a damage because its severity is not one it knows. "
                + "Record the damage again on the Damage section.");
        }
        return recorded.Rank < Shades.Length
            ? (Mix(Shades[recorded.Rank].Fill), Mix(Shades[recorded.Rank].Outline))
            : (MarkRedDark, MarkRedDark);
    }

    /// <summary>The Case page's red mixed with white, as its style sheet mixes them.</summary>
    private static string Mix(double share)
    {
        static int Channel(int red, double share) =>
            (int)Math.Round(red * share + 255 * (1 - share), MidpointRounding.AwayFromZero);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{Channel(MarkRed.Red, share):x2}{Channel(MarkRed.Green, share):x2}{Channel(MarkRed.Blue, share):x2}");
    }

    private static (int Width, int Height) PlanSize()
    {
        var box = DamagePlanGeometry.ViewBox.Split(' ');
        return (
            int.Parse(box[2], CultureInfo.InvariantCulture),
            int.Parse(box[3], CultureInfo.InvariantCulture));
    }

    private static void Append(StringBuilder svg, FormattableString markup) =>
        svg.Append(markup.ToString(CultureInfo.InvariantCulture));
}
