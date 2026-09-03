# Current auto run — EPIC-012

Run record: `automation/runs/20260902T203000Z-claude-fable.md`. Status:
**running** — resumed on 2026-09-03 after the operator answered the five
parked questions.

In flight:

- **Prepare-3** (Workflow run `wf_4e195065-fc8`) — ENG-036 plan and checklist,
  CASE-043 full pipeline; terra writes, sol xhigh reviews, Opus dispositions.
- **Build wave 1** (Workflow run `wf_82493dad-cf6`) — PLAT-070 → DOCS-017 →
  PLAT-068 → ENG-035 → AUTO-018, all serial because every lane touches a
  capacity-one shared-lock path (migrations, OperatorLabels, Cases/Shared,
  Details.cshtml). Implementers gpt-5.6-sol medium under Sonnet wrappers;
  gpt-5.6-terra xhigh reviews each PR with Opus dispositions and the merge to
  `dev`.

Handoff document: `C:\Users\PC\Downloads\Pegasus_EPIC-012_handoff.md` (its §4
now records the answers). Workflow scripts:
`C:\Users\PC\Downloads\Pegasus_EPIC-012_workflows\`.

Remaining after wave 1: wave 2 CASE-038; wave 3 CASE-039, CASE-040, CASE-041,
CASE-029, CASE-042 (blocked by CASE-032), PLAT-069, CASE-009, then ENG-034
serial last; wave 4 ENG-036, ENG-031, ENG-029, DOCS-018, CASE-043 serial;
wave 5 UIIMP-014 → DELIV-030, then the adversarial claims.

Git at resume: `origin/dev` 07ac7f1b; `origin/main` 32f8679d. `main` is two
commits ahead of `dev` through direct pushes (test material, a skills merge).
Lanes branch from `origin/dev` and must not reconcile that divergence; it is
an administrator action.

Kanmer MCP is healthy again (server 0.4.0, sha efe89029); the disk-read
workaround is retired.
