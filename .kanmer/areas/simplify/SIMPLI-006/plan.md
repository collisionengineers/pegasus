# Plan — PRD/FRD/ADR taxonomy + ADR modernization (executed; PR #374)

This ticket was **reshaped** from "consolidate ADRs into nine (renumber to
`0001–0009`)" to the PRD/FRD/ADR taxonomy. The renumber approach in
`docs/temp-plans/simplify/adr-consolidation.md` is **superseded** — ADR IDs stay
stable. Approved plan + full detail: `docs/temp-plans/retire-now-rewrite-agents.md`.

## Approach (as executed)

1. **Governance into `AGENTS.md`** (not an ADR): the documentation model, the
   PRD/FRD/ADR routing table, ADR authoring conventions (stable IDs, YAML
   frontmatter, one-decision, supersede-don't-renumber), and new-Markdown
   placement. `docs/index.md` gets the new authority chain + placement rule.
2. **Frontmatter on every ADR** (`id`/`status`/`date`/`supersedes`/
   `superseded_by`/`related_capabilities`/`related_frd`/`tags`).
3. **Relocate + tombstone the mis-filed ADRs:** 0010/0023 (documentation
   governance) → `AGENTS.md`/`docs/index.md`; 0012/0020 (feature rules) →
   FRD-06/FRD-09 (content relocated first, then tombstoned).
4. **Trim the 8 mixed ADRs** (0005/0006/0008/0013/0018/0021/0022/0024) to their
   technical cores — functional clauses already owned by the FRDs are removed
   with a Functional-behaviour pointer; any clause NOT in a FRD is kept;
   externally-cited clause numbers preserved (e.g. ADR-0013 keeps 10/11/12/14).
5. **Rebuild `docs/adr/README.md`** as a thin `status: accepted` current-view
   index plus a superseded/relocated table.
6. **Reconcile `capabilities.md`** inventory count to the real 231 rows.

Dropped with the renumber approach: the `azure.yaml remoteBuild` /
`Test-AzureDeploymentPlan.ps1` release-config edits (they belonged to the
renumber-to-9 plan, not the taxonomy) — **not done**.

## Acceptance (met)

- Every ADR carries parseable frontmatter; **no ADR encodes documentation/
  process rules** (grep-clean).
- ADR IDs stable (no renumber); the 4 mis-filed ADRs relocated with substance
  preserved; the 8 mixed ADRs trimmed with no rule lost.
- Documentation link checker green (118 files).

## Verify (done — see proof.md)

`pwsh ./scripts/Test-DocumentationLinks.ps1` green; ADR-body grep for doc/process
rules clean; the independent review (PR #374) passed the faithfulness axis
(no dropped rule, no governance in an ADR). Shipped in **PR #374**.
