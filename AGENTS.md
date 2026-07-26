# CollisionSpike v2 agent contract

CollisionSpike v2 is the clean-room case-management application for Collision
Engineers. Build the smallest coherent workflow; do not reproduce the
predecessor's ticket machine, generated ledgers, duplicated engines, dormant
integrations, or speculative layers.

## Start here

1. Read this file and the nearest nested `AGENTS.md`.
2. Resolve claims in the [source-of-truth order](docs/agent-guidance/source-of-truth.md).
   Read only the relevant authority; use [the documentation map](docs/README.md)
   to find it.
3. For a plan, design, schema, API, or architecture change, inspect the
   questionnaire, [feature maturity map](docs/plans/feature-maturity-map.md),
   and [remaining requirements](docs/plans/remaining-requirements.md).
4. Search for the existing owner, caller, model, adapter, test, and name before
   adding anything. State the real caller and evidence that will prove the
   change. Registration is not a caller.
5. Preserve unrelated work. Never treat a dirty worktree as permission to
   restore, clean, move, delete, stage, or edit it.

## Authority, data, and change stops

- `docs/operator-notes/` is read-only operator truth unless the user explicitly
  authorises an edit. [Reference material](docs/reference/README.md) and the
  predecessor are evidence, not requirements or architecture; keep supplied
  sources intact and do not promote a claim without reconciling it through the
  source order.
- An authoritative contradiction or material ambiguity requires direct user
  resolution. Record it in its canonical owner and keep affected work
  reversible; never invent workflow, permission, reference, retention, or
  external-system rules.
- `corpus/` is untrusted, local, ignored, and immutable. Never upload, publish,
  commit, rename, or modify it; put generated evaluations under `artifacts/`.
  Do not fabricate operational emails, images, or work instructions.
- Use Windows and PowerShell 7 for repository workflows. Do not expose secrets
  in source or output; use managed identity/RBAC and approved secret stores.
- Cloud, deployment, credential, account, destructive, or other external writes
  need explicit user authority and exact targets. Never delete
  `rg-collisionspike-dev` as a first step. For Azure work, use
  `$repoplugin-planning:route-collisionspike-azure` and the current Azure docs.

## Product language and invariants

- Fail closed before creating a case or allocating a reference when source
  processing, limits, or standalone Audit type is incomplete or ambiguous.
- After allocation, a principal and reference are immutable. A wrong principal
  closes the case as `Created in error`, with a reason and linked replacement;
  neither reference may be reused.
- Never delete a case. Reopening needs a reason and normal gates; `Created in
  error` never reopens.
- `Audit`, `Triage`, `Needs sorting`, and `Blocked intake` have their settled
  distinct meanings. `Triage` is the only current term; do not create a second
  workflow or use these terms for unrelated concepts.
- Use `$repoplugin-planning:apply-collisionspike-domain` for detailed business
  interpretation. The skill routes to live authorities; it is not itself a
  product-rule source.

## Architecture and evidence

- The approved boundary is `CollisionSpike.Core` for domain rules and ports;
  `CollisionSpike.Infrastructure` depends on Core; and
  `CollisionSpike.Web` and `CollisionSpike.Worker` are composition roots that
  depend on Core and Infrastructure. Core owns business policy called by both
  entry points. Keep one policy owner; duplicate business implementation is a
  stop condition.
- A new top-level directory, project, store, runtime, migration stream, or
  deployment unit needs an accepted ADR and evidence that the existing boundary
  cannot carry it. Follow [architecture](docs/architecture/README.md) and
  [engineering guardrails](docs/agent-guidance/engineering-guardrails.md).
- Every plan, design, schema, API, and architecture change includes a
  Deferred-capability impact: relevant named deferrals, preserved seam or data
  identity, what is excluded now, activation evidence, and any irreversible
  choice. Do not build dormant capability for deferred work.
- Prove the actual entry point. Follow [validation guidance](docs/agent-guidance/validation.md)
  to distinguish planned, implemented, called, locally verified, deployed, live
  verified, and accepted. Run
  `pwsh ./scripts/Invoke-RepoCheck.ps1`; report its repository-consistency
  evidence separately from caller and product evidence.

## Delivery and agent routing

Read [agent routing](docs/agent-guidance/agent-routing.md) before multi-agent
work. Keep one accountable lead and bounded write ownership. Use
`$repoplugin-planning:plan-repository-change` for material planning,
`$repoplugin-implementation:implement-plan-pack` only for an explicitly
requested ready pack, `$repoplugin-review:review-implementation` for
independent review, and the validation/debug skills for evidence or reproduced
failures. The implementation lead calls the real harness `update_plan` before
repository edits or implementation delegation.

For repository documentation, use
`$repoplugin-documentation:bootstrap-repository-documentation`,
`$repoplugin-documentation:maintain-repository-documentation`, or
`$repoplugin-documentation:audit-repository-documentation`. For operator-facing
work, use `$repoplugin-ui-ux:plan-ui-ux-change` and
`$repoplugin-ui-ux:apply-collision-engineers-ui-style`.

Keep commits narrow and stage only scoped paths. Do not edit operator notes in
ordinary work. Report cloud writes, destructive operations, secret exposure,
skipped checks, and remaining ambiguity; do not create generated status ledgers.
