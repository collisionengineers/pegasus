using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Reports;

namespace Pegasus.IntegrationTests.Reports;

public sealed class AssessmentReportRendererTests
{
    [Fact]
    [Trait("Category", "Browser")]
    public async Task ApplicationCompositionRendersApprovedAssessmentAndFeeNoteWithRealChromium()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure((_, options) =>
            options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=renderer;Trusted_Connection=True"));
        services.AddPegasusReportRendering();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var useCase = scope.ServiceProvider.GetRequiredService<GenerateAssessmentReportDraft>();
        var result = await useCase.ExecuteAsync(Snapshot());

        var artifacts = new[] { result.Assessment, result.FeeNote };
        Assert.All(artifacts, artifact =>
        {
            Assert.True(artifact.Pdf.AsSpan().StartsWith("%PDF"u8));
            Assert.True(artifact.PageCount >= 1);
            Assert.Equal(64, artifact.Sha256.Length);
            Assert.Equal(AssessmentReportContract.TemplateVersion, artifact.TemplateVersion);
            Assert.Contains("Playwright", artifact.EngineVersion, StringComparison.Ordinal);
        });
        var evidence = Environment.GetEnvironmentVariable("PEGASUS_RENDER_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            foreach (var artifact in artifacts)
            {
                await File.WriteAllBytesAsync(Path.Combine(evidence, artifact.SuggestedFileName), artifact.Pdf);
            }
        }
    }

    [Fact]
    public void OnlyActiveSignatureResourceIsEmbeddedByteForByte()
    {
        var assembly = typeof(PlaywrightAssessmentReportRenderer).Assembly;
        using var embedded = assembly.GetManifestResourceStream(
            "Pegasus.Infrastructure.Reports.Assets.brand.signatures.andy_patterson.png");
        Assert.NotNull(embedded);
        using var memory = new MemoryStream();
        embedded.CopyTo(memory);
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(RepositoryRoot(), "docs", "design", "brand", "signatures", "andy_patterson.png")),
            memory.ToArray());
        Assert.DoesNotContain(assembly.GetManifestResourceNames(), name =>
            name.Contains("ed_mawdsley", StringComparison.Ordinal) ||
            name.Contains("neil_oreilly", StringComparison.Ordinal));
    }

    private static AssessmentReportSnapshot Snapshot() => new(
        "CE-100", "P-100", new DateOnly(2026, 8, 19), "Alex Example", new DateOnly(2026, 8, 1),
        ["Approved Principal", "1 Example Street"],
        new ReportVehicle("PK12 TMZ", "Ford", "Focus", "2012", "car", "good", "80,000 miles (online data)"),
        AssessmentReportOutcome.Repairable, "roadworthy", null, 5_000m, 5_000m, 4_000m, null, null,
        new ReportRepairCosts(5m, 30m, 50m, 20m, 5m, true),
        ["Front bumper"], ["Bonnet"], ["Paint front panels"], "History clear", "No further comments.",
        new ReportEngineer("A Patterson", "M.Inst.IAEA", "andy_patterson"),
        120m, ["Engineering assessment"], ["box://case/photo-1"],
        [new AcceptedReportSource("assessment", "7", new string('a', 64))]);

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Pegasus.slnx")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
