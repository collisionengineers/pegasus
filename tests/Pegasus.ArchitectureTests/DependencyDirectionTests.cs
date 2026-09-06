using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Pegasus.Core;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Worker;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Web.Pages.Cases;
using Pegasus.Web.Pages.Uploads;
using Pegasus.Core.ReferenceData;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Transport;
using Pegasus.Web.Pages;

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
        "OpenIddict",
        "ModelContextProtocol",
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
    public void ReportRenderingHasOneCorePortAndOneInfrastructureAdapter()
    {
        var core = typeof(Pegasus.Core.Reports.IAssessmentReportRenderer).Assembly;
        var infrastructure = typeof(Pegasus.Infrastructure.DependencyInjection).Assembly;
        var port = typeof(Pegasus.Core.Reports.IAssessmentReportRenderer);

        Assert.DoesNotContain(core.GetReferencedAssemblies(), x =>
            x.Name is "Scriban" or "Microsoft.Playwright" or "PdfSharp");
        Assert.Single(infrastructure.GetTypes(), type =>
            !type.IsAbstract && port.IsAssignableFrom(type));
        Assert.DoesNotContain(infrastructure.GetTypes(), type =>
            type.FullName?.Contains("CollisionRenderer", StringComparison.Ordinal) == true);
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

        // One policy per instruction profile, so the SET is not frozen - INTK-060
        // C03 adds fourteen more beside QDOS. What must hold is where they live
        // and what reaches them: every implementation is Core's, none is
        // duplicated in Infrastructure, and the orchestrator above depends on
        // the interface rather than on any of them.
        var implementations = typeof(CoreAssembly).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .ToArray();
        Assert.Contains(typeof(QdosInstructionExtractionPolicy), implementations);
        Assert.Contains(typeof(PchInstructionExtractionPolicy), implementations);
        Assert.All(implementations, type =>
            Assert.Equal("Pegasus.Core.Intake", type.Namespace));
        Assert.DoesNotContain(
            typeof(InfrastructureAssembly).Assembly.GetTypes(),
            type => !type.IsAbstract && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type));
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
    public async Task UnifiedWorkFunctionsRouteTypedIdentifiersToOwningPorts()
    {
        var intakeProcessor = new RecordingIntakeProcessor();
        var processor = new RecordingCustodyProcessor();
        var workStore = new RecordingExternalWorkStore();
        var intakeId = Guid.NewGuid();
        var workId = Guid.NewGuid();

        await new UnifiedWorkFunction(intakeProcessor, processor, null!, null!, TimeProvider.System).RunAsync(
            UnifiedWorkQueueMessage.Format(UnifiedWorkQueueKind.Intake, intakeId),
            CancellationToken.None);
        await new UnifiedWorkFunction(intakeProcessor, processor, null!, null!, TimeProvider.System).RunAsync(
            UnifiedWorkQueueMessage.Format(UnifiedWorkQueueKind.External, workId),
            CancellationToken.None);
        var poisonReconciler = new ReconcilePoisonedQueueWork(
            null!,
            new ReconcilePoisonedExternalWork(workStore, TimeProvider.System));
        await new UnifiedWorkPoisonFunction(poisonReconciler, null!, TimeProvider.System)
            .RunAsync(UnifiedWorkQueueMessage.Format(UnifiedWorkQueueKind.External, workId), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new UnifiedWorkFunction(intakeProcessor, processor, null!, null!, TimeProvider.System).RunAsync("not-a-work-id", CancellationToken.None));

        Assert.Equal([intakeId], intakeProcessor.ProcessedIds);
        Assert.Equal([workId], processor.ProcessedIds);
        Assert.Equal([workId], workStore.PoisonedIds);
    }

    [Fact]
    public void UnifiedWorkQueueCarriesCanonicalMailboxWakeIdentifiers()
    {
        var mailboxId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var message = UnifiedWorkQueueMessage.FormatMailbox(
            mailboxId,
            subscriptionId,
            MailboxWakeKind.SubscriptionRemoved);

        Assert.True(UnifiedWorkQueueMessage.TryParseMailbox(
            message,
            out var parsedMailboxId,
            out var parsedSubscriptionId,
            out var parsedKind));
        Assert.Equal(mailboxId, parsedMailboxId);
        Assert.Equal(subscriptionId, parsedSubscriptionId);
        Assert.Equal(MailboxWakeKind.SubscriptionRemoved, parsedKind);
        Assert.False(UnifiedWorkQueueMessage.TryParseMailbox(
            message.ToUpperInvariant(),
            out _,
            out _,
            out _));
    }

    private sealed class RecordingIntakeProcessor : IProcessQueuedIntake
    {
        public List<Guid> ProcessedIds { get; } = [];

        public Task<QueuedIntakeProcessingOutcome> ExecuteAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken)
        {
            ProcessedIds.Add(stagedReceiptId);
            return Task.FromResult(default(QueuedIntakeProcessingOutcome));
        }
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

        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
            Guid workItemId,
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

        public Task<bool> HoldsProcessingLeaseAsync(
            Guid workItemId,
            string leaseToken,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

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
        var casePageDependencies = TypeInspection.OnlyConstructorParameterTypes(typeof(DetailsModel));
        var custodyPageDependencies = TypeInspection.OnlyConstructorParameterTypes(typeof(CustodyModel));
        var requestPageDependencies = TypeInspection.OnlyConstructorParameterTypes(typeof(RequestModel));

        Assert.Contains(typeof(IGetCase), casePageDependencies);
        Assert.Contains(typeof(ICreateRequestUploadLink), custodyPageDependencies);
        Assert.Contains(typeof(IRevokeRequestUploadLink), custodyPageDependencies);
        Assert.Contains(typeof(IGetRequestUpload), requestPageDependencies);
        Assert.Contains(typeof(IUploadToRequest), requestPageDependencies);
    }

    [Fact]
    public void WebPagesHaveOneStaffActorAndOperationKeyOwnerPerConcept()
    {
        var pagesRoot = Path.Combine(FindRepositoryRoot(), "src", "Pegasus.Web", "Pages");
        var sources = Directory
            .EnumerateFiles(pagesRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = Path.GetRelativePath(pagesRoot, path).Replace('\\', '/'),
                Content = File.ReadAllText(path)
            })
            .ToArray();

        Assert.Equal(
            ["StaffPageModel.cs"],
            sources
                .Where(source => source.Content.Contains(
                    "StaffActorFactory.TryCreate",
                    StringComparison.Ordinal))
                .Select(source => source.Path)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            ["StaffPageModel.cs", "Upload.cshtml.cs"],
            sources
                .SelectMany(source => Regex
                    .Matches(source.Content, "Guid\\.NewGuid\\(\\)\\.ToString\\(\"N\"\\)")
                    .Select(_ => source.Path))
                .Order(StringComparer.Ordinal));
        Assert.NotNull(typeof(RequestModel).GetCustomAttribute<
            Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
        Assert.False(typeof(StaffPageModel).IsAssignableFrom(typeof(RequestModel)));
    }

    [Fact]
    public void CustodyAndEvaPoliciesHaveOneCoreOwnerAndAdaptersRemainAtBoundaries()
    {
        // ENG-016: the EVA hand-off use cases and the policy-authority
        // capability they carried into persistence are gone with the act.
        // The export reaches Core policy directly, so what has to hold now is
        // that the port and the policy are Core's and the store is not.
        Assert.Equal(typeof(IRetryCaseCustody).Assembly, typeof(RetryCaseCustody).Assembly);
        Assert.Equal(typeof(IExportCaseBundle).Assembly, typeof(EvaHandoffPolicy).Assembly);
        Assert.Equal(typeof(IEvaHandoffProxy).Assembly, typeof(EvaHandoffPolicy).Assembly);
        Assert.NotEqual(typeof(IExportCaseBundle).Assembly, typeof(EvaHandoffStore).Assembly);

        Assert.Equal(typeof(DependencyInjection).Assembly, typeof(EvaHandoffStore).Assembly);
        var boxAdapter = Assert.Single(
            typeof(DependencyInjection).Assembly.GetTypes(),
            type => type.FullName == "Pegasus.Infrastructure.Custody.BoxCaseCustody");
        Assert.Equal(typeof(DependencyInjection).Assembly, boxAdapter.Assembly);
        Assert.Contains(typeof(IExportCaseBundle), typeof(EvaHandoffStore).GetInterfaces());
        Assert.Contains(typeof(ICaseCustody), boxAdapter.GetInterfaces());
        Assert.Contains(
            typeof(ICustodyRecoveryPersistence).GetMethod("RetryAsync")!.GetParameters(),
            parameter => parameter.ParameterType == typeof(CustodyRetryPolicyAuthority));
        Assert.Empty(typeof(CustodyRetryPolicyAuthority).GetConstructors());

        var repositoryRoot = FindRepositoryRoot();
        var evaPersistence = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Pegasus.Infrastructure",
            "Persistence",
            "EvaHandoffStore.cs"));
        var evaImageReader = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Pegasus.Infrastructure",
            "Persistence",
            "EvaCaseImageReader.cs"));
        var evaMapping = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Pegasus.Core",
            "Eva",
            "CaseEvaMapping.cs"));
        var webComposition = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Pegasus.Web",
            "Program.cs"));
        var platform = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "infra",
            "modules",
            "platform.bicep"));
        var custodyPersistence = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Pegasus.Infrastructure",
            "Persistence",
            "EfExternalWorkStore.cs"));
        // Image eligibility and the field mapping stay Core's; the store
        // calls them and owns neither.
        // Image eligibility is selected in exactly one place, and it is the
        // Core policy that selects it. EXT-04 gave the case a second route to
        // EVA, so the reader moved out of the export store to be shared by
        // both; what must not happen is a second copy of the rule.
        Assert.Contains(
            "EvaHandoffPolicy.SelectEligibleImages",
            evaImageReader,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "EvaHandoffPolicy.SelectEligibleImages",
            evaPersistence,
            StringComparison.Ordinal);
        Assert.Contains(
            "CaseEvaMapping.MapForOperatorExport",
            evaPersistence,
            StringComparison.Ordinal);
        Assert.DoesNotContain("EvaMappingAcceptance", evaMapping, StringComparison.Ordinal);
        Assert.DoesNotContain("EVA hand-off is not switched on", evaMapping, StringComparison.Ordinal);
        Assert.DoesNotContain("Eva:AcceptedMapping", webComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("Eva__AcceptedMapping", platform, StringComparison.Ordinal);
        // PLAT-041: a case's photographs are read in one batch, not one call
        // per image, because a remote store resolves the case folder per call.
        // The read moved into the shared reader with EXT-04; the rule is the
        // same and now covers both EVA routes at once.
        Assert.Contains(
            "contentStore.ReadVersionsAsync",
            evaImageReader,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "contentStore.OpenReadVersionAsync",
            evaImageReader,
            StringComparison.Ordinal);
        // The once-per-case proxy is a Core port the store calls, never an
        // adapter the store manufactures.
        Assert.Contains(
            "proxy.RecordFirstGenerationAsync",
            evaPersistence,
            StringComparison.Ordinal);
        Assert.Contains("policy.Decide", custodyPersistence, StringComparison.Ordinal);
        Assert.DoesNotContain(
            typeof(EvaHandoffStore).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "Pegasus.Web" or "Pegasus.Worker");
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
            .Select(element => (string?)element.Attribute("Include"))
            // MSBuild Include paths use backslashes, which are not path separators
            // on Linux; normalize before taking the file name so both platforms agree.
            .Select(include => Path.GetFileNameWithoutExtension(include?.Replace('\\', '/')))
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
