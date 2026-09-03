# Current auto run — EPIC-012

Run record: `automation/runs/20260902T203000Z-claude-fable.md`. Status:
**running** — resumed on 2026-09-03 after the operator answered the five
parked questions.

Handoff document (still current for tooling and context):
`C:\Users\PC\Downloads\Pegasus_EPIC-012_handoff.md`. Its §4 open questions
are all answered and are superseded by D47–D50 in `context.md`. Workflow
scripts: `C:\Users\PC\Downloads\Pegasus_EPIC-012_workflows\`.

State: seventeen tickets ready to leave Preparing (PLAT-070, DOCS-017,
PLAT-068, ENG-035, AUTO-018, CASE-038, CASE-039, ENG-034, CASE-040,
CASE-041, CASE-029, CASE-042, PLAT-069, CASE-009, ENG-031, ENG-029,
DOCS-018, UIIMP-014). ENG-036 and the new CASE-043 are in Prepare-3
(Workflow run `wf_4e195065-fc8`). Build wave 1 starts when Prepare-3
returns.

Git at resume: `origin/dev` 07ac7f1b; `origin/main` 32f8679d. `main` is two
commits ahead of `dev` through direct pushes (test material, a skills
merge). Lanes branch from `origin/dev` and must not reconcile that
divergence; it is an administrator action.

Kanmer MCP is healthy again (server 0.4.0, sha efe89029); the disk-read
workaround is retired.
