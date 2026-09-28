using System.Globalization;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The vehicle drawing as the report prints it: the Case page's plan of the
/// recorded vehicle (<see cref="DamagePlanGeometry"/>) in its flat colours,
/// turned on its side so that it fits the slot on page 1, with the front of
/// the vehicle at the left. It is turned and nothing else, so left stays
/// left. Each recorded damage that names a plan area is drawn as the Case
/// page's comic burst, unclipped and unnumbered; the plan prints with no
/// marks when none does. It carries no words.
/// </summary>
internal static class DamagePlanDrawing
{
    internal static string Svg(string profile, IReadOnlyList<ReportImpact> impacts)
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
        // A quarter turn anticlockwise: the front, at the top of the plan,
        // comes to the left.
        Append(svg, $"<g transform=\"translate(0 {width}) rotate(-90)\">");
        svg.Append(DamagePlanGeometry.FlatArtwork(profile));
        foreach (var (disc, severity) in discs)
        {
            Known(severity);
            Append(svg, $"<path fill=\"{DamageBurst.Fill}\" fill-opacity=\"{DamageBurst.FillOpacity}\" stroke=\"{DamageBurst.Line}\" stroke-width=\"{DamageBurst.LineWidth}\" stroke-linejoin=\"round\" d=\"{DamagePlanGeometry.BurstPath(disc)}\"/>");
        }
        svg.Append("</g></svg>");
        return svg.ToString();
    }

    // Every burst looks the same, but a severity the report does not know is
    // still a damage recorded wrongly.
    private static void Known(string severity)
    {
        if (!AssessmentVocabulary.DamageSeverities.ContainsKey(severity))
        {
            throw new ReportRenderRejectedException(
                "The report cannot draw a damage because its severity is not one it knows. "
                + "Record the damage again on the Damage section.");
        }
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
