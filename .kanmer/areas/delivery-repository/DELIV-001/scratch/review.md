# Review — PR #390 (DELIV-001, docs-only) — 2026-08-17

Reviewer: independent subagent (no session context) commissioned by claude-code; claude-code authored and merges.

## Changes (reviewer's words)
`AGENTS.md`: Simplicity rails section + workflow steps 3/4/5 amended; Kanmer-managed block byte-identical. `docs/engineering.md`: `## Simplicity` (four lenses, dispositions, skip rules, balance, scope/timing, fault-handling shape, test support, plan sizing), placed sensibly after Engineering invariants; anchors resolve; links pass.

## Comments
- **B1** [blocking] "No abstraction without a second concrete caller or an external boundary" dropped engineering.md's third permitted reason ("or an accepted ADR") — a silent policy tightening AGENTS.md's precedence would have made binding → **fixed** `dbbf3214`: clause restored + link to `#abstractions-and-deferred-capabilities`.
- N1 [fix-in-PR] "third copy" beside the product invariant "duplicate business implementation is a stop condition" (second copy) → **fixed**: "a second business implementation, or a third copy of anything else".
- N2 [fix-in-PR] plan-sizing / balance / skip-rules rails restated engineering.md mechanics (one sentence verbatim) → **fixed**: one line + anchor each.
- N3 [fix-in-PR] step 4 said "before every PR" but this docs-only PR has no pass → **fixed**: "for a task that changes code … a docs-only task records n/a".
- N4 [note] `/simplify` exact; `code-simplifier` is a plugin agent enabled per-machine — "or equivalent independent lenses" is the escape hatch. Left.
- N5 [note] `docs/index.md` could gain a routing row — not required. Left.
- N6 [ticket→fixed-in-PR] "persist terminal then rethrow" read as FRD behaviour → rephrased as mechanics; operator-visible behaviour pointed at FRD-02 (which already states the failed state is visible). No separate ticket.

## Scope
Diff = 2 files, in-ticket. Managed block identical. Links pass.

## Verdict
**PASS** after `dbbf3214`. Merge on green CI; then `verifying`.
