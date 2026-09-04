# Plan — UIIMP-016: Chromium accessibility evidence

## Objective

Make the existing package-pinned Playwright Chromium Browser lane the selected automated accessibility release evidence on Linux, remove the Windows Edge/Narrator gate, and state exactly what the automation does not prove.

## Starting state

Research `1d1000fe6d454fb5` and files `dc6fe96cb0eaad8a` show that the executable Browser lane already covers authenticated routes, axe, keyboard actions, focus, constrained/200%-equivalent width, forced colours and reduced motion. Product and operating docs still require Edge Stable, Narrator and manual review.

## Governing docs

The linked `docs/prd/pegasus-product.md` owns the product quality and is modified under the operator's explicit instruction that Chromium automation replace Edge/Narrator evidence. `docs/frd/frd-12-operator-experience.md` retains the required interface behavior while distinguishing it from the narrower evidence claim. `docs/design/README.md`, `docs/engineering.md`, `docs/runbook.md` and `docs/operations.md` align their downstream acceptance and current-state descriptions.

## Required changes

Replace Windows-only evidence wording with the exact existing Browser lane. Preserve semantic and screen-reader-compatible implementation requirements, but explicitly state that the selected evidence does not prove screen-reader interoperability, subjective usability, complete WCAG conformance or operator acceptance.

## Expected files

- `docs/prd/pegasus-product.md`
- `docs/frd/frd-12-operator-experience.md`
- `docs/design/README.md`
- `docs/engineering.md`
- `docs/runbook.md`
- `docs/operations.md`

## Do not modify

- `src/**`
- `tests/**`
- `scripts/**`
- `infra/**`
- `docs/operator-notes.md`
- `corpus/**`

## Constraints

No new dependency, test framework, UI change or general accessibility claim. Reuse the existing `Category=Browser` command and its pinned Chromium/axe owner. Do not claim that Chromium simulates Narrator.

## Ordered steps

### Step 1 — Align the governing evidence contract

- Files: `docs/prd/pegasus-product.md`, `docs/frd/frd-12-operator-experience.md`, `docs/design/README.md`, `docs/engineering.md`
- Change: define the exact automated behavior and explicit exclusions.
- Tests: documentation links, Markdown placement and targeted terminology search.
- Done when: the governing documents no longer require Windows evidence and do not overclaim automation.

### Step 2 — Align operating state and procedure

- Files: `docs/runbook.md`, `docs/operations.md`
- Change: remove Edge/Narrator workstation and release-gate language; name the package-pinned Browser lane and limitations.
- Tests: Browser lane plus documentation checks.
- Done when: procedure and current state agree with the governing contract.

### Step 3 — Deliver for independent review

- Files: all expected files
- Change: record docs-only simplification, commit, push, open the dev PR and move to Review.
- Tests: locked restore/build, Browser lane, documentation links, Markdown placement and diff check.
- Done when: the PR targets dev and ticket traceability is complete.

## Acceptance checks

No Edge/Narrator release requirement remains. All six documents name Chromium automation and its limitations consistently. Existing Browser evidence passes without test or application changes.

## Commands

Run targeted `rg`, documentation links, Markdown placement, locked restore/build, `dotnet test ... --filter 'Category=Browser'`, and `git diff --check`.

## Failure and deviation rules

Stop if the change would remove required semantic/keyboard behavior, require a new dependency, alter a routed page, weaken a test, or claim screen-reader/WCAG conformance.

## Stop condition

Stop with a docs-only PR open in Review. Do not self-review or merge, and do not start DELIV-047.
