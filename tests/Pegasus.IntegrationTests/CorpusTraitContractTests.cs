using Pegasus.Tests.Shared;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Guards the lane boundary for this assembly. Three tests across the mapping
/// corpus were discovered by skip-only attributes and classified by nothing,
/// so the ordinary lane ran them wherever the operator's corpus existed and
/// the focused corpus lane never selected them.
///
/// Needs no database or host, so it carries no SqlServer category.
/// </summary>
public sealed class CorpusTraitContractTests
{
    // LocalDbTemplateFact gates on an external SQL data source rather than on
    // corpus evidence: it must stay in the ordinary lane.
    private static readonly IReadOnlySet<string> Exempt =
        new HashSet<string>(StringComparer.Ordinal) { "LocalDbTemplateFactAttribute" };

    // Named, not counted: renaming a gate away would otherwise leave the rule
    // below passing over nothing.
    private static readonly string[] ExpectedGates =
    [
        "GenuineFormatCorpusFactAttribute",
        "GenuineQdosCorpusFactAttribute",
        "IncidentVehicleSourceFactAttribute",
        "PrincipalIdentificationCorpusFactAttribute",
        "QdosCorpusFactAttribute",
        "QdosLabelledCorpusFactAttribute",
        "QdosMappingCorpusTheoryAttribute",
        "QdosMappingCustodyFactAttribute",
        "ReferencePackFactAttribute",
        "ReferencePackTheoryAttribute",
        "SkippableCorpusFactAttribute",
    ];

    [Fact]
    public void EveryCorpusGatedTestDeclaresTheCorpusCategory()
    {
        var unclassified = CorpusTraitContract.FindUnclassifiedTests(
            typeof(CorpusTraitContractTests).Assembly, Exempt);

        Assert.True(
            unclassified.Count == 0,
            $"These tests read local corpus or private reference evidence but carry no Category=Corpus, so they belong to no lane:{Environment.NewLine}" +
            string.Join(Environment.NewLine, unclassified));
    }

    [Fact]
    public void TheGatesThisRuleCoversStillExist()
    {
        var gates = CorpusTraitContract.FindGates(typeof(CorpusTraitContractTests).Assembly, Exempt);

        Assert.Equal(ExpectedGates, gates);
    }
}
