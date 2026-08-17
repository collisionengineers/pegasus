namespace Pegasus.ArchitectureTests;

/// <summary>Reflection the composition assertions share.</summary>
internal static class TypeInspection
{
    /// <summary>The parameter types of a type's only public constructor — what composition must supply.</summary>
    internal static Type[] OnlyConstructorParameterTypes(Type type) =>
        Assert.Single(type.GetConstructors())
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
}
