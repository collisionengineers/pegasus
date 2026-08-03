using Pegasus.Infrastructure.Custody;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Pins the one shared custody safe-name mapping. Every custody adapter (Box
/// source custody, Box managed content, local managed content) resolves a
/// Case/PO through this single mapping, so folder-name agreement between
/// adapters is structural; these tests pin the mapping itself, including the
/// fixed platform-independent character set.
/// </summary>
public sealed class CustodyNamesTests
{
    [Theory]
    [InlineData("QDOS26001")]
    [InlineData("PCH26123")]
    public void AnAllocatedCaseReferenceMapsToItself(string reference) =>
        Assert.Equal(reference, CustodyNames.SafeName(reference));

    [Fact]
    public void TheInvalidCharacterSetIsFixedAndPlatformIndependent()
    {
        // '/' and ':' are legal file-name characters on Linux but not on
        // Windows; the mapping must replace them everywhere so a remote Box
        // path never depends on which host computed it.
        Assert.Equal("AB_CD_EF", CustodyNames.SafeName("AB/CD:EF"));
        Assert.Equal("A_B_C_D_E_F_G_H_I", CustodyNames.SafeName("A\"B<C>D|E*F?G\\H/I"));
        // A space is a legal name character on every platform and passes
        // through unchanged, exactly as the pre-consolidation mappings did.
        Assert.Equal("AB CD", CustodyNames.SafeName("AB CD"));
    }

    [Fact]
    public void SurroundingWhitespaceIsTrimmedBeforeMapping() =>
        Assert.Equal("QDOS26001", CustodyNames.SafeName("  QDOS26001  "));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyNameIsRefused(string value) =>
        Assert.Throws<ArgumentException>(() => CustodyNames.SafeName(value));

    [Fact]
    public void AnOversizeNameIsRefused() =>
        Assert.Throws<ArgumentException>(() => CustodyNames.SafeName(new string('A', 181)));
}
