using System.Collections.Concurrent;
using Pegasus.Core.Custody;

namespace Pegasus.IntegrationTests;

/// <summary>
/// An <see cref="ICaseCustody"/> that passes every call to a real adapter and
/// watches the files of one work item being filed: how many are in flight at
/// once and which ordinals started. It can hold the first retains until a
/// given number of them are in flight together, which only a caller that files
/// concurrently gets past; fail one chosen ordinal; run a hook once the held
/// retains are all in flight; and run a hook after each file is filed.
/// </summary>
internal sealed class CustodyBatchProbe(ICaseCustody inner, int holdUntilConcurrent = 0) : ICaseCustody
{
    private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(30);

    private readonly TaskCompletionSource allInFlight =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object gate = new();
    private int inFlight;
    private int arrived;

    /// <summary>The most retains that were in flight at one moment.</summary>
    public int MaxInFlight { get; private set; }

    /// <summary>Every ordinal a retain was asked for, in the order the calls began.</summary>
    public ConcurrentQueue<int> Started { get; } = new();

    /// <summary>The failure a retain of this ordinal ends in, if any.</summary>
    public Func<int, Exception?>? Failure { get; init; }

    /// <summary>Runs once, when the held retains are all in flight and before any is released.</summary>
    public Func<Task>? WhenAllInFlight { get; init; }

    /// <summary>
    /// Runs after the real adapter has filed an ordinal and before the caller
    /// has its result. That file's own lease check, made before the probe is
    /// called, has passed by then.
    /// </summary>
    public Func<int, Task>? WhenFiled { get; init; }

    public Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken) =>
        inner.CreateCaseRootAsync(caseId, caseReference, creationOwnerToken, operationKey, cancellationToken);

    public Task<CaseCustodyRoot> GetExistingCaseRootAsync(
        Guid caseId,
        string caseReference,
        CancellationToken cancellationToken) =>
        inner.GetExistingCaseRootAsync(caseId, caseReference, cancellationToken);

    public Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CancellationToken cancellationToken) =>
        inner.RetainAcceptedIntakeSourceAsync(root, source, operationKey, cancellationToken);

    public Task<CustodyDocumentVersion> RetainAcceptedIntakeAttachmentAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference attachment,
        int ordinal,
        string operationKey,
        CancellationToken cancellationToken) =>
        FileAsync(
            ordinal,
            () => inner.RetainAcceptedIntakeAttachmentAsync(
                root, attachment, ordinal, operationKey, cancellationToken),
            cancellationToken);

    public Task<CustodyDocumentVersion> RetainImageCaseAssetAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        int ordinal,
        string operationKey,
        CancellationToken cancellationToken) =>
        FileAsync(
            ordinal,
            () => inner.RetainImageCaseAssetAsync(root, source, ordinal, operationKey, cancellationToken),
            cancellationToken);

    public Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken) =>
        inner.CreateAuditReferenceFolderAsync(
            root, auditReference, creationOwnerToken, operationKey, cancellationToken);

    public Task MergeImageCaseContentsAsync(
        CaseCustodyRoot imageRoot,
        CaseCustodyRoot caseRoot,
        string operationKey,
        CancellationToken cancellationToken) =>
        inner.MergeImageCaseContentsAsync(imageRoot, caseRoot, operationKey, cancellationToken);

    private async Task<CustodyDocumentVersion> FileAsync(
        int ordinal,
        Func<Task<CustodyDocumentVersion>> file,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            inFlight++;
            MaxInFlight = Math.Max(MaxInFlight, inFlight);
        }
        Started.Enqueue(ordinal);
        try
        {
            if (Failure?.Invoke(ordinal) is { } failure)
            {
                await Task.Yield();
                throw failure;
            }
            if (holdUntilConcurrent > 0)
            {
                if (Interlocked.Increment(ref arrived) == holdUntilConcurrent)
                {
                    if (WhenAllInFlight is not null)
                    {
                        await WhenAllInFlight();
                    }
                    allInFlight.TrySetResult();
                }
                await allInFlight.Task.WaitAsync(HoldLimit, cancellationToken);
            }
            var version = await file();
            if (WhenFiled is not null)
            {
                await WhenFiled(ordinal);
            }
            return version;
        }
        finally
        {
            lock (gate)
            {
                inFlight--;
            }
        }
    }
}
