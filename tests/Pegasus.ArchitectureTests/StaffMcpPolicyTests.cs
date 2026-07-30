using System.Reflection;
using ModelContextProtocol.Server;
using Pegasus.Web.Mcp;

namespace Pegasus.ArchitectureTests;

public sealed class StaffMcpPolicyTests
{
    private static readonly ExpectedTool[] Expected =
    [
        Read("operations.get", "GetOperationsSnapshot"),
        Read("intake.list", "ListIntake"),
        Read("intake.get", "GetIntake"),
        Read("cases.search", "SearchCases"),
        Read("cases.get", "GetCase"),
        Read("triage.list", "ListTriage"),
        Read("triage.get", "GetTriage"),

        Named("intake.resolve", "ResolveIntake"),
        Named("intake.reevaluate", "ReevaluateIntake"),
        Named("cases.save", "SaveCase"),
        Named("cases.acquire_edit_lease", "AcquireCaseEditLease"),
        Named("cases.renew_edit_lease", "RenewCaseEditLease"),
        Named("cases.release_edit_lease", "ReleaseCaseEditLease"),
        Named("cases.create_task", "CreateCaseTask"),
        Named("cases.assign_task", "AssignCaseTask"),
        Named("triage.assign", "AssignTriage"),
        Named("triage.unassign", "UnassignTriage"),
        Named("triage.record_finding", "RecordTriageFinding"),
        Named("triage.supersede_finding", "SupersedeTriageFinding"),
        Named("triage.link_response", "LinkTriageResponseEvidence"),
        Named("triage.unlink_response", "UnlinkTriageResponseEvidence"),
        Named("triage.link_case", "LinkTriageCase"),
        Named("triage.unlink_case", "UnlinkTriageCase"),

        Consequential("intake.accept", "AcceptIntake"),
        Consequential("intake.link_case", "LinkIntake"),
        Consequential("intake.unlink_case", "ReverseIntakeLink"),
        Consequential("cases.confirm_completeness", "ConfirmCompleteness"),
        Consequential("cases.hold", "HoldCase"),
        Consequential("cases.release_hold", "ReleaseCase"),
        Consequential("cases.transition", "TransitionCase"),
        Consequential("cases.close", "CloseCase"),
        Consequential("cases.reopen", "ReopenCase"),
        Consequential("cases.archive", "ArchiveCase"),
        Consequential("cases.create_linked_replacement", "CreateLinkedReplacement"),
        Consequential("cases.complete_task", "CompleteCaseTask"),
        Consequential("cases.cancel_task", "CancelCaseTask"),
        Consequential("cases.record_engineer_finding", "RecordEngineerFinding"),
        Consequential("triage.complete", "CompleteTriage"),
        Consequential("triage.cancel", "CancelTriage"),
        Consequential("triage.reopen", "ReopenTriage"),
        Consequential("documents.logical_remove", "LogicallyRemoveDocument"),

        DocumentDownload("documents.download", "DownloadCaseDocument"),
        DocumentExport("documents.export", "ExportCaseDocuments"),

        External("requests.create_box", "CreateBoxFileRequest"),
        External("requests.revoke_box", "RevokeBoxFileRequest"),
        External("requests.create_upload", "CreateRequestUploadLink"),
        External("requests.revoke_upload", "RevokeRequestUploadLink"),
        External("vehicle.request_lookup", "RequestVehicleLookup"),
        External("vehicle.accept_suggestion", "AcceptVehicleSuggestion"),
        External("reports.generate_eva", "GenerateEvaHandoff"),
        External("reports.link_evidence", "LinkReportEvidence"),
        External("reports.unlink_evidence", "UnlinkReportEvidence")
    ];

    [Fact]
    public void AlphaManifestMatchesTheExactOrderedPlanContract()
    {
        var actual = AlphaMcpToolManifest.Tools
            .Select(tool => new ExpectedTool(
                tool.Name,
                tool.CoreContractName,
                tool.Scope,
                tool.Policy,
                tool.Hints.ReadOnly,
                tool.Hints.Destructive,
                tool.Hints.Idempotent,
                tool.Hints.OpenWorld))
            .ToArray();

        Assert.Equal(Expected, actual);
        Assert.Equal(52, actual.Length);
        Assert.Equal(
            actual.Length,
            AlphaMcpToolManifest.ToolTypes.Distinct().Count());
        Assert.Equal(
            actual.Length,
            AlphaMcpToolManifest.Tools.Select(tool => tool.AdapterMethod).Distinct().Count());
    }

