using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
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
}
