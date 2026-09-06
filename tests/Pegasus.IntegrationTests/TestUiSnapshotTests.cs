using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

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
            ["administration-account-confirm--disable"] = new("Disable account"),
            ["administration-account-confirm--enable"] = new("Enable account"),
            ["administration-account-confirm--delete"] = new("Delete account"),
            ["administration-account-confirm--force-logout"] = new("Force logout"),
            ["administration-account-confirm--reset-password"] = new("Reset password"),
            ["administration-account-confirm--clear-lease"] = new("Clear case edit hold"),
            ["administration-principal-eva-submission--default"] = new(
                "EVA API submission for WEBP", "We could not complete that request"),
            // The seeded list before any administrator change: the create
            // form is present and no test-created preset has been added.
            ["administration-valuation-presets--default"] = new("Create preset", "Roof rack"),
            ["case-details--default"] = new(
                "You are editing this case.",
                AlsoRequired: "case-overview-panel",
                AlsoRequired2: "status status--navy\">Review<"),
            ["case-details--unavailable"] = new("<h1>Case unavailable</h1>"),
            ["case-details--conflict"] = new("case changed", "Case unavailable"),
            ["cases--empty"] = new("No cases match these filters."),
            ["cases--unavailable"] = new("<strong>Cases are unavailable</strong>"),
            ["vehicle-images--empty"] = new("No Image-initiated Cases match this view."),
            // Empty with a healthy freshness chip, so this state and
            // inbox--unavailable are not the same rendered page.
            ["inbox--empty"] = new(
                "<p>No mail has been received.</p>", "status--red\">Unavailable<"),
            ["inbox--unavailable"] = new(">Unavailable<"),
            ["inbox--default"] = new("<h1>Inbox</h1>"),
            ["operations--partial-data"] = new(">Partial data</strong>"),
            ["operations--empty"] = new(">No retryable external work<"),
            ["queues--empty"] = new("class=\"muted\">0 items</span>"),
            ["upload--validation"] = new("validation-summary-errors"),
            ["upload-group-status--processing"] = new("data-auto-refresh=\"2000\""),
            ["upload-group-status--needs-decision"] = new("needs a staff decision"),
            ["upload-group-status--default"] = new("Open case"),
            ["upload-request--validation"] = new("Choose a document to upload."),
            ["upload-status--processing"] = new("data-auto-refresh=\"2000\""),
            ["upload-status--needs-decision"] = new("needs a staff decision"),
            ["upload-status--default"] = new("<h1>Complete</h1>")
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
        var scope = ParseScope(Environment.GetEnvironmentVariable("PEGASUS_TEST_UI_SCOPE"));
        ValidateScope(manifest, scope);
        var candidates = await ReadCandidatesAsync(captureDirectory!);
        var assets = await ReadAssetsAsync(captureDirectory!);
        var generated = Generate(manifest, candidates, assets, scope);

        if (mode.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            WriteGenerated(catalogueRoot, generated, scope);
            return;
        }

        Assert.True(mode.Equals("verify", StringComparison.OrdinalIgnoreCase), $"Unknown snapshot mode '{mode}'.");
        foreach (var file in generated)
        {
            var path = Path.Combine(catalogueRoot, file.Key.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Missing generated Test UI file: {file.Key}");
            // Newline-normalised on both sides: a core.autocrlf checkout
            // hands back CRLF for files the generator wrote with LF.
            Assert.True(
                file.Value == NormalizeNewLines(await File.ReadAllTextAsync(path)),
                $"Generated Test UI file is stale: {file.Key}");
        }
        var orphans = CommittedPages(catalogueRoot)
            .Where(page => scope is null || MatchesScope(page, scope))
            .Where(page => !generated.ContainsKey(page))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(orphans.Length == 0, "Committed Test UI pages no state generates:\n- " + string.Join("\n- ", orphans));
        await VerifyOfflineBrowserRenderAsync(catalogueRoot, generated);
    }

    private static async Task VerifyOfflineBrowserRenderAsync(string catalogueRoot, IReadOnlyDictionary<string, string> generated)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        foreach (var file in generated.Where(file => file.Key.StartsWith("pages/", StringComparison.Ordinal)))
        {
            var committedPath = Path.Combine(catalogueRoot, file.Key.Replace('/', Path.DirectorySeparatorChar));
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
            await page.GotoAsync(new Uri(committedPath).AbsoluteUri, new() { WaitUntil = WaitUntilState.NetworkIdle });
            foreach (var image in await page.Locator("img:not([hidden])").AllAsync())
            {
                Assert.True(await image.EvaluateAsync<int>("element => element.naturalWidth") > 0, $"Offline image failed to load: {file.Key}");
            }
            Assert.NotEmpty(await page.ScreenshotAsync(new() { FullPage = true }));
            await page.CloseAsync();
        }
    }

    private static Dictionary<string, string> Generate(
        IReadOnlyList<CatalogueEntry> manifest,
        IReadOnlyList<CapturedResponse> candidates,
        IReadOnlyDictionary<string, string> assets,
        IReadOnlyList<string>? scope)
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
                if (scope is not null && !MatchesScope(state.File, scope))
                {
                    continue;
                }

                var otherMatches = entry.States
                    .Where(other => other.Scenario != state.Scenario)
                    .Select(other => StateMatches.GetValueOrDefault(other.Scenario))
                    .Where(match => match is not null)
                    .Cast<StateMatch>()
                    .ToArray();
                var stateMatch = StateMatches.GetValueOrDefault(state.Scenario);
                var matched = routeCandidates
                    .Where(candidate => stateMatch?.Matches(candidate.Html) ?? otherMatches.All(match => !match.Matches(candidate.Html)))
                    .ToArray();
                // A page is only offline-complete when every receipt image it
                // shows was captured for that receipt; no candidate may borrow
                // another receipt's bytes.
                var selected = matched
                    .Where(candidate => MissingReceiptImages(candidate.Html, assets).Length == 0)
                    .Select(candidate => NormalizeAndRewrite(candidate.Html, state.File, manifest, assets))
                    .Order(StringComparer.Ordinal)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(selected))
                {
                    var uncaptured = matched
                        .SelectMany(candidate => MissingReceiptImages(candidate.Html, assets))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.Ordinal)
                        .ToArray();
                    missing.Add(uncaptured.Length == 0
                        ? $"{state.Scenario} ({entry.Route})"
                        : $"{state.Scenario} ({entry.Route}): matched, but no candidate had its receipt images captured: {string.Join(", ", uncaptured)}");
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
        IReadOnlyList<CatalogueEntry> manifest,
        IReadOnlyDictionary<string, string> assets)
    {
        html = CapturedAssetUrlRegex().Replace(html, match =>
            assets.TryGetValue(AssetPath(match.Groups[2].Value), out var dataUrl)
                ? $"{match.Groups[1].Value}=\"{dataUrl}\""
                : match.Value);
        // Live-only behaviour has no offline caller: a reload timer, the
        // Inbox preview fetch and the case-search JSON handler are dropped
        // rather than pointed at a static page.
        html = LiveAttributeRegex().Replace(html, string.Empty);
        html = AntiforgeryValueRegex().Replace(html, "$1{{antiforgery-token}}$2");
        html = VolatileGuidValueRegex().Replace(html, match =>
            match.Groups[1].Value + "{{" + match.Groups[2].Value.ToLowerInvariant() + "}}" + match.Groups[3].Value);
        html = SupportReferenceRegex().Replace(html, "$1{{request-id}}$2");
        html = LayoutClockRegex().Replace(html, "$1{{office-clock}}$2");
        html = CacheBusterRegex().Replace(html, "$1{{asset-version}}");
        var guidNumber = 0;
        var guids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string StableGuid(Match match)
        {
            if (!guids.TryGetValue(match.Value, out var replacement))
            {
                replacement = $"test-ui-guid-{++guidNumber}";
                guids[match.Value] = replacement;
            }
            return replacement;
        }
        html = GuidRegex().Replace(html, StableGuid);
        html = CompactGuidRegex().Replace(html, StableGuid);

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
                return $"{attribute}=\"#\"";
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

    private static string AssetPath(string attributeValue) =>
        WebUtility.HtmlDecode(attributeValue).Split('?', '#')[0];

    private static string[] MissingReceiptImages(string html, IReadOnlyDictionary<string, string> assets) =>
        IntakeWebDriver.ReceiptImageUrlRegex().Matches(html)
            .Select(match => AssetPath(match.Groups[1].Value))
            .Where(path => !assets.ContainsKey(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IEnumerable<string> CommittedPages(string catalogueRoot) =>
        Directory.EnumerateFiles(Path.Combine(catalogueRoot, "pages"), "*.html", SearchOption.TopDirectoryOnly)
            .Select(page => "pages/" + Path.GetFileName(page));

    private static void WriteGenerated(
        string catalogueRoot,
        Dictionary<string, string> generated,
        IReadOnlyList<string>? scope)
    {
        foreach (var orphan in CommittedPages(catalogueRoot)
                     .Where(page => scope is null || MatchesScope(page, scope))
                     .Where(page => !generated.ContainsKey(page)))
        {
            File.Delete(Path.Combine(catalogueRoot, orphan.Replace('/', Path.DirectorySeparatorChar)));
        }

        foreach (var file in generated)
        {
            var path = Path.Combine(catalogueRoot, file.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Value, new UTF8Encoding(false));
        }
    }

    private static string[]? ParseScope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var scope = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.True(scope.Length > 0, $"Test UI scope contains no usable prefixes: '{value}'");
        return scope;
    }

    private static bool MatchesScopePrefix(string file, string prefix) =>
        file.StartsWith($"pages/{prefix}--", StringComparison.Ordinal);

    private static bool MatchesScope(string file, IReadOnlyList<string> scope) =>
        scope.Any(prefix => MatchesScopePrefix(file, prefix));

    private static void ValidateScope(IReadOnlyList<CatalogueEntry> manifest, IReadOnlyList<string>? scope)
    {
        if (scope is null)
        {
            return;
        }

        var stateFiles = manifest
            .Where(entry => entry.Classification == "visual")
            .SelectMany(entry => entry.States)
            .Select(state => state.File)
            .ToArray();
        var unmatched = scope.Where(prefix => !stateFiles.Any(file => MatchesScopePrefix(file, prefix))).ToArray();
        Assert.True(unmatched.Length == 0, "Test UI scope prefixes matched no catalogue state:\n- " + string.Join("\n- ", unmatched));
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

    private static async Task<IReadOnlyDictionary<string, string>> ReadAssetsAsync(string root)
    {
        var assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var metadataPath in Directory.EnumerateFiles(root, "asset.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var metadata = JsonSerializer.Deserialize<CapturedAssetMetadata>(await File.ReadAllTextAsync(metadataPath), JsonOptions)!;
            // Only receipt images are inlined; wwwroot assets the browser lane
            // fetched stay source-relative references like every other page.
            if (!metadata.Path.StartsWith("/Received/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var bytes = await File.ReadAllBytesAsync(Path.Combine(Path.GetDirectoryName(metadataPath)!, "response.bin"));
            assets[metadata.Path] = $"data:{metadata.ContentType};base64,{Convert.ToBase64String(bytes)}";
        }
        return assets;
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

    private sealed record StateMatch(
        string Required,
        string? Excluded = null,
        string? AlsoRequired = null,
        string? AlsoRequired2 = null)
    {
        public bool Matches(string html) =>
            html.Contains(Required, StringComparison.OrdinalIgnoreCase)
            && (AlsoRequired is null || html.Contains(AlsoRequired, StringComparison.OrdinalIgnoreCase))
            && (AlsoRequired2 is null || html.Contains(AlsoRequired2, StringComparison.OrdinalIgnoreCase))
            && (Excluded is null || !html.Contains(Excluded, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record CapturedMetadata(string Method, string Path, string Query);
    private sealed record CapturedAssetMetadata(string Path, string Query, string ContentType);
    private sealed record CapturedResponse(string Path, string Query, string Html);
    private sealed record CatalogueEntry(string Source, string Route, string Classification, string? Reason, StateEntry[] States);
    private sealed record StateEntry(string State, string File, string Branch, string Scenario);

    [GeneratedRegex("(<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\")[^\"]*(\")", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryValueRegex();

    [GeneratedRegex("(<(?:input|meta)[^>]+(?:name|data-token-kind)=\"(operationkey|operationid|editleasetoken|requestid|externalreceipttoken|token|code_challenge|nonce|state)\"[^>]+value=\")[^\"]*(\")", RegexOptions.IgnoreCase)]
    private static partial Regex VolatileGuidValueRegex();

    [GeneratedRegex("(<code id=\"support-reference\">)[^<]*(</code>)", RegexOptions.IgnoreCase)]
    private static partial Regex SupportReferenceRegex();

    [GeneratedRegex("\\s+data-(?:auto-refresh|mail-preview-url|case-search-url)=\"[^\"]*\"", RegexOptions.IgnoreCase)]
    private static partial Regex LiveAttributeRegex();

    // _Layout renders the rail and utility-bar clocks from the render time
    // itself, so a fresh capture carries the minute it ran. The mail
    // freshness banner is a different value — the last sync, inside a
    // <time> element — and is left alone.
    [GeneratedRegex("(<span>Current · )\\d{1,2}:\\d{2}(</span>)", RegexOptions.IgnoreCase)]
    private static partial Regex LayoutClockRegex();

    [GeneratedRegex("([?&]v=)[A-Za-z0-9_-]+", RegexOptions.IgnoreCase)]
    private static partial Regex CacheBusterRegex();

    [GeneratedRegex("((?:href|src)=\")/(css|js|images)/([^\"]+)", RegexOptions.IgnoreCase)]
    private static partial Regex AssetUrlRegex();

    [GeneratedRegex("\\.[a-z0-9]{8,}\\.([a-z0-9]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex FingerprintedAssetRegex();

    [GeneratedRegex("((?:href|action|src|data-download-href|value))=\"(/[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ApplicationUrlRegex();

    [GeneratedRegex("((?:href|src|data-download-href))=\"(/[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex CapturedAssetUrlRegex();

    [GeneratedRegex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex GuidRegex();

    // "N"-format identifiers (operation keys, receipt ids in element ids)
    // are as run-specific as the hyphenated form and share its numbering.
    [GeneratedRegex("(?<![0-9a-z])[0-9a-f]{32}(?![0-9a-z])", RegexOptions.IgnoreCase)]
    private static partial Regex CompactGuidRegex();

    [GeneratedRegex("[ \\t]+(?=\\n)")]
    private static partial Regex TrailingWhitespaceRegex();
}
