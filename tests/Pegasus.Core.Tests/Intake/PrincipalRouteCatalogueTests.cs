using System.Reflection;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// A Principal exists as its <c>Principals</c> row; the code catalogue owns
/// which of them have an accepted e-mail route and how its instruction is read
/// (operator, 13 October 2026). The route list and the extraction policies are
/// kept together only by these pins.
/// </summary>
public sealed class PrincipalRouteCatalogueTests
{
    [Fact]
    public void EveryAcceptedRouteHasExactlyOneExtractionPolicy()
    {
        var extractionCodes = typeof(CoreAssembly).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (string)type
                .GetField("SupportedPrincipalCode", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!)
            .ToArray();

        Assert.Equal(extractionCodes.Length, extractionCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            extractionCodes.Order(StringComparer.Ordinal),
            PrincipalMailRoutePolicy.AcceptedIdentities.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NoIdentityIsAcceptedForTwoPrincipals()
    {
        var identities = PrincipalMailRoutePolicy.AcceptedIdentities
            .SelectMany(entry => entry.Value.Select(identity => (identity, entry.Key)))
            .ToArray();

        Assert.Equal(
            identities.Length,
            identities.Select(item => item.identity).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
