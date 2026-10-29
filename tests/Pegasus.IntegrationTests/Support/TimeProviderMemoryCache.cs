using Microsoft.Extensions.Caching.Memory;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// The cache a host's registration builds (<see cref="TimeProviderCacheClock"/>),
/// for a test that constructs a cache user directly on its own clock rather
/// than resolving one from a host.
/// </summary>
internal static class TimeProviderMemoryCache
{
    public static MemoryCache Create(TimeProvider clock) =>
        new(new MemoryCacheOptions { Clock = new TimeProviderCacheClock(clock) });
}
