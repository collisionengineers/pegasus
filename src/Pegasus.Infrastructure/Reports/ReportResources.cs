using Pegasus.Core.Documents;
using QuestPDF.Drawing;

namespace Pegasus.Infrastructure.Reports;

internal static class ReportResources
{
    private static readonly Lock FontLock = new();
    private static bool fontsRegistered;

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

    internal static byte[] Logo()
    {
        using var stream = Open("brand.logo.png");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static Stream Open(string suffix)
    {
        var assembly = typeof(QuestPdfAssessmentReportRenderer).Assembly;
        var name = $"Pegasus.Infrastructure.Reports.Assets.{suffix}";
        return assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Required report resource '{name}' is missing.");
    }
}
