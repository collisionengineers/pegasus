using BenchmarkDotNet.Running;
using CollisionDocNet.Performance;

if (args.Length == 1 && string.Equals(args[0], "--verify", StringComparison.Ordinal))
{
    return await PerformanceVerifier.RunAsync(Console.Out, CancellationToken.None).ConfigureAwait(false);
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return 0;
