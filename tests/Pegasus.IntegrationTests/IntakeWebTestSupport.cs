using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Intake;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

public sealed class IntakeWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly DateTimeOffset FixedUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private readonly string environment;
    private readonly bool? localIntakeEnabled;
    private readonly TimeProvider timeProvider;
    private readonly IIntakeArtifactStore? artifactStore;
    private readonly string workingDirectory = Path.Combine(
        Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));

    public IntakeWebApplicationFactory()
        : this("Development", true)
    {
    }

    internal IntakeWebApplicationFactory(TimeProvider timeProvider)
        : this("Development", true, timeProvider)
    {
    }

    internal IntakeWebApplicationFactory(
        string environment,
        bool? localIntakeEnabled,
        TimeProvider? timeProvider = null,
        IIntakeArtifactStore? artifactStore = null)
    {
        this.environment = environment;
        this.localIntakeEnabled = localIntakeEnabled;
        this.timeProvider = timeProvider ?? new TestTimeProvider(FixedUtcNow);
        this.artifactStore = artifactStore;
    }

    internal string DatabasePath => Path.Combine(workingDirectory, "intake-tests.db");

    internal string ArtifactDirectory => Path.Combine(workingDirectory, "intake-artifacts");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = environment.Equals(
                    "Development",
                    StringComparison.OrdinalIgnoreCase)
                    ? "DevelopmentOffline"
                    : "Production",
                ["Database:Provider"] = "Sqlite",
                ["Database:LocalPath"] = DatabasePath,
                ["Intake:LocalArtifactPath"] = ArtifactDirectory,
                ["Features:LocalIntake"] = (localIntakeEnabled ?? false).ToString()
            };

            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(timeProvider);
            if (artifactStore is not null)
            {
                services.RemoveAll<IIntakeArtifactStore>();
                services.AddSingleton(artifactStore);
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        DevelopmentOfflineInitialization.InitializeAsync(scope.ServiceProvider)
            .GetAwaiter()
            .GetResult();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(workingDirectory))
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

internal static partial class IntakeWebDriver
{
    public static HttpClient CreateClient(IntakeWebApplicationFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

    public static async Task<UploadResult> UploadAsync(
        HttpClient client,
        GenuineCorpusSample sample,
        string? externalReceiptToken = null,
        CancellationToken cancellationToken = default) =>
        await UploadAsync(
            client,
            sample.UploadName,
            sample.MediaType,
            sample.Bytes,
            externalReceiptToken,
            cancellationToken);

    public static async Task<UploadResult> UploadAsync(
        HttpClient client,
        string uploadName,
        string mediaType,
        byte[] bytes,
        string? externalReceiptToken = null,
        CancellationToken cancellationToken = default)
    {
        var form = await GetUploadFormTokensAsync(client, cancellationToken);
        return await PostUploadAsync(
            client,
            form.AntiforgeryToken,
            uploadName,
            mediaType,
            bytes,
            externalReceiptToken ?? form.ExternalReceiptToken,
            cancellationToken);
    }

    public static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        CancellationToken cancellationToken = default) =>
        (await GetUploadFormTokensAsync(client, cancellationToken)).AntiforgeryToken;

    public static async Task<UploadFormTokens> GetUploadFormTokensAsync(
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        using var formPage = await client.GetAsync("/Intake/Upload", cancellationToken);
        formPage.EnsureSuccessStatusCode();
        var html = await formPage.Content.ReadAsStringAsync(cancellationToken);
        var tokenTag = AntiforgeryTagRegex().Match(html);
        Assert.True(tokenTag.Success, "The real upload page must render an antiforgery token.");
        var tokenValue = AntiforgeryValueRegex().Match(tokenTag.Value);
        Assert.True(tokenValue.Success, "The antiforgery token must have a value.");
        var receiptTokenTag = ExternalReceiptTokenTagRegex().Match(html);
        Assert.True(receiptTokenTag.Success, "The real upload page must render an external receipt token.");
        var receiptTokenValue = AntiforgeryValueRegex().Match(receiptTokenTag.Value);
        Assert.True(receiptTokenValue.Success, "The external receipt token must have a value.");
        return new(
            WebUtility.HtmlDecode(tokenValue.Groups["value"].Value),
            WebUtility.HtmlDecode(receiptTokenValue.Groups["value"].Value));
    }

    public static async Task<UploadResult> PostUploadAsync(
        HttpClient client,
        string? antiforgeryToken,
        string? uploadName,
        string mediaType,
        byte[]? bytes,
        string? externalReceiptToken = null,
        CancellationToken cancellationToken = default)
    {

        using var multipart = new MultipartFormDataContent();
        if (antiforgeryToken is not null)
        {
            multipart.Add(new StringContent(antiforgeryToken), "__RequestVerificationToken");
        }

        if (externalReceiptToken is not null)
        {
            multipart.Add(new StringContent(externalReceiptToken), "ExternalReceiptToken");
        }

        if (uploadName is not null && bytes is not null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
            multipart.Add(file, "Upload", uploadName);
        }

        using var response = await client.PostAsync("/Intake/Upload", multipart, cancellationToken);
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

    [GeneratedRegex("<input[^>]*name=\"ExternalReceiptToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalReceiptTokenTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryValueRegex();
}

internal sealed record UploadResult(HttpStatusCode StatusCode, Uri? Location, string ResponseBody);

internal sealed record UploadFormTokens(string AntiforgeryToken, string ExternalReceiptToken);

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

    private static string CorpusRoot => Path.Combine(
        FindRepositoryRoot(),
        "corpus",
        "emailevals",
        "qdos-email-corpus");

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
            Skip = "The ignored local corpus/emailevals/qdos-email-corpus is absent; genuine-input evidence was not run.";
        }
    }
}
