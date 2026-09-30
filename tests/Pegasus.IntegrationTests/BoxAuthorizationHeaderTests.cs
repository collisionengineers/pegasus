using Pegasus.Infrastructure.Custody;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The provider used to ask the Box SDK for a header, and the SDK
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

    /// <summary>
    /// The lifetime guard also protects the background renewal: a token that
    /// expired inside the margin plus the early-renewal window would be due
    /// again the moment it was minted, and the renewal would mint every check.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(400)]
    public async Task ATokenWhoseLifetimeIsInsideTheRenewalWindowIsRefusedByBothPaths(int lifetimeSeconds)
    {
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(
            _ => Task.FromResult(new BoxAccessToken("short", lifetimeSeconds)),
            time);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAuthorizationHeaderAsync(default));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.RenewIfDueAsync(default));
    }

    [Fact]
    public async Task RenewingWithNoTokenMintsOneAndTheNextRequestUsesIt()
    {
        var mint = new CountingMint("first");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);

        Assert.True(await provider.RenewIfDueAsync(default));
        Assert.Equal("Bearer first-1", await provider.GetAuthorizationHeaderAsync(default));
        Assert.Equal(1, mint.Count);
    }

    [Fact]
    public async Task RenewingWhileTheTokenIsLiveMintsNothing()
    {
        var mint = new CountingMint("live");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);

        await provider.GetAuthorizationHeaderAsync(default);
        time.Advance(TimeSpan.FromMinutes(30));

        Assert.False(await provider.RenewIfDueAsync(default));
        Assert.Equal(1, mint.Count);
    }

    /// <summary>
    /// The renewal comes five minutes before a request would have to mint: the
    /// margin is 120 seconds, so the token is due from 3,180 seconds of its
    /// 3,600. The request path is unchanged, so until that renewal a request
    /// still gets the live token and never waits.
    /// </summary>
    [Fact]
    public async Task RenewingMintsFiveMinutesBeforeARequestWouldAndRequestsUseTheLiveTokenMeanwhile()
    {
        var mint = new CountingMint("early");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);
        await provider.GetAuthorizationHeaderAsync(default);

        time.Advance(TimeSpan.FromSeconds(3170));
        Assert.False(await provider.RenewIfDueAsync(default));

        time.Advance(TimeSpan.FromSeconds(20));
        // Due for the background renewal, still live for a request.
        Assert.Equal("Bearer early-1", await provider.GetAuthorizationHeaderAsync(default));
        Assert.Equal(1, mint.Count);

        Assert.True(await provider.RenewIfDueAsync(default));
        Assert.Equal("Bearer early-2", await provider.GetAuthorizationHeaderAsync(default));
        Assert.Equal(2, mint.Count);
        Assert.False(await provider.RenewIfDueAsync(default));
    }

    /// <summary>
    /// Inside the early-renewal window the held token is due for renewal but is
    /// still live for a request. A request that arrives while a renewal is
    /// minting gets that live token at once, without waiting on the renewal's
    /// gate, and starts no mint of its own. This is the intended behaviour, not
    /// a gap: the request path is unchanged by the background renewal.
    /// </summary>
    [Fact]
    public async Task ARequestInsideTheEarlyWindowGetsTheLiveTokenAtOnceWhileARenewalMints()
    {
        var mint = new CountingMint("window");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);
        await provider.GetAuthorizationHeaderAsync(default);
        // 3,200 s of 3,600: due for the background renewal (from 3,180 s) and
        // still live for a request (until 3,480 s).
        time.Advance(TimeSpan.FromSeconds(3200));
        mint.Hold();

        var renewal = provider.RenewIfDueAsync(default);
        await mint.Entered;
        var request = provider.GetAuthorizationHeaderAsync(default);

        Assert.True(request.IsCompletedSuccessfully, "The request waited on the renewal's mint.");
        Assert.Equal("Bearer window-1", await request);
        // Only the first mint has finished. The held renewal is the one in flight.
        Assert.Equal(1, mint.Count);

        mint.Release();
        Assert.True(await renewal);
        // The first mint and the one renewal: the request made none.
        Assert.Equal(2, mint.Count);
        Assert.Equal("Bearer window-2", await provider.GetAuthorizationHeaderAsync(default));
        Assert.Equal(2, mint.Count);
    }

    /// <summary>
    /// The renewal takes the request path's gate. A request that arrives while
    /// it mints waits for that mint and gets its token, and no second token is
    /// minted, both at the very first mint and at an expiry the request path
    /// could not have used.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARequestArrivingDuringARenewalGetsTheRenewedTokenAndNoSecondMint(bool heldTokenDead)
    {
        var mint = new CountingMint("shared");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);
        var expected = "Bearer shared-1";
        if (heldTokenDead)
        {
            await provider.GetAuthorizationHeaderAsync(default);
            time.Advance(TimeSpan.FromSeconds(OneHour - 30));
            expected = "Bearer shared-2";
        }
        var mintsBefore = mint.Count;
        mint.Hold();

        var renewal = provider.RenewIfDueAsync(default);
        await mint.Entered;
        var request = provider.GetAuthorizationHeaderAsync(default);
        Assert.False(request.IsCompleted);
        mint.Release();

        Assert.True(await renewal);
        Assert.Equal(expected, await request);
        Assert.Equal(mintsBefore + 1, mint.Count);
    }

    /// <summary>
    /// A renewal that fails changes nothing. The old token is still held and
    /// still served while it lives, and once it is past the margin the next
    /// request mints on demand exactly as it did before there was a renewal.
    /// </summary>
    [Fact]
    public async Task AFailedRenewalLeavesTheOldTokenAndTheNextRequestMintsOnDemand()
    {
        var mint = new CountingMint("kept");
        var time = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(Start);
        using var provider = new BoxJwtAuthorizationHeaderProvider(mint.ExecuteAsync, time);
        await provider.GetAuthorizationHeaderAsync(default);
        time.Advance(TimeSpan.FromSeconds(3200));

        mint.FailNext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RenewIfDueAsync(default));
        Assert.Equal("Bearer kept-1", await provider.GetAuthorizationHeaderAsync(default));
        Assert.Equal(1, mint.Count);

        time.Advance(TimeSpan.FromSeconds(OneHour - 3200 - 30));
        Assert.Equal("Bearer kept-2", await provider.GetAuthorizationHeaderAsync(default));
        Assert.Equal(2, mint.Count);
    }

    private sealed class CountingMint(string prefix)
    {
        private readonly Lock guard = new();
        private readonly TaskCompletionSource entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? gate;
        private int count;
        private bool failNext;

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

        /// <summary>Makes the next mint fail as Box being unreachable would. It issues no token.</summary>
        public void FailNext()
        {
            lock (guard)
            {
                failNext = true;
            }
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
                if (failNext)
                {
                    failNext = false;
                    throw new InvalidOperationException("The Box token endpoint was unreachable.");
                }

                issued = ++count;
            }
            return new($"{prefix}-{issued}", OneHour);
        }
    }
}
