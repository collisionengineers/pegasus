using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// An <see cref="IMemoryCache"/> whose entries expire on a test's
/// <see cref="TimeProvider"/>, so a test that moves its clock past an entry's
/// lifetime sees the entry go as production would.
/// </summary>
internal static class TimeProviderMemoryCache
{
    public static MemoryCache Create(TimeProvider clock) =>
        new(new MemoryCacheOptions { Clock = new Clock(clock) });

    private sealed class Clock(TimeProvider clock) : ISystemClock
    {
        public DateTimeOffset UtcNow => clock.GetUtcNow();
    }
}
