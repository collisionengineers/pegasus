using Pegasus.Core;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests;

public sealed class CursorPagingTests
{
    [Theory]
    [InlineData(null, 50)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    public void NormalizesSupportedLimits(int? requested, int expected) =>
        Assert.Equal(expected, CursorPaging.NormalizeLimit(requested));

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public void RejectsUnsupportedLimits(int requested) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CursorPaging.NormalizeLimit(requested));

    [Fact]
    public void ScopeIsCanonicalAndBindsActorFiltersAndOrder()
    {
        var actor = ActionActor.Automation("grant-1");
        var first = CursorPaging.CreateScope("cases", actor, "principal=p1", null, "created,id");
        var replay = CursorPaging.CreateScope("cases", actor, "principal=p1", null, "created,id");
        var changed = CursorPaging.CreateScope("cases", actor, "principal=p2", null, "created,id");

        Assert.Equal(first, replay);
        Assert.NotEqual(first, changed);
        Assert.Contains("grant-1", first, StringComparison.Ordinal);
    }

    [Fact]
    public void UtcTimestampCodecRoundTripsAndRejectsMalformedValues()
    {
        var value = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.FromHours(1));
        var encoded = CursorPaging.EncodeUtcTimestamp(value);

        Assert.Equal(value.ToUniversalTime(), CursorPaging.DecodeUtcTimestamp(encoded));
        Assert.Throws<CursorRejectedException>(() => CursorPaging.DecodeUtcTimestamp("1.5"));
        Assert.Throws<CursorRejectedException>(() => CursorPaging.DecodeUtcTimestamp(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}
