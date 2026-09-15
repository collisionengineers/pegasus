## Audit complete

**Recommendation: retain 10 plugins and 49 skills; disable 5 plugins and 22 individual skills within retained plugins.** Keep the binlog MCP.

The [full audit covers all 96 skills individually, with reasons and exact paths](C:/Users/Alex/.codex/audits/dotnet-pegasus-20260915.md). I also prepared [proposed configuration changes](C:/Users/Alex/.codex/audits/dotnet-pegasus-20260915.proposed.toml). **Nothing has been disabled or installed.**

## Plugin recommendations

“Keep” includes useful occasional diagnostics and reviews. It does not mean routinely running every skill.

| Plugin | Recommendation | Pegasus value |
| --- | --- | --- |
| `dotnet` | Keep 1/1 | Project-local SDK setup and reproducing SDK-specific problems. |
| `dotnet-advanced` | Keep 3/4 | C# experiments, native interoperability and measured SIMD optimisation. Exclude NuGet publishing. |
| `dotnet-ai` | Keep 1/1 | ONNX inference guidance matches Pegasus’s existing vision subsystem. |
| `dotnet-aspnetcore` | Keep 1/4 | Web API guidance applies to Provider API and Graph webhook endpoints. Other skills target different hosting/instrumentation choices. |
| `dotnet-blazor` | Disable | Pegasus uses Razor Pages; Blazor components, circuits and render modes are absent. |
| `dotnet-data` | Keep 2/2 | EF query diagnosis and data-backed Razor screens. Generic CRUD instructions must respect Core policy and existing Razor skills. |
| `dotnet-diag` | Keep 4/7 | Performance review, traces, dumps and benchmarks. Exclude Android, Apple and classic .NET Framework diagnostics. |
| `dotnet-maui` | Disable | The desktop helper is **WinForms**, so MAUI lifecycle, controls and workloads do not apply. |
| `dotnet-msbuild` | Keep 14/18 + MCP | Build failure/performance analysis and project-file review. Exclude absent custom-target workflows and legacy project migration. |
| `dotnet-nuget` | Disable; enable when needed | Its only skill converts to Central Package Management. Useful if that migration is chosen; it provides no general NuGet diagnostic toolkit. |
| `dotnet-template-engine` | Disable; enable when needed | Useful for creating projects or reusable templates. Current changes mostly extend existing projects. |
| `dotnet-test` | Keep 20/22 | Substantial xUnit generation, review, testability and execution guidance. Exclude MSTest authoring and MTP-only hot reload. |
| `dotnet-test-migration` | Keep 2/6 | xUnit v2→v3 and VSTest→MTP guides start from Pegasus’s actual setup. Retention does not decide whether to migrate. |
| `dotnet-upgrade` | Keep 1/6 | .NET 10→11 guidance matches the current starting point. Its Preview 3 coverage needs refreshing before use. |
| `dotnet11` | Disable until applicable | Its JSON skill requires `net11.0`; Pegasus targets `net10.0`. |

## `setup-local-sdk`: yes, usable

The machine has **host 10.0.10 and SDK 10.0.302**, satisfying the local SDK selection prerequisite. The skill supports stable SDKs as well as previews. [Microsoft’s guidance](https://learn.microsoft.com/en-us/dotnet/core/tools/test-prerelease-sdk-locally).

It has useful applications, but the installed instructions need corrections:

- Check the **host** through `dotnet --info`; `dotnet --version` reports the selected SDK.
- Its optional PowerShell installer uses array splatting where named parameters require a hashtable.
- The existing `global.json` pin can interfere with querying a newly installed, different SDK before updating that pin.
- Preserve Pegasus’s explicit `allowPrerelease = false` for stable installations.

These findings and corrections are explained in the audit. **The existing SDK already works; another installation is unnecessary now.**

## Binlog MCP: definitely worth retaining

It belongs to **`dotnet-msbuild`**, independently of the SDK installation skill.

A `.binlog` records a build. The MCP lets Codex inspect that recording to explain:

- Build failures hidden behind abbreviated console errors.
- Package and assembly resolution.
- Missing or conflicting copied files.
- Slow projects, tasks and analysers.
- Unexpected rebuilds.

I verified that the server responds and exposes **44 tools**. Its running package is **3.0.2**. The launch flag `--prerelease` concerns the **MCP package**, not Pegasus’s SDK.

It is the **only MCP declared by these 15 plugins**. Availability was verified; no actual Pegasus binlog was available to analyse.

The audit also preserves useful focused test-generation and wrapper guidance: their current instructions contain scope guards that make blanket removal unjustified.