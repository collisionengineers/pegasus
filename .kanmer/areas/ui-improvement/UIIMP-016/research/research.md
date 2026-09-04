# Research — UIIMP-016: Chromium-only accessibility evidence

## Question

Which existing Windows-specific accessibility release claims must change so Pegasus can use its package-pinned Playwright Chromium lane on Linux without overstating what automation proves?

## Findings

- `docs/prd/pegasus-product.md` makes Microsoft Edge Stable and Windows Narrator a required quality. `docs/runbook.md` repeats them as a Windows-bound release gate. `docs/operations.md` says the Browser lane does not satisfy them.
- The actual Browser lane already uses package-pinned Playwright Chromium and Deque axe-core against authenticated loopback Kestrel callers. `BrowserTestSupport.StartAsync` fixes Chromium, light scheme, reduced motion and viewport and supports forced colours.
- Existing browser tests prove route responses, axe results, semantic landmarks/headings, no inline style regressions, constrained and 200%-equivalent width, forced colours, reduced motion, text-plus-colour state, keyboard activation, skip-link focus, focus movement and no horizontal overflow.
- No test launches Microsoft Edge or Narrator, consumes a Windows accessibility API, or observes spoken output. Chromium's DOM/accessibility semantics and axe results therefore cannot be called Narrator or screen-reader interoperability evidence.
- W3C states that evaluation tools cannot automatically check all accessibility aspects and cannot determine accessibility by themselves. Sources: https://www.w3.org/WAI/test-evaluate/tools/selecting/ and https://www.w3.org/WAI/standards-guidelines/act/implementations/.
- The operator explicitly selected automation-only evidence to eliminate the Windows handoff. EPIC-013 requires the documentation to say that screen-reader coverage is no longer claimed.
- `docs/frd/frd-12-operator-experience.md` and `docs/design/README.md` describe desired screen-reader-support behavior. Removing an evidence claim does not justify removing semantic and screen-reader-compatible implementation requirements; the docs must distinguish behavior targets from the narrower automated acceptance claim.
- `docs/engineering.md` currently says automated axe never replaces manual keyboard or assistive-technology review. That conflicts with the operator's selected release evidence because the repository already automates named keyboard/focus behaviors and no longer requires assistive-technology review.
- No Razor page implementation change is necessary: the discrepancy is between evidence policy and the already-running production-caller browser suite.

## Implications

This is a docs-only policy alignment. The PRD, engineering evidence tier, design acceptance list, runbook and operations snapshot should name the exact Chromium evidence and explicitly exclude screen-reader/Narrator interoperability and general WCAG-conformance claims. Required semantic, keyboard, focus, reflow, forced-colour and reduced-motion behavior remains unchanged and continues to be enforced where the suite has an exact assertion.

No new browser abstraction, dependency, test copy or Windows-specific fallback should be added. The existing `Browser` category and pinned Playwright/axe packages remain the one executable owner.

## Open questions

None. The operator's automation-only decision is explicit; the limitation must be recorded rather than silently inferred away.
