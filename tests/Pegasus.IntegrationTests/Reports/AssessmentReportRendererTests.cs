using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Reports;

namespace Pegasus.IntegrationTests.Reports;

public sealed class AssessmentReportRendererTests
{
    [Fact]
    public void NoSignatoryResourceIsEmbedded()
    {
        var assembly = typeof(PlaywrightAssessmentReportRenderer).Assembly;
        Assert.DoesNotContain(
            assembly.GetManifestResourceNames(),
            name => name.Contains("brand.signatures", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheRendererPublishesItsEngineVersionWithoutRendering()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();

        var engineVersion = scope.ServiceProvider
            .GetRequiredService<IAssessmentReportRenderer>().EngineVersion;

        Assert.Contains("Playwright", engineVersion, StringComparison.Ordinal);
        Assert.Contains("Chromium", engineVersion, StringComparison.Ordinal);
    }

    /// <summary>
    /// R34B: with the choice made, the report's own document ends with the
    /// fee note's pages — one document, the fee note last, after a page
    /// break — and the separate fee-note document is unchanged. Composition
    /// is pure, so this needs no browser.
    /// </summary>
    [Fact]
    public async Task TheCombinedReportEndsWithTheFeeNotePagesInOneDocument()
    {
        var report = AssessmentReportProjection
            .Project(AssessmentReportDraftWebTests.ReadyInput(Guid.NewGuid())).Snapshot!;

        var plain = await PlaywrightAssessmentReportRenderer.ComposeHtmlAsync(
            report, CaseReportArtifactKind.AssessmentReport);
        var combined = await PlaywrightAssessmentReportRenderer.ComposeHtmlAsync(
            report with { IncludeFeeNote = true }, CaseReportArtifactKind.AssessmentReport);

        // The report body already speaks of the fee note in its terms; the
        // fee-note pages are identified by their own totals block.
        Assert.DoesNotContain("TOTAL DUE", plain, StringComparison.Ordinal);
        Assert.Contains("TOTAL DUE", combined, StringComparison.Ordinal);
        Assert.Contains("Bill To:", combined, StringComparison.Ordinal);
        // The report is first and complete, then a page break, then the fee
        // note — and it is still a single HTML document.
        Assert.StartsWith(plain[..plain.LastIndexOf("</body>", StringComparison.Ordinal)], combined, StringComparison.Ordinal);
        Assert.True(
            combined.IndexOf("Statement of Truth", StringComparison.Ordinal)
                < combined.IndexOf("TOTAL DUE", StringComparison.Ordinal));
        Assert.True(
            combined.LastIndexOf("<div class=\"page-break\"></div>", StringComparison.Ordinal)
                < combined.IndexOf("TOTAL DUE", StringComparison.Ordinal));
        Assert.Equal(2, combined.Split("<html", StringSplitOptions.None).Length);
        Assert.Equal(2, combined.Split("</body>", StringSplitOptions.None).Length);
        Assert.EndsWith("</body></html>", combined.TrimEnd(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The separate documents are exactly what they were: the packaging
    /// choice only ever adds pages to the report itself.
    /// </summary>
    [Fact]
    public async Task TheSeparateFeeNoteDocumentIsUnaffectedByThePackagingChoice()
    {
        var report = AssessmentReportProjection
            .Project(AssessmentReportDraftWebTests.ReadyInput(Guid.NewGuid())).Snapshot!;

        var separate = await PlaywrightAssessmentReportRenderer.ComposeHtmlAsync(
            report, CaseReportArtifactKind.FeeNote);
        var separateWhenCombined = await PlaywrightAssessmentReportRenderer.ComposeHtmlAsync(
            report with { IncludeFeeNote = true }, CaseReportArtifactKind.FeeNote);

        Assert.Equal(separate, separateWhenCombined);
    }

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure((_, options) =>
            options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=renderer;Trusted_Connection=True"));
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }
}
