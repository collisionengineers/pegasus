using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Core.Triage;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal static class AlphaMcpPolicyNames
{
    public const string CurrentUserRecord = "current-user record authorization";
    public const string NamedCommand = "named command policy and current role";
    public const string ConsequentialAction = "consequential-action policy";
    public const string CurrentUserDocument =
        "current-user document/case policy, opaque server-selected custody ID only";
    public const string CurrentUserDocumentExport =
        "current-user document/case policy, same-case selected IDs and recorded export event";
    public const string CurrentUserCaseExternal =
        "current-user case policy plus accepted external-adapter/evidence gate";
}

internal sealed record AlphaMcpToolHints(
    bool ReadOnly,
    bool Destructive,
    bool Idempotent,
    bool OpenWorld);

internal sealed record AlphaMcpParameterSchema(
    string Name,
    Type ParameterType,
    bool IsRequired);

internal sealed record AlphaMcpMethodSchema(
    ImmutableArray<AlphaMcpParameterSchema> Parameters,
    Type OutputType);

internal sealed record AlphaMcpToolDescriptor
{
    public AlphaMcpToolDescriptor(
        string name,
        Type adapterType,
        Type coreContract,
        string scope,
        string policy,
        AlphaMcpToolHints hints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(adapterType);
        ArgumentNullException.ThrowIfNull(coreContract);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        ArgumentNullException.ThrowIfNull(hints);

        Name = name;
        AdapterType = adapterType;
        CoreContract = coreContract;
        Scope = scope;
        Policy = policy;
        Hints = hints;
        AdapterMethod = ResolveAndValidateMethod(adapterType, name, hints);
        Schema = DescribeSchema(AdapterMethod);
        ValidateSoleCoreContract(adapterType, coreContract);
    }

    public string Name { get; }
    public Type AdapterType { get; }
    public MethodInfo AdapterMethod { get; }
    public Type CoreContract { get; }
    public string CoreContractName =>
        CoreContract.Name.StartsWith('I') && CoreContract.Name.Length > 1
            ? CoreContract.Name[1..]
            : CoreContract.Name;
    public string Scope { get; }
    public string Policy { get; }
    public AlphaMcpToolHints Hints { get; }
    public AlphaMcpMethodSchema Schema { get; }

    private static MethodInfo ResolveAndValidateMethod(
        Type adapterType,
        string name,
        AlphaMcpToolHints hints)
    {
        if (adapterType.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
        {
            throw new InvalidOperationException(
                $"MCP adapter '{adapterType.FullName}' is not a tool type.");
        }

        var methods = adapterType
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(item => item.Attribute is not null)
            .ToArray();
        if (methods.Length != 1)
        {
            throw new InvalidOperationException(
                $"MCP adapter '{adapterType.FullName}' must declare exactly one tool method.");
        }

        var (method, attribute) = methods[0];
        if (!string.Equals(attribute!.Name, name, StringComparison.Ordinal)
            || attribute.ReadOnly != hints.ReadOnly
            || attribute.Destructive != hints.Destructive
            || attribute.Idempotent != hints.Idempotent
            || attribute.OpenWorld != hints.OpenWorld
            || !attribute.UseStructuredContent)
        {
            throw new InvalidOperationException(
                $"MCP adapter '{adapterType.FullName}' metadata does not match the alpha manifest.");
        }

        return method;
    }

    private static AlphaMcpMethodSchema DescribeSchema(MethodInfo method)
    {
        var nullability = new NullabilityInfoContext();
        var parameters = method.GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .Select(parameter => new AlphaMcpParameterSchema(
                parameter.Name
                    ?? throw new InvalidOperationException("MCP parameters must have names."),
                parameter.ParameterType,
                !parameter.HasDefaultValue
                    && Nullable.GetUnderlyingType(parameter.ParameterType) is null
                    && (parameter.ParameterType.IsValueType
                        || nullability.Create(parameter).ReadState == NullabilityState.NotNull)))
            .ToImmutableArray();
        var outputType = method.ReturnType.IsGenericType
            && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                ? method.ReturnType.GenericTypeArguments[0]
                : method.ReturnType;
        return new(parameters, outputType);
    }

    private static void ValidateSoleCoreContract(Type adapterType, Type coreContract)
    {
        var constructor = adapterType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"MCP adapter '{adapterType.FullName}' must have one constructor.");
        var coreDependencies = constructor.GetParameters()
            .Where(parameter => parameter.ParameterType.Assembly == typeof(ActionActor).Assembly)
            .ToArray();
        if (coreDependencies.Length != 1 || coreDependencies[0].ParameterType != coreContract)
        {
            throw new InvalidOperationException(
                $"MCP adapter '{adapterType.FullName}' must depend on exactly the declared Core contract.");
        }
    }
}

