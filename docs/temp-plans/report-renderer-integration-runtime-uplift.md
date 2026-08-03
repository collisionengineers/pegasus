# Report renderer runtime uplift — .NET 8 to .NET 10

This is a draft supporting plan for the `report-renderer-integration` task. It
covers one thing only: moving `workspaces/report-renderer/` from `net8.0` to the
repository's `net10.0` target, and deciding whether that runtime is supportable
there. It is not the integration plan, the desktop-removal plan, the MCP
consolidation plan, or the relocation plan.

## Verdict

**Supported. Uplift to `net10.0` is feasible for every remaining project, and
two of the three hardest inputs are already .NET 10 by construction.**

The single genuine blocker in the workspace is `CollisionRenderer.Gui`
(`net8.0-windows10.0.19041.0`, `UseWinUI=true`, `Microsoft.WindowsAppSDK 2.2.0`,
MSIX tooling, `SelfContained`/`WindowsAppSDKSelfContained`). The parallel
desktop-removal plan deletes it. With the GUI gone the solution has no
Windows-only TFM, no Windows App SDK dependency, no `RuntimeIdentifier` pin, no
`Platforms` matrix and no MSIX capability — the whole solution becomes
framework-agnostic `net10.0`, and the `windows-latest` CI runner becomes a
choice rather than a requirement.

A second, independent defect surfaced during verification and is folded in
because the uplift cannot be proved in a container without it: **the Dockerfile's
runtime base image tag does not exist.**

## Feasibility evidence

### Per package

