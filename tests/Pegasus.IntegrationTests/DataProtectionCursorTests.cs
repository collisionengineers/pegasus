using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

public sealed class DataProtectionCursorTests
{
    [Fact]
    public void RoundTripsExactSortKeyAndIdentifier()
    {
        var protector = CreateEphemeral();
        var id = Guid.NewGuid();
        var cursor = protector.Protect("cases:actor=staff-1:order=reference", "CE-2031-0042", id);

        var result = protector.Unprotect(cursor, "cases:actor=staff-1:order=reference");

        Assert.Equal("CE-2031-0042", result.SortKey);
        Assert.Equal(id, result.Id);
    }

    [Fact]
    public void RejectsTamperingAndDifferentQueryScope()
    {
        var protector = CreateEphemeral();
        var cursor = protector.Protect("scope-a", "key", Guid.NewGuid());
        var tampered = cursor[..^1] + (cursor[^1] == 'A' ? 'B' : 'A');

        Assert.Throws<CursorRejectedException>(() => protector.Unprotect(tampered, "scope-a"));
        Assert.Throws<CursorRejectedException>(() => protector.Unprotect(cursor, "scope-b"));
    }

    [Fact]
    public void PersistentKeysSurviveProviderRestart()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"pegasus-cursor-{Guid.NewGuid():N}"));
        try
        {
            var first = CreatePersistent(directory);
            var id = Guid.NewGuid();
            var cursor = first.Protect("scope", "key", id);

            var second = CreatePersistent(directory);
            Assert.Equal(("key", id), second.Unprotect(cursor, "scope"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RejectsMalformedAndOversizedTokensAndInvalidPayloadInputs()
    {
        var protector = CreateEphemeral();
        Assert.Throws<CursorRejectedException>(() => protector.Unprotect("not-protected", "scope"));
        Assert.Throws<CursorRejectedException>(() => protector.Unprotect(new string('x', 4097), "scope"));
        Assert.Throws<ArgumentException>(() => protector.Protect("scope", "key", Guid.Empty));
        Assert.Throws<ArgumentException>(() => protector.Protect("scope", new string('k', 1025), Guid.NewGuid()));
    }

    [Fact]
    public void RejectsAProtectedPayloadFromAnotherCodecVersion()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        using var provider = services.BuildServiceProvider();
        var dataProtection = provider.GetRequiredService<IDataProtectionProvider>();
        var stale = dataProtection.CreateProtector("Pegasus.CursorPaging.v1", "scope")
            .Protect($"{{\"version\":2,\"sortKey\":\"key\",\"id\":\"{Guid.NewGuid():D}\"}}");

        Assert.Throws<CursorRejectedException>(() =>
            new DataProtectionCursorProtector(dataProtection).Unprotect(stale, "scope"));
    }

    private static DataProtectionCursorProtector CreateEphemeral()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        return new DataProtectionCursorProtector(services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>());
    }

    private static DataProtectionCursorProtector CreatePersistent(DirectoryInfo directory)
    {
        var services = new ServiceCollection();
        services.AddDataProtection().PersistKeysToFileSystem(directory);
        return new DataProtectionCursorProtector(services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>());
    }
}
