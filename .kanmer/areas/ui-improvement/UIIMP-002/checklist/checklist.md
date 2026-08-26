# Checklist — UIIMP-002

- [x] Enumerate every current `@page` source and classify it once in the canonical catalogue inventory.
- [x] Create the offline `docs/design/test-ui/index.html` navigation and flat state-page convention.
- [x] Reproduce every visual route with current shells, semantic markup, real classes, and approved existing fixtures/values.
- [x] Add every applicable FRD-12 visual state without inventing domain material or browser-side business behavior.
- [x] Wire only repository-relative existing CSS, compatible JavaScript, sprite markup, and approved assets.
- [x] Add `scripts/Test-UiCatalogue.ps1` coverage for route completeness, uniqueness, pages, links/assets, and runtime isolation.
- [x] Update `docs/design/README.md` with the Test UI boundary and evidence limits.
- [x] Run catalogue validation and documentation/build checks.
- [x] Open every linked state and verify navigation/assets; check representative shells for keyboard, focus, supported width, 200% zoom, and forced colour.
- [x] Record verification results for the post-implementation report and proof.

## Progress notes

- 2026-08-26: Classified 52 current routed Razor sources: 39 visual routes and 13 redirect/download routes.
- 2026-08-26: Built 60 offline state prototypes and corrected every body through page-specific fidelity passes against current Razor pages and partials.
- 2026-08-26: The first independent simplification pass rejected generic skeletons; all findings were applied. A second pass identified omitted defining controls on selected pages; those were restored. The final pass found only 27 missing focus targets; all authenticated shells now match the shared layout's focusable main target.
- 2026-08-26: Validator passed with 52 routed sources, 60 prototypes, and zero broken local references. All 61 HTML files opened and captured in headless Chrome. Representative authenticated, navless auth, external, 200% scale/reflow, and forced-colour renders were inspected.
- 2026-08-26: `dotnet restore ./Pegasus.slnx --locked-mode` and `dotnet build ./Pegasus.slnx --configuration Release --no-restore` passed with zero warnings and zero errors; PowerShell parsing and `git diff --check` passed.
