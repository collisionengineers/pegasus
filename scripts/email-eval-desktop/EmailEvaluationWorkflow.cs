using System.Security.Cryptography;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.EmailEvaluation.Desktop;

public sealed record EmailDisplayMessage(
    string FileName,
    string From,
    string To,
    string Cc,
    string SentAt,
    string Subject,
    string Body,
    IReadOnlyList<string> AttachmentNames);

public sealed record EvaluationSnapshot(
    string Status,
    string Suggestion,
    EmailDisplayMessage? Message,
    string? Error,
    bool CanFile,
    bool CanSkip,
    IReadOnlyList<EmailCategory> Categories);

public sealed class EmailEvaluationWorkflow
{
    /// <summary>
    /// The Actor recorded against the advisory intake read used only to produce
    /// the rule suggestion; never a Pegasus staff identity.
    /// </summary>
    private const string LocalReviewActor = "email-eval-desktop-reviewer";

    private readonly IIntakeSourceReader sourceReader;
    private readonly IInstructionExtractionPolicy extractionPolicy;
    private readonly IMailClassificationPolicy classificationPolicy;
    private readonly CategoryCatalog catalog;
    private readonly TimeProvider timeProvider;
    private List<string> queue = [];
    private int index;
    private EvaluationWorkspace? workspace;
    private string? currentPath;
    private string? suggestion;
    private EmailDisplayMessage? message;
    private string status = "Select a folder containing .eml files.";
    private string? error;
    private bool canFile;

