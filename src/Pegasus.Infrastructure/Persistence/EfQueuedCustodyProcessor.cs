using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfQueuedCustodyProcessor(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    IExternalWorkStore workStore,
    ICaseCustody caseCustody,
    TimeProvider timeProvider) : IProcessQueuedCustody
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task ExecuteAsync(Guid workId, CancellationToken cancellationToken)
    {
        if (workId == Guid.Empty)
        {
            throw new ArgumentException("A custody work identifier is required.", nameof(workId));
        }

        var leaseToken = Guid.NewGuid().ToString("N");
        CustodyWorkPayload payload;
        while (true)
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var work = await context.ExternalWorkItems
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == workId, cancellationToken)
                ?? throw new InvalidOperationException("The custody work item is unavailable.");
            if (work.Kind is not (ExternalWorkKinds.CreateCaseCustody
                or ExternalWorkKinds.CreateAuditReferenceCustody
                or ExternalWorkKinds.CreateImageCaseCustody
                or ExternalWorkKinds.MergeImageCaseCustody))
            {
                throw new InvalidDataException("The external work item is not a supported custody operation.");
            }

            if (work.State is "completed" or "failed")
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            if (string.Equals(work.State, "processing", StringComparison.Ordinal)
                && work.LeaseExpiresAtUtc > now)
            {
                throw new InvalidOperationException("The custody work item is already leased.");
            }

            if (work.State is not ("pending" or "dispatching" or "queued" or "processing"))
            {
                throw new InvalidDataException(
                    $"The custody work item has unknown state '{work.State}'.");
            }

            var claimed = await context.ExternalWorkItems
                .Where(value => value.Id == work.Id
                    && value.State == work.State
                    && value.LeaseToken == work.LeaseToken
                    && value.LeaseExpiresAtUtc == work.LeaseExpiresAtUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.State, "processing")
                    .SetProperty(value => value.AttemptCount, value => value.AttemptCount + 1)
                    .SetProperty(value => value.LeaseToken, leaseToken)
                    .SetProperty(value => value.LeaseExpiresAtUtc, now.Add(LeaseDuration))
                    .SetProperty(value => value.FailureCode, (string?)null)
                    .SetProperty(value => value.FailureReason, (string?)null),
                    cancellationToken);
            if (claimed == 0)
            {
                continue;
            }

            try
            {
                payload = work.Kind switch
                {
                    ExternalWorkKinds.CreateImageCaseCustody => await LoadImageCreatePayloadAsync(
                        context,
                        RequireImageIntakeId(work),
                        work.OperationKey,
                        work.CaseRootCreationToken,
                        cancellationToken),
                    ExternalWorkKinds.MergeImageCaseCustody => await LoadImageMergePayloadAsync(
                        context,
                        RequireImageIntakeId(work),
                        work.OperationKey,
                        cancellationToken),
                    _ => await LoadPayloadAsync(
                        context,
                        work.Kind,
                        work.CaseId ?? throw new InvalidDataException(
                            "The case custody work item has no owning case."),
                        work.OperationKey,
                        work.CaseRootCreationToken,
                        work.AuditFolderCreationToken,
                        cancellationToken)
                };
            }
            catch (Exception exception)
            {
                await workStore.FailProcessingAsync(
                    workId,
                    leaseToken,
                    timeProvider.GetUtcNow(),
                    GetFailureCode(exception),
                    GetFailureReason(exception),
                    CancellationToken.None);
                throw;
            }

            break;
        }

        try
        {
            var leaseGuard = new CustodyEffectLeaseGuard(
                token => workStore.HoldsProcessingLeaseAsync(workId, leaseToken, token));
            await leaseGuard.RequireCurrentAsync(cancellationToken);
            switch (payload)
            {
                case ImageCreatePayload imageCreate:
                    await ProcessImageCreateAsync(
                        workId, leaseToken, imageCreate, leaseGuard, cancellationToken);
                    return;
                case ImageMergePayload imageMerge:
                    await ProcessImageMergeAsync(
                        workId, leaseToken, imageMerge, leaseGuard, cancellationToken);
                    return;
            }

            var casePayload = (WorkPayload)payload;
            var isAuditCustody = string.Equals(
                casePayload.WorkKind,
                ExternalWorkKinds.CreateAuditReferenceCustody,
                StringComparison.Ordinal);
            // CASE-014: an audit's reference already carries its a./ap. prefix,
            // so the case folder is named by the case reference like every
            // other case. This also closes a split that made audit custody
            // behave unlike anything the tests covered: the root was created
            // under the audit identity while GetExistingCaseRootAsync looked
            // it up under the case reference.
            var rootReference = casePayload.CaseReference;
            var root = isAuditCustody
                ? await caseCustody.GetExistingCaseRootAsync(
                    casePayload.CaseId,
                    casePayload.CaseReference,
                    cancellationToken)
                : await caseCustody.CreateCaseRootAsync(
                    casePayload.CaseId,
                    rootReference,
                    RequireCreationOwner(casePayload.CaseRootCreationToken),
                    $"{casePayload.OperationKey}:root",
                    leaseGuard,
                    cancellationToken);
            await leaseGuard.RequireCurrentAsync(cancellationToken);
            if (isAuditCustody)
            {
                if (string.IsNullOrWhiteSpace(casePayload.AuditReference))
                {
                    throw new InvalidDataException(
                        "The later Audit custody operation has no allocated Audit identity.");
                }
                var auditFolderRemoteId = await caseCustody.CreateAuditReferenceFolderAsync(
                    root,
                    casePayload.AuditReference,
                    RequireCreationOwner(casePayload.AuditFolderCreationToken),
                    $"{casePayload.OperationKey}:audit",
                    leaseGuard,
                    cancellationToken);
                await leaseGuard.RequireCurrentAsync(cancellationToken);
                await CompleteAuditCustodyAsync(
                    workId,
                    leaseToken,
                    root,
                    auditFolderRemoteId,
                    cancellationToken);
            }
            else
            {
                var version = await caseCustody.RetainAcceptedIntakeSourceAsync(
                    root,
                    new(
                        casePayload.IntakeReceiptId,
                        casePayload.SourceFileName,
                        casePayload.MediaType,
                        casePayload.SourceHash,
                        casePayload.SourceObjectKey,
                        casePayload.SourceLength),
                    $"{casePayload.OperationKey}:source",
                    leaseGuard,
                    cancellationToken);
                await leaseGuard.RequireCurrentAsync(cancellationToken);
                var retainedFiles = new List<RetainedCaseFile>
                {
                    new(
                        1,
                        casePayload.SourceFileName,
                        casePayload.MediaType,
                        casePayload.SourceLength,
                        casePayload.SourceHash,
                        DocumentSemanticRole.OriginalSource,
                        $"{casePayload.OperationKey}:source",
                        version.RemoteId,
                        version.BoxVersionId,
                        casePayload.SourceAssetId)
                };
                retainedFiles.AddRange(await RetainInstructionAttachmentsAsync(
                    root, casePayload, leaseGuard, cancellationToken));
                // CASE-014: an audit's files live in its own case folder, so
                // there is no separate audit folder to create for one. A later
                // Audit reference on a non-audit case still gets its folder.
                var auditFolderRemoteId = string.IsNullOrWhiteSpace(casePayload.AuditReference)
                        ? null
                        : await caseCustody.CreateAuditReferenceFolderAsync(
                        root,
                        casePayload.AuditReference,
                        RequireCreationOwner(casePayload.AuditFolderCreationToken),
                        $"{casePayload.OperationKey}:audit",
                        leaseGuard,
                        cancellationToken);
                await leaseGuard.RequireCurrentAsync(cancellationToken);
                await CompleteCaseCustodyAsync(
                    workId,
                    leaseToken,
                    root,
                    version,
                    auditFolderRemoteId,
                    retainedFiles,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await workStore.FailProcessingAsync(
                workId,
                leaseToken,
                timeProvider.GetUtcNow(),
                "custody_cancelled",
                "Case evidence storage was interrupted before completion.",
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await workStore.FailProcessingAsync(
                workId,
                leaseToken,
                timeProvider.GetUtcNow(),
                GetFailureCode(exception),
                GetFailureReason(exception),
                CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// DOCS-005: each attachment of the accepted instruction lands beside the
    /// retained source as its own file. The assets were retained at intake
    /// (attachment kind); ordinals follow the source at 002 onward, in stable
    /// file-name order, and replay verifies rather than re-uploads.
    /// </summary>
    private async Task<IReadOnlyList<RetainedCaseFile>> RetainInstructionAttachmentsAsync(
        CaseCustodyRoot root,
        WorkPayload casePayload,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
    {
        var retained = new List<RetainedCaseFile>();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context.Set<IntakeAssetEntity>()
            .AsNoTracking()
            .Where(asset => asset.IntakeReceiptId == casePayload.IntakeReceiptId
                && (asset.Kind == "attachment" || asset.Kind == "embedded_image"))
            .ToListAsync(cancellationToken);
        var attachments = candidates
            .Where(asset => asset.Kind == "attachment")
            .OrderBy(asset => asset.FileName)
            .ThenBy(asset => asset.Id)
            .ToList();
        for (var index = 0; index < attachments.Count; index++)
        {
            var attachment = attachments[index];
            var version = await caseCustody.RetainAcceptedIntakeAttachmentAsync(
                root,
                new(
                    casePayload.IntakeReceiptId,
                    attachment.FileName,
                    attachment.MediaType,
                    attachment.ContentHash,
                    attachment.StorageKey,
                    attachment.ContentLength),
                index + 2,
                $"{casePayload.OperationKey}:attachment:{attachment.Id:N}",
                leaseGuard,
                cancellationToken);
            await leaseGuard.RequireCurrentAsync(cancellationToken);
            retained.Add(new(
                index + 2,
                attachment.FileName,
                attachment.MediaType,
                attachment.ContentLength,
                attachment.ContentHash,
                // DOCS-009: a photograph attached to the instruction is a
                // photograph. Filing every attachment as Instruction hid the
                // case's own damage images from the evidence gallery's image
                // test and from EVA image selection, which both ask this
                // question by semantic role.
                InstructionEvidenceImages.IsImage(attachment.MediaType)
                    ? DocumentSemanticRole.Image
                    : DocumentSemanticRole.Instruction,
                $"{casePayload.OperationKey}:attachment:{attachment.Id:N}",
                version.RemoteId,
                version.BoxVersionId,
                attachment.Id));
        }

        // DOCS-006: photographs embedded in the instruction's documents land
        // as their own files after the attachments, resolved through the one
        // evidence-image selection (which also drops letterhead art and any
        // photo already retained as an attached file).
        var photographs = InstructionEvidenceImages
            .Select(candidates.Select(EfIntakeReceiptStore.MapAsset))
            .Where(record => record.Kind == IntakeAssetKind.EmbeddedImage)
            .ToArray();
        for (var index = 0; index < photographs.Length; index++)
        {
            var photograph = photographs[index];
            var version = await caseCustody.RetainAcceptedIntakeAttachmentAsync(
                root,
                new(
                    casePayload.IntakeReceiptId,
                    photograph.FileName,
                    photograph.MediaType,
                    photograph.ContentHash,
                    photograph.StorageKey,
                    photograph.ContentLength),
                attachments.Count + index + 2,
                $"{casePayload.OperationKey}:embedded:{photograph.Id:N}",
                leaseGuard,
                cancellationToken);
            await leaseGuard.RequireCurrentAsync(cancellationToken);
            retained.Add(new(
                attachments.Count + index + 2,
                photograph.FileName,
                photograph.MediaType,
                photograph.ContentLength,
                photograph.ContentHash,
                DocumentSemanticRole.Image,
                $"{casePayload.OperationKey}:embedded:{photograph.Id:N}",
                version.RemoteId,
                version.BoxVersionId,
                photograph.Id));
        }

        return retained;
    }

    /// <summary>
    /// Records the files intake put in the case folder as case documents, so
    /// the case can list and open them.
    ///
    /// The files are already in Box, uploaded by the custody route above, so
    /// this writes records only — it never sends the content a second time.
    /// The occurrence ordinal is the ordinal the upload used, and the flat
    /// Box name is derived from that ordinal at both ends, so a download
    /// resolves exactly the file that was uploaded (DOCS-007).
    ///
    /// Idempotent by operation key: custody work can be retried, and a
    /// replay must not produce a second copy of a document that is already
    /// recorded.
    /// </summary>
    private static async Task RecordRetainedCaseFilesAsync(
        PegasusDbContext context,
        Guid caseId,
        IReadOnlyList<RetainedCaseFile> retainedFiles,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (retainedFiles.Count == 0)
        {
            return;
        }

        var operationKeys = retainedFiles.Select(file => file.OperationKey).ToArray();
        var alreadyRecorded = await context.Set<DocumentOccurrenceEntity>()
            .Where(occurrence => occurrence.CaseId == caseId
                && operationKeys.Contains(occurrence.OperationKey))
            .Select(occurrence => occurrence.OperationKey)
            .ToListAsync(cancellationToken);
        var recorded = alreadyRecorded.ToHashSet(StringComparer.Ordinal);

        foreach (var file in retainedFiles)
        {
            if (!recorded.Add(file.OperationKey))
            {
                continue;
            }

            var document = new CaseDocumentEntity
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                Ordinal = file.Ordinal,
                SourceOccurrenceIdentity = file.OperationKey
            };
            var version = new DocumentVersionEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Version = 1,
                FileName = file.FileName,
                MediaType = file.MediaType,
                ContentLength = file.ContentLength,
                Sha256 = file.ContentHash,
                BoxFileId = file.BoxFileId,
                BoxVersionId = file.BoxVersionId,
                CustodyStatus = DocumentCustodyStatus.Confirmed,
                CreatedAtUtc = now,
                CreatedBy = "system:custody",
                IsCurrent = true
            };
            context.Add(document);
            context.Add(version);
            if (file.IntakeAssetId is { } intakeAssetId)
            {
                var asset = await context.Set<IntakeAssetEntity>()
                    .SingleAsync(value => value.Id == intakeAssetId, cancellationToken);
                asset.BoxFileId = file.BoxFileId;
                asset.BoxVersionId = file.BoxVersionId;
                asset.CustodyStatus = "confirmed";
            }
            context.Add(new DocumentOccurrenceEntity
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                DocumentId = document.Id,
                VersionId = version.Id,
                Ordinal = file.Ordinal,
                SemanticRole = file.SemanticRole,
                Source = DocumentSource.Intake,
                SourceOccurrenceIdentity = file.OperationKey,
                RecordedAtUtc = now,
                OperationKey = file.OperationKey
            });
        }
    }

    /// <summary>
    /// One file this case's intake put in the case folder, and the record it
    /// needs so the case can show it. The ordinal is the one the upload used,
    /// because the flat file name is built from it at both ends.
    /// </summary>
    private sealed record RetainedCaseFile(
        int Ordinal,
        string FileName,
        string MediaType,
        long ContentLength,
        string ContentHash,
        DocumentSemanticRole SemanticRole,
        string OperationKey,
        string BoxFileId,
        string? BoxVersionId,
        Guid? IntakeAssetId);

    private static async Task<WorkPayload> LoadPayloadAsync(
        PegasusDbContext context,
        string workKind,
        Guid caseId,
        string operationKey,
        string? caseRootCreationToken,
        string? auditFolderCreationToken,
        CancellationToken cancellationToken)
    {
        var caseEntity = await context.Cases
            .AsNoTracking()
            .SingleAsync(value => value.Id == caseId, cancellationToken);
        var receipt = await context.IntakeReceipts
            .AsNoTracking()
            .SingleAsync(value => value.Id == caseEntity.OriginIntakeReceiptId, cancellationToken);

        var source = await context.IntakeAssets
            .AsNoTracking()
            .Where(value => value.IntakeReceiptId == receipt.Id
                && value.Kind == "source"
                && value.Disposition == "source")
            .Select(value => new SourcePayload(
                value.Id,
                value.FileName,
                value.MediaType,
                value.ContentLength,
                value.ContentHash,
                value.StorageKey))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidDataException(
                "The processed intake receipt has no retained source lineage.");
        EnsureSourceMatchesReceipt(receipt, source);

        var stagedSource = await context.IntakeWorkItems
            .AsNoTracking()
            .Where(value => value.ProcessedReceiptId == receipt.Id)
            .Select(value => new StagedSourcePayload(
                value.StagedReceipt.SourceFileName,
                value.StagedReceipt.MediaType,
                value.StagedReceipt.SourceLength,
                value.StagedReceipt.SourceHash,
                value.StagedReceipt.SourceChannel,
                value.StagedReceipt.ExternalReceiptToken))
            .SingleOrDefaultAsync(cancellationToken);
        if (stagedSource is not null)
        {
            EnsureStagedSourceMatchesReceipt(receipt, stagedSource);
        }
        return new(
            workKind,
            caseEntity.Id,
            caseEntity.Type,
            caseEntity.Reference,
            caseEntity.AuditReference,
            receipt.Id,
            receipt.SourceFileName,
            receipt.MediaType,
            receipt.SourceHash,
            source.StorageKey,
            source.ContentLength,
            source.IntakeAssetId,
            operationKey,
            caseRootCreationToken,
            auditFolderCreationToken);
    }

    private static void EnsureSourceMatchesReceipt(
        IntakeReceiptEntity receipt,
        SourcePayload source)
    {
        if (source.ContentLength != receipt.SourceLength
            || !string.Equals(source.SourceHash, receipt.SourceHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.SourceFileName, receipt.SourceFileName, StringComparison.Ordinal)
            || !string.Equals(source.MediaType, receipt.MediaType, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(source.StorageKey))
        {
            throw new InvalidDataException(
                "The retained intake source lineage does not match the processed receipt.");
        }
    }

    private static void EnsureStagedSourceMatchesReceipt(
        IntakeReceiptEntity receipt,
        StagedSourcePayload source)
    {
        if (source.ContentLength != receipt.SourceLength
            || !string.Equals(source.SourceHash, receipt.SourceHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.SourceFileName, receipt.SourceFileName, StringComparison.Ordinal)
            || !string.Equals(source.MediaType, receipt.MediaType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.SourceChannel, receipt.SourceChannel, StringComparison.Ordinal)
            || !string.Equals(
                source.ExternalReceiptToken,
                receipt.ExternalReceiptToken,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The staged intake lineage does not match the processed receipt.");
        }
    }

    private async Task CompleteCaseCustodyAsync(
        Guid workId,
        string leaseToken,
        CaseCustodyRoot root,
        CustodyDocumentVersion version,
        string? auditFolderRemoteId,
        IReadOnlyList<RetainedCaseFile> retainedFiles,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var work = await TakeCompletableWorkAsync(context, workId, leaseToken, now, cancellationToken);
        if (work is null)
        {
            return;
        }

        var caseEntity = await context.Cases
            .SingleAsync(value => value.Id == work.CaseId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .SingleAsync(value => value.CaseId == work.CaseId, cancellationToken);
        ArchivedCaseGuard.RequireMutable(workflow);

        var beforeVersion = workflow.Version;
        caseEntity.CustodyRootRemoteId = root.RemoteId;
        caseEntity.CustodySourceRemoteId = version.RemoteId;
        caseEntity.CustodySourceContentHash = version.ContentHash;
        caseEntity.CustodySourceETag = version.ETag;
        caseEntity.CustodyConfirmedAtUtc = now;
        caseEntity.CustodyState = "confirmed";
        if (auditFolderRemoteId is not null)
        {
            caseEntity.AuditCustodyRemoteId = auditFolderRemoteId;
            caseEntity.AuditCustodyConfirmedAtUtc = now;
        }
        // CASE-013: this used to restate the readiness rule, and the copy was
        // stricter than the one in Core — it required staff confirmation that
        // CaseCompleteness.IsReadyForReview waives for an automatically
        // definitive intake. Core's rule had no caller at all, which is how
        // the two came to disagree. It has one now.
        var completeness = new CaseCompleteness(
            caseEntity.InstructionComplete,
            caseEntity.ImagesComplete,
            caseEntity.InstructionConfirmedByStaff,
            caseEntity.ImagesConfirmedByStaff);
        if (workflow.State == CaseLifecycleState.NotReady.ToString()
            && completeness.IsReadyForReview(automaticallyDefinitive: false))
        {
            workflow.State = CaseLifecycleState.Review.ToString();
        }
        await RecordRetainedCaseFilesAsync(context, caseEntity.Id, retainedFiles, now, cancellationToken);
        CaseMutationGuard.Complete(workflow);
        CompleteWork(work, now, version.RemoteId);
        context.Set<CaseHistoryEntity>().Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseEntity.Id,
            EventType = "custody_confirmed",
            Actor = "system",
            Reason = "Accepted source custody confirmed.",
            OccurredAtUtc = now,
            OperationKey = $"{work.OperationKey}:confirmed",
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CompleteAuditCustodyAsync(
        Guid workId,
        string leaseToken,
        CaseCustodyRoot root,
        string auditFolderRemoteId,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var work = await TakeCompletableWorkAsync(
            context,
            workId,
            leaseToken,
            now,
            cancellationToken,
            "The Audit custody work item lease was lost before completion could be persisted.");
        if (work is null)
        {
            return;
        }
        if (!string.Equals(
                work.Kind,
                ExternalWorkKinds.CreateAuditReferenceCustody,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The work item is not a later Audit custody operation.");
        }

        var caseEntity = await context.Cases
            .SingleAsync(value => value.Id == work.CaseId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .SingleAsync(value => value.CaseId == work.CaseId, cancellationToken);
        ArchivedCaseGuard.RequireMutable(workflow);
        if (string.IsNullOrWhiteSpace(caseEntity.AuditReference))
        {
            throw new InvalidDataException(
                "The later Audit custody operation has no immutable Audit identity.");
        }

        var beforeVersion = workflow.Version;
        caseEntity.CustodyRootRemoteId = root.RemoteId;
        caseEntity.AuditCustodyRemoteId = auditFolderRemoteId;
        caseEntity.AuditCustodyConfirmedAtUtc = now;
        CaseMutationGuard.Complete(workflow);
        CompleteWork(work, now, auditFolderRemoteId);
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseEntity.Id,
            EventType = "audit_custody_confirmed",
            Actor = "system",
            Reason = "Later Audit reference custody confirmed.",
            OccurredAtUtc = now,
            OperationKey = $"{work.OperationKey}:confirmed",
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The classified failure codes an operator's recovery depends on, and — for
    /// anything unclassified — the exception's own type name appended to the
    /// fallback.
    ///
    /// DOCS-008: two production audits failed custody with
    /// <c>custody_unexpected_failure</c> and nothing anywhere retained what
    /// actually threw, so diagnosis meant reading source and writing
    /// reproductions instead of reading a type. A type name carries no case
    /// content, this column is not operator-facing (the operator reads
    /// <see cref="GetFailureReason"/>, which is unchanged), and an unclassified
    /// failure that cannot say what it was is a defect in its own right.
    /// </summary>
    internal static string GetFailureCode(Exception exception) => exception switch
    {
        FileNotFoundException => "source_unavailable",
        InvalidDataException => "source_integrity_conflict",
        UnauthorizedAccessException => "custody_scope_denied",
        CustodyProcessingLeaseLostException => "custody_lease_lost",
        OperationCanceledException => "custody_cancelled",
        HttpRequestException or IOException => "custody_dependency_failure",
        _ => Truncate($"{UnexpectedFailureCode}:{exception.GetType().Name}")
    };

    private const string UnexpectedFailureCode = "custody_unexpected_failure";

    /// <summary>The column holds 100 characters; a long type name must not fail the write it is describing.</summary>
    private static string Truncate(string value) =>
        value.Length <= 100 ? value : value[..100];

    internal static string GetFailureReason(Exception exception) => GetFailureCode(exception) switch
    {
        "source_unavailable" => "The original evidence is unavailable from retained storage.",
        "source_integrity_conflict" => "The retained evidence no longer matches the accepted source.",
        "custody_scope_denied" => "The approved Case storage location could not be verified.",
        "custody_lease_lost" => "Case evidence storage stopped because this processing attempt no longer owns the work.",
        "custody_dependency_failure" =>
            "Case evidence could not be stored because the storage service was unavailable.",
        "custody_cancelled" => "Case evidence storage was interrupted before completion.",
        _ => "Case evidence could not be stored."
    };

    private static string RequireCreationOwner(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                "The custody operation has no predeclared remote creation owner.");
        }
        BoxCaseCustody.ValidateCreationOwnerToken(value);
        return value;
    }

    private static Guid RequireImageIntakeId(ExternalWorkItemEntity work) =>
        work.ImageIntakeId ?? throw new InvalidDataException(
            "The image-case custody work item has no owning Image intake.");

    private static async Task<ImageCreatePayload> LoadImageCreatePayloadAsync(
        PegasusDbContext context,
        Guid imageIntakeId,
        string operationKey,
        string? caseRootCreationToken,
        CancellationToken cancellationToken)
    {
        var intake = await context.ImageIntakes
            .AsNoTracking()
            .SingleAsync(value => value.Id == imageIntakeId, cancellationToken);
        var assets = await LoadImageAssetsAsync(context, intake, cancellationToken);
        return new(
            intake.Id,
            intake.ImageIntakeReference,
            operationKey,
            caseRootCreationToken,
            assets);
    }

    private static async Task<ImageMergePayload> LoadImageMergePayloadAsync(
        PegasusDbContext context,
        Guid imageIntakeId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var intake = await context.ImageIntakes
            .AsNoTracking()
            .SingleAsync(value => value.Id == imageIntakeId, cancellationToken);
        var mergedIntoCaseId = intake.MergedIntoCaseId
            ?? throw new InvalidDataException(
                "The image-case fold has no merged formal Case recorded.");
        var caseEntity = await context.Cases
            .AsNoTracking()
            .SingleAsync(value => value.Id == mergedIntoCaseId, cancellationToken);
        // The case root folder is named for the same reference the create path
        // used: the Audit reference for an Audit-type case, otherwise the Case
        // reference.
        var caseRootReference = string.Equals(caseEntity.Type, "audit", StringComparison.Ordinal)
            ? caseEntity.AuditReference ?? throw new InvalidDataException(
                "The Audit case has no allocated Audit reference for custody.")
            : caseEntity.Reference;
        return new(
            intake.Id,
            intake.ImageIntakeReference,
            intake.CustodyState,
            intake.CustodyRootRemoteId,
            caseEntity.Id,
            caseRootReference,
            caseEntity.CustodyRootRemoteId,
            operationKey);
    }

    /// <summary>
    /// Resolves the retained source images this registration covers: the
    /// origin receipt plus, for a group registration, every member receipt
    /// that is registered against this Image intake — resolved through the
    /// durable group membership (member → latest evaluation → processed
    /// receipt), exactly as registration itself resolved them, and ordered by
    /// the member ordinal so the stored numbering is stable.
    /// </summary>
    private static async Task<IReadOnlyList<ImageAssetPayload>> LoadImageAssetsAsync(
        PegasusDbContext context,
        ImageIntakeEntity intake,
        CancellationToken cancellationToken)
    {
        var receiptIds = await EfImageIntakeStore.ResolveOrderedImageReceiptIdsAsync(
            context,
            intake.OriginReceiptId,
            intake.SubmissionGroupId,
            cancellationToken);
        var receipts = await context.IntakeReceipts
            .AsNoTracking()
            .Where(receipt => receiptIds.Contains(receipt.Id))
            .ToDictionaryAsync(receipt => receipt.Id, cancellationToken);
        var sources = (await context.IntakeAssets
            .AsNoTracking()
            .Where(asset => receiptIds.Contains(asset.IntakeReceiptId)
                && asset.Kind == "source"
                && asset.Disposition == "source")
            .Select(asset => new
            {
                asset.IntakeReceiptId,
                Payload = new SourcePayload(
                    asset.Id,
                    asset.FileName,
                    asset.MediaType,
                    asset.ContentLength,
                    asset.ContentHash,
                    asset.StorageKey)
            })
            .ToArrayAsync(cancellationToken))
            .ToDictionary(source => source.IntakeReceiptId, source => source.Payload);

        var registeredDecision = EfIntakeReceiptStore.ToCode(IntakeDecision.ImageIntakeRegistered);
        var assets = new List<ImageAssetPayload>();
        foreach (var receiptId in receiptIds)
        {
            if (!receipts.TryGetValue(receiptId, out var receipt))
            {
                throw new InvalidDataException(
                    "A registered group member receipt no longer exists.");
            }
            if (!string.Equals(receipt.Decision, registeredDecision, StringComparison.Ordinal))
            {
                // A mixed-batch member (or a receipt a later staff decision
                // re-routed) is not part of this registration's image set.
                continue;
            }
            if (!sources.TryGetValue(receiptId, out var source))
            {
                throw new InvalidDataException(
                    "A registered image receipt has no retained source lineage.");
            }
            EnsureSourceMatchesReceipt(receipt, source);
            assets.Add(new(
                receipt.Id,
                receipt.SourceFileName,
                receipt.MediaType,
                receipt.SourceHash,
                source.StorageKey,
                source.ContentLength));
        }
        if (assets.Count == 0)
        {
            throw new InvalidDataException(
                "The Image intake has no registered image material to store.");
        }
        return assets;
    }

    private async Task ProcessImageCreateAsync(
        Guid workId,
        string leaseToken,
        ImageCreatePayload payload,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
    {
        var root = await caseCustody.CreateCaseRootAsync(
            payload.ImageIntakeId,
            payload.ImageReference,
            RequireCreationOwner(payload.CaseRootCreationToken),
            $"{payload.OperationKey}:root",
            leaseGuard,
            cancellationToken);
        await leaseGuard.RequireCurrentAsync(cancellationToken);
        for (var index = 0; index < payload.Assets.Count; index++)
        {
            var asset = payload.Assets[index];
            await caseCustody.RetainImageCaseAssetAsync(
                root,
                new(
                    asset.IntakeReceiptId,
                    asset.SourceFileName,
                    asset.MediaType,
                    asset.SourceHash,
                    asset.SourceObjectKey,
                    asset.SourceLength),
                index + 1,
                $"{payload.OperationKey}:asset:{asset.IntakeReceiptId:N}",
                leaseGuard,
                cancellationToken);
        }
        await leaseGuard.RequireCurrentAsync(cancellationToken);
        await CompleteImageCreateAsync(workId, leaseToken, root, cancellationToken);
    }

    private async Task ProcessImageMergeAsync(
        Guid workId,
        string leaseToken,
        ImageMergePayload payload,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
    {
        if (string.Equals(payload.ImageCustodyState, ImageCustodyStates.Merged, StringComparison.Ordinal))
        {
            await CompleteImageMergeAsync(
                workId, leaseToken, payload.CaseId, payload.ImageReference, folded: true, cancellationToken);
            return;
        }
        if (payload.ImageCustodyRootRemoteId is null)
        {
            if (payload.ImageCustodyState is null)
            {
                // Registered before image-case custody existed: there is no
                // external folder to fold, and nothing may be invented now.
                await CompleteImageMergeAsync(
                    workId, leaseToken, payload.CaseId, payload.ImageReference, folded: false, cancellationToken);
                return;
            }
            // The create-side work has not completed (or terminally failed and
            // awaits a reasoned retry); the fold retries after it lands.
            throw new IOException(
                "The image evidence folder has not been stored yet; the fold retries after it is.");
        }
        if (string.IsNullOrWhiteSpace(payload.CaseCustodyRootRemoteId))
        {
            throw new IOException(
                "The formal case evidence folder has not been stored yet; the fold retries after it is.");
        }

        var imageRoot = await caseCustody.GetExistingCaseRootAsync(
            payload.ImageIntakeId,
            payload.ImageReference,
            cancellationToken);
        var caseRoot = await caseCustody.GetExistingCaseRootAsync(
            payload.CaseId,
            payload.CaseRootReference,
            cancellationToken);
        await caseCustody.MergeImageCaseContentsAsync(
            imageRoot,
            caseRoot,
            $"{payload.OperationKey}:fold",
            leaseGuard,
            cancellationToken);
        await leaseGuard.RequireCurrentAsync(cancellationToken);
        await CompleteImageMergeAsync(
            workId, leaseToken, payload.CaseId, payload.ImageReference, folded: true, cancellationToken);
    }

    private async Task CompleteImageCreateAsync(
        Guid workId,
        string leaseToken,
        CaseCustodyRoot root,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var work = await TakeCompletableWorkAsync(context, workId, leaseToken, now, cancellationToken);
        if (work is null)
        {
            return;
        }

        var imageIntakeId = RequireImageIntakeId(work);
        var intake = await context.ImageIntakes
            .SingleAsync(value => value.Id == imageIntakeId, cancellationToken);
        intake.CustodyRootRemoteId = root.RemoteId;
        intake.CustodyConfirmedAtUtc ??= now;
        if (!string.Equals(intake.CustodyState, ImageCustodyStates.Merged, StringComparison.Ordinal))
        {
            intake.CustodyState = ImageCustodyStates.Confirmed;
        }
        CompleteWork(work, now, root.RemoteId);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CompleteImageMergeAsync(
        Guid workId,
        string leaseToken,
        Guid caseId,
        string imageReference,
        bool folded,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var work = await TakeCompletableWorkAsync(context, workId, leaseToken, now, cancellationToken);
        if (work is null)
        {
            return;
        }

        var imageIntakeId = RequireImageIntakeId(work);
        var intake = await context.ImageIntakes
            .SingleAsync(value => value.Id == imageIntakeId, cancellationToken);
        var alreadyMerged = string.Equals(intake.CustodyState, ImageCustodyStates.Merged, StringComparison.Ordinal);
        if (folded && !alreadyMerged)
        {
            intake.CustodyState = ImageCustodyStates.Merged;
            intake.CustodyMergedAtUtc ??= now;
            var workflow = await context.CaseWorkflows
                .SingleAsync(value => value.CaseId == caseId, cancellationToken);
            ArchivedCaseGuard.RequireMutable(workflow);
            var beforeVersion = workflow.Version;
            CaseMutationGuard.Complete(workflow);
            context.CaseHistory.Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                EventType = "image_custody_merged",
                Actor = "system",
                Reason = $"Image evidence {imageReference} was moved into the Case evidence storage.",
                OccurredAtUtc = now,
                OperationKey = $"{work.OperationKey}:confirmed",
                BeforeVersion = beforeVersion,
                AfterVersion = workflow.Version
            });
        }
        CompleteWork(work, now, intake.CustodyRootRemoteId);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Loads the work row for completion under the current processing lease.
    /// Returns null when another completion already made it terminal; throws
    /// when the lease was lost before the completion could be persisted.
    /// </summary>
    private static async Task<ExternalWorkItemEntity?> TakeCompletableWorkAsync(
        PegasusDbContext context,
        Guid workId,
        string leaseToken,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string leaseLostMessage =
            "The custody work item lease was lost before completion could be persisted.")
    {
        var work = await context.ExternalWorkItems
            .SingleOrDefaultAsync(
                value => value.Id == workId
                    && value.State == "processing"
                    && value.LeaseToken == leaseToken
                    && value.LeaseExpiresAtUtc > now,
                cancellationToken);
        if (work is not null)
        {
            return work;
        }

        var state = await context.ExternalWorkItems
            .AsNoTracking()
            .Where(value => value.Id == workId)
            .Select(value => value.State)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.Equals(state, "completed", StringComparison.Ordinal))
        {
            return null;
        }

        throw new InvalidOperationException(leaseLostMessage);
    }

    private static void CompleteWork(
        ExternalWorkItemEntity work,
        DateTimeOffset now,
        string? externalReceipt)
    {
        work.State = "completed";
        work.CompletedAtUtc = now;
        work.ExternalReceipt = externalReceipt;
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        work.FailureCode = null;
        work.FailureReason = null;
    }

    private abstract record CustodyWorkPayload;

    private sealed record ImageCreatePayload(
        Guid ImageIntakeId,
        string ImageReference,
        string OperationKey,
        string? CaseRootCreationToken,
        IReadOnlyList<ImageAssetPayload> Assets) : CustodyWorkPayload;

    private sealed record ImageAssetPayload(
        Guid IntakeReceiptId,
        string SourceFileName,
        string MediaType,
        string SourceHash,
        string SourceObjectKey,
        long SourceLength);

    private sealed record ImageMergePayload(
        Guid ImageIntakeId,
        string ImageReference,
        string? ImageCustodyState,
        string? ImageCustodyRootRemoteId,
        Guid CaseId,
        string CaseRootReference,
        string? CaseCustodyRootRemoteId,
        string OperationKey) : CustodyWorkPayload;

    private sealed record WorkPayload(
        string WorkKind,
        Guid CaseId,
        string CaseType,
        string CaseReference,
        string? AuditReference,
        Guid IntakeReceiptId,
        string SourceFileName,
        string MediaType,
        string SourceHash,
        string SourceObjectKey,
        long SourceLength,
        Guid SourceAssetId,
        string OperationKey,
        string? CaseRootCreationToken,
        string? AuditFolderCreationToken) : CustodyWorkPayload;

    private sealed record SourcePayload(
        Guid IntakeAssetId,
        string SourceFileName,
        string MediaType,
        long ContentLength,
        string SourceHash,
        string StorageKey);

    private sealed record StagedSourcePayload(
        string SourceFileName,
        string MediaType,
        long ContentLength,
        string SourceHash,
        string SourceChannel,
        string ExternalReceiptToken);

}
