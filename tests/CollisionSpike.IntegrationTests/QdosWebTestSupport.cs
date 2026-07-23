using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CollisionSpike.IntegrationTests;

public sealed class QdosWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly DateTimeOffset FixedUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private readonly string environment;
    private readonly bool? localQdosIntakeEnabled;
    private readonly string databaseDirectory = Path.Combine(
        Path.GetTempPath(), "CollisionSpike.IntegrationTests", Guid.NewGuid().ToString("N"));

    public QdosWebApplicationFactory()
        : this("Development", true)
    {
    }

    internal QdosWebApplicationFactory(
        string environment,
        bool? localQdosIntakeEnabled)
    {
        this.environment = environment;
        this.localQdosIntakeEnabled = localQdosIntakeEnabled;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var databasePath = Path.Combine(databaseDirectory, "qdos-tests.db");
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:LocalPath"] = databasePath
            };
            if (localQdosIntakeEnabled is not null)
            {
                values["Features:LocalQdosIntake"] = localQdosIntakeEnabled.Value.ToString();
            }

            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new TestTimeProvider(FixedUtcNow));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(databaseDirectory))
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(databaseDirectory, recursive: true);
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

internal static partial class QdosWebDriver
{
    public static HttpClient CreateClient(QdosWebApplicationFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    public static async Task<UploadResult> UploadAsync(
        HttpClient client,
        GenuineCorpusSample sample,
        bool caseCreationAuthorized = true,
        CancellationToken cancellationToken = default)
    {
        using var formPage = await client.GetAsync("/Intake/Qdos", cancellationToken);
        formPage.EnsureSuccessStatusCode();
        var html = await formPage.Content.ReadAsStringAsync(cancellationToken);
        var tokenTag = AntiforgeryTagRegex().Match(html);
        Assert.True(tokenTag.Success, "The real upload page must render an antiforgery token.");
        var tokenValue = AntiforgeryValueRegex().Match(tokenTag.Value);
        Assert.True(tokenValue.Success, "The antiforgery token must have a value.");

        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new StringContent(WebUtility.HtmlDecode(tokenValue.Groups["value"].Value)),
            "__RequestVerificationToken");
        if (caseCreationAuthorized)
        {
            multipart.Add(new StringContent("true"), "CaseCreationAuthorized");
        }
        var file = new ByteArrayContent(sample.Bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(sample.MediaType);
        multipart.Add(file, "Upload", sample.UploadName);

        using var response = await client.PostAsync("/Intake/Qdos", multipart, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return new(response.StatusCode, response.Headers.Location, responseBody);
    }

    public static Guid ReceiptId(UploadResult result)
    {
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.NotNull(result.Location);
        var path = result.Location!.OriginalString.Split('?', 2)[0];
        Assert.True(Guid.TryParse(path.Split('/', StringSplitOptions.RemoveEmptyEntries).Last(), out var id));
        return id;
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryValueRegex();
}

internal sealed record UploadResult(HttpStatusCode StatusCode, Uri? Location, string ResponseBody);

internal sealed record GenuineCorpusSample(string Hash, string UploadName, string MediaType, byte[] Bytes);

internal static class GenuineQdosCorpus
{
    private static readonly Lazy<Dictionary<string, string>> PathsByHash = new(BuildPathsByHash);

    public static bool IsPresent => Directory.Exists(CorpusRoot);

    public static GenuineCorpusSample Read(string expectedHash)
    {
        Assert.True(PathsByHash.Value.TryGetValue(expectedHash, out var path),
            $"The frozen genuine-corpus item {expectedHash[..12]}... is absent.");
        var bytes = File.ReadAllBytes(path!);
        var actualHash = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Equal(expectedHash, actualHash);
        var extension = Path.GetExtension(path);
        return new(
            expectedHash,
            expectedHash[..12] + extension,
            extension.Equals(".eml", StringComparison.OrdinalIgnoreCase) ? "message/rfc822" : "application/pdf",
            bytes);
    }

    private static Dictionary<string, string> BuildPathsByHash()
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(CorpusRoot, "*.*", SearchOption.AllDirectories)
                     .Where(path => Path.GetExtension(path).Equals(".eml", StringComparison.OrdinalIgnoreCase)
                                    || Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = File.OpenRead(path);
            paths[Convert.ToHexString(SHA256.HashData(stream))] = path;
        }

        return paths;
    }

    private static string CorpusRoot => Path.Combine(FindRepositoryRoot(), "corpus", "qdos-email-corpus");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

internal sealed class GenuineQdosCorpusFactAttribute : FactAttribute
{
    public GenuineQdosCorpusFactAttribute()
    {
        if (!GenuineQdosCorpus.IsPresent)
        {
            Skip = "The ignored local corpus/qdos-email-corpus is absent; genuine-input evidence was not run.";
        }
    }
}
