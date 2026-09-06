using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EfUnidentifiedStore against the real migration: the history-truncation,
/// replay-fingerprint, and destination-validation fixes from the INTK-007
/// review, which had no persistence-level coverage.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class UnidentifiedPersistenceTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2031, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RegisteringAnOverlongSafeDetailTruncatesTheHistoryReasonInsteadOfFailing()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var register = scope.ServiceProvider.GetRequiredService<IRegisterUnidentified>();
        var store = scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>();

        // Exceeds UnidentifiedValidation.MaximumReasonLength (500) but stays
        // within MaximumDetailLength (1000), so registration itself is valid
        // and only the history row's narrower column is at risk.
        var overlongDetail = new string('a', 900);
        var origin = UnidentifiedOrigin.Receipt(Guid.NewGuid());

        var result = await register.ExecuteAsync(
            new(
                origin,
                UnidentifiedReasonCode.NoUsableIdentification,
                overlongDetail,
                ActionActor.SystemWorker("test-worker"),
                $"unidentified-test:{Guid.NewGuid():N}",
                CreatedAtUtc));

        Assert.Equal(overlongDetail, result.Item.SafeDetail);
        var history = await store.HistoryAsync(result.Item.Id);
        var entry = Assert.Single(history);
        Assert.Equal(500, entry.Reason.Length);
        Assert.Equal(overlongDetail[..500], entry.Reason);
    }

    [Fact]
    public async Task ResolvingWithAReusedKeyButADifferentTargetConflictsInsteadOfReplaying()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var register = scope.ServiceProvider.GetRequiredService<IRegisterUnidentified>();
        var resolveStore = scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>();
        var origin = UnidentifiedOrigin.Receipt(Guid.NewGuid());
        var registered = await register.ExecuteAsync(
            new(
                origin,
                UnidentifiedReasonCode.NoUsableIdentification,
                "test detail",
                ActionActor.SystemWorker("test-worker"),
                $"unidentified-test:{Guid.NewGuid():N}",
                CreatedAtUtc));
        var operationKey = $"unidentified-resolve-test:{Guid.NewGuid():N}";
        var actor = ActionActor.Automation("test-worker");

        // Resolve once as an ExternalReference.
        await resolveStore.ResolveAsync(
            new(
                registered.Item.Id,
                registered.Item.Version,
                actor,
                operationKey,
                "resolved",
                UnidentifiedResolutionTargetKind.ExternalReference,
                "target-1",
                null,
                CreatedAtUtc));

        // Reusing the same operation key with a different TargetKind must
        // conflict, not silently replay the first result.
        await Assert.ThrowsAsync<UnidentifiedOperationConflictException>(() =>
            resolveStore.ResolveAsync(
                new(
                    registered.Item.Id,
                    registered.Item.Version,
                    actor,
                    operationKey,
                    "resolved",
                    UnidentifiedResolutionTargetKind.Triage,
                    "target-1",
                    null,
                    CreatedAtUtc)));
    }

    [Fact]
    public async Task ResolvingToANonexistentCaseIsRejectedBeforeChangingState()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var register = scope.ServiceProvider.GetRequiredService<IRegisterUnidentified>();
        var resolve = scope.ServiceProvider.GetRequiredService<IResolveUnidentified>();
        var store = scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>();
        var origin = UnidentifiedOrigin.Receipt(Guid.NewGuid());
        var registered = await register.ExecuteAsync(
            new(
                origin,
                UnidentifiedReasonCode.NoUsableIdentification,
                "test detail",
                ActionActor.SystemWorker("test-worker"),
                $"unidentified-test:{Guid.NewGuid():N}",
                CreatedAtUtc));

        await Assert.ThrowsAsync<UnidentifiedResolutionTargetNotFoundException>(() =>
            resolve.ExecuteAsync(
                new(
                    registered.Item.Id,
                    registered.Item.Version,
                    ActionActor.Automation("test-worker"),
                    $"unidentified-resolve-test:{Guid.NewGuid():N}",
                    "resolved",
                    UnidentifiedResolutionTargetKind.InstructionCase,
                    Guid.NewGuid().ToString("N"),
                    null,
                    CreatedAtUtc)));

        var reloaded = await store.GetAsync(registered.Item.Id);
        Assert.Equal(UnidentifiedState.Open, reloaded!.State);
    }

    /// <summary>
    /// INTK-009's Unidentified tab filters: media kind is derived from the
    /// origin receipt's channel and content type, not a stored field, so this
    /// exercises the join and the classification together.
    /// </summary>
    [Fact]
    public async Task ListQueueClassifiesEachRowByItsReceiptsChannelAndContentType()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var receiptStore = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        var register = scope.ServiceProvider.GetRequiredService<IRegisterUnidentified>();
        var store = scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>();

        var emailReceiptId = await StoreReceiptAsync(
            receiptStore,
            IntakeSourceChannel.Mailbox,
            "message/rfc822",
            "unread-message.eml",
            subject: "Vehicle damage claim",
            senderAddress: "claimant@example.test");
        var imageReceiptId = await StoreReceiptAsync(
            receiptStore,
            IntakeSourceChannel.ManualUpload,
            "image/jpeg",
            "damage-photo.jpg");
        var documentReceiptId = await StoreReceiptAsync(
            receiptStore,
            IntakeSourceChannel.ManualUpload,
            "application/pdf",
            "instruction-letter.pdf");

        var emailItem = await RegisterAsync(register, emailReceiptId);
        var imageItem = await RegisterAsync(register, imageReceiptId);
        var documentItem = await RegisterAsync(register, documentReceiptId);

        var all = await store.ListQueueAsync(null);
        Assert.Contains(all, row => row.Id == emailItem.Id);
        Assert.Contains(all, row => row.Id == imageItem.Id);
        Assert.Contains(all, row => row.Id == documentItem.Id);

        var emailRow = Assert.Single(await store.ListQueueAsync(UnidentifiedMediaKind.Email), row => row.Id == emailItem.Id);
        Assert.Equal(UnidentifiedMediaKind.Email, emailRow.MediaKind);
        Assert.Equal("Vehicle damage claim", emailRow.EmailSubject);
        Assert.Equal("claimant@example.test", emailRow.EmailSender);
        Assert.Null(emailRow.FileName);

        var imageRows = await store.ListQueueAsync(UnidentifiedMediaKind.Image);
        Assert.Contains(imageRows, row => row.Id == imageItem.Id);
        Assert.DoesNotContain(imageRows, row => row.Id == emailItem.Id || row.Id == documentItem.Id);
        var imageRow = Assert.Single(imageRows, row => row.Id == imageItem.Id);
        Assert.Equal("damage-photo.jpg", imageRow.FileName);

        var documentRows = await store.ListQueueAsync(null);
        var documentRow = Assert.Single(documentRows, row => row.Id == documentItem.Id);
        Assert.Equal(UnidentifiedMediaKind.Document, documentRow.MediaKind);
        Assert.Equal("instruction-letter.pdf", documentRow.FileName);
    }

    private static async Task<UnidentifiedItem> RegisterAsync(IRegisterUnidentified register, Guid receiptId)
    {
        var result = await register.ExecuteAsync(
            new(
                UnidentifiedOrigin.Receipt(receiptId),
                UnidentifiedReasonCode.NoUsableIdentification,
                "test detail",
                ActionActor.SystemWorker("test-worker"),
                $"unidentified-test:{Guid.NewGuid():N}",
                CreatedAtUtc));
        return result.Item;
    }

    private static async Task<Guid> StoreReceiptAsync(
        IIntakeReceiptStore receiptStore,
        IntakeSourceChannel channel,
        string mediaType,
        string sourceFileName,
        string? subject = null,
        string? senderAddress = null)
    {
        IReadOnlyList<IntakeEvidence> evidence = subject is null
            ? []
            : [new IntakeEvidence(
                IntakeEvidenceSource.Subject,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.Information,
                "subject",
                subject)];
        var mailRouteDecision = senderAddress is null
            ? null
            : new MailRouteEvaluationResult(
                MailRouteDisposition.NeedsSorting,
                null,
                [],
                "test route evaluation",
                "test-policy",
                1,
                [],
                [],
                new MailRouteIdentity(senderAddress, "transport"));

        var draft = new IntakeReceiptDraft(
            sourceFileName,
            mediaType,
            1024,
            Guid.NewGuid().ToString("N"),
            new IntakeSourceIdentity(channel, Guid.NewGuid().ToString("N")),
            CreatedAtUtc,
            CreatedAtUtc,
            "test-actor",
            IntakeDecision.NeedsSorting,
            "test decision reason",
            evidence,
            [],
            null,
            [],
            null,
            null,
            "test-reader",
            "1",
            null,
            null,
            MailRouteDecision: mailRouteDecision);

        var receipt = await receiptStore.StoreAsync(draft, CancellationToken.None);
        return receipt.Id;
    }

    /// <summary>
    /// The reopen and recheck members carry interface defaults so that
    /// in-memory doubles with no recheck queue stay honest (empty page, no
    /// watermark). That default is a trap for the one implementation that MUST
    /// override it: a production store silently inheriting it would report no
    /// stale resolutions for ever and the correction loop would never run.
    /// </summary>
    [Fact]
    public void TheProductionStoreDeclaresEveryReopenAndRecheckMemberItself()
    {
        var declared = typeof(Pegasus.Infrastructure.Persistence.EfUnidentifiedStore)
            .GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(IUnidentifiedStore.ReopenAsync), declared);
        Assert.Contains(nameof(IUnidentifiedStore.ListResolutionsToRecheckAsync), declared);
        Assert.Contains(nameof(IUnidentifiedStore.MarkResolutionRecheckedAsync), declared);
    }
}
