namespace Pegasus.ArchitectureTests;

/// <summary>
/// Tagging an image while the Case has unsaved changes posts at once and keeps
/// the draft: the picker marks the tags that leave the report, and the Case
/// workspace script has the one handler that skips the unsaved-changes
/// question for the picker's forms and for no other form.
/// </summary>
public sealed class ImageTagWhileEditingContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ThePickerMarksTheTagsThatTakeTheImageOutOfTheReport()
    {
        var picker = Read("src/Pegasus.Web/Pages/Cases/Shared/_CaseImageTagPicker.cshtml");

        Assert.Contains("data-tag-picker", picker, StringComparison.Ordinal);
        Assert.Contains("data-tag-leaves-report", picker, StringComparison.Ordinal);
        Assert.Contains("ImageTagVocabulary.TakesImageOutOfReport(tag.Id)", picker, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWorkspaceScriptPostsPickerFormsAtOnceOverADraft()
    {
        var script = Read("src/Pegasus.Web/wwwroot/js/case-workspace.js");
        var handler = script.IndexOf("form.closest('[data-tag-picker]')", StringComparison.Ordinal);

        Assert.True(handler >= 0, "The tag picker's at-once handler is missing.");
        Assert.Contains("data-tag-leaves-report", script[handler..], StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation()", script[handler..], StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(script, "form.closest('[data-tag-picker]')"));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate Pegasus.slnx.");
    }
}
