# Governance and delivery machinery

This category explains how the predecessor repository organised knowledge, tickets, generated views and release work. It is evidence of process history, not a delivery system to reproduce.

## Repository governance files

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`docs/governance/README.md`](../docs/governance/README.md) | Index for old governance documents. | **Predecessor-specific.** |
| [`documentation.md`](../docs/governance/documentation.md) | Old documentation authority, freshness, linking and deletion rules. | **Some hygiene ideas are reusable; authority order is not.** |
| [`decisions-and-reviews.md`](../docs/governance/decisions-and-reviews.md) | Old precedence between reviews, ADRs, tickets and code. | **Conflicts with current v2 source-of-truth if applied directly.** |
| [`repository-data-authority.md`](../docs/governance/repository-data-authority.md) | Rules for old evidence, fixtures, manifests and live data. | **Useful safety prompts only.** Current corpus rules are stricter and authoritative. |
| [`repository-map.md`](../docs/governance/repository-map.md) | Ownership map for the predecessor monorepo. | **Architecture conflict.** v2 has four production projects. |
| [`repository-tree.md`](../docs/governance/repository-tree.md) | Human-readable old/current/proposed repository trees. | **Historical structure only.** |
| [`repository-inventory.json`](../docs/governance/repository-inventory.json) | Generated inventory of predecessor files and ownership. | **Stale generated evidence.** |
| [`repository-reconciliation.json`](../docs/governance/repository-reconciliation.json) | Machine-readable old reset/reconciliation results. | **Historical only.** |
| [`anti-drift-guards.md`](../docs/governance/anti-drift-guards.md) | Old guard register and plan-driven check doctrine. | **Do not recreate automatically.** v2 guards require a concrete invariant and demonstrated failure. |

## Old ticket system entry points

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`docs/tickets/README.md`](../docs/tickets/README.md) | Old ticket lifecycle, work rules and generated plan/status listings. | **Historical status only.** The 308 tickets are not a v2 backlog. |
| [`docs/tickets/BOARD.md`](../docs/tickets/BOARD.md) | Generated predecessor work board. | **No current authority.** Counts and statuses describe the old checkout. |

## Legacy programme plans

| File | Programme described | Current v2 comparison |
| --- | --- | --- |
| [`PLAN-001`](../docs/tickets/plans/PLAN-001-ai-mcp-hardening.md) | AI and MCP hardening/extension. | **MCP planned; AI/vision mostly deferred.** Old architecture and work state are not reusable. |
| [`PLAN-002`](../docs/tickets/plans/PLAN-002-case-done-lifecycle.md) | Detecting and handling case completion. | **Lifecycle planned; terminal meanings and sent-report evidence must use current rules/open decisions.** |
| [`PLAN-003`](../docs/tickets/plans/PLAN-003-operator-fixup-wave.md) | Large set of operator-reported UI/data corrections. | **Historical incident set.** Review tickets individually. |
| [`PLAN-004`](../docs/tickets/plans/PLAN-004-production-readiness.md) | Broad predecessor production-readiness programme. | **Not a v2 release plan.** It mixes potentially relevant business problems with old live-system remediation. |
| [`PLAN-005`](../docs/tickets/plans/PLAN-005-full-remediation-plan.md) | Claimant remediation and repository reconciliation. | **Predecessor-specific.** |
| [`PLAN-006`](../docs/tickets/plans/PLAN-006-repository-structure-documentation-reset.md) | Old repository/documentation reset. | **Directly tied to old architecture.** |
| [`PLAN-007`](../docs/tickets/plans/PLAN-007-server-runtime-foundation.md) | Shared TypeScript server runtime. | **Architecture conflict.** |
| [`PLAN-008`](../docs/tickets/plans/PLAN-008-canonical-service-routes.md) | Canonical routes across old services. | **Architecture conflict; individual external contracts may be reviewed later.** |
| [`PLAN-009`](../docs/tickets/plans/PLAN-009-cloud-estate-cleanup.md) | Cleanup of predecessor cloud resources. | **Separate exact-target operation; never implied by v2 work.** |
| [`PLAN-009.dossier`](../docs/tickets/plans/PLAN-009.dossier.md) | Supporting cloud-cleanup evidence and decisions. | **Historical and potentially stale.** |
| [`PLAN-010`](../docs/tickets/plans/PLAN-010-scripts-and-tooling-dedup.md) | Consolidating old scripts/tooling. | **Predecessor-specific.** |
| [`PLAN-011`](../docs/tickets/plans/PLAN-011-python-doctrine-and-parity.md) | Python packaging and cross-language parity. | **Architecture conflict.** |
| [`PLAN-012`](../docs/tickets/plans/PLAN-012-repository-hardening.md) | Old repository checks and drift guards. | **Review only for proven incidents; do not copy the generated framework.** |
| [`PLAN-012.derivation`](../docs/tickets/plans/PLAN-012.derivation.md) | Derivation/evidence behind PLAN-012. | **Historical process evidence.** |
| [`PLAN-013`](../docs/tickets/plans/PLAN-013-guided-capture-vision-programme.md) | Public guided capture and on-device vision. | **Deferred in v2.** |
| [`PLAN-014`](../docs/tickets/plans/PLAN-014-parse-fed-unified-triage.md) | Reordering old parse/classify/triage stages. | **Some concept overlap; old engine and taxonomy are not approved.** |
| [`PLAN-015`](../docs/tickets/plans/PLAN-015-app-alpha-testing.md) | Old QDOS mailbox alpha. | **Same high-level provider target, different implementation.** Use current v2 acceptance planning only. |
| [`PLAN-016`](../docs/tickets/plans/PLAN-016-inbound-triage-taxonomy-rewrite.md) | Rebuilding old inbound-email categories and precedence. | **Mailbox categorisation remains an open v2 decision.** Useful as questions, not rules. |

## Supporting files inside ticket folders

Every ticket folder is indexed individually in [the complete file index](./file-index.md). Their standard roles are:

- the matching `TKT-*.md` file states the old problem, proposal and acceptance criteria;
- `changes.md` and dated regression variants record old implementation work;
- `verification.md` records old checks, limitations and status;
- `evidence-manifest.json` maps evidence files and provenance;
- `evidence/` contains operator notes, code reads, live snapshots, data exports, queries and remediation scripts.

Those attachments support only the associated predecessor ticket. A green verification file or `done` folder does not establish a current v2 requirement or implementation.
