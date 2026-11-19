using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// What acts in an Engineer's open Glass's session (<see cref="GlassSessionHelp"/>,
/// design 08 section 5; R1170, R1184) can do: read, search, preview (R1206),
/// add and remove, and nothing else. It holds no gateway, session store, login or import,
/// and nothing it calls, followed through every method of this assembly it
/// reaches (async state machines, lambdas and local functions included),
/// reaches the editor's Save and Exit, the gateway, the session store, a
/// Glass's login or the import.
/// </summary>
public sealed class GlassSessionHelpBoundaryTests
{
    private static readonly Type[] ForbiddenTypes =
    [
        typeof(IGlassRepairEstimateGateway),
        typeof(GlassRepairEstimateGateway),
        typeof(IGlassRepairEstimateSessionStore),
        typeof(IPerUserExternalCredentialReader),
        typeof(GlassAutomationAccountSource),
        typeof(IImportRawEstimate),
    ];

    private static readonly string[] ForbiddenEditorMethods = ["SaveAndExitAsync", "OpenAsync"];

    private static readonly string[] HelpActs = ["AddAsync", "PreviewAsync", "ReadAsync", "RemoveAsync", "SearchAsync"];

    private static readonly Assembly Infrastructure = typeof(GlassSessionHelp).Assembly;

    private static readonly Type EditorClient =
        Infrastructure.GetType("Pegasus.Infrastructure.Glass.GlassEditorClient", throwOnError: true)!;

    private static readonly Type PositionWork =
        Infrastructure.GetType("Pegasus.Infrastructure.Glass.GlassEditorPositionWork", throwOnError: true)!;

    [Fact]
    public void HelpHoldsNoGatewayStoreLoginOrImport()
    {
        var held = TypeInspection.OnlyConstructorParameterTypes(typeof(GlassSessionHelp))
            .Concat(Fields(typeof(GlassSessionHelp)))
            .Concat(Fields(PositionWork))
            .ToList();

        foreach (var type in held)
        {
            Assert.DoesNotContain(type, ForbiddenTypes);
        }
        Assert.Contains(typeof(GlassHelpKeys), held);
        Assert.Contains(typeof(IGlassSessionHelpStore), held);
    }

    [Fact]
    public void HelpOffersOnlyReadSearchPreviewAddAndRemove()
    {
        var offered = typeof(GlassSessionHelp)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(HelpActs, offered);
    }

    [Fact]
    public void NothingHelpCallsReachesSaveExitTheGatewayTheStoreALoginOrTheImport()
    {
        var reached = Reached(typeof(GlassSessionHelp));

        // The walk itself works: it reaches the editor's add and remove
        // through the shared position work.
        Assert.Contains(reached, method => method.DeclaringType == EditorClient && method.Name == "AddAsync");
        Assert.Contains(reached, method => method.DeclaringType == EditorClient && method.Name == "RemoveAsync");
        var forbidden = reached
            .Where(method => method.DeclaringType is { } declaring
                && (ForbiddenTypes.Contains(declaring)
                    || (declaring == EditorClient && ForbiddenEditorMethods.Contains(method.Name))))
            .Select(method => method.DeclaringType?.Name + "." + method.Name)
            .ToList();
        Assert.True(forbidden.Count == 0, "Help reaches " + string.Join(", ", forbidden));
    }

    private static IEnumerable<Type> Fields(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType);

    /// <summary>
    /// Every method the type's own methods call, construct or take the address
    /// of, followed through each method of this assembly so reached.
    /// </summary>
    private static List<MethodBase> Reached(Type start)
    {
        var seen = new HashSet<MethodBase>();
        var reached = new List<MethodBase>();
        var queue = new Queue<MethodBase>(Own(start));
        while (queue.Count > 0)
        {
            var method = queue.Dequeue();
            if (!seen.Add(method))
            {
                continue;
            }

            foreach (var called in Calls(method))
            {
                reached.Add(called);
                if (called.DeclaringType?.Assembly == Infrastructure
                    && called.DeclaringType is { IsConstructedGenericType: false })
                {
                    queue.Enqueue(called is MethodInfo { IsGenericMethod: true, IsGenericMethodDefinition: false } constructed
                        ? constructed.GetGenericMethodDefinition()
                        : called);
                }
            }

            // An async method's body is its state machine's MoveNext; an
            // iterator's likewise.
            var machine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                ?? method.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
            if (machine?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is { } moveNext)
            {
                queue.Enqueue(moveNext);
            }
        }

        return reached;
    }

    private static IEnumerable<MethodBase> Own(Type type) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .GroupBy(code => code.Value)
        .ToDictionary(group => group.Key, group => group.First());

    /// <summary>The methods one method's body names as call, callvirt, newobj, ldftn or ldvirtftn operands.</summary>
    private static List<MethodBase> Calls(MethodBase method)
    {
        var found = new List<MethodBase>();
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            return found;
        }

        var typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
        var methodArguments = method is MethodInfo { IsGenericMethod: true } generic ? generic.GetGenericArguments() : null;
        var at = 0;
        while (at < il.Length)
        {
            short value = il[at++];
            if (value == 0xFE)
            {
                value = unchecked((short)(0xFE00 | il[at++]));
            }

            var code = OpCodesByValue[value];
            if (code.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, at);
                try
                {
                    if (method.Module.ResolveMethod(token, typeArguments, methodArguments) is { } called)
                    {
                        found.Add(called);
                    }
                }
                catch (ArgumentException)
                {
                    // A token this context cannot resolve names nothing of this assembly's.
                }
            }

            at += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, at)),
                _ => 4,
            };
        }

        return found;
    }
}
