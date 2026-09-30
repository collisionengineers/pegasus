using System.Reflection;

namespace Pegasus.ArchitectureTests;

/// <summary>The ordered calls that every <see cref="PortRecorder"/> of one test made.</summary>
public sealed class CallLog
{
    private readonly List<string> calls = [];

    public void Add(string call)
    {
        lock (calls)
        {
            calls.Add(call);
        }
    }

    public string[] Snapshot()
    {
        lock (calls)
        {
            return [.. calls];
        }
    }
}

/// <summary>
/// Stands in for a port whose asynchronous methods may each answer with an empty
/// result. It writes every call to one shared <see cref="CallLog"/> as
/// "PortName.Method", so a test can prove the order of calls across several ports,
/// and it lets a test replace the answer of one method by name. Use it where the
/// order or the fault handling of a caller is the subject, not the content of a port.
/// </summary>
public class PortRecorder : DispatchProxy
{
    private readonly Dictionary<string, Func<object?[], object?>> answers = new(StringComparer.Ordinal);
    private CallLog log = new();

    public static TPort Of<TPort>(CallLog log, out PortRecorder recorder)
        where TPort : class
    {
        var port = Create<TPort, PortRecorder>();
        recorder = (PortRecorder)(object)port;
        recorder.log = log;
        return port;
    }

    /// <summary>Replaces the answer of every method of this port called <paramref name="method"/>.</summary>
    public void Answer(string method, Func<object?[], object?> answer) => answers[method] = answer;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod ?? throw new InvalidOperationException("A port call named no method.");
        log.Add($"{method.DeclaringType!.Name}.{method.Name}");
        return answers.TryGetValue(method.Name, out var answer)
            ? answer(args ?? [])
            : EmptyAnswer(method.ReturnType);
    }

    private static object? EmptyAnswer(Type returnType)
    {
        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            throw new NotSupportedException($"{returnType} is not an asynchronous port result.");
        }

        var resultType = returnType.GetGenericArguments()[0];
        object? empty = resultType.IsGenericType
            && resultType.GetGenericTypeDefinition() is var definition
            && (definition == typeof(IReadOnlyList<>) || definition == typeof(IEnumerable<>))
                ? Array.CreateInstance(resultType.GetGenericArguments()[0], 0)
                : resultType.IsValueType
                    ? Activator.CreateInstance(resultType)
                    : null;
        return typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [empty]);
    }
}
