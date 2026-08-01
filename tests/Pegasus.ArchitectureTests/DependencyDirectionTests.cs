using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Pegasus.Core;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Custody;
using Pegasus.Worker.Functions;
using Pegasus.Core.Documents;
using Pegasus.Web.Pages.Cases;
using Pegasus.Web.Pages.Uploads;
using Pegasus.Core.ReferenceData;
using Pegasus.Infrastructure;

namespace Pegasus.ArchitectureTests;

public sealed class DependencyDirectionTests
{
    private static readonly string[] ForbiddenCoreDependencyPrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Azure",
        "Microsoft.Graph",
        "Box",
        "MimeKit",
        "DocumentFormat.OpenXml",
        "UglyToad.PdfPig",
        "Microsoft.Data.SqlClient",
        "System.Net.Http",
        "Pegasus.Infrastructure",
        "Pegasus.Web",
        "Pegasus.Worker"
    ];

    [Fact]
    public void CoreHasNoInfrastructureOrHostDependencies()
    {
        var references = typeof(CoreAssembly).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => IsForbiddenCoreDependency(reference.Name ?? string.Empty));
    }

    [Theory]
    [InlineData("Azure.Storage.Blobs", true)]
    [InlineData("Microsoft.AspNetCore", true)]
    [InlineData("Microsoft.AspNetCore.Mvc", true)]
    [InlineData("Microsoft.EntityFrameworkCore", true)]
    [InlineData("Microsoft.Graph", true)]
    [InlineData("Box.V2", true)]
    [InlineData("DocumentFormat.OpenXml", true)]
    [InlineData("MimeKit", true)]
    [InlineData("UglyToad.PdfPig", true)]
    [InlineData("Microsoft.Data.SqlClient", true)]
    [InlineData("System.Net.Http", true)]
    [InlineData("Pegasus.Infrastructure", true)]
    [InlineData("Pegasus.Web", true)]
    [InlineData("Pegasus.Worker", true)]
    [InlineData("Azureish.Storage", false)]
    [InlineData("Microsoft.AspNetCorey", false)]
    [InlineData("Microsoft.EntityFrameworkCoreExtensions", false)]
    [InlineData("Microsoft.Graphical", false)]
    [InlineData("Boxed", false)]
    [InlineData("MimeKitten", false)]
    [InlineData("UglyToad.PdfPigment", false)]
    [InlineData("Microsoft.Data.SqlClientFactory", false)]
    [InlineData("System.Net.Httpish", false)]
    [InlineData("System.Collections", false)]
    public void CoreDependencyGuardDetectsForbiddenAndAllowedExamples(string assemblyName, bool expected)
    {
        Assert.Equal(expected, IsForbiddenCoreDependency(assemblyName));
    }

    [Fact]
    public void CoreProjectHasNoForbiddenDirectDependencies()
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, "src/Pegasus.Core/Pegasus.Core.csproj"));

        Assert.Empty(ForbiddenDirectDependencies(document));
    }

    [Fact]
    public void CoreDirectDependencyGuardDetectsForbiddenAndAllowedFixtures()
    {
        var document = XDocument.Parse(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Azure.Storage.Blobs" Version="1.0.0" />
                <PackageReference Update="MimeKit" Version="1.0.0" />
                <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="1.0.0" />
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <Reference Include="System.Net.Http" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal(
            ["Azure.Storage.Blobs", "Microsoft.AspNetCore.App", "MimeKit", "System.Net.Http"],
            ForbiddenDirectDependencies(document));
    }

    [Fact]
    public void ProjectReferencesFollowTheModularMonolithDirection()
    {
        var root = FindRepositoryRoot();

        Assert.Empty(ProjectReferences(root, "src/Pegasus.Core/Pegasus.Core.csproj"));
        Assert.Equal(
            ["Pegasus.Core"],
            ProjectReferences(root, "src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj"));
        Assert.Equal(
            ["Pegasus.Core", "Pegasus.Infrastructure"],
            ProjectReferences(root, "src/Pegasus.Web/Pegasus.Web.csproj"));
        Assert.Equal(
            ["Pegasus.Core", "Pegasus.Infrastructure"],
            ProjectReferences(root, "src/Pegasus.Worker/Pegasus.Worker.csproj"));
    }

    [Fact]
    public void ApplicationSolutionExcludesSourceWorkspaces()
    {
        var root = FindRepositoryRoot();
        var solution = XDocument.Load(Path.Combine(root, "Pegasus.slnx"));
        var projectPaths = solution
            .Descendants("Project")
            .Select(element => (string?)element.Attribute("Path"))
            .Where(path => path is not null)
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "src/Pegasus.Core/Pegasus.Core.csproj",
                "src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj",
                "src/Pegasus.Web/Pegasus.Web.csproj",
                "src/Pegasus.Worker/Pegasus.Worker.csproj",
                "tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj",
                "tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj",
                "tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj"
            ],
            projectPaths);
        Assert.DoesNotContain(projectPaths, path =>
            path.StartsWith("workspaces/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplicationProjectsDoNotReferenceSourceWorkspaces()
    {
        var root = FindRepositoryRoot();
        var solution = XDocument.Load(Path.Combine(root, "Pegasus.slnx"));

        foreach (var projectPath in solution
            .Descendants("Project")
            .Select(element => (string?)element.Attribute("Path"))
            .Where(path => path is not null)
            .Cast<string>())
        {
            var project = XDocument.Load(Path.Combine(root, projectPath));
            Assert.DoesNotContain(
                project.Descendants("ProjectReference"),
                reference => ((string?)reference.Attribute("Include"))?
                    .Contains("workspaces", StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    [Fact]
    public void IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary()
    {
        var constructor = Assert.Single(typeof(ProcessIntake).GetConstructors());
        var parameters = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Contains(typeof(IIntakeSourceReader), parameters);
        Assert.Contains(typeof(IIntakeReceiptStore), parameters);
        Assert.Contains(typeof(IIntakeArtifactStore), parameters);
        Assert.Contains(typeof(IInstructionExtractionPolicy), parameters);
        Assert.DoesNotContain(typeof(QdosInstructionExtractionPolicy), parameters);

        var implementations = typeof(CoreAssembly).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .ToArray();
        Assert.Equal([typeof(QdosInstructionExtractionPolicy)], implementations);
    }

    [Fact]
    public void ProviderReferenceRuntimeKeepsWorkbookAuthoringOutsideApplicationProjects()
    {
        var root = FindRepositoryRoot();
        var runtimeProjects = new[]
        {
            "src/Pegasus.Core/Pegasus.Core.csproj",
            "src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj",
            "src/Pegasus.Web/Pegasus.Web.csproj",
            "src/Pegasus.Worker/Pegasus.Worker.csproj"
        };

        foreach (var project in runtimeProjects)
        {
            var document = XDocument.Load(Path.Combine(root, project));
            Assert.DoesNotContain(
                document.Descendants().Select(element => (string?)element.Attribute("Include")),
                include => include is not null &&
                    (include.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                     include.EndsWith(".py", StringComparison.OrdinalIgnoreCase) ||
                     include.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)));
        }

        var resources = typeof(InfrastructureAssembly).Assembly.GetManifestResourceNames();
        Assert.Contains(
            "Pegasus.Infrastructure.Persistence.ReferenceData.provider-domains.v1.json",
            resources);
        Assert.DoesNotContain(resources, name => name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProviderReferenceCatalogBoundaryHasOneInfrastructureImplementation()
    {
        Assert.Equal(typeof(CoreAssembly).Assembly, typeof(IProviderReferenceCatalog).Assembly);

        var implementations = typeof(InfrastructureAssembly).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IProviderReferenceCatalog).IsAssignableFrom(type))
            .ToArray();
        var implementation = Assert.Single(implementations);
        Assert.False(implementation.IsPublic);
        Assert.Equal("EfProviderReferenceCatalog", implementation.Name);
    }

    [Fact]
    public async Task ExternalWorkFunctionsRouteOnlyValidIdentifiersToOwningPorts()
    {
        var processor = new RecordingCustodyProcessor();
        var workStore = new RecordingExternalWorkStore();
        var workId = Guid.NewGuid();

        await new ExternalWorkFunction(processor).RunAsync(workId.ToString("D"), CancellationToken.None);
        var poisonReconciler = new ReconcilePoisonedQueueWork(
            null!,
            new ReconcilePoisonedExternalWork(workStore, TimeProvider.System));
        await new ExternalPoisonFunction(poisonReconciler)
            .RunAsync(workId.ToString("N"), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ExternalWorkFunction(processor).RunAsync("not-a-work-id", CancellationToken.None));

        Assert.Equal([workId], processor.ProcessedIds);
        Assert.Equal([workId], workStore.PoisonedIds);
    }

    private sealed class RecordingCustodyProcessor : IProcessQueuedExternalWork
    {
        public List<Guid> ProcessedIds { get; } = [];

        public Task ExecuteAsync(Guid workId, CancellationToken cancellationToken)
        {
            ProcessedIds.Add(workId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingExternalWorkStore : IExternalWorkStore
    {
        public List<Guid> PoisonedIds { get; } = [];

        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExternalWorkDispatchClaim?>(null);

        public Task MarkDispatchedAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dispatchedAtUtc,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReleaseDispatchAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task MarkPoisonedAsync(
            Guid workItemId,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken)
        {
            PoisonedIds.Add(workItemId);
            return Task.CompletedTask;
        }

        public Task FailProcessingAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset failedAtUtc,
            string failureCode,
            string failureReason,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [Fact]
    public void WebCustodialPagesHaveNoDormantTransportPath()
    {
        var casePageDependencies = Assert.Single(typeof(DetailsModel).GetConstructors())
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        var requestPageDependencies = Assert.Single(typeof(RequestModel).GetConstructors())
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(typeof(IAddCaseDocument), casePageDependencies);
        Assert.Contains(typeof(IGetCase), casePageDependencies);
        Assert.Contains(typeof(ICreateRequestUploadLink), casePageDependencies);
        Assert.Contains(typeof(IRevokeRequestUploadLink), casePageDependencies);
        Assert.Contains(typeof(IGetRequestUpload), requestPageDependencies);
        Assert.Contains(typeof(IUploadToRequest), requestPageDependencies);
    }

    [Fact]
    public void WorkerFunctionAppUsesItsAssignedIdentityForKeyVaultReferences()
    {
        var platformBicep = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "infra",
            "modules",
            "platform.bicep"));
        var workerApp = Regex.Match(
            platformBicep,
            @"resource\s+workerApp\b[\s\S]*?(?=\r?\nresource\s+webHttp5xxAlert\b)",
            RegexOptions.CultureInvariant);

        Assert.True(workerApp.Success, "The Worker Function App resource was not found.");
        Assert.Matches(
            @"identity:\s*\{[\s\S]*?userAssignedIdentities:\s*\{\s*'\$\{workerIdentity\.id\}':\s*\{\s*\}\s*\}",
            workerApp.Value);
        Assert.Matches(
            @"keyVaultReferenceIdentity:\s*workerIdentity\.id",
            workerApp.Value);
    }

    [Fact]
    public void TransientIntakeTagLifecycleUsesContainerScopedBlobDataOwner()
    {
        var platformBicep = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "infra",
            "modules",
            "platform.bicep"));

        foreach (var resourceName in new[]
                 {
                     "workerTransientCustodyOwner",
                     "webTransientCustodyOwner"
                 })
        {
            var assignment = Regex.Match(
                platformBicep,
                $@"resource\s+{resourceName}\b[\s\S]*?\n\}}",
                RegexOptions.CultureInvariant);

            Assert.True(assignment.Success, $"{resourceName} was not found.");
            Assert.Contains("scope: transientIntakeContainer", assignment.Value);
            Assert.Contains("roleDefinitionId: blobDataOwnerRole", assignment.Value);
            Assert.DoesNotContain("blobDataContributorRole", assignment.Value);
        }
    }

    private static bool IsForbiddenCoreDependency(string assemblyName) =>
        ForbiddenCoreDependencyPrefixes.Any(prefix =>
            assemblyName.Equals(prefix, StringComparison.Ordinal) ||
            assemblyName.StartsWith($"{prefix}.", StringComparison.Ordinal));

    private static string[] ForbiddenDirectDependencies(XDocument document) =>
        document
            .Descendants()
            .Where(element => element.Name.LocalName is
                "PackageReference" or "FrameworkReference" or "Reference")
            .Select(element =>
                (string?)element.Attribute("Include") ??
                (string?)element.Attribute("Update") ??
                $"(unnamed {element.Name.LocalName})")
            .Where(IsForbiddenCoreDependency)
            .Order(StringComparer.Ordinal)
            .ToArray();

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

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate Pegasus.slnx.");
    }
}
