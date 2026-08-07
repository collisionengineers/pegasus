# 0014 — Uplift the workspace from `net8.0` to `net10.0`

## Status

Accepted

## Context

ADR-0003 standardised this workspace on a single .NET 8 stack and named each
project's target framework. The repository around it has since moved to .NET 10:
the root `global.json` pins a 10.0.x SDK, every application project targets
`net10.0`, and the two other .NET source workspaces (`document-extraction` and
`ai-centre/services/collision-brain`) are already `net10.0`. This workspace was
the only .NET code left on `net8.0`.

Two things had to happen first, and both have:

- ADR-0012 removed `CollisionRenderer.Gui`, the only project that could not be
  uplifted without a WinUI / Windows App SDK assessment. With it gone the
  solution has no Windows-only target framework and the uplift becomes six
  identical one-line edits.
- ADR-0013 moved Scriban to 7.2.6 and removed the advisory suppression, with its
  own composed-HTML parity proof, so that a template-engine change and a runtime
  change could not fail parity together and leave the cause unattributable.

A second, independent defect surfaced and is folded in because the uplift cannot
be proved in a container without it: the Dockerfile's runtime base image tag,
`mcr.microsoft.com/playwright/dotnet:v1.61.0-jammy`, **does not exist**.
Playwright's jammy publication stops at `v1.59.0`. The container build was
already broken on `net8.0`, before any uplift.

## Decision

Move every project in `CollisionRenderer.sln` to `net10.0`:

- `global.json` SDK pin `10.0.300` → `10.0.302`. The workspace keeps its own
  `global.json`; it is an independently buildable import and `document-extraction`
  keeps one for the same reason.
- `CollisionRenderer.Core`, `.Cli`, `.Api`, `.Mcp`,
  `CollisionRenderer.Core.Tests` and `CollisionRenderer.Mcp.Tests` all move from
  `net8.0` to `net10.0`.
- `Microsoft.Extensions.Hosting` `9.0.0` → `10.0.10` in `.Mcp`. The old pin sat
  below the `10.0.7` floor that `ModelContextProtocol` 1.4.0 imposes on
  `Microsoft.Extensions.*`. Restore was checked for `NU1510`; it did not fire, so
  the reference is retained rather than removed.
- Both test projects: `Microsoft.NET.Test.Sdk` `17.11.1` → `17.14.1`, `xunit`
  `2.9.2` → `2.9.3`, `xunit.runner.visualstudio` `2.8.2` → `3.1.4`. These are the
  repository-proven pairings; the root test projects run `net10.0` on 17.14.1.
- `Directory.Build.props` gains `<Deterministic>true</Deterministic>`. This makes
  an existing SDK default explicit so it cannot be lost silently; it changes no
  compiler output. The property was measured `true` in this workspace before the
  line was added, so nothing here depends on it having been off.
- The Dockerfile's build stage moves to `mcr.microsoft.com/dotnet/sdk:10.0.302`
  and its final stage to `mcr.microsoft.com/playwright/dotnet:v1.61.0-noble`,
  which is built from `mcr.microsoft.com/dotnet/sdk:10.0-noble` and therefore
  carries the .NET 10 runtime the published API needs.
- `scripts/render-starters.ps1` and `src/CollisionRenderer.Mcp/build-mcpb.ps1`
  have their hard-coded `net8.0` publish/output path segments corrected.

`PDFsharp` 6.2.4, `Microsoft.Playwright` 1.61.0 and `ModelContextProtocol` 1.4.0
are unchanged; each has a recorded reason for its pin, and the Playwright comment
pin (the 1.49.x Windows headless-shell launch hang) stays valid.

## Scope of supersession

This ADR supersedes **only ADR-0003's per-project `net8.0` target framework
list** — the Core, Cli, Api and test-project bullets in its Decision, and its
statement that the repository "is built with the .NET 10 SDK while targeting
`net8.0`".

Explicitly **not** superseded by this ADR:

- ADR-0003's "one language and runtime, shared engine, thin clients" rationale,
  which survives intact;
- ADR-0003's rejection of Python and of mixed stacks;
- ADR-0003's WinUI / `net8.0-windows` clause, which is superseded by **ADR-0012**,
  not by this ADR. Cross-referenced deliberately rather than absorbed.

No root ADR is required. Root ADR-0010 makes `docs/adr/` the sole *root*
durable-decision store while leaving workspace-local decisions where they are, and
a workspace-local runtime uplift is a workspace-local decision. A root ADR would
be warranted only for a cross-workspace rule such as "every source workspace
tracks the repository target framework", which is not decided here.

## Consequences

- The workspace builds, tests and runs on `net10.0`, an LTS runtime.
  `.github/workflows/workspaces.yml` already sets `dotnet-version: 10.0.x` and
  needs no change.
