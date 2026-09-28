using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Assessment;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Every real Audatex full-report PDF in the ignored local corpus, through the
/// production PDF estimate parser. The synthetic
/// <see cref="AudatexEstimateFixture"/> proves the parser's rules; this proves
/// they hold on the documents Audatex actually prints, which have differing
/// header heights and section orders. Each document must parse whole and its
/// lines must reproduce the document's own printed section totals — there is
/// no partial import. It reads bytes only and never copies or edits the corpus.
/// </summary>
[Trait("Category", "Corpus")]
public sealed class AudatexEstimateCorpusTests(ITestOutputHelper output)
{
    [SkippableCorpusFact]
    public void EveryLocalAudatexPdfParsesWholeAndReconcilesToItsPrintedTotals()
    {
        var files = Directory.EnumerateFiles(
                CorpusLocator.CorpusRoot!,
                "*audatex*.pdf",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.True(files.Length > 0, "The local corpus holds no Audatex PDF to prove the parser against.");

        var parser = new PdfEstimateDocumentParser();
        var failures = new List<string>();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            try
            {
                var parsed = parser.Parse(File.ReadAllBytes(file));
                Verify(parsed);
                output.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name}: {parsed.Lines.Count} lines, version {parsed.SourceVersion}"));
            }
            catch (Exception exception) when (exception is EstimateParseRejectedException or Xunit.Sdk.XunitException)
            {
                failures.Add($"{name}: {exception.Message}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"These Audatex PDFs did not parse whole and reconcile:{Environment.NewLine}"
            + string.Join(Environment.NewLine, failures));
    }

    private static void Verify(ParsedEstimate parsed)
    {
        Assert.Equal(RepairSpecificationSourceRoute.AudatexPdf, parsed.Route);
        Assert.Equal(AudatexEstimatePdfParser.ProviderName, parsed.ProviderName);
        Assert.False(string.IsNullOrWhiteSpace(parsed.SourceVersion));
        Assert.NotEmpty(parsed.Lines);
        var totals = Assert.IsType<EstimateSourceTotals>(parsed.SourceTotals);

        decimal WorkUnits(string section) => parsed.Lines
            .Where(line => line.SourceRowIdentity!.StartsWith(section + ":", StringComparison.Ordinal))
            .Sum(line => line.WorkUnits ?? 0m);
        decimal Money(string section) => parsed.Lines
            .Where(line => line.SourceRowIdentity!.StartsWith(section + ":", StringComparison.Ordinal))
            .Sum(line => line.Price ?? 0m);
        bool Has(string section) => parsed.Lines
            .Any(line => line.SourceRowIdentity!.StartsWith(section + ":", StringComparison.Ordinal));

        // A section that produced lines must carry its printed total, and the
        // lines must add up to it exactly; a section with no lines prints none.
        if (Has("labour"))
        {
            Assert.Equal<decimal?>(totals.PanelWorkUnits, WorkUnits("labour"));
        }
        if (Has("paint"))
        {
            Assert.Equal<decimal?>(totals.PaintWorkUnits, WorkUnits("paint"));
        }
        if (Has("parts"))
        {
            Assert.Equal<decimal?>(totals.Parts, Money("parts"));
        }
        if (Has("extras"))
        {
            Assert.Equal<decimal?>(totals.Specialist, Money("extras"));
        }
    }
}
