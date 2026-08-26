# Plan — UIIMP-002: Create throwaway HTML replicas of every Pegasus page

## Approach

Create one tracked, offline catalogue under `docs/design/test-ui/`. Its `index.html` is the sole route inventory and classifies every current `@page` source as visual, redirect, download/inline content, or protocol endpoint. Every visual route links to one or more flat HTML state pages that reuse the real shell structure, class names, stylesheet, JavaScript, icons, and approved assets through repository-relative paths. This follows the accepted plain-HTML mockup precedent, avoids a second design system, and stays independently viewable without React, .NET, SQL, or services.

## Governing docs

This chore has no linked PRD, FRD, or ADR and changes no product behavior. The catalogue follows, but does not modify or claim acceptance against, `docs/frd/frd-12-operator-experience.md` and `docs/design/README.md`. The design authority will be updated only to record the Test UI evidence boundary and its non-runtime status; no new ADR is needed because the catalogue is documentation/design material, not a project or deployment unit.

## Steps

1. Enumerate current routed Razor sources and their PageModel results; create the canonical machine-readable list inside `docs/design/test-ui/index.html`, with exactly one route classification and a reason for every non-visual route.
2. Build the catalogue index UI and flat `pages/<route-key>--<state>.html` convention. Make every file open through `file:`, use relative links, and label prototypes as Test UI rather than implemented behavior.
3. For each visual route, reproduce the current rendered shell and semantic page structure using real classes and existing approved fixtures/values. Add only the applicable FRD-12 states—populated, empty, loading/stale/partial/unavailable/failed, validation/conflict/access-denied—without inventing domain material or fake behavior.
4. Reference the tracked `site.css`, compatible portions of `site.js`, inline sprite markup, and approved marks from their existing owners. Where runtime JavaScript requires HTTP/server behavior, represent the static visual state instead of creating prototype business logic.
5. Add `scripts/Test-UiCatalogue.ps1` to compare the canonical list with current `@page` files, reject duplicates/orphans, require a prototype for visual entries, validate linked local files/assets, and prove no application project or publish input references the catalogue.
6. Update `docs/design/README.md` with the Test UI location, evidence limits, naming/state convention, and the rule that approval of a prototype is separate from Live implementation.
7. Run the validator, open every linked state through a local browser, exercise catalogue navigation, inspect representative authenticated/auth/external shells at supported desktop width and 200% zoom, and run documentation/build checks.

## Verification

Proof consists of the validator output showing complete current-route classification, a link/asset pass, browser evidence for every linked prototype, representative keyboard/focus/zoom/forced-colour checks, and a repository/build inspection showing no Test UI reference from `Pegasus.Web` or release inputs.

## Risks / open questions

- Route count will change; the validator derives current truth rather than hard-coding today's 52.
- Static HTML can drift from Razor; using real paths/classes and failing new or removed routes keeps drift visible, while [[UIIMP-003]] governs approved reintegration.
- Some dynamic states cannot function under `file:`; represent their visual state only and never simulate server authority.
- The catalogue is large but remains one coherent unit because coverage and validation are one atomic contract.

## Simplification pass — 2026-08-26

- Reuse: retained the real `site.css`, approved marks, existing shell classes, and Razor-owned page structures; no parallel stylesheet, runtime, component library, or generator was added.
- Simplification: made embedded JSON the single catalogue owner and generated the visible index from it; removed unused inventory metadata and added duplicate prototype/source-state rejection.
- Efficiency: kept one direct PowerShell validator and standalone editable HTML files. No template abstraction was introduced because each disposable page is an independent concrete caller.
- Altitude: the first independent lens correctly rejected generic outlines as underimplementation. Page-specific fidelity passes restored defining forms, tables, controls, actions, state branches, and the shared focus target. The final lens reported no remaining issue after the focus-target correction.