| Package | Pinned | `net10.0` support | Verdict |
| --- | --- | --- | --- |
| `PDFsharp` | 6.2.4 | Ships an explicit `net10.0` target group (also `net8.0`, `net9.0`, `netstandard2.0`) | Keep 6.2.4 |
| `Scriban` | 5.12.1 | Targets `net7.0` and `netstandard2.0`; the `net7.0` asset is selected for `net10.0` | Keep 5.12.1 — see the advisory note |
| `Microsoft.Playwright` | 1.61.0 | `netstandard2.0` only, listed compatible through `net10.0`; latest published version is 1.61.0 | Keep 1.61.0; the comment pin (1.49.x headless-shell hang, playwright#34306) stays valid |
| `ModelContextProtocol` | 1.4.0 | `net8.0` + `netstandard2.0`, compatible with `net10.0`; already depends on `Microsoft.Extensions.*` >= **10.0.7** | Keep 1.4.0; see the `Hosting` conflict below |
| `Microsoft.Extensions.Hosting` | 9.0.0 | Supported on `net10.0`, but the pin is *below* the 10.0.7 floor `ModelContextProtocol` 1.4.0 imposes on `Hosting.Abstractions` | **Bump to 10.0.10** |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | Not verified as supporting `net10.0`. The repository proves 17.14.1 does — all three root test projects are `net10.0` on 17.14.1 and pass `repository-check` | **Bump to 17.14.1** |
| `xunit` | 2.9.2 | Repository-proven pairing is 2.9.3 | **Bump to 2.9.3** |
| `xunit.runner.visualstudio` | 2.8.2 | Nominally compatible; repository-proven pairing is 3.1.4 | **Bump to 3.1.4** |

Scriban 5.12.1 carries known critical/high advisories; that is exactly why the
workspace `NoWarn` lists `NU1901;NU1902;NU1903;NU1904` and why workspace
ADR-0010 exists. .NET 10 makes this *worse* in one specific way: `dotnet restore`
now audits **transitive** packages by default, so the audit surface grows.
Bumping Scriban is a template-engine change with real render-parity risk and is
an explicit **non-goal** here.

### Per project

| Project | Current | Target | Blocking issue |
| --- | --- | --- | --- |
| `CollisionRenderer.Core` | `net8.0` | `net10.0` | None. Pure library |
| `CollisionRenderer.Cli` | `net8.0` | `net10.0` | None. `InvariantGlobalization=false` stays and matters |
| `CollisionRenderer.Api` | `net8.0` (`Sdk.Web`) | `net10.0` | None. Nothing in the ASP.NET Core 10 breaking-change list applies — no cookie auth, no `WithOpenApi`, no MVC analyzers, no `WebHostBuilder`, no Razor runtime compilation, no `IActionContextAccessor`, no `ForwardedHeaders` |
| `CollisionRenderer.Mcp` | `net8.0` | `net10.0` | `Microsoft.Extensions.Hosting` 9.0.0 conflicts with the package's own 10.0.7 floor; `build-mcpb.ps1` hard-codes `net8.0` in the publish path |
| `CollisionRenderer.Core.Tests` | `net8.0` | `net10.0` | Test tooling versions |
| `CollisionRenderer.Mcp.Tests` | `net8.0` | `net10.0` | Test tooling versions |
| `CollisionRenderer.Gui` | `net8.0-windows10.0.19041.0` | — | **Deleted by the desktop-removal plan. Not assessed, not uplifted** |

### Container feasibility — a real defect

`workspaces/report-renderer/Dockerfile` line 15 is
`FROM mcr.microsoft.com/playwright/dotnet:v1.61.0-jammy`. Querying the registry
tag list for `playwright/dotnet` returns, for 1.61.0, only `v1.61.0`,
`v1.61.0-noble`, `v1.61.0-resolute` and their arch suffixes. **No
`v1.61.0-jammy` tag exists.** Jammy publication stops at `v1.59.0`. The container
build is therefore already broken today, on `net8.0`, before any uplift.

The fix is also the uplift enabler:

- `v1.59.0-jammy` was built `FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy` — had
  the tag existed at 1.61.0 it would have carried only a .NET 8 runtime and a
  `net10.0` publish would have failed at container start.
- `v1.61.0-noble` is built `FROM mcr.microsoft.com/dotnet/sdk:10.0-noble`, so it
  already contains the .NET 10 SDK and ASP.NET Core 10 runtime.

`mcr.microsoft.com/dotnet/sdk:10.0.300` (the build stage) does exist; `10.0.301`
and `10.0.302` also exist.

## Ordered change list

Every edit assumes `CollisionRenderer.Gui` has already been deleted and removed
from `CollisionRenderer.sln` by the desktop-removal plan.

### Step 1 — SDK pin

`workspaces/report-renderer/global.json`: `"version": "10.0.300"` →
`"version": "10.0.302"`. Leave `rollForward` and `allowPrerelease` alone; they
already match the root `global.json`. Do **not** delete the workspace
`global.json`: the workspace is an independently buildable import per ADR-0009,
and `document-extraction` keeps its own for the same reason.

### Step 2 — target frameworks

Six identical edits, `net8.0` → `net10.0`:

| File | Line |
| --- | --- |
| `src/CollisionRenderer.Core/CollisionRenderer.Core.csproj` | 4 |
| `src/CollisionRenderer.Cli/CollisionRenderer.Cli.csproj` | 5 |
| `src/CollisionRenderer.Api/CollisionRenderer.Api.csproj` | 4 |
| `src/CollisionRenderer.Mcp/CollisionRenderer.Mcp.csproj` | 5 |
| `tests/CollisionRenderer.Core.Tests/CollisionRenderer.Core.Tests.csproj` | 4 |
| `tests/CollisionRenderer.Mcp.Tests/CollisionRenderer.Mcp.Tests.csproj` | 4 |

### Step 3 — package versions

`src/CollisionRenderer.Mcp/CollisionRenderer.Mcp.csproj` line 13:
`Microsoft.Extensions.Hosting` `9.0.0` → `10.0.10`. That is the
`Microsoft.Extensions.*` line the monolith already uses. After restore, check the
resolved graph: if NuGet reports `NU1510` for this reference, the package is
being pruned as framework-provided and the reference should be removed outright
rather than re-pinned. `Pegasus.Web` already carries
`<NoWarn>$(NoWarn);NU1510</NoWarn>`, so `NU1510` is live in this repository — do
not assume it will not fire.

Both test projects: `Microsoft.NET.Test.Sdk` `17.11.1` → `17.14.1`; `xunit`
`2.9.2` → `2.9.3`; `xunit.runner.visualstudio` `2.8.2` → `3.1.4`.

Leave `PDFsharp`, `Scriban`, `Microsoft.Playwright` and `ModelContextProtocol`
untouched.

### Step 4 — workspace build properties

`workspaces/report-renderer/Directory.Build.props`: the comment block at lines
2–6 is now false — it says "Core/Cli/Api are net8.0; the WinUI Gui is
net8.0-windows". Replace with a statement that all projects target `net10.0`.

Add one property in phase 1:

```xml
<Deterministic>true</Deterministic>
```

This matches both the root `Directory.Build.props` and the `document-extraction`
workspace, and it is a prerequisite for the RPT-01 deterministic-renderer
outcome rather than a stylistic alignment.

Leave `TreatWarningsAsErrors=false`, `NoWarn`, `Version=0.2.6` and the product
metadata unchanged in phase 1. Strictness is step 9, deliberately separate.

### Step 5 — container

| Line | Before | After |
| --- | --- | --- |
| 7 | `FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build` | `FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build` |
| 15 | `FROM mcr.microsoft.com/playwright/dotnet:v1.61.0-jammy AS final` | `FROM mcr.microsoft.com/playwright/dotnet:v1.61.0-noble AS final` |

Leave the `fonts-liberation` / `fonts-dejavu-core` install, the
`PLAYWRIGHT_BROWSERS_PATH`, `ASPNETCORE_URLS`,
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`, `EXPOSE 8080` and `ENTRYPOINT`
lines alone. Both font packages exist on Ubuntu 24.04, but their *versions*
differ from 22.04, and so does the system ICU version — a glyph-metric and
currency-formatting risk covered in verification.

### Step 6 — scripts

- `scripts/render-starters.ps1` line 19: `net8.0` → `net10.0` in the CLI output
  path.
- `src/CollisionRenderer.Mcp/build-mcpb.ps1` line 51: same. If the MCP
  consolidation plan deletes the MCP host, skip this and delete the script with
  the project.

### Step 7 — CI

`.github/workflows/workspaces.yml` already sets `dotnet-version: 10.0.x`, so no
change is required for the uplift itself. Two conditional edits:

- If step 10 is taken, change the renderer restore to `--locked-mode`, matching
  the other two .NET workspace steps.
- Do **not** move the job off `windows-latest` in this task. The same job
  validates three workspaces; the runner choice is not this task's to change.

### Step 8 — the determinism defect

`src/CollisionRenderer.Core/Format.cs:106`:

```csharp
public static string Today() => DateTime.Now.ToString("dd/MM/yyyy", Uk);
```

There is exactly one caller: `Templating/HtmlComposer.cs:491`, which substitutes
`Format.Today()` when `meta.Date` is blank. There is exactly one
`DateTime`/`DateTimeOffset` reference in the whole non-GUI source tree, so this
is the complete surface.

`DateTime.Now` is wrong twice over: it is untestable, and it is *machine local
time*, so the same payload rendered in the UTC container and on a UK desktop
during BST can produce different dates near midnight. Proposed seam:

- Thread a `TimeProvider` (defaulted to `TimeProvider.System`) into
  `HtmlComposer` and through `CollisionRendererFactory`, and derive the document
  date as the Europe/London conversion of `provider.GetUtcNow()` so the answer is
  the UK business date regardless of host clock zone. `TimeProvider` is available
  on `net8.0`, so this change is independent of the uplift.
- Prefer an explicit request-supplied document date over an ambient clock
  wherever a caller can supply one; the ambient clock stays only as the
  documented fallback.

On `Format.Money` and the `en-GB` pin: hard-coding `en-GB` is not the defect —
it is what makes currency output culture-**independent** of the host. The real
exposure is that `CultureInfo.GetCultureInfo("en-GB")` resolves against the
runtime's ICU, and ICU changes between the .NET 8 and .NET 10 runtimes and
between Ubuntu 22.04 and 24.04. Keep the pin; add golden-string assertions so an
ICU shift fails a test instead of silently changing a report. Those assertions
also fail loudly if globalization-invariant mode is ever switched on, because
`GetCultureInfo("en-GB")` throws under `InvariantGlobalization=true` with the
default `PredefinedCulturesOnly`.

### Step 9 — strictness (separate commit; see the reconciliation section)

### Step 10 — optional: lock files

Add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to the
workspace `Directory.Build.props`, restore once, and commit the six generated
`packages.lock.json` files. This is what `document-extraction` and
`ai-centre/services/collision-brain` already do and is what makes
`--locked-mode` in CI meaningful. It adds six tracked files to a workspace that
currently has none.

## The strictness reconciliation

### What is actually true today

MSBuild stops at the **first** `Directory.Build.props` found walking up from a
project. The workspace has its own, so the renderer projects inherit **nothing**
from the root. Verified root contents:

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<LangVersion>latest</LangVersion>
<Deterministic>true</Deterministic>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<Version>0.1.0-alpha.1</Version>
```

So `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended` and
`Deterministic=true` do not apply to the renderer, and `Version` is `0.2.6` from
the workspace file rather than `0.1.0-alpha.1`. Nothing forces a change today.
It is forced only by relocation into `src/`.

### Blast-radius estimate

No build was run (this is a read-only planning pass), so this is an estimate from
two in-repo calibration points and a pattern census, not a measurement.

Calibration 1 — `workspaces/ai-centre/services/collision-brain` has **no**
`Directory.Build.props` of its own, so it already inherits the root strictness
*and* targets `net10.0` *and* passes the workspaces CI job. To do that in
**1,243 lines** it needs `NoWarn=CA1050;CA1707;CA1710;CA1716;CA1848;CA1869`.

Calibration 2 — `workspaces/document-extraction` sets its own strictness and
needs a `.editorconfig` rule disabling `CA1707` under `tests/**`.

Census of the renderer's non-GUI source (**9,143 lines**, 7.4× collision-brain,
with a far larger public surface — 68 public types, 50 public statics in Core
alone):

