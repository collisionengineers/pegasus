# Report renderer desktop removal — draft supporting plan

This is a **draft supporting plan** for the `report-renderer-integration`
task. It is not the task plan itself and it is not a status record. It
covers one bounded piece of work: removing the desktop/UI elements of the
imported report renderer, which the operator has directed explicitly. It
carries no authority over the seam, placement, runtime-uplift, or CI
decisions that the parent task owns.

## Directive and pre-existing authority

The operator has directed removal of the imported renderer's desktop/UI
elements. The design authority already anticipated this and pre-authorised
it. `design/README.md:237`, in the *Web and renderer boundary* table, reads:

> Temporary renderer GUI package assets | Linked by
> `workspaces/report-renderer/src/CollisionRenderer.Gui`; remove when that
> GUI is decommissioned during Pegasus integration

Two further design locations name the GUI as a temporary consumer:
`design/README.md:163` (logo consumers) and `design/README.md:668`
(source-and-runtime map). This plan is therefore a scheduled removal under
existing design authority, not a new design decision.

## What the desktop element actually is

`workspaces/report-renderer/src/CollisionRenderer.Gui` is a complete WinUI 3
/ Windows App SDK desktop application. Verified facts:

| Fact | Value | Evidence |
| --- | --- | --- |
| Target framework | `net8.0-windows10.0.19041.0` | `CollisionRenderer.Gui.csproj:4` |
| Platforms | `x64;ARM64`, default RID `win-x64` | `CollisionRenderer.Gui.csproj:9-10` |
| UI stack | `UseWinUI=true`, self-contained Windows App SDK | `CollisionRenderer.Gui.csproj:12,21` |
| MSIX identity | `71B58B04-E006-42EA-9C51-D1DB853DDB3A` | `Package.appxmanifest:12,16` |
| Embedded browser | two `<WebView2>` controls (HTML preview, PDF preview) | `Pages/DesignPage.xaml:289,315` |
| Local state | `DesktopStateService` persists per-user desktop state | `Services/DesktopStateService.cs` |
| Shell launch | `Process.Start(... UseShellExecute = true)` | `Pages/DesignPage.xaml.cs` |
| Tracked files | 22 | `git ls-files` |

The GUI is the **only** Windows-only project in `CollisionRenderer.sln`.
Every other project — Core, Cli, Api, Mcp, and the two test projects —
targets framework-agnostic `net8.0`.

## Non-negotiable: what must NOT be deleted

Read this section before touching `design/`. A careless reader could delete
`design/assets/report-renderer/` wholesale and destroy governed document
assets.

`design/assets/report-renderer/` contains **two unrelated asset classes**.
Only one of them is being removed.

| Path | Class | Action |
| --- | --- | --- |
| `design/assets/report-renderer/gui/**` | WinUI/MSIX package icons | **DELETE** — 12 files |
| `design/assets/report-renderer/templates/**` | Report Scriban bodies + `report.css` | **KEEP** — governed document assets |
| `design/brand/logos/logo_no_margin.png` | Master brand logo | **KEEP** — checksum-pinned master |
| `design/brand/signatures/**` | Engineer signatures | **KEEP** — provenance-sensitive |

The templates and stylesheet are embedded at build time by
`CollisionRenderer.Core.csproj:21-25`. The logo is embedded by
`CollisionRenderer.Core.csproj:26-29`. The signatures are embedded by
`CollisionRenderer.Core.csproj:30-34`. All three remain live Core build
inputs after the GUI is gone, and the API container build depends on them
(`workspaces/report-renderer/Dockerfile` copies `design/` into the build
context).

`docs/requirements.md:949` governs the signatures:

> Signatures embedded in governed renderer documents are
> provenance-sensitive document assets, not Web decorative imagery.

`design/README.md:235-236` governs the templates and signatures on the same
boundary table row set that authorises the GUI-asset removal. The removal
authority is scoped to the GUI row only.

**Rule for the implementer:** never `rm -r design/assets/report-renderer`.
Delete `design/assets/report-renderer/gui` and nothing else under `design/`.

## Deletion inventory

34 tracked files in total, in two directory trees.

### Workspace: the GUI project (22 files)

Delete the directory `workspaces/report-renderer/src/CollisionRenderer.Gui/`
entirely:

