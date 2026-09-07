using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Email;

namespace Pegasus.IntegrationTests;

public sealed class LocalDurableApprovedSentSourceTests
{
    [Fact]
    public async Task ImmutableCopyMoveAndDeleteProduceDeterministicNonMutatingObservations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pegasus-approved-sent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var copyPath = Path.Combine(root, "report.sent.json");
            var caseId = Guid.NewGuid();
            var copy = new
            {
                version = 1,
                mailboxId = "instructions",
                mailboxAddress = "instructions@example.test",
                sentFolderIdentity = "sent-items",
                immutableItemIdentity = "immutable-item-1",
                internetMessageIdentity = "<report-1@example.test>",
                conversationIdentity = "conversation-1",
                replyChainIdentity = "reply-chain-1",
                inReplyToIdentities = Array.Empty<string>(),
                authoritativeCaseIdentities = new[] { caseId },
                sentDateTimeUtc = new DateTimeOffset(2026, 7, 30, 8, 0, 0, TimeSpan.Zero),
                mimeSha256 = new string('A', 64)
            };
            var immutableBytes = JsonSerializer.SerializeToUtf8Bytes(copy);
            await File.WriteAllBytesAsync(copyPath, immutableBytes);

            var services = new ServiceCollection();
            services.AddLocalApprovedSent(_ => new(
                LocalApprovedSentOptions.RequiredRuntimeProfile,
                "instructions",
                "instructions@example.test",
                "sent-items",
                root));
            using var provider = services.BuildServiceProvider(validateScopes: true);
            var source = provider.GetRequiredService<IApprovedSentSource>();
            var lease = new ApprovedSentPollLease(
                "instructions",
                "instructions@example.test",
                "sent-items",
                Cursor: null,
                "lease-token");

            var discoveredPage = await source.ReadAsync(lease, 10, default);

            var discovered = Assert.Single(discoveredPage.Items);
            Assert.Equal(ApprovedSentItemObservationKind.Discovered, discovered.ObservationKind);
            Assert.Equal("report.sent.json", discovered.CurrentLocationIdentity);
            Assert.Equal("immutable-item-1", discovered.Provenance?.ImmutableItemIdentity);
            Assert.Equal("<report-1@example.test>", discovered.Provenance?.InternetMessageIdentity);
            Assert.Equal("sent-items", discovered.Provenance?.SentFolderIdentity);
            Assert.Equal(caseId, Assert.Single(discovered.Provenance!.AuthoritativeCaseIdentities));
            Assert.Equal(immutableBytes, await File.ReadAllBytesAsync(copyPath));

            var movedDirectory = Path.Combine(root, "archive");
            Directory.CreateDirectory(movedDirectory);
            var movedPath = Path.Combine(movedDirectory, Path.GetFileName(copyPath));
            File.Move(copyPath, movedPath);
            var movedPage = await source.ReadAsync(
                lease with { Cursor = discoveredPage.NextCursor },
                10,
                default);

            var moved = Assert.Single(movedPage.Items);
            Assert.Equal(ApprovedSentItemObservationKind.Moved, moved.ObservationKind);
            Assert.Equal("archive/report.sent.json", moved.CurrentLocationIdentity);
            Assert.Equal(discovered.SourceOccurrenceIdentity, moved.SourceOccurrenceIdentity);
            Assert.Equal(discovered.SourceSha256, moved.SourceSha256);
            AssertSameProvenance(discovered.Provenance, moved.Provenance);
            Assert.Equal(immutableBytes, await File.ReadAllBytesAsync(movedPath));

            File.Delete(movedPath);
            var deletedPage = await source.ReadAsync(
                lease with { Cursor = movedPage.NextCursor },
                10,
                default);

            var deleted = Assert.Single(deletedPage.Items);
            Assert.Equal(ApprovedSentItemObservationKind.Deleted, deleted.ObservationKind);
            Assert.Null(deleted.CurrentLocationIdentity);
            Assert.Equal(discovered.SourceOccurrenceIdentity, deleted.SourceOccurrenceIdentity);
            Assert.Equal(discovered.SourceSha256, deleted.SourceSha256);
            Assert.Equal(discovered.SourceSha256, deleted.OriginalSourceSha256);
            Assert.Null(deleted.ObservedSourceSha256);
            Assert.Equal("missing", deleted.EvidenceMarker);
            Assert.Equal("immutable_sent_source_missing", deleted.MalformedReasonCode);
            AssertSameProvenance(discovered.Provenance, deleted.Provenance);

