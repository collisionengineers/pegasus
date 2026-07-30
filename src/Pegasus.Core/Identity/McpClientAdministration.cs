using System.Net;

namespace Pegasus.Core.Identity;

public static class StaffMcpClientContract
{
    public const string ReadScope = "pegasus.read";
    public const string WriteScope = "pegasus.write";

    public static IReadOnlyList<string> SupportedScopes { get; } =
        [ReadScope, WriteScope];
}

public sealed record PublicMcpClientMetadata(
    string ClientId,
    string DisplayName,
    IReadOnlyList<Uri> RedirectUris,
    Uri Resource,
    IReadOnlyList<string> Scopes);

public sealed record RegisterPublicMcpClientRequest(
    ActionActor Actor,
    PublicMcpClientMetadata Client,
    string Reason,
    string OperationKey);

public sealed record RegisterPublicMcpClientResult(
    PublicMcpClientMetadata Client,
    bool IsPublic,
    bool RequiresPkceS256,
    bool WasReplay);

public sealed record RevokePublicMcpClientRequest(
    ActionActor Actor,
    string ClientId,
    string Reason,
    string OperationKey);

public sealed record RevokePublicMcpClientResult(
    string ClientId,
    long RevokedAuthorizations,
    long RevokedTokens,
    bool WasReplay);

public sealed record RevokeStaffMcpAuthorizationsRequest(
    ActionActor Actor,
    Guid StaffId,
    string Reason,
    string OperationKey);

public sealed record RevokeStaffMcpAuthorizationsResult(
    Guid StaffId,
    long RevokedAuthorizations,
    long RevokedTokens,
    bool WasReplay);

public interface IPublicMcpClientStore
{
    Task<RegisterPublicMcpClientResult> RegisterAsync(
        RegisterPublicMcpClientRequest request,
        CancellationToken cancellationToken);

    Task<RevokePublicMcpClientResult> RevokeAsync(
        RevokePublicMcpClientRequest request,
        CancellationToken cancellationToken);
}

public interface IStaffMcpAuthorizationStore
{
    Task<RevokeStaffMcpAuthorizationsResult> RevokeAsync(
        RevokeStaffMcpAuthorizationsRequest request,
        CancellationToken cancellationToken);
}

public interface IRegisterPublicMcpClient
{
    Task<RegisterPublicMcpClientResult> ExecuteAsync(
        RegisterPublicMcpClientRequest request,
        CancellationToken cancellationToken);
}

public interface IRevokePublicMcpClient
{
    Task<RevokePublicMcpClientResult> ExecuteAsync(
        RevokePublicMcpClientRequest request,
        CancellationToken cancellationToken);
}

public interface IRevokeStaffMcpAuthorizations
{
    Task<RevokeStaffMcpAuthorizationsResult> ExecuteAsync(
        RevokeStaffMcpAuthorizationsRequest request,
        CancellationToken cancellationToken);
}

public sealed class RegisterPublicMcpClient(IPublicMcpClientStore store)
    : IRegisterPublicMcpClient
{
    private readonly IPublicMcpClientStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<RegisterPublicMcpClientResult> ExecuteAsync(
        RegisterPublicMcpClientRequest request,
        CancellationToken cancellationToken) =>
        _store.RegisterAsync(
            PublicMcpClientPolicy.Normalize(request),
            cancellationToken);
}

public sealed class RevokePublicMcpClient(IPublicMcpClientStore store)
    : IRevokePublicMcpClient
{
    private readonly IPublicMcpClientStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<RevokePublicMcpClientResult> ExecuteAsync(
        RevokePublicMcpClientRequest request,
        CancellationToken cancellationToken) =>
        _store.RevokeAsync(
            PublicMcpClientPolicy.Normalize(request),
            cancellationToken);
}

public sealed class RevokeStaffMcpAuthorizations(IStaffMcpAuthorizationStore store)
    : IRevokeStaffMcpAuthorizations
{
    private readonly IStaffMcpAuthorizationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<RevokeStaffMcpAuthorizationsResult> ExecuteAsync(
        RevokeStaffMcpAuthorizationsRequest request,
        CancellationToken cancellationToken) =>
        _store.RevokeAsync(
            PublicMcpClientPolicy.Normalize(request),
            cancellationToken);
}

public static class PublicMcpClientPolicy
{
    public const int MaximumClientIdLength = 100;
    public const int MaximumDisplayNameLength = 200;
    public const int MaximumRedirectUriCount = 10;