```text
workspaces/report-renderer/src/CollisionRenderer.Gui/.gitignore
workspaces/report-renderer/src/CollisionRenderer.Gui/App.xaml
workspaces/report-renderer/src/CollisionRenderer.Gui/App.xaml.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/Brand/BrandResources.xaml
workspaces/report-renderer/src/CollisionRenderer.Gui/CollisionRenderer.Gui.csproj
workspaces/report-renderer/src/CollisionRenderer.Gui/Converters/BoolToVisibilityConverter.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/MainWindow.xaml
workspaces/report-renderer/src/CollisionRenderer.Gui/MainWindow.xaml.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/Models/DensityOption.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/Models/DesignNavArgs.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/Models/TemplateItem.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/Package.appxmanifest
workspaces/report-renderer/src/CollisionRenderer.Gui/Pages/DesignPage.xaml
workspaces/report-renderer/src/CollisionRenderer.Gui/Pages/DesignPage.xaml.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/Pages/TemplateGalleryPage.xaml
workspaces/report-renderer/src/CollisionRenderer.Gui/Pages/TemplateGalleryPage.xaml.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/Properties/launchSettings.json
workspaces/report-renderer/src/CollisionRenderer.Gui/Services/DesktopStateService.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/ViewModels/DesignViewModel.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/ViewModels/TemplateGalleryViewModel.cs
workspaces/report-renderer/src/CollisionRenderer.Gui/app.manifest
workspaces/report-renderer/src/CollisionRenderer.Gui/ui-tests.ps1
```

Directories removed with it: `Brand/`, `Converters/`, `Models/`, `Pages/`,
`Properties/`, `Services/`, `ViewModels/`.

`Models/DensityOption.cs` is the GUI-local density picker model. It lives
inside the GUI project, not in Core, so it disappears with the directory. It
is not a Core deletion decision.

### Monolith: the GUI package assets (12 files)

Delete the directory `design/assets/report-renderer/gui/` entirely:

```text
design/assets/report-renderer/gui/AppIcon.ico
design/assets/report-renderer/gui/LockScreenLogo.scale-200.png
design/assets/report-renderer/gui/SplashScreen.scale-200.png
design/assets/report-renderer/gui/Square150x150Logo.scale-200.png
design/assets/report-renderer/gui/Square44x44Logo.scale-200.png
design/assets/report-renderer/gui/Square44x44Logo.targetsize-24_altform-unplated.png
design/assets/report-renderer/gui/Square44x44Logo.targetsize-48_altform-lightunplated.png
design/assets/report-renderer/gui/StoreLogo.png
design/assets/report-renderer/gui/Wide310x150Logo.scale-200.png
design/assets/report-renderer/gui/brand/mark-32.png
design/assets/report-renderer/gui/brand/mark-48.png
design/assets/report-renderer/gui/brand/mark.png
```

Use the live `git ls-files` count at implementation time, not this figure.

The sole consumer of this directory is `CollisionRenderer.Gui.csproj:26,30`
(the `<ApplicationIcon>` property and the linked `<Content>` glob). No other
tracked file in the repository references
`design/assets/report-renderer/gui`. Verified by grep across `*.md`,
`*.csproj`, `*.json`, `*.ps1`, `*.yml`.

Note that `CollisionRenderer.Gui.csproj:33-35` also links
`design/brand/logos/logo_no_margin.png`. Removing the GUI removes that link;
the master file itself stays, because Core embeds it independently.

**Deletion count: 34 tracked files, 2 directory trees.**

## Edit inventory

### 1. `workspaces/report-renderer/CollisionRenderer.sln`

Three separate removals.

| Location | Current | Change |
| --- | --- | --- |
| Line 18-19 | `Project(...) = "CollisionRenderer.Gui", ...` + `EndProject` | Delete both lines |
| Lines 82-93 | 12 `{9D42585F-7F2A-48C9-BA18-A53B51BE5550}.*` mappings | Delete all 12 |
| Line 127 | `{9D42585F-...} = {827E0CD3-...}` | Delete the nesting line |

Do **not** remove the `src` solution folder.
`{827E0CD3-B72D-47B6-A68D-7590B98EB39B}` still contains Core, Cli, Api and
Mcp (`CollisionRenderer.sln:123-125,128`). Only the GUI's nesting entry goes.