| Signal | Count | Likely rule |
| --- | --- | --- |
| Test methods, all underscore-named | 136 of 136 | `CA1707` |
| Bare `catch { }` / `catch (Exception …)` | 26 | `CA1031` |
| `StartsWith`/`EndsWith`/`IndexOf` on a literal with no `StringComparison` | 4 | `CA1310` / `CA1307` |
| Public property returning an array (`RenderResult.Pdf` is `byte[]`) | 1+ | `CA1819` |
| Public methods taking reference-type parameters without null checks | dozens | `CA1062` |
| `ToLowerInvariant()` used for comparison | 18 | `CA1308`, `CA1862` |

Honest expectation: **low hundreds of diagnostics**, concentrated in `CA1707`
(136 guaranteed hits from test naming alone), `CA1062` and `CA1031`. Almost all
are suppressible by rule rather than fixable by judgement, but "suppress 8–12
rule families across a 9k-line workspace" is a reviewable decision, not a
mechanical one, and it must not be reviewed in the same diff as a runtime change.

### Options

| Option | What it is | Cost | Risk |
| --- | --- | --- | --- |
| A. Inherit-then-fix | Delete the workspace props, let the root props apply, fix every diagnostic | Highest. Also silently changes `Version` from `0.2.6` to `0.1.0-alpha.1` and drops the product metadata | Mixes a runtime change with a large code change; unbounded review |
| B. Mirror `document-extraction` | Keep the workspace props; add `Deterministic`, `EnableNETAnalyzers`, `AnalysisLevel=latest-recommended`, `TreatWarningsAsErrors=true` locally, plus a scoped `NoWarn` and a `tests/**` `.editorconfig` rule | Medium, precedented twice in this repository | Contained; keeps `Version=0.2.6` and the workspace's independent identity |
| C. Defer | Uplift the TFM only; leave strictness until relocation forces it | Lowest | Leaves a known divergence; relocation later lands TFM-strictness debt and a directory move together |

