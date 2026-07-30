using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Bootstrap;

internal static class Program
{
    private const int MaximumManifestBytes = 64 * 1024;
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = BootstrapArguments.Parse(args);
            if (options.ShowHelp)
            {
                WriteUsage();
                return 0;
            }

            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            var manifestBytes = await ReadManifestAsync(
                options.ManifestPath,
                cancellation.Token);
            var observedManifestSha256 = Convert.ToHexStringLower(
                SHA256.HashData(manifestBytes));
            var manifest = DeserializeManifest(manifestBytes);
            var input = ValidateManifest(manifest, options);

            var passwords = new string[InitializeApplication.InitialAdministratorCount];
            try
            {
                for (var index = 0; index < passwords.Length; index++)
                {
                    passwords[index] = ReadConfirmedSecret(index + 1);
                }

                var administrators = input.Administrators
                    .Select((administrator, index) => new InitialAdministratorCredentials(
                        administrator.ManifestIdentity,
                        administrator.UserName,
                        passwords[index]))
                    .ToArray();
                var request = new InitializeApplicationRequest(
                    input.ExpectedMigrationId,
                    observedManifestSha256,
                    options.ApprovedManifestSha256,
                    input.TargetIdentity,
                    administrators,
                    input.PublicMcpClient,
                    Guid.NewGuid().ToString("N"));

                await using var provider = CreateProvider(options);
                await using var scope = provider.CreateAsyncScope();
                var initializeApplication = scope.ServiceProvider
                    .GetRequiredService<IInitializeApplication>();
                _ = await initializeApplication.ExecuteAsync(
                    request,
                    cancellation.Token);
            }
            finally
            {
                Array.Fill(passwords, string.Empty);
            }

