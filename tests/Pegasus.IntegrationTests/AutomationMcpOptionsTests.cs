using Microsoft.Extensions.Configuration;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

public sealed class AutomationMcpOptionsTests
{
    [Fact]
    public void ProductionRequiresSeparateVersionedSecretsFromConfiguredVault()
    {
        var values = BaseConfiguration();
        values["AutomationMcp:KeyVaultUri"] = "https://pegasus.vault.azure.net/";
        values["AutomationMcp:SigningCertificateSecretUris:0"] =
            "https://pegasus.vault.azure.net/secrets/oauth-signing/11111111111111111111111111111111";
        values["AutomationMcp:EncryptionCertificateSecretUris:0"] =
            "https://pegasus.vault.azure.net/secrets/oauth-encryption/22222222222222222222222222222222";

        var options = AutomationMcpOptions.TryCreate(Configuration(values));

        Assert.NotNull(options);
        Assert.False(options.UseDevelopmentKeys);
        Assert.Single(options.SigningCertificateSecretUris);
        Assert.Single(options.EncryptionCertificateSecretUris);
    }

    [Theory]
    [InlineData("https://user@pegasus.vault.azure.net/secrets/oauth-signing/version")]
    [InlineData("https://other.vault.azure.net/secrets/oauth-signing/version")]
    [InlineData("https://pegasus.vault.azure.net:444/secrets/oauth-signing/version")]
    [InlineData("https://pegasus.vault.azure.net/secrets/oauth-signing")]
    public void ProductionRejectsUntrustedOrUnversionedSecretUri(string signingUri)
    {
        var values = BaseConfiguration();
        values["AutomationMcp:KeyVaultUri"] = "https://pegasus.vault.azure.net/";
        values["AutomationMcp:SigningCertificateSecretUris:0"] = signingUri;
        values["AutomationMcp:EncryptionCertificateSecretUris:0"] =
            "https://pegasus.vault.azure.net/secrets/oauth-encryption/version";

        Assert.Throws<InvalidOperationException>(() =>
            AutomationMcpOptions.TryCreate(Configuration(values)));
    }

    [Fact]
    public void ProductionRejectsDevelopmentKeys()
    {
        var values = BaseConfiguration();
        values["AutomationMcp:UseDevelopmentKeys"] = "true";

        Assert.Throws<InvalidOperationException>(() =>
            AutomationMcpOptions.TryCreate(Configuration(values)));
    }

    /// <summary>R1297: with no redirect URI configured, sign-in from Pegasus Desktop is not offered.</summary>
    [Fact]
    public void DesktopSignInIsOffWithoutItsRedirectUris()
    {
        var options = AutomationMcpOptions.TryCreate(Configuration(ProductionConfiguration()));

        Assert.NotNull(options);
        Assert.Empty(options.DesktopRedirectUris);
        Assert.False(options.DesktopSignInEnabled);
    }

    [Fact]
    public void DesktopRedirectUrisAreLoopbackAddressesWithOrWithoutAPort()
    {
        var values = ProductionConfiguration();
        values["AutomationMcp:DesktopRedirectUris"] =
            "http://127.0.0.1/callback; http://[::1]:51793/callback,http://127.0.0.1:62031/callback";

        var options = AutomationMcpOptions.TryCreate(Configuration(values));

        Assert.NotNull(options);
        Assert.True(options.DesktopSignInEnabled);
        Assert.Equal(
            "http://127.0.0.1/callback http://[::1]:51793/callback http://127.0.0.1:62031/callback",
            string.Join(' ', options.DesktopRedirectUris.Select(uri => uri.OriginalString)));
        // The connector's own redirect URIs are a separate setting.
        Assert.Empty(options.RedirectUris);
        Assert.False(options.ConnectorAuthorizationEnabled);
    }

    [Theory]
    [InlineData("http://localhost/callback")]
    [InlineData("https://127.0.0.1/callback")]
    [InlineData("http://192.168.0.10/callback")]
    [InlineData("http://pegasus.example/callback")]
    [InlineData("http://user@127.0.0.1/callback")]
    [InlineData("http://127.0.0.1/callback#fragment")]
    [InlineData("pegasus-desktop://callback")]
    [InlineData("/callback")]
    public void DesktopRedirectUrisRejectAnythingButHttpOnALoopbackAddress(string redirectUri)
    {
        var values = ProductionConfiguration();
        values["AutomationMcp:DesktopRedirectUris"] = "http://127.0.0.1/callback;" + redirectUri;

        Assert.Throws<InvalidOperationException>(() =>
            AutomationMcpOptions.TryCreate(Configuration(values)));
    }

    [Fact]
    public void TheAutomationClientCannotTakePegasusDesktopsClientId()
    {
        var values = ProductionConfiguration();
        values["AutomationMcp:ClientId"] = AutomationMcp.DesktopClientId;

        Assert.Throws<InvalidOperationException>(() =>
            AutomationMcpOptions.TryCreate(Configuration(values)));
    }

    private static Dictionary<string, string?> ProductionConfiguration()
    {
        var values = BaseConfiguration();
        values["AutomationMcp:KeyVaultUri"] = "https://pegasus.vault.azure.net/";
        values["AutomationMcp:SigningCertificateSecretUris:0"] =
            "https://pegasus.vault.azure.net/secrets/oauth-signing/11111111111111111111111111111111";
        values["AutomationMcp:EncryptionCertificateSecretUris:0"] =
            "https://pegasus.vault.azure.net/secrets/oauth-encryption/22222222222222222222222222222222";
        return values;
    }

    private static Dictionary<string, string?> BaseConfiguration() => new(StringComparer.Ordinal)
    {
        [AutomationMcp.FeatureFlag] = "true",
        ["Runtime:Profile"] = "Production",
        ["AutomationMcp:ClientId"] = "pegasus-automation",
        ["AutomationMcp:ClientSecret"] = "integration-only-secret-0123456789",
        ["AutomationMcp:PublicOrigin"] = "https://pegasus.example/"
    };

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
