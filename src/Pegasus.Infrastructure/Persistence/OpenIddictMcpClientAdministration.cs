using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class OpenIddictMcpClientAdministration(
    PegasusDbContext context,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictTokenManager tokenManager,
    TimeProvider timeProvider)
    : IPublicMcpClientStore,
      IStaffMcpAuthorizationStore
{
    private const string RevokedProperty = "pegasus:client-status";
    private const string RevokedPropertyValue = "revoked";

    public async Task<RegisterPublicMcpClientResult> RegisterAsync(
        RegisterPublicMcpClientRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var result = await RegisterCoreAsync(request, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<RevokePublicMcpClientResult> RevokeAsync(
        RevokePublicMcpClientRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(
            "oauth_client",
            request.OperationKey,
            cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != request.ClientId
                || replay.EventKind != "public_mcp_client_revoked"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal))
            {
                throw OperationConflict();
            }

            var counts = ParseRevocationCounts(replay.AfterJson);
            await transaction.CommitAsync(cancellationToken);
            return new(request.ClientId, counts.Authorizations, counts.Tokens, WasReplay: true);
        }

        var application = await applicationManager.FindByClientIdAsync(
            request.ClientId,
            cancellationToken)
            ?? throw new AuthenticationClientAdministrationException(
                AuthenticationClientAdministrationError.ClientNotFound);
        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
        var applicationId = await applicationManager.GetIdAsync(application, cancellationToken)
            ?? throw new InvalidOperationException(
                "The registered OAuth client has no persistent identifier.");
        var wasAlreadyRevoked = IsRevoked(descriptor);
        var tokens = wasAlreadyRevoked
            ? 0L
            : await tokenManager.RevokeByApplicationIdAsync(
                applicationId,
                cancellationToken);
        var authorizations = wasAlreadyRevoked
            ? 0L
            : await authorizationManager.RevokeByApplicationIdAsync(
                applicationId,
                cancellationToken);
        if (!wasAlreadyRevoked)
        {
            await applicationManager.UpdateAsync(
                application,
                CreateRevokedDescriptor(descriptor),
                cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        AddHistory(
            "oauth_client",
            request.ClientId,
            "public_mcp_client_revoked",
            request.Actor,
            request.OperationKey,
            request.Reason,
            Snapshot(descriptor, isActive: !wasAlreadyRevoked),
            RevokedSnapshot(request.ClientId, authorizations, tokens),
            now);
        AddSecurityEvent(
            SecurityEventType.Client,
            request.ClientId,
            request.OperationKey,
            "public_mcp_client_revoked",
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            request.ClientId,
            authorizations,
            tokens,
            WasReplay: false);
    }

    async Task<RevokeStaffMcpAuthorizationsResult> IStaffMcpAuthorizationStore.RevokeAsync(
        RevokeStaffMcpAuthorizationsRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(
            "staff_mcp_authorization",
            request.OperationKey,
            cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != request.StaffId.ToString("D")
                || replay.EventKind != "staff_mcp_authorizations_revoked"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal))
            {
                throw OperationConflict();
            }

            var counts = ParseRevocationCounts(replay.AfterJson);
            await transaction.CommitAsync(cancellationToken);
            return new(request.StaffId, counts.Authorizations, counts.Tokens, WasReplay: true);
        }

        if (!await context.Users.AnyAsync(
                user => user.Id == request.StaffId,
                cancellationToken))
        {
            throw new AuthenticationClientAdministrationException(
                AuthenticationClientAdministrationError.StaffAccountNotFound);
        }

        var subject = request.StaffId.ToString("D");
        var tokens = await tokenManager.RevokeBySubjectAsync(subject, cancellationToken);
        var authorizations = await authorizationManager.RevokeBySubjectAsync(
            subject,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        AddHistory(
            "staff_mcp_authorization",
            subject,
            "staff_mcp_authorizations_revoked",
            request.Actor,
            request.OperationKey,
            request.Reason,
            beforeJson: null,
            RevokedStaffSnapshot(request.StaffId, authorizations, tokens),
            now);
        AddSecurityEvent(
            SecurityEventType.Token,
            subject,
            request.OperationKey,
            "staff_mcp_authorizations_revoked",
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            request.StaffId,
            authorizations,
            tokens,
            WasReplay: false);
    }

    internal async Task<RegisterPublicMcpClientResult> RegisterInitialClientAsync(
        ActionActor actor,
        PublicMcpClientMetadata client,
        string operationKey,
        CancellationToken cancellationToken) =>
        await RegisterCoreAsync(
            new(
                actor,
                client,
                "Approved application-initialization public MCP client.",
                operationKey),
            cancellationToken);

    private async Task<RegisterPublicMcpClientResult> RegisterCoreAsync(
        RegisterPublicMcpClientRequest request,
        CancellationToken cancellationToken)
    {
        var expected = CreateActiveDescriptor(request.Client);
        var expectedSnapshot = Snapshot(request.Client, isActive: true);
        var replay = await FindOperationAsync(
            "oauth_client",
            request.OperationKey,
            cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != request.Client.ClientId
                || replay.EventKind != "public_mcp_client_registered"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal)
                || !string.Equals(replay.AfterJson, expectedSnapshot, StringComparison.Ordinal))
            {
                throw OperationConflict();
            }

            return new(
                request.Client,
                IsPublic: true,
                RequiresPkceS256: true,
                WasReplay: true);
        }

        var application = await applicationManager.FindByClientIdAsync(
            request.Client.ClientId,
            cancellationToken);
        string? beforeJson = null;
        if (application is null)
        {
            await applicationManager.CreateAsync(expected, cancellationToken);
        }
        else
        {
            var current = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(current, application, cancellationToken);
            beforeJson = Snapshot(current, isActive: !IsRevoked(current));
            if (IsRevoked(current))
            {
                await applicationManager.UpdateAsync(
                    application,
                    expected,
                    cancellationToken);
            }
            else if (!Matches(current, expected))
            {
                throw new AuthenticationClientAdministrationException(
                    AuthenticationClientAdministrationError.ClientMetadataConflict);
            }
        }

        var now = timeProvider.GetUtcNow();
        AddHistory(
            "oauth_client",
            request.Client.ClientId,
            "public_mcp_client_registered",
            request.Actor,
            request.OperationKey,
            request.Reason,
            beforeJson,
            expectedSnapshot,
            now);
        AddSecurityEvent(
            SecurityEventType.Client,
            request.Client.ClientId,
            request.OperationKey,
            "public_mcp_client_registered",
            now);
        return new(
            request.Client,
            IsPublic: true,
            RequiresPkceS256: true,
            WasReplay: false);
    }

    private Task<ActionHistoryEntity?> FindOperationAsync(
        string aggregateType,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.ActionHistory.SingleOrDefaultAsync(
            item => item.AggregateType == aggregateType
                && item.CorrelationId == operationKey,
            cancellationToken);

    private void AddHistory(
        string aggregateType,
        string aggregateId,
        string eventKind,
        ActionActor actor,
        string correlationId,
        string reason,
        string? beforeJson,
        string afterJson,
        DateTimeOffset occurredAtUtc)
    {
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role).Select(RoleName)),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "succeeded",
            CorrelationId = correlationId,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson
        });
    }

    private void AddSecurityEvent(
        SecurityEventType type,
        string subjectId,
        string correlationId,
        string reasonCode,
        DateTimeOffset occurredAtUtc)
    {
        context.SecurityEvents.Add(new SecurityEventEntity
        {
            Id = Guid.NewGuid(),
            Type = type.ToString(),
            Outcome = SecurityEventOutcome.Succeeded.ToString(),
            SubjectId = subjectId,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            ReasonCode = reasonCode
        });
    }

    private static OpenIddictApplicationDescriptor CreateActiveDescriptor(
        PublicMcpClientMetadata client)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = client.DisplayName
        };
        descriptor.RedirectUris.UnionWith(client.RedirectUris);
        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Revocation,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Scopes.Profile,
            .. client.Scopes.Select(scope =>
                OpenIddictConstants.Permissions.Prefixes.Scope + scope)
        ]);
        descriptor.AddResourcePermissions(client.Resource.AbsoluteUri);
        descriptor.Requirements.Add(
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        return descriptor;
    }

    private static OpenIddictApplicationDescriptor CreateRevokedDescriptor(
        OpenIddictApplicationDescriptor current)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = current.ClientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = current.DisplayName
        };
        descriptor.Properties[RevokedProperty] =
            JsonSerializer.SerializeToElement(RevokedPropertyValue);
        return descriptor;
    }

    private static bool IsRevoked(OpenIddictApplicationDescriptor descriptor) =>
        descriptor.Properties.TryGetValue(RevokedProperty, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() == RevokedPropertyValue;

    private static bool Matches(
        OpenIddictApplicationDescriptor current,
        OpenIddictApplicationDescriptor expected) =>
        string.Equals(current.ClientId, expected.ClientId, StringComparison.Ordinal)
        && string.Equals(current.ClientType, expected.ClientType, StringComparison.Ordinal)
        && string.Equals(current.ConsentType, expected.ConsentType, StringComparison.Ordinal)
        && string.Equals(current.DisplayName, expected.DisplayName, StringComparison.Ordinal)
        && string.IsNullOrEmpty(current.ClientSecret)
        && current.RedirectUris.SetEquals(expected.RedirectUris)
        && current.PostLogoutRedirectUris.Count == 0
        && current.Permissions.SetEquals(expected.Permissions)
        && current.Requirements.SetEquals(expected.Requirements)
        && current.Properties.Count == 0;

    private static string Snapshot(
        PublicMcpClientMetadata client,
        bool isActive) =>
        JsonSerializer.Serialize(new
        {
            client.ClientId,
            client.DisplayName,
            RedirectUris = client.RedirectUris.Select(uri => uri.AbsoluteUri),
            Resource = client.Resource.AbsoluteUri,
            client.Scopes,
            ClientType = "public",
            ConsentType = "explicit",
            PkceMethod = "S256",
            IsActive = isActive
        });

    private static string Snapshot(
        OpenIddictApplicationDescriptor descriptor,
        bool isActive) =>
        JsonSerializer.Serialize(new
        {
            descriptor.ClientId,
            descriptor.DisplayName,
            RedirectUris = descriptor.RedirectUris
                .Select(uri => uri.AbsoluteUri)
                .OrderBy(uri => uri, StringComparer.Ordinal),
            Permissions = descriptor.Permissions.OrderBy(value => value, StringComparer.Ordinal),
            Requirements = descriptor.Requirements.OrderBy(value => value, StringComparer.Ordinal),
            ClientType = descriptor.ClientType,
            ConsentType = descriptor.ConsentType,
            IsActive = isActive
        });

    private static string RevokedSnapshot(
        string clientId,
        long authorizations,
        long tokens) =>
        JsonSerializer.Serialize(new
        {
            ClientId = clientId,
            IsActive = false,
            RevokedAuthorizations = authorizations,
            RevokedTokens = tokens
        });

    private static string RevokedStaffSnapshot(
        Guid staffId,
        long authorizations,
        long tokens) =>
        JsonSerializer.Serialize(new
        {
            StaffId = staffId,
            RevokedAuthorizations = authorizations,
            RevokedTokens = tokens
        });

    private static (long Authorizations, long Tokens) ParseRevocationCounts(
        string? afterJson)
    {
        if (afterJson is null)
        {
            return (0, 0);
        }

        using var document = JsonDocument.Parse(afterJson);
        var authorizations = document.RootElement.TryGetProperty(
                "RevokedAuthorizations",
                out var authorizationElement)
            && authorizationElement.TryGetInt64(out var authorizationCount)
                ? authorizationCount
                : 0;
        var tokens = document.RootElement.TryGetProperty(
                "RevokedTokens",
                out var tokenElement)
            && tokenElement.TryGetInt64(out var tokenCount)
                ? tokenCount
                : 0;
        return (authorizations, tokens);
    }

    private static AuthenticationClientAdministrationException OperationConflict() =>
        new(AuthenticationClientAdministrationError.OperationConflict);

    private static string RoleName(StaffRole role) => role switch
    {
        StaffRole.Administrator => StaffRoleNames.Administrator,
        StaffRole.Engineer => StaffRoleNames.Engineer,
        StaffRole.User => StaffRoleNames.User,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}
