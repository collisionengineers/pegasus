using Pegasus.Tests.Shared;

namespace Pegasus.Core.Tests;

/// <summary>
/// Guards the lane boundary for this assembly. Two reference-pack tests were
/// discovered by a skip-only attribute and classified by nothing, so the
/// ordinary lane ran them on any machine holding the private pack.
/// </summary>
public sealed class CorpusTraitContractTests
{
    // Every derived Fact/Theory attribute here gates on the private reference
    // pack, so nothing is exempt.
    private static readonly IReadOnlySet<string> Exempt = new HashSet<string>(StringComparer.Ordinal);

    // Named, not counted: renaming a gate away would otherwise leave the rule
    // below passing over nothing.
    private static readonly string[] ExpectedGates =
    [
        "ReferencePackFactAttribute",
        "ReferencePackTheoryAttribute",
    ];

    [Fact]
    public void EveryCorpusGatedTestDeclaresTheCorpusCategory()
    {
        var unclassified = CorpusTraitContract.FindUnclassifiedTests(
            typeof(CorpusTraitContractTests).Assembly, Exempt);

        Assert.True(
            unclassified.Count == 0,
            $"These tests read private reference evidence but carry no Category=Corpus, so the ordinary lane runs them wherever that evidence exists:{Environment.NewLine}" +
            string.Join(Environment.NewLine, unclassified));
    }

    [Fact]
    public void TheGatesThisRuleCoversStillExist()
    {
        var gates = CorpusTraitContract.FindGates(typeof(CorpusTraitContractTests).Assembly, Exempt);

        Assert.Equal(ExpectedGates, gates);
    }
}
