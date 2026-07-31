using System.Text.Json;
using System.Collections.Immutable;
using Npgsql;
using CollisionBrain;
if (File.Exists(".env")) DotNetEnv.Env.NoClobber().Load();
var settings = new Settings();
var command = args.FirstOrDefault()?.ToLowerInvariant();
try
{
    settings.Validate();
    if (command is "healthcheck")
    {
        using var client = new HttpClient();
        var url = args.SkipWhile(x => x != "--url").Skip(1).FirstOrDefault() ?? $"http://localhost:{settings.Port}/health";
        var response = await client.GetAsync(url);
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    if (command is "stdio") return await StdioProxy.RunAsync(settings);
    if (command is "migrate") return await Admin.MigrateAsync(settings, args.Skip(1).ToArray());
    await using var context = new RuntimeContext(settings);
    await context.InitializeAsync();
    return command switch
    {
        "api" => await HttpHost.RunAsync(context),
        "worker" => await WorkerHost.RunAsync(context, args.Skip(1).ToArray()),
        "benchmark" => await Benchmark.RunAsync(context, args.Skip(1).ToArray()),
        "data-export" => await Admin.ExportAsync(context, args.Skip(1).ToArray()),
        "data-import" => await Admin.ImportAsync(context, args.Skip(1).ToArray()),
        _ => await Admin.UsageAsync(),
    };
}
catch (AppError ex) { await Console.Error.WriteLineAsync($"{ex.Code}: {ex.Message}"); return 1; }
catch (Exception ex) { await Console.Error.WriteLineAsync($"internal_error: {ex.Message}"); return 1; }

public static class Admin
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new UtcDateTimeOffsetJsonConverter() },
    };

    public static async Task<int> UsageAsync()
    {
        await Console.Error.WriteLineAsync("Usage: CollisionBrain [api|worker|stdio|migrate|data-export|data-import|benchmark|healthcheck]");
        return 2;
    }

    public static async Task<int> MigrateAsync(Settings settings, string[] args)
    {
        if (!settings.RepositoryDriver.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            await Console.Error.WriteLineAsync("No PostgreSQL migrations required for the memory repository.");
            return 0;
        }

        await using var dataSource = NpgsqlDataSource.Create(settings.DatabaseUrl);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = "CREATE TABLE IF NOT EXISTS schema_migrations (name text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())";
            await schema.ExecuteNonQueryAsync();
        }

        foreach (var file in MigrationFiles())
        {
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT 1 FROM schema_migrations WHERE name=$1";
            check.Parameters.AddWithValue(Path.GetFileName(file));
            if (await check.ExecuteScalarAsync() is not null) continue;
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var apply = connection.CreateCommand())
            {
                apply.Transaction = transaction;
                apply.CommandText = await File.ReadAllTextAsync(file);
                await apply.ExecuteNonQueryAsync();
            }
            await using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = "INSERT INTO schema_migrations(name) VALUES ($1)";
                record.Parameters.AddWithValue(Path.GetFileName(file));
                await record.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            await Console.Error.WriteLineAsync($"Applied migration {Path.GetFileName(file)}");
        }
        return 0;
    }

    public static async Task<int> ExportAsync(RuntimeContext context, string[] args)
    {
        var output = Value(args, "--output") ?? throw new ValidationError("--output is required");
        var documents = new List<ExportItem>();
        string? cursor = null;
        do
        {
            var page = await context.Repository.ListDocumentsAsync(cursor, 100, DocumentStatus.Ready);
            foreach (var summary in page.Documents)
            {
                var document = await context.Repository.GetDocumentAsync(summary.Id);
                if (document is null) continue;
                var source = await context.Objects.GetAsync(document.SourceObjectKey);
                documents.Add(new(summary, Convert.ToBase64String(source.Body)));
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        var directory = Path.GetDirectoryName(Path.GetFullPath(output));
        if (directory is not null) Directory.CreateDirectory(directory);
        await using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, new { schemaVersion = 1, exportedAt = DateTimeOffset.UtcNow, documents }, Json);
        await Console.Error.WriteLineAsync($"Exported {documents.Count} documents to {output}");
        return 0;
    }

    public static async Task<int> ImportAsync(RuntimeContext context, string[] args)
    {
        var input = Value(args, "--input") ?? throw new ValidationError("--input is required");
        await using var stream = File.OpenRead(input);
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var schema) || schema.GetInt32() != 1)
            throw new ValidationError("Unsupported export schema version");
        if (!root.TryGetProperty("documents", out var items) || items.ValueKind != JsonValueKind.Array)
            throw new ValidationError("Export documents are required");

        var principal = new Principal("data-import", ImmutableHashSet.Create(Role.Reader, Role.Contributor, Role.Admin));
        var imported = 0;
        foreach (var item in items.EnumerateArray())
        {
            var summary = item.GetProperty("document");
            var metadata = summary.GetProperty("metadata");
            var body = Convert.FromBase64String(item.GetProperty("sourceBase64").GetString() ?? "");
            var filename = metadata.TryGetProperty("filename", out var name) && name.ValueKind != JsonValueKind.Null ? name.GetString()! : "document.txt";
            var mimeType = metadata.GetProperty("mimeType").GetString() ?? "application/octet-stream";
            var staged = await context.Uploads.StageAsync(body, filename, mimeType);
            var tags = metadata.TryGetProperty("tags", out var tagValues)
                ? tagValues.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
                : Array.Empty<string>();
            var result = await context.Rag.WriteAsync(
                principal,
                metadata.GetProperty("title").GetString() ?? "",
                null,
                staged.UploadRef,
                metadata.TryGetProperty("source", out var source) && source.ValueKind != JsonValueKind.Null ? source.GetString() : null,
                tags);
            if (!(bool)result["deduplicated"]!) imported++;
        }
        while (await context.Rag.ProcessOneJobAsync()) { }
        await Console.Error.WriteLineAsync($"Imported {imported} documents from {input}");
        return 0;
    }

    private static IEnumerable<string> MigrationFiles()
    {
        var root = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "migrations"))
            ? Path.Combine(AppContext.BaseDirectory, "migrations")
            : Path.Combine(Directory.GetCurrentDirectory(), "migrations");
        return Directory.EnumerateFiles(root, "*.sql").OrderBy(Path.GetFileName, StringComparer.Ordinal);
    }

    private static string? Value(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private sealed record ExportItem(DocumentSummary Document, string SourceBase64);
}

