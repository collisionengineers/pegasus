# Root plan — PRD/FRD/ADR documentation architecture (SIMPLI-002/004/005/006)

Branch `task/retire-now-rewrite-agents`; worktree
`../pegasus-worktrees/retire-now-rewrite-agents` from `origin/dev`. Claim is
recorded in Kanmer (`take_ticket`), not in `NOW.md`. Approved 2026-08-13.

## Core principle (do not violate)

> **ADRs record durable technical/architectural PRODUCT decisions only.**
> Repository rules, conventions, and process — the documentation model, the
> PRD/FRD/ADR definitions and routing, ADR-authoring conventions, and file
> placement — are governance and live in `AGENTS.md` (with `docs/index.md` as the
> navigation index + authority chain). No meta-ADR "authorises" the taxonomy or
> defines how ADRs are written.

Consequence: the documentation-governance ADRs **0010** and **0023** are the
mistake; their still-true rules move to `AGENTS.md`/`docs/index.md` and the files
become superseded tombstones — the same relocation applied to mis-filed
*functional* ADRs (0012/0020 → FRD).

This **supersedes** `docs/temp-plans/simplify/adr-consolidation.md` (which
renumbered 24 ADRs → `0001–0009`). ADR IDs stay **stable**; we consolidate by
superseding + indexing, never renumbering.

## Decisions locked (user, 2026-08-13)

- Full decomposition now: `requirements.md` → 1 PRD + 12 FRDs; split the 8 mixed
  ADRs; move 0012/0020 → FRD.
- Retire `requirements.md`; PRD tier inherits its "intent" authority.
- Per-domain granularity: ~1 PRD + ~12 FRDs, keyed to capability IDs.
- Authority chain: `operator-notes.md > PRD > FRD > capabilities.md > ADR >
  architecture.md/operations.md > runbook.md/engineering.md/design.md`.
- Kanmer is the claim mechanism (no NOW.md line); AGENTS.md gains a
  read-only-Azure-permitted rule; prod Worker is enabled (live-verified
  2026-08-13) → `operations.md` takes that value; `operator-notes.md` protected;
  `design.md` stays UI/design authority; `capabilities.md` stays the ID registry.

## Definitions (written into AGENTS.md; indexed in docs/index.md)

- **PRD** (`docs/prd/`) — what & why: business need, users, outcomes, scope,
  permanent boundaries, quality/capacity targets, acceptance model. No mechanics.
- **FRD** (`docs/frd/`) — how a capability must behave: I/O, states, rules, edge
  cases, fail-closed behaviour, acceptance evidence. Implements a PRD outcome;
  cites `design.md` for UI. Never invents scope or records a tech choice.
- **ADR** (`docs/adr/`) — durable technical/architectural product decision only.
- **operator-notes.md** = supreme business truth & PRD/FRD seed (protected).
  **capabilities.md** = schedule + capability-ID registry; its Canonical-owner
  column is the join key to PRD/FRD/ADR.

## Part A — PRD/FRD taxonomy & requirements.md retirement

`docs/prd/pegasus-product.md`; `docs/frd/frd-01..12` (case-identity, intake,
triage, parties-accounts-access, documents-custody, vehicle-evidence,
eva-handoff, email-mailbox, provider-routes, mcp-automation, reports,
operator-experience), each dir with a README index. Preserve heading slugs so
existing `#anchor` links resolve. Roadmap/schedule sections (Ordered release
sequence, Delivery dependencies, Deferred seams) → `capabilities.md` (reconcile
the 228 vs 230 vs 199/201 count drift). Then delete `requirements.md`.

## Part B — ADR method (technical only, best-practice form)

Stable append-only IDs; YAML frontmatter (`id, status, date, supersedes,
superseded_by, related_capabilities, related_frd, tags`); one decision per ADR;
MADR template (Status·Context·Decision·Consequences·Options·Links); move
non-durable content out (cost/history → operations/runbook; functional → FRD;
governance → AGENTS.md); `adr/README.md` becomes a thin frontmatter-driven index;
"current decisions" = index filtered to `status: accepted`; consolidate tangled
clusters (e.g. hosting/deploy across 0002/0007/0014/0015) by superseding.

Relocation/split: 0010/0023 (governance) → AGENTS.md/index + tombstone;
0012/0020 (feature rules) → FRD-06/FRD-09 + tombstone; 8 mixed
(0005/0006/0008/0013/0018/0021/0022/0024) → thin ADR + functional clauses to the
mapped FRDs; 11 pure-technical kept + frontmatter.

## Part C — AGENTS.md (governance home; SIMPLI-002)

Below the `kanmer:instructions` block (preserve it, the `CLAUDE.md` symlink, the
filename, and the `#repository-task-workflow` anchor): documentation model +
routing + ADR conventions + placement; workflow claim = Kanmer `take_ticket`;
read-only-Azure-permitted safety rail (writes/deploys/credential/destructive/
external still need exact-target approval), coordinated with the runbook matrix.

## Part D — Retire NOW.md (SIMPLI-004)

Relocate durable facts to `operations.md`/`open-decisions.md` first (Worker
enabled, live-verified 2026-08-13); `open-decisions.md:25` owns the `## Path`
sequence; retarget ~6 canonical links + 2 code comments; delete `NOW.md`.

## Part E — Cleanup (SIMPLI-005), last

Archive non-actionable Kanmer tickets; remove orphaned temp-plans (keep-web-warm,
mcp-assessment-toolset, send-to-claude-channel-integration, kanmer-tickets/plan.md,
superseded simplify/adr-consolidation.md).

## Sequencing — 5 reviewable PRs on one branch (each link-checker-green)

1. **PR-1** Governance + scaffold: doc model + routing + ADR conventions +
   placement into AGENTS.md/docs-index; create `docs/prd/` + 12 FRDs (content
   migrated, slugs preserved); fold 0010/0023 → AGENTS.md/index + tombstone;
   banner+freeze `requirements.md`.
2. **PR-2** Cutover: retarget every checked-doc `requirements.md#x` link
   (~230 capabilities.md cells + ~25 elsewhere); delete `requirements.md`;
   zero-residual grep + manual anchor spot-check.
3. **PR-3** ADR modernization: frontmatter on all ADRs; thin 8 mixed ADRs;
   tombstone 0012/0020; rebuild `adr/README.md`; hosting/deploy consolidating
   ADR; reconcile capability counts.
4. **PR-4** NOW.md retirement + AGENTS.md workflow rewrite (Parts C-workflow + D).
5. **PR-5** Cleanup (Part E).

## Verification

`pwsh ./scripts/Test-DocumentationLinks.ps1` green per PR (checks file paths, not
anchors — anchors are a manual spot-check); `git grep -nE
'\]\([^)]*requirements\.md'` → 0 after PR-2; every ADR has parseable frontmatter;
no ADR encodes documentation/process rules (grep the ADR set); 7 integration
tests still find `AGENTS.md`; `CLAUDE.md` still a symlink;
`#repository-task-workflow` anchor resolves; independent review + green CI per PR
before merge to `dev`; `dev`→`main` needs explicit `MERGE AUTH GRANTED`.

## Risks

Governance leaking back into ADRs (grep is the acceptance test); broken links /
silent bad anchors (slug preservation + scripted retarget + grep + manual
spot-check); lost normative rule when thinning ADR-0013's 11 clauses (clause map
+ reviewer count check); renumber temptation (rejected); operator-notes.md
protected (conflicts → open-decisions line, not an edit); CoreAssembly.cs
capability IDs keyed on IDs not paths (untouched).
