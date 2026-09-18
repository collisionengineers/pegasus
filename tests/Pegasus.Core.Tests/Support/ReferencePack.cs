namespace Pegasus.Core.Tests.Support;

/// <summary>
/// The local, git-ignored reference-evidence pack. It differs per machine and
/// is located only through <c>PEGASUS_REFERENCE_PACK_ROOT</c> (runbook
/// "Private reference evidence"). The integration project carries its own
/// copy of this locator because the pack is read by two assemblies and
/// neither is a dependency of the other.
/// </summary>
internal static class ReferencePack
{
    public const string RootVariable = "PEGASUS_REFERENCE_PACK_ROOT";

    /// <summary>The configured root, or <c>null</c> when the variable is unset.</summary>
    public static string? ConfiguredRoot()
    {
        var root = Environment.GetEnvironmentVariable(RootVariable);
        return string.IsNullOrWhiteSpace(root) ? null : root;
    }

    /// <summary>The root a pack-gated test may read from; the gate has already skipped otherwise.</summary>
    public static string Root() =>
        ConfiguredRoot() ?? throw new InvalidOperationException("The reference-pack test should have been skipped.");

    /// <summary>
    /// The skip reason when the pack is unavailable, or <c>null</c>. Stated,
    /// never silent: an absent pack is inconclusive, not a pass.
    /// </summary>
    public static string? SkipReason()
    {
        var root = ConfiguredRoot();
        if (root is null)
        {
            return $"{RootVariable} is not set; the reference pack is a local, "
                + "git-ignored collection that differs per machine. INCONCLUSIVE, not passed.";
        }

        return Directory.Exists(root)
            ? null
            : $"{RootVariable} names a directory that does not exist on this machine. INCONCLUSIVE, not passed.";
    }
}

/// <summary>A fact that needs the reference pack; skips with a stated reason without it.</summary>
internal sealed class ReferencePackFactAttribute : FactAttribute
{
    public ReferencePackFactAttribute()
    {
        Skip = ReferencePack.SkipReason();
    }
}

/// <summary>A theory that needs the reference pack; skips with a stated reason without it.</summary>
internal sealed class ReferencePackTheoryAttribute : TheoryAttribute
{
    public ReferencePackTheoryAttribute()
    {
        Skip = ReferencePack.SkipReason();
    }
}
