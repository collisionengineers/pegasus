using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfDocumentCustodyStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    LocalDocumentContentStore contentStore,
    TimeProvider timeProvider) :
    IAddCaseDocument,
    IDownloadCaseDocument,
    IExportCaseDocuments,
    ILogicallyRemoveDocument
{
    public async Task<AddCaseDocumentResult> ExecuteAsync(
        AddCaseDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateAddCommand(command);
        var contentHash = ComputeSha256(command.Content.Span);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var caseEntity = await RequireCaseAsync(context, command.CaseId, cancellationToken);

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
        EnsureExpectedVersion(caseEntity.Version, command.ExpectedCaseVersion);

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

        var now = timeProvider.GetUtcNow();
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
            CreatedBy = command.Actor.Trim(),
            IsCurrent = true
        };
        var occurrence = new DocumentOccurrenceEntity
        {
            Id = Guid.NewGuid(),
            CaseId = command.CaseId,
            DocumentId = document.Id,
            VersionId = version.Id,
            SemanticRole = command.SemanticRole,
            Source = command.Source,
            SourceOccurrenceIdentity = command.SourceOccurrenceIdentity,
            RecordedAtUtc = now,
            OperationKey = command.OperationKey
        };

        await contentStore.StoreAsync(
            command.CaseId,
            version.Id,
            command.Content,
            contentHash,
            cancellationToken);
        context.Add(version);
        context.Add(occurrence);
        caseEntity.Version = checked(caseEntity.Version + 1);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ToOccurrence(occurrence), ToVersion(version), false);
    }

    async Task<DocumentDownload?> IDownloadCaseDocument.ExecuteAsync(
        DownloadCaseDocumentQuery query,
        CancellationToken cancellationToken)
    {
        ValidateActor(query.Actor);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await context.Set<CaseEntity>().AnyAsync(value => value.Id == query.CaseId, cancellationToken))
        {
            return null;
        }

        var item = await (
            from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                on occurrence.VersionId equals version.Id
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
            return null;
        }

        var stream = await contentStore.OpenReadAsync(
            query.CaseId,
            item.Version.Id,
            item.Version.Sha256,
            item.Version.ContentLength,
            cancellationToken);
        return new(
            stream,
            item.Version.FileName,
            item.Version.MediaType,
            item.Version.ContentLength,
            item.Version.Sha256);
    }

    async Task<DocumentExport> IExportCaseDocuments.ExecuteAsync(
        ExportCaseDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        ValidateActor(command.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OperationKey);
        ArgumentNullException.ThrowIfNull(command.Selections);
        if (command.Selections.Count == 0 || command.Selections.Count != command.Selections.Distinct().Count())
        {
            throw new ArgumentException("At least one unique document selection is required.", nameof(command));
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        _ = await RequireCaseAsync(context, command.CaseId, cancellationToken);

        var requested = command.Selections
            .OrderBy(value => value.OccurrenceId)
            .ThenBy(value => value.VersionId)
            .ToArray();
        var items = new List<ExportItem>(requested.Length);
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
            items.Add(item);
        }

        return await BuildExportAsync(command.CaseId, items, cancellationToken);
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
        var caseEntity = await RequireCaseAsync(context, command.CaseId, cancellationToken);
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
        EnsureExpectedVersion(caseEntity.Version, command.ExpectedCaseVersion);

        version.IsLogicallyRemoved = true;
        version.IsCurrent = false;
        version.RemovalReason = command.Reason.Trim();
        version.RemovalOperationKey = command.OperationKey;
        caseEntity.Version = checked(caseEntity.Version + 1);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<DocumentExport> BuildExportAsync(
        Guid caseId,
        IReadOnlyList<ExportItem> items,
        CancellationToken cancellationToken)
    {
        var output = new MemoryStream();
        var manifest = new List<DocumentExportManifestEntry>(items.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "manifest.json"
        };
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
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
                await using var source = await contentStore.OpenReadAsync(
                    caseId,
                    item.Version.Id,
                    item.Version.Sha256,
                    item.Version.ContentLength,
                    cancellationToken);
                await source.CopyToAsync(destination, cancellationToken);
            }

            var manifestArchiveEntry = archive.CreateEntry("manifest.json", CompressionLevel.NoCompression);
            manifestArchiveEntry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            await using var manifestStream = manifestArchiveEntry.Open();
            await JsonSerializer.SerializeAsync(manifestStream, manifest, cancellationToken: cancellationToken);
        }

        output.Position = 0;
        return new(output, $"case-{caseId:N}-documents.zip", manifest);
    }

    private static async Task<CaseEntity> RequireCaseAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return await context.Set<CaseEntity>()
            .SingleOrDefaultAsync(value => value.Id == caseId, cancellationToken)
            ?? throw new InvalidOperationException("The case is unavailable.");
    }

    private static void EnsureExpectedVersion(long actualVersion, long? expectedVersion)
    {
        if (expectedVersion is not null && expectedVersion.Value != actualVersion)
        {
            throw new DbUpdateConcurrencyException("The case version is stale.");
        }
    }

    private static void ValidateAddCommand(AddCaseDocumentCommand command)
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

    private static void ValidateActor(string actor) => ArgumentException.ThrowIfNullOrWhiteSpace(actor);

    private static void EnsureReplayMatches(
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

    private static string GetSafeFileName(string fileName)
    {
        var value = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.Any(char.IsControl))
        {
            throw new ArgumentException("The document file name is invalid.", nameof(fileName));
        }

        return value;
    }

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

    private static string ComputeSha256(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static DocumentOccurrence ToOccurrence(DocumentOccurrenceEntity value) => new(
        value.Id,
        value.CaseId,
        value.DocumentId,
        value.VersionId,
        value.SemanticRole,
        value.Source,
        value.SourceOccurrenceIdentity,
        value.RecordedAtUtc);

    private static DocumentVersion ToVersion(DocumentVersionEntity value) => new(
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

    private sealed record ExportItem(
        DocumentOccurrenceEntity Occurrence,
        DocumentVersionEntity Version);
}
