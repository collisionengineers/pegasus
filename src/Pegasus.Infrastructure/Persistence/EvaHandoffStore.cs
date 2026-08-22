using System.Buffers.Binary;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Eva;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EvaHandoffStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    ICaseDataQueries caseDataQueries,
    IVehicleEvidenceQueries vehicleEvidenceQueries,
    IDocumentContentStore contentStore,
    IEvaHandoffProxy proxy,
    EvaMappingAcceptance mappingAcceptance,
    TimeProvider timeProvider) : IEvaHandoffQueries, IEvaHandoffPersistence, IExportCaseBundle
{
    public Task<GenerateEvaHandoffResult> ExecuteAsync(
        GenerateEvaHandoffRequest request,
        CancellationToken cancellationToken = default) =>
        new GenerateEvaHandoff(this).ExecuteAsync(request, cancellationToken);

    private const string GeneratedEvent = "eva_handoff_generated";
    private const string ReusedEvent = "eva_handoff_revision_reused";

    public async Task<EvaHandoffPreparation?> GetPreparationAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty)
        {
            return null;
        }

        var caseData = await caseDataQueries.GetAsync(caseId, cancellationToken);
        if (caseData is null)
        {
            return null;
        }

        var vehicle = await vehicleEvidenceQueries.GetAsync(caseId, cancellationToken);
        var mapping = MapAcceptedCase(caseData, vehicle);
        var reasons = mapping.BlockingReasons.ToList();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var caseState = await (
                from workflow in context.CaseWorkflows.AsNoTracking()
                join caseRecord in context.Cases.AsNoTracking()
                    on workflow.CaseId equals caseRecord.Id
                where workflow.CaseId == caseId
                select new
                {
                    workflow.Version,
                    workflow.State,
                    workflow.ArchivedAtUtc,
                    caseRecord.CustodyState,
                    caseRecord.CustodyConfirmedAtUtc,
                    caseRecord.Type,
                    caseRecord.AuditReference,
                    caseRecord.AuditCustodyRemoteId,
                    caseRecord.AuditCustodyConfirmedAtUtc
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (caseState is null)
        {
            return null;
        }

        var images = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                      && occurrence.SemanticRole == DocumentSemanticRole.Image
                      && version.DocumentId == occurrence.DocumentId
                       && version.IsCurrent
                       && !version.IsLogicallyRemoved
                       && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                       && occurrence.ThirdPartyVehicleConfirmedAtUtc == null
                       && (version.MediaType == "image/jpeg" || version.MediaType == "image/png")
                 orderby occurrence.Ordinal
                select new EvaHandoffImageOption(
                    occurrence.Id,
                    occurrence.DocumentId,
                    version.Id,
                    version.Version,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256,
                    occurrence.Source,
                    occurrence.SourceOccurrenceIdentity,
                    occurrence.Ordinal))
            .ToArrayAsync(cancellationToken);
        reasons.AddRange(EvaHandoffPolicy.Evaluate(new(
            ParseLifecycleState(caseState.State),
            caseState.ArchivedAtUtc is not null,
            caseState.Version,
            caseData.Version,
            IsConfirmedCustody(caseState.CustodyState, caseState.CustodyConfirmedAtUtc),
            string.Equals(caseState.Type, "audit", StringComparison.Ordinal),
            !string.Equals(caseState.Type, "audit", StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(caseState.AuditCustodyRemoteId)
                    && caseState.AuditCustodyConfirmedAtUtc is not null),
            mapping.Source is not null,
            images.Length)));

        var proxyEvidence = await context.EvaFirstHandoffProxies
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        var firstProxyRevisionId = proxyEvidence?.RevisionId;
        var revisions = await context.EvaHandoffRevisions
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.Revision)
            .Select(item => new EvaHandoffRevisionSummary(
                item.Revision,
                item.FileName,
                item.BundleSha256,
                item.JsonSha256,
                item.GeneratedAtUtc,
                item.GeneratedBy,
                firstProxyRevisionId == item.Id))
            .ToArrayAsync(cancellationToken);

        // PLAT-031: null still means "no such case" — whether the hand-off
        // is switched on is a separate fact the caller is told, so the
        // operator surface can stay quiet about a capability that is off
        // without anything having to pretend the case is missing.
        return new(
            caseId,
            caseState.Version,
            caseData.Identity.Reference,
            images,
            revisions,
            proxyEvidence?.RecordedAtUtc,
            reasons.Distinct(StringComparer.Ordinal).ToArray(),
            CaseEvaMapping.IsSwitchedOn(mappingAcceptance));
    }

    public async Task<EvaHandoffRevisionArtifact?> GetRevisionAsync(
        Guid caseId,
        int revision,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (caseId == Guid.Empty || revision <= 0)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var artifact = await context.EvaHandoffRevisions
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.Revision == revision)
            .Select(item => new EvaHandoffRevisionArtifact(
                item.Revision,
                item.FileName,
                item.BundleContent,
                item.BundleSha256))
            .SingleOrDefaultAsync(cancellationToken);
        if (artifact is null)
        {
            return null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSafeBundleFileName(artifact.FileName)
            || !HashesMatch(artifact.BundleSha256, Hash(artifact.Content)))
        {
            throw new InvalidDataException("The persisted EVA handoff artifact failed its integrity check.");
        }

        return artifact;
    }

    public async Task<DownloadEvaHandoffResult> DownloadAsync(
        DownloadEvaHandoffRequest request,
        string normalizedReason,
        string requestHash,
        EvaHandoffPolicyAuthority policy,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await context.EvaHandoffDownloadOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == request.OperationKey, cancellationToken);
        var replayDecision = policy.DecideReplay(
            replay is not null,
            replay is not null
                && replay.CaseId == request.CaseId
                && HashesMatch(replay.RequestHash, requestHash));
        if (replayDecision == EvaOperationReplayDecision.Conflict)
        {
            return new(
                DownloadEvaHandoffOutcome.Conflict,
                null,
                "The EVA download operation key was already used for another request.");
        }
        if (replayDecision == EvaOperationReplayDecision.Replay)
        {
            var replayRevision = await context.EvaHandoffRevisions
                .AsNoTracking()
                .SingleAsync(item => item.Id == replay!.RevisionId, cancellationToken);
            if (replayRevision.Revision != request.Revision)
            {
                return new(
                    DownloadEvaHandoffOutcome.Conflict,
                    null,
                    "The EVA download operation key belongs to another business revision.");
            }
            return new(
                DownloadEvaHandoffOutcome.Replay,
                Artifact(replayRevision),
                "The original EVA download preparation was replayed.");
        }

        var workflow = await context.CaseWorkflows
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken);
        if (workflow is null)
        {
            return new(DownloadEvaHandoffOutcome.NotFound, null, "The case was not found.");
        }
        ArchivedCaseGuard.RequireMutable(workflow);
        var downloadVersionConflict = policy.RenderedVersionConflict(
            request.ExpectedCaseVersion, workflow.Version);
        if (downloadVersionConflict is not null)
        {
            return new(DownloadEvaHandoffOutcome.Conflict, null,
                downloadVersionConflict);
        }
        try
        {
            RequireLease(workflow, request.Actor, request.EditLeaseToken, timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
            when (exception is CaseEditLeaseExpiredException or CaseEditLeaseConflictException)
        {
            return new(DownloadEvaHandoffOutcome.Conflict, null, exception.Message);
        }
        var revision = await context.EvaHandoffRevisions
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId
                && item.Revision == request.Revision, cancellationToken);
        if (revision is null)
        {
            return new(DownloadEvaHandoffOutcome.NotFound, null,
                "The EVA handoff revision was not found.");
        }
        EvaHandoffRevisionArtifact artifact;
        try
        {
            artifact = Artifact(revision);
        }
        catch (InvalidDataException exception)
        {
            return new(DownloadEvaHandoffOutcome.Refused, null, exception.Message);
        }

        var beforeVersion = workflow.Version;
        workflow.Version = checked(workflow.Version + 1);
        ClearLease(workflow);
        var now = timeProvider.GetUtcNow();
        context.EvaHandoffDownloadOperations.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            RevisionId = revision.Id,
            OperationKey = request.OperationKey,
            RequestHash = requestHash,
            Reason = normalizedReason,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            PreparedAtUtc = now
        });
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Workflow = workflow,
            EventType = "eva_handoff_download_prepared",
            OperationKey = request.OperationKey,
            RequestHash = requestHash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = normalizedReason,
            OccurredAtUtc = now,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version,
            ResultJson = JsonSerializer.Serialize(new
            {
                revision = revision.Revision,
                outcome = "prepared"
            })
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = request.CaseId.ToString("D"),
            EventKind = "eva_handoff_download_prepared",
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = request.OperationKey,
            Reason = normalizedReason,
            BeforeJson = JsonSerializer.Serialize(new { workflowVersion = beforeVersion }),
            AfterJson = JsonSerializer.Serialize(new
            {
                workflowVersion = workflow.Version,
                businessRevision = revision.Revision,
                integrity = "verified"
            }),
            PolicyVersion = "eva-handoff-v2"
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(DownloadEvaHandoffOutcome.Conflict, null,
                "The case changed during EVA download preparation. Reload before retrying.");
        }
        return new(DownloadEvaHandoffOutcome.Prepared, artifact,
            "The EVA handoff archive was prepared.");
    }

    public async Task<GenerateEvaHandoffResult> GenerateAsync(
        GenerateEvaHandoffRequest request,
        string requestHash,
        EvaHandoffPolicyAuthority policy,
        CancellationToken cancellationToken = default)
    {
        var operationKey = request.OperationKey;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.EvaHandoffOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);
        var replayDecision = policy.DecideReplay(
            replay is not null,
            replay is not null
                && replay.CaseId == request.CaseId
                && HashesMatch(replay.RequestHash, requestHash));
        if (replayDecision == EvaOperationReplayDecision.Conflict)
        {
            return Conflict("The EVA handoff operation key was already used for a different request.");
        }
        if (replayDecision == EvaOperationReplayDecision.Replay)
        {

            var replayRevision = await context.EvaHandoffRevisions
                .AsNoTracking()
                .SingleAsync(item => item.Id == replay!.RevisionId, cancellationToken);
            var firstProxyOperation = await context.EvaFirstHandoffProxies
                .AsNoTracking()
                .Where(item => item.CaseId == request.CaseId)
                .Select(item => item.OperationKey)
                .SingleOrDefaultAsync(cancellationToken);
            return Generated(
                replayRevision,
                string.Equals(firstProxyOperation, operationKey, StringComparison.Ordinal));
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken);
        if (workflow is null)
        {
            return new(
                GenerateEvaHandoffOutcome.NotFound,
                null,
                ["The case was not found."]);
        }
        var renderedVersionConflict = policy.RenderedVersionConflict(
            request.ExpectedCaseVersion, workflow.Version);
        if (renderedVersionConflict is not null)
        {
            return Conflict(renderedVersionConflict);
        }
        var lifecycle = ParseLifecycleState(workflow.State);
        var stageReasons = policy.Evaluate(new(
            lifecycle,
            workflow.ArchivedAtUtc is not null,
            workflow.Version,
            workflow.Version,
            CaseCustodyConfirmed: true,
            AuditRequired: false,
            AuditCustodyConfirmed: true,
            MappingAccepted: true,
            EligibleImageCount: 1));
        if (stageReasons.Count != 0)
        {
            return new(GenerateEvaHandoffOutcome.Blocked, null, stageReasons);
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        }
        catch (InvalidOperationException exception)
            when (exception is CaseEditLeaseExpiredException or CaseEditLeaseConflictException)
        {
            return Conflict(exception.Message);
        }

        var caseData = await caseDataQueries.GetAsync(request.CaseId, cancellationToken);
        if (caseData is null)
        {
            return new(
                GenerateEvaHandoffOutcome.NotFound,
                null,
                ["The accepted case data was not found."]);
        }
        if (caseData.Version != workflow.Version)
        {
            return Conflict("The accepted case evidence changed during EVA generation. Reload before retrying.");
        }

        var vehicle = await vehicleEvidenceQueries.GetAsync(request.CaseId, cancellationToken);
        var mapping = MapAcceptedCase(caseData, vehicle);
        var reasons = mapping.BlockingReasons.ToList();
        var candidateRows = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>()
                join version in context.Set<DocumentVersionEntity>()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == request.CaseId
                      && version.DocumentId == occurrence.DocumentId
                 orderby occurrence.Ordinal
                select new SelectedDocument(
                    occurrence.Id,
                    occurrence.Ordinal,
                    occurrence.CaseId,
                    occurrence.DocumentId,
                    occurrence.Source,
                    occurrence.SourceOccurrenceIdentity,
                    occurrence.SemanticRole,
                    version.Id,
                    version.DocumentId,
                    version.Version,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256,
                    version.CustodyStatus,
                    version.IsCurrent,
                    version.IsLogicallyRemoved,
                    occurrence.ThirdPartyVehicleConfirmedAtUtc != null))
            .ToArrayAsync(cancellationToken);
        var eligibleVersionIds = policy.SelectEligibleImages(candidateRows.Select(
                selected => new EvaHandoffImageCandidate(
                    selected.OccurrenceId,
                    selected.DocumentId,
                    selected.VersionId,
                    selected.Version,
                    selected.FileName,
                    selected.MediaType,
                    selected.ContentLength,
                    selected.Sha256,
                    selected.SemanticRole,
                    selected.Source,
                    selected.SourceOccurrenceIdentity,
                    selected.CustodyStatus == DocumentCustodyStatus.Confirmed,
                    selected.IsCurrent,
                    selected.IsLogicallyRemoved,
                    selected.IsThirdPartyVehicle,
                    selected.Ordinal)))
            .Select(candidate => candidate.VersionId)
            .ToHashSet();
        var selectedRows = candidateRows
            .Where(selected => eligibleVersionIds.Contains(selected.VersionId))
            .ToArray();
        reasons.AddRange(policy.Evaluate(new(
            ParseLifecycleState(workflow.State),
            workflow.ArchivedAtUtc is not null,
            workflow.Version,
            caseData.Version,
            IsConfirmedCustody(workflow.Case.CustodyState, workflow.Case.CustodyConfirmedAtUtc),
            string.Equals(workflow.Case.Type, "audit", StringComparison.Ordinal),
            !string.Equals(workflow.Case.Type, "audit", StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(workflow.Case.AuditCustodyRemoteId)
                    && workflow.Case.AuditCustodyConfirmedAtUtc is not null),
            mapping.Source is not null,
            selectedRows.Length)));
        if (reasons.Count != 0 || mapping.Source is null)
        {
            return new(
                GenerateEvaHandoffOutcome.Blocked,
                null,
                reasons.Distinct(StringComparer.Ordinal).ToArray());
        }

        var bundleImages = new List<EvaBundleImage>(selectedRows.Length);
        foreach (var selected in selectedRows)
        {
            if (selected.ContentLength > int.MaxValue)
            {
                return Blocked($"The selected image '{selected.FileName}' is too large for the offline EVA handoff.");
            }

            await using var content = await contentStore.OpenReadVersionAsync(
                new(
                    request.CaseId,
                    workflow.Case.Reference,
                    selected.OccurrenceId,
                    selected.Ordinal,
                    selected.DocumentId,
                    selected.VersionId,
                    selected.Version,
                    selected.SemanticRole,
                    selected.FileName,
                    selected.MediaType),
                selected.Sha256,
                selected.ContentLength,
                cancellationToken);
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int)selected.ContentLength));
            await content.ReadExactlyAsync(bytes, cancellationToken);
            bundleImages.Add(new(
                selected.OccurrenceId,
                selected.DocumentId,
                selected.VersionId,
                selected.Version,
                selected.FileName,
                selected.MediaType,
                selected.SemanticRole,
                selected.Source,
                selected.SourceOccurrenceIdentity,
                bytes,
                selected.Sha256,
                CustodyConfirmed: true,
                IsCurrent: true,
                selected.Ordinal));
        }

        var bundle = EvaBundleSchema.CreateOfflineReplay(
            mapping.Source,
            new(bundleImages));
        var existingRevision = await context.EvaHandoffRevisions
            .SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId
                        && item.InputFingerprint == bundle.Sha256,
                cancellationToken);

        var maximumRevision = await context.EvaHandoffRevisions
            .Where(item => item.CaseId == request.CaseId)
            .Select(item => (int?)item.Revision)
            .MaxAsync(cancellationToken) ?? 0;
        var hasFirstProxy = await context.EvaFirstHandoffProxies
            .AnyAsync(item => item.CaseId == request.CaseId, cancellationToken);
        var revisionDecision = policy.DecideRevision(
            existingRevision?.Revision,
            maximumRevision,
            hasFirstProxy);
        var firstSentToEngineerRecorded = false;
        EvaHandoffRevisionEntity revision;
        if (!revisionDecision.ReuseExisting)
        {
            revision = NewRevision(
                request,
                revisionDecision.BusinessRevision,
                bundle,
                now);
            context.EvaHandoffRevisions.Add(revision);

            if (revisionDecision.RecordFirstProxy)
            {
                var receipt = await proxy.RecordFirstGenerationAsync(
                    new(
                        request.CaseId,
                        revision.Revision,
                        bundle.Sha256,
                        request.Actor,
                        operationKey),
                    cancellationToken);
                if (receipt.ClaimsExternalDelivery || receipt.ClaimsEngineerAssignment)
                {
                    throw new InvalidDataException(
                        "The offline EVA proxy must not claim delivery or Engineer assignment.");
                }

                context.EvaFirstHandoffProxies.Add(new()
                {
                    CaseId = request.CaseId,
                    RevisionId = revision.Id,
                    OperationKey = operationKey,
                    AdapterKey = receipt.AdapterKey,
                    AdapterVersion = receipt.AdapterVersion,
                    RecordedAtUtc = receipt.RecordedAtUtc,
                    ActorSubjectId = request.Actor.SubjectId,
                    ClaimsExternalDelivery = false,
                    ClaimsEngineerAssignment = false
                });
                firstSentToEngineerRecorded = true;
            }
        }
        else
        {
            revision = existingRevision
                ?? throw new InvalidOperationException(
                    "Core selected revision replay without a matching persisted revision.");
        }

        context.EvaHandoffOperations.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            RevisionId = revision.Id,
            OperationKey = operationKey,
            RequestHash = requestHash,
            RecordedAtUtc = now,
            ActorSubjectId = request.Actor.SubjectId
        });

        var beforeVersion = workflow.Version;
        workflow.Version = checked(workflow.Version + 1);
        ClearLease(workflow);
        AddAuditEvidence(
            context,
            workflow,
            request,
            operationKey,
            requestHash,
            existingRevision is null ? GeneratedEvent : ReusedEvent,
            beforeVersion,
            workflow.Version,
            revision,
            firstSentToEngineerRecorded,
            now);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The case changed during EVA generation. Reload before retrying.");
        }

        return Generated(revision, firstSentToEngineerRecorded);
    }

    /// <summary>
    /// CASE-019: the operator's own download of a case as the EVA-format
    /// archive. A read — it opens no transaction, moves no case version,
    /// records no revision and writes no first-hand-off proxy. Generating a
    /// hand-off remains the only thing that does any of that.
    /// </summary>
    /// Explicit, so the class keeps one unambiguous ExecuteAsync — generating
    /// a hand-off — and an export is reached only through its own port.
    async Task<ExportCaseBundleResult?> IExportCaseBundle.ExecuteAsync(
        ExportCaseBundleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            return null;
        }

        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        var caseData = await caseDataQueries.GetAsync(request.CaseId, cancellationToken);
        if (caseData is null)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var caseRecord = await context.Cases
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CaseId, cancellationToken);
        if (caseRecord is null)
        {
            return null;
        }

        var vehicle = await vehicleEvidenceQueries.GetAsync(request.CaseId, cancellationToken);
        var export = CaseEvaMapping.MapForOperatorExport(
            BuildEvidence(caseData, vehicle, includeSuggestions: true),
            mappingAcceptance,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        if (export.Source is null)
        {
            return new(null, export.UnrecordedFields, export.BlockingReasons);
        }

        var images = await LoadEligibleImagesAsync(
            context,
            request.CaseId,
            caseRecord.Reference,
            cancellationToken);
        if (images.Count == 0)
        {
            return new(
                null,
                export.UnrecordedFields,
                [EvaHandoffPolicy.NoRetainedImagesReason]);
        }

        return new(
            EvaBundleSchema.CreateOfflineReplay(export.Source, new(images)),
            export.UnrecordedFields,
            []);
    }

    /// <summary>
    /// Every eligible retained photograph of a case, with its content, chosen
    /// by the same <see cref="EvaHandoffPolicy.SelectEligibleImages"/> the
    /// hand-off uses — so an operator export and a hand-off never disagree
    /// about which images belong to a case.
    /// </summary>
    private async Task<List<EvaBundleImage>> LoadEligibleImagesAsync(
        PegasusDbContext context,
        Guid caseId,
        string caseReference,
        CancellationToken cancellationToken)
    {
        var candidateRows = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                      && version.DocumentId == occurrence.DocumentId
                orderby occurrence.Ordinal
                select new SelectedDocument(
                    occurrence.Id,
                    occurrence.Ordinal,
                    occurrence.CaseId,
                    occurrence.DocumentId,
                    occurrence.Source,
                    occurrence.SourceOccurrenceIdentity,
                    occurrence.SemanticRole,
                    version.Id,
                    version.DocumentId,
                    version.Version,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256,
                    version.CustodyStatus,
                    version.IsCurrent,
                    version.IsLogicallyRemoved,
                    occurrence.ThirdPartyVehicleConfirmedAtUtc != null))
            .ToArrayAsync(cancellationToken);
        var eligibleVersionIds = EvaHandoffPolicy.SelectEligibleImages(candidateRows.Select(
                selected => new EvaHandoffImageCandidate(
                    selected.OccurrenceId,
                    selected.DocumentId,
                    selected.VersionId,
                    selected.Version,
                    selected.FileName,
                    selected.MediaType,
                    selected.ContentLength,
                    selected.Sha256,
                    selected.SemanticRole,
                    selected.Source,
                    selected.SourceOccurrenceIdentity,
                    selected.CustodyStatus == DocumentCustodyStatus.Confirmed,
                    selected.IsCurrent,
                    selected.IsLogicallyRemoved,
                    selected.IsThirdPartyVehicle,
                    selected.Ordinal)))
            .Select(candidate => candidate.VersionId)
            .ToHashSet();

        var images = new List<EvaBundleImage>();
        foreach (var selected in candidateRows.Where(
            selected => eligibleVersionIds.Contains(selected.VersionId)
                        && selected.ContentLength <= int.MaxValue))
        {
            await using var content = await contentStore.OpenReadVersionAsync(
                new(
                    caseId,
                    caseReference,
                    selected.OccurrenceId,
                    selected.Ordinal,
                    selected.DocumentId,
                    selected.VersionId,
                    selected.Version,
                    selected.SemanticRole,
                    selected.FileName,
                    selected.MediaType),
                selected.Sha256,
                selected.ContentLength,
                cancellationToken);
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int)selected.ContentLength));
            await content.ReadExactlyAsync(bytes, cancellationToken);
            images.Add(new(
                selected.OccurrenceId,
                selected.DocumentId,
                selected.VersionId,
                selected.Version,
                selected.FileName,
                selected.MediaType,
                selected.SemanticRole,
                selected.Source,
                selected.SourceOccurrenceIdentity,
                bytes,
                selected.Sha256,
                CustodyConfirmed: true,
                IsCurrent: true,
                selected.Ordinal));
        }

        return images;
    }

    private EvaMappingResult MapAcceptedCase(
        CaseDataProjection caseData,
        CaseVehicleEvidence? vehicle) =>
        CaseEvaMapping.MapForProduction(
            BuildEvidence(caseData, vehicle, includeSuggestions: false),
            mappingAcceptance);

    /// <summary>
    /// The thirteen EVA fields read off one case, written once.
    ///
    /// <paramref name="includeSuggestions"/> is the whole difference between
    /// the hand-off and an operator export (CASE-019), and it is a difference
    /// in what counts as evidence, not in what the fields are. False is the
    /// hand-off: only a staff-confirmed or extracted value counts, which is
    /// the bar EVA delivery has always had. True is the operator's own
    /// download: a suggested value counts too, and travels with its real
    /// suggested status — which is how the lookup-derived mileage ENG-013
    /// writes reaches an export without ever being able to reach a hand-off.
    /// </summary>
    private static EvaAcceptedCaseEvidence BuildEvidence(
        CaseDataProjection caseData,
        CaseVehicleEvidence? vehicle,
        bool includeSuggestions)
    {
        var caseId = caseData.Identity.CaseId;
        var inspection = ResolveInspection(caseData);
        var acceptedVehicle = vehicle?.CaseId == caseId
            ? vehicle.Confirmed
            : null;
        return new EvaAcceptedCaseEvidence(
            caseId,
            caseData.Version,
            caseData.AcceptedAtUtc != default,
            caseData.Completeness.Values.InstructionComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            caseData.Completeness.Values.ImagesComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            new(
                caseData.Identity.Reference,
                EvaEvidenceStatus.Accepted,
                $"case-identity:{caseId:D}",
                "case-reference/v1"),
            FromCaseField(caseData.Provider.WorkProviderCode, static value => value, includeSuggestions),
            Fallback(
                FromVehicleField(acceptedVehicle?.Registration, static value => value),
                caseData.Vehicle.Registration,
                static value => value,
                includeSuggestions),
            Fallback(
                VehicleModel(acceptedVehicle),
                caseData.Vehicle.Model,
                static value => value,
                includeSuggestions),
            FromCaseField(caseData.Claimant.Name, static value => value, includeSuggestions),
            FromCaseField(caseData.Accident.IncidentDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), includeSuggestions),
            FromCaseField(caseData.Instruction.InstructionDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), includeSuggestions),
            FromCaseField(caseData.Inspection.InspectionDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), includeSuggestions),
            inspection,
            FromCaseField(caseData.Accident.Circumstances, static value => value, includeSuggestions),
            FromCaseField(caseData.Instruction.VatStatus, static value => value, includeSuggestions),
            Fallback(
                FromVehicleField(acceptedVehicle?.Mileage, static value => value.ToString(CultureInfo.InvariantCulture)),
                caseData.Vehicle.Mileage,
                static value => value.ToString(CultureInfo.InvariantCulture),
                includeSuggestions),
            Fallback(
                FromVehicleField(acceptedVehicle?.MileageUnit, static value => value switch
                {
                    VehicleMileageUnit.Miles => "miles",
                    VehicleMileageUnit.Kilometres => "kilometres",
                    _ => value.ToString()
                }),
                caseData.Vehicle.MileageUnit,
                static value => value.ToLowerInvariant(),
                includeSuggestions));
    }

    private static EvaAddressResolution ResolveInspection(CaseDataProjection caseData)
    {
        var mode = Accepted(caseData.Inspection.Mode);
        var address = Accepted(caseData.Inspection.Address);
        if (mode is null || address is null)
        {
            return new(
                mode?.Value == CaseInspectionMode.ImageBasedAssessment
                    ? EvaInspectionMode.ImageBasedAssessment
                    : EvaInspectionMode.PhysicalAddress,
                MissingEvidence);
        }

        var modeEvidence = FromCaseValue(mode, static value => value.ToString());
        var addressEvidence = FromCaseValue(address, static value => value);
        var evidence = addressEvidence with
        {
            Status = modeEvidence.Status == EvaEvidenceStatus.Corrected
                     || addressEvidence.Status == EvaEvidenceStatus.Corrected
                ? EvaEvidenceStatus.Corrected
                : EvaEvidenceStatus.Accepted,
            Source = $"{modeEvidence.Source}|{addressEvidence.Source}",
            SourceVersion = $"{modeEvidence.SourceVersion}|{addressEvidence.SourceVersion}"
        };
        return mode.Value switch
        {
            CaseInspectionMode.ImageBasedAssessment => new(
                EvaInspectionMode.ImageBasedAssessment,
                evidence),
            CaseInspectionMode.PhysicalAddress
                when !string.Equals(
                    address.Value.Trim(),
                    CaseEvaMapping.ImageBasedAssessment,
                    StringComparison.Ordinal) => new(
                        EvaInspectionMode.PhysicalAddress,
                        evidence),
            _ => new(EvaInspectionMode.PhysicalAddress, evidence with
            {
                Status = EvaEvidenceStatus.Suggested
            })
        };
    }

    private static EvaEvidenceValue VehicleModel(ConfirmedVehicleEvidence? vehicle)
    {
        var values = new List<EvaEvidenceValue>(2);
        if (vehicle?.Make is not null)
        {
            values.Add(FromVehicleField(vehicle.Make, static value => value));
        }
        if (vehicle?.Model is not null)
        {
            values.Add(FromVehicleField(vehicle.Model, static value => value));
        }
        if (values.Count == 0)
        {
            return MissingEvidence;
        }

        return values.Aggregate(Combine);
    }

    private static EvaEvidenceValue FromCaseField<T>(
        CaseField<T> field,
        Func<T, string> format,
        bool includeSuggestions)
        where T : notnull =>
        Accepted(field) is { } value
            ? FromCaseValue(value, format)
            : includeSuggestions && field.Suggestion is { } suggestion
                ? FromCaseValue(suggestion, format) with { Status = EvaEvidenceStatus.Suggested }
                : MissingEvidence;

    /// <summary>
    /// The vehicle fields have their own confirmed record, which is the only
    /// thing a hand-off will read. An operator export falls back to the case's
    /// own field when that record has nothing — which is where ENG-013 writes
    /// what the DVLA and DVSA lookup found, so an export carries a mileage the
    /// documents never supplied. It never overrides a confirmed value.
    /// </summary>
    private static EvaEvidenceValue Fallback<T>(
        EvaEvidenceValue confirmed,
        CaseField<T> field,
        Func<T, string> format,
        bool includeSuggestions)
        where T : notnull =>
        !string.IsNullOrWhiteSpace(confirmed.Value) || !includeSuggestions
            ? confirmed
            : FromCaseField(field, format, includeSuggestions: true);

    private static CaseDataValue<T>? Accepted<T>(CaseField<T> field)
        where T : notnull =>
        field.Confirmed is { IsAccepted: true } confirmed
            ? confirmed
            : field.Fact is { IsAccepted: true } fact
                ? fact
                : null;

    private static EvaEvidenceValue FromCaseValue<T>(
        CaseDataValue<T> value,
        Func<T, string> format)
        where T : notnull
    {
        var sourceVersion = !string.IsNullOrWhiteSpace(value.Source.PolicyKey)
                            && value.Source.PolicyVersion > 0
            ? $"{value.Source.PolicyKey.Trim()}/v{value.Source.PolicyVersion}"
            : string.Empty;
        var confirmed = value.ConfirmedByActor is null
            ? string.Empty
            : $";confirmed={value.ConfirmedByActor}@{value.ConfirmedAtUtc:O}";
        return new(
            format(value.Value),
            value.Source.Kind == CaseDataSourceKind.StaffCorrection
                ? EvaEvidenceStatus.Corrected
                : EvaEvidenceStatus.Accepted,
            $"case-data:{value.Source.Kind}:{value.Source.Identity}:{value.Source.Label}{confirmed}",
            sourceVersion);
    }

    private static EvaEvidenceValue FromVehicleField<T>(
        ConfirmedVehicleField<T>? field,
        Func<T, string> format)
        where T : notnull
    {
        if (field is null)
        {
            return MissingEvidence;
        }

        var external = field.ExternalProvenance is null
            ? string.Empty
            : $";provider={field.ExternalProvenance.Provider};response={field.ExternalProvenance.ResponseIdentity};observed={field.ExternalProvenance.RetrievedAtUtc:O}";
        var sourceVersion = !string.IsNullOrWhiteSpace(field.PolicyKey)
                            && field.PolicyVersion > 0
            ? $"{field.PolicyKey.Trim()}/v{field.PolicyVersion}"
            : string.Empty;
        return new(
            format(field.Value),
            field.SourceKind.Equals(CaseDataCodes.StaffCorrection, StringComparison.Ordinal)
                ? EvaEvidenceStatus.Corrected
                : EvaEvidenceStatus.Accepted,
            $"vehicle:{field.SourceKind}:{field.SourceIdentity}:{field.SourceLabel};confirmed={field.ConfirmedByActor}@{field.ConfirmedAtUtc:O}{external}",
            sourceVersion);
    }

    private static EvaEvidenceValue Combine(EvaEvidenceValue first, EvaEvidenceValue second) => new(
        string.Join(' ', new[] { first.Value, second.Value }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())),
        first.Status == EvaEvidenceStatus.Corrected || second.Status == EvaEvidenceStatus.Corrected
            ? EvaEvidenceStatus.Corrected
            : first.IsAccepted && second.IsAccepted
                ? EvaEvidenceStatus.Accepted
                : EvaEvidenceStatus.Suggested,
        $"{first.Source}|{second.Source}",
        $"{first.SourceVersion}|{second.SourceVersion}");

    private static EvaEvidenceValue MissingEvidence { get; } =
        new(null, EvaEvidenceStatus.Unrecorded, "unrecorded", "unrecorded");

    private static EvaHandoffRevisionEntity NewRevision(
        GenerateEvaHandoffRequest request,
        int revision,
        EvaBundle bundle,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = request.CaseId,
        Revision = revision,
        AcceptedCaseVersion = request.ExpectedCaseVersion,
        SchemaVersion = EvaBundleSchema.SchemaVersion,
        InputFingerprint = bundle.Sha256,
        FileName = $"{Path.GetFileNameWithoutExtension(bundle.FileName)}-Revision-{revision:000}.zip",
        BundleContent = bundle.Content,
        BundleSha256 = bundle.Sha256,
        JsonContent = bundle.JsonContent,
        JsonSha256 = bundle.JsonSha256,
        ProvenanceContent = bundle.ProvenanceContent,
        ProvenanceSha256 = bundle.ProvenanceSha256,
        ManifestContent = bundle.ManifestContent,
        GeneratedAtUtc = now,
        GeneratedBy = request.Actor.SubjectId
    };

    private static GenerateEvaHandoffResult Generated(
        EvaHandoffRevisionEntity revision,
        bool firstSentToEngineerRecorded) => new(
        GenerateEvaHandoffOutcome.Generated,
        Bundle(revision),
        [],
        revision.Revision,
        firstSentToEngineerRecorded);

    private static EvaBundle Bundle(EvaHandoffRevisionEntity revision) => new(
        revision.BundleContent,
        revision.BundleSha256,
        revision.JsonContent,
        revision.JsonSha256,
        revision.ProvenanceContent,
        revision.ProvenanceSha256,
        revision.ManifestContent,
        revision.FileName);


    private static void AddAuditEvidence(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        GenerateEvaHandoffRequest request,
        string operationKey,
        string requestHash,
        string eventType,
        long beforeVersion,
        long afterVersion,
        EvaHandoffRevisionEntity revision,
        bool firstSentToEngineerRecorded,
        DateTimeOffset now)
    {
        var evidenceReason = firstSentToEngineerRecorded
            ? $"Generated immutable EVA handoff revision {revision.Revision} and recorded the once-per-case First sent to Engineer export proxy. No delivery or Engineer assignment was claimed."
            : eventType == ReusedEvent
                ? $"Reused immutable EVA handoff revision {revision.Revision} for unchanged accepted inputs. No delivery or Engineer assignment was claimed."
                : $"Generated immutable EVA handoff revision {revision.Revision}. No delivery or Engineer assignment was claimed.";
        var reason = $"{request.Reason.Trim()} {evidenceReason}";
        var roles = RolesJson(request.Actor);
        var result = JsonSerializer.Serialize(new
        {
            revision.Revision,
            revision.FileName,
            Sha256 = revision.BundleSha256,
            FirstSentToEngineerRecorded = firstSentToEngineerRecorded
        });
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = roles,
            Reason = reason,
            OccurredAtUtc = now,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion,
            ResultJson = result
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = request.CaseId.ToString("D"),
            EventKind = eventType,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = roles,
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = JsonSerializer.Serialize(new { Version = beforeVersion }),
            AfterJson = result,
            PolicyVersion = $"{CaseEvaMapping.MappingKey}/v{CaseEvaMapping.MappingVersion}"
        });
    }

    private static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string token,
        DateTimeOffset now) =>
        CaseMutationGuard.RequireLease(workflow, actor, token, now);

    private static void ClearLease(CaseWorkflowEntity workflow) =>
        CaseMutationGuard.ClearLease(workflow);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();


    private static bool HashesMatch(string? stored, string expected)
    {
        if (stored is null || stored.Length != expected.Length)
        {
            return false;
        }
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(stored),
                Convert.FromHexString(expected));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsSafeBundleFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= 260
        && fileName.Equals(fileName.Trim(), StringComparison.Ordinal)
        && fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
        && !fileName.Contains('/')
        && !fileName.Contains('\\')
        && !fileName.Any(char.IsControl)
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static EvaHandoffRevisionArtifact Artifact(EvaHandoffRevisionEntity revision)
    {
        if (!IsSafeBundleFileName(revision.FileName)
            || revision.BundleContent.Length == 0
            || revision.BundleSha256.Length != 64
            || !HashesMatch(revision.BundleSha256, Hash(revision.BundleContent)))
        {
            throw new InvalidDataException("The stored EVA handoff archive failed integrity validation.");
        }

        return new(
            revision.Revision,
            revision.FileName,
            revision.BundleContent,
            revision.BundleSha256);
    }

    private static bool IsTerminalWorkflow(string state) =>
        Enum.TryParse<CaseLifecycleState>(state, out var parsed)
        && CaseLifecycleRules.IsTerminal(parsed);

    private static CaseLifecycleState ParseLifecycleState(string state) =>
        Enum.TryParse<CaseLifecycleState>(state, ignoreCase: false, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidDataException($"Unknown case lifecycle state '{state}'.");

    private static bool IsConfirmedCustody(string state, DateTimeOffset? confirmedAtUtc) =>
        state.Equals("confirmed", StringComparison.OrdinalIgnoreCase)
        && confirmedAtUtc is not null;

    private static bool IsSupportedImage(string mediaType) =>
        mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase);

    private static string RolesJson(ActionActor actor) =>
        JsonSerializer.Serialize(actor.Roles.OrderBy(role => role).Select(role => role.ToString()));

    private static GenerateEvaHandoffResult Blocked(string reason) =>
        new(GenerateEvaHandoffOutcome.Blocked, null, [reason]);

    private static GenerateEvaHandoffResult Conflict(string reason) =>
        new(GenerateEvaHandoffOutcome.Conflict, null, [reason]);

    private sealed record SelectedDocument(
        Guid OccurrenceId,
        int Ordinal,
        Guid CaseId,
        Guid DocumentId,
        DocumentSource Source,
        string SourceOccurrenceIdentity,
        DocumentSemanticRole SemanticRole,
        Guid VersionId,
        Guid VersionDocumentId,
        int Version,
        string FileName,
        string MediaType,
        long ContentLength,
        string Sha256,
        DocumentCustodyStatus CustodyStatus,
        bool IsCurrent,
        bool IsLogicallyRemoved,
        bool IsThirdPartyVehicle);
}
