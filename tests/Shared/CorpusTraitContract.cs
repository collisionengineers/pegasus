using System.Reflection;

namespace Pegasus.Tests.Shared;

/// <summary>
/// The selection rule the corpus lanes depend on. A test gated on local corpus
/// or private reference evidence must carry <c>Category=Corpus</c>. Without it
/// the ordinary <c>Category!=Corpus</c> lane runs the test wherever that
/// evidence happens to exist, and the focused <c>Category=Corpus</c> lane never
/// selects it at all — so the test belongs to no lane.
///
/// A skip decision is not a trait. Every gate in this repository is a derived
/// <see cref="FactAttribute"/> that sets <c>Skip</c> at discovery time, which
/// keeps the test off a machine without the evidence but publishes no category
/// for the runner to filter on.
///
/// Compiled into each test assembly rather than referenced, so it reflects over
/// that assembly's own attributes while the rule stays written once.
/// </summary>
internal static class CorpusTraitContract
{
    /// <summary>
    /// Names the corpus-gated discovery attributes declared in an assembly.
    /// </summary>
    internal static IReadOnlyList<string> FindGates(Assembly assembly, IReadOnlySet<string> exempt)
    {
        var declared = assembly.GetTypes()
            .Where(type => typeof(FactAttribute).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToArray();

        var stale = exempt.Where(name => !declared.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (stale.Length > 0)
        {
            throw new InvalidOperationException(
                $"Exempted attributes that no longer exist: {string.Join(", ", stale)}. " +
                "Remove the exemption rather than leaving it to cover a future attribute by name.");
        }

        return declared.Where(name => !exempt.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Every test in the assembly that a corpus-gated attribute discovers but
    /// no <c>Category=Corpus</c> trait classifies.
    /// </summary>
    internal static IReadOnlyList<string> FindUnclassifiedTests(Assembly assembly, IReadOnlySet<string> exempt)
    {
        var gates = assembly.GetTypes()
            .Where(type => typeof(FactAttribute).IsAssignableFrom(type))
            .Where(type => !exempt.Contains(type.Name))
            .ToHashSet();

        return assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => (Method: method, Gate: GateOf(method, gates)))
            .Where(entry => entry.Gate is not null && !IsCorpusClassified(entry.Method))
            .Select(entry => $"{entry.Method.DeclaringType!.FullName}.{entry.Method.Name} [{entry.Gate!.Name}]")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static Type? GateOf(MethodInfo method, IReadOnlySet<Type> gates) =>
        method.GetCustomAttributes(inherit: false)
            .Select(attribute => attribute.GetType())
            .FirstOrDefault(gates.Contains);

    private static bool IsCorpusClassified(MethodInfo method) =>
        DeclaresCorpus(method) || DeclaresCorpus(method.DeclaringType!);

    private static bool DeclaresCorpus(MemberInfo member) =>
        member.GetCustomAttributesData().Any(data =>
            data.AttributeType == typeof(TraitAttribute)
            && data.ConstructorArguments.Count == 2
            && data.ConstructorArguments[0].Value as string == "Category"
            && data.ConstructorArguments[1].Value as string == "Corpus");
}
