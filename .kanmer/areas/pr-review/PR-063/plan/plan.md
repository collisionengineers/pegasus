# Plan — PR-063

## Approach

Create PR-063's required isolated branch/worktree from the exact head of `task/uiimp-002-test-ui`, then open a stacked PR back into that parent branch. Correct every visual default against its current Razor/PageModel branch, add honest branch evidence to the existing canonical inventory, remove whitespace defects, and update the parent ticket's evidence. This keeps one correction diff and avoids an independent conflicting implementation of UIIMP-002.

## Governing docs

`docs/frd/frd-12-operator-experience.md` requires exact operator state vocabulary, responsive/accessibility support and no false completed state. The plan preserves its state distinctions in the static evidence without changing product behavior. `docs/design/README.md` remains the UI authority and is clarified only for the existing Test UI evidence boundary; no PRD/ADR change is needed.

## Steps

1. Create `task/pr-063-default-fidelity` in `../pegasus-worktrees/pr-063-default-fidelity` from the current UIIMP-002 branch head and record the take.
2. Add a concise branch claim to every visual inventory state; extend the existing validator to require that claim while stating that semantic fidelity remains manual.
3. Correct every default prototype identified in research, reusing current Razor markup, partial structure, exact labels and established repository fixture values. Keep static-only behavior and no parallel CSS/JS/business policy.
4. Preserve the three current layout families and their defining navigation/user controls, with only the explicit Test UI evidence marker.
5. Remove EOF whitespace errors and run the catalogue validator plus a scripted inventory audit proving all 39 default mappings carry branch evidence.
6. Open every catalogue HTML file in headless browser, then inspect representative authenticated/auth/external pages for navigation, keyboard/focus, supported width, 200% zoom and forced colours.
7. Run PowerShell parse, documentation checks, locked restore/build as proportionate regression evidence, and `git diff --check` against the stacked base and `origin/dev`.
8. Run the required four-lens simplification pass over only PR-063's diff and apply behavior-preserving findings.
9. Correct [[UIIMP-002]] checklist/report claims, write PR-063's implementation report, commit/push, open a PR targeting `task/uiimp-002-test-ui`, and move PR-063 to Review without self-review or merge.

## Proof

Review receives the full mapping in research, inventory branch claims, validator output, zero-output whitespace checks, browser/accessibility evidence, build/docs results and a stacked PR whose diff is only the correction.

## Risks and mitigations

- Broad static edits can drift: compare all defaults, not samples, and keep one explicit branch claim per state.
- A static prototype cannot execute Razor mechanics: preserve rendered controls/content and keep the existing non-runtime boundary explicit.
- Stacked topology can become stale: base from the exact parent head and target only the parent branch.
- No abstraction is justified: use the existing inventory, validator, HTML files, real stylesheet and layouts.

## Simplification pass — 2026-08-26

- Reuse: retained the existing standalone HTML files, canonical inventory, validator, real `site.css`, assets and layout vocabulary. Extracting a template/runtime would break the double-clickable boundary.
- Simplification: the inventory's `branch` field is the smallest reviewable source-branch claim; no helper, generator or parallel metadata owner was added.
- Efficiency: the bounded validator scans 52 routed sources and 60 prototypes directly; no cache or combined framework is justified.
- Altitude: README owns the human evidence contract, the index owns route/state facts, and the validator enforces structural presence.
- Independent-lens correctness findings: replaced all 21 generic non-default branch placeholders with concrete PageModel/state conditions and changed the validator wording from “reviewed” to “documented,” because it verifies presence rather than semantic truth.
