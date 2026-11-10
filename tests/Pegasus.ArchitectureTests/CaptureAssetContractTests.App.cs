namespace Pegasus.ArchitectureTests;

public sealed partial class CaptureAssetContractTests
{
    /// <summary>
    /// The app's wordings settled on 10 November 2026 are served as ruled,
    /// and the ones they replace are gone: "photos" and "Number plate" (R866),
    /// no "one-time" (R872), "Sent to the customer" (R873), the Bodyshop's
    /// VIN text (R874), the review line under a strip (R862) and the server's
    /// size sentence (R878).
    /// </summary>
    [Fact]
    public void TheAppHoldsTheTenNovemberWordings()
    {
        var script = Read(ScriptPath);

        Assert.Contains("Accident scene photos (${nScene})", script, StringComparison.Ordinal);
        Assert.Contains("Other vehicle photos (${nOther})", script, StringComparison.Ordinal);
        Assert.Contains("plate:'Number plate'", script, StringComparison.Ordinal);
        Assert.Contains("We’ve created a link for <strong>", script, StringComparison.Ordinal);
        Assert.Contains("'Sent to the customer'", script, StringComparison.Ordinal);
        Assert.Contains("'A clear shot of the VIN plate or chassis number.'", script, StringComparison.Ordinal);
        Assert.Contains("Plus ${n} sent earlier", script, StringComparison.Ordinal);
        Assert.Contains("'The file is larger than this link accepts.'", script, StringComparison.Ordinal);

        Assert.DoesNotContain("Accident scene images", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Other vehicle images", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Registration plate", script, StringComparison.Ordinal);
        Assert.DoesNotContain("one-time", script, StringComparison.Ordinal);
        Assert.DoesNotContain("if it’s easy to get to", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// The damage step's "Could you add a few more?" opens only when a
    /// damaged area has fewer than 3 photos, not on a total (R875).
    /// </summary>
    [Fact]
    public void TheDamageNudgeOpensOnlyForAShortArea()
    {
        var script = Read(ScriptPath);

        Assert.Contains("if(nb && weakAreas().length) nb.onclick=()=>nudge(dmgTotal());", script, StringComparison.Ordinal);
        Assert.DoesNotContain("dmgTotal()<8", script, StringComparison.Ordinal);
        Assert.DoesNotContain("You’ve taken ${n}.", script, StringComparison.Ordinal);
    }
}
