using System.Text.Json;
using Pegasus.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class IntakeStablePersistenceTests
{
    [Fact]
    public async Task UnknownFormatIsRetainedAsUnsupportedWithStableCodesAndVersionOneEnvelopes()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, "unknown-format.xyz",
        "application/x-unknown",
        [0x01, 0x02, 0x03]);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None));

        Assert.Equal(IntakeDecision.Unsupported, receipt.Decision);
        Assert.Null(receipt.InstructionDraft);
        Assert.Null(receipt.ExtractionPolicyKey);
        var sourceAsset = Assert.Single(receipt.AssetRecords);
        Assert.Equal(IntakeAssetKind.Source, sourceAsset.Kind);

        Assert.Equal("unsupported", await factory.Database.ScalarAsync<string>(
            "SELECT Decision FROM IntakeReceipts"));
        Assert.Equal("manual_upload", await factory.Database.ScalarAsync<string>(
            "SELECT SourceChannel FROM IntakeReceipts"));
        Assert.Equal("source", await factory.Database.ScalarAsync<string>(
            "SELECT Kind FROM IntakeAssets"));
        Assert.Equal("source", await factory.Database.ScalarAsync<string>(
            "SELECT Disposition FROM IntakeAssets"));
        Assert.Equal("intake_receipt_recorded", await factory.Database.ScalarAsync<string>(
            "SELECT EventType FROM IntakeReceiptEvents"));
        AssertEnvelopeVersionOne(await factory.Database.ScalarAsync<string>(
            "SELECT EvidenceJson FROM IntakeReceipts"));
        AssertEnvelopeVersionOne(await factory.Database.ScalarAsync<string>(
            "SELECT FieldsJson FROM IntakeReceipts"));
        AssertEnvelopeVersionOne(await factory.Database.ScalarAsync<string>(
            "SELECT OcrCandidatesJson FROM IntakeReceipts"));
        AssertEnvelopeVersionOne(await factory.Database.ScalarAsync<string>(
            "SELECT DetailsJson FROM IntakeReceiptEvents"));
    }

    [Fact]
    public async Task UnknownPersistedDecisionCodeFailsVisibleRead()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receiptId = await UploadUnknownAsync(factory);
        await ExecuteAsync(factory, "UPDATE IntakeReceipts SET Decision='future_decision'");

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => queries.GetAsync(receiptId, CancellationToken.None));

        Assert.Contains("future_decision", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownPersistedDecisionCodeFailsVisibleAcrossQueueQueries()
    {
        using var factory = new IntakeWebApplicationFactory();
        await UploadUnknownAsync(factory);
        await ExecuteAsync(factory, "UPDATE IntakeReceipts SET Decision='future_decision'");

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();

        var countsError = await Assert.ThrowsAsync<InvalidDataException>(
            () => queries.GetCountsAsync(CancellationToken.None));
        var unfilteredListError = await Assert.ThrowsAsync<InvalidDataException>(
            () => queries.ListAsync(null, 1, 100, CancellationToken.None));

        Assert.Contains("future_decision", countsError.Message, StringComparison.Ordinal);
        Assert.Contains("future_decision", unfilteredListError.Message, StringComparison.Ordinal);

        // A filtered list now selects by persisted code in SQL, so it does not
        // read a row it did not ask for and cannot throw on one. What the rule
        // forbids is silent reinterpretation, and that still holds: the row is
        // absent from a filter for a known decision rather than appearing under
        // it. The always-on guarantee is the counts, which still scan every
        // non-case-linked receipt and so throw on a corrupt code wherever it
        // sits; the unfiltered list is paged in SQL, so it throws only when the
        // page it reads contains the corrupt row. A corrupt code on a
        // case-linked receipt is invisible to both. Recovering the throw here
        // would mean scanning every receipt on every page load, which is the
        // defect this filter fixed.
        var filtered = await queries.ListAsync(
            IntakeDecision.Unsupported,
            1,
            100,
            CancellationToken.None);
        Assert.Empty(filtered.Items);
        Assert.Equal(0, filtered.TotalCount);
    }

    [Fact]
    public async Task UnknownPersistedJsonEnvelopeVersionFailsVisibleRead()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receiptId = await UploadUnknownAsync(factory);
        await ExecuteAsync(factory,
            "UPDATE IntakeReceipts SET EvidenceJson='{\"version\":2,\"data\":[]}'");

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => queries.GetAsync(receiptId, CancellationToken.None));

        Assert.Contains("version '2'", error.Message, StringComparison.Ordinal);
    }

    private static async Task<Guid> UploadUnknownAsync(IntakeWebApplicationFactory factory)
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        return IntakeWebDriver.ReceiptId(await IntakeWebDriver.UploadAndProcessAsync(factory, client, "unknown-format.xyz",
        "application/x-unknown",
        [0x01]));
    }

    private static Task ExecuteAsync(IntakeWebApplicationFactory factory, string sql) =>
        factory.Database.ExecuteAsync(sql);


    private static void AssertEnvelopeVersionOne(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("data", out _));
    }

    /// <summary>
    /// Keyset continuation over the received-items list, against real SQL.
    ///
    /// The property that matters is that the pages PARTITION the list: every
    /// receipt appears exactly once across them, in the same order the offset
    /// list uses, whatever the page size. An offset page cannot promise that
    /// while receipts keep arriving, which is the whole reason for the cursor.
    /// </summary>
    [Fact]
    public async Task TheReceivedListPagesDeterministicallyByCursor()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var expected = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            var upload = await IntakeWebDriver.UploadAndProcessAsync(
                factory,
                client,
                $"keyset-{index}.xyz",
                "application/x-unknown",
                [0x01, 0x02, (byte)index]);
            expected.Add(IntakeWebDriver.ReceiptId(upload));
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var protector = new FakeCursorProtector();
        var list = new ListIntakeByCursor(
            services.GetRequiredService<IIntakeReceiptQueries>(),
            protector);

        // The order the offset list already promises, so the two views can
        // never disagree about what "newest first" means.
        var offset = await services.GetRequiredService<IIntakeReceiptQueries>()
            .ListAsync(null, 1, 50, CancellationToken.None);
        var offsetOrder = offset.Items.Select(item => item.Id).ToArray();
        Assert.Equal(5, offsetOrder.Length);

        foreach (var pageSize in new[] { 1, 2, 5 })
        {
            var seen = new List<Guid>();
            string? cursor = null;
            var pages = 0;
            do
            {
                var page = await list.ExecuteAsync(new(actor, null, cursor, pageSize));
                Assert.True(page.Items.Count <= pageSize);
                seen.AddRange(page.Items.Select(item => item.Id));
                cursor = page.NextCursor;
                pages++;
                Assert.True(pages <= 10, "The continuation did not terminate.");
            }
            while (cursor is not null);

            Assert.Equal(offsetOrder, seen);
            Assert.Equal(seen.Count, seen.Distinct().Count());
        }
    }

    /// <summary>
    /// A cursor is bound to the query, the actor and the filters it was minted
    /// under. Replaying one against a different filter is refused rather than
    /// resuming into a list it never described.
    /// </summary>
    [Fact]
    public async Task ACursorMintedForAnotherScopeIsRejected()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        for (var index = 0; index < 3; index++)
        {
            await IntakeWebDriver.UploadAndProcessAsync(
                factory,
                client,
                $"scope-{index}.xyz",
                "application/x-unknown",
                [0x09, (byte)index]);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var list = new ListIntakeByCursor(
            services.GetRequiredService<IIntakeReceiptQueries>(),
            new FakeCursorProtector());

        var unfiltered = await list.ExecuteAsync(new(actor, null, null, 1));
        Assert.NotNull(unfiltered.NextCursor);

        await Assert.ThrowsAsync<CursorRejectedException>(() =>
            list.ExecuteAsync(new(actor, IntakeDecision.Unsupported, unfiltered.NextCursor, 1)));

        var otherActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await Assert.ThrowsAsync<CursorRejectedException>(() =>
            list.ExecuteAsync(new(otherActor, null, unfiltered.NextCursor, 1)));
    }

    /// <summary>
    /// Stands in for the host's data-protection adapter: it binds the payload
    /// to the scope exactly as that adapter does, so the scope rule is tested
    /// here without dragging data-protection key management into a store test.
    /// </summary>
    internal sealed class FakeCursorProtector : ICursorProtector
    {
        public string Protect(string scope, string sortKey, Guid id) =>
            $"{scope.GetHashCode(StringComparison.Ordinal)}|{sortKey}|{id:N}";

        public (string SortKey, Guid Id) Unprotect(string cursor, string scope)
        {
            var parts = cursor.Split('|');
            if (parts.Length != 3
                || parts[0] != scope.GetHashCode(StringComparison.Ordinal)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture)
                || !Guid.TryParseExact(parts[2], "N", out var id))
            {
                throw new CursorRejectedException();
            }

            return (parts[1], id);
        }
    }
}
