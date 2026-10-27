using Pegasus.Core.Intake;

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

    [Fact]
    public void AnImageWithoutAFingerprintMatchesNothing()
    {
        Assert.DoesNotContain(KnownGraphics.Entries, entry => entry.Matches(Image(null, 1042, 580)));
    }

    /// <summary>
    /// The entries are distinct graphics: no two sized entries share a size,
    /// and the one entry that matches any size is not within reach of a
    /// sized entry's reference, so an image is never two graphics at once.
    /// </summary>
    [Fact]
    public void TheEntriesAreDistinct()
    {
        var sized = KnownGraphics.Entries.Where(entry => entry.WidthPixels is not null).ToArray();
        Assert.Equal(sized.Length, sized.Select(entry => (entry.WidthPixels, entry.HeightPixels)).Distinct().Count());
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

    private static IntakeAssetCandidate Image(ulong? fingerprint, int width, int height) => new(
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