**Recommendation: B, in a separate commit from steps 1–8, ideally a separate
PR.** It reaches the same end state as A, matches the pattern the repository has
already accepted twice, keeps the workspace independently buildable per ADR-0009,
and does not require the runtime change to be re-reviewed if the suppression list
is contested. Start the `NoWarn` from collision-brain's proven list, build, and
add only what actually fires — do not pre-emptively widen it.

If the operator prefers C, that is defensible; state explicitly in the ADR that
strictness is deferred to relocation so it is not lost.

## Nullable, analyzer and API-removal risks specific to this code

Both configurations already set `Nullable=enable` and `ImplicitUsings=enable`, so
the TFM change adds no *new* nullable regime — only new BCL annotations.
`LangVersion=latest` means the compiler moves C# 13 → C# 14 with the SDK.

Checked and **clear**: `BinaryFormatter always throws`;
`String.Trim(params ReadOnlySpan<char>)` removal (the only trim with arguments is
`TrimEnd('\r')`, a single-char overload that survives); `FilePatternMatch.Stem`,
`HttpListenerRequest.UserAgent`, LDAP, `MailAddress`, `DriveInfo`,
`BufferedStream.WriteByte`, `X500DistinguishedName`, `CoseSigner`, `MLDsa` — none
appear in the tree; `System.Text.Json` property-name-conflict checks (only
triggers under polymorphism or reference preservation, neither of which the
renderer uses); the ASP.NET Core 10 breaking-change list.