internal static class AlphaMcpToolManifest
{
    private static readonly AlphaMcpToolHints ReadHints = new(true, false, true, false);
    private static readonly AlphaMcpToolHints NamedCommandHints = new(false, false, true, false);
    private static readonly AlphaMcpToolHints ConsequentialHints = new(false, true, true, false);
    private static readonly AlphaMcpToolHints DocumentDownloadHints = new(true, false, true, true);
    private static readonly AlphaMcpToolHints DocumentExportHints = new(false, false, true, true);
    private static readonly AlphaMcpToolHints ExternalHints = new(false, true, true, true);
    private static readonly Dictionary<string, AlphaMcpToolDescriptor> ByName;

    static AlphaMcpToolManifest()
    {
        Tools =
        [
            Read<OperationsGetMcpTool, IGetOperationsSnapshot>(AlphaMcpToolNames.OperationsGet),
            Read<IntakeListMcpTool, IListIntake>(AlphaMcpToolNames.IntakeList),
            Read<IntakeGetMcpTool, IGetIntake>(AlphaMcpToolNames.IntakeGet),
            Read<CasesSearchMcpTool, ISearchCases>(AlphaMcpToolNames.CasesSearch),
            Read<CasesGetMcpTool, IGetCase>(AlphaMcpToolNames.CasesGet),
            Read<TriageListMcpTool, IListTriage>(AlphaMcpToolNames.TriageList),
            Read<TriageGetMcpTool, IGetTriage>(AlphaMcpToolNames.TriageGet),

            Named<IntakeResolveMcpTool, IResolveIntake>(AlphaMcpToolNames.IntakeResolve),
            Named<IntakeReevaluateMcpTool, IReevaluateIntake>(AlphaMcpToolNames.IntakeReevaluate),
            Named<CasesSaveMcpTool, ISaveCase>(AlphaMcpToolNames.CasesSave),
            Named<CasesAcquireEditLeaseMcpTool, IAcquireCaseEditLease>(AlphaMcpToolNames.CasesAcquireEditLease),
            Named<CasesRenewEditLeaseMcpTool, IRenewCaseEditLease>(AlphaMcpToolNames.CasesRenewEditLease),
            Named<CasesReleaseEditLeaseMcpTool, IReleaseCaseEditLease>(AlphaMcpToolNames.CasesReleaseEditLease),
            Named<CasesCreateTaskMcpTool, ICreateCaseTask>(AlphaMcpToolNames.CasesCreateTask),
            Named<CasesAssignTaskMcpTool, IAssignCaseTask>(AlphaMcpToolNames.CasesAssignTask),
            Named<TriageAssignMcpTool, IAssignTriage>(AlphaMcpToolNames.TriageAssign),
            Named<TriageUnassignMcpTool, IUnassignTriage>(AlphaMcpToolNames.TriageUnassign),
            Named<TriageRecordFindingMcpTool, IRecordTriageFinding>(AlphaMcpToolNames.TriageRecordFinding),
            Named<TriageSupersedeFindingMcpTool, ISupersedeTriageFinding>(AlphaMcpToolNames.TriageSupersedeFinding),
            Named<TriageLinkResponseMcpTool, ILinkTriageResponseEvidence>(AlphaMcpToolNames.TriageLinkResponse),
            Named<TriageUnlinkResponseMcpTool, IUnlinkTriageResponseEvidence>(AlphaMcpToolNames.TriageUnlinkResponse),
            Named<TriageLinkCaseMcpTool, ILinkTriageCase>(AlphaMcpToolNames.TriageLinkCase),
            Named<TriageUnlinkCaseMcpTool, IUnlinkTriageCase>(AlphaMcpToolNames.TriageUnlinkCase),

            Consequential<IntakeAcceptMcpTool, IAcceptIntake>(AlphaMcpToolNames.IntakeAccept),
            Consequential<IntakeLinkCaseMcpTool, ILinkIntake>(AlphaMcpToolNames.IntakeLinkCase),
            Consequential<IntakeUnlinkCaseMcpTool, IReverseIntakeLink>(AlphaMcpToolNames.IntakeUnlinkCase),
            Consequential<CasesConfirmCompletenessMcpTool, IConfirmCompleteness>(AlphaMcpToolNames.CasesConfirmCompleteness),
            Consequential<CasesHoldMcpTool, IHoldCase>(AlphaMcpToolNames.CasesHold),
            Consequential<CasesReleaseHoldMcpTool, IReleaseCase>(AlphaMcpToolNames.CasesReleaseHold),
            Consequential<CasesTransitionMcpTool, ITransitionCase>(AlphaMcpToolNames.CasesTransition),
            Consequential<CasesCloseMcpTool, ICloseCase>(AlphaMcpToolNames.CasesClose),
            Consequential<CasesReopenMcpTool, IReopenCase>(AlphaMcpToolNames.CasesReopen),
            Consequential<CasesArchiveMcpTool, IArchiveCase>(AlphaMcpToolNames.CasesArchive),
            Consequential<CasesCreateLinkedReplacementMcpTool, ICreateLinkedReplacement>(AlphaMcpToolNames.CasesCreateLinkedReplacement),
            Consequential<CasesCompleteTaskMcpTool, ICompleteCaseTask>(AlphaMcpToolNames.CasesCompleteTask),
            Consequential<CasesCancelTaskMcpTool, ICancelCaseTask>(AlphaMcpToolNames.CasesCancelTask),
            Consequential<CasesRecordEngineerFindingMcpTool, IRecordEngineerFinding>(AlphaMcpToolNames.CasesRecordEngineerFinding),
            Consequential<TriageCompleteMcpTool, ICompleteTriage>(AlphaMcpToolNames.TriageComplete),
            Consequential<TriageCancelMcpTool, ICancelTriage>(AlphaMcpToolNames.TriageCancel),
            Consequential<TriageReopenMcpTool, IReopenTriage>(AlphaMcpToolNames.TriageReopen),
            Create<DocumentsLogicalRemoveMcpTool, ILogicallyRemoveDocument>(
                AlphaMcpToolNames.DocumentsLogicalRemove,
                AlphaMcpPolicyNames.ConsequentialAction,
                ConsequentialHints),

            Create<DocumentsDownloadMcpTool, IDownloadCaseDocument>(
                AlphaMcpToolNames.DocumentsDownload,
                AlphaMcpPolicyNames.CurrentUserDocument,
                DocumentDownloadHints,
                StaffMcpPolicies.ReadScope),
            Create<DocumentsExportMcpTool, IExportCaseDocuments>(
                AlphaMcpToolNames.DocumentsExport,
                AlphaMcpPolicyNames.CurrentUserDocumentExport,
                DocumentExportHints),

            External<RequestsCreateBoxMcpTool, ICreateBoxFileRequest>(AlphaMcpToolNames.RequestsCreateBox),
            External<RequestsRevokeBoxMcpTool, IRevokeBoxFileRequest>(AlphaMcpToolNames.RequestsRevokeBox),
            External<RequestsCreateUploadMcpTool, ICreateRequestUploadLink>(AlphaMcpToolNames.RequestsCreateUpload),
            External<RequestsRevokeUploadMcpTool, IRevokeRequestUploadLink>(AlphaMcpToolNames.RequestsRevokeUpload),
            External<VehicleRequestLookupMcpTool, IRequestVehicleLookup>(AlphaMcpToolNames.VehicleRequestLookup),
            External<VehicleAcceptSuggestionMcpTool, IAcceptVehicleSuggestion>(AlphaMcpToolNames.VehicleAcceptSuggestion),
            External<ReportsGenerateEvaMcpTool, IGenerateEvaHandoff>(AlphaMcpToolNames.ReportsGenerateEva),
            External<ReportsLinkEvidenceMcpTool, ILinkReportEvidence>(AlphaMcpToolNames.ReportsLinkEvidence),
            External<ReportsUnlinkEvidenceMcpTool, IUnlinkReportEvidence>(AlphaMcpToolNames.ReportsUnlinkEvidence)
        ];

        if (Tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count() != Tools.Length
            || Tools.Select(tool => tool.AdapterType).Distinct().Count() != Tools.Length
            || Tools.Select(tool => tool.AdapterMethod).Distinct().Count() != Tools.Length)
        {
            throw new InvalidOperationException(
                "The alpha MCP manifest contains a duplicate name, type or method.");
        }

        ToolTypes = Tools.Select(tool => tool.AdapterType).ToImmutableArray();
        ByName = Tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    public static ImmutableArray<AlphaMcpToolDescriptor> Tools { get; }
    public static ImmutableArray<Type> ToolTypes { get; }
    public static JsonSerializerOptions SerializerOptions => McpJsonUtilities.DefaultOptions;

    public static bool TryGet(string name, out AlphaMcpToolDescriptor descriptor) =>
        ByName.TryGetValue(name, out descriptor!);

    private static AlphaMcpToolDescriptor Read<TAdapter, TContract>(string name) =>
        Create<TAdapter, TContract>(
            name,
            AlphaMcpPolicyNames.CurrentUserRecord,
            ReadHints,
            StaffMcpPolicies.ReadScope);

    private static AlphaMcpToolDescriptor Named<TAdapter, TContract>(string name) =>
        Create<TAdapter, TContract>(
            name,
            AlphaMcpPolicyNames.NamedCommand,
            NamedCommandHints);

    private static AlphaMcpToolDescriptor Consequential<TAdapter, TContract>(string name) =>
        Create<TAdapter, TContract>(
            name,
            AlphaMcpPolicyNames.ConsequentialAction,
            ConsequentialHints);

    private static AlphaMcpToolDescriptor External<TAdapter, TContract>(string name) =>
        Create<TAdapter, TContract>(
            name,
            AlphaMcpPolicyNames.CurrentUserCaseExternal,
            ExternalHints);

    private static AlphaMcpToolDescriptor Create<TAdapter, TContract>(
        string name,
        string policy,
        AlphaMcpToolHints hints,
        string scope = StaffMcpPolicies.WriteScope) =>
        new(name, typeof(TAdapter), typeof(TContract), scope, policy, hints);
}