The GUI is the only project whose mappings resolve to `Debug|x64` /
`Release|x64`; every other project maps all six solution configurations to
`Any CPU`. After the deletion the six `SolutionConfigurationPlatforms`
entries at lines 26-31 become vestigial but remain internally consistent.
Recommendation: **leave lines 26-31 unchanged** to keep the diff reviewable.
See open question 3.

### 2. `design/README.md`

Five lines across three named locations.

| Line | Current text | Change |
| --- | --- | --- |
| 163 | `- linked by ...CollisionRenderer.Gui;` | Delete the bullet; ensure line 162 punctuation continues into 164 correctly |
| 234 | `Master logo \| Renderer Core and temporary renderer GUI today; approved source for a reviewed future Web copy` | Drop `and temporary renderer GUI today`, leaving `Renderer Core today` |
| 237 | Whole `Temporary renderer GUI package assets` row | Delete the row |
| 665 | `... \| Renderer Core, temporary renderer GUI, and the checksummed Web copy embedded by _Layout.cshtml` | Drop `temporary renderer GUI, ` |
| 668 | Whole `Temporary renderer GUI assets` row | Delete the row |

The GUI appears twice more inside table cells (234 and 665) that a naive
grep does not surface as separate locations.

After these edits, `rg -n "CollisionRenderer\.Gui|renderer GUI" design/` must
return nothing.

### 3. `docs/operations.md`

Line 66, the *What Windows gives this project that Linux does not* table.
Two defects are fixed by one edit:

- the GUI is being deleted, so it must leave the row;
- the stated TFM is wrong. The row claims both target `net10.0-windows`.
  `scripts/email-eval-desktop/Pegasus.EmailEvaluation.Desktop.csproj:4` does
  target `net10.0-windows`; `CollisionRenderer.Gui.csproj:4` targeted
  `net8.0-windows10.0.19041.0`. The error disappears with the GUI, but the
  replacement row must not inherit it.

Replace the row with a `scripts/email-eval-desktop`-only row that states
`net10.0-windows` and Windows Forms. Do not add a WinUI clause; after this
change the repository contains no WinUI code.

No other line in `docs/operations.md` mentions the GUI. Lines 52-53, 378 and
383 reference the renderer workspace generally and stay unchanged.

### 4. `workspaces/report-renderer/docs/ARCHITECTURE.md`

| Line | Change |
| --- | --- |
| 13 | `five shipped projects and two test projects` → `four shipped projects and two test projects` |
| 19 | Delete the `CollisionRenderer.Gui` project-graph row |
| 28 | Delete the `- GUI: Microsoft.WindowsAppSDK 2.2.0, CommunityToolkit.Mvvm 8.4.2 ...` bullet |
| 124-126 | Delete the whole `### GUI` subsection under *Host surfaces and parity* |

Line 17's Core boundary claim — "No Windows-only dependency" — becomes
trivially true for the whole solution. See open question 4.

### 5. `workspaces/report-renderer/docs/DEVELOPMENT.md`

The largest documentation change. Nine locations:

| Line(s) | Change |
| --- | --- |
| 13 | Delete the `Windows \| CollisionRenderer.Gui build/run` prerequisite row |
| 14 | Delete the `WebView2 runtime \| GUI preview` prerequisite row |
| 18 | Delete the sentence naming the GUI packages |
| 39-56 | Collapse the Windows-only and cross-platform build sections into one `### Build` body; the solution now builds on any platform |
| 58-66 | Delete the whole `### GUI only` subsection |
| 89-93 | Remove the `Or, on a platform that can build every project in the solution:` qualifier — `dotnet test CollisionRenderer.sln` is now unconditional |
| 203-219 | Delete the whole `## Run the GUI` section |
| 265 | Delete host-parity step 4 and renumber step 5 to 4 |
| 292-294 | Delete the `### GUI build fails on non-Windows` troubleshooting entry |

### 6. `workspaces/report-renderer/README.md`

| Line | Change |
| --- | --- |
| 3 | `serves the command line, Windows GUI, HTTP API and MCP host` → remove `Windows GUI, ` |
| 9 | `five shipped projects` → `four shipped projects` |
| 13 | Delete the `src/CollisionRenderer.Gui — WinUI 3 desktop authoring and preview host.` bullet |
| 56 | Delete the sentence about the Windows-only GUI and per-project builds |

### 7. `workspaces/report-renderer/NOTICE.md`