    [Fact]
    public void EveryAttributedToolIsManifestedAndEveryAdapterHasOneSoleCoreContract()
    {
        var assembly = typeof(AlphaMcpToolManifest).Assembly;
        var attributedTypes = assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var manifestedTypes = AlphaMcpToolManifest.ToolTypes
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(manifestedTypes, attributedTypes);
        foreach (var descriptor in AlphaMcpToolManifest.Tools)
        {
            var methods = descriptor.AdapterType
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .ToArray();
            Assert.Equal(descriptor.AdapterMethod, Assert.Single(methods));

            var constructor = Assert.Single(descriptor.AdapterType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            var coreDependencies = constructor.GetParameters()
                .Where(parameter => parameter.ParameterType.Assembly == typeof(Pegasus.Core.Identity.ActionActor).Assembly)
                .ToArray();
            Assert.Equal(descriptor.CoreContract, Assert.Single(coreDependencies).ParameterType);
        }
    }

    [Fact]
    public void ManifestSchemasDeriveAuthorizationAndKeepMutationPreconditionsExplicit()
    {
        foreach (var descriptor in AlphaMcpToolManifest.Tools)
        {
            var schemaParameters = descriptor.Schema.Parameters;
            Assert.DoesNotContain(schemaParameters, parameter =>
                parameter.ParameterType == typeof(Pegasus.Core.Identity.ActionActor)
                || parameter.Name.Contains("actor", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("role", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("client", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("secret", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(schemaParameters, parameter =>
                parameter.ParameterType == typeof(Stream)
                || parameter.ParameterType == typeof(byte[])
                || parameter.ParameterType == typeof(ReadOnlyMemory<byte>));

            if (!descriptor.Hints.ReadOnly)
            {
                Assert.Contains(schemaParameters, parameter =>
                    parameter.Name is "operationId" or "operationKey");
            }

            Assert.DoesNotContain(
                EnumerateTypeGraph(descriptor.Schema.OutputType),
                type => type.Name.Contains("Secret", StringComparison.Ordinal)
                    || typeof(Stream).IsAssignableFrom(type)
                    || type == typeof(byte[])
                    || type == typeof(ReadOnlyMemory<byte>));
        }
    }

    [Fact]
    public void ExcludedAndLegacySurfacesAreNotManifested()
    {
        var names = AlphaMcpToolManifest.Tools.Select(tool => tool.Name).ToArray();
        Assert.DoesNotContain(names, name => name.StartsWith("pegasus_", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("account", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("principal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("config", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("cloud", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("ai", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("documents.upload", names);
        Assert.DoesNotContain("triage.acquire_edit_lease", names);
    }

    private static IEnumerable<Type> EnumerateTypeGraph(Type root)
    {
        var pending = new Stack<Type>();
        var seen = new HashSet<Type>();
        pending.Push(root);
        while (pending.TryPop(out var type))
        {
            if (!seen.Add(type))
            {
                continue;
            }

            yield return type;
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    pending.Push(argument);
                }
            }
            if (type.Assembly == typeof(AlphaMcpToolManifest).Assembly)
            {
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    pending.Push(property.PropertyType);
                }
            }
        }
    }

    private static ExpectedTool Read(string name, string contract) =>
        new(
            name,
            contract,
            "pegasus.read",
            "current-user record authorization",
            ReadOnly: true,
            Destructive: false,
            Idempotent: true,
            OpenWorld: false);

    private static ExpectedTool Named(string name, string contract) =>
        new(
            name,
            contract,
            "pegasus.write",
            "named command policy and current role",
            ReadOnly: false,
            Destructive: false,
            Idempotent: true,
            OpenWorld: false);

    private static ExpectedTool Consequential(string name, string contract) =>
        new(
            name,
            contract,
            "pegasus.write",
            "consequential-action policy",
            ReadOnly: false,
            Destructive: true,
            Idempotent: true,
            OpenWorld: false);

    private static ExpectedTool DocumentDownload(string name, string contract) =>
        new(
            name,
            contract,
            "pegasus.read",
            "current-user document/case policy, opaque server-selected custody ID only",
            ReadOnly: true,
            Destructive: false,
            Idempotent: true,
            OpenWorld: true);

    private static ExpectedTool DocumentExport(string name, string contract) =>
        new(
            name,
            contract,
            "pegasus.write",
            "current-user document/case policy, same-case selected IDs and recorded export event",
            ReadOnly: false,
            Destructive: false,
            Idempotent: true,
            OpenWorld: true);

    private static ExpectedTool External(string name, string contract) =>
        new(
            name,
            contract,
            "pegasus.write",
            "current-user case policy plus accepted external-adapter/evidence gate",
            ReadOnly: false,
            Destructive: true,
            Idempotent: true,
            OpenWorld: true);

    private sealed record ExpectedTool(
        string Name,
        string CoreContract,
        string Scope,
        string Policy,
        bool ReadOnly,
        bool Destructive,
        bool Idempotent,
        bool OpenWorld);
}
