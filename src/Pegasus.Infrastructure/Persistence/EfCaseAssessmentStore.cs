using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists the assessment surface with exactly the guards and evidence of a
/// staff case save: one serializable transaction, operation-key replay via
/// the case workflow event stream, optimistic case version, the server-owned
/// edit lease, and the same three history records (workflow event, permanent
/// action history with before/after values, case history). An Automation
/// save differs from a staff save only in the stored provenance: its values
/// carry the unconfirmed mark until staff review.
/// </summary>
public sealed class EfCaseAssessmentStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IRepairSpecificationStore repairSpecifications) : ICaseAssessmentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseAssessmentProjection?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        var specificationId = await CurrentSpecificationIdAsync(caseId, cancellationToken);
        var lines = await context.CaseEstimateLines.AsNoTracking()
            .Where(item => item.CaseId == caseId
                && item.RepairSpecificationId == specificationId)
            .OrderBy(item => item.Position)
            .ToArrayAsync(cancellationToken);
        var caseDataFields = await context.CaseDataFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync(cancellationToken);
        return Map(workflow, fields, lines, caseDataFields);
    }

    public async Task<CaseAssessmentProjection> SaveAsync(
        SaveAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request = AssessmentPolicy.ValidateAndNormalize(request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = RequestHash(request);
        var replayed = await FindReplayAsync(context, request, requestHash, cancellationToken);
        if (replayed)
        {
            return await GetRequiredAsync(context, request.CaseId, cancellationToken);
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        RequireVersion(workflow, request.ExpectedVersion);
        var now = UtcNow();
        RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        ArchivedCaseGuard.RequireMutable(workflow);
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            || !AssessmentPolicy.IsWritableState(state))
        {
            throw new InvalidOperationException(
                "The assessment can be saved only on a Not ready, Review, or Report preparation case.");
        }

        if (request.AiWorkRequestId is { } workRequestId)
        {
            var workRequest = await context.AiWorkRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.RequestId == workRequestId, cancellationToken)
                ?? throw new InvalidOperationException(
                    "The referenced Send to AI work request was not found.");
            if (workRequest.CaseId != request.CaseId)
            {
                throw new InvalidOperationException(
                    "The referenced Send to AI work request belongs to another case.");
            }
        }

        var fields = await context.CaseAssessmentFields
            .Where(item => item.CaseId == request.CaseId)
            .ToListAsync(cancellationToken);
        var specification = await EfRepairSpecificationStore.DraftQuery(context, request.CaseId)
            .SingleOrDefaultAsync(cancellationToken);
        if (specification is null && request.EstimateLines is not null)
        {
            var acceptedExists = await EfRepairSpecificationStore.AcceptedQuery(context, request.CaseId)
                .AnyAsync(cancellationToken);
            if (acceptedExists)
            {
                throw new InvalidOperationException(
                    "An accepted repair specification is immutable; start a reasoned correction draft before editing its lines.");
            }
            specification = EfRepairSpecificationStore.NewLegacyDraft(
                request.CaseId, workflow.Case, request.Actor.SubjectId, request.OperationKey, now);
            context.CaseRepairSpecifications.Add(specification);
        }
        var specificationId = specification?.Id;
        var lines = await context.CaseEstimateLines
            .Where(item => item.CaseId == request.CaseId
                && item.RepairSpecificationId == specificationId)
            .OrderBy(item => item.Position)
            .ToListAsync(cancellationToken);

        var merged = fields.ToDictionary(
            item => item.FieldPath,
            item => item.Value,
            StringComparer.Ordinal);
        foreach (var (path, value) in request.Fields)
        {
            if (value is null)
            {
                merged.Remove(path);
            }
            else
            {
                merged[path] = value;
            }
        }

        AssessmentPolicy.ValidateMergedState(request.Fields, merged);

        var confirmedBy = request.Actor.Kind == ActorKind.Staff ? request.Actor.SubjectId : null;
        var beforeFields = new Dictionary<string, object?>(StringComparer.Ordinal);
        var afterFields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (path, value) in request.Fields)
        {
            var existing = fields.SingleOrDefault(item => item.FieldPath == path);
            beforeFields[path] = existing is null
                ? null
                : new { existing.Value, existing.ConfirmedBy };
            if (value is null)
            {
                if (existing is not null)
                {
                    context.CaseAssessmentFields.Remove(existing);
                    fields.Remove(existing);
                }
                afterFields[path] = null;
                continue;
            }

            if (existing is null)
            {
                existing = new()
                {
                    CaseId = request.CaseId,
                    Case = workflow.Case,
                    FieldPath = path,
                    Value = value,
                    RecordedByKind = request.Actor.Kind.ToString(),
                    RecordedBy = request.Actor.SubjectId,
                    RecordedAtUtc = now,
                    ConfirmedBy = confirmedBy,
                    ConfirmedAtUtc = confirmedBy is null ? null : now
                };
                context.CaseAssessmentFields.Add(existing);
                fields.Add(existing);
            }
            else if (confirmedBy is null
                && string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                // An automation resubmission of a value that has not changed
                // leaves the record alone: saving unchanged data must not
                // reset readiness or advisory state (FRD-01 case identity and
                // lifecycle, the progression rules), so a value a staff Engineer
                // already confirmed stays confirmed and keeps its
                // provenance. A staff save still re-stamps, because that is
                // how an Engineer confirms a value.
            }
            else
            {
                existing.Value = value;
                existing.RecordedByKind = request.Actor.Kind.ToString();
                existing.RecordedBy = request.Actor.SubjectId;
                existing.RecordedAtUtc = now;
                existing.ConfirmedBy = confirmedBy;
                existing.ConfirmedAtUtc = confirmedBy is null ? null : now;
            }

            afterFields[path] = new { existing.Value, existing.ConfirmedBy };
        }

        object? beforeLines = null;
        object? afterLines = null;
        if (request.EstimateLines is { } replacementLines)
        {
            beforeLines = lines.Select(LineEvidence).ToArray();
            context.CaseEstimateLines.RemoveRange(lines);
            lines.Clear();
            var position = 0;
            foreach (var line in replacementLines)
            {
                position++;
                var entity = new CaseEstimateLineEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = request.CaseId,
                    Case = workflow.Case,
                    RepairSpecificationId = specificationId,
                    RepairSpecification = specification,
                    Position = position,
                    LineType = line.Type,
                    GuideCode = line.GuideCode,
                    Description = line.Description,
                    WorkUnits = line.WorkUnits,
                    Price = line.Price,
                    Unpriced = line.Unpriced,
                    PartNumber = line.PartNumber,
                    Betterment = line.Betterment,
                    Status = line.Status,
                    EvidenceLabel = line.EvidenceLabel,
                    Justification = line.Justification,
                    RecordedByKind = request.Actor.Kind.ToString(),
                    RecordedBy = request.Actor.SubjectId,
                    RecordedAtUtc = now,
                    ConfirmedBy = confirmedBy,
                    ConfirmedAtUtc = confirmedBy is null ? null : now
                };
                context.CaseEstimateLines.Add(entity);
                lines.Add(entity);
            }
        }

        afterLines = request.EstimateLines is null
            ? null
            : lines.Select(LineEvidence).ToArray();
        var beforeVersion = workflow.Version;
        workflow.Version++;
        ClearLease(workflow);
        AddHistory(
            context,
            workflow,
            request,
            requestHash,
            beforeVersion,
            JsonSerializer.Serialize(
                new { Fields = beforeFields, EstimateLines = beforeLines },
                JsonOptions),
            JsonSerializer.Serialize(
                new
                {
                    Fields = afterFields,
                    EstimateLines = afterLines,
                    request.AiWorkRequestId
                },
                JsonOptions),
            now);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CaseVersionConflictException(
                request.CaseId,
                request.ExpectedVersion,
                request.ExpectedVersion + 1);
        }

        return await GetRequiredAsync(context, request.CaseId, cancellationToken);
    }

    private async Task<CaseAssessmentProjection> GetRequiredAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleAsync(item => item.CaseId == caseId, cancellationToken);
        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        var specificationId = await CurrentSpecificationIdAsync(caseId, cancellationToken);
        var lines = await context.CaseEstimateLines.AsNoTracking()
            .Where(item => item.CaseId == caseId
                && item.RepairSpecificationId == specificationId)
            .OrderBy(item => item.Position)
            .ToArrayAsync(cancellationToken);
        var caseDataFields = await context.CaseDataFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync(cancellationToken);
        return Map(workflow, fields, lines, caseDataFields);
    }

    private static async Task<bool> FindReplayAsync(
        PegasusDbContext context,
        SaveAssessmentRequest request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var replay = await context.CaseWorkflowEvents.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId && item.OperationKey == request.OperationKey,
                cancellationToken);
        if (replay is null)
        {
            return false;
        }

        if (!FixedTimeEquals(replay.RequestHash, requestHash))
        {
            throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
        }

        return true;
    }

    private static void AddHistory(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        SaveAssessmentRequest request,
        string requestHash,
        long beforeVersion,
        string beforeJson,
        string afterJson,
        DateTimeOffset occurredAtUtc)
    {
        const string EventType = "case_assessment_saved";
        var rolesJson = JsonSerializer.Serialize(
            request.Actor.Roles.OrderBy(role => role),
            JsonOptions);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = EventType,
            OperationKey = request.OperationKey,
            RequestHash = requestHash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = rolesJson,
            Reason = request.Reason,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = workflow.CaseId.ToString("D"),
            EventKind = EventType,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = rolesJson,
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = request.OperationKey,
            Reason = request.Reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            PolicyVersion = $"{AssessmentPolicy.PolicyKey}/v{AssessmentPolicy.PolicyVersion}"
        });
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Case = workflow.Case,
            EventType = EventType,
            Actor = request.Actor.SubjectId,
            Reason = request.Reason,
            OccurredAtUtc = occurredAtUtc,
            OperationKey = request.OperationKey,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });
    }

    internal static CaseAssessmentProjection Map(
        CaseWorkflowEntity workflow,
        IReadOnlyList<CaseAssessmentFieldEntity> fields,
        IReadOnlyList<CaseEstimateLineEntity> lines,
        IReadOnlyList<CaseDataFieldEntity> caseDataFields) => new(
        workflow.CaseId,
        workflow.Case.Reference,
        workflow.Version,
        Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            ? state
            : throw new InvalidDataException(
                $"Unknown persisted case lifecycle state '{workflow.State}'."),
        workflow.AssignedEngineerId,
        fields.Select(item => new AssessmentFieldValue(
                item.FieldPath,
                item.Value,
                ParseActorKind(item.RecordedByKind),
                item.RecordedBy,
                item.RecordedAtUtc,
                item.ConfirmedBy,
                item.ConfirmedAtUtc))
            .ToArray(),
        lines.Select(item => new CaseEstimateLineRecord(
                item.Id,
                item.Position,
                item.LineType,
                item.GuideCode,
                item.Description,
                item.WorkUnits,
                item.Price,
                item.Unpriced,
                item.PartNumber,
                item.Betterment,
                item.Status,
                item.EvidenceLabel,
                item.Justification,
                ParseActorKind(item.RecordedByKind),
                item.RecordedBy,
                item.RecordedAtUtc,
                item.ConfirmedBy,
                item.ConfirmedAtUtc))
            .ToArray(),
        MapCaseOwned(caseDataFields));

    /// <summary>
    /// The current specification for report/read purposes is the accepted
    /// one, or the open draft when nothing is accepted yet. <see
    /// cref="IRepairSpecificationStore"/> is the single owner of both
    /// queries; this store only resolves which one wins.
    /// </summary>
    private async Task<Guid?> CurrentSpecificationIdAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var accepted = await repairSpecifications.GetCurrentAcceptedAsync(caseId, cancellationToken);
        if (accepted is not null)
        {
            return accepted.SpecificationId;
        }
        var draft = await repairSpecifications.GetCurrentDraftAsync(caseId, cancellationToken);
        return draft?.SpecificationId;
    }

    private static AssessmentCaseOwnedData MapCaseOwned(
        IReadOnlyList<CaseDataFieldEntity> caseDataFields)
    {
        string? Current(string fieldName)
        {
            var values = caseDataFields
                .Where(item => item.FieldName == fieldName)
                .ToArray();
            var current =
                values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Confirmed)
                ?? values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Fact)
                ?? values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Suggestion);
            return current?.Value;
        }

        DateOnly? CurrentDate(string fieldName) =>
            Current(fieldName) is { } value
                ? DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;

        var inspectionMode = Current(CaseDataFieldNames.InspectionMode) switch
        {
            null => null,
            "physical_address" => nameof(CaseInspectionMode.PhysicalAddress),
            "image_based_assessment" => nameof(CaseInspectionMode.ImageBasedAssessment),
            var unknown => throw new InvalidDataException(
                $"Unknown persisted inspection mode '{unknown}'.")
        };
        return new(
            Current(CaseDataFieldNames.VehicleRegistration),
            Current(CaseDataFieldNames.VehicleMake),
            Current(CaseDataFieldNames.VehicleModel),
            Current(CaseDataFieldNames.VehicleMileage) is { } mileage
                ? long.Parse(mileage, NumberStyles.None, CultureInfo.InvariantCulture)
                : null,
            Current(CaseDataFieldNames.VehicleMileageUnit),
            CurrentDate(CaseDataFieldNames.IncidentDate),
            CurrentDate(CaseDataFieldNames.InstructionDate),
            inspectionMode,
            Current(CaseDataFieldNames.InspectionAddress));
    }

    private static object LineEvidence(CaseEstimateLineEntity line) => new
    {
        line.Position,
        line.LineType,
        line.GuideCode,
        line.Description,
        line.WorkUnits,
        line.Price,
        line.Unpriced,
        line.PartNumber,
        line.Betterment,
        line.Status,
        line.EvidenceLabel,
        line.Justification,
        line.ConfirmedBy
    };

    private static ActorKind ParseActorKind(string value) =>
        Enum.TryParse<ActorKind>(value, out var kind)
            ? kind
            : throw new InvalidDataException($"Unknown persisted actor kind '{value}'.");

    private static void RequireVersion(CaseWorkflowEntity workflow, long expectedVersion) =>
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);

    private static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string token,
        DateTimeOffset now) =>
        CaseMutationGuard.RequireLease(workflow, actor, token, now);

    private static void ClearLease(CaseWorkflowEntity workflow) =>
        CaseMutationGuard.ClearLease(workflow);

    private DateTimeOffset UtcNow()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string RequestHash(SaveAssessmentRequest request)
    {
        var material = JsonSerializer.Serialize(new
        {
            Command = "save_assessment",
            request.CaseId,
            request.ExpectedVersion,
            ActorKind = request.Actor.Kind.ToString(),
            request.Actor.SubjectId,
            Roles = request.Actor.Roles
                .OrderBy(role => role)
                .Select(role => role.ToString())
                .ToArray(),
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken,
            Fields = request.Fields.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            request.EstimateLines,
            request.AiWorkRequestId
        }, JsonOptions);
        return Hash(material);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != 64 || right.Length != 64
            || left.Any(character => !char.IsAsciiHexDigit(character))
            || right.Any(character => !char.IsAsciiHexDigit(character)))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));
    }
}
