using System.Text;
using System.Text.Json;

namespace Pegasus.JevMailEvaluation;

public sealed record ConditionMetrics(
    string Condition,
    int Count,
    double FamilyAccuracy,
    double FamilyMacroF1,
    double FamilyTopTwoRecall,
    double DetailedAccuracy,
    double MeanConfidence,
    double MeanInputTokens,
    double MeanLatencyMilliseconds);

public sealed record ConfidenceCoverage(double Threshold, int Covered, double Coverage, double Accuracy);

public sealed record EvaluationSummary(
    string ActualModel,
    string PromptVersion,
    int CohortCount,
    IReadOnlyList<ConditionMetrics> Conditions,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>> ConfusionMatrices,
    IReadOnlyDictionary<string, IReadOnlyList<ConfidenceCoverage>> ConfidenceCoverage,
    int FlatHierarchicalDisagreements,
    int StabilityCases,
    double StabilityAgreement,
    int FailureCount,
    IReadOnlyDictionary<string, int> FailuresByStage);

public static class EvaluationReportWriter
{
    public static async Task<EvaluationSummary> WriteAsync(
        IReadOnlyDictionary<string, GoldLabel> labels,
        IReadOnlyList<JevPrediction> predictions,
        string outputDirectory,
        IReadOnlyList<EvaluationFailure>? failures = null,
        CancellationToken cancellationToken = default)
    {
        JevEvaluationRunner.EnsureSingleModel(predictions);
        var primary = predictions.Where(item => item.Repeat == 0).ToArray();
        var labelledPrimary = primary.Where(item => labels.ContainsKey(item.Id)).ToArray();
        var conditions = labelledPrimary.GroupBy(item => item.Condition, StringComparer.Ordinal)
            .Select(group => Metrics(group.Key, group.ToArray(), labels))
            .OrderBy(item => item.Condition, StringComparer.Ordinal)
            .ToArray();
        var curves = labelledPrimary.GroupBy(item => item.Condition, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ConfidenceCoverage>)Enumerable.Range(10, 10)
                    .Select(value => Coverage(value / 20d, group.ToArray(), labels))
                    .ToArray(),
                StringComparer.Ordinal);
        var confusion = labelledPrimary.GroupBy(item => item.Condition, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>)group
                    .GroupBy(item => labels[item.Id].Family, StringComparer.Ordinal)
                    .ToDictionary(
                        row => row.Key,
                        row => (IReadOnlyDictionary<string, int>)row.GroupBy(item => item.Family, StringComparer.Ordinal)
                            .ToDictionary(cell => cell.Key, cell => cell.Count(), StringComparer.Ordinal),
                        StringComparer.Ordinal),
                StringComparer.Ordinal);
        var paired = primary.Where(item => item.Condition is "flat" or "hierarchical")
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(group => group.Count() == 2)
            .ToArray();
        var repeated = predictions.Where(item => item.Repeat > 0)
            .GroupBy(item => (item.Id, item.Condition))
            .ToArray();
        var stableComparisons = repeated.Sum(group => group.Count());
        var stableMatches = repeated.Sum(group =>
        {
            var baseline = predictions.Single(item => item.Id == group.Key.Id && item.Condition == group.Key.Condition && item.Repeat == 0);
            return group.Count(item => item.Family == baseline.Family && item.Subtype == baseline.Subtype);
        });
        var first = predictions.Count == 0 ? null : predictions[0];
        var recordedFailures = failures ?? [];
        var summary = new EvaluationSummary(
            first?.ActualModel ?? "none",
            first?.PromptVersion ?? JevQuestions.PromptVersion,
            labels.Count,
            conditions,
            confusion,
            curves,
            paired.Count(group => group.Select(item => item.Family).Distinct(StringComparer.Ordinal).Count() > 1),
            repeated.Length,
            stableComparisons == 0 ? 1 : stableMatches / (double)stableComparisons,
            recordedFailures.Count,
            recordedFailures.GroupBy(item => item.Stage, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));

        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "summary.json"),
            JsonSerializer.Serialize(summary, EvaluationJson.Options),
            cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "summary.md"), Markdown(summary), cancellationToken);
        return summary;
    }

    internal static ConditionMetrics Metrics(string condition, IReadOnlyList<JevPrediction> predictions, IReadOnlyDictionary<string, GoldLabel> labels)
    {
        var familyAccuracy = predictions.Average(item => item.Family == labels[item.Id].Family ? 1d : 0d);
        var detailedAccuracy = predictions.Average(item =>
            item.Family == labels[item.Id].Family && item.Subtype == labels[item.Id].Subtype ? 1d : 0d);
        var topTwoRecall = predictions.Average(item => item.FamilyProbabilities
            .OrderByDescending(pair => pair.Value)
            .Take(2)
            .Any(pair => pair.Key == labels[item.Id].Family) ? 1d : 0d);
        var classes = labels.Values.Select(item => item.Family).Distinct(StringComparer.Ordinal).ToArray();
        var f1 = classes.Average(label =>
        {
            var tp = predictions.Count(item => item.Family == label && labels[item.Id].Family == label);
            var fp = predictions.Count(item => item.Family == label && labels[item.Id].Family != label);
            var fn = predictions.Count(item => item.Family != label && labels[item.Id].Family == label);
            return tp == 0 ? 0 : 2d * tp / ((2d * tp) + fp + fn);
        });
        return new(
            condition,
            predictions.Count,
            familyAccuracy,
            f1,
            topTwoRecall,
            detailedAccuracy,
            predictions.Average(item => item.Confidence),
            predictions.Average(item => item.InputTokens),
            predictions.Average(item => item.LatencyMilliseconds));
    }

    private static ConfidenceCoverage Coverage(double threshold, JevPrediction[] predictions, IReadOnlyDictionary<string, GoldLabel> labels)
    {
        var covered = predictions.Where(item => item.Confidence >= threshold).ToArray();
        return new(
            threshold,
            covered.Length,
            covered.Length / (double)predictions.Length,
            covered.Length == 0 ? 0 : covered.Average(item => item.Family == labels[item.Id].Family ? 1d : 0d));
    }

    private static string Markdown(EvaluationSummary summary)
    {
        var text = new StringBuilder()
            .AppendLine("# Jev mail evaluation")
            .AppendLine();
        text.AppendLine(FormattableString.Invariant($"- Model: `{summary.ActualModel}`"));
        text.AppendLine(FormattableString.Invariant($"- Prompt: `{summary.PromptVersion}`"));
        text.AppendLine(FormattableString.Invariant($"- Labelled cohort: {summary.CohortCount}"));
        text.AppendLine(FormattableString.Invariant($"- Flat/hierarchical family disagreements: {summary.FlatHierarchicalDisagreements}"));
        text.AppendLine(FormattableString.Invariant($"- Repeat stability: {summary.StabilityAgreement:P1} across {summary.StabilityCases} condition/message groups"));
        text.AppendLine(FormattableString.Invariant($"- Recorded failures: {summary.FailureCount}"));
        text.AppendLine();
        text.AppendLine("| Condition | N | Family accuracy | Macro F1 | Top-two recall | Detailed accuracy | Mean confidence | Mean input tokens | Mean latency |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var condition in summary.Conditions)
        {
            text.AppendLine(FormattableString.Invariant($"| {condition.Condition} | {condition.Count} | {condition.FamilyAccuracy:P1} | {condition.FamilyMacroF1:F3} | {condition.FamilyTopTwoRecall:P1} | {condition.DetailedAccuracy:P1} | {condition.MeanConfidence:F3} | {condition.MeanInputTokens:F0} | {condition.MeanLatencyMilliseconds:F0} ms |"));
        }
        text.AppendLine().AppendLine("This local pilot is evidence only and does not activate Jev in Pegasus production paths.");
        return text.ToString();
    }
}
