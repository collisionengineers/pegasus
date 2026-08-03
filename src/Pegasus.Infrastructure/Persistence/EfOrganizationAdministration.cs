using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfOrganizationAdministration(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IOrganizationAdministrationStore,
      IOrganizationAdministrationQueries
{
    private const string CreateOrganizationKind = "create_organization";
    private const string UpdateOrganizationRolesKind = "update_organization_roles";
    private const string CreatePrincipalKind = "create_principal";
    private const string ReplacePrincipalKind = "replace_principal";
    private const string PolicyVersion = "organization-principal-administration/v1";
    private const int MaximumProjectedPrincipals = 100;
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<PegasusDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<Organization> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteWithConcurrencyRetryAsync(
            token => CreateOrganizationOnceAsync(request, token),
            cancellationToken);

    public Task<Organization> UpdateOrganizationRolesAsync(
        UpdateOrganizationRolesRequest request,
        CancellationToken cancellationToken) =>
        ExecuteWithConcurrencyRetryAsync(
            token => UpdateOrganizationRolesOnceAsync(request, token),
            cancellationToken);

    public Task<Principal> CreatePrincipalAsync(
        CreatePrincipalRequest request,
        CancellationToken cancellationToken) =>
        ExecuteWithConcurrencyRetryAsync(
            token => CreatePrincipalOnceAsync(request, token),
            cancellationToken);

    public Task<Principal> ReplacePrincipalAsync(
        ReplacePrincipalRequest request,
        CancellationToken cancellationToken) =>
        ExecuteWithConcurrencyRetryAsync(
            token => ReplacePrincipalOnceAsync(request, token),
            cancellationToken);

    private async Task<Organization> CreateOrganizationOnceAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = HashRequest(new
        {
            command = CreateOrganizationKind,
            actor = ActorMaterial(request.Actor),
            request.Name,
            roles = RoleNames(request.Roles)
        });

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await FindReceiptAsync(context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            var replay = ReadReplay<Organization>(receipt, CreateOrganizationKind, requestHash);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var normalizedName = request.Name.ToUpperInvariant();
        var nameAlreadyExists = await context.Organizations
            .AsNoTracking()
            .AnyAsync(
                item => item.NormalizedName == normalizedName,
                cancellationToken);
        OrganizationAdministrationPolicy.RequireUniqueOrganizationName(
            nameAlreadyExists);

        var entity = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Version = 0,
            Roles = request.Roles.Select(role => new OrganizationRoleEntity
            {
                Role = ToCode(role)
            }).ToList()
        };
        var result = ToOrganization(entity);
        var now = _timeProvider.GetUtcNow();
        context.Organizations.Add(entity);
        AddReceipt(
            context,
            request.OperationKey,
            CreateOrganizationKind,
            requestHash,
            result,
            now);
        AddHistory(
            context,
            "organization",
            result.Id,
            "organization_created",
            request.Actor,
            request.OperationKey,
            now,
            reason: null,
            before: null,
            after: result);
        await SaveChangesAsync(context, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<Organization> UpdateOrganizationRolesOnceAsync(
        UpdateOrganizationRolesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = HashRequest(new
        {
            command = UpdateOrganizationRolesKind,
            actor = ActorMaterial(request.Actor),
            request.OrganizationId,
            request.ExpectedVersion,
            roles = RoleNames(request.Roles),
            request.Reason
        });

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await FindReceiptAsync(context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            var replay = ReadReplay<Organization>(receipt, UpdateOrganizationRolesKind, requestHash);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var entity = await context.Organizations
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.Id == request.OrganizationId, cancellationToken)
            ?? throw Error(OrganizationAdministrationError.OrganizationNotFound);
        var before = ToOrganization(entity);
        var hasActivePrincipals = await context.Principals
            .AsNoTracking()
            .AnyAsync(
                item => item.OrganizationId == entity.Id && item.IsActive,
                cancellationToken);
        var after = OrganizationAdministrationPolicy.PlanRoleUpdate(
            before,
            request.ExpectedVersion,
            request.Roles,
            hasActivePrincipals);
        var requestedRoleCodes = after.Roles
            .Select(ToCode)
            .ToHashSet(StringComparer.Ordinal);

        var rolesToRemove = entity.Roles
            .Where(role => !requestedRoleCodes.Contains(role.Role))
            .ToArray();
        if (rolesToRemove.Length > 0)
        {
            context.OrganizationRoles.RemoveRange(rolesToRemove);
        }
        var currentRoleCodes = entity.Roles.Select(role => role.Role).ToHashSet(StringComparer.Ordinal);
        foreach (var roleCode in requestedRoleCodes.Where(role => !currentRoleCodes.Contains(role)))
        {
            entity.Roles.Add(new OrganizationRoleEntity
            {
                OrganizationId = entity.Id,
                Role = roleCode
            });
        }
        foreach (var removed in rolesToRemove)
        {
            entity.Roles.Remove(removed);
        }
        entity.Version = after.Version;
        var now = _timeProvider.GetUtcNow();
        AddReceipt(
            context,
            request.OperationKey,
            UpdateOrganizationRolesKind,
            requestHash,
            after,
            now);
        AddHistory(
            context,
            "organization",
            entity.Id,
            "organization_roles_updated",
            request.Actor,
            request.OperationKey,
            now,
            request.Reason,
            before,
            after);
        await SaveChangesAsync(context, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return after;
    }

    private async Task<Principal> CreatePrincipalOnceAsync(
        CreatePrincipalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = HashRequest(new
        {
            command = CreatePrincipalKind,
            actor = ActorMaterial(request.Actor),
            request.OrganizationId,
            request.Code,
            inspectionMode = ProviderInspectionModePolicy.ToCode(request.InspectionMode)
        });

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await FindReceiptAsync(context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            var replay = ReadReplay<Principal>(receipt, CreatePrincipalKind, requestHash);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var organization = await context.Organizations
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.Id == request.OrganizationId, cancellationToken)
            ?? throw Error(OrganizationAdministrationError.OrganizationNotFound);
        var codeAlreadyExists = await context.Principals
            .AsNoTracking()
            .AnyAsync(
                item => item.Code == request.Code,
                cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var lineageId = Guid.NewGuid();
        var result = OrganizationAdministrationPolicy.PlanPrincipalCreation(
            Guid.NewGuid(),
            lineageId,
            ToOrganization(organization),
            request.Code,
            codeAlreadyExists,
            request.InspectionMode);
        var lineage = new PrincipalSequenceLineageEntity
        {
            Id = lineageId,
            CreatedAtUtc = now
        };
        var entity = new PrincipalEntity
        {
            Id = result.Id,
            OrganizationId = result.OrganizationId,
            Code = result.Code,
            SequenceLineageId = result.SequenceLineageId,
            PredecessorId = result.PredecessorId,
            SuccessorId = result.SuccessorId,
            IsActive = result.IsActive,
            InspectionMode = ProviderInspectionModePolicy.ToCode(result.InspectionMode),
            Version = result.Version
        };
        context.PrincipalSequenceLineages.Add(lineage);
        context.Principals.Add(entity);
        AddReceipt(
            context,
            request.OperationKey,
            CreatePrincipalKind,
            requestHash,
            result,
            now);
        AddHistory(
            context,
            "principal",
            entity.Id,
            "principal_created",
            request.Actor,
            request.OperationKey,
            now,
            reason: null,
            before: null,
            after: result);
        await SaveChangesAsync(context, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<Principal> ReplacePrincipalOnceAsync(
        ReplacePrincipalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = HashRequest(new
        {
            command = ReplacePrincipalKind,
            actor = ActorMaterial(request.Actor),
            request.PrincipalId,
            request.ExpectedVersion,
            request.SuccessorOrganizationId,
            request.SuccessorCode,
            request.Reason
        });

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await FindReceiptAsync(context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            var replay = ReadReplay<Principal>(receipt, ReplacePrincipalKind, requestHash);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var predecessor = await context.Principals
            .SingleOrDefaultAsync(item => item.Id == request.PrincipalId, cancellationToken)
            ?? throw Error(OrganizationAdministrationError.PrincipalNotFound);
        var before = ToPrincipal(predecessor);
        var successorOrganization = await context.Organizations
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(
                item => item.Id == request.SuccessorOrganizationId,
                cancellationToken)
            ?? throw Error(OrganizationAdministrationError.OrganizationNotFound);
        var codeAlreadyExists = await context.Principals
            .AsNoTracking()
            .AnyAsync(
                item => item.Code == request.SuccessorCode,
                cancellationToken);
        var replacement = OrganizationAdministrationPolicy.PlanPrincipalReplacement(
            before,
            request.ExpectedVersion,
            ToOrganization(successorOrganization),
            Guid.NewGuid(),
            request.SuccessorCode,
            codeAlreadyExists);
        predecessor.IsActive = replacement.Predecessor.IsActive;
        predecessor.SuccessorId = replacement.Predecessor.SuccessorId;
        predecessor.Version = replacement.Predecessor.Version;
        var result = replacement.Successor;
        var successor = new PrincipalEntity
        {
            Id = result.Id,
            OrganizationId = result.OrganizationId,
            Code = result.Code,
            SequenceLineageId = result.SequenceLineageId,
            PredecessorId = result.PredecessorId,
            SuccessorId = result.SuccessorId,
            IsActive = result.IsActive,
            InspectionMode = ProviderInspectionModePolicy.ToCode(result.InspectionMode),
            Version = result.Version
        };
        var predecessorAfter = replacement.Predecessor;
        var now = _timeProvider.GetUtcNow();
        context.Principals.Add(successor);
        AddReceipt(
            context,
            request.OperationKey,
            ReplacePrincipalKind,
            requestHash,
            result,
            now);
        AddHistory(
            context,
            "principal",
            predecessor.Id,
            "principal_replaced",
            request.Actor,
            request.OperationKey,
            now,
            request.Reason,
            before,
            predecessorAfter);
        AddHistory(
            context,
            "principal",
            successor.Id,
            "principal_created_as_successor",
            request.Actor,
            request.OperationKey,
            now,
            request.Reason,
            before: null,
            after: result);
        await SaveChangesAsync(context, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<OrganizationQuerySlice> ListAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        if (offset < 0 || limit < 1 || limit > ListOrganizations.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Organizations
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip(offset)
            .Take(limit + 1)
            .Select(item => new OrganizationProjection(
                item.Id,
                item.Name,
                item.Version,
                item.Roles
                    .OrderBy(role => role.Role)
                    .Select(role => role.Role)
                    .ToArray(),
                item.Principals
                    .OrderBy(principal => principal.Code)
                    .ThenBy(principal => principal.Id)
                    .Take(MaximumProjectedPrincipals + 1)
                    .Select(principal => new PrincipalProjection(
                        principal.Id,
                        principal.OrganizationId,
                        principal.Code,
                        principal.SequenceLineageId,
                        principal.PredecessorId,
                        principal.SuccessorId,
                        principal.IsActive,
                        principal.Version,
                        principal.Cases.Count,
                        principal.InspectionMode))
                    .ToArray()))
            .ToArrayAsync(cancellationToken);

        var hasMore = rows.Length > limit;
        return new(
            rows.Take(limit).Select(ToListItem).ToArray(),
            hasMore);
    }

    public async Task<OrganizationDetails?> GetAsync(
        Guid organizationId,
        int principalLimit,
        Guid? requiredPrincipalId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "An organization identifier is required.",
                nameof(organizationId));
        }
        if (requiredPrincipalId == Guid.Empty)
        {
            throw new ArgumentException(
                "A required principal identifier cannot be empty.",
                nameof(requiredPrincipalId));
        }
        if (principalLimit < 1 || principalLimit > MaximumProjectedPrincipals)
        {
            throw new ArgumentOutOfRangeException(nameof(principalLimit));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.Organizations
            .AsNoTracking()
            .AsSplitQuery()
            .Where(item => item.Id == organizationId)
            .Select(item => new OrganizationProjection(
                item.Id,
                item.Name,
                item.Version,
                item.Roles
                    .OrderBy(role => role.Role)
                    .Select(role => role.Role)
                    .ToArray(),
                item.Principals
                    .Where(principal =>
                        requiredPrincipalId == null
                        || (Guid?)principal.Id == requiredPrincipalId)
                    .OrderBy(principal => principal.Code)
                    .ThenBy(principal => principal.Id)
                    .Take(principalLimit + 1)
                    .Select(principal => new PrincipalProjection(
                        principal.Id,
                        principal.OrganizationId,
                        principal.Code,
                        principal.SequenceLineageId,
                        principal.PredecessorId,
                        principal.SuccessorId,
                        principal.IsActive,
                        principal.Version,
                        principal.Cases.Count,
                        principal.InspectionMode))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        return new(
            row.Id,
            row.Name,
            ParseRoles(row.Roles),
            row.Version,
            row.Principals.Take(principalLimit).Select(ToSummary).ToArray(),
            row.Principals.Length > principalLimit);
    }

    private static OrganizationListItem ToListItem(OrganizationProjection row) =>
        new(
            row.Id,
            row.Name,
            ParseRoles(row.Roles),
            row.Version,
            row.Principals.Take(MaximumProjectedPrincipals).Select(ToSummary).ToArray(),
            row.Principals.Length > MaximumProjectedPrincipals);

    private static PrincipalAdministrationSummary ToSummary(PrincipalProjection row) =>
        new(
            row.Id,
            row.OrganizationId,
            row.Code,
            row.SequenceLineageId,
            row.PredecessorId,
            row.SuccessorId,
            row.IsActive,
            row.Version,
            row.AllocatedCaseCount,
            ProviderInspectionModePolicy.Parse(row.InspectionMode));

    private static Organization ToOrganization(OrganizationEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Roles.Select(role => ParseRole(role.Role)).OrderBy(role => role).ToArray(),
            entity.Version);

    private static Principal ToPrincipal(PrincipalEntity entity) =>
        new(
            entity.Id,
            entity.OrganizationId,
            entity.Code,
            entity.SequenceLineageId,
            entity.PredecessorId,
            entity.SuccessorId,
            entity.IsActive,
            entity.Version,
            ProviderInspectionModePolicy.Parse(entity.InspectionMode));

    private static OrganizationRole[] ParseRoles(IEnumerable<string> roles) =>
        roles.Select(ParseRole).OrderBy(role => role).ToArray();

    private static OrganizationRole ParseRole(string role) => role switch
    {
        "work_provider" => OrganizationRole.WorkProvider,
        "instruction_intermediary" => OrganizationRole.InstructionIntermediary,
        _ => throw new InvalidOperationException("The persisted organization role is invalid.")
    };

    private static string ToCode(OrganizationRole role) => role switch
    {
        OrganizationRole.WorkProvider => "work_provider",
        OrganizationRole.InstructionIntermediary => "instruction_intermediary",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static string[] RoleNames(IEnumerable<OrganizationRole> roles) =>
        roles.OrderBy(role => role).Select(role => role.ToString()).ToArray();

    private static Task<OrganizationAdministrationOperationEntity?> FindReceiptAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.OrganizationAdministrationOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);

    private static T ReadReplay<T>(
        OrganizationAdministrationOperationEntity receipt,
        string commandKind,
        string requestHash)
    {
        if (!string.Equals(receipt.CommandKind, commandKind, StringComparison.Ordinal)
            || !SameHash(receipt.RequestHash, requestHash))
        {
            throw Error(OrganizationAdministrationError.OperationConflict);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(receipt.ResultJson, SerializerOptions)
                ?? throw Error(OrganizationAdministrationError.OperationConflict);
        }
        catch (JsonException)
        {
            throw Error(OrganizationAdministrationError.OperationConflict);
        }
    }

    private static bool SameHash(string left, string right)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(left),
                Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void AddReceipt<T>(
        PegasusDbContext context,
        string operationKey,
        string commandKind,
        string requestHash,
        T result,
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
        string aggregateType,
        Guid aggregateId,
        string eventKind,
        ActionActor actor,
        string operationKey,
        DateTimeOffset occurredAtUtc,
        string? reason,
        object? before,
        object after) =>
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = aggregateType,
            AggregateId = aggregateId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role).Select(role => role.ToString()),
                SerializerOptions),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = before is null ? null : SerializeObject(before),
            AfterJson = SerializeObject(after),
            PolicyVersion = PolicyVersion
        });

    private static string SerializeObject(object value) =>
        JsonSerializer.Serialize(value, value.GetType(), SerializerOptions);

    private static ActorRequestMaterial ActorMaterial(ActionActor actor) =>
        new(
            actor.Kind.ToString(),
            actor.SubjectId,
            actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray());

    private static string HashRequest<T>(T material) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(material, SerializerOptions))))
            .ToLowerInvariant();

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
                await Task.Delay(
                    TimeSpan.FromMilliseconds(25 * attempt),
                    cancellationToken);
            }
        }

        throw new UnreachableException();
    }

    private static bool IsRetryableConcurrencyFailure(Exception exception) =>
        exception switch
        {
            OrganizationAdministrationException
            {
                Error:
                    OrganizationAdministrationError.DuplicateOrganizationName
                    or OrganizationAdministrationError.DuplicatePrincipalCode
                    or OrganizationAdministrationError.StaleVersion
            } => true,
            SqlException { Number: 1205 or 2601 or 2627 } => true,
            DbUpdateException { InnerException: { } innerException } =>
                IsRetryableConcurrencyFailure(innerException),
            _ when exception.InnerException is not null =>
                IsRetryableConcurrencyFailure(exception.InnerException),
            _ => false
        };

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
            throw Error(OrganizationAdministrationError.StaleVersion);
        }
        catch (DbUpdateException exception) when (
            TryMapUniqueConstraint(exception, out var error))
        {
            throw Error(error);
        }
    }

    private static bool TryMapUniqueConstraint(
        DbUpdateException exception,
        out OrganizationAdministrationError error)
    {
        if (exception.GetBaseException() is not SqlException { Number: 2601 or 2627 } sqlException)
        {
            error = default;
            return false;
        }

        if (sqlException.Message.Contains(
                "IX_Organizations_NormalizedName",
                StringComparison.OrdinalIgnoreCase))
        {
            error = OrganizationAdministrationError.DuplicateOrganizationName;
            return true;
        }
        if (sqlException.Message.Contains(
                "IX_Principals_Code",
                StringComparison.OrdinalIgnoreCase))
        {
            error = OrganizationAdministrationError.DuplicatePrincipalCode;
            return true;
        }

        error = default;
        return false;
    }

    private static OrganizationAdministrationException Error(
        OrganizationAdministrationError error) => new(error);

    private sealed record ActorRequestMaterial(
        string Kind,
        string SubjectId,
        string[] Roles);

    private sealed record OrganizationProjection(
        Guid Id,
        string Name,
        long Version,
        string[] Roles,
        PrincipalProjection[] Principals);

    private sealed record PrincipalProjection(
        Guid Id,
        Guid OrganizationId,
        string Code,
        Guid SequenceLineageId,
        Guid? PredecessorId,
        Guid? SuccessorId,
        bool IsActive,
        long Version,
        int AllocatedCaseCount,
        string InspectionMode);
}
