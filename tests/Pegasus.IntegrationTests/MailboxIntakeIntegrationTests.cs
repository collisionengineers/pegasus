using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;
using MimeKit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

public sealed class MailboxIntakeIntegrationTests
{
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 7, 8, 9, 10, 0, TimeSpan.Zero);

    [Fact]
    public async Task FullVersionedMailRouteDecisionReloadsWithoutLosingAuditEvidence()
    {
        await using var connection = new SqliteConnection(
            $"Data Source=MailboxRouteAudit-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddPegasusInfrastructure((_, options) => options.UsePegasusSqlite(connection));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await MigrateAsync(provider);

        var decision = new MailRouteEvaluationResult(
            MailRouteDisposition.Accepted,
            new("QDOS", MailRouteKind.DirectProvider, "QDOS"),
            [
                new("direct.sender-exactly-one", true, "One transport sender was proved."),
                new("forward.staff-transport", true, "The outer sender is retained staff transport."),
                new("forward.original-exactly-one", true, "One attached original sender was proved."),
                new("direct.qdos-domain", true, "The proved original uses the accepted route domain.")
            ],
            "The proved attached original selected the direct route.",
            "qdos_mail_route",
            2,
            [new("staff@collisionengineers.co.uk", "outer message")],
            [new("instructions@qdosassist.co.uk", "attached original")],
            new("instructions@qdosassist.co.uk", "attached original"));

        await using (var scope = provider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
            var stored = await store.StoreAsync(
                new(
                    SourceFileName: "route-audit.eml",
                    MediaType: "message/rfc822",
                    SourceLength: 1,
                    SourceHash: new string('A', 64),
                    SourceIdentity: new(IntakeSourceChannel.Mailbox, "route-audit-token"),
                    ReceivedAtUtc: RecordedAtUtc,
                    ProcessedAtUtc: RecordedAtUtc,
                    Actor: "system-worker:approved-inbox-poller",
                    Decision: IntakeDecision.NeedsSorting,
                    DecisionReason: "The accepted route did not contain a reviewable instruction.",
                    Evidence: [],
                    Fields: [],
                    InstructionDraft: null,
                    MissingFields: [],
                    FailureCode: null,
                    FailureReason: null,
                    SourceReaderKey: "protocol_reader",
                    SourceReaderVersion: "1",
                    ExtractionPolicyKey: "protocol_policy",
                    ExtractionPolicyVersion: 1,
                    MailRouteDecision: decision),
                CancellationToken.None);

            var reloaded = await scope.ServiceProvider
                .GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(stored.Id, CancellationToken.None);
            var audit = Assert.IsType<MailRouteEvaluationResult>(reloaded?.MailRouteDecision);
            Assert.Equal(MailRouteDisposition.Accepted, audit.Disposition);
            Assert.Equal("QDOS", audit.SelectedRoute?.RouteOwnerCode);
            Assert.Equal(MailRouteKind.DirectProvider, audit.SelectedRoute?.Kind);
            Assert.Equal("QDOS", audit.SelectedRoute?.WorkProviderCode);
            Assert.Equal("qdos_mail_route", audit.PolicyKey);
            Assert.Equal(2, audit.PolicyVersion);
            Assert.Equal(4, audit.Predicates.Count);
            Assert.Equal("staff@collisionengineers.co.uk", Assert.Single(audit.TransportIdentities).Address);
            Assert.Equal("instructions@qdosassist.co.uk", Assert.Single(audit.OriginalIdentities).Address);
            Assert.Equal("instructions@qdosassist.co.uk", audit.EffectiveSender?.Address);
            Assert.Equal("attached original", audit.EffectiveSender?.SourceLabel);
        }
    }

    [Fact]
    public async Task ImmutableLocalMailboxPollIsIdempotentAndEntersNormalDurableIntake()
    {
        var workingRoot = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.MailboxIntakeIntegrationTests",
            Guid.NewGuid().ToString("N"));
        var inboxRoot = Path.Combine(workingRoot, "approved-inbox");
        var artifactRoot = Path.Combine(workingRoot, "artifacts");
        Directory.CreateDirectory(inboxRoot);
        await File.WriteAllBytesAsync(
            Path.Combine(inboxRoot, "0001-forwarded.eml"),
            CreateForwardedProtocolMessage());

        try
        {
            await using var connection = new SqliteConnection(
                $"Data Source=MailboxPoll-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddPegasusInfrastructure(
                (_, options) => options.UsePegasusSqlite(connection),
                _ => artifactRoot);
            services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
            services.AddScoped<ReceiveIntake>();
            services.AddScoped<ProcessQueuedIntake>();
            services.AddLocalApprovedInbox(_ => new(
                LocalApprovedInboxOptions.RequiredRuntimeProfile,
                "instructions",
                "instructions@collisionengineers.co.uk",
                inboxRoot));
            await using var provider = services.BuildServiceProvider(validateScopes: true);
            await MigrateAsync(provider);

            Guid stagedReceiptId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                var actor = ActionActor.SystemWorker("approved-inbox-poller");
                Assert.Equal(1, await poll.ExecuteAsync(10, actor, CancellationToken.None));
                Assert.Equal(0, await poll.ExecuteAsync(10, actor, CancellationToken.None));

                Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM IntakeStagedReceipts"));
                Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM IntakeWorkItems"));
                Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM ApprovedInboxPollStates"));

                var workStore = scope.ServiceProvider.GetRequiredService<IIntakeWorkStore>();
                var nowUtc = DateTimeOffset.UtcNow.AddMinutes(1);
                var work = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
                    nowUtc,
                    TimeSpan.FromMinutes(1),
                    CancellationToken.None));
                stagedReceiptId = work.StagedReceiptId;
                await workStore.MarkDispatchedAsync(
                    work.Id,
                    Assert.IsType<string>(work.LeaseToken),
                    nowUtc,
                    CancellationToken.None);
                await scope.ServiceProvider
                    .GetRequiredService<ProcessQueuedIntake>()
                    .ExecuteAsync(stagedReceiptId, CancellationToken.None);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
                var summary = Assert.Single(await queries.ListAsync(null, CancellationToken.None));
                var receipt = Assert.IsType<IntakeReceipt>(
                    await queries.GetAsync(summary.Id, CancellationToken.None));
                Assert.Equal(IntakeSourceChannel.Mailbox, receipt.SourceIdentity.Channel);
                Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
                var route = Assert.IsType<MailRouteEvaluationResult>(receipt.MailRouteDecision);
                Assert.Equal(MailRouteDisposition.NoMatch, route.Disposition);
                Assert.Equal(
                    "technical-forwarder@collisionengineers.co.uk",
                    Assert.Single(route.TransportIdentities).Address);
                Assert.Equal("original@example.invalid", Assert.Single(route.OriginalIdentities).Address);
                Assert.Equal("original@example.invalid", route.EffectiveSender?.Address);
            }

            Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM IntakeReceipts"));
            Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM IntakeEvaluations"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(workingRoot))
            {
                Directory.Delete(workingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalApprovedInboxRejectsMutationOfAnObservedImmutableItem()
    {
        var workingRoot = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.MailboxMutationIntegrationTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingRoot);
        var itemPath = Path.Combine(workingRoot, "0001.eml");
        await File.WriteAllBytesAsync(itemPath, CreateForwardedProtocolMessage());

        try
        {
            var services = new ServiceCollection();
            services.AddLocalApprovedInbox(_ => new(
                LocalApprovedInboxOptions.RequiredRuntimeProfile,
                "instructions",
                "instructions@collisionengineers.co.uk",
                workingRoot));
            await using var provider = services.BuildServiceProvider(validateScopes: true);
            var source = provider.GetRequiredService<IApprovedInboxSource>();
            var lease = new ApprovedInboxPollLease(
                "instructions",
                "instructions@collisionengineers.co.uk",
                null,
                "first-lease");
            var first = await source.ReadAsync(lease, 10, CancellationToken.None);
            Assert.Single(first.Messages);

            await File.WriteAllTextAsync(itemPath, "changed immutable source");
            var replayLease = lease with
            {
                Cursor = first.NextCursor,
                LeaseToken = "replay-lease"
            };

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                source.ReadAsync(replayLease, 10, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(workingRoot))
            {
                Directory.Delete(workingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LocalApprovedInboxActivationFailsClosedOutsideOfflineProfile()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new LocalApprovedInboxOptions(
            "Production",
            "instructions",
            "instructions@collisionengineers.co.uk",
            Path.GetTempPath()));

        Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task MigrateAsync(IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] CreateForwardedProtocolMessage()
    {
        var original = new MimeMessage
        {
            Subject = "Attached protocol message",
            Body = new TextPart("plain") { Text = "Protocol-only attached content." }
        };
        original.From.Add(new MailboxAddress("Original", "original@example.invalid"));
        original.To.Add(new MailboxAddress("Inbox", "inbox@example.invalid"));

        var attached = new MessagePart
        {
            Message = original,
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
            {
                FileName = "original.eml"
            }
        };
        var outer = new MimeMessage
        {
            Subject = "Forwarded protocol container",
            Body = new Multipart("mixed")
            {
                new TextPart("plain") { Text = "Protocol-only outer content." },
                attached
            }
        };
        outer.From.Add(new MailboxAddress(
            "Technical Forwarder",
            "technical-forwarder@collisionengineers.co.uk"));
        outer.To.Add(new MailboxAddress("Approved Inbox", "instructions@collisionengineers.co.uk"));

        using var stream = new MemoryStream();
        outer.WriteTo(stream);
        return stream.ToArray();
    }
}
