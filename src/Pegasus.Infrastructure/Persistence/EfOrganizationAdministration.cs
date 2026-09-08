using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfOrganizationAdministration(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IOrganizationAdministrationStore,
      IOrganizationAdministrationQueries
{
    private const string CreatePrincipalKind = "create_principal";
    private const string UpdatePrincipalEvaSubmissionKind = "update_principal_eva_submission";
    private const string UpdatePrincipalDefaultInspectionLocationKind =
        "update_principal_default_inspection_location";
    private const string ReplacePrincipalKind = "replace_principal";
    private const string PolicyVersion = "organization-principal-administration/v1";
    internal static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<PegasusDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

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

    public Task<Principal> UpdatePrincipalEvaSubmissionAsync(
        UpdatePrincipalEvaSubmissionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteWithConcurrencyRetryAsync(
            token => UpdatePrincipalEvaSubmissionOnceAsync(request, token),
            cancellationToken);

    public Task<PrincipalAdministrationSummary> UpdatePrincipalDefaultInspectionLocationAsync(
        UpdatePrincipalDefaultInspectionLocationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteWithConcurrencyRetryAsync(
            token => UpdatePrincipalDefaultInspectionLocationOnceAsync(request, token),
            cancellationToken);

    private async Task<Principal> CreatePrincipalOnceAsync(
        CreatePrincipalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = HashRequest(new
        {
            command = CreatePrincipalKind,
            actor = ActorMaterial(request.Actor),
            request.Name,
            request.Code,
            inspectionMode = ProviderInspectionModePolicy.ToCode(request.InspectionMode),
            request.EvaManualSubmission
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

        var normalizedName = request.Name.ToUpperInvariant();
        OrganizationAdministrationPolicy.RequireUniqueOrganizationName(
            await context.Organizations.AnyAsync(item => item.NormalizedName == normalizedName, cancellationToken));
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Version = 0
        };
        organization.Roles.Add(new OrganizationRoleEntity
        {
            OrganizationId = organization.Id,
            Role = ToCode(OrganizationRole.WorkProvider)
        });
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
            request.InspectionMode,
            request.EvaManualSubmission);
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
            EvaManualSubmission = result.EvaManualSubmission,
            Version = result.Version
        };
        context.PrincipalSequenceLineages.Add(lineage);
        context.Organizations.Add(organization);
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

    /// <summary>
    /// EXT-04: switch a principal's EVA submission settings.
    ///
    /// The only principal attribute that changes in place. Everything else
    /// about a principal is immutable once work has been allocated against it,
    /// and a wrong principal is closed and replaced rather than edited — but a
    /// delivery route is not part of who the work belongs to, and a setting
    /// that could only be chosen at creation could never be switched on for
    /// the principals that already exist.
    ///
    /// It writes the same attributed permanent history every other
    /// administration operation writes, so switching the route on is as
    /// traceable as creating the principal was.
    /// </summary>
    private async Task<Principal> UpdatePrincipalEvaSubmissionOnceAsync(
        UpdatePrincipalEvaSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = HashRequest(new
        {
            command = UpdatePrincipalEvaSubmissionKind,
            actor = ActorMaterial(request.Actor),
            request.PrincipalId,
            request.ExpectedVersion,
            request.EvaManualSubmission,
            request.Reason
        });

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await FindReceiptAsync(context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            var replay = ReadReplay<Principal>(
                receipt,
                UpdatePrincipalEvaSubmissionKind,
                requestHash);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var entity = await context.Principals
            .SingleOrDefaultAsync(item => item.Id == request.PrincipalId, cancellationToken)
            ?? throw Error(OrganizationAdministrationError.PrincipalNotFound);
        var before = ToPrincipal(entity);
        var result = OrganizationAdministrationPolicy.PlanPrincipalEvaSubmissionUpdate(
            before,
            request.ExpectedVersion,
            request.EvaManualSubmission);

        entity.EvaManualSubmission = result.EvaManualSubmission;
        entity.Version = result.Version;

        var now = _timeProvider.GetUtcNow();
        AddReceipt(
            context,
            request.OperationKey,
            UpdatePrincipalEvaSubmissionKind,
            requestHash,
            result,
            now);
        AddHistory(
            context,
            "principal",
            entity.Id,
            "principal_eva_submission_updated",
            request.Actor,
            request.OperationKey,
            now,
            request.Reason,
            before,
            result);
        await SaveChangesAsync(context, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// EXT-18/S05 item 6: the principal's one default inspection-location
    /// choice. Writes only the directory-facing summary columns F/G1 added to
    /// <see cref="PrincipalEntity"/> — the shared <see cref="Principal"/>
    /// record and B's separate CE assessment method are untouched.
    /// </summary>
    private async Task<PrincipalAdministrationSummary> UpdatePrincipalDefaultInspectionLocationOnceAsync(
        UpdatePrincipalDefaultInspectionLocationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = HashRequest(new
        {
            command = UpdatePrincipalDefaultInspectionLocationKind,
            actor = ActorMaterial(request.Actor),
            request.PrincipalId,
            request.ExpectedVersion,
            kind = request.Kind.ToString(),
            request.Label,
            request.Address,
            request.Postcode,
            request.SourceKind,
            request.SourceRecordId,
            request.SourceVersion,
            request.Reason
        });

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await FindReceiptAsync(context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            var replay = ReadReplay<PrincipalAdministrationSummary>(
                receipt,
                UpdatePrincipalDefaultInspectionLocationKind,
                requestHash);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var entity = await context.Principals
            .SingleOrDefaultAsync(item => item.Id == request.PrincipalId, cancellationToken)
            ?? throw Error(OrganizationAdministrationError.PrincipalNotFound);
        if (entity.Version != request.ExpectedVersion)
        {
            throw Error(OrganizationAdministrationError.StaleVersion);
        }
        if (!entity.IsActive)
        {
            throw Error(OrganizationAdministrationError.PrincipalInactive);
        }

        var isImageBased = request.Kind == InspectionAddressEvidenceKind.ImageBasedAssessment;
        var changed = entity.DefaultInspectionLocationLabel != request.Label
            || entity.DefaultInspectionAddress != (isImageBased ? null : request.Address)
            || entity.DefaultInspectionPostcode != request.Postcode
            || entity.DefaultInspectionSourceKind != request.SourceKind
            || entity.DefaultInspectionSourceRecordId != request.SourceRecordId?.ToString("D")
            || entity.DefaultInspectionSourceVersion != request.SourceVersion;

        // The allocated-case count is unaffected by this mutation, so it is
        // computed once and reused for both the before and after snapshots
        // (EXT-18/S05 item 6: a staff override keeps the fact it replaced).
        var allocatedCaseCount = await context.Cases
            .AsNoTracking()
            .CountAsync(item => item.PrincipalId == entity.Id, cancellationToken);
        var before = ToSummary(entity, allocatedCaseCount);

        entity.DefaultInspectionLocationLabel = request.Label;
        entity.DefaultInspectionAddress = isImageBased ? null : request.Address;
        entity.DefaultInspectionPostcode = request.Postcode;
        entity.DefaultInspectionSourceKind = request.SourceKind;
        entity.DefaultInspectionSourceRecordId = request.SourceRecordId?.ToString("D");
        entity.DefaultInspectionSourceVersion = request.SourceVersion;
        entity.Version = changed ? checked(entity.Version + 1) : entity.Version;

        var result = ToSummary(entity, allocatedCaseCount);
        var now = _timeProvider.GetUtcNow();
        AddReceipt(
            context,
            request.OperationKey,
            UpdatePrincipalDefaultInspectionLocationKind,
            requestHash,
            result,
            now);
        AddHistory(
            context,
            "principal",
            entity.Id,
            "principal_default_inspection_location_updated",
            request.Actor,
            request.OperationKey,
            now,
            request.Reason,
            before,
            result);
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
        var codeAlreadyExists = await context.Principals
            .AsNoTracking()
            .AnyAsync(
                item => item.Code == request.SuccessorCode,
                cancellationToken);
        var replacement = OrganizationAdministrationPolicy.PlanPrincipalReplacement(
            before,
            request.ExpectedVersion,
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
            EvaManualSubmission = result.EvaManualSubmission,
            DefaultInspectionLocationLabel = predecessor.DefaultInspectionLocationLabel,
            DefaultInspectionAddress = predecessor.DefaultInspectionAddress,
            DefaultInspectionPostcode = predecessor.DefaultInspectionPostcode,
            DefaultInspectionSourceKind = predecessor.DefaultInspectionSourceKind,
            DefaultInspectionSourceRecordId = predecessor.DefaultInspectionSourceRecordId,
            DefaultInspectionSourceVersion = predecessor.DefaultInspectionSourceVersion,
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

    public async Task<IReadOnlyList<PrincipalAdministrationDetails>> ListPrincipalsAsync(
        int offset, int limit, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Principals.AsNoTracking()
            .OrderBy(item => item.Organization.Name).ThenBy(item => item.Code)
            .Skip(offset).Take(limit)
            .Select(item => new
            {
                Principal = item,
                item.Organization.Name,
                AllocatedCount = context.Cases.Count(caseItem => caseItem.PrincipalId == item.Id)
            })
            .ToArrayAsync(cancellationToken);
        return rows.Select(row => new PrincipalAdministrationDetails(
            row.Name, ToSummary(row.Principal, row.AllocatedCount))).ToArray();
    }

    public async Task<PrincipalAdministrationDetails?> GetPrincipalAsync(
        Guid principalId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.Principals.AsNoTracking()
            .Where(item => item.Id == principalId)
            .Select(item => new
            {
                Principal = item,
                item.Organization.Name,
                AllocatedCount = context.Cases.Count(caseItem => caseItem.PrincipalId == item.Id)
            })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new(row.Name, ToSummary(row.Principal, row.AllocatedCount));
    }

    private static PrincipalAdministrationSummary ToSummary(
        PrincipalEntity entity,
        int allocatedCaseCount) =>
        new(
            entity.Id,
            entity.OrganizationId,
            entity.Code,
            entity.SequenceLineageId,
            entity.PredecessorId,
            entity.SuccessorId,
            entity.IsActive,
            entity.Version,
            allocatedCaseCount,
            ProviderInspectionModePolicy.Parse(entity.InspectionMode),
            entity.EvaManualSubmission,
            entity.DefaultInspectionLocationLabel,
            entity.DefaultInspectionAddress,
            entity.DefaultInspectionPostcode,
            entity.DefaultInspectionSourceKind,
            entity.DefaultInspectionSourceRecordId is { Length: > 0 } sourceRecordId
                ? Guid.Parse(sourceRecordId)
                : null,
            entity.DefaultInspectionSourceVersion);

    private static Organization ToOrganization(OrganizationEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Roles.Select(role => ParseRole(role.Role)).OrderBy(role => role).ToArray(),
            entity.Version);

    internal static Principal ToPrincipal(PrincipalEntity entity) =>
        new(
            entity.Id,
            entity.OrganizationId,
            entity.Code,
            entity.SequenceLineageId,
            entity.PredecessorId,
            entity.SuccessorId,
            entity.IsActive,
            entity.Version,
            ProviderInspectionModePolicy.Parse(entity.InspectionMode),
            entity.EvaManualSubmission);

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

    internal static Task<OrganizationAdministrationOperationEntity?> FindReceiptAsync(
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

    internal static bool SameHash(string left, string right)
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

    internal static void AddReceipt<T>(
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

    internal static void AddHistory(
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

    internal static string HashRequest<T>(T material) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(material, SerializerOptions))))
            .ToLowerInvariant();

    private static Task<T> ExecuteWithConcurrencyRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken) =>
        ExecuteWithConcurrencyRetryAsync(operation, IsRetryableConcurrencyFailure, cancellationToken);

    /// <summary>
    /// Three attempts, 25 ms × attempt apart, for the failures the caller
    /// names as concurrency races; shared with the principal credential store.
    /// </summary>
    internal static async Task<T> ExecuteWithConcurrencyRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, bool> isRetryable,
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
                && isRetryable(exception))
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

}
