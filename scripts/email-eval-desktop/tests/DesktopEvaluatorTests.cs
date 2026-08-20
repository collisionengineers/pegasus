using MimeKit;
using Pegasus.Core.Intake;
using Pegasus.EmailEvaluation.Desktop;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.EmailEvaluation.Desktop.Tests;

public sealed class DesktopEvaluatorTests
{
    [Fact]
    public void CatalogParsesAllTwelveCategoriesWithoutReplyFolder()
    {
        var catalog = CategoryCatalog.Load();

        Assert.Equal(12, catalog.Categories.Count);
        Assert.Equal(
            [
                "General", "billing", "new-instruction-received", "non-client-related",
                "in-progress-cases", "post-report-emails", "pre-instruction-emails", "internal-cc",
                "Report sent", "case-rejected", "query-sent", "additional-image-request"
            ],
            catalog.Categories.Select(category => category.Name));
        Assert.DoesNotContain(catalog.Categories, category => category.Name.Contains("reply", StringComparison.OrdinalIgnoreCase));
        using var folder = TemporaryFolder();
        var workspace = new EvaluationWorkspace(folder.Path);
        workspace.EnsureTaxonomyFolders(catalog);
        Assert.All(catalog.Categories, category =>
            Assert.True(Directory.Exists(Path.Combine(folder.Path, "emailevallocal", category.Family, category.Name))));
    }

    [Fact]
    public async Task FolderQueueUsesCaseInsensitiveExtensionAndOrdinalFilenameOrder()
    {
        using var folder = TemporaryFolder();
        WriteFixture(folder.Path, "z-last.EML");
        WriteFixture(folder.Path, "A-first.eml");
        Directory.CreateDirectory(Path.Combine(folder.Path, "nested"));
        WriteFixture(Path.Combine(folder.Path, "nested"), "ignored.eml");
        await File.WriteAllTextAsync(Path.Combine(folder.Path, "not-email.txt"), "ignore");

        var workflow = CreateWorkflow();
        var snapshot = await workflow.SelectFolderAsync(folder.Path);

        Assert.Equal("A-first.eml", snapshot.Message?.FileName);
        Assert.True(snapshot.CanFile);
        Assert.True(snapshot.CanSkip);
        Assert.Equal("Suggested: No category", snapshot.Suggestion);

        await workflow.SkipAsync();
        Assert.Equal("z-last.EML", workflow.Snapshot.Message?.FileName);
        await workflow.SkipAsync();
        Assert.Equal("No unreviewed .eml files remain.", workflow.Snapshot.Status);
    }

    [Fact]
    public async Task RuleClassifiedEmailPopulatesCategorySubtypeEvidenceAndPolicyVersion()
    {
        using var folder = TemporaryFolder();
        WriteFixture(
            folder.Path,
            "autoreply.eml",
            subject: "Automatic reply: Case 128294.001",
            body: "I am currently out of the office.");

        var snapshot = await CreateWorkflow().SelectFolderAsync(folder.Path);

        // "General/autoreply" is the settled Core taxonomy category the QDOS
        // policy's subject.automatic-reply predicate resolves to.
        Assert.Equal(
            $"Suggested: Received / General/autoreply (policy {QdosMailClassificationPolicy.Key} v{QdosMailClassificationPolicy.Version})"
                + $"{Environment.NewLine}Evidence:"
                + $"{Environment.NewLine}  - subject.automatic-reply: The subject carries the generated 'Automatic reply:' prefix.",
            snapshot.Suggestion);

        var filed = await CreateWorkflow().SelectFolderAsync(folder.Path);
        Assert.Equal(snapshot.Suggestion, filed.Suggestion);
    }

