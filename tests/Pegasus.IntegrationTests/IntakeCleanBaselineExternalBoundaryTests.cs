using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Queues;
using Pegasus.Infrastructure.Maintenance;

namespace Pegasus.IntegrationTests;

public sealed class IntakeCleanBaselineExternalBoundaryTests
{
    [Fact]
    public async Task GraphBaselineUsesGetOnlyAndRequiresTheNegativeMailboxDenial()
    {
        var invocation = Invocation();
        var handler = new GraphFakeHandler(invocation, denyNonTarget: true);
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.example.test/v1.0/")
        };
        var graph = new CleanBaselineGraphClient(http, session: null, invocation);

        var capabilities = await graph.ValidateScopeAsync(default);
        var baseline = await graph.AcquireBaselineAsync(default);

        Assert.Contains(capabilities, item =>
            item.Capability == "graph_non_target_mailbox_read" && item.ResultCode == "denied");
        Assert.Equal(IntakeCleanBaselineService.Sha256(baseline.Cursor), baseline.CursorSha256);
        Assert.Contains("delta-token", baseline.Cursor, StringComparison.Ordinal);
        Assert.All(handler.Methods, method => Assert.Equal(HttpMethod.Get, method));
    }

    [Fact]
    public async Task GraphValidationStopsWhenTheNonTargetMailboxIsReadable()
    {
        var invocation = Invocation();
        using var http = new HttpClient(new GraphFakeHandler(invocation, denyNonTarget: false))
        {
            BaseAddress = new Uri("https://graph.example.test/v1.0/")
        };
        var graph = new CleanBaselineGraphClient(http, session: null, invocation);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.ValidateScopeAsync(default));

        Assert.Contains("non-target", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GraphValidationDoesNotTreatANonexistentMailboxAsScopeDenial()
    {
        var invocation = Invocation();
        using var http = new HttpClient(new GraphFakeHandler(
            invocation,
            denyNonTarget: true,
            nonTargetExists: false))
        {
            BaseAddress = new Uri("https://graph.example.test/v1.0/")
        };
        var graph = new CleanBaselineGraphClient(http, session: null, invocation);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.ValidateScopeAsync(default));

        Assert.Contains("not-found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OperatorTokenValidationRejectsWrongTenantWrongOperatorAndApplicationIdentity()
    {
        var invocation = Invocation();
        var session = new NamedOperatorTokenSession(invocation);
        var validId = Token(invocation, [new("amr", "mfa")]);
        var valid = Token(invocation, [new("scp", "Mail.Read.Shared")]);
        var accepted = session.ValidateGraphToken(valid, validId, requireMfa: true);
        Assert.Equal(invocation.OperatorUpn, accepted.Upn);

        Assert.Throws<UnauthorizedAccessException>(() => session.ValidateGraphToken(
            Token(invocation with { TenantId = Guid.NewGuid() }, [new("scp", "Mail.Read.Shared")]),
            validId,
            requireMfa: true));
        Assert.Throws<UnauthorizedAccessException>(() => session.ValidateGraphToken(
            Token(invocation with { OperatorUpn = "other@example.test" }, [new("scp", "Mail.Read.Shared")]),
            validId,
            requireMfa: true));
        Assert.Throws<UnauthorizedAccessException>(() => session.ValidateGraphToken(
            Token(invocation, [new("roles", "Mail.Read")]),
            validId,
            requireMfa: true));
        Assert.Throws<UnauthorizedAccessException>(() => session.ValidateGraphToken(
            Token(invocation, [
                new("scp", "Mail.Read.Shared"),
                new("wids", "62e90394-69f5-4237-9190-012177145e10")
            ]),
            validId,
            requireMfa: true));
        Assert.Throws<UnauthorizedAccessException>(() => session.ValidateGraphToken(
            valid,
            Token(invocation, []),
            requireMfa: true));
    }

    [Fact]
    public void RoleEvidenceRejectsMissingBroadCredentialedStaleAndDriftedCensus()
    {
        var invocation = Invocation();
        var now = new DateTimeOffset(2031, 1, 2, 12, 0, 0, TimeSpan.Zero);
        var evidence = Evidence(invocation, now);
        CleanBaselineAccessEvidenceValidator.Validate(invocation, evidence, now);

        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with { StorageAccount = "wrong-account" },
                now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with { CapturedAtUtc = now.AddHours(-5) },
                now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with
                {
                    PublicClient = evidence.PublicClient with { PasswordCredentialCount = 1 }
                },
                now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with { Mailbox = evidence.Mailbox with { CanDeleteItems = true } },
                now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with
                {
                    Mailbox = evidence.Mailbox with { NonTargetMailboxIdentity = "wrong@example.test" }
                },
                now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with { SqlRoles = ["public", "db_datareader"] },
                now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with { SqlRoles = ["public", "db_datareader", "db_datawriter", "db_owner"] },
                now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with
                {
                    RoleAssignments = [.. evidence.RoleAssignments, evidence.RoleAssignments[0]]
                },
                now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with
                {
                    RoleAssignments = evidence.RoleAssignments
                        .Select((item, index) => index == 0 ? item with { Inherited = true } : item)
                        .ToArray()
                },
                now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            CleanBaselineAccessEvidenceValidator.Validate(
                invocation,
                evidence with
                {
                    RoleDefinitions = evidence.RoleDefinitions
                        .Select((item, index) => index == 0
                            ? item with { Actions = ["Microsoft.Storage/storageAccounts/listKeys/action"] }
                            : item)
                        .ToArray()
                },
                now));
    }

    [Fact]
    public void RoleEvidenceIsHashBoundAndMustUseTheIgnoredOperationDirectory()
    {
        var invocation = Invocation();
        var now = DateTimeOffset.UtcNow;
        var evidence = Evidence(invocation, now);
        var directory = Path.Combine(
            RepositoryRoot(),
            "artifacts",
            "operations",
            "intake-clean-baseline",
            "test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "role-evidence.json");
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                evidence,
                CleanBaselineJsonContext.Default.CleanBaselineAccessEvidence);
            File.WriteAllBytes(path, bytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var bound = invocation with { AccessEvidencePath = path, AccessEvidenceSha256 = hash };

            var loaded = CleanBaselineAccessEvidenceValidator.Load(bound, new FixedTimeProvider(now));
            Assert.Equal(evidence.OperatorObjectId, loaded.OperatorObjectId);
            Assert.Throws<InvalidDataException>(() =>
                CleanBaselineAccessEvidenceValidator.Load(
                    bound with { AccessEvidenceSha256 = new string('0', 64) },
                    new FixedTimeProvider(now)));
            var outsideRepository = Path.Combine(
                Path.GetTempPath(),
                "artifacts",
                "operations",
                "intake-clean-baseline",
                "role-evidence.json");
            Assert.Throws<InvalidOperationException>(() =>
                CleanBaselineAccessEvidenceValidator.Load(
                    bound with { AccessEvidencePath = outsideRepository },
                    new FixedTimeProvider(now)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AzuriteStoresEnforceExactBlobEtagAndQueueMessageIdentity()
    {
        await using var azurite = await AzuriteFixture.TryStartAsync();
        if (azurite is null)
        {
            throw new InvalidOperationException(
                "Azurite 3.36.0 is required for the clean-baseline storage verification lane.");
        }
        var container = new BlobContainerClient(azurite.ConnectionString, "transient-intake");
        await container.CreateAsync();
        var queues = CleanBaselineQueueStore.QueueNames.ToDictionary(
            name => name,
            name => new QueueClient(azurite.ConnectionString, name),
            StringComparer.Ordinal);
        foreach (var queue in queues.Values)
        {
            await queue.CreateAsync();
        }
        await CleanBaselineStorageCapabilityProbe.ValidateAsync(container, queues, default);

        var target = Guid.NewGuid();
        var blobName = $"sha256/{new string('e', 64)}";
        await container.GetBlobClient(blobName).UploadAsync(
            BinaryData.FromString("fixture"),
            overwrite: false);
        await queues["intake-work"].SendMessageAsync(target.ToString("D"));

        var blobStore = CleanBaselineBlobStore.ForLocalFixture(
            azurite.ConnectionString,
            "transient-intake");
        var queueStore = CleanBaselineQueueStore.ForLocalFixture(azurite.ConnectionString);
        var blobPlan = await blobStore.InspectExactAsync(
            new Dictionary<string, (int Total, int Target)> { [blobName] = (1, 1) },
            default);
        var queuePlan = await queueStore.InspectAsync(new HashSet<Guid> { target }, default);
        Assert.Empty(queuePlan.StopConditions);
        Assert.Single(queuePlan.Messages);

        Assert.Equal(1, await queueStore.DeleteExactAsync(queuePlan.Messages, default));
        await using (var preparedBlob = await blobStore.PrepareDeleteAsync(blobPlan, default))
        {
            var leasedProperties = (await container.GetBlobClient(blobName).GetPropertiesAsync()).Value;
            Assert.Equal(LeaseDurationType.Fixed, leasedProperties.LeaseDuration);
            Assert.Equal(1, await preparedBlob.DeleteAsync(default));
        }
        Assert.Equal(0, await queueStore.CountTargetMessagesAsync(new HashSet<Guid> { target }, default));
        Assert.Equal(0, await blobStore.CountExistingAsync(blobPlan, default));
        var remaining = await queues["intake-work"].PeekMessagesAsync(32);
        Assert.Empty(remaining.Value);
        await queues["intake-work"].SendMessageAsync(target.ToString("D"));
        Assert.Equal(
            1,
            await queueStore.CountTargetMessagesAsync(new HashSet<Guid> { target }, default));
    }

    [Fact]
    public async Task AzuriteQueueDeleteValidatesTheWholeCensusBeforeRemovingAnExactTarget()
    {
        await using var azurite = await AzuriteFixture.TryStartAsync();
        if (azurite is null)
        {
            throw new InvalidOperationException(
                "Azurite 3.36.0 is required for the clean-baseline storage verification lane.");
        }

        var target = Guid.NewGuid();
        var queue = new QueueClient(azurite.ConnectionString, "intake-work");
        var poison = new QueueClient(azurite.ConnectionString, "intake-work-poison");
        await queue.CreateAsync();
        await poison.CreateAsync();
        await queue.SendMessageAsync(target.ToString("D"));
        var store = CleanBaselineQueueStore.ForLocalFixture(azurite.ConnectionString);
        var plan = await store.InspectAsync(new HashSet<Guid> { target }, default);
        Assert.Single(plan.Messages);

        await queue.SendMessageAsync(target.ToString("D"));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.DeleteExactAsync(plan.Messages, default));

        Assert.Contains("queue count differs", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, (await queue.PeekMessagesAsync(32)).Value.Length);
    }

    [Fact]
    public async Task AzuriteStoresStopForStaleEtagAndUnknownQueueBody()
    {
        await using var azurite = await AzuriteFixture.TryStartAsync();
        if (azurite is null)
        {
            throw new InvalidOperationException(
                "Azurite 3.36.0 is required for the clean-baseline storage verification lane.");
        }
        var container = new BlobContainerClient(azurite.ConnectionString, "transient-intake");
        await container.CreateAsync();
        var blobName = $"sha256/{new string('f', 64)}";
        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(BinaryData.FromString("first"));
        var blobStore = CleanBaselineBlobStore.ForLocalFixture(
            azurite.ConnectionString,
            "transient-intake");
        var plan = await blobStore.InspectExactAsync(
            new Dictionary<string, (int Total, int Target)> { [blobName] = (1, 1) },
            default);
        await blob.UploadAsync(BinaryData.FromString("changed"), overwrite: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => blobStore.DeleteExactAsync(plan, default));

        var missingPlan = await blobStore.InspectExactAsync(
            new Dictionary<string, (int Total, int Target)> { [blobName] = (1, 1) },
            default);
        await blob.DeleteIfExistsAsync();
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(
            () => blobStore.DeleteExactAsync(missingPlan, default));
        Assert.Contains("lease state drifted", missing.Message, StringComparison.Ordinal);

        var firstName = $"sha256/{new string('1', 64)}";
        var secondName = $"sha256/{new string('2', 64)}";
        var firstBlob = container.GetBlobClient(firstName);
        var secondBlob = container.GetBlobClient(secondName);
        await firstBlob.UploadAsync(BinaryData.FromString("first"));
        await secondBlob.UploadAsync(BinaryData.FromString("second"));
        var twoBlobPlan = await blobStore.InspectExactAsync(
            new Dictionary<string, (int Total, int Target)>
            {
                [firstName] = (1, 1),
                [secondName] = (1, 1)
            },
            default);
        var competingLease = secondBlob.GetBlobLeaseClient(Guid.NewGuid().ToString("D"));
        await competingLease.AcquireAsync(BlobLeaseClient.InfiniteLeaseDuration);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => blobStore.DeleteExactAsync(twoBlobPlan, default));
            Assert.True((await firstBlob.ExistsAsync()).Value);
            Assert.Equal(
                LeaseState.Available,
                (await firstBlob.GetPropertiesAsync()).Value.LeaseState);
        }
        finally
        {
            await competingLease.ReleaseAsync();
        }

        var queue = new QueueClient(azurite.ConnectionString, "intake-work");
        await queue.CreateAsync();
        var poison = new QueueClient(azurite.ConnectionString, "intake-work-poison");
        await poison.CreateAsync();
        await queue.SendMessageAsync("not-a-guid");
        await queue.SendMessageAsync(Guid.NewGuid().ToString("D"));
        var inventory = await CleanBaselineQueueStore.ForLocalFixture(azurite.ConnectionString)
            .InspectAsync(new HashSet<Guid>(), default);
        Assert.Contains(inventory.StopConditions, item => item.Code == "unknown_queue_message");
        Assert.Contains(inventory.StopConditions, item => item.Code == "non_target_queue_message");
    }

    [Fact]
    public async Task BlobLeaseCleanupBoundsEveryAttemptAndContinuesAfterCancellationAndFault()
    {
        var attempts = new List<int>();
        var failures = await CleanBaselineBlobStore.ReleaseEveryLeaseAsync(
            new Func<CancellationToken, Task>[]
            {
                async cancellationToken =>
                {
                    attempts.Add(1);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                },
                _ =>
                {
                    attempts.Add(2);
                    throw new InvalidOperationException("fixture release failure");
                },
                cancellationToken =>
                {
                    attempts.Add(3);
                    Assert.False(cancellationToken.IsCancellationRequested);
                    return Task.CompletedTask;
                }
            },
            TimeSpan.FromMilliseconds(50));

        Assert.Equal([1, 2, 3], attempts);
        Assert.Collection(
            failures,
            failure => Assert.IsAssignableFrom<OperationCanceledException>(failure),
            failure => Assert.IsType<InvalidOperationException>(failure));
    }

    private static ProductionIntakeCleanBaselineInvocation Invocation() => new()
    {
        Operation = CleanBaselineOperation.ValidateAccess,
        TenantId = Guid.Parse("858cf5b3-aa0a-47a6-9b40-4851fd0afa94"),
        SubscriptionId = Guid.Parse("e6076573-23a5-46a8-acef-7e22d264e5db"),
        ResourceGroup = "rg-pegasus-prod",
        SqlServer = "pegasus-prod-sql-252ow37gij.database.windows.net",
        SqlDatabase = "pegasus",
        StorageAccount = "pegcustody252ow37gij",
        BlobContainer = "transient-intake",
        MailboxIdentity = "instructions@collisionengineers.co.uk",
        InboxFolderIdentity = "inbox-fixture",
        NonTargetMailboxIdentity = "negative-scope@example.test",
        OperatorUpn = "digital@collisionengineers.co.uk",
        PublicClientId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        AccessEvidencePath = "fixture.json",
        AccessEvidenceSha256 = new string('a', 64)
    };

    private static CleanBaselineAccessEvidence Evidence(
        ProductionIntakeCleanBaselineInvocation invocation,
        DateTimeOffset capturedAtUtc)
    {
        var operatorId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var scope = $"/subscriptions/{invocation.SubscriptionId:D}/resourceGroups/{invocation.ResourceGroup}" +
            $"/providers/Microsoft.Storage/storageAccounts/{invocation.StorageAccount}";
        return new(
            1,
            invocation.TenantId,
            invocation.SubscriptionId,
            operatorId,
            invocation.OperatorUpn,
            invocation.PublicClientId,
            invocation.ResourceGroup,
            invocation.SqlServer,
            invocation.SqlDatabase,
            invocation.StorageAccount,
            capturedAtUtc,
            new(
                true,
                0,
                0,
                [
                    new(Guid.Parse("00000003-0000-0000-c000-000000000000"), "Mail.Read.Shared"),
                    new(Guid.Parse("022907d3-0f1b-48f7-badc-1ba6abab6d66"), "user_impersonation"),
                    new(Guid.Parse("e406a681-f3d4-42a8-90b6-c2b029497af1"), "user_impersonation")
                ]),
            new(
                invocation.MailboxIdentity,
                invocation.InboxFolderIdentity,
                invocation.NonTargetMailboxIdentity,
                "Reviewer",
                false,
                false,
                false),
            [],
            ["public", "db_datareader", "db_datawriter"],
            [
                new(
                    operatorId,
                    "Storage Blob Data Contributor",
                    $"/subscriptions/{invocation.SubscriptionId:D}/providers/Microsoft.Authorization/roleDefinitions/{CleanBaselineAccessEvidenceValidator.BlobContributor:D}",
                    scope,
                    false,
                    "User"),
                new(
                    operatorId,
                    "Storage Queue Data Contributor",
                    $"/subscriptions/{invocation.SubscriptionId:D}/providers/Microsoft.Authorization/roleDefinitions/{CleanBaselineAccessEvidenceValidator.QueueContributor:D}",
                    scope,
                    false,
                    "User")
            ],
            [
                new(CleanBaselineAccessEvidenceValidator.BlobContributor, "Storage Blob Data Contributor", [], ["Microsoft.Storage/storageAccounts/blobServices/containers/blobs/*"]),
                new(CleanBaselineAccessEvidenceValidator.QueueContributor, "Storage Queue Data Contributor", [], ["Microsoft.Storage/storageAccounts/queueServices/queues/messages/*"])
            ]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
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

    private static string Token(
        ProductionIntakeCleanBaselineInvocation invocation,
        IReadOnlyList<Claim> additionalClaims)
    {
        var claims = new List<Claim>
        {
            new("tid", invocation.TenantId.ToString("D")),
            new("oid", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            new("azp", invocation.PublicClientId.ToString("D")),
            new("preferred_username", invocation.OperatorUpn)
        };
        claims.AddRange(additionalClaims);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: $"https://login.microsoftonline.com/{invocation.TenantId:D}/v2.0",
            audience: "fixture",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10)));
    }

    private sealed class GraphFakeHandler(
        ProductionIntakeCleanBaselineInvocation invocation,
        bool denyNonTarget,
        bool nonTargetExists = true) : HttpMessageHandler
    {
        internal List<HttpMethod> Methods { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains(invocation.NonTargetMailboxIdentity, StringComparison.OrdinalIgnoreCase)
                || path.Contains(
                    Uri.EscapeDataString(invocation.NonTargetMailboxIdentity),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(
                    denyNonTarget
                        ? nonTargetExists ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound
                        : HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
                });
            }
            var delta =
                $"https://graph.example.test/v1.0/users/{Uri.EscapeDataString(invocation.MailboxIdentity)}" +
                $"/mailFolders/{Uri.EscapeDataString(invocation.InboxFolderIdentity)}/messages/delta?$deltatoken=delta-token";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"{{\"value\":[],\"@odata.deltaLink\":\"{delta}\"}}",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }

    private sealed class AzuriteFixture : IAsyncDisposable
    {
        private readonly Process process;
        private readonly string location;

        private AzuriteFixture(Process process, string location, string connectionString)
        {
            this.process = process;
            this.location = location;
            ConnectionString = connectionString;
        }

        internal string ConnectionString { get; }

        internal static async Task<AzuriteFixture?> TryStartAsync()
        {
            var root = RepositoryRoot();
            var script = Path.Combine(root, "node_modules", "azurite", "dist", "src", "azurite.js");
            if (!File.Exists(script))
            {
                return null;
            }
            var location = Path.Combine(Path.GetTempPath(), "pegasus-azurite-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(location);
            var start = new ProcessStartInfo("node")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add("--silent");
            start.ArgumentList.Add("--skipApiVersionCheck");
            start.ArgumentList.Add("--location");
            start.ArgumentList.Add(location);
            var process = Process.Start(start)
                ?? throw new InvalidOperationException("Azurite did not start.");
            try
            {
                await WaitForPortAsync(10_000, process);
                await WaitForPortAsync(10_001, process);
            }
            catch
            {
                process.Kill(entireProcessTree: true);
                process.Dispose();
                Directory.Delete(location, recursive: true);
                throw;
            }
            return new(process, location, "UseDevelopmentStorage=true");
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            process.Dispose();
            if (Directory.Exists(location))
            {
                Directory.Delete(location, recursive: true);
            }
        }

        private static async Task WaitForPortAsync(int port, Process process)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        "Azurite exited during startup: " + await process.StandardError.ReadToEndAsync());
                }
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", port);
                    return;
                }
                catch (SocketException)
                {
                    await Task.Delay(50);
                }
            }
            throw new TimeoutException("Azurite did not bind its disposable loopback port.");
        }

    }
}
