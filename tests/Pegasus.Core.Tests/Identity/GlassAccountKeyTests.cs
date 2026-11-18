using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Identity;

/// <summary>
/// One rule makes a Glass's login an account key (design 07 section 1): the
/// credential store and the Automation's login both take theirs from
/// <see cref="GlassAccountKey"/>, so keys the store minted before the rule
/// moved into Core are the keys it makes now, and the same login is one slot
/// whoever holds it.
/// </summary>
public sealed class GlassAccountKeyTests
{
    /// <summary>
    /// SHA-256 of <c>glass_repair_estimate\nA.ENGINEER</c>, the value the
    /// credential store's own rule gave before it moved here.
    /// </summary>
    private const string StoredKey = "c8085adcc039d128449e2dfa9ed4a0bf8288d428b34b74d8122a1d10f3751067";

    [Fact]
    public void ALoginIsTheKeyTheCredentialStoreMinted() =>
        Assert.Equal(StoredKey, GlassAccountKey.Of("a.engineer"));

    [Theory]
    [InlineData("A.ENGINEER")]
    [InlineData("a.Engineer")]
    public void ALoginIsOneKeyWhateverItsCase(string username) =>
        Assert.Equal(StoredKey, GlassAccountKey.Of(username));

    [Fact]
    public void AnotherLoginIsAnotherKeyAndTheUsernameIsNotInIt()
    {
        var key = GlassAccountKey.Of("b.engineer");

        Assert.NotEqual(StoredKey, key);
        Assert.Equal(64, key.Length);
        Assert.DoesNotContain("engineer", key, StringComparison.OrdinalIgnoreCase);
    }
}