Checked and **needs attention**:

- **`field` is a contextual keyword in C# 14.** No identifier named `field`
  exists in the non-GUI tree, so this is clear today — but it is the kind of
  thing that breaks on the next edit, so it belongs in the ADR's consequences.
- **Empty environment variables (.NET 9).** `Environment.GetEnvironmentVariable`
  now returns `""` rather than `null` for a set-but-empty variable on Windows,
  and `SetEnvironmentVariable(name, "")` no longer deletes. Seven production call
  sites and four test sites are affected: `PdfEngine.cs:158,235`,
  `Api/Program.cs:451`, `Mcp/BrowserBootstrap.cs:147,151`,
  `Mcp/Tools/HealthTools.cs:35`, `Mcp/Valuation/EvidencePathResolver.cs:32`.
  `BrowserLaunchPlan.Normalize` and `ApiAuthOptions.SplitEnv` both use
  `IsNullOrWhiteSpace`, so they are safe; the others use `??`, `== "1"` or direct
  `null` checks and must each be read. The save/restore pattern in
  `BrowserBootstrapTests` and `EvidencePathResolverTests` passes `null` back,
  which is still correct — but any test relying on `""` meaning "unset" changes
  behaviour.
- **`NU1510` for pruned direct references** (.NET 10). Applies to the
  `Microsoft.Extensions.Hosting` reference. Already live in this repository.
- **Single-file native-library probing (.NET 10).** `build-mcpb.ps1` publishes
  `PublishSingleFile=true` `--self-contained` and then overlays a loose
  `.playwright` driver directory beside the executable. The .NET 10 change
  removes the application directory from `NATIVE_DLL_SEARCH_DIRECTORIES` for
  single-file apps. The Playwright driver is launched as a *process*, not
  P/Invoked, so this should not bite — but "should not" is not evidence. If the
  MCP host survives consolidation, the produced `.mcpb` must be launched once and
  a render driven through it.
- **`Deterministic=true` plus `ContinuousIntegrationBuild`.** Adding
  `Deterministic` changes embedded source paths. It does not change PDF bytes,
  but it does change assembly bytes, and `ChromiumPdfEngine.EngineVersion` reads
  `AssemblyInformationalVersionAttribute` — confirm the reported engine version
  string is unchanged, because it is embedded in render results and surfaced
  through the API and MCP responses.

## Verification plan

State the boundary honestly up front: **this workspace has no Pegasus caller, so
no amount of green renderer output is tier-5, tier-6 or tier-12 evidence for
Pegasus.** The uplift changes no Pegasus capability. What it can produce is
tier-1 evidence for the repository plus workspace-internal evidence shaped like
tiers 2, 3 and 7.

| Check | Command | Tier | Proves |
| --- | --- | --- | --- |
| Workspace restore | `dotnet restore ./CollisionRenderer.sln` (add `--locked-mode` if step 10 taken) | 1 | Package graph resolves on `net10.0`; surfaces `NU1015`/`NU1510`/`NU19xx` |
| Workspace build | `dotnet build ./CollisionRenderer.sln --configuration Release --no-restore` | 1 | Compilation on `net10.0` |
| Browser install | `dotnet run --project ./src/CollisionRenderer.Cli --configuration Release --no-build -- install-browser` | 1 | The renderer's own gate; Playwright driver starts under .NET 10 |
| Full workspace tests | `dotnet test ./CollisionRenderer.sln --configuration Release --no-build` | 2 / 3 / 7 | 136 tests including real-Chromium integration renders |
| Application solution untouched | `dotnet test ./tests/Pegasus.ArchitectureTests --configuration Release` | 1 | The workspace guards still pass — the uplift did not smuggle the workspace into `Pegasus.slnx` |
| Documentation links | `pwsh ./scripts/Test-DocumentationLinks.ps1` | 1 | The doc edits resolve |
| Container build | `docker build -f workspaces/report-renderer/Dockerfile .` from the repository root | 1 | The base-image tag exists and the `net10.0` publish runs on the noble image |
| PDF parity | `pwsh ./scripts/visual-regression.ps1` | workspace-internal, no Pegasus tier | Rendered output is unchanged across the uplift |

