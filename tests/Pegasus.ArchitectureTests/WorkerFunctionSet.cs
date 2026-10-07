using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The Worker's functions as the compiled assembly declares them. Every list that must
/// agree with it (bicep Disabled settings, the release scripts' census, the composition
/// tests) is checked against this one reading, never against a hand-kept copy.
/// </summary>
internal static class WorkerFunctionSet
{
    /// <summary>
    /// The functions the Worker runs. The due-work sweep and the approved-inbox recovery
    /// are not here: they run inside <see cref="PendingWorkRecoveryFunction"/> on every
    /// fifth minute, with no function, schedule or Disabled setting of their own.
    /// </summary>
    internal static readonly string[] ExpectedNames =
    [
        nameof(PendingWorkRecoveryFunction),
        nameof(UnifiedWorkFunction),
        nameof(UnifiedWorkPoisonFunction),
        nameof(StagedArtifactReconciliationFunction),
        nameof(SentEvidencePollFunction)
    ];

    internal sealed record Declared(string Name, Type Type, MethodInfo Method);

    internal static IReadOnlyList<Declared> Declarations() =>
        typeof(PendingWorkRecoveryFunction).Assembly.GetTypes()
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => (Type: type, Method: method, Attribute: method.GetCustomAttribute<FunctionAttribute>()))
                .Where(item => item.Attribute is not null)
                .Select(item => new Declared(item.Attribute!.Name, item.Type, item.Method)))
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>The set is exactly the expected six, and each one can be built from the composition.</summary>
    internal static void AssertEveryFunctionActivates(IServiceProvider scopedServices)
    {
        var declared = Declarations();
        Assert.Equal(
            ExpectedNames.Order(StringComparer.Ordinal),
            declared.Select(item => item.Name));
        foreach (var function in declared)
        {
            Assert.NotNull(ActivatorUtilities.CreateInstance(scopedServices, function.Type));
        }
    }
}
