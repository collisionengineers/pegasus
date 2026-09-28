using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The printed report holds no invented wording (operator, 27 September
/// 2026): every word it prints is Core's, and the layout decides geometry
/// alone. So the files that lay the report out hold no printed text of their
/// own, not a heading, a label or the word "Page".
/// </summary>
public sealed partial class ReportLayoutWordingTests
{
    [Theory]
    [InlineData("AssessmentReportLayout.cs")]
    [InlineData("ReportChrome.cs")]
    public void TheReportsLayoutHoldsNoPrintedTextOfItsOwn(string file)
    {
        var source = Source(file);

        // What is printed is handed to it, never written in it.
        Assert.All(
            PrintedLiteral().Matches(source),
            printed => Assert.True(
                string.IsNullOrWhiteSpace(printed.Groups["text"].Value),
                $"{file} prints \"{printed.Groups["text"].Value}\", which Core does not supply."));
        // Beyond what it refuses with, a colour and the name of its typeface,
        // it holds no words at all.
        var words = Literal().Matches(Refusal().Replace(source, string.Empty))
            .Select(literal => Escape().Replace(literal.Groups["text"].Value, string.Empty))
            .Where(text => text.Any(char.IsLetter) && !Colour().IsMatch(text) && text != "Liberation Sans")
            .ToArray();
        Assert.True(words.Length == 0, $"{file} holds the words: {string.Join(" | ", words)}");
    }

    /// <summary>The vehicle drawing carries no words: it is drawn, and nothing in it is set in type.</summary>
    [Fact]
    public void TheVehicleDrawingCarriesNoWords()
    {
        var source = Source("DamagePlanDrawing.cs");

        Assert.DoesNotContain("<text", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("QuestPDF", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FRONT", source, StringComparison.Ordinal);
        Assert.DoesNotContain("REAR\"", source, StringComparison.Ordinal);
    }

    /// <summary>The file with its comments taken out.</summary>
    private static string Source(string file)
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Infrastructure", "Reports", file));
        return Comment().Replace(source, string.Empty);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex(@"^\s*//.*$", RegexOptions.Multiline)]
    private static partial Regex Comment();

    /// <summary>A text written where it would be printed: handed to Text, Span or Line.</summary>
    [GeneratedRegex(@"\b(?:Text|Span|Line)\(\s*\$?""(?<text>(?:[^""\\]|\\.)*)""")]
    private static partial Regex PrintedLiteral();

    [GeneratedRegex(@"\$?""(?<text>(?:[^""\\]|\\.)*)""")]
    private static partial Regex Literal();

    /// <summary>What a render is refused with is read on screen, never printed.</summary>
    [GeneratedRegex(@"throw new \w+\([^;]*;", RegexOptions.Singleline)]
    private static partial Regex Refusal();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Colour();

    /// <summary>A line break or another escaped character, which is no word.</summary>
    [GeneratedRegex(@"\\.")]
    private static partial Regex Escape();
}