| Line | Change |
| --- | --- |
| 14 | Delete the `Microsoft.WindowsAppSDK 2.2.0` row |
| 15 | Delete the `CommunityToolkit.Mvvm 8.4.2` row |
| 76 | `the same care as CLI, GUI or API output` → `the same care as CLI or API output` |

The brand-asset, provenance, privacy and security sections are unaffected.
Line 35's statement about the logo and signatures remains correct and must
not be weakened.

### 8. `workspaces/report-renderer/Directory.Build.props`

Line 5 comment names the WinUI GUI target framework. Rewrite to drop the
WinUI clause. This is a comment only; no build property changes.

### 9. New ADR in the renderer workspace

`workspaces/report-renderer/docs/adr/README.md` states the index rules
explicitly:

> - Accepted ADR bodies are historical records and remain unchanged.
> - Corrections or changed decisions are recorded in a new ADR, not by
>   rewriting an old body.
> - A superseding ADR must identify the exact decision or detail it
>   replaces. Unmentioned parts of an older ADR remain in force.

Two accepted ADRs are contradicted by this removal:

- `0002-modular-shared-core-thin-clients.md` — its title and its lines 9-13,
  27-29 and 48-49 name the GUI as one of three thin clients and reject a
  network-only design specifically to protect "the offline desktop
  scenario".
- `0003-unified-dotnet-8-stack.md` — its lines 23-24, 28 and 36-38 record
  `net8.0-windows` WinUI 3 as a first-class deliverable and justify the
  stack choice partly by the desktop requirement.

**Do not edit either body.** Add a workspace ADR recording that the WinUI 3
desktop host is decommissioned on operator direction under the pre-existing
`design/README.md:237` authority, and stating precisely which details it
supersedes:

- ADR-0002: only the enumeration of the GUI as a host and the offline
  desktop rationale in *Alternatives considered*. The shared-Core /
  thin-client decision itself is **unchanged** and still governs Cli, Api
  and Mcp.
- ADR-0003: only the `CollisionRenderer.Gui` / `net8.0-windows` bullet and
  the consequences that depend on it. The `net8.0` target for Core, Cli and
  Api is **unchanged**.

Add the corresponding row to the immutable index table in the workspace
`docs/adr/README.md` and update its preamble sentence, which currently reads
"The existing ADR-0001 through ADR-0010 bodies remain unchanged."

The docs-migration plan may decide this workspace ADR never needs to exist,
if the workspace is retired in the same change set. Coordinate before
writing it.

### 10. `workspaces/README.md` — no edit required

Lines 37-42 already govern divergence from the import snapshot:

> Each manifest describes the snapshot **at import time**. The current
> tracked tree differs where post-import repository work has been accepted
> ... so current file counts come from `git ls-files`, not from these import
> records.

The `report-renderer/` provenance row (line 25) records the import snapshot
and is **not** to be recalculated. Recalculating it would falsify the
provenance record. The divergence created by this removal is exactly the
case the paragraph anticipates. Confirm this reading in the task PR rather
than editing the row.

### 11. Optional / discretionary

- `workspaces/report-renderer/.gitattributes:14` (`*.xaml text eol=lf`)
  becomes dead after the deletion; zero `.xaml` files remain in the
  workspace. Harmless. Leave it unless the reviewer objects.
- MSIX-related ignore patterns live only in the deleted GUI `.gitignore`;
  the workspace root `.gitignore` has no GUI-specific entries. No change.
- `workspaces/report-renderer/.dockerignore` — no change.
- `workspaces/report-renderer/Dockerfile` — no change. `COPY design/
  design/` remains correct and now copies a smaller tree.

## Shared-code decision table

Every candidate file assessed against **all** callers found by grep across
the workspace.

