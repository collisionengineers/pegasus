using CollisionBrain;

namespace CollisionBrain.Tests;

public sealed class AuthTests
{
    [Fact]
    public async Task SharedSecretAuthenticatesBearerAndRejectsWrongSecret()
    {
        var provider = new SharedSecretAuthProvider("correct-secret");

        var principal = await provider.AuthenticateAsync("Bearer correct-secret");

        Assert.Equal("shared-secret", principal.Subject);
        Assert.True(principal.Has(Role.Reader));
        Assert.True(principal.Has(Role.Contributor));
        Assert.True(principal.Has(Role.Admin));
        await Assert.ThrowsAsync<UnauthorizedError>(() => provider.AuthenticateAsync("Bearer wrong-secret"));
    }

    [Fact]
    public async Task SharedSecretRequiresBearerHeader()
    {
        var provider = new SharedSecretAuthProvider("correct-secret");

        await Assert.ThrowsAsync<UnauthorizedError>(() => provider.AuthenticateAsync(null));
        await Assert.ThrowsAsync<UnauthorizedError>(() => provider.AuthenticateAsync("correct-secret"));
    }
}