### The parity check, precisely

`scripts/visual-regression.ps1` renders every catalogue template from its starter
payload, rasterises with Poppler `pdftoppm`, and compares page PNGs by SHA-256.
It needs `pdftoppm` on `PATH` — packaged on Linux, a manual Poppler install on
Windows (`docs/operations.md:52` already records this).

Two things the implementer must get right:

1. **Do not compare PDF bytes.** Chromium embeds a creation timestamp and a
   document identifier, so two renders of the same payload on the same build
   differ. Before drawing any conclusion, run the control experiment: on the
   *pre-uplift* commit, render one template twice and compare (a) the PDF bytes
   and (b) the rasterised PNGs. Expect (a) to differ and (b) to match. If (b)
   also differs, the baseline is not stable and the parity check must be fixed
   before it can gate anything.
2. **Capture the baseline before the uplift.** `artifacts/` is gitignored, so no
   approved baseline is tracked. Run `visual-regression.ps1 -Approve` on the
   pre-uplift commit, then re-run without `-Approve` after. Starter payloads fill
   date fields with the literal `DD/MM/YYYY` (`StarterComposer.cs:140`), so
   `Format.Today()` is *not* reached on the starter path and the baseline is
   date-stable — but this is only true for starters, which is precisely why the
   determinism fix in step 8 is in scope.

Run the parity check twice: once for the TFM change on Windows against the local
Chromium, and once for the container base-image change (jammy → noble), because
the ICU and font-package versions differ between Ubuntu 22.04 and 24.04. The
second run has no valid "before" — the jammy tag does not exist — so record it as
a first baseline, not as a comparison.

## Sequencing against the other plans

| Plan | Order | Why |
| --- | --- | --- |
| Desktop (GUI) removal | **Before** the uplift | The GUI is the only project that cannot be uplifted without a WinUI/Windows App SDK assessment. CI builds the whole solution, so a half-uplifted solution with a `net8.0-windows` project either fails or forces a multi-TFM solution nobody wants. Removing it first makes the uplift six identical one-line edits |
| MCP consolidation | **Before** the uplift, if it deletes the host | If `CollisionRenderer.Mcp` and `.Mcp.Tests` are going away, uplifting them, bumping their `Hosting` pin and fixing `build-mcpb.ps1` is wasted work with a `.mcpb` single-file risk attached. If the host survives, order does not matter |
| Relocation into the monolith | **After** the uplift, strictly | Three reasons. (1) Relocation must be a pure move; a diff that both changes runtime and moves directories is unreviewable. (2) Once the projects sit under `src/`, the root `Directory.Build.props` applies automatically, so relocation would land the TFM change *and* the full strictness blast radius *and* the `Version` change in one commit. (3) ADR-0009 forbids a production project referencing workspace source until a capability-specific ADR defines the Core render contract and proves the caller |

Recommended commit sequence within this task:

1. TFM + SDK + packages + Dockerfile + scripts (steps 1–7).
2. Determinism seam and golden-string formatting tests (step 8).
3. Strictness, option B (step 9).
4. Lock files, if taken (step 10).
5. Documentation and ADR.

## Documentation edits required

### `docs/operations.md:66` — a factual error

The current row in "What Windows gives this project that Linux does not" says
`scripts/email-eval-desktop` and `CollisionRenderer.Gui` both target
`net10.0-windows` with Windows Forms and WinUI 3 respectively.
`CollisionRenderer.Gui` targets `net8.0-windows10.0.19041.0`. This is wrong today
and becomes doubly wrong once the GUI is deleted. Because the desktop-removal
plan deletes the project, the correct edit is to **remove
`CollisionRenderer.Gui` from the row entirely**, leaving `scripts/email-eval-desktop`
and its Windows Forms justification. If this task lands before desktop removal,
correct the TFM instead and let the removal plan drop the row.

Also review the currency-check block at lines 68–74. It already states .NET 10
LTS through 2028-11-14 and is dated 2026-07-27; the uplift does not change those
vendor facts, but the block explicitly says to refresh them before changing an
SDK or target framework, so record the re-check date.

Line 378's renderer verification command is already correct and needs no change
for the uplift.

### Workspace ADR-0003 supersession