| File | GUI-only? | Other callers | Verdict |
| --- | --- | --- | --- |
| `Core/AuthoringCatalog.cs` (819 lines) | No | `Cli/Program.cs:91,106,115,124`; `Api/Program.cs:49,53-54,60-61`; `Core.Tests/CoreTests.cs:36,54,71,74-75,574`; `Core.Tests/IntegrationTests.cs:27`; `Core.Tests/PreviewAndStarterTests.cs:49,65,73,82,137,148,157`; `Mcp.Tests/RenderToolsTests.cs:48` | **KEEP** |
| `Core/StarterComposer.cs` (167 lines) | No | `AuthoringCatalog.cs:147` (`StarterComposer.Wash`) | **KEEP** |
| `Core/PlaceholderScanner.cs` (85 lines) | No | `StarterComposer.cs:14-15`; `Contracts.cs:163`; `Core.Tests/PreviewAndStarterTests.cs:14-36,52` | **KEEP** |
| `Core/JsonPath.cs` (184 lines) | No | `Api/Program.cs:176,340` (`/v1/render.multipart`); `StarterComposer.cs:80,94,100,117,156,162`; `Core.Tests/JsonPathTests.cs` (whole file) | **KEEP** |
| `Core/PreviewComposer.cs` + `CollisionRendererFactory.CreatePreviewComposer()` | Production: **yes** | Production: none after the GUI goes. Tests: `Core.Tests/PreviewAndStarterTests.cs:109` (class `PreviewComposerTests`, 5 tests) | **DEFER** |
| `Gui/Models/DensityOption.cs` | Yes | None outside the GUI project | **DELETE** |

### Why `PreviewComposer` is deferred, not deleted

`PreviewComposer` is validation-free, Chromium-free HTML composition. After
the GUI is removed its only remaining exercise is `PreviewComposerTests`. It
is **production-orphaned but test-covered**.

It is deferred rather than deleted for three reasons:

1. It is Core library code, not desktop/UI code. The operator directive is
   scoped to desktop/UI removal. Deleting a Core public contract
   (`IPreviewComposer`, `PreviewResult`) plus a factory method plus five
   tests is a different change with a different review question.
2. A fast, validation-tolerant HTML preview is a plausible input to the
   seam/placement plan — a Pegasus Web preview surface would want exactly
   this and would not want Chromium in the request path.
3. Keeping it costs nothing at build time and its tests keep passing.

Record the orphaning explicitly in the task PR so the seam/placement plan
inherits the decision rather than rediscovering it.

### Api authoring endpoints: dead weight assessment

`CollisionRenderer.Api` exposes three authoring endpoints
(`Api/Program.cs:48-63`): `GET /v1/authoring-templates`,
`GET /v1/authoring-templates/{id}/form` and
`GET /v1/authoring-templates/{id}/blank`.

These were plausibly shaped for a form-driven client. They do **not** become
dead weight, because `CollisionRenderer.Cli` independently exposes the same
catalogue through `forms list`, `forms blank`, `forms schema` and
`forms starter` (`Cli/Program.cs:91,106,115,124`), and the Api endpoints have
no dependency on the GUI. They are flagged here only so the seam/placement
plan can decide whether an HTTP host is wanted at all.

`Cli` and `Api` are console and minimal-API hosts respectively. Neither is
desktop or UI: the Api has no `wwwroot`, no Razor, no static files, and
returns only JSON and PDF bytes. **They are out of scope for this plan.**
Their fate is decided by the separate seam/placement plan.

`CollisionRenderer.Mcp` is a stdio MCP host, likewise out of scope, and is
owned by the separate MCP consolidation plan. It must not be disturbed here.

## Package removals

| Package | Version | Referenced by | After removal |
| --- | --- | --- | --- |
| `Microsoft.WindowsAppSDK` | `2.2.0` | Gui only (`csproj:47`) | Gone from the graph |
| `CommunityToolkit.Mvvm` | `8.4.2` | Gui only (`csproj:48`) | Gone from the graph |
| `Microsoft.Playwright` | `1.61.0` | Core (`csproj:14`), Gui (`csproj:49`), Mcp (`csproj:17`) | **Still required** |

`Microsoft.Playwright` stays. Verified: Core drives headless Chromium for
every PDF render, and Mcp references it directly for browser bootstrap.
Removing the GUI removes one of three references, not the dependency.

The `Microsoft.WindowsAppSDK` removal is what eliminates the last
Windows-only NuGet package and the last MSIX tooling hook
(`EnableMsixTooling`, `<ProjectCapability Include="Msix" />`) from the
solution.

`PDFsharp 6.2.4`, `Scriban 5.12.1`, `ModelContextProtocol 1.4.0` and
`Microsoft.Extensions.Hosting 9.0.0` are unaffected.

## Verification plan

Mapped to the evidence tiers in `docs/operations.md`.

**Applicable tier: 1 — Static/build/architecture.** No other tier applies.
The renderer workspace has no Pegasus caller, no route, no persisted result,
no Core policy owner and no operator-visible result
(`workspaces/README.md:16`), so tiers 2-12 are not implicated by this
change. State that explicitly in the PR rather than leaving it inferred.

