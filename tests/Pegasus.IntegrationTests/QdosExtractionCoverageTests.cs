using System.Globalization;
using System.Text;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Instruction-field extraction coverage over the local genuine corpus
/// (git-ignored, immutable, per-machine — INTK-021). The operator's bar:
/// names and registrations must extract from the real document shapes, not
/// only from synthetic geometry. Runs only where the corpus exists; the
/// per-file rows land under artifacts/evaluation for inspection.
/// </summary>
public sealed class QdosExtractionCoverageTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset ReceivedAtUtc =
        new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [QdosCorpusFact]
    public async Task RealInstructionEmailsExtractTheCoreFieldSet()
    {
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var routePolicy = new QdosMailRoutePolicy();
        var extraction = new QdosInstructionExtractionPolicy();
        var context = new EstablishedPrincipalContext(
            QdosInstructionExtractionPolicy.SupportedPrincipalCode,
            QdosMailRoutePolicy.Key,
            QdosMailRoutePolicy.Version);

        var fieldCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var conflictCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var accepted = 0;
        var processed = 0;
        var rows = new StringBuilder("file,field,value,conflict\n");

        foreach (var path in QdosCorpus.VolumeRoots
                     .SelectMany(EnumerateEmails)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var readResult = await reader.ReadAsync(Source(path, processed), CancellationToken.None);
            processed++;
            if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            {
                continue;
            }

            var route = routePolicy.Evaluate(readResult);
            if (route.Disposition != MailRouteDisposition.Accepted)
            {
                continue;
            }

            accepted++;
            var result = extraction.Extract(readResult, ReceivedAtUtc, context);
            foreach (var field in result.Fields)
            {
                if (field.HasConflict)
                {
                    conflictCounts[field.Name] = conflictCounts.GetValueOrDefault(field.Name) + 1;
                    rows.AppendLine(CultureInfo.InvariantCulture, $"{CsvName(path)},{field.Name},,conflict");
                }
                else if (!string.IsNullOrWhiteSpace(field.SuggestedValue))
                {
                    fieldCounts[field.Name] = fieldCounts.GetValueOrDefault(field.Name) + 1;
                    rows.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"{CsvName(path)},{field.Name},{CsvValue(field.SuggestedValue)},");
                }
            }
        }

        QdosCorpus.WriteArtifact("extraction-coverage.csv", rows.ToString());
        output.WriteLine($"extraction coverage: processed={processed}; accepted-route={accepted}");
        foreach (var name in fieldCounts.Keys
                     .Concat(conflictCounts.Keys)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            output.WriteLine(
                $"  {name}: value={fieldCounts.GetValueOrDefault(name)}/{accepted}"
                + $" conflict={conflictCounts.GetValueOrDefault(name)}");
        }

        Assert.True(accepted > 0, "The corpus yielded no accepted-route instruction emails.");
        // The operator's floor: the majority of real instruction emails carry
        // a readable claimant name and vehicle registration, and extraction
        // must read them — not only synthetic geometry.
        AssertCoverage(fieldCounts, accepted, "Vehicle registration", 0.60);
        AssertCoverage(fieldCounts, accepted, "Claimant name", 0.60);
    }

    private static void AssertCoverage(
        Dictionary<string, int> counts,
        int accepted,
        string field,
        double floor)
    {
        var covered = counts.GetValueOrDefault(field);
        Assert.True(
            covered >= accepted * floor,
            $"'{field}' extracted from {covered}/{accepted} accepted emails — below the {floor:P0} floor.");
    }

    private static IEnumerable<string> EnumerateEmails(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.eml", SearchOption.AllDirectories)
            : [];

    private static IntakeSource Source(string path, int index) =>
        new(
            Path.GetFileName(path),
            "message/rfc822",
            File.ReadAllBytes(path),
            ReceivedAtUtc,
            "extraction-coverage",
            new(IntakeSourceChannel.Mailbox, $"coverage-{index:00000}"));

    private static string CsvName(string path) =>
        $"\"{Path.GetFileName(path).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string CsvValue(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