    [Fact]
    public async Task CompletedSourceIsFilteredAndMalformedLogBlocksSelection()
    {
        using var folder = TemporaryFolder();
        var source = WriteFixture(folder.Path, "completed.eml");
        var workspace = new EvaluationWorkspace(folder.Path);
        var catalog = CategoryCatalog.Load();
        workspace.EnsureTaxonomyFolders(catalog);
        workspace.Commit(source, "Received", "General", null, "already reviewed", DateTimeOffset.UtcNow);

        var workflow = CreateWorkflow();
        var snapshot = await workflow.SelectFolderAsync(folder.Path);
        Assert.Equal("No unreviewed .eml files remain.", snapshot.Status);

        await File.AppendAllTextAsync(workspace.LogPath, "not-json\n");
        snapshot = await workflow.SelectFolderAsync(folder.Path);
        Assert.Contains("malformed", snapshot.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenuineFixtureDisplaysDecodedHeadersAndBody()
    {
        using var folder = TemporaryFolder();
        WriteFixture(folder.Path, "fixture.eml");

        var snapshot = await CreateWorkflow().SelectFolderAsync(folder.Path);

        Assert.NotNull(snapshot.Message);
        Assert.NotEmpty(snapshot.Message!.Subject);
        Assert.NotEmpty(snapshot.Message.Body);
        Assert.NotEmpty(snapshot.Message.From);
        Assert.NotEmpty(snapshot.Message.To);
    }

    [Fact]
    public void CategoryNamesRejectTraversalReservedNamesAndTrailingPunctuation()
    {
        Assert.Throws<ArgumentException>(() => EvaluationWorkspace.ValidateCategoryName(".."));
        Assert.Throws<ArgumentException>(() => EvaluationWorkspace.ValidateCategoryName("CON"));
        Assert.Throws<ArgumentException>(() => EvaluationWorkspace.ValidateCategoryName("name/escape"));
        Assert.Throws<ArgumentException>(() => EvaluationWorkspace.ValidateCategoryName("name."));
        Assert.Equal("Needs review", EvaluationWorkspace.ValidateCategoryName(" Needs review "));
    }

    [Fact]
    public async Task FilingCopiesSourceAndEscapesReasonInOneJsonLine()
    {
        using var folder = TemporaryFolder();
        var source = WriteFixture(folder.Path, "file-me.eml");
        var workflow = CreateWorkflow();
        var loaded = await workflow.SelectFolderAsync(folder.Path);

        var result = await workflow.TryFileAsync(
            "Received",
            "General",
            null,
            "A quote: \"acknowledged\"\nwith a new line");

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(folder.Path, "emailevallocal", "Received", "General", "file-me.eml")));
        Assert.True(File.Exists(source));
        var logLines = await File.ReadAllLinesAsync(Path.Combine(folder.Path, "emailevallocal", "evaluation-log.jsonl"));
        Assert.Single(logLines);
        using var document = System.Text.Json.JsonDocument.Parse(logLines[0]);
        Assert.Equal("A quote: \"acknowledged\"\nwith a new line", document.RootElement.GetProperty("reason").GetString());
        Assert.Null(document.RootElement.GetProperty("suggestedCategory").GetString());
        Assert.DoesNotContain('\n', logLines[0]);
        Assert.Equal("No unreviewed .eml files remain.", result.Snapshot.Status);
    }

    [Fact]
    public void LogFailureRollsBackCopiedEmail()
    {
        using var folder = TemporaryFolder();
        var source = WriteFixture(folder.Path, "log-failure.eml");
        var workspace = new EvaluationWorkspace(folder.Path);
        workspace.EnsureTaxonomyFolders(CategoryCatalog.Load());
        Directory.CreateDirectory(workspace.LogPath);

        Assert.ThrowsAny<Exception>(() =>
            workspace.Commit(source, "Received", "General", null, "reason", DateTimeOffset.UtcNow));
        Assert.False(File.Exists(Path.Combine(folder.Path, "emailevallocal", "Received", "General", "log-failure.eml")));
    }

    [Fact]
    public async Task OtherCreatesCustomFolderAndCollisionDoesNotAdvance()
    {
        using var folder = TemporaryFolder();
        WriteFixture(folder.Path, "other.eml");
        var workflow = CreateWorkflow();
        await workflow.SelectFolderAsync(folder.Path);

        var filed = await workflow.TryFileAsync("Other", "Other", "Custom review", "manual reason");
        Assert.True(filed.Success);
        Assert.True(File.Exists(Path.Combine(folder.Path, "emailevallocal", "Other", "Custom review", "other.eml")));

        var workspace = new EvaluationWorkspace(folder.Path);
        var lowerCasePath = workspace.ResolveDestination("Other", "custom review", "second.eml");
        Directory.CreateDirectory(Path.GetDirectoryName(lowerCasePath)!);
        var mixedCasePath = workspace.ResolveDestination("Other", "CUSTOM REVIEW", "third.eml");
        Assert.Equal(Path.GetDirectoryName(lowerCasePath), Path.GetDirectoryName(mixedCasePath), StringComparer.OrdinalIgnoreCase);
        Assert.Single(Directory.EnumerateDirectories(Path.Combine(folder.Path, "emailevallocal", "Other")));

        using var second = TemporaryFolder();
        WriteFixture(second.Path, "collision.eml");
        var secondWorkflow = CreateWorkflow();
        await secondWorkflow.SelectFolderAsync(second.Path);
        var destination = Path.Combine(second.Path, "emailevallocal", "Received", "General", "collision.eml");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(destination, "existing");
        var blocked = await secondWorkflow.TryFileAsync("Received", "General", null, "reason");
        Assert.False(blocked.Success);
        Assert.Contains("already exists", blocked.Snapshot.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("collision.eml", blocked.Snapshot.Message?.FileName);
        Assert.False(File.Exists(Path.Combine(second.Path, "emailevallocal", "evaluation-log.jsonl")));
    }

    private static EmailEvaluationWorkflow CreateWorkflow() =>
        new(
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System),
            new QdosInstructionExtractionPolicy(),
            new QdosMailClassificationPolicy(),
            CategoryCatalog.Load());

    /// <summary>
    /// Builds a deterministic in-memory .eml fixture and writes it under
    /// <paramref name="destination"/>. Content is synthetic test data, never a
    /// repository-tracked or corpus file (corpus/ is local, ignored and immutable).
    /// </summary>
    private static string WriteFixture(
        string destination,
        string fileName,
        string subject = "Case update",
        string body = "Please see the update below regarding the case.")
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Synthetic sender", "sender@example.test"));
        message.To.Add(new MailboxAddress("Pegasus review", "review@example.test"));
        message.Date = DateTimeOffset.UtcNow;
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        var target = Path.Combine(destination, fileName);
        using var stream = File.Create(target);
        message.WriteTo(stream);
        return target;
    }

    private static TempDirectory TemporaryFolder() => new();

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PegasusEmailEvalTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
