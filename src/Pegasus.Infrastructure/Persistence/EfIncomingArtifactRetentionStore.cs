using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfIncomingArtifactRetentionStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory) : IIncomingArtifactRetentionStore
{
    internal const string PendingCode = "pending";
    internal const string ConfirmedCode = "confirmed";
    internal const string FailedCode = "failed";
    internal const string UnknownCode = "unknown";

    private static readonly Dictionary<IncomingArtifactCustodyState, string[]> ForwardSourceCodes =
        Enum.GetValues<IncomingArtifactCustodyState>()
            .ToDictionary(target => target, ForwardSourceCodesOf);

    public async Task<RetainedIncomingArtifact?> FindAsync(
        string operationKey,
        CancellationToken cancellationToken)
    {
        var (receiptId, assetId) = ParseOperationKey(operationKey);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var asset = await context.Set<IntakeAssetEntity>().AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == assetId && item.IntakeReceiptId == receiptId,
                cancellationToken);

        return asset is null
            ? null
            : new(
                asset.Id,
                operationKey,
                string.IsNullOrWhiteSpace(asset.CustodyStatus)
                    ? IncomingArtifactCustodyState.Unknown
                    : ParseCustodyState(asset.CustodyStatus),
                BoxFileId: asset.BoxFileId,
                BoxVersionId: asset.BoxVersionId,
                Sha256: asset.ContentHash,
                ContentLength: asset.ContentLength);
    }

    public async Task<bool> TryClaimHandOverAsync(
        string operationKey,
        CancellationToken cancellationToken)
    {
        var (receiptId, assetId) = ParseOperationKey(operationKey);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<IntakeAssetEntity>()
            .Where(item => item.Id == assetId
                && item.IntakeReceiptId == receiptId
                && item.CustodyStatus == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(item => item.CustodyStatus, UnknownCode),
                cancellationToken) == 1;
    }

    public async Task RecordAsync(
        RetainedIncomingArtifact artifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var (receiptId, assetId) = ParseOperationKey(artifact.OperationKey);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var targetState = ToCode(artifact.State);
        var sourceStates = ForwardSourceCodes[artifact.State];
        var movedAsset = await context.Set<IntakeAssetEntity>()
            .Where(item => item.Id == assetId
                && item.IntakeReceiptId == receiptId
                && (item.CustodyStatus == null || sourceStates.Contains(item.CustodyStatus)))
            .ExecuteUpdateAsync(
                update => update.SetProperty(item => item.CustodyStatus, targetState),
                cancellationToken);

        if (movedAsset == 0
            && !await context.Set<IntakeAssetEntity>().AsNoTracking()
                .AnyAsync(
                    item => item.Id == assetId && item.IntakeReceiptId == receiptId,
                    cancellationToken))
        {
            throw new KeyNotFoundException($"Intake asset '{assetId}' was not found.");
        }

        if (artifact.State == IncomingArtifactCustodyState.Confirmed
            && (artifact.BoxFileId is not null || artifact.BoxVersionId is not null))
        {
            await context.Set<IntakeAssetEntity>()
                .Where(item => item.Id == assetId
                    && item.IntakeReceiptId == receiptId
                    && item.CustodyStatus == ConfirmedCode)
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(item => item.BoxFileId,
                            item => artifact.BoxFileId ?? item.BoxFileId)
                        .SetProperty(item => item.BoxVersionId,
                            item => artifact.BoxVersionId ?? item.BoxVersionId),
                    cancellationToken);
        }
    }

    private static (Guid ReceiptId, Guid AssetId) ParseOperationKey(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var parts = operationKey.Split(':');
        if (parts.Length == 3
            && string.Equals(parts[0], "intake", StringComparison.Ordinal)
            && Guid.TryParseExact(parts[1], "N", out var receiptId)
            && Guid.TryParseExact(parts[2], "N", out var assetId))
        {
            return (receiptId, assetId);
        }

        throw new ArgumentException(
            "Incoming artifact retention keys must address an intake asset.",
            nameof(operationKey));
    }

    private static string[] ForwardSourceCodesOf(IncomingArtifactCustodyState target) =>
        [.. Enum.GetValues<IncomingArtifactCustodyState>()
            .Where(source => IncomingArtifactCustodyProgress.MovesForward(source, target))
            .Select(ToCode)];

    internal static string ToCode(IncomingArtifactCustodyState state) => state switch
    {
        IncomingArtifactCustodyState.Pending => PendingCode,
        IncomingArtifactCustodyState.Confirmed => ConfirmedCode,
        IncomingArtifactCustodyState.Failed => FailedCode,
        IncomingArtifactCustodyState.Unknown => UnknownCode,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    internal static IncomingArtifactCustodyState ParseCustodyState(string value) => value switch
    {
        PendingCode => IncomingArtifactCustodyState.Pending,
        ConfirmedCode => IncomingArtifactCustodyState.Confirmed,
        FailedCode => IncomingArtifactCustodyState.Failed,
        UnknownCode => IncomingArtifactCustodyState.Unknown,
        _ => throw new InvalidOperationException(
            $"The incoming artifact custody state '{value}' is not recognized.")
    };
}
