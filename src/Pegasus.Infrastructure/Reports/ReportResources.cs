using Pegasus.Core.Documents;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Pegasus.Infrastructure.Reports;

internal static class ReportResources
{
    private static readonly Lock FontLock = new();
    private static bool fontsRegistered;

    /// <summary>Whether this process has registered the report's fonts.</summary>
    internal static bool FontsRegistered
    {
        get
        {
            lock (FontLock)
            {
                return fontsRegistered;
            }
        }
    }

    internal static void RegisterFonts()
    {
        lock (FontLock)
        {
            if (fontsRegistered)
            {
                return;
            }
            // First-use cost of the embedded fonts, once per process, for the
            // performance telemetry that the renderer's constructor used to emit.
            using var timing = DocumentReadTelemetry.Start("report.renderer.initialize");
            foreach (var face in new[] { "Regular", "Bold", "Italic", "BoldItalic" })
            {
                using var stream = Open($"fonts.LiberationSans-{face}.ttf");
                FontManager.RegisterFont(stream);
            }
            fontsRegistered = true;
        }
    }

    /// <summary>
    /// The logo as the governed master (<c>docs/design/brand/logos/logo_no_margin.png</c>,
    /// embedded as it is), loaded once for the process and shared by every document. A
    /// shared image is not disposed when a document ends, and it keeps the scaled version
    /// each document asks for, so the 3150 by 1756 master is decoded and scaled once
    /// rather than once per rendered document. It lives as long as the process.
    /// </summary>
    internal static Image Logo() => SharedLogo.Value;

    private static readonly Lazy<Image> SharedLogo = new(() =>
    {
        using var stream = Open("brand.logo.png");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Image.FromBinaryData(memory.ToArray());
    });

    private static Stream Open(string suffix)
    {
        var assembly = typeof(QuestPdfAssessmentReportRenderer).Assembly;
        var name = $"Pegasus.Infrastructure.Reports.Assets.{suffix}";
        return assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Required report resource '{name}' is missing.");
    }
}
