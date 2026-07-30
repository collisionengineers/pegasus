using System.Security.Cryptography;

namespace Pegasus.Core.Identity;

public sealed record InitialAdministratorCredentials(
    string ManifestIdentity,
    string UserName,
    string TemporaryPassword);

public sealed record InitializeApplicationRequest(
    string ExpectedMigrationId,
    string ManifestSha256,
    string ApprovedManifestSha256,
    string TargetIdentity,
    IReadOnlyList<InitialAdministratorCredentials> InitialAdministrators,
    PublicMcpClientMetadata PublicMcpClient,
    string CorrelationId);

public sealed record InitializeApplicationStoreRequest(
    string ExpectedMigrationId,
    string ManifestSha256,
    string TargetIdentity,
    IReadOnlyList<InitialAdministratorCredentials> InitialAdministrators,
    PublicMcpClientMetadata PublicMcpClient,
    string CorrelationId,
    ActionActor Actor);

public sealed record InitializeApplicationResult(
    string ManifestSha256,
    string MigrationId,
    string TargetIdentity,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<StaffAccountSummary> InitialAdministrators,
    RegisterPublicMcpClientResult PublicMcpClient);

public interface IApplicationInitializationStore
{
    Task<InitializeApplicationResult> InitializeAsync(
        InitializeApplicationStoreRequest request,
        CancellationToken cancellationToken);
}

public interface IInitializeApplication
{
    Task<InitializeApplicationResult> ExecuteAsync(
        InitializeApplicationRequest request,
        CancellationToken cancellationToken);
}

public sealed class InitializeApplication(IApplicationInitializationStore store)
    : IInitializeApplication
{
    public const int InitialAdministratorCount = 2;

    private readonly IApplicationInitializationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<InitializeApplicationResult> ExecuteAsync(
        InitializeApplicationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var manifestSha256 = NormalizeSha256(
            request.ManifestSha256,
            nameof(request.ManifestSha256));
        var approvedManifestSha256 = NormalizeSha256(
            request.ApprovedManifestSha256,
            nameof(request.ApprovedManifestSha256));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(manifestSha256),
                Convert.FromHexString(approvedManifestSha256)))
        {
            throw new ApplicationInitializationException(
                ApplicationInitializationError.ManifestMismatch);
        }

        var expectedMigrationId = StaffAccountAdministrationPolicy.NormalizeRequiredText(
            request.ExpectedMigrationId,
            150,
            nameof(request.ExpectedMigrationId));
        var targetIdentity = StaffAccountAdministrationPolicy.NormalizeRequiredText(
            request.TargetIdentity,
            200,
            nameof(request.TargetIdentity));
        var correlationId = StaffAccountAdministrationPolicy.NormalizeRequiredText(
            request.CorrelationId,
            StaffAccountAdministrationPolicy.MaximumOperationKeyLength,
            nameof(request.CorrelationId));
        var administrators = NormalizeAdministrators(request.InitialAdministrators);
        var client = PublicMcpClientPolicy.NormalizeForInitialization(request.PublicMcpClient);
        var actor = ActionActor.Bootstrap(manifestSha256);
        StaffAuthorization.Require(actor, StaffAccessRight.InitializeApplication);

        return _store.InitializeAsync(
            new(
                expectedMigrationId,
                manifestSha256,
                targetIdentity,
                administrators,
                client,
                correlationId,
                actor),
            cancellationToken);
    }

    private static InitialAdministratorCredentials[] NormalizeAdministrators(
        IReadOnlyList<InitialAdministratorCredentials> administrators)
    {
        ArgumentNullException.ThrowIfNull(administrators);
        if (administrators.Count != InitialAdministratorCount)
        {
            throw new ArgumentException(
                $"Application initialization requires exactly {InitialAdministratorCount} approved administrators.",
                nameof(administrators));
        }

        var normalized = administrators.Select(administrator =>
        {
            ArgumentNullException.ThrowIfNull(administrator);
            StaffAccountAdministrationPolicy.ValidateTemporaryPassword(
                administrator.TemporaryPassword,
                nameof(administrator.TemporaryPassword));
            return administrator with
            {
                ManifestIdentity = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                    administrator.ManifestIdentity,
                    100,
                    nameof(administrator.ManifestIdentity)),
                UserName = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                    administrator.UserName,
                    StaffAccountAdministrationPolicy.MaximumUserNameLength,
                    nameof(administrator.UserName))
            };
        }).ToArray();

        if (normalized.Select(item => item.ManifestIdentity)
                .Distinct(StringComparer.Ordinal).Count() != InitialAdministratorCount
            || normalized.Select(item => item.UserName)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != InitialAdministratorCount)
        {
            throw new ArgumentException(
                "The approved initial administrators require distinct manifest identities and usernames.",
                nameof(administrators));
        }

        return normalized;
    }

    private static string NormalizeSha256(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != 64
            || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "A 64-character hexadecimal SHA-256 is required.",
                parameterName);
        }

        return value.ToLowerInvariant();
    }
}

public enum ApplicationInitializationError
{
    ManifestMismatch,
    MigrationMismatch,
    AlreadyInitialized,
    NonEmptyTarget,
    InvalidInitialAccount,
    InvalidPublicClient
}

public sealed class ApplicationInitializationException(
    ApplicationInitializationError error)
    : InvalidOperationException("Application initialization was denied without changing the target.")
{
    public ApplicationInitializationError Error { get; } = error;
}
