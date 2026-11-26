using Azure.Storage.Blobs;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Brego;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web;

/// <summary>
/// What the one-time import did with one login.
/// </summary>
internal enum IntegrationLoginImportResult
{
    Imported,
    AlreadySet,
    SettingsAbsent,
    KeyVaultReferenceUnresolved
}

/// <summary>
/// The one-time import of the integrations' logins from the settings that held
/// them before R1288 (ADR-0091). It ships in the release that moves the logins
/// into the database and is deleted, with those settings, in the next one.
///
/// <para>
/// A login is imported only when its row is absent or empty and every one of
/// its settings is present and resolved; a value the platform left as an
/// unresolved Key Vault reference is never stored. An existing login is never
/// overwritten. Brego's stored sign-in comes from the blob it was kept in, and
/// Brego imports with its e-mail address alone when the blob holds none. The
/// log names which logins were imported, never a value, and a failure is
/// logged and the host starts.
/// </para>
/// </summary>
internal static partial class IntegrationLoginSettingsImport
{
    private const string Category = "Pegasus.Web.IntegrationLoginSettingsImport";

    private static readonly (IntegrationLoginField Field, string Key)[] GlassValuationKeys =
    [
        (IntegrationLoginField.Username, "Glass:ValuationAccount:Username"),
        (IntegrationLoginField.Password, "Glass:ValuationAccount:Password")
    ];

    private static readonly (IntegrationLoginField Field, string Key)[] CazanaKeys =
    [
        (IntegrationLoginField.Email, "Cazana:CompanionAccount:Email"),
        (IntegrationLoginField.Password, "Cazana:CompanionAccount:Password")
    ];

    private static readonly (IntegrationLoginField Field, string Key)[] BregoKeys =
    [
        (IntegrationLoginField.Email, "Brego:ValuationAccount:Email")
    ];

    private static readonly (IntegrationLoginField Field, string Key)[] ExperianHistoryKeys =
        ExperianKeys("Experian:HistoryAccount");

    private static readonly (IntegrationLoginField Field, string Key)[] ExperianCapKeys =
        ExperianKeys("Experian:CapAccount");

    /// <summary>
    /// Runs the import on the host's own services. Nothing it meets stops the
    /// host: a failure is logged by login and the next login is tried.
    /// </summary>
    public static async Task RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        BlobClient? bregoSessionBlob)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Category);
        if (services.GetService<EfIntegrationLoginStore>() is not { } store)
        {
            return;
        }

        try
        {
            await ImportAsync(store, configuration, bregoSessionBlob, logger, CancellationToken.None);
        }
        catch (Exception exception)
        {
            LogImportFailed(logger, exception);
        }
    }

    /// <summary>Imports each login in turn and answers what happened to each.</summary>
    internal static async Task<IReadOnlyDictionary<IntegrationLogin, IntegrationLoginImportResult>> ImportAsync(
        EfIntegrationLoginStore store,
        IConfiguration configuration,
        BlobClient? bregoSessionBlob,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        var results = new Dictionary<IntegrationLogin, IntegrationLoginImportResult>();
        foreach (var login in Enum.GetValues<IntegrationLogin>())
        {
            try
            {
                var result = await ImportOneAsync(store, configuration, login, bregoSessionBlob, cancellationToken);
                results[login] = result;
                LogLoginImport(logger, login, result);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogLoginImportFailed(logger, login, exception);
            }
        }

        return results;
    }

    private static async Task<IntegrationLoginImportResult> ImportOneAsync(
        EfIntegrationLoginStore store,
        IConfiguration configuration,
        IntegrationLogin login,
        BlobClient? bregoSessionBlob,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<IntegrationLoginField, string>();
        foreach (var (field, key) in KeysOf(login))
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                return IntegrationLoginImportResult.SettingsAbsent;
            }

            if (KeyVaultReference.IsUnresolved(value))
            {
                return IntegrationLoginImportResult.KeyVaultReferenceUnresolved;
            }

            values[field] = value;
        }

        Func<CancellationToken, Task<BregoStoredSignIn?>>? readSignIn = null;
        if (login == IntegrationLogin.Brego && bregoSessionBlob is not null)
        {
            var email = values[IntegrationLoginField.Email];
            var blobStore = new AzureBlobBregoSessionStore(bregoSessionBlob);
            readSignIn = token => blobStore.TryLoadAsync(email, token);
        }

        return await store.ImportAsync(login, values, readSignIn, cancellationToken)
            ? IntegrationLoginImportResult.Imported
            : IntegrationLoginImportResult.AlreadySet;
    }

    private static (IntegrationLoginField Field, string Key)[] KeysOf(IntegrationLogin login) =>
        login switch
        {
            IntegrationLogin.GlassValuation => GlassValuationKeys,
            IntegrationLogin.Cazana => CazanaKeys,
            IntegrationLogin.Brego => BregoKeys,
            IntegrationLogin.ExperianHistory => ExperianHistoryKeys,
            IntegrationLogin.ExperianCap => ExperianCapKeys,
            _ => throw new ArgumentOutOfRangeException(nameof(login))
        };

    private static (IntegrationLoginField Field, string Key)[] ExperianKeys(string section) =>
    [
        (IntegrationLoginField.ClientId, section + ":ClientId"),
        (IntegrationLoginField.ClientSecret, section + ":ClientSecret"),
        (IntegrationLoginField.Username, section + ":Username"),
        (IntegrationLoginField.Password, section + ":Password")
    ];

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Integration login {Login} settings import: {Result}")]
    private static partial void LogLoginImport(ILogger logger, IntegrationLogin login, IntegrationLoginImportResult result);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Integration login {Login} was not imported from its settings; the host starts without it")]
    private static partial void LogLoginImportFailed(ILogger logger, IntegrationLogin login, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The integration logins settings import failed; the host starts without it")]
    private static partial void LogImportFailed(ILogger logger, Exception exception);
}
