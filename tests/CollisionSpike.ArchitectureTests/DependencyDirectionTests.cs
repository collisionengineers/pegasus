using System.Reflection;
using System.Xml.Linq;
using CollisionSpike.Core;

namespace CollisionSpike.ArchitectureTests;

public sealed class DependencyDirectionTests
{
    private static readonly string[] ForbiddenCoreDependencyPrefixes =
    [
        "Azure.",
        "Box.",
        "Microsoft.AspNetCore.",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Graph"
    ];

    [Fact]
    public void CoreHasNoInfrastructureOrHostDependencies()
    {
        var references = typeof(CoreAssembly).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => IsForbiddenCoreDependency(reference.Name ?? string.Empty));
    }

    [Theory]
    [InlineData("Azure.Storage.Blobs", true)]
    [InlineData("Microsoft.EntityFrameworkCore", true)]
    [InlineData("System.Collections", false)]
    public void CoreDependencyGuardDetectsForbiddenAndAllowedExamples(string assemblyName, bool expected)
    {
        Assert.Equal(expected, IsForbiddenCoreDependency(assemblyName));
    }

    [Fact]
    public void ProjectReferencesFollowTheModularMonolithDirection()
    {
        var root = FindRepositoryRoot();

        Assert.Empty(ProjectReferences(root, "src/CollisionSpike.Core/CollisionSpike.Core.csproj"));
        Assert.Equal(
            ["CollisionSpike.Core"],
            ProjectReferences(root, "src/CollisionSpike.Infrastructure/CollisionSpike.Infrastructure.csproj"));
        Assert.Equal(
            ["CollisionSpike.Core", "CollisionSpike.Infrastructure"],
            ProjectReferences(root, "src/CollisionSpike.Web/CollisionSpike.Web.csproj"));
        Assert.Equal(
            ["CollisionSpike.Core", "CollisionSpike.Infrastructure"],
            ProjectReferences(root, "src/CollisionSpike.Worker/CollisionSpike.Worker.csproj"));
    }

    private static bool IsForbiddenCoreDependency(string assemblyName) =>
        ForbiddenCoreDependencyPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal));

    private static string[] ProjectReferences(string root, string relativeProjectPath)
    {
        var document = XDocument.Load(Path.Combine(root, relativeProjectPath));

        return document
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include")))
            .Where(name => name is not null)
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CollisionSpike.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate CollisionSpike.slnx.");
    }
}
