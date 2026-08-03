using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.ImageIntake;
using Pegasus.Infrastructure.Vision;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The open-decision-1 evaluation harness. Local-only (`Category=Corpus`): it
/// reads the ignored immutable corpus, runs the vendored engine over the
/// case-attributed labelled cohort, and writes the wrong-suggestion-rate
/// report under `artifacts/vrm-recognition-eval/`. Labels are parsed from the
/// corpus's own case-export file names and never leave this machine. The
/// deterministic 20% holdout is untouched unless
/// `PEGASUS_VRM_EVAL_HOLDOUT=1` explicitly evaluates it once at the fixed
/// bar. `PEGASUS_VRM_EVAL_LIMIT` bounds a run; the report records exactly
/// what was and was not evaluated — a bounded run is never presented as the
/// full cohort.
/// </summary>
public sealed class VrmRecognitionCorpusEvaluationTests
{
    private static readonly double[] CandidateThresholds = [0.5, 0.6, 0.7, 0.8, 0.9];

    private static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = true };

    [SkippableCorpusFact]
    [Trait("Category", "Corpus")]
    public async Task EvaluateLabelledCohortAndProposeTheProvisionalBar()
    {
        var corpusRoot = CorpusLocator.CorpusRoot!;
        var samples = LabelledSamples(corpusRoot);
        Assert.True(samples.Count > 0, "The corpus holds no case-attributed labelled images.");

        // Deterministic split by relative-path hash: 80% cohort, 20% untouched
        // holdout. The holdout is evaluated only on explicit request, once the
        // bar has been fixed from the cohort.
        var ordered = samples
            .OrderBy(sample => sample.PathHash, StringComparer.Ordinal)
            .ToArray();
        var holdoutStart = (int)(ordered.Length * 0.8);
        var cohort = ordered[..holdoutStart];
        var holdout = ordered[holdoutStart..];
        var evaluateHoldout = Environment.GetEnvironmentVariable("PEGASUS_VRM_EVAL_HOLDOUT") == "1";
        var target = evaluateHoldout ? holdout : cohort;

        var limit = int.TryParse(
            Environment.GetEnvironmentVariable("PEGASUS_VRM_EVAL_LIMIT"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedLimit) && parsedLimit > 0
            ? Math.Min(parsedLimit, target.Length)
            : target.Length;
        var evaluated = target[..limit];

        using var engine = new OnnxVrmRecognitionEngine();
        var outcomes = new List<SampleOutcome>(evaluated.Length);
        foreach (var sample in evaluated)
        {
            var bytes = await File.ReadAllBytesAsync(sample.Path);
            var result = await engine.RecognizeAsync(bytes, CancellationToken.None);
            var best = result.Candidates.Count > 0 ? result.Candidates[0] : null;
            outcomes.Add(new(
                sample.Label,
                result.Kind,
                best?.NormalizedRegistration,
                best?.Confidence ?? 0));
        }

        var thresholds = CandidateThresholds
            .Select(threshold =>
            {
                var suggested = outcomes
                    .Where(outcome => outcome.Kind == VrmRecognitionOutcomeKind.Suggested
                        && outcome.BestRegistration is not null
                        && outcome.BestConfidence >= threshold)
                    .ToArray();
                var wrong = suggested.Count(outcome =>
                    !string.Equals(outcome.BestRegistration, outcome.Label, StringComparison.Ordinal));
                return new
                {
                    threshold,
                    evaluated = outcomes.Count,
                    suggestionRate = Rate(suggested.Length, outcomes.Count),
                    wrongSuggestionRate = Rate(wrong, suggested.Length),
                    abstentionRate = Rate(outcomes.Count - suggested.Length, outcomes.Count),
                    suggestions = suggested.Length,
                    wrongSuggestions = wrong
                };
            })
            .ToArray();

        var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var reportDirectory = Path.Combine(
            CorpusLocator.RepositoryRoot,
            "artifacts",
            "vrm-recognition-eval",
            runId);
        Directory.CreateDirectory(reportDirectory);
        var report = new
        {
            runId,
            engine = "fast-alpr-onnx v1 (ADR-0018)",
            corpusImages = samples.Count,
            cohortSize = cohort.Length,
            holdoutSize = holdout.Length,
            evaluatedSet = evaluateHoldout ? "holdout" : "cohort",
            evaluatedCount = evaluated.Length,
            evaluationBounded = evaluated.Length < target.Length,
            technicalFailures = outcomes.Count(outcome =>
                outcome.Kind == VrmRecognitionOutcomeKind.TechnicalFailure),
            thresholds
        };
        var reportPath = Path.Combine(reportDirectory, "report.json");
        await File.WriteAllTextAsync(
            reportPath,
            JsonSerializer.Serialize(report, ReportJsonOptions));
        Assert.True(File.Exists(reportPath));
    }

    private static double Rate(int part, int whole) =>
        whole == 0 ? 0 : Math.Round((double)part / whole, 4);

    /// <summary>
    /// Case-export file names attribute images to a case-level VRM as
    /// `...__{PROVIDER}_{VRM}_img_{n}_{m}.{ext}` (provider prefix optional,
    /// `UnknownVRM` for unattributed exports). Unlabelled images are excluded
    /// from the accuracy cohort.
    /// </summary>
    private static List<LabelledSample> LabelledSamples(string corpusRoot)
    {
        var samples = new List<LabelledSample>();
        foreach (var path in Directory.EnumerateFiles(corpusRoot, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(path);
            if (!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var label = ParseLabel(Path.GetFileNameWithoutExtension(path));
            if (label is null)
            {
                continue;
            }

            var relative = Path.GetRelativePath(corpusRoot, path);
            samples.Add(new(
                path,
                label,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relative)))));
        }

        return samples;
    }

    private static string? ParseLabel(string fileName)
    {
        var markerIndex = fileName.LastIndexOf("_img_", StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return null;
        }

        var prefix = fileName[..markerIndex];
        var separatorIndex = prefix.LastIndexOf("__", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return null;
        }

        var attribution = prefix[(separatorIndex + 2)..];
        var token = attribution.Split('_', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (token is null || token.Equals("UnknownVRM", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = token.ToUpperInvariant();
        if (normalized.Length is < 2 or > 10
            || !normalized.All(character =>
                char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
            || !normalized.Any(char.IsAsciiDigit)
            || !normalized.Any(char.IsAsciiLetterUpper))
        {
            return null;
        }

        return normalized;
    }

    private sealed record LabelledSample(string Path, string Label, string PathHash);

    private sealed record SampleOutcome(
        string Label,
        VrmRecognitionOutcomeKind Kind,
        string? BestRegistration,
        double BestConfidence);
}

internal static class CorpusLocator
{
    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null
                && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    /// <summary>
    /// `corpus/` at the repository root, or `PEGASUS_CORPUS_ROOT` when the
    /// local immutable corpus lives beside another checkout (task worktrees
    /// do not carry the ignored corpus).
    /// </summary>
    public static string? CorpusRoot
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable("PEGASUS_CORPUS_ROOT");
            if (!string.IsNullOrWhiteSpace(overridden) && Directory.Exists(overridden))
            {
                return overridden;
            }

            var local = Path.Combine(RepositoryRoot, "corpus");
            return Directory.Exists(local) ? local : null;
        }
    }
}

internal sealed class SkippableCorpusFactAttribute : FactAttribute
{
    public SkippableCorpusFactAttribute()
    {
        if (CorpusLocator.CorpusRoot is null)
        {
            Skip = "The ignored local corpus/ is absent; the VRM evaluation was not run.";
        }
    }
}
