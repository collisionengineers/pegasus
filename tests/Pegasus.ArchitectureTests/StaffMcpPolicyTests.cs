using System.Reflection;
using Pegasus.Core;

namespace Pegasus.ArchitectureTests;

public sealed class StaffMcpPolicyTests
{
    private static readonly string[] ApprovedToolNames =
    [
        "pegasus_case_workflow_get",
        "pegasus_document_download",
        "pegasus_document_export",
        "pegasus_document_upload",
        "pegasus_eva_handoff_generate",
        "pegasus_eva_handoff_get",
        "pegasus_inspection_address_get",
        "pegasus_inspection_address_resolve",
        "pegasus_intake_accept",
        "pegasus_intake_get",
        "pegasus_intake_list",
        "pegasus_triage_get",
        "pegasus_triage_list"
    ];

    private static readonly string[] ForbiddenToolNameFragments =
    [
        "admin",
        "account",
        "role",
        "credential",
        "oauth",
        "cloud",
        "deploy",
        "delete",
        "mailbox"
    ];

    [Fact]
    public void StaffMcpExposesOnlyTheApprovedCoreBackedToolMap()
    {
        var toolTypes = StaffMcpToolTypes();
        var tools = toolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Select(method => (Method: method, Attribute: ToolAttribute(method)))
            .Where(tool => tool.Attribute is not null)
            .Select(tool => (tool.Method, Attribute: tool.Attribute!))
            .ToArray();

        Assert.Equal(
            ApprovedToolNames,
            tools.Select(tool => NamedString(tool.Attribute, "Name"))
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotContain(tools, tool =>
        {
            var name = NamedString(tool.Attribute, "Name");
            return ForbiddenToolNameFragments
                .Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        });

        var coreAssembly = typeof(CoreAssembly).Assembly;
        var webAssembly = typeof(Program).Assembly;
        foreach (var dependency in toolTypes
                     .SelectMany(type => Assert.Single(type.GetConstructors(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)).GetParameters())
                     .Select(parameter => parameter.ParameterType))
        {
            Assert.True(
                dependency.Assembly == coreAssembly
                || dependency.Assembly == webAssembly
                || dependency.FullName == "Microsoft.AspNetCore.Http.IHttpContextAccessor",
                $"MCP tool dependency '{dependency.FullName}' bypasses the Core/Web security boundary.");
        }
    }

    [Fact]
    public void StaffMcpMutationContractsAreIdempotentBoundedAndNeverDestructive()
    {
        var tools = StaffMcpToolTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Select(method => (Method: method, Attribute: ToolAttribute(method)))
            .Where(tool => tool.Attribute is not null)
            .Select(tool => (tool.Method, Attribute: tool.Attribute!))
            .ToArray();

        Assert.All(tools, tool =>
        {
            Assert.True(NamedBoolean(tool.Attribute, "Idempotent"));
            Assert.False(NamedBoolean(tool.Attribute, "Destructive"));
            Assert.False(NamedBoolean(tool.Attribute, "OpenWorld"));
            Assert.Equal(typeof(CancellationToken), tool.Method.GetParameters()[^1].ParameterType);

            if (!NamedBoolean(tool.Attribute, "ReadOnly"))
            {
                var operationId = Assert.Single(
                    tool.Method.GetParameters(),
                    parameter => parameter.Name == "operationId");
                Assert.Equal(typeof(Guid), operationId.ParameterType);
            }
        });
    }

    private static Type[] StaffMcpToolTypes() => typeof(Program).Assembly.GetTypes()
        .Where(type => type.GetCustomAttributesData().Any(attribute =>
            attribute.AttributeType.FullName ==
            "ModelContextProtocol.Server.McpServerToolTypeAttribute"))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    private static CustomAttributeData? ToolAttribute(MethodInfo method) =>
        method.GetCustomAttributesData().SingleOrDefault(attribute =>
            attribute.AttributeType.FullName ==
            "ModelContextProtocol.Server.McpServerToolAttribute");

    private static string NamedString(CustomAttributeData attribute, string name) =>
        (string)attribute.NamedArguments.Single(argument => argument.MemberName == name)
            .TypedValue.Value!;

    private static bool NamedBoolean(CustomAttributeData attribute, string name) =>
        (bool)attribute.NamedArguments.Single(argument => argument.MemberName == name)
            .TypedValue.Value!;
}