public static class WorkerHost
{
    public static async Task<int> RunAsync(RuntimeContext context, string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services => services.AddSingleton(context).AddHostedService<Worker>())
            .Build();
        await host.RunAsync();
        return 0;
    }
}

public static class Benchmark
{
    public static async Task<int> RunAsync(RuntimeContext context, string[] args)
    {
        var inputIndex = Array.IndexOf(args, "--input");
        if (inputIndex < 0 || inputIndex + 1 >= args.Length) throw new ValidationError("--input is required");
        using var doc = await JsonDocument.ParseAsync(File.OpenRead(ResolvePath(args[inputIndex + 1])));
        var root = doc.RootElement;
        var documents = root.GetProperty("documents");
        var principal = new Principal("retrieval-benchmark", System.Collections.Immutable.ImmutableHashSet.Create(Role.Reader, Role.Contributor, Role.Admin));
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var d in documents.EnumerateArray())
        {
            var result = await context.Rag.WriteAsync(principal, d.GetProperty("title").GetString()!, d.GetProperty("text").GetString(), null, d.TryGetProperty("source", out var source) ? source.GetString() : null, d.TryGetProperty("tags", out var tags) ? tags.EnumerateArray().Select(x => x.GetString()!).ToArray() : null);
            keys[(string)result["document_id"]!] = d.GetProperty("key").GetString()!;
        }
        while (await context.Rag.ProcessOneJobAsync()) { }
        var reports = new List<Dictionary<string, object?>>();
        foreach (var q in root.GetProperty("queries").EnumerateArray())
        {
            var query = q.GetProperty("query").GetString()!;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var result = await context.Rag.LookupAsync(principal, query, q.TryGetProperty("limit", out var limit) ? limit.GetInt32() : 8, new());
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var retrieved = ((IEnumerable<Dictionary<string, object?>>)result["matches"]!).Select(x => keys[(string)x["documentId"]!]).Distinct(StringComparer.Ordinal).ToArray();
            var relevant = q.GetProperty("relevant_document_keys").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
            var first = Array.FindIndex(retrieved, relevant.Contains);
            var recall = retrieved.Count(relevant.Contains) / (double)relevant.Count;
            reports.Add(new Dictionary<string, object?> { ["query"] = query, ["recall"] = recall, ["reciprocal_rank"] = first < 0 ? 0 : 1.0 / (first + 1), ["lookup_ms"] = Math.Round(elapsed, 3), ["retrieved_document_keys"] = retrieved });
        }
        var averageRecall = reports.Average(x => (double)x["recall"]!);
        var averageMrr = reports.Average(x => (double)x["reciprocal_rank"]!);
        var output = new Dictionary<string, object?> { ["benchmark"] = root.GetProperty("name").GetString(), ["generated_at"] = DateTimeOffset.UtcNow, ["embedding"] = context.Embeddings.Descriptor, ["document_count"] = documents.GetArrayLength(), ["query_count"] = reports.Count, ["recall_at_k"] = Math.Round(averageRecall, 4), ["mean_reciprocal_rank"] = Math.Round(averageMrr, 4), ["mean_lookup_ms"] = Math.Round(reports.Average(x => (double)x["lookup_ms"]!), 3), ["results"] = reports };
        var outputIndex = Array.IndexOf(args, "--output");
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        jsonOptions.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        var json = JsonSerializer.Serialize(output, jsonOptions);
        if (outputIndex >= 0 && outputIndex + 1 < args.Length) await File.WriteAllTextAsync(args[outputIndex + 1], json); else Console.WriteLine(json);
        return 0;
    }
    private static string ResolvePath(string value)
    {
        if (File.Exists(value)) return value;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, value);
            if (File.Exists(candidate)) return candidate;
        }
        throw new ValidationError($"Input file was not found: {value}");
    }
}
