using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.Identity.Client;

namespace Pegasus.Infrastructure.Maintenance;

internal sealed class NamedOperatorTokenSession
{
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/Mail.Read.Shared"];
    private readonly ProductionIntakeCleanBaselineInvocation invocation;
    private readonly IPublicClientApplication application;
    private AuthenticationResult? interactiveResult;

    internal NamedOperatorTokenSession(ProductionIntakeCleanBaselineInvocation invocation)
    {
        this.invocation = invocation;
        application = PublicClientApplicationBuilder
            .Create(invocation.PublicClientId.ToString("D"))
            .WithAuthority(AzureCloudInstance.AzurePublic, invocation.TenantId)
            .WithRedirectUri("http://localhost")
            .Build();
    }

    internal async Task<OperatorToken> SignInAsync(CancellationToken cancellationToken)
    {
        if (interactiveResult is null)
        {
            interactiveResult = await application.AcquireTokenInteractive(GraphScopes)
                .WithLoginHint(invocation.OperatorUpn)
                .WithPrompt(Prompt.ForceLogin)
                .WithClaims("{\"access_token\":{\"amr\":{\"essential\":true,\"values\":[\"mfa\"]}}," +
                    "\"id_token\":{\"amr\":{\"essential\":true,\"values\":[\"mfa\"]}}}")
                .ExecuteAsync(cancellationToken);
        }
        return Validate(interactiveResult.AccessToken, interactiveResult.IdToken, requireMfa: true);
    }

    internal async Task<string> GetTokenAsync(
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        await SignInAsync(cancellationToken);
        AuthenticationResult result;
        try
        {
            result = await application.AcquireTokenSilent(scopes, interactiveResult!.Account)
                .ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException exception)
        {
            throw new InvalidOperationException(
                "The dedicated public client lacks pre-consent for one required delegated resource; " +
                "do not substitute Azure CLI or another application identity.",
                exception);
        }
        Validate(result.AccessToken, result.IdToken, requireMfa: false);
        return result.AccessToken;
    }

    internal OperatorToken ValidateGraphToken(string accessToken, string? idToken, bool requireMfa) =>
        Validate(accessToken, idToken, requireMfa);

    internal static IReadOnlyList<string> Scope(string resource) => [$"{resource}/.default"];

