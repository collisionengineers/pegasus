using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The Worker's one-off pass that renames the Box folders of every Case the
/// <c>CaseReferenceSuffix</c> migration rewrote: <c>QDOS26157</c> becomes
/// <c>QDOS26157-p</c>, and its Audit folder <c>a.QDOS26157</c> becomes
/// <c>a.QDOS26157-p</c> (operator, 4 November 2026; R656). The Case folder is
/// proved by its name (<see cref="BoxContentClient.ProveCaseFolderAsync"/>),
/// so custody work for an existing Case fails until its folder carries the
/// new name; a failed work item retries and finds it renamed.
/// </summary>
/// <remarks>
/// Each folder is read by the id the Case recorded and renamed only when it
/// holds the old name, so a folder already renamed, or minted with the
/// suffix, is left alone. A folder holding neither name is logged and left,
/// and the pass carries on. When every Case's folders carry the new name, one
/// <c>ActionHistory</c> row records the pass done and later starts do
/// nothing; a pass with any failure records nothing and runs again at the
/// next start.
/// </remarks>
internal sealed partial class CaseReferenceSuffixBoxRename(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    TimeProvider timeProvider,
    ILogger<CaseReferenceSuffixBoxRename> logger) : BackgroundService
{
    internal const string AggregateType = "case_reference_suffix";
    internal const string CompletedEventKind = "case_reference_suffix_box_renamed";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Every hosted service finishes starting before this runs.
        await Task.Yield();
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
            {
                await started.Task.WaitAsync(stoppingToken);
            }
            await RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; the next start runs the pass again.
        }
        catch (Exception exception)
        {
            LogPassFailed(logger, exception);
        }
    }

    /// <summary>Runs the pass once; answers whether it is recorded done.</summary>
    internal async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        SuffixedCaseFolders[] cases;
        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            if (await context.ActionHistory.AsNoTracking().AnyAsync(
                    item => item.AggregateType == AggregateType && item.EventKind == CompletedEventKind,
                    cancellationToken))
            {
                return true;
            }

            cases = await context.Cases
                .AsNoTracking()
                .Where(item => item.CustodyRootRemoteId != null
                    && item.Reference.EndsWith(CaseReferenceFormat.ReferenceSuffix))
                .OrderBy(item => item.Reference)
                .Select(item => new SuffixedCaseFolders(
                    item.Id,
                    item.Reference,
                    item.CustodyRootRemoteId!,
                    item.AuditReference,
                    item.AuditCustodyRemoteId))
                .ToArrayAsync(cancellationToken);
        }

        var client = services.GetRequiredService<BoxContentClient>();
        var renamed = 0;
        var failed = 0;
        foreach (var folders in cases)
        {
            try
            {
                renamed += await RenameAsync(client, folders, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                failed++;
                LogCaseFailed(logger, folders.CaseId, folders.Reference, exception);
            }
        }

        if (failed > 0)
        {
            LogPassIncomplete(logger, renamed, failed);
            return false;
        }

        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            context.ActionHistory.Add(new()
            {
                Id = Guid.NewGuid(),
                AggregateType = AggregateType,
                AggregateId = SendToAiControlEntity.CaseReferenceSuffixId,
                EventKind = CompletedEventKind,
                ActorKind = nameof(ActorKind.SystemWorker),
                ActorSubjectId = "case-reference-suffix",
                ActorRolesJson = "[]",
                OccurredAtUtc = timeProvider.GetUtcNow(),
                Outcome = "Succeeded",
                CorrelationId = CompletedEventKind,
                Reason = $"{renamed} Box folders renamed for {cases.Length} Cases."
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        LogPassCompleted(logger, renamed, cases.Length);
        return true;
    }

    /// <summary>
    /// Renames one Case's folder, then its Audit folder, from the name without
    /// the suffix to the name with it; answers how many it renamed.
    /// </summary>
    internal static async Task<int> RenameAsync(
        BoxContentClient client,
        SuffixedCaseFolders folders,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(folders);
        var renamed = await RenameFolderAsync(
            client, folders.CaseFolderId, folders.Reference, client.RootFolderId, cancellationToken);
        if (folders.AuditReference is not null && folders.AuditFolderId is not null)
        {
            renamed += await RenameFolderAsync(
                client, folders.AuditFolderId, folders.AuditReference, folders.CaseFolderId, cancellationToken);
        }
        return renamed;
    }

    private static async Task<int> RenameFolderAsync(
        BoxContentClient client,
        string folderId,
        string reference,
        string parentId,
        CancellationToken cancellationToken)
    {
        var newName = CustodyNames.SafeName(reference);
        var oldName = CustodyNames.SafeName(CaseReferenceFormat.WithoutSuffix(reference));
        var folder = await client.GetFolderAsync(folderId, cancellationToken);
        if (!string.Equals(folder.ParentId, parentId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Case's Box folder is not where its Case keeps it.");
        }
        if (string.Equals(folder.Name, newName, StringComparison.Ordinal))
        {
            return 0;
        }
        if (!string.Equals(folder.Name, oldName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Case's Box folder holds neither its old nor its new name.");
        }

        var result = await client.RenameFolderAsync(
            folder.Id,
            newName,
            folder.ETag ?? throw new InvalidDataException("Box omitted the Case folder's ETag."),
            cancellationToken);
        if (!string.Equals(result.Id, folder.Id, StringComparison.Ordinal)
            || !string.Equals(result.Name, newName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Box did not rename the Case folder as asked.");
        }
        return 1;
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The Box folders of Case {CaseId} ({Reference}) could not be renamed to its -p reference.")]
    private static partial void LogCaseFailed(ILogger logger, Guid caseId, string reference, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The -p Box folder rename renamed {Renamed} folders and failed for {Failed} Cases; it runs again at the next start.")]
    private static partial void LogPassIncomplete(ILogger logger, int renamed, int failed);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The -p Box folder rename renamed {Renamed} folders for {Cases} Cases and is done.")]
    private static partial void LogPassCompleted(ILogger logger, int renamed, int cases);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The -p Box folder rename could not run; it runs again at the next start.")]
    private static partial void LogPassFailed(ILogger logger, Exception exception);
}

/// <summary>One rewritten Case's Box folders, by the ids it recorded.</summary>
internal sealed record SuffixedCaseFolders(
    Guid CaseId,
    string Reference,
    string CaseFolderId,
    string? AuditReference,
    string? AuditFolderId);
