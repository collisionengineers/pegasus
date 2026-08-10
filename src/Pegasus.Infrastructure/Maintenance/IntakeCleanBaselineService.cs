using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pegasus.Infrastructure.Maintenance;

internal sealed class IntakeCleanBaselineService(
    ProductionIntakeCleanBaselineInvocation invocation,
    ICleanBaselineAccessValidator accessValidator,
    ICleanBaselineSqlStore sql,
    ICleanBaselineBlobStore blobs,
    ICleanBaselineQueueStore queues,
    ICleanBaselineGraphClient graph,
    TimeProvider timeProvider)
{
    private const int ManifestSchemaVersion = 2;

    internal async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        ValidateInvocation(invocation);
        var access = await accessValidator.ValidateAsync(cancellationToken);
        var accessJson = JsonSerializer.Serialize(
            access,
            CleanBaselineJsonContext.Default.CleanBaselineAccessReport);
        var accessHash = Sha256(accessJson);

        return invocation.Operation switch
        {
            CleanBaselineOperation.ValidateAccess => accessJson,
            CleanBaselineOperation.Plan => await PlanAsync(accessHash, cancellationToken),
            CleanBaselineOperation.Execute => await ExecuteAsync(accessHash, cancellationToken),
            CleanBaselineOperation.Verify => await VerifyAsync(accessHash, cancellationToken),
            _ => throw new InvalidOperationException("The maintenance operation is unsupported.")
        };
    }

    private async Task<string> PlanAsync(
        string accessHash,
        CancellationToken cancellationToken)
    {
        var cutoff = invocation.PreTestCutoffUtc
            ?? throw new InvalidOperationException("Plan requires PreTestCutoffUtc.");
        var path = RequireOutputPath(invocation.ManifestPath, "Plan requires ManifestPath.");
        if (File.Exists(path))
        {
            throw new InvalidOperationException(
                "Plan refuses to overwrite an existing manifest. Choose a new ignored path.");
        }

        var snapshot = await ReadSnapshotAsync(cutoff, sqlSession: null, cancellationToken);
        ThrowIfStopped(snapshot.StopConditions);
        var manifest = new CleanBaselineManifest(
            ManifestSchemaVersion,
            Guid.NewGuid().ToString("N"),
            timeProvider.GetUtcNow(),
            cutoff,
            Scope(),
            accessHash,
            snapshot.SqlRows,
            snapshot.Blobs,
            snapshot.QueueMessages,
            snapshot.TargetStagedReceiptIds,
            snapshot.Retained,
            snapshot.PollCursorSha256,
            snapshot.StopConditions,
            SnapshotHash(snapshot));
        var json = JsonSerializer.Serialize(
            manifest,
            CleanBaselineJsonContext.Default.CleanBaselineManifest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(false), cancellationToken);
        var hash = Sha256(await File.ReadAllBytesAsync(path, cancellationToken));
        return JsonSerializer.Serialize(new
        {
            operation = "Plan",
            manifestPath = path,
            manifestSha256 = hash,
            sqlRowCount = manifest.SqlRows.Count,
            blobCount = manifest.Blobs.Count,
            queueMessageCount = manifest.QueueMessages.Count,
            stopConditionCount = manifest.StopConditions.Count,
            result = "planned"
        });
    }

    private async Task<string> ExecuteAsync(
        string accessHash,
        CancellationToken cancellationToken)
    {
        var (manifest, manifestHash) = await ReadApprovedManifestAsync(cancellationToken);
        var receiptPath = RequireOutputPath(
            invocation.ExecutionReceiptPath,
            "Execute requires ExecutionReceiptPath.");
        CleanBaselineExecutionReceipt? existingReceipt = null;
        if (File.Exists(receiptPath))
        {
            existingReceipt = await ReadExecutionReceiptAsync(receiptPath, cancellationToken);
            if (!string.Equals(existingReceipt.ManifestSha256, manifestHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The execution receipt path belongs to a different manifest.");
            }
        }
        ValidateManifestScope(manifest);
        if (!string.Equals(manifest.AccessCensusSha256, accessHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The access census drifted after Plan; create and approve a new manifest.");
        }

        await using var lockedSql = await sql.BeginLockedExecutionAsync(cancellationToken);
        var snapshot = await ReadSnapshotAsync(
            manifest.PreTestCutoffUtc,
            lockedSql,
            cancellationToken);
        ThrowIfStopped(snapshot.StopConditions);
        EnsureRetainedFingerprint(manifest.Retained, snapshot.Retained);
        if (!string.Equals(manifest.PollCursorBeforeSha256, snapshot.PollCursorSha256, StringComparison.Ordinal)
            && (existingReceipt is null
                || !string.Equals(
                    existingReceipt.BaselineCursorSha256,
                    snapshot.PollCursorSha256,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The approved Inbox poll cursor drifted after Plan.");
        }
        if (existingReceipt is not null
            && snapshot.SqlRows.Count == 0
            && snapshot.Blobs.Count == 0
            && snapshot.QueueMessages.Count == 0
            && string.Equals(
                snapshot.PollCursorSha256,
                existingReceipt.BaselineCursorSha256,
                StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(
                existingReceipt,
                CleanBaselineJsonContext.Default.CleanBaselineExecutionReceipt);
        }
        EnsureManifestTargetsUnchanged(manifest, snapshot);

        var baseline = await graph.AcquireBaselineAsync(cancellationToken);
        await using var preparedBlobs = await blobs.PrepareDeleteAsync(
            manifest.Blobs,
            cancellationToken);
        await using var preparedQueues = await queues.PrepareDeleteAsync(
            manifest.QueueMessages,
            cancellationToken);
        var deletedRows = await lockedSql.DeleteExactRowsAsync(manifest.SqlRows, cancellationToken);
        var deletedBlobs = await preparedBlobs.DeleteAsync(cancellationToken);
        var deletedQueues = await preparedQueues.DeleteAsync(cancellationToken);
        await lockedSql.WritePollCursorAsync(
            invocation.MailboxIdentity,
            manifest.PollCursorBeforeSha256,
            baseline.Cursor,
            cancellationToken);
        await lockedSql.CommitAsync(cancellationToken);

        var receipt = new CleanBaselineExecutionReceipt(
            1,
            manifestHash,
            timeProvider.GetUtcNow(),
            baseline.CursorSha256,
            deletedRows,
            deletedQueues,
            deletedBlobs,
            "executed");
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        await File.WriteAllTextAsync(
            receiptPath,
            JsonSerializer.Serialize(
                receipt,
                CleanBaselineJsonContext.Default.CleanBaselineExecutionReceipt),
            new UTF8Encoding(false),
            cancellationToken);
        return JsonSerializer.Serialize(receipt, CleanBaselineJsonContext.Default.CleanBaselineExecutionReceipt);
    }

    private async Task<string> VerifyAsync(
        string accessHash,
        CancellationToken cancellationToken)
    {
        var (manifest, manifestHash) = await ReadApprovedManifestAsync(cancellationToken);
        ValidateManifestScope(manifest);
        if (!string.Equals(manifest.AccessCensusSha256, accessHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The access census no longer matches the approved manifest.");
        }
        var receiptPath = RequireExistingPath(
            invocation.ExecutionReceiptPath,
            "Verify requires the content-safe execution receipt.");
        var receipt = await ReadExecutionReceiptAsync(receiptPath, cancellationToken);
        if (!string.Equals(receipt.ManifestSha256, manifestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The execution receipt is not bound to this manifest.");
        }

        var remainingRows = await sql.CountExistingRowsAsync(manifest.SqlRows, cancellationToken);
        var remainingQueues = await queues.CountTargetMessagesAsync(
            manifest.TargetStagedReceiptIds.ToHashSet(),
            cancellationToken);
        var remainingBlobs = await blobs.CountExistingAsync(manifest.Blobs, cancellationToken);
        var retained = await sql.ReadRetainedFingerprintAsync(cancellationToken);
        var retainedUnchanged = retained == manifest.Retained;
        var pollCursorHash = await sql.ReadPollCursorHashAsync(
            invocation.MailboxIdentity,
            cancellationToken);
        var cursorMatches = string.Equals(
            pollCursorHash,
            receipt.BaselineCursorSha256,
            StringComparison.Ordinal);
        if (remainingRows != 0
            || remainingQueues != 0
            || remainingBlobs != 0
            || !retainedUnchanged
            || !cursorMatches)
        {
            throw new InvalidOperationException(
                "Verify failed: one or more exact cleanup or retained-record invariants do not hold.");
        }

        var report = new CleanBaselineVerificationReport(
            1,
            manifestHash,
            timeProvider.GetUtcNow(),
            remainingRows,
            remainingQueues,
            remainingBlobs,
            retainedUnchanged,
            cursorMatches,
            "verified");
        return JsonSerializer.Serialize(
            report,
            CleanBaselineJsonContext.Default.CleanBaselineVerificationReport);
    }

    private async Task<CleanBaselineSnapshot> ReadSnapshotAsync(
        DateTimeOffset cutoff,
        ICleanBaselineSqlSession? sqlSession,
        CancellationToken cancellationToken)
    {
        var source = sqlSession ?? sql;
        var inventory = await source.InventoryAsync(
            cutoff,
            invocation.MailboxIdentity,
            cancellationToken);
        var blobInventory = await blobs.InspectExactAsync(
            inventory.BlobReferences,
            cancellationToken);
        var queueInventory = await queues.InspectAsync(
            inventory.TargetStagedReceiptIds,
            cancellationToken);
        var stops = inventory.StopConditions
            .Concat(queueInventory.StopConditions)
            .Concat(blobInventory
                .Where(item => item.TotalReferenceCount != item.TargetReferenceCount)
                .Select(item => Stop(
                    "shared_blob",
                    "Blob",
                    item.Name,
                    "The content-addressed Blob has a non-target SQL reference.")))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.ResourceIdentityHash, StringComparer.Ordinal)
            .ToArray();
        return new(
            inventory.Rows,
            blobInventory,
            queueInventory.Messages,
            inventory.TargetStagedReceiptIds.Order().ToArray(),
            await source.ReadRetainedFingerprintAsync(cancellationToken),
            await source.ReadPollCursorHashAsync(invocation.MailboxIdentity, cancellationToken),
            stops);
    }

    private async Task<(CleanBaselineManifest Manifest, string Hash)> ReadApprovedManifestAsync(
        CancellationToken cancellationToken)
    {
        var path = RequireExistingPath(invocation.ManifestPath, "The approved manifest is required.");
        var approvedHash = invocation.ManifestSha256;
        if (approvedHash is null || !IsSha256(approvedHash))
        {
            throw new InvalidOperationException(
                "Execute and Verify require the operator-approved 64-character manifest SHA-256.");
        }
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var actualHash = Sha256(bytes);
        if (!string.Equals(actualHash, approvedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The manifest SHA-256 does not match operator approval.");
        }
        var manifest = JsonSerializer.Deserialize(
            bytes,
            CleanBaselineJsonContext.Default.CleanBaselineManifest)
            ?? throw new InvalidDataException("The approved manifest is empty.");
        if (manifest.SchemaVersion != ManifestSchemaVersion || !IsSha256(manifest.SnapshotSha256))
        {
            throw new InvalidDataException("The approved manifest schema is unsupported.");
        }
        return (manifest, actualHash);
    }

    private static async Task<CleanBaselineExecutionReceipt> ReadExecutionReceiptAsync(
        string path,
        CancellationToken cancellationToken) =>
        JsonSerializer.Deserialize(
            await File.ReadAllBytesAsync(path, cancellationToken),
            CleanBaselineJsonContext.Default.CleanBaselineExecutionReceipt)
        ?? throw new InvalidDataException("The execution receipt is empty.");

    private void ValidateManifestScope(CleanBaselineManifest manifest)
    {
        if (manifest.Scope != Scope())
        {
            throw new InvalidOperationException("The manifest scope differs from this invocation.");
        }
        ThrowIfStopped(manifest.StopConditions);
    }

    private static void EnsureManifestTargetsUnchanged(
        CleanBaselineManifest manifest,
        CleanBaselineSnapshot current)
    {
        var plannedRows = manifest.SqlRows.ToDictionary(RowIdentity, StringComparer.Ordinal);
        var currentRows = current.SqlRows.ToDictionary(RowIdentity, StringComparer.Ordinal);
        if (!currentRows.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(plannedRows.Keys))
        {
            throw new InvalidOperationException(
                "The exact SQL target set drifted after Plan; create and approve a new manifest.");
        }
        foreach (var (identity, planned) in plannedRows)
        {
            if (currentRows.TryGetValue(identity, out var observed)
                && !string.Equals(planned.RowSha256, observed.RowSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"SQL row drift detected for {identity}.");
            }
        }
        var plannedBlobs = manifest.Blobs.ToDictionary(item => item.Name, StringComparer.Ordinal);
        if (!current.Blobs.Select(item => item.Name).ToHashSet(StringComparer.Ordinal)
            .SetEquals(plannedBlobs.Keys))
        {
            throw new InvalidOperationException("The exact Blob target set drifted after Plan.");
        }
        foreach (var observed in current.Blobs)
        {
            if (!plannedBlobs.TryGetValue(observed.Name, out var planned)
                || !string.Equals(planned.ETag, observed.ETag, StringComparison.Ordinal)
                || planned.Length != observed.Length
                || !string.Equals(planned.ContentSha256, observed.ContentSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Blob identity or ETag drifted after Plan.");
            }
        }
        var plannedQueues = manifest.QueueMessages.ToDictionary(QueueIdentity, StringComparer.Ordinal);
        if (!current.QueueMessages.Select(QueueIdentity).ToHashSet(StringComparer.Ordinal)
            .SetEquals(plannedQueues.Keys))
        {
            throw new InvalidOperationException("The exact queue target set drifted after Plan.");
        }
        foreach (var observed in current.QueueMessages)
        {
            if (!plannedQueues.TryGetValue(QueueIdentity(observed), out var planned)
                || !string.Equals(planned.BodySha256, observed.BodySha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Queue message identity or body drifted after Plan.");
            }
        }
    }

    private CleanBaselineScope Scope() => new(
        invocation.TenantId,
        invocation.SubscriptionId,
        invocation.ResourceGroup,
        invocation.SqlServer,
        invocation.SqlDatabase,
        invocation.StorageAccount,
        invocation.BlobContainer,
        invocation.MailboxIdentity,
        invocation.InboxFolderIdentity,
        invocation.NonTargetMailboxIdentity,
        invocation.OperatorUpn,
        invocation.PublicClientId);

    private static void EnsureRetainedFingerprint(
        CleanBaselineRetainedFingerprint expected,
        CleanBaselineRetainedFingerprint observed)
    {
        if (expected != observed)
        {
            throw new InvalidOperationException(
                "Case/PO, Triage, or Principal identities drifted after Plan.");
        }
    }

    private static void EnsurePollCursor(string expected, string observed)
    {
        if (!string.Equals(expected, observed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The approved Inbox poll cursor drifted after Plan.");
        }
    }

    private static void ThrowIfStopped(IReadOnlyList<CleanBaselineStopCondition> stops)
    {
        if (stops.Count > 0)
        {
            throw new InvalidOperationException(
                $"The clean-baseline run is blocked by {stops.Count} stop condition(s): " +
                string.Join(", ", stops.Select(item => item.Code).Distinct(StringComparer.Ordinal)));
        }
    }

    private static string SnapshotHash(CleanBaselineSnapshot snapshot) => Sha256(
        JsonSerializer.Serialize(new
        {
            rows = snapshot.SqlRows,
            blobs = snapshot.Blobs,
            queues = snapshot.QueueMessages,
            targetStagedReceiptIds = snapshot.TargetStagedReceiptIds,
            retained = snapshot.Retained,
            pollCursorSha256 = snapshot.PollCursorSha256,
            stops = snapshot.StopConditions
        }));

    internal static CleanBaselineStopCondition Stop(
        string code,
        string resourceType,
        string resourceIdentity,
        string detail) => new(
            code,
            resourceType,
            Sha256(resourceIdentity),
            detail);

    internal static string RowIdentity(CleanBaselineSqlRow row) =>
        $"{row.Schema}.{row.Table}:" + string.Join(
            ",",
            row.Key.Select(item => $"{item.Column}={item.Value}"));

    private static string QueueIdentity(CleanBaselineQueueItem item) =>
        $"{item.Queue}:{item.MessageId}";

    internal static string Sha256(string value) =>
        Sha256(Encoding.UTF8.GetBytes(value));

    internal static string Sha256(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string RequireExistingPath(string? value, string error)
    {
        var path = RequireOutputPath(value, error);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(error, path);
        }
        return path;
    }

    private static string RequireOutputPath(string? value, string error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(error);
        }
        var path = Path.GetFullPath(value);
        var normalized = path.Replace('\\', '/');
        if (!normalized.Contains("/artifacts/operations/intake-clean-baseline/", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path) != ".json")
        {
            throw new InvalidOperationException(
                "Maintenance manifests and receipts must be ignored JSON beneath " +
                "artifacts/operations/intake-clean-baseline/.");
        }
        return path;
    }

    internal static void ValidateInvocation(ProductionIntakeCleanBaselineInvocation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Require(value.TenantId, "858cf5b3-aa0a-47a6-9b40-4851fd0afa94", nameof(value.TenantId));
        Require(value.SubscriptionId, "e6076573-23a5-46a8-acef-7e22d264e5db", nameof(value.SubscriptionId));
        Require(value.ResourceGroup, "rg-pegasus-prod", nameof(value.ResourceGroup));
        Require(value.SqlServer, "pegasus-prod-sql-252ow37gij.database.windows.net", nameof(value.SqlServer));
        Require(value.SqlDatabase, "pegasus", nameof(value.SqlDatabase));
        Require(value.StorageAccount, "pegcustody252ow37gij", nameof(value.StorageAccount));
        Require(value.BlobContainer, "transient-intake", nameof(value.BlobContainer));
        Require(value.MailboxIdentity, "instructions@collisionengineers.co.uk", nameof(value.MailboxIdentity));
        Require(value.OperatorUpn, "digital@collisionengineers.co.uk", nameof(value.OperatorUpn));
        if (value.PublicClientId == Guid.Empty
            || string.IsNullOrWhiteSpace(value.InboxFolderIdentity)
            || string.IsNullOrWhiteSpace(value.NonTargetMailboxIdentity)
            || value.NonTargetMailboxIdentity.Equals(value.MailboxIdentity, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(value.AccessEvidencePath)
            || string.IsNullOrWhiteSpace(value.AccessEvidenceSha256)
            || value.AccessEvidenceSha256.Length != 64
            || !value.AccessEvidenceSha256.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException(
                "The public client, exact Inbox folder, non-target mailbox, and access evidence are required.");
        }
    }

    private static void Require(Guid actual, string expected, string name)
    {
        if (actual != Guid.Parse(expected))
        {
            throw new InvalidOperationException($"{name} is outside the exact production scope.");
        }
    }

    private static void Require(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{name} is outside the exact production scope.");
        }
    }
}
