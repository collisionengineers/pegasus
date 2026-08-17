# 0012 — Decommission the WinUI 3 desktop host

## Status

Accepted

## Context

`CollisionRenderer.Gui` was a complete WinUI 3 / Windows App SDK desktop
application: `net8.0-windows10.0.19041.0`, `UseWinUI=true`, MSIX tooling, a
self-contained Windows App SDK runtime, and two WebView2 controls used to
preview composed HTML and the rendered PDF. It was the only Windows-only
project in `CollisionRenderer.sln`; every other project targeted the
framework-agnostic `net8.0`.

The operator directed on 2026-08-03 that the renderer's desktop/UI elements be
removed. That direction is not a new design decision — the root design
authority already scheduled it. `docs/design/README.md`, in the *Web and renderer
boundary* table, recorded the GUI package assets as:

> Temporary renderer GUI package assets | Linked by
> `workspaces/report-renderer/src/CollisionRenderer.Gui`; remove when that GUI
> is decommissioned during Pegasus integration

Two further design locations named the GUI as a temporary consumer of the
master brand logo. The removal is therefore a scheduled decommission under
pre-existing authority.

Two accepted ADRs describe the GUI as a live deliverable, so a superseding
record is required by this index's rules.

## Decision

Remove `CollisionRenderer.Gui` and its Windows-only dependency surface from the
workspace:

- delete `src/CollisionRenderer.Gui/` and its solution entry, configuration
  mappings and nesting entry;
- delete the linked WinUI/MSIX package assets under
  `docs/design/assets/report-renderer/gui/`;
- drop `Microsoft.WindowsAppSDK` and `CommunityToolkit.Mvvm` from the package
  graph, together with `EnableMsixTooling`, the `Msix` project capability, the
  `Platforms`/`RuntimeIdentifier` pins and the MSIX identity
  `71B58B04-E006-42EA-9C51-D1DB853DDB3A`.

**The HTML preview capability is retained**, on the operator decision of
2026-08-03. `PreviewComposer`, `IPreviewComposer`, `PreviewResult`,
`CollisionRendererFactory.CreatePreviewComposer()` and their tests all stay.
The preview composer lives in `CollisionRenderer.Core` and was only *hosted* by
the GUI; deleting the WebView2 host does not delete the composer. It remains a
library capability with no host surface.

`CollisionRenderer.Cli`, `CollisionRenderer.Api`, `CollisionRenderer.Mcp` and
both test projects are unchanged. No Core source file is deleted.

## Scope of supersession

This ADR supersedes only the details named here. Unmentioned parts of both
older ADRs remain in force, and neither body is edited.

**ADR-0002 — *Modular shared Core with thin CLI/GUI/API clients*.** Superseded:

- the enumeration of the GUI as one of three thin-client hosts (Context, and
  the Decision's naming of `CollisionRenderer.Gui` alongside `.Cli` and
  `.Api`);
- the parity consequence insofar as it names the GUI;
- the *Alternatives considered* rejection of a rendering microservice on the
  grounds that it "would force a network dependency on the offline desktop
  scenario". There is no longer an offline desktop scenario in this workspace,
  so that rationale no longer supports that rejection.

**Not superseded, and still governing:** the shared-Core / thin-client decision
itself, `CollisionRendererFactory` as the single composition root, the
prohibition on hosts special-casing rendering behaviour, and the requirement
that Core stay free of Windows-only dependencies. These now govern Cli, Api and
Mcp.

**ADR-0003 — *Unified .NET 8 stack*.** Superseded:

- the `CollisionRenderer.Gui` — `net8.0-windows`, WinUI 3 / Windows App SDK
  desktop app with a WebView2 preview — bullet in the Decision;
- the Decision's statement that per-project `TargetFramework` "distinguishes
  the WinUI host (`net8.0-windows`) from the framework-agnostic Core/CLI/API".
  No such distinction remains;
- the consequences that depend on the WinUI host: the "first-class native
  Windows desktop app (WinUI 3) *and* a Linux cloud container from one
  codebase" claim, the "GUI hosts Core in-process" clause, and the stated need
  for WinUI 3 / Windows App SDK skills.

**Not superseded by this ADR:** ADR-0003's `net8.0` targets for Core, Cli and
Api, its one-language/one-runtime rationale, and its rejection of Python and of
mixed stacks. The `net8.0` target list is superseded separately by ADR-0014.

## Consequences

- The solution has four shipped projects and two test projects. It contains no
  Windows-only target framework, no Windows App SDK dependency, no
  `RuntimeIdentifier` pin, no `Platforms` matrix and no MSIX capability, so it
  builds on any platform the .NET SDK supports.
- `.github/workflows/workspaces.yml` continues to run on `windows-latest` and
  is **not** changed here. This removal makes a Linux lane *possible*; choosing
  one is a separate decision.
- The `Debug|x64`, `Debug|x86`, `Release|x64` and `Release|x86` solution
  configurations are now vestigial — every remaining project maps all six to
  `Any CPU`. They are deliberately left in place for a smaller, more auditable
  diff.
- `Microsoft.Playwright` is unaffected. The GUI was one of three references;
  Core drives headless Chromium for every PDF render and Mcp references it for
  browser bootstrap.
- The governed document assets under `docs/design/assets/report-renderer/templates/`,
  `docs/design/brand/logos/` and `docs/design/brand/signatures/` are untouched and remain
  live Core build inputs.
- Per-user desktop state written by the deleted `DesktopStateService` under
  `%APPDATA%` on individual developer machines is untracked, machine-local and
  not reversed by reverting this change. It contains no governed asset.
- Whether the MSIX identity has any external registration — Store, MDM,
  internal package feed, or code-signing certificate binding — is **not**
  resolved here. It is deleted from source only; any external retirement is an
  operator action outside this repository.
- A staff-facing preview surface remains unavailable. No capability identifier
  allocates a report preview, and the composed preview HTML is the
  validation-free path, so any future web caller must isolate it in a sandboxed
  frame under a restrictive content security policy and must never interpolate
  it into a page template.
