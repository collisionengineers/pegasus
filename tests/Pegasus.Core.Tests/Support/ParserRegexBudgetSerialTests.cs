namespace Pegasus.Core.Tests.Support;

/// <summary>
/// Tests that run document or mail text through the intake parsers, whose
/// generated regexes carry a 100 ms match timeout (the engine's label
/// patterns one second, #1822). xUnit runs a collection that disables
/// parallelization after, and apart from, every parallel one.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ParserRegexBudgetSerialTests
{
    public const string Name = "Parser regex budgets";
}
