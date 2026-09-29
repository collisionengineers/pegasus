using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfDocumentCustodyStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    IDocumentContentStore contentStore,
    TimeProvider timeProvider) :
    IAddCaseDocument,
    IDownloadCaseDocument,
    IGetCaseDocumentMetadata,
    IReadCaseDocumentPreview,
    IExportCaseDocuments,
    ILogicallyRemoveDocument,
    IMarkAsOriginalReportStore,
    IRecogniseOriginalReportStore,
    ITagCaseImage,
    IUntagCaseImage,
    ICreateImageTag,
    ISetCaseImageInReport
{
    internal const string OriginalReportRecordedEventKind = "original_report_recorded";
    /// <summary>The two history words an image tag writes on the case.</summary>
    internal const string ImageTaggedEventKind = "case_image_tagged";
    internal const string ImageUntaggedEventKind = "case_image_untagged";
    /// <summary>The history words for putting an image in the report or taking it out.</summary>
    internal const string ImageInReportEventKind = "case_image_in_report";
    internal const string ImageOutOfReportEventKind = "case_image_out_of_report";

    public async Task<AddCaseDocumentResult> ExecuteAsync(
        AddCaseDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateAddCommand(command);
        var contentHash = ComputeSha256(command.Content.Span);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var replayOccurrence = await context.Set<DocumentOccurrenceEntity>()
            .SingleOrDefaultAsync(
                occurrence => occurrence.CaseId == command.CaseId
                    && occurrence.OperationKey == command.OperationKey,
                cancellationToken);
        if (replayOccurrence is not null)
        {
            var replayVersion = await context.Set<DocumentVersionEntity>()
                .SingleAsync(version => version.Id == replayOccurrence.VersionId, cancellationToken);
            EnsureReplayMatches(command, replayOccurrence, replayVersion, contentHash);
            return new(ToOccurrence(replayOccurrence), ToVersion(replayVersion), true);
        }
        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            timeProvider.GetUtcNow());
        var now = timeProvider.GetUtcNow();
        var pending = await PrepareAddAsync(
            context, contentStore, workflow, command, contentHash, now, cancellationToken);
        try
        {
            CaseMutationGuard.Complete(workflow);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return pending.Result with { IsReplay = false };
        }
        catch (Exception exception)
        {
            Exception? rollbackFailure = null;
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception caught)
            {
                rollbackFailure = caught;
            }

            try
            {
                if (pending.ContentWrite.Disposition == DocumentContentWriteDisposition.Created)
                {
                    await DocumentContentRollback.RemoveOrphanAsync(
                        dbContextFactory,
                        contentStore,
                        command.CaseId,
                        workflow.Case.Reference,
                        pending.Version.Id,
                        exception);
                }
            }
            catch (Exception cleanupFailure) when (rollbackFailure is not null)
            {
                throw new AggregateException(
                    "The document database write failed, its rollback could not be confirmed, and custody cleanup did not complete.",
                    exception,
                    rollbackFailure,
                    cleanupFailure);
            }

            if (rollbackFailure is not null)
            {
                throw new AggregateException(
                    "The document database transaction failed and its rollback could not be confirmed.",
                    exception,
                    rollbackFailure);
            }

            throw;
        }
    }
    async Task<CaseDocumentMetadata?> IGetCaseDocumentMetadata.ExecuteAsync(
        GetCaseDocumentMetadataQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateActor(query.Actor);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await (
            from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                on occurrence.DocumentId equals version.DocumentId
            where occurrence.CaseId == query.CaseId
                && occurrence.Id == query.OccurrenceId
                && version.Id == query.VersionId
                && occurrence.VersionId == version.Id
                && version.DocumentId == occurrence.DocumentId
                && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                && !version.IsLogicallyRemoved
            select new CaseDocumentMetadata(
                query.CaseId,
                occurrence.Id,
                occurrence.DocumentId,
                version.Id,
                version.FileName,
                version.MediaType,
                version.ContentLength,
                version.Sha256))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The occurrence lookup a preview needs, and nothing else. It
    /// takes no operation key, writes no <c>ActionHistory</c> row and opens no
    /// content: the caller reads the bytes through
    /// <see cref="IReadLogicalDocumentVersion"/>, which verifies the same
    /// custody hash on every read. The Case-membership rule is the audited
    /// download's rule, restated by the same query shape rather than relaxed.
    /// A version whose custody is still <c>Pending</c> is returned with that
    /// state, so the caller can answer "not yet" instead of "no such file".
    /// </summary>
    async Task<CaseDocumentPreview?> IReadCaseDocumentPreview.ExecuteAsync(
        CaseDocumentPreviewQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateActor(query.Actor);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await (
            from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                on occurrence.DocumentId equals version.DocumentId
            where occurrence.CaseId == query.CaseId
                && occurrence.Id == query.OccurrenceId
                && version.Id == query.VersionId
                && occurrence.VersionId == version.Id
                && version.DocumentId == occurrence.DocumentId
                && !version.IsLogicallyRemoved
            select new CaseDocumentPreview(
                query.CaseId,
                occurrence.Id,
                occurrence.DocumentId,
                version.Id,
                version.FileName,
                version.MediaType,
                version.ContentLength,
                version.Sha256,
                version.CustodyStatus))
            .SingleOrDefaultAsync(cancellationToken);
    }

    async Task<DocumentDownload?> IDownloadCaseDocument.ExecuteAsync(
        DownloadCaseDocumentQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateActor(query.Actor);
        var operationKey = ValidateOperationKey(query.OperationKey);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var caseIdentity = await context.Set<CaseEntity>()
            .Where(value => value.Id == query.CaseId)
            .Select(value => new { value.Reference, value.CustodyRootRemoteId, value.AuditCustodyRemoteId })
            .SingleOrDefaultAsync(cancellationToken);
        if (caseIdentity is null)
        {
            return null;
        }

        var history = await FindDocumentHistoryAsync(context, operationKey, cancellationToken);
        var item = await (
            from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                on occurrence.DocumentId equals version.DocumentId
            where occurrence.CaseId == query.CaseId
                && occurrence.Id == query.OccurrenceId
                && version.Id == query.VersionId
                && version.DocumentId == occurrence.DocumentId
                && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                && !version.IsLogicallyRemoved
            select new
            {
                Occurrence = occurrence,
                Version = version,
                Folder = context.Set<CaseDocumentEntity>()
                    .Where(document => document.Id == occurrence.DocumentId)
                    .Select(document => document.CustodyFolder)
                    .First()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            if (history is not null)
            {
                throw new InvalidOperationException(
                    "The document operation key was already used for another audited action.");
            }

            return null;
        }

        var afterJson = DocumentActionHistory.Serialize(new DocumentDownloadHistoryValue(
            query.CaseId,
            query.OccurrenceId,
            query.VersionId,
            item.Version.Sha256));
        if (history is not null)
        {
            DocumentActionHistory.RequireExactReplay(
                history,
                "case_document",
                query.VersionId.ToString("D"),
                "document_downloaded",
                query.Actor,
                reason: null,
                afterJson: afterJson);
        }

        var stream = await contentStore.OpenReadVersionAsync(
            Address(
                query.CaseId,
                caseIdentity.Reference,
                CaseCustodyFolders.RootOf(
                    item.Folder, caseIdentity.CustodyRootRemoteId, caseIdentity.AuditCustodyRemoteId),
                item.Occurrence,
                item.Version),
            item.Version.Sha256,
            item.Version.ContentLength,
            cancellationToken);
        try
        {
            if (history is null)
            {
                context.ActionHistory.Add(DocumentActionHistory.Succeeded(
                    "case_document",
                    query.VersionId.ToString("D"),
                    "document_downloaded",
                    query.Actor,
                    timeProvider.GetUtcNow(),
                    operationKey,
                    afterJson: afterJson));
                await context.SaveChangesAsync(cancellationToken);
            }

            return new(
                stream,
                item.Version.FileName,
                item.Version.MediaType,
                item.Version.ContentLength,
                item.Version.Sha256);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    async Task<DocumentExport> IExportCaseDocuments.ExecuteAsync(
        ExportCaseDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateActor(command.Actor);
        var operationKey = ValidateOperationKey(command.OperationKey);
        ArgumentNullException.ThrowIfNull(command.Selections);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.MaximumArchiveBytes);
        if (command.Selections.Count == 0 || command.Selections.Count != command.Selections.Distinct().Count())
        {
            throw new ArgumentException("At least one unique document selection is required.", nameof(command));
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var caseIdentity = await context.Set<CaseEntity>()
            .Where(value => value.Id == command.CaseId)
            .Select(value => new { value.Reference, value.CustodyRootRemoteId, value.AuditCustodyRemoteId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The case is unavailable.");
        var caseRootRemoteId = caseIdentity.CustodyRootRemoteId;

        // A case exports only in Review (operator decision 2026-08-04). This
        // is a precondition, not a greyed button: export had no stage
        // condition at all, so the rule existed nowhere until now and any
        // caller could take the bundle at any stage.
        var stage = await context.CaseWorkflows
            .AsNoTracking()
            .Where(value => value.CaseId == command.CaseId)
            .Select(value => value.State)
            .SingleOrDefaultAsync(cancellationToken);
        if (stage != nameof(CaseLifecycleState.Review))
        {
            throw new CaseNotInReviewException(command.CaseId);
        }

        var history = await FindDocumentHistoryAsync(context, operationKey, cancellationToken);

        var requested = command.Selections
            .OrderBy(value => value.OccurrenceId)
            .ThenBy(value => value.VersionId)
            .ToArray();
        var items = new List<ExportItem>(requested.Length);
        var selectedContentLength = 0L;
        foreach (var selection in requested)
        {
            var item = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.DocumentId equals version.DocumentId
                where occurrence.CaseId == command.CaseId
                    && occurrence.Id == selection.OccurrenceId
                    && version.Id == selection.VersionId
                    && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                    && !version.IsLogicallyRemoved
                select new ExportItem(
                    occurrence,
                    version,
                    context.Set<CaseDocumentEntity>()
                        .Where(document => document.Id == occurrence.DocumentId)
                        .Select(document => document.CustodyFolder)
                        .First()))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("A selected document version is unavailable.");
            if (item.Version.ContentLength < 0)
            {
                throw new InvalidDataException("A selected document has an invalid custody length.");
            }
            if (item.Version.ContentLength > command.MaximumArchiveBytes - selectedContentLength)
            {
                throw new InvalidOperationException(
                    "The selected documents exceed the maximum archive byte limit.");
            }

            selectedContentLength += item.Version.ContentLength;
            items.Add(item);
        }

        var afterJson = DocumentActionHistory.Serialize(new DocumentExportHistoryValue(
            command.CaseId,
            items.Select(item => new DocumentExportHistoryItem(
                    item.Occurrence.Id,
                    item.Version.Id,
                    item.Version.Sha256))
                .ToArray()));
        if (history is not null)
        {
            DocumentActionHistory.RequireExactReplay(
                history,
                "case_document",
                command.CaseId.ToString("D"),
                "documents_exported",
                command.Actor,
                reason: null,
                afterJson: afterJson);
        }

        var export = await BuildExportAsync(
            command.CaseId,
            caseIdentity.Reference,
            caseRootRemoteId,
            caseIdentity.AuditCustodyRemoteId,
            items,
            command.MaximumArchiveBytes,
            cancellationToken);
        try
        {
            if (history is null)
            {
                var workflow = await RequireWorkflowAsync(
                    context,
                    command.CaseId,
                    cancellationToken);
                CaseMutationGuard.Require(
                    workflow,
                    command.Actor,
                    command.ExpectedCaseVersion,
                    command.EditLeaseToken,
                    timeProvider.GetUtcNow());
                context.ActionHistory.Add(DocumentActionHistory.Succeeded(
                    "case_document",
                    command.CaseId.ToString("D"),
                    "documents_exported",
                    command.Actor,
                    timeProvider.GetUtcNow(),
                    operationKey,
                    afterJson: afterJson));
                CaseMutationGuard.Complete(workflow);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return export;
        }
        catch
        {
            await export.DisposeAsync();
            throw;
        }
    }

    async Task ILogicallyRemoveDocument.ExecuteAsync(
        LogicallyRemoveDocumentCommand command,
        CancellationToken cancellationToken)
    {
        ValidateActor(command.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OperationKey);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var occurrence = await context.Set<DocumentOccurrenceEntity>()
            .SingleOrDefaultAsync(
                value => value.CaseId == command.CaseId && value.Id == command.OccurrenceId,
                cancellationToken)
            ?? throw new InvalidOperationException("The document occurrence is unavailable.");
        var version = await context.Set<DocumentVersionEntity>()
            .SingleAsync(value => value.Id == occurrence.VersionId, cancellationToken);
        if (version.IsLogicallyRemoved)
        {
            if (!string.Equals(version.RemovalReason, command.Reason.Trim(), StringComparison.Ordinal)
                || !string.Equals(version.RemovalOperationKey, command.OperationKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The document has already been removed for a different reason.");
            }

            return;
        }
        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            timeProvider.GetUtcNow());

        version.IsLogicallyRemoved = true;
        version.IsCurrent = false;
        version.RemovalReason = command.Reason.Trim();
        version.RemovalOperationKey = command.OperationKey;
        var beforeVersion = workflow.Version;
        CaseMutationGuard.Complete(workflow);
        AddRemovalNote(context, workflow, command, beforeVersion, timeProvider.GetUtcNow());
        await EfCaseReportGenerationStore.SourceDocumentChangedAsync(
            context, command.CaseId, occurrence.OperationKey, timeProvider.GetUtcNow(), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    async Task<OriginalReportRecorded> IMarkAsOriginalReportStore.MarkAsOriginalReportAsync(
        MarkAsOriginalReportCommand command,
        OriginalReportReading? reading,
        CancellationToken cancellationToken)
    {
        OriginalReportPolicy.ValidateRequest(command);
        var operationKey = command.OperationKey.Trim();
        var requestHash = CaseOperationReplay.Hash(JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            command.CaseId,
            command.ExpectedVersion,
            command.DocumentOccurrenceId,
            command.DocumentVersionId,
            actorKind = command.Actor.Kind.ToString(),
            actorSubjectId = command.Actor.SubjectId,
            actorRoles = command.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            operationKey,
            command.EditLeaseToken
        }));

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        if (await CaseOperationReplay.FindAsync(
                context,
                command.CaseId,
                operationKey,
                requestHash,
                cancellationToken))
        {
            var replay = await context.CaseWorkflowEvents.AsNoTracking()
                .SingleAsync(
                    item => item.CaseId == command.CaseId
                        && item.OperationKey == operationKey,
                    cancellationToken);
            return JsonSerializer.Deserialize<OriginalReportRecorded>(replay.ResultJson!)
                ?? throw new InvalidDataException(
                    "The original-report operation result is invalid.");
        }

        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedVersion,
            command.EditLeaseToken,
            now);
        var occurrence = await context.Set<DocumentOccurrenceEntity>()
            .SingleOrDefaultAsync(
                item => item.CaseId == command.CaseId
                    && item.Id == command.DocumentOccurrenceId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The document occurrence is unavailable.");
        var version = await context.Set<DocumentVersionEntity>()
            .SingleAsync(item => item.Id == occurrence.VersionId, cancellationToken);
        if (!version.IsCurrent || version.IsLogicallyRemoved || version.Id != command.DocumentVersionId)
        {
            throw new InvalidOperationException(
                "The document occurrence is unavailable.");
        }
        OriginalReportPolicy.RequireEligible(
            CaseTypeCodes.Parse(workflow.Case.Type), LifecycleStateOf(workflow), occurrence.SemanticRole);

        var existing = await CurrentAuditReports(context, command.CaseId)
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.Id != occurrence.Id)
            {
                throw new InvalidOperationException(
                    "A different document is already marked as the original report.");
            }

            return new(
                command.CaseId,
                occurrence.Id,
                version.FileName,
                workflow.Version);
        }

        var result = await RecordOriginalReportAsync(
            context, workflow, occurrence, version, command.Actor, operationKey, requestHash,
            reading, "original-report-role-v2", now, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    async Task<IReadOnlyList<FiledOriginalReportCandidate>> IRecogniseOriginalReportStore.FindAwaitingCandidatesAsync(
        Guid caseId,
        Guid receiptId,
        IReadOnlyCollection<Guid> intakeAssetIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intakeAssetIds);
        if (intakeAssetIds.Count == 0)
        {
            return [];
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .Include(value => value.Case)
            .SingleOrDefaultAsync(value => value.CaseId == caseId, cancellationToken);
        if (workflow is null || !await AwaitsRecognitionAsync(context, workflow, cancellationToken))
        {
            return [];
        }

        // The receipt's files are found by the operation key the filer gave
        // each on this Case, so a file filed any other way is never read.
        var assetsByKey = intakeAssetIds.Distinct().ToDictionary(
            assetId => AutomaticCaseEvidencePromotionOperationKey.For(caseId, receiptId, assetId),
            StringComparer.Ordinal);
        var operationKeys = assetsByKey.Keys.ToArray();
        var filed = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                    && operationKeys.Contains(occurrence.OperationKey)
                    && occurrence.SemanticRole != DocumentSemanticRole.Image
                    && version.IsCurrent
                    && !version.IsLogicallyRemoved
                    && version.CustodyStatus != DocumentCustodyStatus.Failed
                orderby occurrence.Ordinal
                select new { OccurrenceId = occurrence.Id, occurrence.OperationKey, VersionId = version.Id })
            .ToListAsync(cancellationToken);
        return filed
            .Select(item => new FiledOriginalReportCandidate(
                assetsByKey[item.OperationKey], item.OccurrenceId, item.VersionId))
            .ToArray();
    }

    async Task<OriginalReportRecorded?> IRecogniseOriginalReportStore.RecordRecognisedAsync(
        RecordRecognisedOriginalReport command,
        OriginalReportReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(reading);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.ExecuteSystemWork);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OperationKey);
        var operationKey = command.OperationKey.Trim();
        var requestHash = CaseOperationReplay.Hash(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            command.CaseId,
            command.IntakeReceiptId,
            command.DocumentOccurrenceId,
            command.DocumentVersionId,
            operationKey
        }));

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        if (await CaseOperationReplay.FindAsync(
                context,
                command.CaseId,
                operationKey,
                requestHash,
                cancellationToken))
        {
            var replay = await context.CaseWorkflowEvents.AsNoTracking()
                .SingleAsync(
                    item => item.CaseId == command.CaseId
                        && item.OperationKey == operationKey,
                    cancellationToken);
            return JsonSerializer.Deserialize<OriginalReportRecorded>(replay.ResultJson!)
                ?? throw new InvalidDataException(
                    "The original-report operation result is invalid.");
        }

        // Everything the recognition was decided on is read again here: a
        // Case that no longer awaits its report, or a document that changed,
        // records nothing.
        var authority = await CaseMutationAuthority.LoadAsync(context, command.CaseId, cancellationToken);
        if (authority?.Workflow is not { } workflow
            || !await AwaitsRecognitionAsync(context, workflow, cancellationToken))
        {
            return null;
        }

        var occurrence = await context.Set<DocumentOccurrenceEntity>()
            .SingleOrDefaultAsync(
                item => item.CaseId == command.CaseId
                    && item.Id == command.DocumentOccurrenceId,
                cancellationToken);
        var version = occurrence is null
            ? null
            : await context.Set<DocumentVersionEntity>()
                .SingleAsync(item => item.Id == occurrence.VersionId, cancellationToken);
        if (occurrence is null
            || version is null
            || !version.IsCurrent
            || version.IsLogicallyRemoved
            || version.Id != command.DocumentVersionId
            || version.CustodyStatus == DocumentCustodyStatus.Failed
            || occurrence.SemanticRole == DocumentSemanticRole.Image)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (authority.SystemWorkYields(now))
        {
            throw new IntakeDependencyUnavailableException(
                "Original report recognition is waiting for the Case editor.");
        }

        var result = await RecordOriginalReportAsync(
            context, workflow, occurrence, version, command.Actor, operationKey, requestHash,
            reading, "original-report-recognition-v1", now, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // The Case changed between the read above and this save: an editor
            // claimed it or another writer advanced it. Nothing was recorded.
            throw new IntakeDependencyUnavailableException(
                "The Case changed while its original report was recognised.", exception);
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Records <paramref name="occurrence"/> as the Case's original report —
    /// the one body a staff Mark and a recognition share: the role, the
    /// Original report cells filled from its own reading, the Case version and
    /// the history line.
    /// </summary>
    private static async Task<OriginalReportRecorded> RecordOriginalReportAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        DocumentOccurrenceEntity occurrence,
        DocumentVersionEntity version,
        ActionActor actor,
        string operationKey,
        string requestHash,
        OriginalReportReading? reading,
        string policyVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var beforeVersion = workflow.Version;
        var beforeRole = occurrence.SemanticRole;
        occurrence.SemanticRole = DocumentSemanticRole.AuditReport;
        // The marked document fills the Original report cells staff have not
        // confirmed (v28 P51). A reading of any other bytes fills nothing from
        // the report.
        var filled = await OriginalReportPrefillWriter.ApplyAsync(
            context,
            await CaseWorkScope.CurrentIdAsync(context, workflow.CaseId, cancellationToken),
            reading is not null
                && string.Equals(reading.Sha256, version.Sha256, StringComparison.OrdinalIgnoreCase)
                    ? reading
                    : null,
            workflow.Case.StandaloneAuditAssessment is { } verdict ? AuditAssessmentCode.Parse(verdict) : null,
            now,
            cancellationToken);
        CaseMutationGuard.Complete(workflow);
        var result = new OriginalReportRecorded(
            workflow.CaseId,
            occurrence.Id,
            version.FileName,
            workflow.Version);
        var historyLine = $"Original report: {version.FileName}";
        CaseMutationHistory.Add(
            context,
            workflow,
            actor,
            operationKey,
            historyLine,
            OriginalReportRecordedEventKind,
            requestHash,
            beforeVersion,
            workflow.Version,
            JsonSerializer.Serialize(new
            {
                occurrence.Id,
                SemanticRole = beforeRole.ToString(),
                Fields = filled.ToDictionary(item => item.Key, item => item.Value.Before)
            }),
            JsonSerializer.Serialize(new
            {
                occurrence.Id,
                SemanticRole = occurrence.SemanticRole.ToString(),
                Fields = filled.ToDictionary(item => item.Key, item => (string?)item.Value.After)
            }),
            policyVersion,
            now);
        context.CaseWorkflowEvents.Local.Single(item =>
            item.CaseId == workflow.CaseId
            && item.OperationKey == operationKey).ResultJson = JsonSerializer.Serialize(result);
        return result;
    }

    /// <summary>The Case's current, not removed document with the Audit report role.</summary>
    private static IQueryable<DocumentOccurrenceEntity> CurrentAuditReports(PegasusDbContext context, Guid caseId) =>
        from item in context.Set<DocumentOccurrenceEntity>()
        join itemVersion in context.Set<DocumentVersionEntity>()
            on item.VersionId equals itemVersion.Id
        where item.CaseId == caseId
            && item.SemanticRole == DocumentSemanticRole.AuditReport
            && itemVersion.IsCurrent
            && !itemVersion.IsLogicallyRemoved
        select item;

    private static async Task<bool> AwaitsRecognitionAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        CancellationToken cancellationToken) =>
        OriginalReportPolicy.AwaitsRecognition(
            CaseTypeCodes.Parse(workflow.Case.Type),
            LifecycleStateOf(workflow),
            workflow.ArchivedAtUtc is not null,
            workflow.Case.StandaloneAuditEvidenceId,
            await CurrentAuditReports(context, workflow.CaseId).AnyAsync(cancellationToken));

    private static CaseLifecycleState LifecycleStateOf(CaseWorkflowEntity workflow) =>
        Enum.TryParse<CaseLifecycleState>(workflow.State, out var parsedState)
            && Enum.IsDefined(parsedState)
                ? parsedState
                : throw new InvalidDataException(
                    $"Case '{workflow.CaseId}' has an unrecognized lifecycle state.");

    /// <summary>
    /// Puts the removal on the case's Notes tab, in the same transaction, so a
    /// file is never removed without the note or noted without the removal.
    /// </summary>
    /// <remarks>
    /// <c>CaseWorkflowEvents</c>, not <c>CaseHistory</c>. Only the former is
    /// read by anything operator-facing; a row written to the latter persists,
    /// reports success and leaves the timeline empty — which is exactly how the
    /// Release 22 note defect reached production. The neighbouring
    /// <c>custody_confirmed</c> writes model the wrong table and are invisible
    /// today; do not copy them.
    ///
    /// The removal reason is the note body — it is already required, already
    /// bounded at 500, and already the thing a person typed to explain
    /// themselves.
    ///
    /// The actor is the member of staff who pressed the control, not a system
    /// identity. "Created by the system" describes who writes the note, not who
    /// acted; every other staff mutation on this timeline records the staff
    /// actor.
    ///
    /// <c>(CaseId, AfterVersion)</c> is unique, so the note must carry a
    /// version some mutation has claimed. It carries the one
    /// <see cref="CaseMutationGuard.Complete"/> just claimed, which nothing else
    /// takes — a version-neutral note would collide on a second removal.
    /// </remarks>
    private static void AddRemovalNote(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        LogicallyRemoveDocumentCommand command,
        long beforeVersion,
        DateTimeOffset occurredAtUtc) =>
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = "case_document_removed",
            OperationKey = command.OperationKey,
            RequestHash = command.OperationKey,
            ActorKind = command.Actor.Kind.ToString(),
            ActorSubjectId = command.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(command.Actor.Roles.OrderBy(role => role)),
            Reason = command.Reason.Trim(),
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });

    async Task ITagCaseImage.ExecuteAsync(
        TagCaseImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateActor(command.Actor);
        var operationKey = ValidateOperationKey(command.OperationKey);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var occurrence = await context.Set<DocumentOccurrenceEntity>()
            .SingleOrDefaultAsync(
                value => value.CaseId == command.CaseId && value.Id == command.OccurrenceId,
                cancellationToken)
            ?? throw new InvalidOperationException("The document occurrence is unavailable.");
        var tag = await context.Set<ImageTagEntity>()
            .SingleOrDefaultAsync(value => value.Id == command.TagId, cancellationToken)
            ?? throw new InvalidOperationException("The image tag is unavailable.");
        var assignment = await context.Set<DocumentOccurrenceTagEntity>()
            .SingleOrDefaultAsync(
                value => value.OccurrenceId == occurrence.Id && value.TagId == tag.Id,
                cancellationToken);
        var history = await FindDocumentHistoryAsync(context, operationKey, cancellationToken);
        var afterJson = DocumentActionHistory.Serialize(
            new ImageTagHistoryValue(occurrence.Id, tag.Id, tag.Name));
        // A replay asserts the audited action, not the current state: a tag is
        // reversible, so the same key can be resubmitted after the tag has
        // legitimately come off again. It is answered before the taggable-image
        // rule for the same reason the untag path does: the image may since
        // have been superseded or removed, which does not unmake the action
        // this key already recorded.
        if (history is not null)
        {
            DocumentActionHistory.RequireExactReplay(
                history,
                "case_document",
                command.CaseId.ToString("D"),
                ImageTaggedEventKind,
                command.Actor,
                reason: null,
                afterJson);
            return;
        }
        // Only a first submission puts a tag on, so only it has to hold the
        // rule about which images may carry one.
        await RequireTaggableImageAsync(
            context, command.CaseId, command.OccurrenceId, cancellationToken);
        if (assignment is not null)
        {
            throw new InvalidOperationException("This image already carries that tag.");
        }

        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            now);
        var reportImagesBefore = await EfCaseAssetPreparationStore.LoadCurrentAsync(
            context, command.CaseId, cancellationToken);
        context.Set<DocumentOccurrenceTagEntity>().Add(new()
        {
            OccurrenceId = occurrence.Id,
            TagId = tag.Id,
            AppliedByKind = command.Actor.Kind.ToString(),
            AppliedBySubjectId = command.Actor.SubjectId,
            AppliedAtUtc = now,
            OperationKey = operationKey
        });
        if (ImageTagVocabulary.TakesImageOutOfReport(tag.Id) && occurrence.InReport)
        {
            // The tag took the image out, so the Case history says so, as
            // it does when staff take one out. The line has a key of its
            // own: the tag's replay finds its one line by the command's key.
            SetInReport(occurrence, inReport: false, command.Actor, now);
            context.ActionHistory.Add(DocumentActionHistory.Succeeded(
                "case_document",
                command.CaseId.ToString("D"),
                ImageOutOfReportEventKind,
                command.Actor,
                now,
                $"out-of-report:{CaseOperationReplay.Hash(operationKey)}",
                afterJson: DocumentActionHistory.Serialize(
                    new ImageInReportHistoryValue(occurrence.Id, InReport: false))));
        }
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "case_document",
            command.CaseId.ToString("D"),
            ImageTaggedEventKind,
            command.Actor,
            now,
            operationKey,
            afterJson: afterJson));
        CaseMutationGuard.Complete(workflow);
        await SaveStalingChangedReportImagesAsync(
            context, command.CaseId, reportImagesBefore, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    async Task IUntagCaseImage.ExecuteAsync(
        UntagCaseImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateActor(command.Actor);
        var operationKey = ValidateOperationKey(command.OperationKey);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var occurrence = await context.Set<DocumentOccurrenceEntity>()
            .SingleOrDefaultAsync(
                value => value.CaseId == command.CaseId && value.Id == command.OccurrenceId,
                cancellationToken)
            ?? throw new InvalidOperationException("The document occurrence is unavailable.");
        var tag = await context.Set<ImageTagEntity>()
            .SingleOrDefaultAsync(value => value.Id == command.TagId, cancellationToken)
            ?? throw new InvalidOperationException("The image tag is unavailable.");
        var assignment = await context.Set<DocumentOccurrenceTagEntity>()
            .SingleOrDefaultAsync(
                value => value.OccurrenceId == occurrence.Id && value.TagId == tag.Id,
                cancellationToken);
        var history = await FindDocumentHistoryAsync(context, operationKey, cancellationToken);
        var afterJson = DocumentActionHistory.Serialize(
            new ImageTagHistoryValue(occurrence.Id, tag.Id, tag.Name));
        if (history is not null)
        {
            DocumentActionHistory.RequireExactReplay(
                history,
                "case_document",
                command.CaseId.ToString("D"),
                ImageUntaggedEventKind,
                command.Actor,
                reason: null,
                afterJson);
            return;
        }
        if (assignment is null)
        {
            throw new InvalidOperationException("This image does not carry that tag.");
        }

        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            now);
        var reportImagesBefore = await EfCaseAssetPreparationStore.LoadCurrentAsync(
            context, command.CaseId, cancellationToken);
        context.Set<DocumentOccurrenceTagEntity>().Remove(assignment);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "case_document",
            command.CaseId.ToString("D"),
            ImageUntaggedEventKind,
            command.Actor,
            now,
            operationKey,
            afterJson: afterJson));
        CaseMutationGuard.Complete(workflow);
        await SaveStalingChangedReportImagesAsync(
            context, command.CaseId, reportImagesBefore, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Puts one image in the report or takes it out, at once (operator, 26
    /// September 2026): the flag, the image's preparation version, one history
    /// line and the Case version, under the same guards and replay rule as a
    /// tag. An image put back in follows the images already ordered.
    /// </summary>
    async Task ISetCaseImageInReport.ExecuteAsync(
        SetCaseImageInReportCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateActor(command.Actor);
        var operationKey = ValidateOperationKey(command.OperationKey);
        var eventKind = command.InReport ? ImageInReportEventKind : ImageOutOfReportEventKind;
        var afterJson = DocumentActionHistory.Serialize(
            new ImageInReportHistoryValue(command.OccurrenceId, command.InReport));

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (await FindDocumentHistoryAsync(context, operationKey, cancellationToken) is { } history)
        {
            DocumentActionHistory.RequireExactReplay(
                history,
                "case_document",
                command.CaseId.ToString("D"),
                eventKind,
                command.Actor,
                reason: null,
                afterJson);
            return;
        }

        var occurrence = await RequireTaggableImageAsync(
            context, command.CaseId, command.OccurrenceId, cancellationToken);
        if (occurrence.InReport == command.InReport)
        {
            throw new InvalidOperationException(command.InReport
                ? "This image is already in the report."
                : "This image is already out of the report.");
        }

        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            now);
        var reportImagesBefore = await EfCaseAssetPreparationStore.LoadCurrentAsync(
            context, command.CaseId, cancellationToken);
        SetInReport(occurrence, command.InReport, command.Actor, now);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "case_document",
            command.CaseId.ToString("D"),
            eventKind,
            command.Actor,
            now,
            operationKey,
            afterJson: afterJson));
        CaseMutationGuard.Complete(workflow);
        await SaveStalingChangedReportImagesAsync(
            context, command.CaseId, reportImagesBefore, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Writes whether the report uses an image. Out of the report it keeps no
    /// order and no page of its own; its preparation version moves, so a
    /// Case save staged against the old state is refused.
    /// </summary>
    private static void SetInReport(
        DocumentOccurrenceEntity occurrence,
        bool inReport,
        ActionActor actor,
        DateTimeOffset now)
    {
        occurrence.InReport = inReport;
        occurrence.SupportingOrder = null;
        if (!inReport)
        {
            occurrence.PreparationFullPage = false;
        }
        occurrence.PreparationVersion = checked(occurrence.PreparationVersion + 1);
        occurrence.PreparedBy = $"{actor.Kind}:{actor.SubjectId}";
        occurrence.PreparedAtUtc = now;
    }

    /// <summary>
    /// Saves this command's writes and, when they change the images the report
    /// uses or how one prints, marks the current generation stale in the same
    /// transaction: tags and In report decide them (operator, 26 September 2026).
    /// </summary>
    private static async Task SaveStalingChangedReportImagesAsync(
        PegasusDbContext context,
        Guid caseId,
        IReadOnlyList<CaseAssetPreparation> reportImagesBefore,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);
        var reportImagesAfter = await EfCaseAssetPreparationStore.LoadCurrentAsync(
            context, caseId, cancellationToken);
        if (CaseReportFreshness.ClassifyImages(reportImagesBefore, reportImagesAfter).IsStale)
        {
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context, caseId, CaseReportStaleReasons.ImagePreparationChanged, now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Adds an operator's own word to the shared vocabulary. The vocabulary is
    /// global, so this takes no case and no lease - only the casework right, a
    /// name nothing else already uses, and an operation key that makes a
    /// resubmitted form return the tag it already created.
    /// </summary>
    async Task<CreateImageTagResult> ICreateImageTag.ExecuteAsync(
        CreateImageTagCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateActor(command.Actor);
        var operationKey = ValidateOperationKey(command.OperationKey);
        var name = ImageTagVocabulary.Normalize(command.Name);
        var normalizedName = ImageTagVocabulary.NormalizeKey(name);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var replay = await context.Set<ImageTagEntity>()
            .SingleOrDefaultAsync(
                value => value.CreateOperationKey == operationKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.NormalizedName, normalizedName, StringComparison.Ordinal)
                || !string.Equals(replay.Colour, command.Colour.ToString(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The image tag operation key was reused with a different name or colour.");
            }

            return new(EfImageTagVocabularyReader.ToImageTag(replay), IsReplay: true);
        }
        if (await context.Set<ImageTagEntity>()
                .AnyAsync(value => value.NormalizedName == normalizedName, cancellationToken))
        {
            throw new ImageTagNameInUseException(name);
        }

        var created = new ImageTagEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = normalizedName,
            Colour = command.Colour.ToString(),
            IsBuiltIn = false,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            CreatedBy = command.Actor.SubjectId,
            CreateOperationKey = operationKey,
            Version = 1
        };
        context.Set<ImageTagEntity>().Add(created);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "image_tag",
            created.Id.ToString("D"),
            "image_tag_created",
            command.Actor,
            created.CreatedAtUtc,
            operationKey,
            afterJson: DocumentActionHistory.Serialize(
                new ImageTagCreatedHistoryValue(created.Id, created.Name, created.Colour))));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKeyFailure(exception))
        {
            // The AnyAsync check above is not itself transactional: a concurrent create with the
            // same normalized name can still land between it and this insert. IX_ImageTags_NormalizedName
            // is the actual guard, so its violation is the same refusal the pre-check reports.
            throw new ImageTagNameInUseException(name);
        }
        return new(EfImageTagVocabularyReader.ToImageTag(created), IsReplay: false);
    }

    private static bool IsDuplicateKeyFailure(Exception exception) => exception switch
    {
        SqlException { Number: 2601 or 2627 } => true,
        DbUpdateException { InnerException: { } innerException } =>
            IsDuplicateKeyFailure(innerException),
        _ => false
    };

    /// <summary>
    /// The occurrence a tag may be put on: a custody-confirmed, current,
    /// unremoved JPEG or PNG image of this case - the same set the operator's
    /// Images tab draws.
    /// </summary>
    private static async Task<DocumentOccurrenceEntity> RequireTaggableImageAsync(
        PegasusDbContext context,
        Guid caseId,
        Guid occurrenceId,
        CancellationToken cancellationToken)
    {
        var occurrence = await context.Set<DocumentOccurrenceEntity>()
            .SingleOrDefaultAsync(
                value => value.CaseId == caseId && value.Id == occurrenceId,
                cancellationToken)
            ?? throw new InvalidOperationException("The document occurrence is unavailable.");
        var version = await context.Set<DocumentVersionEntity>()
            .SingleAsync(value => value.Id == occurrence.VersionId, cancellationToken);
        if (occurrence.SemanticRole != DocumentSemanticRole.Image
            || version.CustodyStatus != DocumentCustodyStatus.Confirmed
            || !version.IsCurrent
            || version.IsLogicallyRemoved
            || !IsSupportedImageMediaType(version.MediaType))
        {
            throw new InvalidOperationException(
                "Only a custody-confirmed current JPEG or PNG image may be tagged.");
        }

        return occurrence;
    }

    private async Task<DocumentExport> BuildExportAsync(
        Guid caseId,
        string caseReference,
        string? caseRootRemoteId,
        string? auditRootRemoteId,
        IReadOnlyList<ExportItem> items,
        long maximumArchiveBytes,
        CancellationToken cancellationToken)
    {
        var output = new MemoryStream((int)Math.Min(maximumArchiveBytes, 64 * 1024L));
        try
        {
            var boundedOutput = new MaximumLengthWriteStream(output, maximumArchiveBytes);
            var manifest = new List<DocumentExportManifestEntry>(items.Count);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "manifest.json"
            };
            using (var archive = new ZipArchive(boundedOutput, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var item in items)
                {
                    var fileName = MakeUniqueFileName(item.Version.FileName, names);
                    var manifestEntry = new DocumentExportManifestEntry(
                        fileName,
                        item.Occurrence.Id,
                        item.Version.Id,
                        item.Occurrence.SemanticRole,
                        item.Version.ContentLength,
                        item.Version.Sha256);
                    manifest.Add(manifestEntry);

                    var entry = archive.CreateEntry(fileName, CompressionLevel.NoCompression);
                    entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    await using var destination = entry.Open();
                    await using var source = await contentStore.OpenReadVersionAsync(
                        Address(
                            caseId,
                            caseReference,
                            CaseCustodyFolders.RootOf(item.Folder, caseRootRemoteId, auditRootRemoteId),
                            item.Occurrence,
                            item.Version),
                        item.Version.Sha256,
                        item.Version.ContentLength,
                        cancellationToken);
                    await source.CopyToAsync(destination, cancellationToken);
                }

                var manifestArchiveEntry = archive.CreateEntry("manifest.json", CompressionLevel.NoCompression);
                manifestArchiveEntry.LastWriteTime =
                    new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                await using var manifestStream = manifestArchiveEntry.Open();
                await JsonSerializer.SerializeAsync(
                    manifestStream,
                    manifest,
                    cancellationToken: cancellationToken);
            }

            output.Position = 0;
            return new(output, $"case-{caseId:N}-documents.zip", manifest);
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    private static async Task<CaseWorkflowEntity> RequireWorkflowAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return await context.CaseWorkflows
            .Include(value => value.Case)
            .SingleOrDefaultAsync(value => value.CaseId == caseId, cancellationToken)
            ?? throw new InvalidOperationException("The case is unavailable.");
    }

    internal static void ValidateAddCommand(AddCaseDocumentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateActor(command.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.MediaType);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.SourceOccurrenceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OperationKey);
        if (command.Content.IsEmpty)
        {
            throw new ArgumentException("Document content is required.", nameof(command));
        }
    }

    private static void ValidateActor(ActionActor actor) =>
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

    private static string ValidateOperationKey(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var normalized = operationKey.Trim();
        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationKey),
                "The operation key cannot exceed 100 characters.");
        }

        return normalized;
    }

    private static Task<ActionHistoryEntity?> FindDocumentHistoryAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.ActionHistory.SingleOrDefaultAsync(
            value => value.AggregateType == "case_document"
                && value.CorrelationId == operationKey,
            cancellationToken);

    internal static void EnsureReplayMatches(
        AddCaseDocumentCommand command,
        DocumentOccurrenceEntity occurrence,
        DocumentVersionEntity version,
        string contentHash)
    {
        if (occurrence.SemanticRole != command.SemanticRole
            || occurrence.Source != command.Source
            || !string.Equals(occurrence.SourceOccurrenceIdentity, command.SourceOccurrenceIdentity, StringComparison.Ordinal)
            || !string.Equals(version.FileName, GetSafeFileName(command.FileName), StringComparison.Ordinal)
            || !string.Equals(version.MediaType, command.MediaType.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(version.Sha256, contentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The document operation key was reused with different content or metadata.");
        }
    }

    internal static string GetSafeFileName(string fileName)
    {
        var value = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.Any(char.IsControl))
        {
            throw new ArgumentException("The document file name is invalid.", nameof(fileName));
        }

        return value;
    }

    private static bool IsSupportedImageMediaType(string mediaType) =>
        string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase);

    private static string MakeUniqueFileName(string fileName, HashSet<string> names)
    {
        if (names.Add(fileName))
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{stem} ({suffix}){extension}";
            if (names.Add(candidate))
            {
                return candidate;
            }
        }
    }

    internal static string ComputeSha256(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    internal static DocumentOccurrence ToOccurrence(DocumentOccurrenceEntity value) => new(
        value.Id,
        value.CaseId,
        value.DocumentId,
        value.VersionId,
        value.SemanticRole,
        value.Source,
        value.SourceOccurrenceIdentity,
        value.RecordedAtUtc,
        [],
        value.Ordinal);

    internal static ManagedDocumentContentAddress Address(
        Guid caseId,
        string caseReference,
        string? caseRootRemoteId,
        DocumentOccurrenceEntity occurrence,
        DocumentVersionEntity version) => new(
        caseId,
        caseReference,
        caseRootRemoteId,
        occurrence.Id,
        occurrence.Ordinal,
        occurrence.DocumentId,
        version.Id,
        version.Version,
        occurrence.SemanticRole,
        version.FileName,
        version.MediaType,
        version.BoxFileId,
        version.BoxVersionId);

    internal static DocumentVersion ToVersion(DocumentVersionEntity value) => new(
        value.Id,
        value.DocumentId,
        value.Version,
        value.FileName,
        value.MediaType,
        value.ContentLength,
        value.Sha256,
        value.CustodyStatus,
        value.CreatedAtUtc,
        value.CreatedBy,
        value.IsCurrent,
        value.IsLogicallyRemoved,
        value.RemovalReason);

    /// <summary>
    /// The number the Case's next document takes: one past its highest, and
    /// never 1, which is the source the Case was created from.
    /// </summary>
    internal static async Task<int> NextDocumentOrdinalAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var lastOrdinal = await context.Set<CaseDocumentEntity>()
            .Where(value => value.CaseId == caseId)
            .Select(value => (int?)value.Ordinal)
            .MaxAsync(cancellationToken) ?? 1;
        return checked(lastOrdinal + 1);
    }

    internal static async Task<PendingDocumentAdd> PrepareAddAsync(
        PegasusDbContext context,
        IDocumentContentStore contentStore,
        CaseWorkflowEntity workflow,
        AddCaseDocumentCommand command,
        string contentHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var document = await context.Set<CaseDocumentEntity>()
            .SingleOrDefaultAsync(
                value => value.CaseId == command.CaseId
                    && value.SourceOccurrenceIdentity == command.SourceOccurrenceIdentity,
                cancellationToken);
        if (document is null)
        {
            document = new()
            {
                Id = Guid.NewGuid(),
                CaseId = command.CaseId,
                Ordinal = await NextDocumentOrdinalAsync(context, command.CaseId, cancellationToken),
                SourceOccurrenceIdentity = command.SourceOccurrenceIdentity
            };
            context.Add(document);
        }

        var existingVersions = await context.Set<DocumentVersionEntity>()
            .Where(version => version.DocumentId == document.Id)
            .ToListAsync(cancellationToken);
        foreach (var existingVersion in existingVersions)
        {
            existingVersion.IsCurrent = false;
        }

        var version = new DocumentVersionEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Version = existingVersions.Count == 0 ? 1 : checked(existingVersions.Max(value => value.Version) + 1),
            FileName = GetSafeFileName(command.FileName),
            MediaType = command.MediaType.Trim(),
            ContentLength = command.Content.Length,
            Sha256 = contentHash,
            CustodyStatus = DocumentCustodyStatus.Confirmed,
            CreatedAtUtc = now,
            CreatedBy = $"{command.Actor.Kind}:{command.Actor.SubjectId}",
            IsCurrent = true
        };
        var occurrence = new DocumentOccurrenceEntity
        {
            Id = Guid.NewGuid(),
            CaseId = command.CaseId,
            DocumentId = document.Id,
            VersionId = version.Id,
            Ordinal = document.Ordinal,
            SemanticRole = command.SemanticRole,
            Source = command.Source,
            SourceOccurrenceIdentity = command.SourceOccurrenceIdentity,
            RecordedAtUtc = now,
            OperationKey = command.OperationKey
        };
        context.Add(version);
        context.Add(occurrence);
        await EfCaseReportGenerationStore.SourceDocumentChangedAsync(
            context, command.CaseId, command.OperationKey, now, cancellationToken);
        var contentWrite = await contentStore.StoreVersionAsync(
            Address(
                command.CaseId,
                workflow.Case.Reference,
                CaseCustodyFolders.RootOf(workflow.Case, document.CustodyFolder),
                occurrence,
                version),
            command.Content,
            contentHash,
            cancellationToken);
        version.BoxFileId = contentWrite.RemoteId;
        version.BoxVersionId = contentWrite.BoxVersionId;
        return new(
            new(ToOccurrence(occurrence), ToVersion(version), false),
            version,
            contentWrite);
    }

    internal sealed record PendingDocumentAdd(
        AddCaseDocumentResult Result,
        DocumentVersionEntity Version,
        DocumentContentWriteResult ContentWrite);

    private sealed class MaximumLengthWriteStream(Stream inner, long maximumLength) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set
            {
                EnsureWithinLimit(value);
                inner.Position = value;
            }
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
        {
            var previousPosition = inner.Position;
            var position = inner.Seek(offset, origin);
            if (position > maximumLength)
            {
                inner.Position = previousPosition;
                ThrowArchiveLimitExceeded();
            }

            return position;
        }

        public override void SetLength(long value)
        {
            EnsureWithinLimit(value);
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureWriteFits(count);
            inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureWriteFits(buffer.Length);
            inner.Write(buffer);
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            EnsureWriteFits(count);
            return inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            EnsureWriteFits(buffer.Length);
            return inner.WriteAsync(buffer, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            EnsureWriteFits(1);
            inner.WriteByte(value);
        }

        private void EnsureWithinLimit(long value)
        {
            if (value > maximumLength)
            {
                ThrowArchiveLimitExceeded();
            }
        }

        private void EnsureWriteFits(int count)
        {
            if (count > maximumLength - inner.Position)
            {
                ThrowArchiveLimitExceeded();
            }
        }

        private static void ThrowArchiveLimitExceeded() =>
            throw new InvalidOperationException(
                "The generated document archive exceeds the maximum archive byte limit.");
    }

    private sealed record DocumentDownloadHistoryValue(
        Guid CaseId,
        Guid OccurrenceId,
        Guid VersionId,
        string Sha256);

    private sealed record DocumentExportHistoryValue(
        Guid CaseId,
        IReadOnlyList<DocumentExportHistoryItem> Documents);

    private sealed record DocumentExportHistoryItem(
        Guid OccurrenceId,
        Guid VersionId,
        string Sha256);

    private sealed record ImageTagHistoryValue(
        Guid OccurrenceId,
        Guid TagId,
        string Name);

    private sealed record ImageInReportHistoryValue(
        Guid OccurrenceId,
        bool InReport);

    private sealed record ImageTagCreatedHistoryValue(
        Guid TagId,
        string Name,
        string Colour);

    private sealed record ExportItem(
        DocumentOccurrenceEntity Occurrence,
        DocumentVersionEntity Version,
        string Folder);
}