### V1 — Solution restores, builds and tests without the GUI

From `workspaces/report-renderer/`:

```sh
dotnet restore CollisionRenderer.sln
dotnet build CollisionRenderer.sln -c Release --no-restore
dotnet run --project src/CollisionRenderer.Cli -c Release --no-build -- install-browser
dotnet test CollisionRenderer.sln -c Release --no-build
```

Expected: six projects build (Core, Cli, Api, Mcp, Core.Tests, Mcp.Tests).
Test totals must not be recorded in tracked documentation
(`docs/DEVELOPMENT.md:95`); the discovery output is the current count.

### V2 — The solution now builds on Linux

The removal's whole point. Run V1 unchanged on a Linux host. This is evidence
*for* the runtime-uplift plan, not a change owned here.

### V3 — No dangling solution GUID

```sh
rg -n '9D42585F-7F2A-48C9-BA18-A53B51BE5550' workspaces/report-renderer/
dotnet sln workspaces/report-renderer/CollisionRenderer.sln list
```

The first must return nothing. The second must list exactly six projects and
must not error.

### V4 — No remaining reference to deleted paths

Each of these must return **no matches**:

```sh
rg -n --hidden -g '!.git' 'CollisionRenderer\.Gui' .
rg -n --hidden -g '!.git' 'design/assets/report-renderer/gui' .
rg -n --hidden -g '!.git' 'WindowsAppSDK|WindowsAppSdk' .
rg -n --hidden -g '!.git' 'CommunityToolkit\.Mvvm' .
rg -n --hidden -g '!.git' 'WebView2' .
rg -n --hidden -g '!.git' 'appxmanifest|71B58B04-E006-42EA-9C51-D1DB853DDB3A' .
```

Two known allowable survivors, which must be inspected individually rather
than assumed:

- `workspaces/ai-centre/docs/adr/0002-windows-desktop-stack.md:16` mentions
  WinUI 3 as an unexercised option in a different workspace. Unrelated.
  Leave it.
- Any new renderer ADR and its index row will legitimately contain the
  string `CollisionRenderer.Gui` as a historical record. Exclude
  `workspaces/report-renderer/docs/adr/` from the first pattern, or verify
  the hits are the new ADR only.

And confirm the retained assets survived:

```sh
git ls-files design/assets/report-renderer/
git ls-files design/brand/
```

The first must list exactly the five `templates/` files (four `.scriban` plus
`report.css`) and nothing under `gui/`. The second must still list
`logos/logo_no_margin.png` and all three signature PNGs.

### V5 — Rendered PDF output is byte-identical

