using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
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
    IExportCaseDocuments,
    ILogicallyRemoveDocument,
    IConfirmThirdPartyVehicleEvidence,
    ICaseDocumentStateQueries
{
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
    async Task<CaseDocumentState?> ICaseDocumentStateQueries.GetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CaseWorkflows
            .AsNoTracking()
            .Where(value => value.CaseId == caseId)
            .Select(value => new CaseDocumentState(value.CaseId, value.Version))
            .SingleOrDefaultAsync(cancellationToken);
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
            .Select(value => new { value.Reference, value.CustodyRootRemoteId })
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
            select new { Occurrence = occurrence, Version = version })
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
                caseIdentity.CustodyRootRemoteId,
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
            .Select(value => new { value.Reference, value.CustodyRootRemoteId })
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
                select new ExportItem(occurrence, version))
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
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

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

    async Task IConfirmThirdPartyVehicleEvidence.ExecuteAsync(
        ConfirmThirdPartyVehicleEvidenceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateActor(command.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);
        var operationKey = ValidateOperationKey(command.OperationKey);
        var reason = command.Reason.Trim();
        if (reason.Length > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The third-party vehicle confirmation reason cannot exceed 500 characters.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var occurrence = await context.Set<DocumentOccurrenceEntity>()
            .SingleOrDefaultAsync(
                value => value.CaseId == command.CaseId && value.Id == command.OccurrenceId,
                cancellationToken)
            ?? throw new InvalidOperationException("The document occurrence is unavailable.");
        var history = await FindDocumentHistoryAsync(context, operationKey, cancellationToken);
        var afterJson = DocumentActionHistory.Serialize(new ThirdPartyVehicleEvidenceHistoryValue(
            occurrence.Id,
            occurrence.ThirdPartyVehicleConfirmedAtUtc,
            occurrence.ThirdPartyVehicleConfirmationReason));
        if (history is not null)
        {
            if (occurrence.ThirdPartyVehicleConfirmedAtUtc is null)
            {
                throw new InvalidDataException(
                    "The audited third-party vehicle confirmation is missing from the document occurrence.");
            }

            DocumentActionHistory.RequireExactReplay(
                history,
                "case_document",
                command.CaseId.ToString("D"),
                "third_party_vehicle_evidence_confirmed",
                command.Actor,
                reason,
                afterJson);
            return;
        }

        var version = await context.Set<DocumentVersionEntity>()
            .SingleAsync(value => value.Id == occurrence.VersionId, cancellationToken);
        if (occurrence.SemanticRole != DocumentSemanticRole.Image
            || version.CustodyStatus != DocumentCustodyStatus.Confirmed
            || !version.IsCurrent
            || version.IsLogicallyRemoved
            || !IsSupportedImageMediaType(version.MediaType))
        {
            throw new InvalidOperationException(
                "Only a custody-confirmed current JPEG or PNG image may be confirmed as third-party vehicle evidence.");
        }
        if (occurrence.ThirdPartyVehicleConfirmedAtUtc is not null)
        {
            throw new InvalidOperationException(
                "This image has already been confirmed as third-party vehicle evidence.");
        }

        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            now);
        occurrence.ThirdPartyVehicleConfirmedAtUtc = now;
        occurrence.ThirdPartyVehicleConfirmationReason = reason;
        occurrence.ThirdPartyVehicleConfirmationOperationKey = operationKey;
        afterJson = DocumentActionHistory.Serialize(new ThirdPartyVehicleEvidenceHistoryValue(
            occurrence.Id,
            occurrence.ThirdPartyVehicleConfirmedAtUtc,
            occurrence.ThirdPartyVehicleConfirmationReason));
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "case_document",
            command.CaseId.ToString("D"),
            "third_party_vehicle_evidence_confirmed",
            command.Actor,
            now,
            operationKey,
            reason,
            afterJson: afterJson));
        CaseMutationGuard.Complete(workflow);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<DocumentExport> BuildExportAsync(
        Guid caseId,
        string caseReference,
        string? caseRootRemoteId,
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
                            caseRootRemoteId,
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
        value.ThirdPartyVehicleConfirmedAtUtc,
        value.ThirdPartyVehicleConfirmationReason,
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
            var lastOrdinal = await context.Set<CaseDocumentEntity>()
                .Where(value => value.CaseId == command.CaseId)
                .Select(value => (int?)value.Ordinal)
                .MaxAsync(cancellationToken) ?? 1;
            document = new()
            {
                Id = Guid.NewGuid(),
                CaseId = command.CaseId,
                Ordinal = checked(lastOrdinal + 1),
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
        var contentWrite = await contentStore.StoreVersionAsync(
            Address(
                command.CaseId,
                workflow.Case.Reference,
                workflow.Case.CustodyRootRemoteId,
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

    private sealed record ThirdPartyVehicleEvidenceHistoryValue(
        Guid OccurrenceId,
        DateTimeOffset? ConfirmedAtUtc,
        string? Reason);

    private sealed record ExportItem(
        DocumentOccurrenceEntity Occurrence,
        DocumentVersionEntity Version);
}
