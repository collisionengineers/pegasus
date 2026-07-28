# EV-2026-07-23 — foundation tooling audit

Scope: managed CFB v3 fixed-header reader only. This record is local toolchain,
static-review and unit evidence; it is not full CFB conformance, independent
differential evidence, complete `.doc` extraction evidence, or CollisionSpike caller
evidence.

## Environment

- Host: Windows `10.0.26200`, x64.
- SDK: .NET SDK `10.0.302`; MSBuild `18.6.11`.
- Target: `net10.0`.
- Tests: MSTest.Sdk `4.0.2` on Microsoft.Testing.Platform `2.0.2`.
- Top-level repository: not initialised as Git.

## Plugin-guided checks

### .NET SDK

`dotnet --version` resolved the `global.json` pin exactly to `10.0.302`. A
project-local SDK was not installed because the pinned stable SDK is already
available. `.dotnet/` remains ignored for a future isolated SDK requirement.

### MSBuild organisation

All two project files, `Directory.Build.props`, `global.json` and the solution
were inspected. Shared language/analyser/restore settings are centralised in
`Directory.Build.props`; target frameworks and project references remain
project-owned. There is no TFM-conditional property in `.props`, custom target,
or ordinary `PackageReference` that justifies `Directory.Build.targets` or
Central Package Management yet.

The evaluated production-project values were obtained with:

```powershell
dotnet msbuild src\CollisionDocNet.Storage\CollisionDocNet.Storage.csproj -getProperty:TargetFramework -getProperty:LangVersion -getProperty:ImplicitUsings -getProperty:Nullable -getProperty:TreatWarningsAsErrors -getProperty:RestorePackagesWithLockFile
```

They resolved to `net10.0`, `latest`, `enable`, `enable`, `true`, and `true`.
Both projects now have repository lock-state files, and
`scripts/Invoke-RepoCheck.ps1` restores them with `--locked-mode`.

### Standard performance-pattern scan

Hot path: `CompoundFileHeaderReader.Read(ReadOnlySpan<byte>)` and its production
types. Every production C# file was read in full. Exact recipe hit counts:

| Recipe | Hits |
| --- | ---: |
| `IndexOf(string)` without `StringComparison` | 0 |
| `Substring` allocation | 0 |
| `StartsWith`/`EndsWith` without `StringComparison` | 0 |
| `Contains(string)` without `StringComparison` | 0 |
| parameterless `ToLower`/`ToUpper` | 0 |
| three-or-more chained `Replace` calls | 0 |
| `params` signatures | 0 |
| LINQ `All`/`Any` on characters | 0 |
| static `Dictionary` / `FrozenDictionary` | 0 / 0 |
| per-call `List` / `Dictionary` allocation | 0 / 0 |
| `StringComparer.CurrentCulture` | 0 |
| LINQ `Select`/`Where`/`Cast`/`Take`/`Aggregate` | 0 |
| runtime or compiled regular expressions | 0 |
| async/task signals | 0 |
| I/O/serialization signals | 0 |
| unsealed eligible leaf classes | 0 |
| sealed eligible leaf records | 1 |

No performance anti-pattern was classified. Positive evidence includes
`ReadOnlySpan<byte>`, compile-time span data, direct loops instead of LINQ, no
I/O, and 1/1 eligible leaf reference types sealed. This is a static scan, not a
benchmark or allocation measurement.

### Pseudo-mutation test-gap analysis

The analysis covered every non-trivial branch, range, arithmetic/endian read,
header-output mapping and result invariant. Counts use grouped mutation sites:

| Verdict | Sites |
| --- | ---: |
| Killed by an assertion | 43 |
| Survived | 0 |
| No coverage | 0 |
| Equivalent | 2 |
| Total | 45 |

The equivalent sites are the immutable builder's initial capacity (which can
change allocation without changing output) and the redundant conjunction in
`IsSuccess` under the type's private construction invariants.

The review found and then closed gaps for both ends of signature/CLSID/reserved
ranges, invalid values on both sides of ordered constants, more than one
non-zero alignment remainder, containers larger than the minimum, every header
DIFAT slot, little-endian DIFAT decoding, and the default result error state.
The resulting MSTest suite contains 30 discovered cases.

## Commands and current results

Focused command:

```powershell
dotnet test --project tests\unit\CollisionDocNet.Storage.Tests\CollisionDocNet.Storage.Tests.csproj --configuration Release --filter "FullyQualifiedName~CompoundFileHeaderReaderTests"
```

Result: exit `0`; 30 succeeded, 0 failed, 0 skipped. Boundary: generated
in-memory CFB headers only. No document corpus, network, external executable,
external office-suite process, native code, macro, OLE execution or filesystem input was
used.

Complete command:

```powershell
.\scripts\Invoke-RepoCheck.ps1
```

Result: exit `0`; locked restore succeeded, formatting was unchanged, Release
build produced 0 warnings and 0 errors, all 30 tests passed, all declared JSON
parsed, and all local Markdown links resolved.

> Performance and pseudo-mutation findings are AI-assisted static analysis and
> may contain false positives or omissions. Treat them as review evidence, not
> a substitute for benchmarks, an actual mutation runner or independent human
> review.
