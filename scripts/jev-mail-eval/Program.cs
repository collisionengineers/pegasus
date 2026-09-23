namespace Pegasus.JevMailEvaluation;

public static class Program
{
    public static Task<int> Main(string[] args) => ProgramEntry.RunAsync(args);
}

public static class ProgramEntry
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Help();
            return 0;
        }
        try
        {
            var options = Options.Parse(args.Skip(1).ToArray());
            switch (args[0])
            {
                case "prepare":
                    {
                        var entries = await CorpusSampler.PrepareAsync(options.Required("corpus"), cancellationToken: cancellationToken);
                        await EvaluationJson.WriteJsonLinesAsync(options.Required("cohort"), entries, cancellationToken);
                        Console.WriteLine($"Prepared {entries.Count} unique messages: {string.Join(", ", entries.GroupBy(item => item.Stratum).Select(group => $"{group.Key}={group.Count()}"))}.");
                        break;
                    }
                case "review":
                    await new CohortReviewer(Console.In, Console.Out, new EvidenceStateBuilder()).ReviewAsync(
                        options.Required("corpus"), options.Required("cohort"), options.Required("labels"), cancellationToken);
                    break;
                case "classify":
                    {
                        if (!options.Flag("acknowledge-external-upload"))
                        {
                            throw new InvalidOperationException("The classify command requires --acknowledge-external-upload.");
                        }
                        var apiKey = Environment.GetEnvironmentVariable("jev_api_key");
                        if (string.IsNullOrWhiteSpace(apiKey))
                        {
                            throw new InvalidOperationException("jev_api_key is not available in the environment.");
                        }
                        var path = options.Value("file");
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            Console.Write("Full path to .eml file: ");
                            path = Console.ReadLine();
                        }
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            throw new ArgumentException("A full .eml file path is required.");
                        }
                        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
                        var resolvedPath = JevSingleMailClassifier.ResolveEmlPath(path);
                        Console.WriteLine("Reading email and asking JEV...");
                        var result = await new JevSingleMailClassifier(new(httpClient, apiKey), new())
                            .ClassifyAsync(resolvedPath, cancellationToken);
                        Console.Write(PlainTextClassificationFormatter.Format(resolvedPath, result));
                        break;
                    }
                case "run":
                    {
                        if (!options.Flag("acknowledge-external-upload"))
                        {
                            throw new InvalidOperationException("The run command requires --acknowledge-external-upload.");
                        }
                        var apiKey = Environment.GetEnvironmentVariable("jev_api_key");
                        if (string.IsNullOrWhiteSpace(apiKey))
                        {
                            throw new InvalidOperationException("jev_api_key is not available in the environment.");
                        }
                        var cohort = EvaluationJson.ReadJsonLines<CohortEntry>(options.Required("cohort")).ToArray();
                        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
                        var output = options.Required("predictions");
                        await new JevEvaluationRunner(new(httpClient, apiKey), new()).RunAsync(
                            options.Required("corpus"), cohort, output, cancellationToken);
                        break;
                    }
                case "report":
                    {
                        var labels = EvaluationJson.ReadJsonLines<GoldLabel>(options.Required("labels")).ToDictionary(item => item.Id, StringComparer.Ordinal);
                        var predictionsPath = options.Required("predictions");
                        var predictions = EvaluationJson.ReadJsonLines<JevPrediction>(predictionsPath).ToArray();
                        var failuresPath = Path.Combine(
                            Path.GetDirectoryName(Path.GetFullPath(predictionsPath))!,
                            $"{Path.GetFileNameWithoutExtension(predictionsPath)}.failures.jsonl");
                        var failures = File.Exists(failuresPath)
                            ? EvaluationJson.ReadJsonLines<EvaluationFailure>(failuresPath).ToArray()
                            : [];
                        await EvaluationReportWriter.WriteAsync(labels, predictions, options.Required("output"), failures, cancellationToken);
                        break;
                    }
                default:
                    throw new ArgumentException($"Unknown command '{args[0]}'.");
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void Help() => Console.WriteLine("""
        Pegasus Jev mail evaluation (local evidence harness)

        prepare --corpus <dir> --cohort <jsonl>
        review --corpus <dir> --cohort <jsonl> --labels <jsonl>
        classify [--file <absolute.eml>] --acknowledge-external-upload
        run --corpus <dir> --cohort <jsonl> --predictions <jsonl> --acknowledge-external-upload
        report --labels <jsonl> --predictions <jsonl> --output <dir>
        """);
}

internal sealed class Options(Dictionary<string, string?> values)
{
    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{args[index]}'.");
            }
            var key = args[index][2..];
            values[key] = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++index]
                : null;
        }
        return new(values);
    }

    public string Required(string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"--{name} is required.");

    public bool Flag(string name) => values.ContainsKey(name);

    public string? Value(string name) => values.TryGetValue(name, out var value) ? value : null;
}
