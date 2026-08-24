using Pegasus.Infrastructure.Custody;

namespace Pegasus.IntegrationTests;

/// <summary>
/// PLAT-039. The provider used to ask the Box SDK for a header, and the SDK
/// answers from a token cache it never expires — it re-mints only when the
/// cache is empty. Pegasus calls Box with its own <see cref="HttpClient"/>,
/// so the SDK's 401-and-refresh path never ran: a Web replica minted one
/// token at first use and served it for the life of the container, and every
/// Box read failed with 401 an hour later. Nothing here had a test, which is
/// the reason it shipped.
/// </summary>
public sealed class BoxAuthorizationHeaderTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);

    private const int OneHour = 3600;

    [Fact]
    public async Task TheFirstCallMintsAToken()
    {
        var mint = new CountingMint("first");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);

        Assert.Equal("Bearer first-1", await provider.GetAuthorizationHeaderAsync(default));
        Assert.Equal(1, mint.Count);
    }

    [Fact]
    public async Task ASecondCallInsideTheLifetimeReusesTheSameToken()
    {
        var mint = new CountingMint("live");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);

        var first = await provider.GetAuthorizationHeaderAsync(default);
        time.Advance(TimeSpan.FromMinutes(30));
        var second = await provider.GetAuthorizationHeaderAsync(default);

        Assert.Equal(first, second);
        Assert.Equal(1, mint.Count);
    }

    /// <summary>
    /// The defect itself: an hour after the first call the old token is dead,
    /// and before this fix the provider went on presenting it forever.
    /// </summary>
    [Fact]
    public async Task ACallPastTheLifetimeMintsAgain()
    {
        var mint = new CountingMint("aged");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);

        Assert.Equal("Bearer aged-1", await provider.GetAuthorizationHeaderAsync(default));
        time.Advance(TimeSpan.FromSeconds(OneHour));
        Assert.Equal("Bearer aged-2", await provider.GetAuthorizationHeaderAsync(default));
        Assert.Equal(2, mint.Count);
    }

    /// <summary>
    /// The renewal margin: a request that starts a few seconds before Box's
    /// stated expiry would arrive holding a dead token, so the token is
    /// replaced before the boundary rather than on it.
    /// </summary>
    [Fact]
    public async Task ATokenInsideTheRenewalMarginIsReplacedBeforeItExpires()
    {
        var mint = new CountingMint("margin");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);

        await provider.GetAuthorizationHeaderAsync(default);
        time.Advance(TimeSpan.FromSeconds(OneHour - 30));

        Assert.Equal("Bearer margin-2", await provider.GetAuthorizationHeaderAsync(default));
    }

    /// <summary>
    /// An export reads every photograph of a case, so a renewal lands in the
    /// middle of concurrent Box work. One token, not one per caller.
    /// </summary>
    [Fact]
    public async Task ConcurrentCallersAcrossAnExpiryMintOnce()
    {
        var mint = new CountingMint("shared");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);

        await provider.GetAuthorizationHeaderAsync(default);
        time.Advance(TimeSpan.FromSeconds(OneHour));
        mint.Hold();

        var callers = Enumerable
            .Range(0, 8)
            .Select(_ => provider.GetAuthorizationHeaderAsync(default))
            .ToArray();
        // Release only once a mint is genuinely in flight, so the other seven
        // are queued behind it rather than merely arriving after it.
        await mint.Entered;
        mint.Release();
        var headers = await Task.WhenAll(callers);

        Assert.Equal(2, mint.Count);
        Assert.Single(headers.Distinct(StringComparer.Ordinal));
        Assert.Equal("Bearer shared-2", headers[0]);
    }

    [Fact]
    public async Task AMintThatReturnsNoTokenFailsClosed()
    {
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(
            _ => Task.FromResult(new BoxAccessToken(null, OneHour)),
            time);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAuthorizationHeaderAsync(default));
    }

    /// <summary>
    /// Without a stated lifetime there is no honest renewal point, and
    /// assuming one is what this ticket exists to stop.
    /// </summary>
    [Fact]
    public async Task AMintThatStatesNoLifetimeFailsClosed()
    {
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(
            _ => Task.FromResult(new BoxAccessToken("token", null)),
            time);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAuthorizationHeaderAsync(default));
    }

    private sealed class CountingMint(string prefix)
    {
        private readonly Lock guard = new();
        private readonly TaskCompletionSource entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? gate;
        private int count;

        /// <summary>Completes when a held mint has actually begun.</summary>
        public Task Entered => entered.Task;

        public int Count
        {
            get
            {
                lock (guard)
                {
                    return count;
                }
            }
        }

        public void Hold()
        {
            lock (guard)
            {
                gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public void Release()
        {
            TaskCompletionSource? held;
            lock (guard)
            {
                held = gate;
                gate = null;
            }
            held?.SetResult();
        }

        public async Task<BoxAccessToken> ExecuteAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource? held;
            lock (guard)
            {
                held = gate;
            }
            if (held is not null)
            {
                entered.TrySetResult();
                await held.Task.WaitAsync(cancellationToken);
            }

            int issued;
            lock (guard)
            {
                issued = ++count;
            }
            return new($"{prefix}-{issued}", OneHour);
        }
    }
}
