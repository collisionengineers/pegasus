# Review — PR #388 (SIMPLI-007) — 2026-08-17

Reviewer: independent subagent (no session context) commissioned by claude-code; claude-code implemented and merges. Reviewer ran the coverage harness and read-only probes.

## Changes (reviewer's words)
Gate deleted from Core (marker kept), Web registration and the registration/manifest tests removed; the runner owns offline-candidate validation with the roster read from `docs/capabilities.md` (131 rows at `0.1.0-alpha.1`; DOC-06 absent; the 15 later alpha rows present); every C# blocker code preserved plus real evidence-file re-hashing; env contract removed; acceptance test lane kept (13 tests); docs honest.

## Comments
- N1 [fix-in-PR] gate/capability id matching was case-insensitive (`-notin`, hashtable) → **fixed** `88fcde2a`: ordinal dictionaries + `-cnotin`.
- N2 [fix-in-PR] register column names in the comment → **fixed** `88fcde2a`.
- N3 [note] StrictMode: an observation missing a property throws rather than aggregating a blocker — still fail-closed.
- N4 [note] coverage runs before the evidence directory exists → offline blockers on the console only (consistent with the other prerequisite checks; release blockers are recorded).
- N5 [note] `/diagnostics/version` self-comparison dropped — largely self-referential given `/p:SourceRevisionId`.
- N6 [note] stale hashed-file list fix — good, now called out.

## Plan coverage
All 6 steps DONE. Both planner decisions (delete, not a tooling project; roster derived from the register) confirmed sound.

## Report accuracy
6 files, +279/−516 confirmed at `c9e657c3`; `88fcde2a` adds the two nit fixes.

## Verdict
**PASS.** Merge on green CI; then `verifying`.
