using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

// Dev-only microbenchmarks for the C-10 items. Not part of Pegasus.slnx and not
// run by CI. From the repository root:
//
//   dotnet run -c Release --project tests/Pegasus.Benchmarks -- --filter "*"
//
// Results land under artifacts/performance/benchmarkdotnet (ignored by git).
var root = AppContext.BaseDirectory;
while (root is not null && !File.Exists(Path.Combine(root, "Pegasus.slnx")))
{
    root = Path.GetDirectoryName(root);
}

var config = DefaultConfig.Instance;
if (root is not null)
{
    config = config.WithArtifactsPath(
        Path.Combine(root, "artifacts", "performance", "benchmarkdotnet"));
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