The GUI contributed nothing to PDF production (`docs/ARCHITECTURE.md:126`:
"Preview is a GUI-only concern implemented with WebView2; it does not change
PDF production"). Prove it rather than assert it.

On one machine, in one session, **before** the change:

```sh
dotnet run --project src/CollisionRenderer.Cli -- forms starter \
  --template market-valuation-evidence --out /tmp/val.json
dotnet run --project src/CollisionRenderer.Cli -- render \
  --template market-valuation-evidence --data /tmp/val.json --out /tmp/before.pdf
```

Repeat for `fee-note`, `expert-report` and `advert-evidence-pack`. Record
each `RenderResult` SHA-256 as reported by the CLI. Then apply the change,
rebuild, and render the **same** JSON files to `after.pdf`. Compare
SHA-256s.

Constraint from `docs/DEVELOPMENT.md:268`: byte identity is only a valid
comparison under equivalent Chromium, font, OS and attachment conditions.
Because before and after run on the same machine in the same session with
the same pinned Playwright revision, that condition holds. Do not carry these
hashes into tracked documentation or across machines.

Any divergence is a **stop condition** — it would mean the GUI was not
actually output-neutral, and the removal must halt for operator review.

### V6 — CI

`.github/workflows/workspaces.yml` runs `runs-on: windows-latest` and builds
the whole `CollisionRenderer.sln`. It keeps passing unchanged after this
removal; the solution simply has one fewer project.

**Do not change the CI OS in this plan.** Removing the GUI is what makes a
Linux job *possible*, and that is a real dependency the runtime-uplift plan
should record — but the change belongs there, not here. Note the dependency
in the PR body and stop.

## Rollback

This is a pure deletion with documentation edits. It carries no migration, no
schema change, no deployed artefact and no external side effect.

- The work lands as a **single reviewable commit on a task branch** cut from
  `dev`, per `docs/engineering.md:11`.
- Reversal is `git revert <sha>` on that single commit. Deleted binary assets
  are restored intact from the object store; no re-export from an upstream
  design bundle is needed.
- Recovery before merge is `git restore` from `origin/dev`.
- No operator action, service restart, cache purge or re-deployment is
  required in either direction, because nothing in Pegasus references the
  GUI.

Residual state not reversed by `git revert`, and out of scope: per-user
desktop state written by `DesktopStateService` under `%APPDATA%` on
individual developer machines, and any `artifacts/gui-ui-tests` output from
`ui-tests.ps1`. Both are untracked, gitignored, machine-local, and contain no
governed asset.

## Non-goals

This plan does **not**:

1. delete, move, rename or restructure `CollisionRenderer.Cli`,
   `CollisionRenderer.Api`, `CollisionRenderer.Mcp`, or either test project;
2. delete any Core source file;
3. delete `design/assets/report-renderer/templates/**`, `report.css`,
   `design/brand/logos/logo_no_margin.png` or `design/brand/signatures/**`;
4. change `.github/workflows/workspaces.yml`, including its runner OS;
5. change the renderer's target framework, SDK pin, `global.json`, or
   `Directory.Build.props` build properties (the only props change is a stale
   comment);
6. change the `report-renderer/` provenance row or SHA-256 in
   `workspaces/README.md`;
7. change the workspace integration status in `workspaces/README.md` — the
   workspace is still "Planned integration — no Pegasus caller, deployment,
   or acceptance" after this change;
8. decide where the renderer ultimately lives, what its Pegasus seam is, or
   whether the Api host survives;
9. edit any accepted ADR body;
10. claim a Pegasus caller, deployment, or acceptance.

## Stop conditions

Halt and return to the operator if any of these occur:

- **S1.** V5 shows any PDF SHA-256 differing before and after. The GUI was
  not output-neutral; the removal's premise is wrong.
- **S2.** A grep in V4 finds a `design/assets/report-renderer/gui` reference
  in a file this plan does not list. Something else consumes the assets and
  was not accounted for.
- **S3.** Removing the GUI breaks the build of any remaining project. That
  would mean an undiscovered inbound dependency on GUI code, which
  contradicts ADR-0002's thin-client model.
- **S4.** Any deletion touches a path under
  `design/assets/report-renderer/templates/`, `design/brand/logos/` or
  `design/brand/signatures/`. Governed document assets; revert immediately.
- **S5.** The reviewer disputes the ADR supersession scope. ADR bodies are
  immutable and the index is an authority; do not force it.
- **S6.** The work starts to expand into the Api or Cli. That is the
  seam/placement plan's territory.

## Open questions for the operator

1. **`PreviewComposer` disposition.** Keep it as Core capability for a future
   Pegasus preview surface (this plan's recommendation), or delete it with
   its five tests now that it has no production caller?
2. **MSIX identity retirement.** `71B58B04-E006-42EA-9C51-D1DB853DDB3A` is
   deleted from source. Is there any external registration — Store, MDM,
   internal package feed, code-signing certificate binding, installed
   machines — that needs a separate retirement action outside this
   repository?
3. **Solution configuration platforms.** After the GUI goes, the `Debug|x64`,
   `Debug|x86`, `Release|x64` and `Release|x86` solution configurations serve
   no project that behaves differently from `Any CPU`. Simplify the solution
   to `Debug|Any CPU` / `Release|Any CPU` in the same commit, or leave the
   vestigial entries for a smaller, more auditable diff?
4. **Core boundary statement.** `docs/ARCHITECTURE.md:17` currently says Core
   has "No Windows-only dependency". With the GUI gone the whole solution is
   Windows-free. Strengthen that to a solution-level invariant now, or leave
   the wording for the runtime-uplift plan that will assert it as a CI gate?
5. **Ordering.** Should this removal land as its own PR ahead of the
   seam/placement work, so the runtime-uplift plan can immediately assume a
   Linux-buildable solution — or be folded into a larger
   `report-renderer-integration` change?
6. **Provenance reading.** Confirm the reading in section 10 of the edit
   inventory: the import manifest row stays untouched, and
   `workspaces/README.md:37-42` already covers this divergence. No
   recalculated hash.
