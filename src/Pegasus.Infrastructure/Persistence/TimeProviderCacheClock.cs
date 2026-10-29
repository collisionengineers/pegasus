using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The <see cref="MemoryCacheOptions.Clock"/> that reads the host's
/// <see cref="TimeProvider"/>, so <see cref="IMemoryCache"/> entries expire on
/// the same time as everything else in the process, a test's moved clock
/// included. <c>MemoryCacheOptions</c> takes only an <see cref="ISystemClock"/>.
/// </summary>
internal sealed class TimeProviderCacheClock(TimeProvider timeProvider) : ISystemClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