    private OperatorToken Validate(string accessToken, string? idToken, bool requireMfa)
    {
        var principal = ReadJwt(accessToken);
        var tenant = RequiredGuid(principal, "tid");
        var objectId = RequiredGuid(principal, "oid");
        var client = RequiredGuid(principal, principal.HasClaim(item => item.Type == "azp") ? "azp" : "appid");
        var upn = principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst("upn")?.Value
            ?? throw new InvalidOperationException("The delegated token omitted the named operator.");
        var scopes = (principal.FindFirst("scp")?.Value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tenant != invocation.TenantId
            || client != invocation.PublicClientId
            || !upn.Equals(invocation.OperatorUpn, StringComparison.OrdinalIgnoreCase)
            || scopes.Length == 0
            || principal.HasClaim(item => item.Type == "roles")
            || principal.FindFirst("idtyp")?.Value.Equals("app", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new UnauthorizedAccessException(
                "The token is not the exact allowlisted named-operator delegated public-client identity.");
        }
        var identityPrincipal = string.IsNullOrWhiteSpace(idToken) ? principal : ReadJwt(idToken);
        if (requireMfa && !identityPrincipal.FindAll("amr").Any(claim =>
                claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException("The interactive named-operator sign-in did not prove MFA.");
        }
        var groups = principal.FindAll("groups")
            .Select(claim => Guid.TryParse(claim.Value, out var value) ? value : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .ToArray();
        if (principal.HasClaim(item => item.Type is "_claim_names" or "hasgroups"))
        {
            throw new UnauthorizedAccessException(
                "The operator has an overage group claim, so effective role census cannot fail closed.");
        }
        var tenantRoles = principal.FindAll("wids")
            .Select(claim => Guid.TryParse(claim.Value, out var value) ? value : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .ToArray();
        if (tenantRoles.Length != 0)
        {
            throw new UnauthorizedAccessException(
                "The named operator holds an unexpected tenant directory role.");
        }
        return new(tenant, objectId, upn, client, scopes, groups);
    }

    private static ClaimsPrincipal ReadJwt(string value)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(value);
        return new ClaimsPrincipal(new ClaimsIdentity(token.Claims, "jwt"));
    }

    private static Guid RequiredGuid(ClaimsPrincipal principal, string claim) =>
        Guid.TryParse(principal.FindFirst(claim)?.Value, out var value) && value != Guid.Empty
            ? value
            : throw new UnauthorizedAccessException($"The delegated token omitted valid {claim} identity.");
}

internal sealed record OperatorToken(
    Guid TenantId,
    Guid ObjectId,
    string Upn,
    Guid ClientId,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<Guid> GroupIds);

internal sealed class NamedOperatorTokenCredential(
    NamedOperatorTokenSession session,
    string resource) : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        var token = await session.GetTokenAsync(
            NamedOperatorTokenSession.Scope(resource),
            cancellationToken);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return new(token, jwt.ValidTo);
    }
}

internal sealed class CleanBaselineGraphClient(
    HttpClient httpClient,
    NamedOperatorTokenSession? session,
    ProductionIntakeCleanBaselineInvocation invocation) : ICleanBaselineGraphClient
{
    internal async Task<IReadOnlyList<CleanBaselineCapabilityResult>> ValidateScopeAsync(
        CancellationToken cancellationToken)
    {
        var token = await GetGraphTokenAsync(cancellationToken);
        var tokenInfo = session?.ValidateGraphToken(token.AccessToken, token.IdToken, requireMfa: false);
        var scopes = tokenInfo?.Scopes ?? token.Scopes;
        var mailScopes = scopes.Where(scope => scope.StartsWith("Mail.", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (!mailScopes.SequenceEqual(["Mail.Read.Shared"], StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "Graph delegated scope must be exactly Mail.Read.Shared; send/delete/read-write scopes are prohibited.");
        }

        using var positive = await SendAsync(
            InitialDeltaUri(invocation.MailboxIdentity, invocation.InboxFolderIdentity),
            token.AccessToken,
            cancellationToken);
        if (!positive.IsSuccessStatusCode)
        {
            throw new UnauthorizedAccessException("The exact shared-mailbox Inbox read test failed.");
        }
        using var negative = await SendAsync(
            new Uri(httpClient.BaseAddress!,
                $"users/{Uri.EscapeDataString(invocation.NonTargetMailboxIdentity)}/mailFolders/inbox/messages?$top=1&$select=id"),
            token.AccessToken,
            cancellationToken);
        if (negative.StatusCode != HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException(
                "The known non-target mailbox probe must return 403; readable and not-found results do not prove folder scope.");
        }
        return
        [
            new("graph_exact_inbox_read", true, "allowed"),
            new("graph_non_target_mailbox_read", true, "denied"),
            new("graph_outlook_mutation", true, "not_requested")
        ];
    }

    public async Task<CleanBaselineGraphBaseline> AcquireBaselineAsync(
        CancellationToken cancellationToken)
    {
        var token = await GetGraphTokenAsync(cancellationToken);
        var next = InitialDeltaUri(invocation.MailboxIdentity, invocation.InboxFolderIdentity);
        Uri? delta = null;
        var pages = 0;
        while (next is not null)
        {
            if (++pages > 10_000)
            {
                throw new InvalidOperationException("Graph delta pagination exceeded the fail-closed bound.");
            }
            using var response = await SendAsync(next, token.AccessToken, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Graph baseline request failed with {(int)response.StatusCode}.",
                    inner: null,
                    response.StatusCode);
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            next = ReadLink(document.RootElement, "@odata.nextLink");
            delta = ReadLink(document.RootElement, "@odata.deltaLink") ?? delta;
            if (next is null && delta is null)
            {
                throw new InvalidDataException("Graph ended baseline pagination without a delta link.");
            }
        }
        var cursor = JsonSerializer.Serialize(new
        {
            version = 1,
            pageUri = delta,
            skipCount = 0
        });
        return new(cursor, IntakeCleanBaselineService.Sha256(cursor));
    }

    private Uri InitialDeltaUri(string mailbox, string folder) => new(
        httpClient.BaseAddress!,
        $"users/{Uri.EscapeDataString(mailbox)}/mailFolders/{Uri.EscapeDataString(folder)}" +
        "/messages/delta?$select=id&$top=100");

    private async Task<(string AccessToken, string? IdToken, IReadOnlyList<string> Scopes)> GetGraphTokenAsync(
        CancellationToken cancellationToken)
    {
        if (session is null)
        {
            return ("fixture-token", null, ["Mail.Read.Shared"]);
        }
        var signedIn = await session.SignInAsync(cancellationToken);
        var accessToken = await session.GetTokenAsync(
            ["https://graph.microsoft.com/Mail.Read.Shared"],
            cancellationToken);
        return (accessToken, null, signedIn.Scopes);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Uri uri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        ValidateGraphUri(uri);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private Uri? ReadLink(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var uri = new Uri(value.GetString()!, UriKind.Absolute);
        ValidateGraphUri(uri);
        return uri;
    }

    private void ValidateGraphUri(Uri uri)
    {
        var expected = httpClient.BaseAddress
            ?? throw new InvalidOperationException("Graph BaseAddress is required.");
        var mailbox = Uri.EscapeDataString(invocation.MailboxIdentity);
        var folder = Uri.EscapeDataString(invocation.InboxFolderIdentity);
        var targetPrefix = $"/v1.0/users/{mailbox}/mailFolders/{folder}/messages/delta";
        var negativePrefix = $"/v1.0/users/{Uri.EscapeDataString(invocation.NonTargetMailboxIdentity)}/mailFolders/inbox/messages";
        if (!uri.Scheme.Equals(expected.Scheme, StringComparison.OrdinalIgnoreCase)
            || !uri.Host.Equals(expected.Host, StringComparison.OrdinalIgnoreCase)
            || (uri.IsDefaultPort ? -1 : uri.Port) != (expected.IsDefaultPort ? -1 : expected.Port)
            || (!uri.AbsolutePath.Equals(targetPrefix, StringComparison.OrdinalIgnoreCase)
                && !uri.AbsolutePath.Equals(negativePrefix, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException("Graph request escaped the exact mailbox validation boundary.");
        }
    }
}

internal static class CleanBaselineAccessEvidenceValidator
{
    internal static readonly Guid BlobContributor = Guid.Parse("ba92f5b4-2d11-453d-a403-e96b0029c9fe");
    internal static readonly Guid QueueContributor = Guid.Parse("974c5e8b-45b9-4653-ba55-5f855dd0fb88");
    private static readonly Dictionary<Guid, string> RequiredRoles =
        new Dictionary<Guid, string>
        {
            [BlobContributor] = "Storage Blob Data Contributor",
            [QueueContributor] = "Storage Queue Data Contributor"
        };
    private static readonly Dictionary<Guid, string> RequiredDelegatedPermissions =
        new Dictionary<Guid, string>
        {
            [Guid.Parse("00000003-0000-0000-c000-000000000000")] = "Mail.Read.Shared",
            [Guid.Parse("022907d3-0f1b-48f7-badc-1ba6abab6d66")] = "user_impersonation",
            [Guid.Parse("e406a681-f3d4-42a8-90b6-c2b029497af1")] = "user_impersonation"
        };

    internal static CleanBaselineAccessEvidence Load(
        ProductionIntakeCleanBaselineInvocation invocation,
        TimeProvider timeProvider)
    {
        var path = Path.GetFullPath(invocation.AccessEvidencePath);
        RequireIgnoredOperationPath(path);
        var bytes = File.ReadAllBytes(path);
        var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!actualHash.Equals(invocation.AccessEvidenceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The role-evidence SHA-256 does not match the approved artifact.");
        }
        var evidence = JsonSerializer.Deserialize(
            bytes,
            CleanBaselineJsonContext.Default.CleanBaselineAccessEvidence)
            ?? throw new InvalidDataException("The role evidence is empty.");
        Validate(invocation, evidence, timeProvider.GetUtcNow());
        return evidence;
    }

    internal static void Validate(
        ProductionIntakeCleanBaselineInvocation invocation,
        CleanBaselineAccessEvidence evidence,
        DateTimeOffset now)
    {
        var age = now - evidence.CapturedAtUtc;
        if (evidence.SchemaVersion != 1
            || evidence.TenantId != invocation.TenantId
            || evidence.SubscriptionId != invocation.SubscriptionId
            || evidence.OperatorObjectId == Guid.Empty
            || evidence.PublicClientId != invocation.PublicClientId
            || !evidence.OperatorUpn.Equals(invocation.OperatorUpn, StringComparison.OrdinalIgnoreCase)
            || !evidence.ResourceGroup.Equals(invocation.ResourceGroup, StringComparison.Ordinal)
            || !evidence.SqlServer.Equals(invocation.SqlServer, StringComparison.OrdinalIgnoreCase)
            || !evidence.SqlDatabase.Equals(invocation.SqlDatabase, StringComparison.Ordinal)
            || !evidence.StorageAccount.Equals(invocation.StorageAccount, StringComparison.Ordinal)
            || age < TimeSpan.FromMinutes(-5)
            || age > TimeSpan.FromHours(4))
        {
            throw new UnauthorizedAccessException(
                "The role evidence is stale or bound to the wrong operator, application, tenant, subscription, or resource target.");
        }
        ValidatePublicClient(evidence.PublicClient);
        if (!evidence.Mailbox.MailboxIdentity.Equals(invocation.MailboxIdentity, StringComparison.OrdinalIgnoreCase)
            || !evidence.Mailbox.InboxFolderIdentity.Equals(invocation.InboxFolderIdentity, StringComparison.Ordinal)
            || !evidence.Mailbox.NonTargetMailboxIdentity.Equals(
                invocation.NonTargetMailboxIdentity,
                StringComparison.OrdinalIgnoreCase)
            || !evidence.Mailbox.AccessRights.Equals("Reviewer", StringComparison.Ordinal)
            || evidence.Mailbox.CanSendAs
            || evidence.Mailbox.CanSendOnBehalf
            || evidence.Mailbox.CanDeleteItems)
        {
            throw new UnauthorizedAccessException(
                "The Exchange evidence is not the exact read-only Inbox Reviewer grant or exposes send/delete capability.");
        }
        if (evidence.DirectoryRoles.Count != 0)
        {
            throw new UnauthorizedAccessException(
                "The operator has an unexpected tenant directory role; tenant-wide administration is prohibited.");
        }
        ValidateSqlRoles(evidence.SqlRoles);
        ValidateStorageRoles(invocation, evidence);
    }

    internal static void ValidateSqlRoles(IReadOnlyList<string> roles)
    {
        var normalized = roles.Select(value => value.ToLowerInvariant()).ToArray();
        if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length
            || !normalized.Contains("db_datareader", StringComparer.Ordinal)
            || !normalized.Contains("db_datawriter", StringComparer.Ordinal)
            || normalized.Any(value => value is not ("public" or "db_datareader" or "db_datawriter")))
        {
            throw new UnauthorizedAccessException(
                "SQL role census must contain only public, db_datareader, and db_datawriter; missing, duplicate, db_owner, or unknown roles are prohibited.");
        }
    }

    private static void ValidatePublicClient(CleanBaselinePublicClientEvidence client)
    {
        var permissions = client.DelegatedPermissions
            .Select(item => (item.ResourceApplicationId, item.Permission))
            .ToArray();
        if (!client.IsPublicClient
            || client.PasswordCredentialCount != 0
            || client.KeyCredentialCount != 0
            || permissions.Distinct().Count() != permissions.Length
            || permissions.Length != RequiredDelegatedPermissions.Count
            || RequiredDelegatedPermissions.Any(required =>
                !permissions.Contains((required.Key, required.Value))))
        {
            throw new UnauthorizedAccessException(
                "The application must be a credential-free public client with only the exact delegated Graph, SQL, and Storage permissions.");
        }
    }

    private static void ValidateStorageRoles(
        ProductionIntakeCleanBaselineInvocation invocation,
        CleanBaselineAccessEvidence evidence)
    {
        var exactScope = $"/subscriptions/{invocation.SubscriptionId:D}/resourceGroups/{invocation.ResourceGroup}" +
            $"/providers/Microsoft.Storage/storageAccounts/{invocation.StorageAccount}";
        if (evidence.RoleDefinitions.Select(item => item.RoleDefinitionId).Distinct().Count()
                != evidence.RoleDefinitions.Count
            || evidence.RoleDefinitions.Count != RequiredRoles.Count
            || evidence.RoleAssignments.Select(item =>
                    (item.PrincipalId, item.RoleDefinitionId, item.Scope.ToLowerInvariant()))
                .Distinct().Count() != evidence.RoleAssignments.Count
            || evidence.RoleAssignments.Count != RequiredRoles.Count)
        {
            throw new UnauthorizedAccessException(
                "The Azure role census contains missing, duplicate, or unknown assignments or definitions.");
        }
        foreach (var definition in evidence.RoleDefinitions)
        {
            if (!RequiredRoles.TryGetValue(definition.RoleDefinitionId, out var requiredName)
                || !definition.RoleName.Equals(requiredName, StringComparison.Ordinal)
                || definition.Actions.Concat(definition.DataActions).Any(action =>
                    action.Contains("listKeys", StringComparison.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException(
                    "The Azure role census contains a prohibited, unknown, renamed, or storage-key-capable definition.");
            }
        }
        foreach (var assignment in evidence.RoleAssignments)
        {
            var id = Guid.Parse(assignment.RoleDefinitionId.TrimEnd('/').Split('/').Last());
            if (assignment.PrincipalId != evidence.OperatorObjectId
                || !RequiredRoles.TryGetValue(id, out var requiredName)
                || !assignment.RoleName.Equals(requiredName, StringComparison.Ordinal)
                || !assignment.Scope.Equals(exactScope, StringComparison.OrdinalIgnoreCase)
                || assignment.Inherited
                || !assignment.PrincipalKind.Equals("User", StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "Storage roles must be the two direct built-in data-contributor assignments on the exact account; inherited or broader roles are prohibited.");
            }
        }
    }

    private static void RequireIgnoredOperationPath(string path)
    {
        DirectoryInfo? repositoryRoot = new(
            Path.GetDirectoryName(typeof(CleanBaselineAccessEvidenceValidator).Assembly.Location)!);
        while (repositoryRoot is not null
            && !File.Exists(Path.Combine(repositoryRoot.FullName, "Pegasus.slnx")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }
        if (repositoryRoot is null)
        {
            throw new InvalidOperationException(
                "The repository root could not be resolved for role-evidence path validation.");
        }
        var approvedDirectory = Path.GetFullPath(Path.Combine(
            repositoryRoot.FullName,
            "artifacts",
            "operations",
            "intake-clean-baseline")) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(approvedDirectory, StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Role evidence must be a JSON artifact under this repository's ignored artifacts/operations/intake-clean-baseline directory.");
        }
    }
}

internal sealed class CleanBaselineAccessValidator(
    ProductionIntakeCleanBaselineInvocation invocation,
    CleanBaselineAccessEvidence evidence,
    NamedOperatorTokenSession session,
    CleanBaselineSqlStore sql,
    CleanBaselineGraphClient graph,
    BlobContainerClient blobContainer,
    IReadOnlyDictionary<string, QueueClient> queues) : ICleanBaselineAccessValidator
{
    public async Task<CleanBaselineAccessReport> ValidateAsync(CancellationToken cancellationToken)
    {
        var operatorToken = await session.SignInAsync(cancellationToken);
        if (operatorToken.ObjectId != evidence.OperatorObjectId)
        {
            throw new UnauthorizedAccessException(
                "The interactive operator object ID does not match the approved administrative role evidence.");
        }
        var graphCapabilities = await graph.ValidateScopeAsync(cancellationToken);
        var sqlRoles = await sql.ReadEffectiveRolesAsync(cancellationToken);
        CleanBaselineAccessEvidenceValidator.ValidateSqlRoles(sqlRoles);
        if (!sqlRoles.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(evidence.SqlRoles))
        {
            throw new UnauthorizedAccessException("The live SQL role census drifted from the approved evidence.");
        }

        await blobContainer.GetPropertiesAsync(cancellationToken: cancellationToken);
        foreach (var queueName in CleanBaselineQueueStore.QueueNames)
        {
            await queues[queueName].GetPropertiesAsync(cancellationToken);
        }
        await CleanBaselineStorageCapabilityProbe.ValidateAsync(
            blobContainer,
            queues,
            cancellationToken);
        var capabilities = graphCapabilities.Concat(
        [
            new("sql_exact_database_read", true, "allowed"),
            new("sql_manifest_bound_write", true, "role_present"),
            new("blob_exact_container_read", true, "allowed"),
            new("blob_delete_permission", true, "non_mutating_missing_object_probe"),
            new("queue_exact_queues_read", true, "allowed"),
            new("queue_delete_permission", true, "non_mutating_missing_message_probe")
        ]).ToArray();
        return new(
            1,
            invocation.TenantId,
            invocation.OperatorUpn,
            invocation.PublicClientId,
            operatorToken.ObjectId,
            invocation.SubscriptionId.ToString("D"),
            invocation.ResourceGroup,
            invocation.SqlServer,
            invocation.SqlDatabase,
            invocation.StorageAccount,
            invocation.BlobContainer,
            invocation.MailboxIdentity,
            invocation.InboxFolderIdentity,
            invocation.NonTargetMailboxIdentity,
            invocation.AccessEvidenceSha256.ToLowerInvariant(),
            evidence.RoleAssignments,
            sqlRoles,
            capabilities,
            "validated");
    }
}

internal static class CleanBaselineStorageCapabilityProbe
{
    internal static async Task ValidateAsync(
        BlobContainerClient blobContainer,
        IReadOnlyDictionary<string, QueueClient> queues,
        CancellationToken cancellationToken)
    {
        var missingBlob = blobContainer.GetBlobClient(
            $"__pegasus_missing_permission_probe__/{Guid.NewGuid():N}");
        try
        {
            var response = await missingBlob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                new BlobRequestConditions { IfMatch = new ETag("\"0x000000000000000\"") },
                cancellationToken);
            if (response.Value)
            {
                throw new InvalidOperationException(
                    "The non-mutating Blob permission probe unexpectedly deleted an object.");
            }
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 412)
        {
            // Authorization succeeded; the deliberately absent/non-matching target was not mutated.
        }

        var missingMessageId = Guid.NewGuid().ToString("D");
        var impossiblePopReceipt = Convert.ToBase64String(new byte[32]);
        foreach (var queueName in CleanBaselineQueueStore.QueueNames)
        {
            try
            {
                await queues[queueName].DeleteMessageAsync(
                    missingMessageId,
                    impossiblePopReceipt,
                    cancellationToken);
                throw new InvalidOperationException(
                    "The non-mutating Queue permission probe unexpectedly deleted a message.");
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                // Authorization succeeded; the deliberately absent message was not mutated.
            }
        }
    }
}
