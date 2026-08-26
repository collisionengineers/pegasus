# Post-implementation report — UIIMP-002

## Summary

Added an isolated, double-clickable Test UI catalogue for every current routed Pegasus Razor source. The catalogue classifies all 52 routes, provides 60 page-specific static states for the 39 visual routes, reuses the real Web stylesheet and approved assets, and remains outside application and release inputs.

## Changes

| File | Change | Why |
|---|---|---|
| `docs/design/test-ui/index.html` | Added the canonical machine-readable route inventory and its generated navigation | Keep one route/state owner while making every prototype locally discoverable. |
| `docs/design/test-ui/pages/*.html` | Added 60 standalone replicas covering authenticated, navless-auth, and external shells plus applicable default/empty/validation/stale/partial/unavailable/failed/conflict/access-denied states | Give each visual Razor route a disposable, locally viewable, page-specific design surface without application dependencies. The exact files and source mappings are enumerated by the canonical index. |
| `scripts/Test-UiCatalogue.ps1` | Added route, classification, uniqueness, prototype, local-reference, orphan, and publish-isolation validation | Make route drift and broken/off-boundary catalogue changes fail deterministically. |
| `docs/design/README.md` | Added the Test UI evidence boundary, naming convention, and Live UI separation | Prevent static design evidence from being mistaken for implemented, accepted, or deployed behavior. |

## Governing docs

The ticket has no linked PRD, FRD, or ADR and changes no product behavior. The prototypes follow the existing `docs/design/README.md` shell, component, state, responsive, and accessibility rules and the relevant FRD-12 state vocabulary without modifying either authority. No ADR was required because no new application project, runtime, deployment unit, or policy owner was introduced.

## Risks / follow-ups

- Static replicas can drift inside an existing route; the validator detects route additions/removals and broken assets but cannot prove every markup detail. Review remains responsible for comparing an edited prototype with its current Razor owner.
- The files intentionally do not execute handlers, authentication, validation authority, uploads, downloads, redirects, or business policy.
- [[UIIMP-001]] consumes this catalogue for Test UI mode. [[UIIMP-003]] governs any explicitly approved reintegration into Live Razor pages.
- No deployment is required or performed.

## Verification hand-off

On merged `dev`/the verification branch:

1. Run `./scripts/Test-UiCatalogue.ps1`; expect `52 routed sources, 60 prototypes, 0 broken local references`.
2. Run `dotnet restore ./Pegasus.slnx --locked-mode` and `dotnet build ./Pegasus.slnx --configuration Release --no-restore`; expect zero warnings and zero errors.
3. Open `docs/design/test-ui/index.html` through `file:` and follow every generated link; expect all 60 prototypes and tracked assets to load without an application process.
4. Capture representative authenticated, navless auth, and external shells at 1280×900, at 200% scale/reflow, and in forced-colour mode. Exercise the skip link and keyboard order; every authenticated main target must accept programmatic focus.
5. Confirm no `Pegasus.Web` project or `scripts/Build-ReleaseArtifacts.ps1` input references `docs/design/test-ui`.
