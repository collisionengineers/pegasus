using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists immutable case report generations over the Foundation
/// <c>CaseReportGenerations</c> and <c>GeneratedCaseArtifacts</c> tables.
/// </summary>
/// <remarks>
/// <para>
/// Every write is a short serializable transaction and none of them holds a
/// lock through Chromium or Box: the freeze writes the snapshot and one
/// Pending artifact row, rendering and custody happen outside, and a second
/// short transaction records what custody actually did. This is
/// <c>EvaSubmissionStore</c>'s shape, deliberately not
/// <c>EfMarketResearchAiJobCompletionStore</c>'s.
/// </para>
/// <para>
/// The snapshot hash is the SHA-256 of the canonical serialization of the
/// snapshot's <em>material</em> facts — the freeze operation key, its actor
/// and its timestamp are excluded, so asking for the fee note of an already
/// frozen report reuses that generation instead of freezing a second one.
/// </para>
/// </remarks>
public sealed class EfCaseReportGenerationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    ICaseReportSnapshotSource snapshotSource,
    IReadLogicalDocumentVersion documentReader,
    TimeProvider timeProvider)
    : ICaseReportGenerationStore, ICaseReportGenerationQueries, IGeneratedCaseArtifactStore
{
    internal const string PolicyVersion = "case_report_generation/v1";
    internal const string ReadyEventKind = "case_report_generation_ready";
    internal const string FrozenEventKind = "case_report_generation_frozen";
    internal const string ArtifactConfirmedEventKind = "case_report_artifact_confirmed";
    internal const string ArtifactOutcomeEventKind = "case_report_artifact_outcome_recorded";
    internal const string StaleEventKind = "case_report_generation_stale";

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseReportFreezeResult> FreezeAsync(
        FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        var operationKey = ValidateOperationKey(request.OperationKey);

        // Read model first, outside every transaction: it reaches the
        // Assessment workspace query and case documents, and must never run
        // under a serializable lock.
        var inputs = await snapshotSource
            .GetAsync(request.CaseId, request.Actor, cancellationToken)
            .ConfigureAwait(false);
        if (inputs is null)
        {
            return new(CaseReportFreezeOutcome.NotFound, null, null, []);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var replay = await context.Set<GeneratedCaseArtifactEntity>()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            var replayed = await RequireRecordAsync(context, request.CaseId, replay.GenerationId, cancellationToken)
                .ConfigureAwait(false);
            return new(
                replay.State == nameof(CaseReportArtifactStatus.Confirmed)
                    ? CaseReportFreezeOutcome.AlreadyConfirmed
                    : CaseReportFreezeOutcome.Frozen,
                replayed,
                replay.Id,
                []);
        }

        var workflow = await context.CaseWorkflows
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            .ConfigureAwait(false);
        if (workflow is null)
        {
            return new(CaseReportFreezeOutcome.NotFound, null, null, []);
        }

        var now = timeProvider.GetUtcNow();
        CaseMutationGuard.Require(
            workflow, request.Actor, request.ExpectedCaseVersion, request.LeaseToken, now);

        var readiness = CaseReportReadiness.Evaluate(inputs.Readiness);
        if (!readiness.IsReady)
        {
            return new(CaseReportFreezeOutcome.NotReady, null, null, readiness.Reasons);
        }

        var (reportDate, overridden) = CaseReportReadiness.ResolveReportDate(
            readiness.RecordedReportDate,
            readiness.ReportDateOverridden,
            DateOnly.FromDateTime(now.UtcDateTime));
        var projected = AssessmentReportProjection.Project(
            inputs.Projection with { ReportDate = reportDate });
        if (projected.Snapshot is null)
        {
            return new(CaseReportFreezeOutcome.NotReady, null, null, projected.Reasons);
        }

        var snapshot = BuildSnapshot(request, inputs, readiness, projected.Snapshot, reportDate, overridden, now, operationKey);
        var snapshotHash = HashOf(MaterialOf(snapshot));

        // Reuse only the Case's current, un-staled generation: a stale or
        // superseded generation with the same material hash is history, not
        // a deliverable snapshot — reusing it would wedge the report journey
        // on an undeliverable current forever (B09 review, lifecycle).
        var generation = await context.Set<CaseReportGenerationEntity>()
            .SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId
                    && item.SnapshotHash == snapshotHash
                    && item.SupersededById == null
                    && item.State != nameof(CaseReportGenerationState.Stale),
                cancellationToken)
            .ConfigureAwait(false);
        if (generation is null)
        {
            generation = new CaseReportGenerationEntity
            {
                Id = Guid.NewGuid(),
                CaseId = request.CaseId,
                CaseVersion = workflow.Version,
                SnapshotHash = snapshotHash,
                SnapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions),
                TemplateVersion = request.TemplateVersion,
                RendererVersion = request.RendererVersion,
                State = nameof(CaseReportGenerationState.Pending),
                GeneratedAtUtc = now,
                Version = 1,
            };
            await SupersedeCurrentAsync(context, request.CaseId, generation.Id, cancellationToken)
                .ConfigureAwait(false);
            context.Set<CaseReportGenerationEntity>().Add(generation);
        }
        else if (await context.Set<GeneratedCaseArtifactEntity>()
            .SingleOrDefaultAsync(
                item => item.GenerationId == generation.Id && item.Kind == request.Kind.ToString(),
                cancellationToken).ConfigureAwait(false) is { } existing)
        {
            // The same kind of the same frozen snapshot: reuse the artifact
            // row and its recorded operation key, never a second render
            // identity for the same bytes.
            var record = await RequireRecordAsync(context, request.CaseId, generation.Id, cancellationToken)
                .ConfigureAwait(false);
            return new(
                existing.State == nameof(CaseReportArtifactStatus.Confirmed)
                    ? CaseReportFreezeOutcome.AlreadyConfirmed
                    : CaseReportFreezeOutcome.Frozen,
                record,
                existing.Id,
                []);
        }

        var artifact = new GeneratedCaseArtifactEntity
        {
            Id = Guid.NewGuid(),
            GenerationId = generation.Id,
            Kind = request.Kind.ToString(),
            State = nameof(CaseReportArtifactStatus.Pending),
            OperationKey = operationKey,
        };
        context.Set<GeneratedCaseArtifactEntity>().Add(artifact);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "case",
            request.CaseId.ToString("D"),
            FrozenEventKind,
            request.Actor,
            now,
            snapshot.OperationKey,
            request.Reason,
            afterJson: DocumentActionHistory.Serialize(new
            {
                GenerationId = generation.Id,
                ArtifactId = artifact.Id,
                Kind = request.Kind.ToString(),
                SnapshotHash = snapshotHash,
                CaseVersion = workflow.Version,
                snapshot.TemplateVersion,
                snapshot.RendererVersion,
                snapshot.ReportDate,
                snapshot.ReportDateOverridden,
            })));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var frozen = await RequireRecordAsync(context, request.CaseId, generation.Id, cancellationToken)
            .ConfigureAwait(false);
        return new(CaseReportFreezeOutcome.Frozen, frozen, artifact.Id, []);
    }

    public async Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
        ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        RequireHash(request.Sha256);
        var caseKey = request.CaseId.ToString("D");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var (generation, artifact) = await RequireArtifactAsync(
            context, request.CaseId, request.GenerationId, request.ArtifactId, cancellationToken)
            .ConfigureAwait(false);

        if (artifact.State == nameof(CaseReportArtifactStatus.Confirmed))
        {
            if (!string.Equals(artifact.Sha256, request.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The generated artifact is already confirmed with different immutable bytes.");
            }

            return await RequireRecordAsync(context, request.CaseId, request.GenerationId, cancellationToken)
                .ConfigureAwait(false);
        }

        artifact.State = nameof(CaseReportArtifactStatus.Confirmed);
        artifact.VersionId = request.VersionId;
        artifact.Sha256 = request.Sha256;
        artifact.FailureCode = null;

        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "case",
            caseKey,
            ArtifactConfirmedEventKind,
            request.Actor,
            request.OccurredAtUtc,
            artifact.OperationKey,
            reason: "Confirmed the immutable generated artifact",
            afterJson: DocumentActionHistory.Serialize(new
            {
                GenerationId = generation.Id,
                ArtifactId = artifact.Id,
                artifact.Kind,
                request.DocumentId,
                request.VersionId,
                request.Sha256,
                request.ContentLength,
                request.FileName,
                request.MediaType,
                request.BoxFileId,
                request.BoxVersionId,
            })));

        var siblings = await context.Set<GeneratedCaseArtifactEntity>()
            .Where(item => item.GenerationId == generation.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var everyArtifactConfirmed = siblings
            .All(item => item.Id == artifact.Id || item.State == nameof(CaseReportArtifactStatus.Confirmed));
        if (everyArtifactConfirmed && generation.State == nameof(CaseReportGenerationState.Pending))
        {
            generation.State = nameof(CaseReportGenerationState.Confirmed);
        }

        // A06: custody confirmation alone is never "ready". The generation
        // becomes ready only when every artifact it was asked for is
        // confirmed and no material change has staled it in the meantime.
        if (everyArtifactConfirmed && generation.State != nameof(CaseReportGenerationState.Stale))
        {
            var snapshot = DeserializeSnapshot(generation);
            var alreadyReady = await context.ActionHistory
                .AsNoTracking()
                .AnyAsync(
                    item => item.AggregateType == "case"
                        && item.AggregateId == caseKey
                        && item.EventKind == ReadyEventKind
                        && item.CorrelationId == snapshot.OperationKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!alreadyReady)
            {
                context.ActionHistory.Add(DocumentActionHistory.Succeeded(
                    "case",
                    caseKey,
                    ReadyEventKind,
                    request.Actor,
                    request.OccurredAtUtc,
                    snapshot.OperationKey,
                    reason: "Every artifact of the current case report generation is confirmed",
                    afterJson: DocumentActionHistory.Serialize(new
                    {
                        GenerationId = generation.Id,
                        generation.SnapshotHash,
                        generation.TemplateVersion,
                        generation.RendererVersion,
                        Artifacts = siblings
                            .OrderBy(item => item.Kind, StringComparer.Ordinal)
                            .Select(item => new { item.Id, item.Kind })
                            .ToArray(),
                    })));
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return await RequireRecordAsync(context, request.CaseId, request.GenerationId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
        RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.Status == CaseReportArtifactStatus.Confirmed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request), request.Status, "A confirmation is recorded through ConfirmArtifactAsync.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var (generation, artifact) = await RequireArtifactAsync(
            context, request.CaseId, request.GenerationId, request.ArtifactId, cancellationToken)
            .ConfigureAwait(false);
        if (artifact.State == nameof(CaseReportArtifactStatus.Confirmed))
        {
            throw new InvalidOperationException(
                "A confirmed generated artifact is immutable and never returns to an unresolved state.");
        }

        artifact.State = request.Status.ToString();
        // The logical version is retained so a retry after a process restart
        // can ask custody what actually happened instead of rendering blind.
        // Everything else custody reported about the object — its document,
        // Box identities and pending storage key — lives on that version row.
        artifact.VersionId = request.VersionId;
        artifact.FailureCode = request.FailureCode;

        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "case",
            request.CaseId.ToString("D"),
            ArtifactOutcomeEventKind,
            request.Actor,
            request.OccurredAtUtc,
            artifact.OperationKey,
            reason: $"Recorded the {request.Status} custody outcome of a generated artifact",
            afterJson: DocumentActionHistory.Serialize(new
            {
                GenerationId = generation.Id,
                ArtifactId = artifact.Id,
                artifact.Kind,
                Status = request.Status.ToString(),
                request.DocumentId,
                request.VersionId,
                request.BoxFileId,
                request.BoxVersionId,
                request.PendingContentStorageKey,
                request.FailureCode,
            })));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return await RequireRecordAsync(context, request.CaseId, request.GenerationId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CaseReportGenerationRecord?> GetAsync(
        ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await LoadRecordAsync(context, caseId, generationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CaseReportGenerationRecord?> GetCurrentAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var current = await context.Set<CaseReportGenerationEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.SupersededById == null)
            .OrderByDescending(item => item.GeneratedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
        {
            return null;
        }

        var artifacts = await ArtifactsByGenerationAsync(context, [current.Id], cancellationToken)
            .ConfigureAwait(false);
        return Map(current, artifacts);
    }

    public async Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var generations = await context.Set<CaseReportGenerationEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.GeneratedAtUtc)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var artifacts = await ArtifactsByGenerationAsync(
            context, generations.Select(item => item.Id).ToArray(), cancellationToken).ConfigureAwait(false);
        return generations.Select(item => Map(item, artifacts)).ToArray();
    }

    public async Task<int> MarkStaleAsync(
        Guid caseId, string reasonCode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var marked = await MarkStaleAsync(
            context, caseId, reasonCode, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        if (marked == 0)
        {
            return 0;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return marked;
    }

    /// <summary>
    /// The same-context stale core the sibling B stores call inside their
    /// own transactions, so a material change and the staleness it causes
    /// commit atomically. Only the Case's current generation moves: a
    /// superseded generation keeps its bytes, its state and its history
    /// exactly as issued. This never saves — the caller's transaction does.
    /// </summary>
    internal static async Task<int> MarkStaleAsync(
        PegasusDbContext context,
        Guid caseId,
        string reasonCode,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var current = await context.Set<CaseReportGenerationEntity>()
            .Where(item => item.CaseId == caseId
                && item.SupersededById == null
                && item.State != nameof(CaseReportGenerationState.Stale))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var generation in current)
        {
            generation.State = nameof(CaseReportGenerationState.Stale);
            context.ActionHistory.Add(new ActionHistoryEntity
            {
                Id = Guid.NewGuid(),
                AggregateType = "case",
                AggregateId = caseId.ToString("D"),
                EventKind = StaleEventKind,
                ActorKind = nameof(ActorKind.SystemWorker),
                ActorSubjectId = PolicyVersion,
                ActorRolesJson = "[]",
                OccurredAtUtc = nowUtc,
                Outcome = "Succeeded",
                CorrelationId = DeserializeSnapshot(generation).OperationKey,
                Reason = reasonCode,
                AfterJson = DocumentActionHistory.Serialize(new
                {
                    GenerationId = generation.Id,
                    generation.SnapshotHash,
                    ReasonCode = reasonCode,
                }),
                PolicyVersion = PolicyVersion,
            });
        }

        return current.Length;
    }

    async Task<CaseReportGeneration?> ICaseReportGenerationQueries.GetAsync(
        ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken)
    {
        var record = await GetAsync(actor, caseId, generationId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        return new CaseReportGeneration(
            record.Id,
            record.CaseId,
            record.CaseVersion,
            record.Version,
            record.SnapshotHash,
            record.TemplateVersion,
            record.Snapshot.CalculationPolicyVersion,
            record.Snapshot.GeneratedBy.ToActor(),
            record.GeneratedAtUtc,
            record.Artifacts
                .Where(artifact => artifact.Status == CaseReportArtifactStatus.Confirmed)
                .Select(artifact => new CaseReportArtifact(
                    artifact.DocumentId!.Value,
                    artifact.VersionId!.Value,
                    artifact.Sha256!,
                    artifact.ContentLength ?? 0,
                    artifact.FileName ?? string.Empty,
                    artifact.MediaType ?? "application/pdf",
                    artifact.Kind.ToString()))
                .ToArray());
    }

    public async Task<LogicalDocumentContent> OpenAsync(
        ActionActor actor, Guid caseId, Guid generationId, Guid artifactId,
        CancellationToken cancellationToken)
    {
        var record = await GetAsync(actor, caseId, generationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The case report generation is unavailable.");
        var artifact = record.Artifacts.SingleOrDefault(item => item.Id == artifactId)
            ?? throw new InvalidOperationException("The generated artifact is unavailable.");
        if (artifact.Status != CaseReportArtifactStatus.Confirmed)
        {
            throw new InvalidOperationException(
                "Only a confirmed generated artifact can be reopened; nothing is regenerated on read.");
        }

        return await documentReader.OpenAsync(
            new ReadLogicalDocumentVersionRequest(
                actor,
                artifact.DocumentId,
                artifact.VersionId,
                IntakeAssetId: null,
                caseId,
                IntakeReceiptId: null,
                artifact.Sha256!,
                artifact.ContentLength ?? 0),
            cancellationToken).ConfigureAwait(false);
    }

    private static CaseReportGenerationSnapshot BuildSnapshot(
        FreezeCaseReportGenerationRequest request,
        CaseReportFreezeInputs inputs,
        CaseReportReadinessResult readiness,
        AssessmentReportSnapshot report,
        DateOnly reportDate,
        bool overridden,
        DateTimeOffset now,
        string operationKey)
    {
        var signatory = readiness.Signatory
            ?? throw new InvalidOperationException("A ready case report has a resolved sign-off Engineer.");
        var estimate = inputs.Readiness.CurrentEstimate
            ?? throw new InvalidOperationException("A ready case report has a Current estimate.");
        var valuation = inputs.Readiness.AppliedValuation
            ?? throw new InvalidOperationException("A ready case report has an accepted Engineer's Value.");

        return new CaseReportGenerationSnapshot(
            request.CaseId,
            inputs.CaseVersion,
            inputs.CaseReference,
            operationKey,
            CaseReportActor.Of(request.Actor),
            now,
            signatory.StaffId,
            Convert.ToHexStringLower(SHA256.HashData(signatory.Signature)),
            signatory.SignatureContentType,
            estimate.SpecificationId,
            estimate.Version,
            report.Costs,
            valuation.AcceptedEngineerValue,
            valuation.Id,
            readiness.Content,
            inputs.Projection.Guides ?? ReportGuideSources.None,
            reportDate,
            overridden,
            report.AgreedFee,
            report.FeeDescriptionLines,
            inputs.Projection.Sources
                .Select(source => new CaseReportSnapshotSource(
                    source.Name,
                    source.Version,
                    source.DocumentId ?? Guid.Empty,
                    source.VersionId ?? Guid.Empty,
                    source.Sha256,
                    source.BoxFileId,
                    source.BoxVersionId))
                .ToArray(),
            inputs.Projection.Photos
                .Select(photo => SnapshotImageOf(photo, inputs.Readiness.ConfirmedImageSources))
                .ToArray(),
            request.TemplateVersion,
            request.RendererVersion,
            // Bytes are never frozen: the hashes above pin them and they are
            // reopened through custody once, at render time.
            report with
            {
                Photos = [],
                Signatory = report.Signatory with { SignatureContent = [] },
            });
    }

    /// <summary>
    /// One prepared image as the snapshot pins it. The custody-confirmed
    /// source version supplies the document identity and length; it is looked
    /// up once.
    /// </summary>
    private static CaseReportSnapshotImage SnapshotImageOf(
        ReportImageEvidence photo, IReadOnlyDictionary<Guid, DocumentVersion> confirmed)
    {
        var occurrenceId = photo.OccurrenceId ?? Guid.Empty;
        confirmed.TryGetValue(occurrenceId, out var version);
        return new CaseReportSnapshotImage(
            occurrenceId,
            photo.VersionId ?? Guid.Empty,
            version?.DocumentId ?? Guid.Empty,
            version?.ContentLength ?? 0,
            photo.Sha256,
            photo.ContentType,
            photo.Role,
            photo.Order,
            photo.Rotation,
            photo.AppliedCrop,
            photo.BoxFileId,
            photo.BoxVersionId);
    }

    /// <summary>
    /// The snapshot's material facts: the freeze's own operation key, actor
    /// and timestamp are excluded so the same accepted facts hash to the same
    /// generation however many artifacts are asked of them.
    /// </summary>
    private static CaseReportGenerationSnapshot MaterialOf(CaseReportGenerationSnapshot snapshot) =>
        snapshot with
        {
            OperationKey = string.Empty,
            GeneratedBy = CaseReportActor.None,
            GeneratedAtUtc = default,
        };

    private static string HashOf(CaseReportGenerationSnapshot snapshot) =>
        Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, SnapshotJsonOptions))));

    private static async Task SupersedeCurrentAsync(
        PegasusDbContext context, Guid caseId, Guid supersededById, CancellationToken cancellationToken)
    {
        var current = await context.Set<CaseReportGenerationEntity>()
            .Where(item => item.CaseId == caseId && item.SupersededById == null)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var generation in current)
        {
            generation.SupersededById = supersededById;
            generation.State = nameof(CaseReportGenerationState.Stale);
        }
    }

    private static async Task<(CaseReportGenerationEntity Generation, GeneratedCaseArtifactEntity Artifact)>
        RequireArtifactAsync(
            PegasusDbContext context, Guid caseId, Guid generationId, Guid artifactId,
            CancellationToken cancellationToken)
    {
        var generation = await context.Set<CaseReportGenerationEntity>()
            .SingleOrDefaultAsync(item => item.Id == generationId && item.CaseId == caseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The case report generation is unavailable.");
        var artifact = await context.Set<GeneratedCaseArtifactEntity>()
            .SingleOrDefaultAsync(
                item => item.Id == artifactId && item.GenerationId == generationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The generated artifact is unavailable.");
        return (generation, artifact);
    }

    private static async Task<CaseReportGenerationRecord> RequireRecordAsync(
        PegasusDbContext context, Guid caseId, Guid generationId, CancellationToken cancellationToken) =>
        await LoadRecordAsync(context, caseId, generationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The case report generation is unavailable.");

    private static async Task<CaseReportGenerationRecord?> LoadRecordAsync(
        PegasusDbContext context, Guid caseId, Guid generationId, CancellationToken cancellationToken)
    {
        var generation = await context.Set<CaseReportGenerationEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == generationId && item.CaseId == caseId, cancellationToken)
            .ConfigureAwait(false);
        if (generation is null)
        {
            return null;
        }

        var artifacts = await ArtifactsByGenerationAsync(context, [generationId], cancellationToken)
            .ConfigureAwait(false);
        return Map(generation, artifacts);
    }

    /// <summary>
    /// Reads every artifact of the named generations, joining the custody
    /// version row for the document, Box and pending-storage identities the
    /// artifact table does not repeat.
    /// </summary>
    private static async Task<ILookup<Guid, CaseReportArtifactRecord>> ArtifactsByGenerationAsync(
        PegasusDbContext context, IReadOnlyList<Guid> generationIds, CancellationToken cancellationToken)
    {
        var rows = await (
                from artifact in context.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                where generationIds.Contains(artifact.GenerationId)
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on artifact.VersionId equals version.Id into versions
                from version in versions.DefaultIfEmpty()
                select new
                {
                    artifact.Id,
                    artifact.GenerationId,
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
                row.GenerationId,
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
            .ToLookup(record => record.GenerationId);
    }

    private static CaseReportGenerationRecord Map(
        CaseReportGenerationEntity generation, ILookup<Guid, CaseReportArtifactRecord> artifacts) =>
        new(
            generation.Id,
            generation.CaseId,
            generation.CaseVersion,
            generation.Version,
            generation.SnapshotHash,
            DeserializeSnapshot(generation),
            generation.TemplateVersion,
            generation.RendererVersion,
            Enum.Parse<CaseReportGenerationState>(generation.State),
            generation.GeneratedAtUtc,
            generation.SupersededById,
            artifacts[generation.Id].OrderBy(item => item.Kind).ToArray());

    private static CaseReportGenerationSnapshot DeserializeSnapshot(CaseReportGenerationEntity generation) =>
        JsonSerializer.Deserialize<CaseReportGenerationSnapshot>(
            generation.SnapshotJson, SnapshotJsonOptions)
        ?? throw new InvalidDataException(
            $"The frozen snapshot of case report generation '{generation.Id}' is unreadable.");

    private static void RequireHash(string sha256)
    {
        if (sha256.Length != 64 || !sha256.All(char.IsAsciiHexDigitLower))
        {
            throw new ArgumentException("A lower-case SHA-256 hash is required.", nameof(sha256));
        }
    }

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
}

/// <summary>
/// Rehydrates a frozen snapshot for rendering: the pinned image and signature
/// bytes are reopened through the immutable custody reader and the staff
/// account, then re-verified against the frozen hashes. Nothing is re-read
/// from current state, so a later material change can never leak into an
/// already frozen generation's bytes.
/// </summary>
public sealed class EfCaseReportContentSource(
    IReadLogicalDocumentVersion documentReader,
    IStaffAccountQueries staffAccountQueries) : ICaseReportContentSource
{
    public async Task<AssessmentReportSnapshot> ComposeAsync(
        CaseReportGenerationSnapshot snapshot,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var profiles = await staffAccountQueries.ListSignOffEngineersAsync(cancellationToken)
            .ConfigureAwait(false);
        var signatory = profiles.SingleOrDefault(profile => profile.StaffId == snapshot.SignatoryStaffId)
            ?? throw new InvalidOperationException(
                "The frozen sign-off Engineer account is no longer eligible to sign a report.");
        var signatureHash = Convert.ToHexStringLower(SHA256.HashData(signatory.Signature));
        if (!signatureHash.Equals(snapshot.SignatureSha256, StringComparison.Ordinal))
        {
            throw new ReportRenderRejectedException(
                "The sign-off Engineer's signature no longer matches the frozen generation.");
        }

        var photos = new List<ReportImageEvidence>(snapshot.Images.Count);
        foreach (var image in snapshot.Images)
        {
            await using var content = await documentReader.OpenAsync(
                new ReadLogicalDocumentVersionRequest(
                    actor,
                    image.DocumentId,
                    image.VersionId,
                    IntakeAssetId: null,
                    snapshot.CaseId,
                    IntakeReceiptId: null,
                    image.Sha256,
                    image.ContentLength),
                cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            await content.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            photos.Add(new ReportImageEvidence(
                content.FileName,
                image.ContentType,
                buffer.ToArray(),
                image.Sha256,
                image.Role,
                image.Order,
                image.Rotation,
                image.Crop,
                image.OccurrenceId,
                image.VersionId,
                image.BoxFileId,
                image.BoxVersionId));
        }

        return snapshot.Report with
        {
            Photos = photos,
            Signatory = snapshot.Report.Signatory with
            {
                SignatureContent = signatory.Signature,
                SignatureContentType = snapshot.SignatureContentType,
            },
        };
    }
}