    public EmailEvaluationWorkflow(
        IIntakeSourceReader sourceReader,
        IInstructionExtractionPolicy extractionPolicy,
        IMailClassificationPolicy classificationPolicy,
        CategoryCatalog catalog,
        TimeProvider? timeProvider = null)
    {
        this.sourceReader = sourceReader;
        this.extractionPolicy = extractionPolicy;
        this.classificationPolicy = classificationPolicy;
        this.catalog = catalog;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public EvaluationSnapshot Snapshot => new(
        status,
        suggestion is null ? "Suggested: No category" : $"Suggested: {suggestion}",
        message,
        error,
        canFile,
        currentPath is not null,
        catalog.Categories);

    public async Task<EvaluationSnapshot> SelectFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        currentPath = null;
        message = null;
        suggestion = null;
        canFile = false;
        error = null;
        if (!Directory.Exists(folderPath))
        {
            return Fail("The selected folder does not exist.");
        }

        workspace = new EvaluationWorkspace(folderPath);
        try
        {
            workspace.EnsureTaxonomyFolders(catalog);
            var completed = workspace.ReadCompletedSourcePaths();
            var candidates = Directory.EnumerateFiles(workspace.SelectedFolder, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetExtension(path), ".eml", StringComparison.OrdinalIgnoreCase))
                .ToList();
            queue = candidates
                .Where(path => !completed.Contains(Path.GetFullPath(path)))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            index = 0;
            if (queue.Count == 0)
            {
                return Clear(candidates.Count == 0
                    ? "No .eml files found in the selected folder."
                    : "No unreviewed .eml files remain.");
            }

            await LoadCurrentAsync(cancellationToken);
            return Snapshot;
        }
        catch (InvalidDataException exception)
        {
            return Fail(exception.Message);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return Fail($"Unable to open the selected folder: {exception.Message}");
        }
    }

    public async Task<EvaluationSnapshot> SkipAsync(CancellationToken cancellationToken = default)
    {
        if (currentPath is null)
        {
            return Snapshot;
        }

        index++;
        if (index >= queue.Count)
        {
            currentPath = null;
            message = null;
            suggestion = null;
            canFile = false;
            status = "No unreviewed .eml files remain.";
            error = null;
            return Snapshot;
        }

        await LoadCurrentAsync(cancellationToken);
        return Snapshot;
    }

    public async Task<(bool Success, EvaluationSnapshot Snapshot)> TryFileAsync(
        string family,
        string category,
        string? customCategory,
        string reasoning,
        CancellationToken cancellationToken = default)
    {
        if (workspace is null || currentPath is null)
        {
            return (false, Fail("Select a folder and load an email before filing."));
        }

        error = null;
        if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(category))
        {
            return (false, Fail("Choose a category before filing the email."));
        }

        var reason = reasoning.Trim();
        if (reason.Length == 0)
        {
            return (false, Fail("Explain why this is the correct category before filing."));
        }

        var selectedCategory = category;
        if (string.Equals(family, "Other", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                selectedCategory = EvaluationWorkspace.ValidateCategoryName(customCategory ?? string.Empty);
            }
            catch (ArgumentException exception)
            {
                return (false, Fail(exception.Message));
            }
        }
        else if (catalog.Find(family, category) is null)
        {
            return (false, Fail("Choose one of the retained Received or Sent categories."));
        }

        try
        {
            workspace.Commit(
                currentPath,
                family,
                selectedCategory,
                suggestion,
                reason,
                timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return (false, Fail(exception.Message));
        }

        await SkipAsync(cancellationToken);
        return (true, Snapshot);
    }

    private async Task LoadCurrentAsync(CancellationToken cancellationToken)
    {
        currentPath = queue[index];
        message = null;
        suggestion = null;
        canFile = false;
        error = null;
        status = $"Reviewing {Path.GetFileName(currentPath)}";

        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(currentPath, cancellationToken);
            var display = await LocalEmailDisplayReader.ReadAsync(new MemoryStream(content), cancellationToken);
            message = new EmailDisplayMessage(
                Path.GetFileName(currentPath),
                display.From,
                display.To,
                display.Cc,
                display.SentAt,
                display.Subject,
                display.Body,
                display.AttachmentNames);
            canFile = true;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            error = $"Unable to read this email: {exception.Message}";
            status = "The email could not be parsed. Skip it or correct the source file.";
            canFile = false;
            return;
        }

        // The rule suggestion is advisory evidence beside the human review (ADR-0016);
        // a fault evaluating it must never block the display or filing the message
        // already supports, so it is isolated behind its own recoverable-fault guard.
        try
        {
            var intakeSource = new IntakeSource(
                Path.GetFileName(currentPath),
                "message/rfc822",
                content,
                timeProvider.GetUtcNow(),
                LocalReviewActor,
                new(IntakeSourceChannel.ManualUpload, Convert.ToHexString(SHA256.HashData(content))));
            var readResult = await sourceReader.ReadAsync(intakeSource, cancellationToken);
            suggestion = FormatSuggestion(classificationPolicy.Classify(readResult));
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            suggestion = null;
        }
    }

    /// <summary>
    /// Renders the rule-generated category, subtype, matched evidence and policy
    /// version beside the human review. Unclassified stays null (rendered as
    /// "No category"): the reviewer's own selection remains authoritative either way.
    /// </summary>
    private static string? FormatSuggestion(MailClassificationResult result)
    {
        if (result.Outcome == MailClassificationOutcome.Unclassified)
        {
            return null;
        }

        var headline = result.Outcome == MailClassificationOutcome.Classified
            ? FormatCategory(result.Category!)
            : $"Ambiguous ({string.Join(", ", result.AmbiguousCandidates)})";

        var matched = result.Predicates.Where(predicate => predicate.Matched).ToArray();
        var evidenceLines = matched.Length == 0
            ? "  (no matched predicates)"
            : string.Join(
                Environment.NewLine,
                matched.Select(predicate => $"  - {predicate.Key}: {predicate.Detail}"));

        return $"{headline} (policy {result.PolicyKey} v{result.PolicyVersion}){Environment.NewLine}Evidence:{Environment.NewLine}{evidenceLines}";
    }

    private static string FormatCategory(MailCategory category)
    {
        var direction = category.Direction == MailDirection.Received ? "Received" : "Sent";
        var name = category.Subtype is null ? category.Name : $"{category.Name}/{category.Subtype}";
        return $"{direction} / {name}";
    }

    private EvaluationSnapshot Clear(string messageText)
    {
        queue = [];
        index = 0;
        currentPath = null;
        message = null;
        suggestion = null;
        canFile = false;
        status = messageText;
        error = null;
        return Snapshot;
    }

    private EvaluationSnapshot Fail(string messageText)
    {
        error = messageText;
        canFile = false;
        status = "Action required.";
        return Snapshot;
    }
}