    public static RegisterPublicMcpClientRequest Normalize(
        RegisterPublicMcpClientRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(
            request.Actor,
            StaffAccessRight.ManageAuthenticationClients);
        return request with
        {
            Client = Normalize(request.Client),
            Reason = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                request.Reason,
                StaffAccountAdministrationPolicy.MaximumReasonLength,
                nameof(request.Reason)),
            OperationKey = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                request.OperationKey,
                StaffAccountAdministrationPolicy.MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    public static RevokePublicMcpClientRequest Normalize(
        RevokePublicMcpClientRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(
            request.Actor,
            StaffAccessRight.ManageAuthenticationClients);
        return request with
        {
            ClientId = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                request.ClientId,
                MaximumClientIdLength,
                nameof(request.ClientId)),
            Reason = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                request.Reason,
                StaffAccountAdministrationPolicy.MaximumReasonLength,
                nameof(request.Reason)),
            OperationKey = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                request.OperationKey,
                StaffAccountAdministrationPolicy.MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    public static RevokeStaffMcpAuthorizationsRequest Normalize(
        RevokeStaffMcpAuthorizationsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAccountAdministrationPolicy.RequireStaffId(request.StaffId);
        var isSelf = request.Actor.Kind == ActorKind.Staff
            && Guid.TryParse(request.Actor.SubjectId, out var actorStaffId)
            && actorStaffId == request.StaffId;
        if (!isSelf)
        {
            StaffAuthorization.Require(request.Actor, StaffAccessRight.ReviewStaffAccess);
        }

        return request with
        {
            Reason = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                request.Reason,
                StaffAccountAdministrationPolicy.MaximumReasonLength,
                nameof(request.Reason)),
            OperationKey = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                request.OperationKey,
                StaffAccountAdministrationPolicy.MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    internal static PublicMcpClientMetadata NormalizeForInitialization(
        PublicMcpClientMetadata client) => Normalize(client);

    private static PublicMcpClientMetadata Normalize(PublicMcpClientMetadata client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var clientId = StaffAccountAdministrationPolicy.NormalizeRequiredText(
            client.ClientId,
            MaximumClientIdLength,
            nameof(client.ClientId));
        var displayName = StaffAccountAdministrationPolicy.NormalizeRequiredText(
            client.DisplayName,
            MaximumDisplayNameLength,
            nameof(client.DisplayName));
        ArgumentNullException.ThrowIfNull(client.RedirectUris);
        if (client.RedirectUris.Count is < 1 or > MaximumRedirectUriCount)
        {
            throw new ArgumentException(
                $"A public MCP client requires between 1 and {MaximumRedirectUriCount} redirect URIs.",
                nameof(client));
        }

        var redirectUris = client.RedirectUris
            .Select(ValidateRedirectUri)
            .Distinct()
            .OrderBy(uri => uri.AbsoluteUri, StringComparer.Ordinal)
            .ToArray();
        if (redirectUris.Length != client.RedirectUris.Count)
        {
            throw new ArgumentException(
                "A public MCP client cannot contain duplicate redirect URIs.",
                nameof(client));
        }

        var resource = ValidateResource(client.Resource);
        ArgumentNullException.ThrowIfNull(client.Scopes);
        var scopes = client.Scopes
            .Select(scope => StaffAccountAdministrationPolicy.NormalizeRequiredText(
                scope,
                100,
                nameof(client.Scopes)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray();
        if (scopes.Length == 0
            || scopes.Length != client.Scopes.Count
            || scopes.Any(scope => !StaffMcpClientContract.SupportedScopes.Contains(
                scope,
                StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                "A public MCP client requires a unique, supported Pegasus MCP scope set.",
                nameof(client));
        }

        return new(clientId, displayName, redirectUris, resource, scopes);
    }

    private static Uri ValidateRedirectUri(Uri redirectUri)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);
        if (!redirectUri.IsAbsoluteUri
            || !string.IsNullOrEmpty(redirectUri.UserInfo)
            || !string.IsNullOrEmpty(redirectUri.Fragment))
        {
            throw new ArgumentException(
                "A redirect URI must be absolute and cannot contain user information or a fragment.",
                nameof(redirectUri));
        }

        var isHttps = redirectUri.Scheme.Equals(
            Uri.UriSchemeHttps,
            StringComparison.OrdinalIgnoreCase);
        var isLoopbackHttp = redirectUri.Scheme.Equals(
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase)
            && IPAddress.TryParse(redirectUri.Host, out var address)
            && IPAddress.IsLoopback(address);
        if (!isHttps && !isLoopbackHttp)
        {
            throw new ArgumentException(
                "A redirect URI must use HTTPS or an RFC 8252 loopback IP HTTP URI.",
                nameof(redirectUri));
        }

        return new Uri(redirectUri.AbsoluteUri, UriKind.Absolute);
    }

    private static Uri ValidateResource(Uri resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!resource.IsAbsoluteUri
            || !resource.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !resource.AbsolutePath.Equals("/mcp", StringComparison.Ordinal)
            || !string.IsNullOrEmpty(resource.UserInfo)
            || !string.IsNullOrEmpty(resource.Query)
            || !string.IsNullOrEmpty(resource.Fragment))
        {
            throw new ArgumentException(
                "The public MCP client resource must be the exact absolute HTTPS /mcp URI.",
                nameof(resource));
        }

        return new Uri(resource.AbsoluteUri, UriKind.Absolute);
    }
}

public enum AuthenticationClientAdministrationError
{
    ClientNotFound,
    ClientMetadataConflict,
    StaffAccountNotFound,
    OperationConflict
}

public sealed class AuthenticationClientAdministrationException(
    AuthenticationClientAdministrationError error)
    : InvalidOperationException("The authentication-client request could not be completed.")
{
    public AuthenticationClientAdministrationError Error { get; } = error;
}
