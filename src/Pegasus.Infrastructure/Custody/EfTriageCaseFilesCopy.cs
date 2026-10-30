using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// Copies a linked Triage Case's files onto its instruction Case
/// (<see cref="TriageCaseFilesPolicy"/>, FRD-03). Box has no copy, so each
/// file is read from the Triage Case's custody, verified against its hash,
/// and filed on the Case as a document of its own under the Case's next
/// number, with the crop, rotation, tags and report choice the photograph
/// had. The link queued this work in its own transaction. Each file has its
/// own operation key under the Triage and the Case, so a replay or a relink
/// to the same Case files nothing twice.
/// </summary>
internal sealed class EfTriageCaseFilesCopy(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    IExternalWorkStore workStore,
    IDocumentContentStore documentContentStore,
    ICaseArtifactCustody artifactCustody,
    TimeProvider timeProvider) : IProcessQueuedTriageCaseFiles
{
    internal const string ActorId = "triage-case-files";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The operation key every file copied from this Triage Case starts with;
    /// a Case's occurrence keys are unique within the Case.
    /// </summary>
    internal static string FileKeyPrefix(Guid triageCaseId) => $"triage-case-file:{triageCaseId:N}:";

    public async Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("A Triage files work identifier is required.", nameof(workItemId));
        }

        var claim = await ClaimAsync(workItemId, cancellationToken);
        if (claim is null)
        {
            return;
        }

        try
        {
            await CopyAsync(claim, cancellationToken);
        }
        catch (Exception exception)
        {
            await workStore.FailProcessingAsync(
                claim.Id,
                claim.LeaseToken,
                timeProvider.GetUtcNow(),
                EfQueuedCustodyProcessor.GetFailureCode(exception),
                EfQueuedCustodyProcessor.GetFailureReason(exception),
                CancellationToken.None);
            throw;
        }
    }

    private async Task<Claim?> ClaimAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var work = await context.ExternalWorkItems.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken)
            ?? throw new InvalidOperationException("The Triage files work item is unavailable.");
        if (work.Kind != ExternalWorkKinds.CopyTriageCaseFiles)
        {
            throw new InvalidDataException("The external work item is not a Triage files copy.");
        }
        if (work.State is ExternalWorkStatePersistence.Completed or ExternalWorkStatePersistence.Failed)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (work.State == ExternalWorkStatePersistence.Processing && work.LeaseExpiresAtUtc > now)
        {
            throw new InvalidOperationException("The Triage files work item is already leased.");
        }

        var leaseToken = Guid.NewGuid().ToString("N");
        var claimed = await context.ExternalWorkItems
            .Where(value => value.Id == work.Id
                && value.State == work.State
                && value.LeaseToken == work.LeaseToken
                && value.LeaseExpiresAtUtc == work.LeaseExpiresAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.State, ExternalWorkStatePersistence.Processing)
                .SetProperty(value => value.AttemptCount, value => value.AttemptCount + 1)
                .SetProperty(value => value.LeaseToken, leaseToken)
                .SetProperty(value => value.LeaseExpiresAtUtc, now.Add(LeaseDuration))
                .SetProperty(value => value.FailureCode, (string?)null)
                .SetProperty(value => value.FailureReason, (string?)null),
                cancellationToken);
        return claimed == 0
            ? null
            : new(
                work.Id,
                leaseToken,
                work.CaseId ?? throw new InvalidDataException("The Triage files work item has no Case."),
                work.SourceCaseId ?? throw new InvalidDataException("The Triage files work item has no Triage Case."),
                work.OperationKey);
    }

    private async Task CopyAsync(Claim claim, CancellationToken cancellationToken)
    {
        var prefix = FileKeyPrefix(claim.TriageCaseId);
        IReadOnlyList<SourceFile> files;
        IReadOnlyList<TriageCaseFile> selected;
        await using (var context = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            // The Triage Case's own files land first: its custody, a vehicle
            // images fold into it, and any version still being filed.
            if (await TriageFilesArePendingAsync(context, claim.TriageCaseId, cancellationToken))
            {
                await workStore.DeferProcessingAsync(
                    claim.Id,
                    claim.LeaseToken,
                    timeProvider.GetUtcNow().Add(ImageCustodyRetryPolicy.FolderWait),
                    cancellationToken);
                return;
            }

            files = await ReadTriageFilesAsync(context, claim.TriageCaseId, cancellationToken);
            // A file this copy filed before is not "already held": its replay
            // returns the same document and its preparation is applied again.
            var held = await (
                    from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                    join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                        on occurrence.VersionId equals version.Id
                    where occurrence.CaseId == claim.CaseId
                        && version.IsCurrent
                        && !version.IsLogicallyRemoved
                        && !occurrence.OperationKey.StartsWith(prefix)
                    select version.Sha256)
                .ToArrayAsync(cancellationToken);
            selected = TriageCaseFilesPolicy.SelectFilesToCopy(
                [.. files.Select(file => new TriageCaseFile(file.Occurrence.Id, file.Sha256, file.Occurrence.SemanticRole))],
                held);
        }

        var sources = files.ToDictionary(file => file.Occurrence.Id);
        var actor = ActionActor.SystemWorker(ActorId);
        var copied = new List<(SourceFile Source, Guid OccurrenceId)>(selected.Count);
        await using (artifactCustody.BeginDocumentBatch())
        {
            foreach (var file in selected)
            {
                if (!await workStore.HoldsProcessingLeaseAsync(claim.Id, claim.LeaseToken, cancellationToken))
                {
                    throw new CustodyProcessingLeaseLostException();
                }

                var source = sources[file.OccurrenceId];
                await using var content = await documentContentStore.OpenReadVersionAsync(
                    source.Address, source.Sha256, source.ContentLength, cancellationToken);
                var result = await artifactCustody.RetainAsync(
                    new(
                        actor,
                        claim.CaseId,
                        null,
                        source.Occurrence.Id.ToString("N"),
                        prefix + source.Occurrence.Id.ToString("N"),
                        source.Address.FileName,
                        source.Address.MediaType,
                        source.ContentLength,
                        source.Sha256,
                        content,
                        source.Occurrence.SemanticRole,
                        source.Occurrence.Source),
                    cancellationToken);
                copied.Add((source, result.OccurrenceId
                    ?? throw new InvalidDataException("The copied Triage file has no Case occurrence.")));
            }
        }

        await RecordAsync(claim, copied, TriageCaseFilesPolicy.CompletesImages(selected), cancellationToken);
    }

    private static async Task<bool> TriageFilesArePendingAsync(
        PegasusDbContext context,
        Guid triageCaseId,
        CancellationToken cancellationToken) =>
        await context.ExternalWorkItems.AsNoTracking().AnyAsync(
            item => item.CaseId == triageCaseId
                && (item.Kind == ExternalWorkKinds.CreateCaseCustody
                    || item.Kind == ExternalWorkKinds.MergeImageCaseCustody)
                && item.State != ExternalWorkStatePersistence.Completed
                && item.State != ExternalWorkStatePersistence.Failed,
            cancellationToken)
        || await (
                from version in context.Set<DocumentVersionEntity>().AsNoTracking()
                join document in context.Set<CaseDocumentEntity>().AsNoTracking()
                    on version.DocumentId equals document.Id
                where document.CaseId == triageCaseId
                    && version.IsCurrent
                    && !version.IsLogicallyRemoved
                    && version.CustodyStatus == DocumentCustodyStatus.Pending
                select version.Id)
            .AnyAsync(cancellationToken);

    /// <summary>The Triage Case's current filed documents, in document order, with what a copy needs.</summary>
    private static async Task<IReadOnlyList<SourceFile>> ReadTriageFilesAsync(
        PegasusDbContext context,
        Guid triageCaseId,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                join document in context.Set<CaseDocumentEntity>().AsNoTracking()
                    on occurrence.DocumentId equals document.Id
                join caseEntity in context.Cases.AsNoTracking()
                    on occurrence.CaseId equals caseEntity.Id
                where occurrence.CaseId == triageCaseId
                    && version.DocumentId == occurrence.DocumentId
                    && version.IsCurrent
                    && !version.IsLogicallyRemoved
                    && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                orderby document.Ordinal, occurrence.Ordinal
                select new
                {
                    Occurrence = occurrence,
                    version.Id,
                    version.Version,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256,
                    version.BoxFileId,
                    version.BoxVersionId,
                    caseEntity.Reference,
                    Root = document.CustodyFolder == CaseCustodyFolders.Audit
                        ? caseEntity.AuditCustodyRemoteId
                        : caseEntity.CustodyRootRemoteId
                })
            .ToArrayAsync(cancellationToken);
        return [.. rows.Select(row => new SourceFile(
            row.Occurrence,
            new ManagedDocumentContentAddress(
                triageCaseId,
                row.Reference,
                row.Root,
                row.Occurrence.Id,
                row.Occurrence.Ordinal,
                row.Occurrence.DocumentId,
                row.Id,
                row.Version,
                row.FileName,
                row.MediaType,
                row.BoxFileId,
                row.BoxVersionId),
            row.Sha256,
            row.ContentLength))];
    }

    /// <summary>
    /// Gives each copied photograph the preparation and tags it had on the
    /// Triage Case, completes the Case's images when photographs arrived, and
    /// completes the work, in one transaction under the work's lease.
    /// </summary>
    private async Task RecordAsync(
        Claim claim,
        IReadOnlyList<(SourceFile Source, Guid OccurrenceId)> copied,
        bool completesImages,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var work = await context.ExternalWorkItems
                .SingleOrDefaultAsync(item => item.Id == claim.Id, cancellationToken)
            ?? throw new InvalidOperationException("The Triage files work item is unavailable.");
        if (work.State != ExternalWorkStatePersistence.Processing
            || work.LeaseToken != claim.LeaseToken
            || work.LeaseExpiresAtUtc <= now)
        {
            throw new CustodyProcessingLeaseLostException();
        }

        if (copied.Count > 0)
        {
            var authority = await CaseMutationGuard.LoadAsync(context, claim.CaseId, cancellationToken)
                ?? throw new InvalidOperationException("The linked instruction Case is unavailable.");
            var targetIds = copied.Select(item => item.OccurrenceId).ToArray();
            var targets = await context.Set<DocumentOccurrenceEntity>()
                .Where(item => targetIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            var sourceIds = copied.Select(item => item.Source.Occurrence.Id).ToArray();
            var sourceTags = await context.Set<DocumentOccurrenceTagEntity>().AsNoTracking()
                .Where(item => sourceIds.Contains(item.OccurrenceId))
                .ToArrayAsync(cancellationToken);
            var heldTags = (await context.Set<DocumentOccurrenceTagEntity>().AsNoTracking()
                    .Where(item => targetIds.Contains(item.OccurrenceId))
                    .Select(item => new { item.OccurrenceId, item.TagId })
                    .ToArrayAsync(cancellationToken))
                .Select(item => (item.OccurrenceId, item.TagId))
                .ToHashSet();
            foreach (var (source, occurrenceId) in copied)
            {
                var target = targets[occurrenceId];
                var from = source.Occurrence;
                target.InReport = from.InReport;
                target.RotationDegrees = from.RotationDegrees;
                target.CropLeft = from.CropLeft;
                target.CropTop = from.CropTop;
                target.CropWidth = from.CropWidth;
                target.CropHeight = from.CropHeight;
                target.PreparedBy = from.PreparedBy;
                target.PreparedAtUtc = from.PreparedAtUtc;
                target.PreparationFullPage = from.PreparationFullPage;
                foreach (var tag in sourceTags.Where(item => item.OccurrenceId == from.Id))
                {
                    if (heldTags.Add((target.Id, tag.TagId)))
                    {
                        context.Add(new DocumentOccurrenceTagEntity
                        {
                            OccurrenceId = target.Id,
                            TagId = tag.TagId,
                            AppliedByKind = tag.AppliedByKind,
                            AppliedBySubjectId = tag.AppliedBySubjectId,
                            AppliedAtUtc = tag.AppliedAtUtc,
                            OperationKey = $"triage-tag:{target.Id:N}:{tag.TagId:N}"
                        });
                    }
                }
            }

            var beforeVersion = authority.Version;
            if (completesImages && authority.Workflow is { } workflow && !authority.Case.ImagesComplete)
            {
                var (before, after, evaluation) = await EfCaseArtifactCustody.CompleteCaseImagesAsync(
                    context, workflow, now, cancellationToken);
                var imagesKey = $"{claim.OperationKey}:images-complete";
                CaseMutationHistory.Add(
                    context,
                    workflow,
                    ActionActor.SystemWorker(ActorId),
                    imagesKey,
                    "The linked Triage Case's photographs completed the Case's images.",
                    "case_images_completed_from_triage_photographs",
                    CaseOperationReplay.Hash(imagesKey),
                    beforeVersion,
                    checked(beforeVersion + 1),
                    JsonSerializer.Serialize(before),
                    JsonSerializer.Serialize(after),
                    $"{evaluation.PolicyKey}/v{evaluation.PolicyVersion}",
                    now);
            }

            authority.CompleteSystemMutation();
            context.CaseHistory.Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = claim.CaseId,
                EventType = "triage_case_files_copied",
                Actor = "system",
                Reason = $"The files of linked Triage Case {copied[0].Source.Address.CaseReference} were copied to the Case.",
                OccurredAtUtc = now,
                OperationKey = $"{claim.OperationKey}:copied",
                BeforeVersion = beforeVersion,
                AfterVersion = authority.Version
            });
        }

        work.State = ExternalWorkStatePersistence.Completed;
        work.CompletedAtUtc = now;
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        work.FailureCode = null;
        work.FailureReason = null;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record Claim(Guid Id, string LeaseToken, Guid CaseId, Guid TriageCaseId, string OperationKey);

    private sealed record SourceFile(
        DocumentOccurrenceEntity Occurrence,
        ManagedDocumentContentAddress Address,
        string Sha256,
        long ContentLength);
}