- `LangVersion` is `latest`, so the compiler moves from C# 13 to C# 14 with the
  SDK. **`field` is a contextual keyword in C# 14.** No identifier named `field`
  exists in this tree today, so nothing breaks now — but the next edit that
  introduces one will, and that is why it is recorded here.
- **Empty environment variables.** The .NET 9 change is on the write side:
  `Environment.SetEnvironmentVariable(name, string.Empty)` now sets an empty value
  instead of deleting, and `ProcessStartInfo.Environment[name] = null` now deletes
  instead of setting empty. This workspace makes no such call: there is no
  `ProcessStartInfo` environment-dictionary mutation anywhere, and no write of an
  empty value. Every read site was inspected — `Api/Program.cs`,
  `Rendering/PdfEngine.cs` (both sites), `Mcp/BrowserBootstrap.cs` (both sites),
  `Mcp/Tools/HealthTools.cs` and `Mcp/Valuation/EvidencePathResolver.cs` — and
  each either guards with `IsNullOrWhiteSpace` or compares with `== "1"`, so a
  set-but-empty variable behaves identically to an unset one. No code was changed.
  Note also that this is runtime-libraries behaviour keyed to the runtime the
  application executes on, not to the compile-time target framework: a `net8.0`
  binary rolled forward onto .NET 10 already saw it. **This uplift did not
  introduce the behaviour and did not fix anything.**
- **`NuGetAuditMode` defaults to `all` on `net10.0`**, up from `direct`. Combined
  with ADR-0013's removal of the `NU19xx` suppression, restore now audits the full
  transitive graph with nothing masked. That is the intended end state. It also
  means `TreatWarningsAsErrors=false` leaves any future advisory as a warning, so
  a clean build is **not** evidence of a clean audit; the evidence is
  `dotnet list package --vulnerable --include-transitive`, which currently reports
  no vulnerable packages in any of the six projects.
- **`Deterministic=true` does not change the reported engine version.**
  `ChromiumPdfEngine.EngineVersion` reads
  `AssemblyInformationalVersionAttribute` and is embedded in render results and
  surfaced through the API and MCP responses, so it was measured rather than
  assumed: the string is byte-identical before and after, on the same commit. Its
  `+<commit-sha>` suffix comes from source-revision embedding and therefore tracks
  HEAD — that is pre-existing behaviour, unrelated to `Deterministic`.
- **The container build is unverified.** Docker is not installed on the
  workstation that made this change, so the corrected base image tags are a
  documentation and configuration fix, not a proven build. The `v1.61.0-noble`
  image moves the runtime from Ubuntu 22.04 to 24.04, changing both the
  Liberation/DejaVu font versions and the system ICU. There is no valid earlier
  image to compare against, because the tag it replaces never existed, so the
  first successful noble build establishes a baseline rather than confirming
  parity.
- **The `.mcpb` single-file bundle is unverified under .NET 10.** The .NET 10
  single-file change removes the application directory from
  `NATIVE_DLL_SEARCH_DIRECTORIES`. The only P/Invokes on that path are
  `GetStdHandle`/`SetStdHandle` on `kernel32` in `BrowserBootstrap.cs`, with no
  `DefaultDllImportSearchPaths` attribute, so they keep the default search set;
  and the Playwright driver is launched as a process rather than P/Invoked. No
  change was made. Only building and launching the bundle would prove it.
- **Strictness is deliberately deferred.** MSBuild stops at this workspace's own
  `Directory.Build.props`, so these projects inherit nothing from the repository
  root — not `TreatWarningsAsErrors=true`, not
  `AnalysisLevel=latest-recommended`, and `Version` stays `0.2.6` rather than the
  root's release version. Adopting root strictness is estimated at low hundreds of
  diagnostics across roughly 9,100 lines, concentrated in `CA1707` (every test
  method is underscore-named), `CA1062` and `CA1031`. That is a reviewable
  suppression decision, not a mechanical one, and it must not be reviewed in the
  same diff as a runtime change. It is deferred, and this consequence exists so it
  is not lost.
- **Lock files are deliberately not added.** The workspace has none, so the
  absence of `--locked-mode` on its CI restore is correct today rather than a
  defect. Adding six `packages.lock.json` files is a separate decision.
- Render output is unchanged. All 12 template identifiers were composed at all 3
  density values before and after the uplift; the 36 composed-HTML SHA-256 hashes
  are identical, and identical to the pre-Scriban-upgrade baseline. Those 36
  outputs carry only 14 distinct hashes — density currently reaches
  `market-valuation-evidence` alone — so read them as 36 samples, not 36
  independent documents. See ADR-0013.