            var laterPath = Path.Combine(root, "later.sent.json");
            var laterBytes = JsonSerializer.SerializeToUtf8Bytes(copy with
            {
                immutableItemIdentity = "immutable-item-later",
                internetMessageIdentity = "<report-later@example.test>"
            });
            await File.WriteAllBytesAsync(laterPath, laterBytes);
            using var restartedProvider = CreateSourceProvider(root);
            var restartedSource = restartedProvider.GetRequiredService<IApprovedSentSource>();
            var laterPage = await restartedSource.ReadAsync(
                lease with { Cursor = deletedPage.NextCursor },
                10,
                default);
            var later = Assert.Single(laterPage.Items);
            Assert.Equal("immutable-item-later", later.Provenance?.ImmutableItemIdentity);

            var finalPage = await restartedSource.ReadAsync(
                lease with { Cursor = laterPage.NextCursor },
                10,
                default);
            Assert.Empty(finalPage.Items);
            Assert.False(finalPage.HasMore);
            Assert.Equal(laterPage.NextCursor, finalPage.NextCursor);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChangedKnownCopyIsTerminalThenRestartContinuesWithLaterValidCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pegasus-approved-sent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var knownPath = Path.Combine(root, "known.sent.json");
            var originalBytes = CreateCopy("known-item", "<known@example.test>", "conversation-known");
            await File.WriteAllBytesAsync(knownPath, originalBytes);
            using var provider = CreateSourceProvider(root);
            var source = provider.GetRequiredService<IApprovedSentSource>();
            var lease = Lease();
            var initial = await source.ReadAsync(lease, 10, default);

            var changedBytes = CreateCopy("known-item", "<changed@example.test>", "conversation-changed");
            await File.WriteAllBytesAsync(knownPath, changedBytes);
            await File.WriteAllBytesAsync(
                Path.Combine(root, "later.sent.json"),
                CreateCopy("later-item", "<later@example.test>", "conversation-later"));