            Console.Out.WriteLine("Application initialization completed.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Application initialization was cancelled.");
            return 5;
        }
        catch (ApplicationInitializationException exception)
        {
            Console.Error.WriteLine($"Application initialization was denied: {exception.Error}.");
            return 4;
        }
        catch (BootstrapUsageException)
        {
            WriteUsage();
            return 2;
        }
        catch (BootstrapInputException)
        {
            Console.Error.WriteLine("The bootstrap manifest or concealed input is invalid.");
            return 3;
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine("The bootstrap manifest or concealed input is invalid.");
            return 3;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Application initialization failed without reporting sensitive detail.");
            return 1;
        }
    }

    private static async Task<byte[]> ReadManifestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > MaximumManifestBytes)
        {
            throw new BootstrapInputException();
        }

        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    private static BootstrapManifest DeserializeManifest(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<BootstrapManifest>(
                    bytes,
                    ManifestSerializerOptions)
                ?? throw new BootstrapInputException();
        }
        catch (JsonException)
        {
            throw new BootstrapInputException();
        }
    }

    private static ValidatedBootstrapManifest ValidateManifest(
        BootstrapManifest manifest,
        BootstrapArguments options)
    {
        if (manifest.SchemaVersion != 1
            || !IsExactSourceRevision(manifest.SourceRevision)
            || !MatchesAssemblyIdentity(manifest.ProductVersion, manifest.SourceRevision)
            || !IsSqlServerHost(manifest.SqlServer)
            || !string.Equals(
                manifest.SqlServer,
                options.SqlServer,
                StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(manifest.SqlDatabase)
            || !string.Equals(
                manifest.SqlDatabase,
                options.SqlDatabase,
                StringComparison.Ordinal)
            || !TryNormalizeIssuer(manifest.Issuer, out var manifestIssuer)
            || !manifestIssuer.Equals(options.Issuer)
            || manifest.Administrators is null
            || manifest.PublicMcpClient is null)
        {
            throw new BootstrapInputException();
        }

        var targetIdentity = CreateTargetIdentity(
            manifest.SqlServer,
            manifest.SqlDatabase);
        if (!string.Equals(
                manifest.TargetIdentity,
                targetIdentity,
                StringComparison.Ordinal))
        {
            throw new BootstrapInputException();
        }

        if (manifest.Administrators.Count != InitializeApplication.InitialAdministratorCount
            || manifest.Administrators.Any(administrator => administrator is null))
        {
            throw new BootstrapInputException();
        }

        var administrators = manifest.Administrators
            .Select(administrator => new ApprovedAdministrator(
                RequiredText(administrator.ManifestIdentity),
                RequiredText(administrator.UserName)))
            .ToArray();
        if (administrators.Select(item => item.ManifestIdentity)
                .Distinct(StringComparer.Ordinal).Count() != administrators.Length
            || administrators.Select(item => item.UserName)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != administrators.Length)
        {
            throw new BootstrapInputException();
        }

        var client = manifest.PublicMcpClient;
        if (client.RedirectUris is null || client.Scopes is null)
        {
            throw new BootstrapInputException();
        }
        var redirectUris = client.RedirectUris
            .Select(ParseAbsoluteUri)
            .ToArray();
        var resource = ParseAbsoluteUri(client.Resource);
        var expectedResource = new Uri(options.Issuer, "/mcp");
        if (!resource.Equals(expectedResource)
            || client.Scopes.Count != StaffMcpClientContract.SupportedScopes.Count
            || !client.Scopes.ToHashSet(StringComparer.Ordinal)
                .SetEquals(StaffMcpClientContract.SupportedScopes))
        {
            throw new BootstrapInputException();
        }

        var publicClient = new PublicMcpClientMetadata(
            RequiredText(client.ClientId),
            RequiredText(client.DisplayName),
            redirectUris,
            resource,
            client.Scopes);
        return new(
            RequiredText(manifest.ExpectedMigrationId),
            targetIdentity,
            administrators,
            publicClient);
    }

    private static ServiceProvider CreateProvider(BootstrapArguments options)
    {
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:{options.SqlServer},1433",
            InitialCatalog = options.SqlDatabase,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = false,
            PersistSecurityInfo = false,
            ConnectTimeout = 30,
            MultipleActiveResultSets = false
        };
        var services = new ServiceCollection();
        services
            .AddIdentityCore<PegasusIdentityUser>(identity =>
            {
                identity.Password.RequiredLength =
                    StaffAccountAdministrationPolicy.MinimumPasswordLength;
                identity.Password.RequireDigit = false;
                identity.Password.RequireLowercase = false;
                identity.Password.RequireNonAlphanumeric = false;
                identity.Password.RequireUppercase = false;
                identity.Lockout.AllowedForNewUsers = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PegasusDbContext>();
        services.AddOpenIddict()
            .AddCore(openIddict =>
            {
                openIddict.UseEntityFrameworkCore()
                    .UseDbContext<PegasusDbContext>();
            });
        services.AddPegasusInfrastructure((_, database) =>
            database.UseSqlServer(connection.ConnectionString));
        services.AddPegasusApplicationInitialization();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = true
        });
    }

    private static string ReadConfirmedSecret(int ordinal)
    {
        var password = ReadSecret($"Initial Administrator {ordinal} temporary password: ");
        var confirmation = ReadSecret($"Confirm Initial Administrator {ordinal} temporary password: ");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new BootstrapInputException();
        }

        return password;
    }

    private static string ReadSecret(string prompt)
    {
        Console.Error.Write(prompt);
        if (Console.IsInputRedirected)
        {
            var redirected = Console.ReadLine() ?? throw new BootstrapInputException();
            Console.Error.WriteLine();
            return redirected;
        }

        var buffer = new char[StaffAccountAdministrationPolicy.MaximumPasswordLength];
        var length = 0;
        var tooLong = false;
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.Error.WriteLine();
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (length > 0)
                    {
                        buffer[--length] = '\0';
                    }

                    continue;
                }

                if (char.IsControl(key.KeyChar))
                {
                    continue;
                }

                if (length == buffer.Length)
                {
                    tooLong = true;
                    continue;
                }

                buffer[length++] = key.KeyChar;
            }

            if (tooLong)
            {
                throw new BootstrapInputException();
            }

            return new string(buffer, 0, length);
        }
        finally
        {
            Array.Clear(buffer);
        }
    }

    private static bool MatchesAssemblyIdentity(
        string? productVersion,
        string? sourceRevision)
    {
        var informationalVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return string.Equals(
            informationalVersion,
            $"{productVersion}+{sourceRevision}",
            StringComparison.Ordinal);
    }

    private static bool IsExactSourceRevision(string? value) =>
        value is not null
        && value.Length == 40
        && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static bool IsSqlServerHost(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value == value.Trim()
        && value.Contains('.')
        && Uri.CheckHostName(value) == UriHostNameType.Dns;

    internal static bool TryNormalizeIssuer(string? value, out Uri issuer)
    {
        issuer = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            || !candidate.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(candidate.UserInfo)
            || candidate.AbsolutePath != "/"
            || !string.IsNullOrEmpty(candidate.Query)
            || !string.IsNullOrEmpty(candidate.Fragment))
        {
            return false;
        }

        issuer = new Uri(candidate.GetLeftPart(UriPartial.Authority) + '/', UriKind.Absolute);
        return true;
    }

    private static string CreateTargetIdentity(string sqlServer, string sqlDatabase) =>
        $"sqlserver://{sqlServer.ToLowerInvariant()}/{Uri.EscapeDataString(sqlDatabase)}";

    private static Uri ParseAbsoluteUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : throw new BootstrapInputException();

    private static string RequiredText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim()
            ? value
            : throw new BootstrapInputException();

    private static void WriteUsage() => Console.Error.WriteLine(
        "Usage: Pegasus.Bootstrap --manifest <path> --approved-manifest-sha256 <sha256> " +
        "--server <sql-fqdn> --database <database> --issuer <https-origin>");
}

