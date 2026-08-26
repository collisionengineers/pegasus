# Plan — UIIMP-004

## Approach

Replace the parallel handwritten UI with deterministic output from the real Razor application. Reuse `IntakeWebApplicationFactory`, `BrowserTestSupport`, existing test fixtures, current layouts/assets and Playwright. One manifest owns the 52 route classifications and 60 selected states; a scenario registry implements only the setup needed to render each named state.

## Governing docs

- Meets `docs/frd/frd-12-operator-experience.md` by capturing the current server-known state, preserving semantic/accessibility markup and validating responsive, zoom, forced-colour and reduced-motion behavior.
- Does not modify FRD-12 or Live UI behavior.

## Steps

1. Convert the embedded inventory into a single JSON manifest that retains source, route, classification, reason, state, output path and scenario key. Generate the index from it.
2. Extend the existing integration browser support only where needed to expose captured post-JavaScript DOM and isolated scenario configuration; do not add a second web host abstraction.
3. Implement the 60 scenario keys using current PageModel conditions, existing repository fixtures/helpers, real invalid requests and existing failure doubles. Record readiness selectors so capture waits for the actual state.
4. Normalize only documented volatile antiforgery/operation/lease/cache/generated values. Rewrite only root-relative application assets and internal routes to local targets. Preserve every other rendered node and attribute.
5. Add an explicit PowerShell update command that captures to a temporary tree, validates it, and replaces generated Test UI files only on success. Ordinary validation captures to temp and byte-compares without tracked writes.
6. Replace all manual prototypes and catalogue HTML with generated output carrying provenance metadata. Update the existing validator to consume the manifest rather than owning another inventory.
7. Add parity tests: all routes/states covered; post-normalization DOM exact; live/offline screenshots identical at standard viewport for all states; representative responsive/200%-zoom/forced-colour/reduced-motion/keyboard/axe checks; negative tests for unauthorized transforms and manual drift.
8. Update design/readme/runbook guidance, correct the prior parity claim through this ticket, run canonical restore/build/focused tests and publish isolation.
9. Run the required simplification pass over the branch diff and record reuse, simplification, efficiency and altitude findings before PR.

## Proof

Verification on merged `dev` reruns clean regeneration, all-state DOM and screenshot parity, focused accessibility checks, restore/build and publish isolation. Proof names exact counts and any environment prerequisites; deployment is `n/a`.

## Risks

- Some states require scoped dependency overrides or real invalid posts; use existing factory composition and doubles rather than HTML mutation.
- Request-specific values must be normalized narrowly; every rule is allow-listed and negative-tested.
- File-origin behavior can differ from HTTP; screenshot comparison catches offline regressions while DOM comparison catches structural drift.
