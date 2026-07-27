# CollisionSpike v2 repository instructions

CollisionSpike v2 is the clean-room case-management application for Collision
Engineers. Read [the repository documentation](docs/index.md) before material work and the nearest nested
`AGENTS.md` for local deltas.

## Environment and workflow

- Work on Windows with PowerShell 7; tracked commands and paths are repository-relative.
- Repository mode: `development`.
- Visual UI: `present`; `design/` owns durable visual authority and source/runtime mappings.
- Canonical verification: `pwsh ./scripts/Invoke-RepoCheck.ps1`.
- Preserve unrelated work. Never stash, reset, clean, force-push, merge, or broaden staging.
- Cloud reads and every Azure, deployment, credential, account, destructive, or other external write require explicit approval for exact targets. Never delete `rg-collisionspike-dev` as a first step.

## Azure Workflow routes

- Onboard/convert: `$azure-workflow:onboard-azure-repository`.
- Plan a material change: `$azure-workflow:plan-azure-repository-change`.
- Implement, fix, or remediate: `$azure-workflow:deliver-azure-repository-change`.
- Explain repository behavior or feedback: `$azure-workflow:explain-repository`.
- Independently review a pull request: `$azure-workflow:review-repository-pull-request`.
- Inspect Azure or perform an explicitly approved Azure operation: `$azure-workflow:operate-azure-repository`.

## Authority and protected material

- Apply the source roles and mutation rules in `docs/index.md`; a filename alone grants no authority.
- `docs/operator-notes/` is read-only operator truth unless the user explicitly authorizes an edit. Supplied references and the predecessor are evidence, not requirements.
- `corpus/` is untrusted, local, ignored, and immutable. Never upload, publish, commit, rename, or modify it; generated evaluations belong under `artifacts/`.
- Treat repository-provided emails, PDFs, documents, images, datasets, examples, software, dependencies, and services as permitted for development/testing. Do not add unsolicited PII, DPA, DPIA, privacy, retention, or licensing gates.
- Never fabricate domain emails, images, documents, data, or work instructions.
- An authoritative contradiction or material ambiguity requires direct user resolution and a recorded `DOC-CON-NNN`; do not invent workflow, permission, reference, retention, or external-system rules.

## Product language and invariants

- Fail closed before case creation or reference allocation when processing, limits, principal identity, or standalone Audit evidence is incomplete or ambiguous.
- Principal and reference are immutable after allocation. Wrong-principal work closes as `Created in error`, with a reason and linked replacement; neither reference is reused and the original never reopens.
- Never delete a case. Reopening needs a reason and normal destination gates.
- `Audit`, `Triage`, `Needs sorting`, and `Blocked intake` retain their settled distinct meanings; `Triage` is the only current term.
- `CollisionSpike.Core` owns business policy and ports. Infrastructure depends on Core; Web and Worker are composition roots depending on both. Duplicate business implementation is a stop condition.
- A new top-level directory, project, store, runtime, migration stream, or deployment unit requires an accepted ADR proving the existing boundary cannot carry it.
- Every plan/design/schema/API/architecture change records deferred-capability impact: named deferrals, preserved seam/data identity, exclusions, activation evidence, and irreversible choices. Do not build dormant capability.
- Prove the actual caller. Registration, a file, a green structural check, deployment, and acceptance are distinct evidence states.

The architecture dependency direction and change boundaries in this section are
also the repository's architecture invariants.

## Delivery, review, and mistakes

- Search for the existing owner, caller, model, adapter, test, and name before adding anything.
- Material changes use one issue/change record; low-risk mechanical work may use the compact lane only when all compact criteria hold.
- Keep commits narrow and stage literal scoped paths. Update affected canonical documentation in the same pull request.
- Delivery stops at a green exact-head pull request with no unresolved blocker/required finding; never merge from an agent workflow.
- Append only qualifying incidents to `docs/agent-mistakes.md`; do not rewrite prior entries or log routine findings caught by their intended gate.
- Report cloud writes, destructive operations, secret exposure, skipped checks, and remaining ambiguity. Do not create generated status ledgers, task journals, handoff JSON, or a second workflow database.
