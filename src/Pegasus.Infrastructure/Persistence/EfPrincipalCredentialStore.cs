using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// API-04 persistence. The verifier is the same PBKDF2
/// <see cref="PasswordHasher{TUser}"/> that hashes staff passwords; every
/// command runs in a Serializable transaction, replays through the shared
/// administration operation receipts (the receipt carries the record, never
/// the secret), and writes permanent <c>principal_credential_*</c> action
/// history whose JSON never includes the hash.
/// </summary>
public sealed class EfPrincipalCredentialStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IPrincipalCredentialStore,
      IPrincipalCredentialQueries
{
    private const string AggregateType = "principal_api_credential";
    private const string PolicyVersion = "principal-api-credential/v1";
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);
    private static readonly PasswordHasher<PrincipalApiCredentialEntity> Hasher = new();

    private readonly IDbContextFactory<PegasusDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<PrincipalCredentialIssueResult> IssueAsync(
        PrincipalCredentialCommandRequest request,
        string keyId,
        string secret,
        CancellationToken cancellationToken) =>
        ExecuteWithConcurrencyRetryAsync(
            token => IssueOnceAsync(request, keyId, secret, token),
            cancellationToken);

    public Task<PrincipalCredentialRecord> PauseAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            request,
            "pause_principal_credential",
            "principal_credential_paused",
            (current, now) => PrincipalCredentialPolicy.PlanPause(current, request.ExpectedVersion, now),
            cancellationToken);

    public Task<PrincipalCredentialRecord> ResumeAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            request,
            "resume_principal_credential",
            "principal_credential_resumed",
            (current, _) => PrincipalCredentialPolicy.PlanResume(current, request.ExpectedVersion),
            cancellationToken);

    public Task<PrincipalCredentialRecord> RevokeAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            request,
            "revoke_principal_credential",
            "principal_credential_revoked",
            (current, now) => PrincipalCredentialPolicy.PlanRevoke(current, request.ExpectedVersion, now),
            cancellationToken);

    public async Task<PrincipalCredentialVerification?> VerifySecretAsync(
        string keyId,
        string secret,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.PrincipalApiCredentials
            .AsNoTracking()
            .Where(item => item.KeyId == keyId)
            .Select(item => new { Credential = item, item.Principal.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var verified = Hasher.VerifyHashedPassword(row.Credential, row.Credential.SecretHash, secret);
        return verified == PasswordVerificationResult.Failed
            ? null
            : new(ToRecord(row.Credential), row.IsActive);
    }

    public async Task<PrincipalCredentialRecord?> GetAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.PrincipalApiCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.PrincipalId == principalId, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    private async Task<PrincipalCredentialIssueResult> IssueOnceAsync(
        PrincipalCredentialCommandRequest request,
        string keyId,
        string secret,
        CancellationToken cancellationToken)
    {
        const string commandKind = "issue_principal_credential";
        var requestHash = HashRequest(request, commandKind);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await FindReceiptAsync(context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            return new(ReadReplay(receipt, commandKind, requestHash), true);
        }

        var principalEntity = await context.Principals
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.PrincipalId, cancellationToken)
            ?? throw new PrincipalCredentialException(PrincipalCredentialError.PrincipalNotFound);
        var entity = await context.PrincipalApiCredentials
            .SingleOrDefaultAsync(item => item.PrincipalId == request.PrincipalId, cancellationToken);
        var before = entity is null ? null : ToRecord(entity);
        var now = UtcNow();
        var planned = PrincipalCredentialPolicy.PlanIssue(
            before,
            EfOrganizationAdministration.ToPrincipal(principalEntity),
            request.ExpectedVersion,
            keyId,
            now);
        if (entity is null)
        {
            entity = new PrincipalApiCredentialEntity
            {
                PrincipalId = planned.PrincipalId,
                KeyId = planned.KeyId,
                SecretHash = string.Empty,
                State = planned.State.ToString()
            };
            context.PrincipalApiCredentials.Add(entity);
        }

        Apply(entity, planned);
        entity.SecretHash = Hasher.HashPassword(entity, secret);
        AddHistory(
            context,
            before is null ? "principal_credential_issued" : "principal_credential_reset",
            request,
            now,
            before,
            planned);
        AddReceipt(context, request.OperationKey, commandKind, requestHash, planned, now);
        await SaveChangesAsync(context, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(planned, false);
    }

    private Task<PrincipalCredentialRecord> TransitionAsync(
        PrincipalCredentialCommandRequest request,
        string commandKind,
        string eventKind,
        Func<PrincipalCredentialRecord?, DateTimeOffset, PrincipalCredentialRecord> plan,
        CancellationToken cancellationToken) =>
        ExecuteWithConcurrencyRetryAsync(
            async token =>
            {
                var requestHash = HashRequest(request, commandKind);
                await using var context = await _contextFactory.CreateDbContextAsync(token);
                await using var transaction = await context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    token);
                var receipt = await FindReceiptAsync(context, request.OperationKey, token);
                if (receipt is not null)
                {
                    return ReadReplay(receipt, commandKind, requestHash);
                }

                var entity = await context.PrincipalApiCredentials
                    .SingleOrDefaultAsync(item => item.PrincipalId == request.PrincipalId, token);
                var before = entity is null ? null : ToRecord(entity);
                var now = UtcNow();
                var planned = plan(before, now);
                Apply(entity!, planned);
                AddHistory(context, eventKind, request, now, before, planned);
                AddReceipt(context, request.OperationKey, commandKind, requestHash, planned, now);
                await SaveChangesAsync(context, token);
                await transaction.CommitAsync(token);
                return planned;
            },
            cancellationToken);

    private static void Apply(PrincipalApiCredentialEntity entity, PrincipalCredentialRecord planned)
    {
        entity.KeyId = planned.KeyId;
        entity.State = planned.State.ToString();
        entity.IssuedAtUtc = planned.IssuedAtUtc;
        entity.RotatedAtUtc = planned.RotatedAtUtc;
        entity.PausedAtUtc = planned.PausedAtUtc;
        entity.RevokedAtUtc = planned.RevokedAtUtc;
        entity.Version = planned.Version;
    }

    private static PrincipalCredentialRecord ToRecord(PrincipalApiCredentialEntity entity) =>
        new(
            entity.PrincipalId,
            entity.KeyId,
            Enum.TryParse<PrincipalCredentialState>(entity.State, out var state) && Enum.IsDefined(state)
                ? state
                : throw new InvalidDataException(
                    $"Unknown persisted principal credential state '{entity.State}'."),
            entity.IssuedAtUtc,
            entity.RotatedAtUtc,
            entity.PausedAtUtc,
            entity.RevokedAtUtc,
            entity.Version);

    private static Task<OrganizationAdministrationOperationEntity?> FindReceiptAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.OrganizationAdministrationOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);

    private static PrincipalCredentialRecord ReadReplay(
        OrganizationAdministrationOperationEntity receipt,
        string commandKind,
        string requestHash)
    {
        if (!string.Equals(receipt.CommandKind, commandKind, StringComparison.Ordinal)
            || !string.Equals(receipt.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new PrincipalCredentialException(PrincipalCredentialError.OperationConflict);
        }

        try
        {
            return JsonSerializer.Deserialize<PrincipalCredentialRecord>(receipt.ResultJson, SerializerOptions)
                ?? throw new PrincipalCredentialException(PrincipalCredentialError.OperationConflict);
        }
        catch (JsonException)
        {
            throw new PrincipalCredentialException(PrincipalCredentialError.OperationConflict);
        }
    }

    private static void AddReceipt(
        PegasusDbContext context,
        string operationKey,
        string commandKind,
        string requestHash,
        PrincipalCredentialRecord result,
        DateTimeOffset completedAtUtc) =>
        context.OrganizationAdministrationOperations.Add(new()
        {
            OperationKey = operationKey,
            CommandKind = commandKind,
            RequestHash = requestHash,
            ResultJson = JsonSerializer.Serialize(result, SerializerOptions),
            CompletedAtUtc = completedAtUtc
        });

    private static void AddHistory(
        PegasusDbContext context,
        string eventKind,
        PrincipalCredentialCommandRequest request,
        DateTimeOffset occurredAtUtc,
        PrincipalCredentialRecord? before,
        PrincipalCredentialRecord after) =>
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = request.PrincipalId.ToString("D"),
            EventKind = eventKind,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()),
                SerializerOptions),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = request.OperationKey,
            Reason = request.Reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, SerializerOptions),
            AfterJson = JsonSerializer.Serialize(after, SerializerOptions),
            PolicyVersion = PolicyVersion
        });

    // The generated key id and secret are deliberately outside the hash: a
    // replayed operation key must match on what the administrator asked
    // for, and the secret is never persisted in any form but the verifier.
    private static string HashRequest(PrincipalCredentialCommandRequest request, string commandKind) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        new
                        {
                            Command = commandKind,
                            request.PrincipalId,
                            request.ExpectedVersion,
                            ActorKind = request.Actor.Kind.ToString(),
                            request.Actor.SubjectId,
                            request.OperationKey,
                            request.Reason
                        },
                        SerializerOptions))))
            .ToLowerInvariant();

    private static async Task SaveChangesAsync(
        PegasusDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PrincipalCredentialException(PrincipalCredentialError.StaleVersion);
        }
    }

    private static async Task<T> ExecuteWithConcurrencyRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception exception) when (
                attempt < 3
                && IsRetryableConcurrencyFailure(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new UnreachableException();
    }

    // A deadlock or a unique-key race (two first submissions of one
    // operation key, or two concurrent issues for one Principal) is retried;
    // the retry then finds the receipt or the row and answers from it.
    private static bool IsRetryableConcurrencyFailure(Exception exception) =>
        exception switch
        {
            SqlException { Number: 1205 or 2601 or 2627 } => true,
            _ when exception.InnerException is not null =>
                IsRetryableConcurrencyFailure(exception.InnerException),
            _ => false
        };

    private DateTimeOffset UtcNow()
    {
        var now = _timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }
}
