using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists report delivery preparations over the Foundation
/// <c>CaseReportDeliveryIntents</c> table. One preparation is one short
/// serializable transaction that re-reads the Case, its lease and the
/// generation's rows; the payload it pins is never rewritten, and nothing
/// here records a Sent state — that is A's transport observation.
/// </summary>
public sealed class EfCaseReportDeliveryPreparationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ICaseReportDeliveryPreparationStore
{
    internal const string PolicyVersion = "case_report_delivery_preparation/v1";
    internal const string PreparedEventKind = "case_report_delivery_prepared";

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseReportDeliveryPreparationRecord> PrepareAsync(
        PrepareCaseReportDeliveryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = command.Request;
        CaseReportDeliveryPolicy.RequireStaff(request.Actor);
        var operationKey = ValidateOperationKey(request.OperationKey);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var generation = await context.Set<CaseReportGenerationEntity>()
            .SingleOrDefaultAsync(
                item => item.Id == request.GenerationId && item.CaseId == request.CaseId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Case report generation '{request.GenerationId}' is unavailable on case '{request.CaseId}'.");
        var workflow = await context.CaseWorkflows
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");

        CaseReportDeliveryPolicy.RequireDeliverable(
            generation.Id,
            Enum.Parse<CaseReportGenerationState>(generation.State),
            generation.SupersededById is null,
            generation.Version,
            request.ExpectedGenerationVersion);
        var artifacts = CaseReportDeliveryPolicy.Attachments(
            generation.Id,
            await ArtifactsAsync(context, generation.Id, cancellationToken).ConfigureAwait(false));

        var payload = new Payload(
            request.CaseId,
            request.ExpectedCaseVersion,
            generation.Id,
            generation.Version,
            CaseReportActor.Of(request.Actor),
            artifacts,
            command.Addressing.To,
            command.Addressing.Cc,
            command.Addressing.Subject);
        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        var payloadHash = HashOf(payloadJson);

        // The same key is the same operation only with the same inputs; the
        // same key with different inputs is a conflict, never a second write.
        var replay = await context.Set<CaseReportDeliveryIntentEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.GenerationId == generation.Id && item.OperationKey == operationKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            if (!CaseOperationReplay.FixedTimeEquals(replay.PayloadHash, payloadHash))
            {
                throw new CaseOperationConflictException(request.CaseId, operationKey);
            }

            return Map(replay, workflow.Version, generation, artifacts);
        }

        var now = timeProvider.GetUtcNow();
        CaseMutationGuard.Require(
            workflow, request.Actor, request.ExpectedCaseVersion, request.LeaseToken, now);

        var entity = new CaseReportDeliveryIntentEntity
        {
            Id = Guid.NewGuid(),
            GenerationId = generation.Id,
            GenerationVersion = generation.Version,
            PayloadJson = payloadJson,
            PayloadHash = payloadHash,
            ActorSubjectId = request.Actor.SubjectId,
            PreparedAtUtc = now,
            OperationKey = operationKey,
            Version = 1,
        };
        context.Set<CaseReportDeliveryIntentEntity>().Add(entity);
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = request.CaseId.ToString("D"),
            EventKind = PreparedEventKind,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                request.Actor.Roles.OrderBy(role => role), PayloadJsonOptions),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = "Prepared the case report for delivery",
            AfterJson = DocumentActionHistory.Serialize(new
            {
                PreparationId = entity.Id,
                GenerationId = generation.Id,
                generation.Version,
                PayloadHash = payloadHash,
                Artifacts = artifacts,
                command.Addressing.To,
                command.Addressing.Cc,
                command.Addressing.Subject,
            }),
            PolicyVersion = PolicyVersion,
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, workflow.Version, generation, artifacts);
    }

    public async Task<CaseReportDeliveryPreparationRecord?> GetAsync(
        ActionActor actor, Guid caseId, Guid preparationId, CancellationToken cancellationToken)
    {
        CaseReportDeliveryPolicy.RequireStaff(actor);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The caller's own predicate stays inside the translatable query —
        // composing it over the Row projection would force client evaluation.
        var row = await (
                from intent in context.Set<CaseReportDeliveryIntentEntity>().AsNoTracking()
                join generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                    on intent.GenerationId equals generation.Id
                join workflow in context.CaseWorkflows.AsNoTracking()
                    on generation.CaseId equals workflow.CaseId
                where generation.CaseId == caseId && intent.Id == preparationId
                select new Row(intent, generation, workflow.Version))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : await MapAsync(context, row, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CaseReportDeliveryPreparationRecord?> GetCurrentAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken)
    {
        CaseReportDeliveryPolicy.RequireStaff(actor);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await (
                from intent in context.Set<CaseReportDeliveryIntentEntity>().AsNoTracking()
                join generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                    on intent.GenerationId equals generation.Id
                join workflow in context.CaseWorkflows.AsNoTracking()
                    on generation.CaseId equals workflow.CaseId
                where generation.CaseId == caseId && generation.SupersededById == null
                orderby intent.PreparedAtUtc descending
                select new Row(intent, generation, workflow.Version))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : await MapAsync(context, row, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CaseReportDeliveryPreparationRecord> MapAsync(
        PegasusDbContext context, Row row, CancellationToken cancellationToken)
    {
        var confirmed = (await ArtifactsAsync(context, row.Generation.Id, cancellationToken).ConfigureAwait(false))
            .Where(artifact => artifact.Status == CaseReportArtifactStatus.Confirmed)
            .OrderBy(artifact => artifact.Kind)
            .Select(CaseReportDeliveryPolicy.AttachmentOf)
            .ToArray();
        return Map(row.Intent, row.CaseVersion, row.Generation, confirmed);
    }

    private static CaseReportDeliveryPreparationRecord Map(
        CaseReportDeliveryIntentEntity entity,
        long caseVersion,
        CaseReportGenerationEntity generation,
        IReadOnlyList<StaffMailAttachment> confirmedArtifacts)
    {
        var payload = JsonSerializer.Deserialize<Payload>(entity.PayloadJson, PayloadJsonOptions)
            ?? throw new InvalidDataException(
                $"The payload of report delivery preparation '{entity.Id}' is unreadable.");
        return new(
            new CaseReportDeliveryPreparation(
                entity.Id,
                payload.CaseId,
                entity.GenerationId,
                entity.GenerationVersion,
                entity.Version,
                payload.Artifacts,
                payload.PreparedBy.ToActor(),
                entity.PreparedAtUtc),
            new CaseReportDeliveryAddressing(payload.To, payload.Cc, payload.Subject),
            // The frozen Case version travels in the payload; the live one is
            // the row this read just joined. The send boundary compares them.
            payload.CaseVersion,
            caseVersion,
            Enum.Parse<CaseReportGenerationState>(generation.State),
            generation.SupersededById is null,
            generation.Version,
            confirmedArtifacts);
    }

    /// <summary>
    /// Every artifact row of the generation joined to its custody version,
    /// which carries the document identity, length, file name and media type
    /// the artifact table does not repeat.
    /// </summary>
    private static async Task<IReadOnlyList<CaseReportArtifactRecord>> ArtifactsAsync(
        PegasusDbContext context, Guid generationId, CancellationToken cancellationToken)
    {
        var rows = await (
                from artifact in context.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                where artifact.GenerationId == generationId
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on artifact.VersionId equals version.Id into versions
                from version in versions.DefaultIfEmpty()
                select new
                {
                    artifact.Id,
                    artifact.Kind,
                    artifact.State,
                    artifact.OperationKey,
                    artifact.VersionId,
                    ArtifactSha256 = artifact.Sha256,
                    artifact.FailureCode,
                    DocumentId = (Guid?)version.DocumentId,
                    ContentLength = (long?)version.ContentLength,
                    FileName = (string?)version.FileName,
                    MediaType = (string?)version.MediaType,
                    version.BoxFileId,
                    version.BoxVersionId,
                    version.PendingContentStorageKey,
                })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .Select(row => new CaseReportArtifactRecord(
                row.Id,
                generationId,
                Enum.Parse<CaseReportArtifactKind>(row.Kind),
                Enum.Parse<CaseReportArtifactStatus>(row.State),
                row.OperationKey,
                row.DocumentId,
                row.VersionId,
                row.ArtifactSha256,
                row.ContentLength,
                row.FileName,
                row.MediaType,
                row.BoxFileId,
                row.BoxVersionId,
                row.PendingContentStorageKey,
                row.FailureCode))
            .ToArray();
    }

    private static string HashOf(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ValidateOperationKey(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var normalized = operationKey.Trim();
        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationKey), "The operation key cannot exceed 100 characters.");
        }

        return normalized;
    }

    private sealed record Row(
        CaseReportDeliveryIntentEntity Intent, CaseReportGenerationEntity Generation, long CaseVersion);

    /// <summary>
    /// The immutable delivery intent the row pins. Its hash is the replay
    /// key's fingerprint: the same operation key with a different payload is
    /// a conflict. The preparing actor's kind and roles travel here because
    /// the row keeps only the subject identifier.
    /// </summary>
    private sealed record Payload(
        Guid CaseId,
        long CaseVersion,
        Guid GenerationId,
        long GenerationVersion,
        CaseReportActor PreparedBy,
        IReadOnlyList<StaffMailAttachment> Artifacts,
        IReadOnlyList<StaffMailRecipient> To,
        IReadOnlyList<StaffMailRecipient> Cc,
        string Subject);
}