            var terminalPage = await source.ReadAsync(
                lease with { Cursor = initial.NextCursor },
                1,
                default);
            var terminal = Assert.Single(terminalPage.Items);
            Assert.True(terminalPage.HasMore);
            Assert.Equal(ApprovedSentItemObservationKind.Changed, terminal.ObservationKind);
            Assert.Equal("changed", terminal.EvidenceMarker);
            Assert.Equal("immutable_sent_source_changed", terminal.MalformedReasonCode);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(originalBytes)), terminal.OriginalSourceSha256);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(changedBytes)), terminal.ObservedSourceSha256);
            Assert.Equal("known-item", terminal.Provenance?.ImmutableItemIdentity);

            using var restartedProvider = CreateSourceProvider(root);
            var restartedSource = restartedProvider.GetRequiredService<IApprovedSentSource>();
            var resumed = await restartedSource.ReadAsync(
                lease with { Cursor = terminal.NextCursor },
                10,
                default);
            var later = Assert.Single(resumed.Items);
            Assert.Equal("later-item", later.Provenance?.ImmutableItemIdentity);
            Assert.False(resumed.HasMore);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReusedKnownLocationIsTerminalThenRestartDiscoversReplacementAndLaterCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pegasus-approved-sent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var knownPath = Path.Combine(root, "known.sent.json");
            var originalBytes = CreateCopy("known-item", "<known@example.test>", "conversation-known");
            await File.WriteAllBytesAsync(knownPath, originalBytes);
            using var provider = CreateSourceProvider(root);
            var source = provider.GetRequiredService<IApprovedSentSource>();
            var lease = Lease();
            var initial = await source.ReadAsync(lease, 10, default);

            var replacementBytes = CreateCopy(
                "replacement-item",
                "<replacement@example.test>",
                "conversation-replacement");
            await File.WriteAllBytesAsync(knownPath, replacementBytes);
            await File.WriteAllBytesAsync(
                Path.Combine(root, "later.sent.json"),
                CreateCopy("later-item", "<later@example.test>", "conversation-later"));

            var terminalPage = await source.ReadAsync(
                lease with { Cursor = initial.NextCursor },
                1,
                default);
            var terminal = Assert.Single(terminalPage.Items);
            Assert.True(terminalPage.HasMore);
            Assert.Equal(ApprovedSentItemObservationKind.Deleted, terminal.ObservationKind);
            Assert.Equal("reused", terminal.EvidenceMarker);
            Assert.Equal("immutable_sent_source_reused", terminal.MalformedReasonCode);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(originalBytes)), terminal.OriginalSourceSha256);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(replacementBytes)), terminal.ObservedSourceSha256);
            Assert.Null(terminal.CurrentLocationIdentity);
            Assert.Equal("known-item", terminal.Provenance?.ImmutableItemIdentity);

            using var restartedProvider = CreateSourceProvider(root);
            var restartedSource = restartedProvider.GetRequiredService<IApprovedSentSource>();
            var resumed = await restartedSource.ReadAsync(
                lease with { Cursor = terminal.NextCursor },
                10,
                default);
            Assert.Equal(2, resumed.Items.Count);
            Assert.Contains(
                resumed.Items,
                item => item.Provenance?.ImmutableItemIdentity == "replacement-item");
            Assert.Contains(
                resumed.Items,
                item => item.Provenance?.ImmutableItemIdentity == "later-item");
            Assert.False(resumed.HasMore);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MalformedCopyIsQuarantinableWithoutBlockingValidImmutableCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pegasus-approved-sent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var malformedPath = Path.Combine(root, "malformed.sent.json");
            var malformedBytes = "{ not valid json"u8.ToArray();
            await File.WriteAllBytesAsync(malformedPath, malformedBytes);
            await File.WriteAllBytesAsync(
                Path.Combine(root, "valid.sent.json"),
                CreateCopy("valid-item", "<valid@example.test>", "conversation-valid"));

            using var provider = CreateSourceProvider(root);
            var source = provider.GetRequiredService<IApprovedSentSource>();
            var page = await source.ReadAsync(Lease(), 10, default);

            Assert.Equal(2, page.Items.Count);
            var malformed = Assert.Single(page.Items, item => item.Provenance is null);
            Assert.Equal("sent_copy_malformed_json", malformed.MalformedReasonCode);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(malformedBytes)), malformed.SourceSha256);
            Assert.Equal("malformed.sent.json", malformed.CurrentLocationIdentity);
            Assert.Equal(malformedBytes, await File.ReadAllBytesAsync(malformedPath));
            Assert.Contains(page.Items, item => item.Provenance?.ImmutableItemIdentity == "valid-item");
            Assert.False(page.HasMore);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LocalSentSourceRejectsEveryNonOfflineRuntimeProfile()
    {
        Assert.Throws<InvalidOperationException>(() => new LocalApprovedSentOptions(
            "Production",
            "instructions",
            "instructions@example.test",
            "sent-items",
            Path.GetTempPath()));
    }
    private static void AssertSameProvenance(
        ApprovedSentItemProvenance? expected,
        ApprovedSentItemProvenance? actual)
    {
        var expectedValue = Assert.IsType<ApprovedSentItemProvenance>(expected);
        var actualValue = Assert.IsType<ApprovedSentItemProvenance>(actual);
        Assert.Equal(expectedValue.MailboxId, actualValue.MailboxId);
        Assert.Equal(expectedValue.MailboxAddress, actualValue.MailboxAddress);
        Assert.Equal(expectedValue.SentFolderIdentity, actualValue.SentFolderIdentity);
        Assert.Equal(expectedValue.ImmutableItemIdentity, actualValue.ImmutableItemIdentity);
        Assert.Equal(expectedValue.InternetMessageIdentity, actualValue.InternetMessageIdentity);
        Assert.Equal(expectedValue.ConversationIdentity, actualValue.ConversationIdentity);
        Assert.Equal(expectedValue.ReplyChainIdentity, actualValue.ReplyChainIdentity);
        Assert.Equal(
            expectedValue.InReplyToIdentities.ToArray(),
            actualValue.InReplyToIdentities.ToArray());
        Assert.Equal(
            expectedValue.AuthoritativeCaseIdentities.ToArray(),
            actualValue.AuthoritativeCaseIdentities.ToArray());
        Assert.Equal(expectedValue.SentAtUtc, actualValue.SentAtUtc);
        Assert.Equal(expectedValue.MimeSha256, actualValue.MimeSha256);
    }

    private static ServiceProvider CreateSourceProvider(string root)
    {
        var services = new ServiceCollection();
        services.AddLocalApprovedSent(_ => new(
            LocalApprovedSentOptions.RequiredRuntimeProfile,
            "instructions",
            "instructions@example.test",
            "sent-items",
            root));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ApprovedSentPollLease Lease() => new(
        "instructions",
        "instructions@example.test",
        "sent-items",
        Cursor: null,
        "lease-token",
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        1,
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static byte[] CreateCopy(
        string immutableItemIdentity,
        string internetMessageIdentity,
        string conversationIdentity) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            mailboxId = "instructions",
            mailboxAddress = "instructions@example.test",
            sentFolderIdentity = "sent-items",
            immutableItemIdentity,
            internetMessageIdentity,
            conversationIdentity,
            replyChainIdentity = $"reply-{immutableItemIdentity}",
            inReplyToIdentities = Array.Empty<string>(),
            authoritativeCaseIdentities = Array.Empty<Guid>(),
            sentDateTimeUtc = new DateTimeOffset(2026, 7, 30, 8, 0, 0, TimeSpan.Zero),
            mimeSha256 = new string('A', 64)
        });
}
