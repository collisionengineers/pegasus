namespace Pegasus.Core.Tests.Support;

/// <summary>
/// Tests that run document or mail text through the intake parsers, whose
/// regexes carry a 100 ms match timeout. xUnit runs a collection that disables
/// parallelization after, and apart from, every parallel one.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ParserRegexBudgetSerialTests
{
    public const string Name = "Parser regex budgets";
}
