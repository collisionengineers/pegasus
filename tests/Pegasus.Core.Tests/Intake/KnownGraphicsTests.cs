using System.Numerics;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class KnownGraphicsTests
{
    [Fact]
    public void EveryEntryMatchesACopyAtItsReferenceAndAtItsLimit()
    {
        foreach (var entry in KnownGraphics.Entries)
        {
            var width = entry.WidthPixels ?? 1042;
            var height = entry.HeightPixels ?? 580;

            Assert.True(entry.Matches(Image(entry.Fingerprint, width, height)), entry.Name);
            Assert.True(entry.Matches(Image(FlipBits(entry.Fingerprint, entry.MaximumDistance), width, height)), entry.Name);
            Assert.False(entry.Matches(Image(FlipBits(entry.Fingerprint, entry.MaximumDistance + 1), width, height)), entry.Name);
            Assert.False(InstructionEvidenceImages.IsRetentionCandidate(Image(entry.Fingerprint, width, height)), entry.Name);
        }
    }

    [Fact]
    public void ASizedEntryNeverMatchesAnImageOfAnotherSize()
    {
        // The pie chart's fingerprint on a photograph-sized image of another
        // size is a photograph, not the chart.
        var pieChart = Assert.Single(KnownGraphics.Entries, entry => entry.Name == "Bodyshop pie chart");

        Assert.False(pieChart.Matches(Image(pieChart.Fingerprint, 301, 300)));
        Assert.True(InstructionEvidenceImages.IsRetentionCandidate(Image(pieChart.Fingerprint, 301, 300)));
    }

    [Fact]
    public void TheCollisionEngineersLogoMatchesAtAnySize()
    {
        var logo = Assert.Single(KnownGraphics.Entries, entry => entry.Name == "Collision Engineers logo");

        Assert.Null(logo.WidthPixels);
        Assert.True(logo.Matches(Image(logo.Fingerprint, 3150, 1756)));
        Assert.True(logo.Matches(Image(logo.Fingerprint, 563, 318)));
    }

    /// <summary>
    /// The Repair Costs Analysis chart from page 5 of a.QDOS26123's
    /// "Bodyshopreport316478-V1.pdf" (R476), as the PDF reader takes it out:
    /// a 300x300 PNG with the soft mask as alpha. It passes the size rule, so
    /// only the bar chart entry keeps it out of the photographs.
    /// </summary>
    [Fact]
    public void TheBodyshopReportBarChartIsNotAPhotograph()
    {
        const string resource = "Pegasus.Core.Tests.Intake.Fixtures.bodyshop-report-316478-bar-chart.png";
        using var stream = typeof(KnownGraphicsTests).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"{resource} is not embedded.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        var chart = Assert.Single(ImageMeasurement.Complete(
            [Image(null, null, null) with { MediaType = "image/png", Content = content.ToArray() }]));

        Assert.Equal(300, chart.WidthPixels);
        Assert.Equal(300, chart.HeightPixels);
        Assert.Equal("Bodyshop bar chart", Assert.Single(KnownGraphics.Entries, entry => entry.Matches(chart)).Name);
        Assert.False(InstructionEvidenceImages.IsRetentionCandidate(chart));
    }

    [Fact]
    public void AnImageWithoutAFingerprintMatchesNothing()
    {
        Assert.DoesNotContain(KnownGraphics.Entries, entry => entry.Matches(Image(null, 1042, 580)));
    }

    /// <summary>
    /// The entries are distinct graphics: two sized entries of one size sit
    /// farther apart than their limits added together, and the one entry that
    /// matches any size is not within reach of a sized entry's reference, so
    /// an image is never two graphics at once.
    /// </summary>
    [Fact]
    public void TheEntriesAreDistinct()
    {
        var sized = KnownGraphics.Entries.Where(entry => entry.WidthPixels is not null).ToArray();
        foreach (var size in sized.GroupBy(entry => (entry.WidthPixels, entry.HeightPixels)))
        {
            var entries = size.ToArray();
            for (var first = 0; first < entries.Length; first++)
            {
                for (var second = first + 1; second < entries.Length; second++)
                {
                    Assert.True(
                        BitOperations.PopCount(entries[first].Fingerprint ^ entries[second].Fingerprint)
                            > entries[first].MaximumDistance + entries[second].MaximumDistance,
                        $"{entries[first].Name} and {entries[second].Name} can match one image.");
                }
            }
        }

        Assert.Equal(KnownGraphics.Entries.Count, KnownGraphics.Entries.Select(entry => entry.Name).Distinct().Count());
        foreach (var any in KnownGraphics.Entries.Where(entry => entry.WidthPixels is null))
        {
            Assert.All(sized, entry => Assert.False(
                any.Matches(Image(entry.Fingerprint, entry.WidthPixels!.Value, entry.HeightPixels!.Value)),
                entry.Name));
        }
    }

    private static ulong FlipBits(ulong fingerprint, int count) =>
        fingerprint ^ (count >= 64 ? ulong.MaxValue : (1UL << count) - 1);

    private static IntakeAssetCandidate Image(ulong? fingerprint, int? width, int? height) => new(
        "message, page 1, image 1",
        "page-1-image-1.jpg",
        "image/jpeg",
        new byte[1_000],
        IntakeAssetKind.EmbeddedImage,
        IntakeAssetDisposition.Embedded,
        1,
        WidthPixels: width,
        HeightPixels: height,
        Fingerprint: fingerprint);
}
