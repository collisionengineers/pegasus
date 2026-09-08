using System.Security.Cryptography;
using Pegasus.Infrastructure.Intake;
using UglyToad.PdfPig;

namespace Pegasus.IntegrationTests;

[Trait("Category", "Corpus")]
public sealed class PdfOcrQualificationTests
{
    [Theory]
    [InlineData("1046012231790__VX21TZD calculation sheet.pdf", "c75b94438ad6a57aae8b6edb8de554498920c1626cb0c8b0046a3322a55c1016", 3, false)]
    [InlineData("1313339771083__LT72PYX Calculation....pdf", "3bc7244f310be857b82ff87c7c2e3de3c23ff8f43a5109599b1d5f77b0bdd09d", 6, false)]
    [InlineData("1710254173321__Calculation ML23 OXR.pdf", "a0a56cc291b93c5a28c9ef575b98c65aee759fd9bedfd811a1463ebe6e0817f8", 4, false)]
    [InlineData("1952640666665__YL69YFO CALCULATION SHEET.pdf", "6918c91fce058b5365446681045056158336b90e1de89ba1bdc5a1d7394f728c", 6, true)]
    [InlineData("2228602993671__CalculationPDF.pdf", "ab3472cd160a08f19251439c02966f747753051a1204db97a2465d505a0bb45f", 4, false)]
    public void OnlyTheGenuineAnonymousType3ExportQualifies(
        string fileName, string expectedHash, int expectedPages, bool qualifies)
    {
        var bytes = File.ReadAllBytes(FindSource(fileName));
        Assert.Equal(expectedHash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var document = PdfDocument.Open(bytes);
        Assert.Equal(expectedPages, document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            Assert.Equal(qualifies, PdfOcrQualification.HasUnusableTextMap(document, page));
            Assert.NotEmpty(page.Letters);
            // The failure is not a replacement-character signal: readable and
            // unusable exports both lack it. Original bytes are never modified.
            Assert.DoesNotContain('\uFFFD', page.Text);
        }
        if (qualifies)
            Assert.DoesNotContain("YL69YFO", string.Join("\n", document.GetPages().Select(page => page.Text)));
    }

    private static string FindSource(string fileName)
    {
        // Reuse the repository locator; task worktrees do not carry this ignored
        // supplied pack, so inspect ancestors as well as the active worktree.
        var directory = new DirectoryInfo(CorpusPackage.RepositoryRoot);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "pegasus_pack", "glasses-integration", "glass_ref_docs", fileName);
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("The immutable supplied Glass PDF is absent; genuine-source evidence cannot run.", fileName);
    }
}
