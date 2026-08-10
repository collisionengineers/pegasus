using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Pegasus.Infrastructure.Maintenance;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class IntakeCleanBaselineIntegrationTests
{
    private static readonly DateTimeOffset Cutoff =
        new(2031, 5, 6, 12, 0, 0, TimeSpan.Zero);
    private const string Mailbox = "instructions@collisionengineers.co.uk";

    [Fact]
    public async Task PlanExecuteVerifyDeletesOnlyExactManifestRowsAndIsIdempotent()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var target = Guid.NewGuid();
        var retained = Guid.NewGuid();
        var targetStorageKey = $"sha256/{new string('a', 64)}";
        await SeedPollStateAsync(database, "old-cursor");
        await SeedStagedReceiptAsync(database, target, Cutoff.AddMinutes(-1), targetStorageKey);
        await SeedWorkItemAsync(database, target);
        await SeedStagedReceiptAsync(
            database,
            retained,
            Cutoff.AddMinutes(1),
            $"sha256/{new string('b', 64)}");

        var sql = CleanBaselineSqlStore.ForLocalFixture(database.ConnectionString);
        var blob = new FakeBlobStore(targetStorageKey);
        var queue = new FakeQueueStore(target);
        var graph = new FakeGraphClient();
        var paths = TestPaths();
        try
        {
            var planInvocation = Invocation(
                CleanBaselineOperation.Plan,
                paths.Manifest,
                paths.Receipt,
                cutoff: Cutoff);
            var planned = await Service(planInvocation, sql, blob, queue, graph).RunAsync(default);
            using var planResult = JsonDocument.Parse(planned);
            var manifestHash = planResult.RootElement.GetProperty("manifestSha256").GetString()!;
            var manifestText = await File.ReadAllTextAsync(paths.Manifest);
            Assert.DoesNotContain("old-cursor", manifestText, StringComparison.Ordinal);
            Assert.DoesNotContain("delta-token", manifestText, StringComparison.Ordinal);
            Assert.Contains(target.ToString("D"), manifestText, StringComparison.OrdinalIgnoreCase);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(
                    Invocation(
                        CleanBaselineOperation.Execute,
                        paths.Manifest,
                        paths.Receipt,
                        new string('0', 64)),
                    sql,
                    blob,
                    queue,
                    graph).RunAsync(default));
            Assert.Single(blob.Existing);
            Assert.Single(queue.Existing);
            Assert.Equal(1, await CountAsync(database, "IntakeStagedReceipts", target));

            var executeInvocation = Invocation(
                CleanBaselineOperation.Execute,
                paths.Manifest,
                paths.Receipt,
                manifestHash);
            var changedNegativeMailbox = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(
                    executeInvocation with { NonTargetMailboxIdentity = "different@example.test" },
                    sql,
                    blob,
                    queue,
                    graph).RunAsync(default));
            Assert.Contains("scope differs", changedNegativeMailbox.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(blob.Existing);
            Assert.Single(queue.Existing);

            var executed = await Service(executeInvocation, sql, blob, queue, graph).RunAsync(default);
            Assert.DoesNotContain("delta-token", executed, StringComparison.Ordinal);
            Assert.Equal(0, await CountAsync(database, "IntakeWorkItems", target, "StagedReceiptId"));
            Assert.Equal(0, await CountAsync(database, "IntakeStagedReceipts", target));
            Assert.Equal(1, await CountAsync(database, "IntakeStagedReceipts", retained));
            Assert.Empty(blob.Existing);
            Assert.Empty(queue.Existing);
            Assert.Equal(1, graph.BaselineCalls);

            var repeated = await Service(executeInvocation, sql, blob, queue, graph).RunAsync(default);
            Assert.Equal(executed, repeated);
            Assert.Equal(1, graph.BaselineCalls);

            var verifyInvocation = Invocation(
                CleanBaselineOperation.Verify,
                paths.Manifest,
                paths.Receipt,
                manifestHash);
            queue.Existing.Add("intake-work:replacement-message");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(verifyInvocation, sql, blob, queue, graph).RunAsync(default));
            queue.Existing.Clear();
            var verified = await Service(verifyInvocation, sql, blob, queue, graph).RunAsync(default);
            using var verification = JsonDocument.Parse(verified);
            Assert.Equal("verified", verification.RootElement.GetProperty("result").GetString());
        }
        finally
        {
            if (Directory.Exists(paths.Directory))
            {
                Directory.Delete(paths.Directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task InventoryStopsForCaseAndTriageLinkedReceipts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseReceipt = Guid.NewGuid();
        var triageReceipt = Guid.NewGuid();
        await SeedReceiptAsync(database, caseReceipt, Cutoff.AddMinutes(-2));
        await SeedReceiptAsync(database, triageReceipt, Cutoff.AddMinutes(-1));
        await SeedCaseAsync(database, caseReceipt);
        await SeedTriageAsync(database, triageReceipt);

        var inventory = await CleanBaselineSqlStore.ForLocalFixture(database.ConnectionString)
            .InventoryAsync(Cutoff, Mailbox, default);

        Assert.Contains(inventory.StopConditions, item => item.Code == "case_link");
        Assert.Contains(inventory.StopConditions, item => item.Code == "triage_link");
        Assert.Contains(inventory.Rows, item => item.Table == "Cases" && item.Classification == "case_link");
        Assert.Contains(inventory.Rows, item => item.Table == "Triage" && item.Classification == "triage_link");
    }

    [Fact]
    public async Task InventoryStopsForAnUnenumeratedForeignKeyDependent()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var receipt = Guid.NewGuid();
        await SeedReceiptAsync(database, receipt, Cutoff.AddMinutes(-1));
        await database.ExecuteAsync(
            "CREATE TABLE dbo.UnexpectedReceiptDependents " +
            "(Id uniqueidentifier NOT NULL PRIMARY KEY, IntakeReceiptId uniqueidentifier NOT NULL, " +
            "CONSTRAINT FK_UnexpectedReceiptDependents_IntakeReceipts FOREIGN KEY (IntakeReceiptId) " +
            "REFERENCES dbo.IntakeReceipts(Id)); " +
            $"INSERT dbo.UnexpectedReceiptDependents (Id, IntakeReceiptId) VALUES ('{Guid.NewGuid():D}', '{receipt:D}');");

        var inventory = await CleanBaselineSqlStore.ForLocalFixture(database.ConnectionString)
            .InventoryAsync(Cutoff, Mailbox, default);

        Assert.Contains(inventory.StopConditions, item => item.Code == "unenumerated_fk_dependent");
        Assert.Contains(inventory.Rows, item => item.Table == "UnexpectedReceiptDependents");
    }

    [Fact]
    public async Task PlanStopsForSharedBlobAndUnknownQueueMessage()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var target = Guid.NewGuid();
        var retained = Guid.NewGuid();
        var sharedKey = $"sha256/{new string('c', 64)}";
        await SeedPollStateAsync(database, null);
        await SeedStagedReceiptAsync(database, target, Cutoff.AddMinutes(-1), sharedKey);
        await SeedStagedReceiptAsync(database, retained, Cutoff.AddMinutes(1), sharedKey);
        var sql = CleanBaselineSqlStore.ForLocalFixture(database.ConnectionString);
        var paths = TestPaths();
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(
                    Invocation(CleanBaselineOperation.Plan, paths.Manifest, paths.Receipt, cutoff: Cutoff),
                    sql,
                    new FakeBlobStore(sharedKey),
                    new FakeQueueStore(target, unknown: true),
                    new FakeGraphClient()).RunAsync(default));
            Assert.Contains("stop condition", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(paths.Manifest));
        }
        finally
        {
            if (Directory.Exists(paths.Directory))
            {
                Directory.Delete(paths.Directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteStopsOnSqlRowDriftBeforeAnyMutation()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var target = Guid.NewGuid();
        var key = $"sha256/{new string('d', 64)}";
        await SeedPollStateAsync(database, "old-cursor");
        await SeedStagedReceiptAsync(database, target, Cutoff.AddMinutes(-1), key);
        var sql = CleanBaselineSqlStore.ForLocalFixture(database.ConnectionString);
        var blob = new FakeBlobStore(key);
        var queue = new FakeQueueStore(target);
        var graph = new FakeGraphClient();
        var paths = TestPaths();
        try
        {
            var plan = await Service(
                Invocation(CleanBaselineOperation.Plan, paths.Manifest, paths.Receipt, cutoff: Cutoff),
                sql,
                blob,
                queue,
                graph).RunAsync(default);
            using var planResult = JsonDocument.Parse(plan);
            var hash = planResult.RootElement.GetProperty("manifestSha256").GetString()!;
            await database.ExecuteAsync(
                $"UPDATE dbo.IntakeStagedReceipts SET SourceFileName = N'drift.eml' WHERE Id = '{target:D}'");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(
                    Invocation(CleanBaselineOperation.Execute, paths.Manifest, paths.Receipt, hash),
                    sql,
                    blob,
                    queue,
                    graph).RunAsync(default));
            Assert.Contains("drift", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(blob.Existing);
            Assert.Single(queue.Existing);
            Assert.Equal(0, graph.BaselineCalls);
        }
        finally
        {
            if (Directory.Exists(paths.Directory))
            {
                Directory.Delete(paths.Directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteStopsWhenAnExactQueueTargetDisappearsAfterPlan()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var target = Guid.NewGuid();
        var key = $"sha256/{new string('e', 64)}";
        await SeedPollStateAsync(database, "old-cursor");
        await SeedStagedReceiptAsync(database, target, Cutoff.AddMinutes(-1), key);
        var sql = CleanBaselineSqlStore.ForLocalFixture(database.ConnectionString);
        var blob = new FakeBlobStore(key);
        var queue = new FakeQueueStore(target);
        var graph = new FakeGraphClient();
        var paths = TestPaths();
        try
        {
            var plan = await Service(
                Invocation(CleanBaselineOperation.Plan, paths.Manifest, paths.Receipt, cutoff: Cutoff),
                sql,
                blob,
                queue,
                graph).RunAsync(default);
            using var result = JsonDocument.Parse(plan);
            var hash = result.RootElement.GetProperty("manifestSha256").GetString()!;
            queue.Existing.Clear();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(
                    Invocation(CleanBaselineOperation.Execute, paths.Manifest, paths.Receipt, hash),
                    sql,
                    blob,
                    queue,
                    graph).RunAsync(default));
            Assert.Contains("queue target set drifted", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(blob.Existing);
            Assert.Equal(1, await CountAsync(database, "IntakeStagedReceipts", target));
            Assert.Equal(0, graph.BaselineCalls);
        }
        finally
        {
            if (Directory.Exists(paths.Directory))
            {
                Directory.Delete(paths.Directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SqlDeleteStopsWhenAManifestRowDisappearsAfterPreflight()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var target = Guid.NewGuid();
        await SeedStagedReceiptAsync(
            database,
            target,
            Cutoff.AddMinutes(-1),
            $"sha256/{new string('f', 64)}");
        var sql = CleanBaselineSqlStore.ForLocalFixture(database.ConnectionString);
        var inventory = await sql.InventoryAsync(Cutoff, Mailbox, default);
        Assert.Single(inventory.Rows);

        await using (var connection = database.CreateConnection())
        {
            await connection.OpenAsync();
            await using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE dbo.IntakeStagedReceipts WHERE Id = @id";
            delete.Parameters.AddWithValue("@id", target);
            Assert.Equal(1, await delete.ExecuteNonQueryAsync());
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sql.DeleteExactRowsAsync(inventory.Rows, default));
        Assert.Contains("identity drift", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LockedExecutionBlocksANewSharedBlobReferenceUntilTheRunEnds()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var storageKey = $"sha256/{new string('d', 64)}";
        await SeedStagedReceiptAsync(
            database,
            Guid.NewGuid(),
            Cutoff.AddMinutes(-1),
            storageKey);
        var sql = CleanBaselineSqlStore.ForLocalFixture(database.ConnectionString);

        await using (var locked = await sql.BeginLockedExecutionAsync(default))
        {
            var inventory = await locked.InventoryAsync(Cutoff, Mailbox, default);
            Assert.Equal((1, 1), inventory.BlobReferences[storageKey]);

            await using var competing = database.CreateConnection();
            await competing.OpenAsync();
            await using var insert = competing.CreateCommand();
            insert.CommandTimeout = 1;
            insert.CommandText =
                "INSERT dbo.IntakeStagedReceipts " +
                "(Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, " +
                "ExternalReceiptToken, ReceivedAtUtc, Actor, StorageKey, StagedAtUtc) VALUES " +
                "(@id, N'fixture.eml', N'message/rfc822', 7, @hash, N'mailbox', @token, @received, " +
                "N'fixture', @storage, @received)";
            var competingId = Guid.NewGuid();
            insert.Parameters.AddWithValue("@id", competingId);
            insert.Parameters.AddWithValue("@hash", competingId.ToString("N").PadRight(64, '0'));
            insert.Parameters.AddWithValue("@token", competingId.ToString("N"));
            insert.Parameters.AddWithValue("@received", Cutoff.AddMinutes(1));
            insert.Parameters.AddWithValue("@storage", storageKey);

            var exception = await Assert.ThrowsAsync<SqlException>(() => insert.ExecuteNonQueryAsync());
            Assert.Equal(-2, exception.Number);
        }
    }

    [Fact]
    public void ExactScopeValidationRejectsWrongTenantOperatorAndManagedIdentityInputs()
    {
        var baseline = Invocation(CleanBaselineOperation.ValidateAccess, null, null);
        Assert.Throws<InvalidOperationException>(() =>
            IntakeCleanBaselineService.ValidateInvocation(baseline with { TenantId = Guid.NewGuid() }));
        Assert.Throws<InvalidOperationException>(() =>
            IntakeCleanBaselineService.ValidateInvocation(baseline with { OperatorUpn = "other@example.test" }));
        Assert.Throws<InvalidOperationException>(() =>
            IntakeCleanBaselineService.ValidateInvocation(baseline with { PublicClientId = Guid.Empty }));
    }

    private static IntakeCleanBaselineService Service(
        ProductionIntakeCleanBaselineInvocation invocation,
        ICleanBaselineSqlStore sql,
        ICleanBaselineBlobStore blobs,
        ICleanBaselineQueueStore queues,
        ICleanBaselineGraphClient graph) => new(
            invocation,
            new PassingAccessValidator(invocation),
            sql,
            blobs,
            queues,
            graph,
            new FixedTimeProvider(Cutoff.AddHours(1)));

    private static ProductionIntakeCleanBaselineInvocation Invocation(
        CleanBaselineOperation operation,
        string? manifest,
        string? receipt,
        string? manifestHash = null,
        DateTimeOffset? cutoff = null) => new()
        {
            Operation = operation,
            TenantId = Guid.Parse("858cf5b3-aa0a-47a6-9b40-4851fd0afa94"),
            SubscriptionId = Guid.Parse("e6076573-23a5-46a8-acef-7e22d264e5db"),
            ResourceGroup = "rg-pegasus-prod",
            SqlServer = "pegasus-prod-sql-252ow37gij.database.windows.net",
            SqlDatabase = "pegasus",
            StorageAccount = "pegcustody252ow37gij",
            BlobContainer = "transient-intake",
            MailboxIdentity = Mailbox,
            InboxFolderIdentity = "inbox-fixture",
            NonTargetMailboxIdentity = "negative-scope@example.test",
            OperatorUpn = "digital@collisionengineers.co.uk",
            PublicClientId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            AccessEvidencePath = "fixture-access-evidence.json",
            AccessEvidenceSha256 = new string('a', 64),
            ManifestPath = manifest,
            ManifestSha256 = manifestHash,
            ExecutionReceiptPath = receipt,
            PreTestCutoffUtc = cutoff
        };

    private static (string Directory, string Manifest, string Receipt) TestPaths()
    {
        var root = RepositoryRoot();
        var directory = Path.Combine(
            root,
            "artifacts",
            "operations",
            "intake-clean-baseline",
            "test-" + Guid.NewGuid().ToString("N"));
        return (
            directory,
            Path.Combine(directory, "manifest.json"),
            Path.Combine(directory, "execution.json"));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private static async Task SeedPollStateAsync(LocalDbTestDatabase database, string? cursor)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT dbo.ApprovedInboxPollStates " +
            "(MailboxId, MailboxAddress, [Cursor], DueAtUtc) VALUES (@mailbox, @address, @cursor, @due)";
        command.Parameters.AddWithValue("@mailbox", Mailbox);
        command.Parameters.AddWithValue("@address", Mailbox);
        command.Parameters.AddWithValue("@cursor", (object?)cursor ?? DBNull.Value);
        command.Parameters.AddWithValue("@due", Cutoff);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedStagedReceiptAsync(
        LocalDbTestDatabase database,
        Guid id,
        DateTimeOffset received,
        string storageKey)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT dbo.IntakeStagedReceipts " +
            "(Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, " +
            "ExternalReceiptToken, ReceivedAtUtc, Actor, StorageKey, StagedAtUtc) VALUES " +
            "(@id, N'fixture.eml', N'message/rfc822', 7, @hash, N'mailbox', @token, @received, " +
            "N'fixture', @storage, @received)";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@hash", id.ToString("N").PadRight(64, '0'));
        command.Parameters.AddWithValue("@token", id.ToString("N"));
        command.Parameters.AddWithValue("@received", received);
        command.Parameters.AddWithValue("@storage", storageKey);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedWorkItemAsync(LocalDbTestDatabase database, Guid stagedReceiptId)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT dbo.IntakeWorkItems " +
            "(Id, StagedReceiptId, OperationKey, State, AttemptCount, DueAtUtc) VALUES " +
            "(@id, @staged, @operation, N'pending', 0, @due)";
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@staged", stagedReceiptId);
        command.Parameters.AddWithValue("@operation", stagedReceiptId.ToString("N"));
        command.Parameters.AddWithValue("@due", Cutoff);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedReceiptAsync(
        LocalDbTestDatabase database,
        Guid id,
        DateTimeOffset received)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT dbo.IntakeReceipts " +
            "(Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, " +
            "ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, " +
            "DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES " +
            "(@id, N'fixture.eml', N'message/rfc822', 7, @hash, N'mailbox', @token, @received, @received, " +
            "N'fixture', N'1', 0, N'needs_sorting', N'fixture', N'[]', N'[]', N'[]')";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@hash", id.ToString("N").PadRight(64, '0'));
        command.Parameters.AddWithValue("@token", id.ToString("N"));
        command.Parameters.AddWithValue("@received", received);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedCaseAsync(LocalDbTestDatabase database, Guid receiptId)
    {
        var organization = Guid.NewGuid();
        var lineage = Guid.NewGuid();
        var principal = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT dbo.Organizations (Id, Name, Version) VALUES (@org, N'Fixture', 0); " +
            "INSERT dbo.PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES (@lineage, @now); " +
            "INSERT dbo.Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, InspectionMode, Version) " +
            "VALUES (@principal, @org, N'FIX', @lineage, 1, N'physical_address', 0); " +
            "INSERT dbo.Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, " +
            "CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, " +
            "ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES " +
            "(@case, @principal, @lineage, 2031, 1, N'FIX31001', N'inspection', N'review', N'pending', @receipt, " +
            "0, 0, 0, 0, @now, 0, @concurrency)";
        command.Parameters.AddWithValue("@org", organization);
        command.Parameters.AddWithValue("@lineage", lineage);
        command.Parameters.AddWithValue("@principal", principal);
        command.Parameters.AddWithValue("@case", caseId);
        command.Parameters.AddWithValue("@receipt", receiptId);
        command.Parameters.AddWithValue("@now", Cutoff);
        command.Parameters.AddWithValue("@concurrency", Guid.NewGuid());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedTriageAsync(LocalDbTestDatabase database, Guid receiptId)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT dbo.Triage (Id, OriginReceiptId, SourceChannel, ExternalReceiptToken, SourceHash, " +
            "EvaluationRevisionId, NormalizedVehicleRegistration, State, CreatedAtUtc, CreationOperationKey, " +
            "Version, ConcurrencyToken) VALUES (@id, @receipt, N'mailbox', @token, @hash, @revision, N'FIXTURE', " +
            "N'open', @now, @operation, 0, @concurrency)";
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@receipt", receiptId);
        command.Parameters.AddWithValue("@token", receiptId.ToString("N"));
        command.Parameters.AddWithValue("@hash", receiptId.ToString("N").PadRight(64, '0'));
        command.Parameters.AddWithValue("@revision", Guid.NewGuid());
        command.Parameters.AddWithValue("@now", Cutoff);
        command.Parameters.AddWithValue("@operation", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("@concurrency", Guid.NewGuid());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(
        LocalDbTestDatabase database,
        string table,
        Guid id,
        string column = "Id")
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM dbo.[{table}] WHERE [{column}] = @id";
        command.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private sealed class PassingAccessValidator(ProductionIntakeCleanBaselineInvocation invocation)
        : ICleanBaselineAccessValidator
    {
        public Task<CleanBaselineAccessReport> ValidateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CleanBaselineAccessReport(
                1,
                invocation.TenantId,
                invocation.OperatorUpn,
                invocation.PublicClientId,
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                invocation.SubscriptionId.ToString("D"),
                invocation.ResourceGroup,
                invocation.SqlServer,
                invocation.SqlDatabase,
                invocation.StorageAccount,
                invocation.BlobContainer,
                invocation.MailboxIdentity,
                invocation.InboxFolderIdentity,
                invocation.NonTargetMailboxIdentity,
                invocation.AccessEvidenceSha256,
                [
                    new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "Storage Blob Data Contributor", "blob", "storage", false, "User"),
                    new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "Storage Queue Data Contributor", "queue", "storage", false, "User")
                ],
                ["db_datareader", "db_datawriter"],
                [new("fixture", true, "allowed")],
                "validated"));
    }

    private sealed class FakeBlobStore(params string[] names) : ICleanBaselineBlobStore
    {
        internal HashSet<string> Existing { get; } = names.ToHashSet(StringComparer.Ordinal);

        public Task<IReadOnlyList<CleanBaselineBlobItem>> InspectExactAsync(
            IReadOnlyDictionary<string, (int Total, int Target)> references,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CleanBaselineBlobItem>>(
            references.Where(item => Existing.Contains(item.Key))
                .Select(item => new CleanBaselineBlobItem(
                    item.Key,
                    "etag-1",
                    7,
                    item.Key.Split('/').Last(),
                    item.Value.Total,
                    item.Value.Target))
                .ToArray());

        public Task<int> DeleteExactAsync(
            IReadOnlyList<CleanBaselineBlobItem> blobs,
            CancellationToken cancellationToken)
        {
            var deleted = blobs.Count(item => Existing.Remove(item.Name));
            return Task.FromResult(deleted);
        }

        public Task<ICleanBaselinePreparedDeletion> PrepareDeleteAsync(
            IReadOnlyList<CleanBaselineBlobItem> blobs,
            CancellationToken cancellationToken) => Task.FromResult<ICleanBaselinePreparedDeletion>(
                new FakePreparedDeletion(token => DeleteExactAsync(blobs, token)));

        public Task<int> CountExistingAsync(
            IReadOnlyList<CleanBaselineBlobItem> blobs,
            CancellationToken cancellationToken) =>
            Task.FromResult(blobs.Count(item => Existing.Contains(item.Name)));
    }

    private sealed class FakeQueueStore(Guid stagedReceiptId, bool unknown = false) : ICleanBaselineQueueStore
    {
        internal HashSet<string> Existing { get; } = ["intake-work:message-1"];

        public Task<CleanBaselineQueueInventory> InspectAsync(
            IReadOnlySet<Guid> targetStagedReceiptIds,
            CancellationToken cancellationToken)
        {
            if (unknown)
            {
                return Task.FromResult(new CleanBaselineQueueInventory(
                    [],
                    [IntakeCleanBaselineService.Stop(
                        "unknown_queue_message",
                        "QueueMessage",
                        "intake-work:unknown",
                        "fixture unknown queue message")]));
            }
            var messages = Existing.Count == 0 || !targetStagedReceiptIds.Contains(stagedReceiptId)
                ? []
                : new CleanBaselineQueueItem[]
                {
                    new(
                        "intake-work",
                        "message-1",
                        IntakeCleanBaselineService.Sha256(stagedReceiptId.ToString("D")),
                        stagedReceiptId,
                        Cutoff.AddMinutes(-1),
                        Cutoff.AddDays(1))
                };
            return Task.FromResult(new CleanBaselineQueueInventory(messages, []));
        }

        public Task<int> DeleteExactAsync(
            IReadOnlyList<CleanBaselineQueueItem> messages,
            CancellationToken cancellationToken)
        {
            var deleted = messages.Count(item => Existing.Remove($"{item.Queue}:{item.MessageId}"));
            return Task.FromResult(deleted);
        }

        public Task<ICleanBaselinePreparedDeletion> PrepareDeleteAsync(
            IReadOnlyList<CleanBaselineQueueItem> messages,
            CancellationToken cancellationToken) => Task.FromResult<ICleanBaselinePreparedDeletion>(
                new FakePreparedDeletion(token => DeleteExactAsync(messages, token)));

        public Task<int> CountTargetMessagesAsync(
            IReadOnlySet<Guid> targetStagedReceiptIds,
            CancellationToken cancellationToken) => Task.FromResult(
                Existing.Count > 0 && targetStagedReceiptIds.Contains(stagedReceiptId) ? 1 : 0);
    }

    private sealed class FakePreparedDeletion(
        Func<CancellationToken, Task<int>> delete) : ICleanBaselinePreparedDeletion
    {
        public Task<int> DeleteAsync(CancellationToken cancellationToken) => delete(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeGraphClient : ICleanBaselineGraphClient
    {
        internal int BaselineCalls { get; private set; }

        public Task<CleanBaselineGraphBaseline> AcquireBaselineAsync(CancellationToken cancellationToken)
        {
            BaselineCalls++;
            var cursor =
                "{\"version\":1,\"pageUri\":\"https://graph.microsoft.com/v1.0/delta?delta-token\",\"skipCount\":0}";
            return Task.FromResult(new CleanBaselineGraphBaseline(
                cursor,
                IntakeCleanBaselineService.Sha256(cursor)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
