using System.Text;
using BenchmarkDotNet.Attributes;
using Pegasus.Core.Intake;

namespace Pegasus.Benchmarks;

/// <summary>
/// The QDOS instruction reader over a synthetic letter of several pages (the
/// per-fragment line split of PR 1266 / #1206, register row A2-12). The text is
/// invented labelled rows, not corpus material.
/// </summary>
[MemoryDiagnoser]
public class InstructionExtractionBenchmarks
{
    private static readonly EstablishedPrincipalContext QdosContext =
        new("QDOS", PrincipalMailRoutePolicy.Key, PrincipalMailRoutePolicy.Version);

    private static readonly InstructionExtractionTiming ProcessedAtUtc =
        new(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));

    private readonly QdosInstructionExtractionPolicy policy = new();
    private IntakeSourceReadResult read = null!;

    [Params(1, 8, 32)]
    public int Pages { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var content = new List<IntakeContentFragment>(Pages);
        for (var page = 1; page <= Pages; page++)
        {
            content.Add(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 7: instruction letter, page " + page.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Page(page)));
        }

        read = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable, content, [], [], false);
    }

    [Benchmark]
    public InstructionExtractionResult Extract() =>
        policy.Extract(read, ProcessedAtUtc, QdosContext);

    private static string Page(int page)
    {
        var text = new StringBuilder();
        text.AppendLine("Our Ref: AKH//" + (47000 + page).ToString(System.Globalization.CultureInfo.InvariantCulture) + "/1");
        text.AppendLine("Date: 22/08/2026");
        text.AppendLine("Date of Accident: 14/08/2026");
        text.AppendLine("Inspection Date: 20/08/2026");
        text.AppendLine("Damage Area - Nearside: Moderate: Nearside rear wheel arch is");
        text.AppendLine("damaged. Nearside door is damaged.");
        text.AppendLine();
        text.AppendLine("Pre-existing Damage:");
        text.AppendLine();
        text.AppendLine("No.");
        text.AppendLine();
        text.AppendLine("TP Vehicle: SCANIA");
        return text.ToString();
    }
}
