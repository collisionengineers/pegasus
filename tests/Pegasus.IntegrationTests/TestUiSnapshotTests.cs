using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class TestUiSnapshotTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, StateMatch> StateMatches =
        new Dictionary<string, StateMatch>(StringComparer.Ordinal)
        {
            ["sign-in--validation"] = new("validation-summary-errors"),
            ["sign-in--signed-out"] = new("You are signed out"),
            ["administration-accounts--empty"] = new("No staff accounts are available."),
            ["administration-configuration--default"] = new("Workflow configuration"),
            ["case-details--unavailable"] = new("<h1>Case unavailable</h1>"),
            ["case-details--conflict"] = new("case changed", "Case unavailable"),
            ["cases--empty"] = new("No matching cases."),
            ["cases--unavailable"] = new("<h2>Cases are unavailable</h2>"),
            ["vehicle-images--empty"] = new("No Image-initiated Cases match this view."),
            ["inbox--empty"] = new("empty-state"),
            ["inbox--unavailable"] = new(">Unavailable<"),
            ["operations--empty"] = new("empty-state"),
            ["queues--empty"] = new("No cases are waiting."),
            ["upload--validation"] = new("validation-summary-errors"),
            ["upload-group-status--processing"] = new("data-auto-refresh=\"2000\""),
            ["upload-group-status--needs-decision"] = new("needs a staff decision"),
            ["upload-request--validation"] = new("Choose a document to upload."),
            ["upload-status--processing"] = new("data-auto-refresh=\"2000\""),
            ["upload-status--needs-decision"] = new("needs a staff decision")
        };

    [Fact]
    public async Task CapturedRazorResponsesMatchCommittedTestUiSnapshots()
    {
        var mode = Environment.GetEnvironmentVariable("PEGASUS_TEST_UI_MODE");
        if (string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        var captureDirectory = Environment.GetEnvironmentVariable("PEGASUS_TEST_UI_CAPTURE_DIR");
        Assert.False(string.IsNullOrWhiteSpace(captureDirectory));
        var repoRoot = FindRepoRoot();
        var catalogueRoot = Path.Combine(repoRoot, "docs", "design", "test-ui");
        var manifest = await ReadManifestAsync(Path.Combine(catalogueRoot, "catalogue.json"));
        var candidates = await ReadCandidatesAsync(captureDirectory!);
        var generated = Generate(manifest, candidates, catalogueRoot);

        if (mode.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            WriteGenerated(catalogueRoot, generated);
            return;
        }

        Assert.True(mode.Equals("verify", StringComparison.OrdinalIgnoreCase), $"Unknown snapshot mode '{mode}'.");
        foreach (var file in generated)
        {
            var path = Path.Combine(catalogueRoot, file.Key.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Missing generated Test UI file: {file.Key}");
            Assert.True(
                file.Value == await File.ReadAllTextAsync(path),
                $"Generated Test UI file is stale: {file.Key}");
        }
    }

    private static Dictionary<string, string> Generate(
        IReadOnlyList<CatalogueEntry> manifest,
        IReadOnlyList<CapturedResponse> candidates,
        string catalogueRoot)
    {
        var output = new Dictionary<string, string>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var entry in manifest.Where(item => item.Classification == "visual"))
        {
            var routePattern = RoutePattern(entry.Route);
            var routeCandidates = candidates
                .Where(candidate => routePattern.IsMatch(candidate.Path))
                .ToArray();
            foreach (var state in entry.States)
            {
                var otherMatches = entry.States
                    .Where(other => other.Scenario != state.Scenario)
                    .Select(other => StateMatches.GetValueOrDefault(other.Scenario))
                    .Where(match => match is not null)
                    .Cast<StateMatch>()
                    .ToArray();
                var stateMatch = StateMatches.GetValueOrDefault(state.Scenario);
                var selected = routeCandidates
                    .Where(candidate => stateMatch?.Matches(candidate.Html) ?? otherMatches.All(match => !match.Matches(candidate.Html)))
                    .Select(candidate => NormalizeAndRewrite(candidate.Html, state.File, manifest))
                    .Order(StringComparer.Ordinal)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(selected))
                {
                    missing.Add($"{state.Scenario} ({entry.Route})");
                    continue;
                }
                output[state.File] = selected;
            }
        }

        Assert.True(missing.Count == 0, "No captured Razor response matched:\n- " + string.Join("\n- ", missing));

        output["index.html"] = BuildIndex(manifest);
        return output;
    }

    private static string NormalizeAndRewrite(
        string html,
        string outputFile,
        IReadOnlyList<CatalogueEntry> manifest)
    {
        html = AntiforgeryValueRegex().Replace(html, "$1{{antiforgery-token}}$2");
        html = VolatileGuidValueRegex().Replace(html, match =>
            match.Groups[1].Value + "{{" + match.Groups[2].Value.ToLowerInvariant() + "}}" + match.Groups[3].Value);
        html = CacheBusterRegex().Replace(html, "$1{{asset-version}}");

        var outputDirectoryDepth = outputFile.Count(character => character == '/') + 1;
        var sourcePrefix = string.Concat(Enumerable.Repeat("../", outputDirectoryDepth + 2));
        html = AssetUrlRegex().Replace(html, match =>
        {
            var assetName = FingerprintedAssetRegex().Replace(match.Groups[3].Value, ".$1");
            return $"{match.Groups[1].Value}{sourcePrefix}src/Pegasus.Web/wwwroot/{match.Groups[2].Value}/{assetName}";
        });
        html = ApplicationUrlRegex().Replace(html, match =>
        {
            var attribute = match.Groups[1].Value;
            var url = WebUtility.HtmlDecode(match.Groups[2].Value);
            var path = url.Split('?', '#')[0];
            var target = manifest
                .Where(entry => entry.Classification == "visual")
                .FirstOrDefault(entry => RoutePattern(entry.Route).IsMatch(path))?
                .States.FirstOrDefault(state => state.State == "default")?.File;
            if (target is null)
            {
                return match.Value;
            }
            var currentDirectory = Path.GetDirectoryName(outputFile)?.Replace('\\', '/') ?? string.Empty;
            var relative = Path.GetRelativePath(currentDirectory, target).Replace('\\', '/');
            return $"{attribute}=\"{relative}\"";
        });

        return TrailingWhitespaceRegex().Replace(NormalizeNewLines(html), string.Empty).TrimEnd() + "\n";
    }

    private static string BuildIndex(IReadOnlyList<CatalogueEntry> manifest)
    {
        var visual = new StringBuilder();
        var nonvisual = new StringBuilder();
        foreach (var entry in manifest)
        {
            if (entry.Classification == "visual")
            {
                visual.Append("<li><strong>").Append(WebUtility.HtmlEncode(entry.Route)).Append("</strong><ul>");
                foreach (var state in entry.States)
                {
                    visual.Append("<li><a href=\"").Append(WebUtility.HtmlEncode(state.File)).Append("\">")
                        .Append(WebUtility.HtmlEncode(state.State)).Append("</a> — ")
                        .Append(WebUtility.HtmlEncode(state.Branch)).Append("</li>");
                }
                visual.Append("</ul></li>");
            }
            else
            {
                nonvisual.Append("<tr><td>").Append(WebUtility.HtmlEncode(entry.Route)).Append("</td><td>")
                    .Append(WebUtility.HtmlEncode(entry.Classification)).Append("</td><td>")
                    .Append(WebUtility.HtmlEncode(entry.Reason)).Append("</td></tr>");
            }
        }

        return "<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Pegasus Test UI</title><link rel=\"stylesheet\" href=\"../../../src/Pegasus.Web/wwwroot/css/site.css\"></head><body><main class=\"app-shell\"><header class=\"page-header\"><p class=\"eyebrow\">Razor-rendered snapshots</p><h1>Pegasus Test UI</h1></header><section aria-labelledby=\"visual-pages\"><h2 id=\"visual-pages\" class=\"section-label\">Visual routes</h2><ul class=\"link-list\">"
            + visual + "</ul></section><section aria-labelledby=\"nonvisual\"><h2 id=\"nonvisual\" class=\"section-label\">Non-visual routes</h2><div class=\"table-wrap\"><table><thead><tr><th>Route</th><th>Classification</th><th>Reason</th></tr></thead><tbody>"
            + nonvisual + "</tbody></table></div></section></main></body></html>\n";
    }

    private static void WriteGenerated(string catalogueRoot, IReadOnlyDictionary<string, string> generated)
    {
        var pagesRoot = Path.Combine(catalogueRoot, "pages");
        var expectedPages = generated.Keys
            .Where(key => key.StartsWith("pages/", StringComparison.Ordinal))
            .Select(key => Path.GetFullPath(Path.Combine(catalogueRoot, key.Replace('/', Path.DirectorySeparatorChar))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existingPage in Directory.EnumerateFiles(pagesRoot, "*.html", SearchOption.TopDirectoryOnly))
        {
            if (!expectedPages.Contains(Path.GetFullPath(existingPage)))
            {
                File.Delete(existingPage);
            }
        }

        foreach (var file in generated)
        {
            var path = Path.Combine(catalogueRoot, file.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Value, new UTF8Encoding(false));
        }
    }

    private static async Task<IReadOnlyList<CatalogueEntry>> ReadManifestAsync(string path) =>
        JsonSerializer.Deserialize<List<CatalogueEntry>>(
            await File.ReadAllTextAsync(path),
            JsonOptions)
        ?? throw new InvalidOperationException("The Test UI catalogue manifest is empty.");

    private static async Task<IReadOnlyList<CapturedResponse>> ReadCandidatesAsync(string root)
    {
        var responses = new List<CapturedResponse>();
        foreach (var metadataPath in Directory.EnumerateFiles(root, "response.json", SearchOption.AllDirectories))
        {
            var metadata = JsonSerializer.Deserialize<CapturedMetadata>(await File.ReadAllTextAsync(metadataPath), JsonOptions)!;
            var html = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(metadataPath)!, "response.html"));
            responses.Add(new CapturedResponse(metadata.Path, metadata.Query, html));
        }
        return responses;
    }

    private static Regex RoutePattern(string route)
    {
        var pattern = string.Join(
            "/",
            route.Split('/').Select(segment =>
                segment.StartsWith('{') && segment.EndsWith('}')
                    ? "[^/]+"
                    : Regex.Escape(segment)));
        return new Regex("^" + pattern + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Could not find the Pegasus repository root.");
    }

    private static string NormalizeNewLines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private sealed record StateMatch(string Required, string? Excluded = null)
    {
        public bool Matches(string html) =>
            html.Contains(Required, StringComparison.OrdinalIgnoreCase)
            && (Excluded is null || !html.Contains(Excluded, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record CapturedMetadata(string Method, string Path, string Query);
    private sealed record CapturedResponse(string Path, string Query, string Html);
    private sealed record CatalogueEntry(string Source, string Route, string Classification, string? Reason, StateEntry[] States);
    private sealed record StateEntry(string State, string File, string Branch, string Scenario);

    [GeneratedRegex("(<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\")[^\"]*(\")", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryValueRegex();

    [GeneratedRegex("(<(?:input|meta)[^>]+(?:name|data-token-kind)=\"(operationkey|editleasetoken|requestid)\"[^>]+value=\")[^\"]*(\")", RegexOptions.IgnoreCase)]
    private static partial Regex VolatileGuidValueRegex();

    [GeneratedRegex("([?&]v=)[A-Za-z0-9_-]+", RegexOptions.IgnoreCase)]
    private static partial Regex CacheBusterRegex();

    [GeneratedRegex("((?:href|src)=\")/(css|js|images)/([^\"]+)", RegexOptions.IgnoreCase)]
    private static partial Regex AssetUrlRegex();

    [GeneratedRegex("\\.[a-z0-9]{8,}\\.([a-z0-9]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex FingerprintedAssetRegex();

    [GeneratedRegex("((?:href|action))=\"(/[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ApplicationUrlRegex();

    [GeneratedRegex("[ \\t]+(?=\\n)")]
    private static partial Regex TrailingWhitespaceRegex();
}