`workspaces/report-renderer/docs/adr/0003-unified-dotnet-8-stack.md` is Accepted
and standardises on `net8.0`, naming each project's TFM including the WinUI host.
The workspace ADR index states the rules plainly: numbers are never reused,
accepted bodies remain unchanged, changed decisions are recorded in a new ADR, and
a superseding ADR must identify the exact decision it replaces.

So:

- **Do not edit ADR-0003's body.**
- Add a new workspace ADR — next free number is `0012` — recording the .NET 10
  uplift. Its scope-of-supersession section must name exactly what it replaces:
  ADR-0003's per-project `net8.0` target framework list. ADR-0003's "one language
  and runtime, shared engine, thin clients" rationale survives intact, and its
  WinUI clause is superseded by the desktop-removal decision, not by this one —
  cross-reference, do not absorb.
- Update the status column for ADR-0003 in the workspace ADR index, add the new
  row, and extend the immutable-index preamble which currently says "The existing
  ADR-0001 through ADR-0010 bodies remain unchanged".

On the root store: root ADR-0010 makes `docs/adr/` the sole **root**
durable-decision store while leaving workspace-local decisions where they are. A
workspace-local runtime uplift is therefore a workspace-local decision and does
**not** need a root ADR. It would need one only if the operator wants the general
rule "every workspace tracks the repository target framework" — a cross-workspace
constraint that belongs at root.

**Coordination note:** if the documentation-migration plan retires the workspace
in the same change set, this workspace ADR-0012 may never need to exist.
Coordinate before writing it.

### `workspaces/README.md` provenance

The register describes each workspace's manifest as the snapshot **at import
time** and then says explicitly that the current tracked tree differs where
post-import repository work has been accepted, "so current file counts come from
`git ls-files`, not from these import records."

So the rule is already written: do **not** regenerate the report-renderer
manifest hash or file count. The "updating a source import requires a reviewed
provenance change and regenerated current manifest" sentence governs re-importing
from upstream, not local edits.

One fact makes this worth an explicit prose edit: `git ls-files
workspaces/report-renderer` currently returns **108 files**, exactly the
manifest's recorded count. This workspace has had *zero* post-import divergence
so far. The uplift is its first, and it is a substantial one. Recommend adding the
uplift to that sentence's list of examples, so a later reader who diffs the tree
against the manifest hash understands why it no longer matches. Leave the register
table, the integration status and the provenance row untouched.

### Workspace docs with stale facts

- `docs/DEVELOPMENT.md:18` names the runtime image `v1.61.0-jammy` and lists GUI
  and MCP package versions. Update the image tag to `v1.61.0-noble`, drop the GUI
  package sentence with the GUI, and update `Microsoft.Extensions.Hosting` to the
  new pin. The prerequisites rows for `Windows` / `CollisionRenderer.Gui` and
  `WebView2 runtime` go with the GUI.
- `docs/ARCHITECTURE.md:168` names `v1.61.0-jammy`. Same correction.
- `README.md` lines 9–17 and 56 describe five shipped projects including the GUI.
  The desktop-removal plan owns those lines; check they were done before merging.
- `NOTICE.md` needs no version change — every listed package version stays the
  same. The Playwright runtime-image note stays true.

## Non-goals and stop conditions

Explicit non-goals:

- Uplifting, fixing, packaging or assessing `CollisionRenderer.Gui`.
- Any Pegasus caller, adapter, `ProjectReference`, `Pegasus.slnx` entry, Core
  render contract, or deployment.
- Moving projects out of `workspaces/`.
- Upgrading `Scriban` off 5.12.1, `Microsoft.Playwright` off 1.61.0, or
  `PDFsharp` off 6.2.4. Each has an accepted, documented reason for its pin.
- Changing template markup, CSS, the design system under `design/`, the
  12-template catalogue, density behaviour, or API authentication.
- Introducing central package management. There is no `Directory.Packages.props`
  anywhere in this repository, at root or in any workspace; every project pins
  inline. Adding CPM for one workspace would be a new repository-wide convention
  and is out of scope. **There is nothing to reconcile against.**
- Moving the workspaces CI job off `windows-latest`, or adding a Linux workspace
  lane.
- Publishing, tagging, or pushing any container image.

Stop conditions — halt and report rather than working around:

1. `dotnet build` on `net10.0` produces a compile error that requires changing
   render semantics rather than syntax. Report the error; do not "fix" output.
2. The pre-uplift control experiment shows rasterised output is *not* stable
   across two runs on the same commit. The parity gate is then invalid and must
   be repaired first.
3. Rasterised parity fails after the uplift on the local Chromium path. That is a
   real rendering regression, not a tolerance question. Do not approve a new
   baseline to make it pass.
4. Restore reports an `NU1605` downgrade or an unresolvable graph after the
   `Microsoft.Extensions.Hosting` bump.
5. The strictness step produces a diagnostic that cannot be suppressed by rule
   and requires a behavioural code change. Stop; that belongs in its own task.
6. The desktop-removal plan has not merged and `CollisionRenderer.Gui` is still
   in the solution. Do not attempt a multi-TFM solution.

## Open questions for the operator

1. **Does the MCP host survive?** If the MCP consolidation plan deletes
   `CollisionRenderer.Mcp`, three items in this plan disappear. Confirm before
   starting.
2. **Strictness now or at relocation?** Recommendation is option B — mirror
   `document-extraction`, in its own commit. Confirm, or accept option C and
   record the deferral in the ADR.
3. **Root ADR or workspace ADR only?** Recommendation is a workspace ADR only. A
   root ADR is warranted only if the durable rule you want is "every source
   workspace tracks the repository target framework", which would also bind
   `document-extraction` and `ai-centre` (both already `net10.0`).
4. **Lock files?** Adding six `packages.lock.json` files brings the renderer in
   line with the other two workspaces and lets CI use `--locked-mode`, at the
   cost of six new tracked files in a workspace that currently matches its import
   manifest exactly.
5. **Scriban.** The uplift does not change the advisory position, but .NET 10
   audits transitive packages by default and the pin is now 20 months old with
   critical advisories against it. Workspace ADR-0010 accepted the risk on the
   basis of first-party embedded templates and encoded values. Confirm that
   acceptance still stands, or open a separate task to move to the 7.x line with
   its own parity evidence. Do not fold it into this one.
6. **Container ownership.** The Dockerfile currently references a base image tag
   that does not exist, which means nobody has built this image recently. Is the
   container in scope as a maintained artefact at all, or should the Dockerfile
   be deleted rather than repaired? If it is maintained, note that the noble
   Playwright image is SDK-based (roughly 2 GB) and a slimmer `aspnet:10.0-noble`
   plus `playwright install --with-deps chromium` runtime stage would be the
   better long-term shape — a separate task.
7. **Font and ICU drift.** Moving the container from Ubuntu 22.04 to 24.04
   changes both the Liberation/DejaVu font versions and the system ICU. There is
   no valid "before" image to compare against. Accept the noble output as a new
   baseline, or require a one-off comparison against `v1.59.0-jammy` (the last
   jammy tag, .NET 8) before switching?
8. **Where does the determinism fix live?** `TimeProvider` works on `net8.0`, so
   step 8 could land ahead of the uplift as its own change. Before, inside, or
   after this task?

## Things that could not be verified

- **A hard minimum `Microsoft.NET.Test.Sdk` version for `net10.0`.** A web search
  surfaced a claim of "18.0 or later", but it could not be corroborated from
  Microsoft Learn or the vstest release notes, and the claim is contradicted by
  this repository: all three root test projects target `net10.0` on 17.14.1 and
  pass CI. The plan therefore recommends 17.14.1 as the repository-proven floor,
  not 18.x. The current published version is 18.8.1.
- **Whether `Microsoft.Extensions.Hosting` is pruned for a `net10.0` console
  project.** The plan says to check the restore output for `NU1510` rather than
  predicting it.
- **Whether the single-file `.mcpb` bundle still finds its `.playwright` driver
  under .NET 10.** Only running the built bundle proves it.
- **Any `net10.0`-specific statement from the Playwright team.** The
  `playwright.dev` .NET Docker page does not mention .NET 10 at all. The evidence
  used here is stronger than a statement anyway: the 1.61.0 image Dockerfile is
  `FROM mcr.microsoft.com/dotnet/sdk:10.0-noble`, and the NuGet package targets
  `netstandard2.0`, which `net10.0` consumes.
- **The exact analyzer diagnostic count under root strictness.** No build was
  run. The estimate should be replaced with a measured number as the first action
  of step 9.