internal sealed record BootstrapArguments(
    string ManifestPath,
    string ApprovedManifestSha256,
    string SqlServer,
    string SqlDatabase,
    Uri Issuer,
    bool ShowHelp)
{
    public static BootstrapArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            return new(string.Empty, string.Empty, string.Empty, string.Empty, null!, true);
        }

        if (args.Length != 10)
        {
            throw new BootstrapUsageException();
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            var option = args[index];
            if (option is not (
                "--manifest" or
                "--approved-manifest-sha256" or
                "--server" or
                "--database" or
                "--issuer")
                || !values.TryAdd(option, args[index + 1]))
            {
                throw new BootstrapUsageException();
            }
        }

        var approvedSha256 = values["--approved-manifest-sha256"];
        if (approvedSha256.Length != 64
            || approvedSha256.Any(character => !char.IsAsciiHexDigit(character))
            || !Program.TryNormalizeIssuer(values["--issuer"], out var issuer)
            || !Program.IsSqlServerHost(values["--server"])
            || string.IsNullOrWhiteSpace(values["--database"])
            || values["--database"] != values["--database"].Trim()
            || values["--database"].Length > 128)
        {
            throw new BootstrapUsageException();
        }

        return new(
            values["--manifest"],
            approvedSha256.ToLowerInvariant(),
            values["--server"],
            values["--database"],
            issuer,
            false);
    }
}

internal sealed record BootstrapManifest
{
    public required int SchemaVersion { get; init; }

    public required string ProductVersion { get; init; }

    public required string SourceRevision { get; init; }

    public required string ExpectedMigrationId { get; init; }

    public required string TargetIdentity { get; init; }

    public required string SqlServer { get; init; }

    public required string SqlDatabase { get; init; }

    public required string Issuer { get; init; }

    public required IReadOnlyList<BootstrapAdministrator> Administrators { get; init; }

    public required BootstrapPublicMcpClient PublicMcpClient { get; init; }
}

internal sealed record BootstrapAdministrator
{
    public required string ManifestIdentity { get; init; }

    public required string UserName { get; init; }
}

internal sealed record BootstrapPublicMcpClient
{
    public required string ClientId { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<string> RedirectUris { get; init; }

    public required string Resource { get; init; }

    public required IReadOnlyList<string> Scopes { get; init; }
}

internal sealed record ApprovedAdministrator(string ManifestIdentity, string UserName);

internal sealed record ValidatedBootstrapManifest(
    string ExpectedMigrationId,
    string TargetIdentity,
    IReadOnlyList<ApprovedAdministrator> Administrators,
    PublicMcpClientMetadata PublicMcpClient);

internal sealed class BootstrapUsageException : Exception;

internal sealed class BootstrapInputException : Exception;
