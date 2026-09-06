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
