# Plan — KANMER-009: Organize the EPIC-011 work pack and reconcile its Kanmer map

## Objective

Create one durable, self-validating planning handoff in the adjacent `pegasus-work-pack` and reconcile only the approved live-board changes, without implementing product features or changing cloud state.

## Starting state

The adjacent folder contained one HTML prototype, one companion DOCX, eight estimate PDFs, and a short README. The source artifacts were useful evidence but had no immutable manifest, sample oracles, current live-board map, explicit authority boundary, route/action reconciliation, or acceptance matrix. The live board was observed through Kanmer 0.3.12; EPIC-011 and existing owners already covered most product scope. The user approved the exact reconciliation plan and settled the remaining scope choices.

## Governing docs

This board/meta chore has no linked repository PRD, FRD, or ADR because it changes no Pegasus product requirement or architecture. It meets the repository workflow and documentation model by keeping transient planning material outside the repository, treating `docs/index.md` and its authority chain as canonical, and recording product follow-ups on Kanmer. It does not modify `docs/operator-notes.md` or create a governing document. EPIC-011 shared context and `decisions/2026-09-01-work-pack.md` bind the programme decisions.

## Required changes

- Preserve the supplied HTML, DOCX, and eight PDFs byte-for-byte.
- Replace the README with a navigation and authority index.
- Add a source manifest, sample catalogue, eight redacted machine-readable oracles, settled-decision record, Azure OCR activation note, live ticket map, UI reconciliation, and acceptance matrix.
- Create the approved non-duplicate feature/fix/chore tickets and apply the approved existing-ticket, dependency, governing-reference, PR, and hygiene reconciliations.
- Record dated live state and explicit refresh instructions rather than presenting snapshots as current forever.
- Validate all references, structured files, source hashes, sample counts, Kanmer allocations, and dependency outcomes.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `../pegasus-work-pack/README.md` | Audience-first navigation, authority, boundaries, and refresh rule. |
| Add | `../pegasus-work-pack/artifact-manifest.yml` | Immutable source inventory and SHA-256 evidence. |
| Add | `../pegasus-work-pack/decisions-and-constraints.md` | Settled scope, exclusions, and implementation constraints. |
| Add | `../pegasus-work-pack/ticket-map.md` | Dated live roster, exact ticket actions, dependencies, PR disposition, and board hygiene. |
| Add | `../pegasus-work-pack/ui-reconciliation.md` | Grouped route/action-to-caller ownership map. |
| Add | `../pegasus-work-pack/acceptance-matrix.md` | Capability acceptance and evidence tiers. |
| Add | `../pegasus-work-pack/source-sample-catalog.md` | Eight-sample parser/OCR classification and handoff. |
| Add | `../pegasus-work-pack/azure-ocr.md` | Approval-gated Document Intelligence activation constraints. |
| Add | `../pegasus-work-pack/sample-oracles/*.json` | One redacted expected-result oracle for each supplied PDF. |
| Inspect only | `../pegasus-work-pack/Pegasus_UI_*`, `../pegasus-work-pack/audatex-samples/*.pdf`, `../pegasus-work-pack/glasses-samples/*.pdf` | Immutable source evidence. |
| Inspect only | `docs/**`, source/tests, live Kanmer, GitHub PRs, Azure resource inventory | Canonical context and read-only facts. |

The added YAML and JSON files are planning artifacts, not generated application assets.

## Do not modify

- Any supplied HTML, DOCX, or PDF source artifact.
- Pegasus product code, tests, repository governing docs, current-state docs, branches, worktrees, or user-owned working-tree changes.
- Azure resources, identities, RBAC, credentials, deployment state, mailboxes, or Box locations.
- Active PR branches or ticket pipeline documents owned by another task.

## Constraints

- Kanmer is authoritative for ticket state and gates; search before ticket creation and use structured links once.
- Repository product documents outrank prototype evidence.
- Keep the four settled exclusions: autonomous outbound mail, standalone Images list, runtime-managed templates, and direct Glass's/Audatex service-launch controls.
- Use only redacted, assessment-relevant oracle fields. Original samples stay local and immutable.
- Azure planning uses `prebuilt-layout`, a custom-subdomain endpoint for Entra authentication, and production managed identity/RBAC; every external write remains exact-target approval-gated.
- Dated board and PR observations must include refresh procedures.

## Ordered steps

1. Inventory and hash every original artifact; classify all eight PDFs and draft one oracle per sample.
2. Reconcile prototype route/action families against canonical documents, production callers, existing tickets, and explicit exclusions.
3. Search the live board, allocate only approved missing outcomes, update approved existing owners, and add true dependency edges.
4. Inspect proof and dependency truth, retaining real holds and removing only stale edges on already-Done tickets.
5. Write the pack navigation, decisions, manifest, sample/OCR handoff, ticket map, UI reconciliation, and acceptance matrix.
6. Validate source hashes, structured-file parsing, relative links, ticket IDs/gates/relationships, complete EPIC-011 roster, and original-artifact immutability.
7. Record the result and stop without product implementation, PR merge, deployment, or cloud mutation.

## Acceptance checks

- The ten immutable source artifacts (HTML, DOCX, and eight PDFs) retain their pre-change SHA-256 values; the README baseline remains recorded separately because it is intentionally replaced.
- Exactly eight sample-oracle JSON files parse and each maps to one manifest PDF.
- Every relative link resolves and every referenced Kanmer ID exists.
- Every prototype route/action family maps to one existing/adjusted/new owner, an explicit exclusion, or a stated no-action result.
- Allocated ticket IDs, governing references, groups, links, blockers, and `docs_todo` values match the approved map.
- The dated EPIC-011 roster exactly matches live Kanmer at the observation point.
- No Pegasus source, governing document, cloud resource, PR branch, mailbox, or Box location changes.

## Commands

Run from `C:/Users/PGUSER/Documents/github/pegasus-work-pack` unless noted:

```powershell
rg --files
Get-FileHash -Algorithm SHA256 -LiteralPath <source-artifact>
Get-Content -Raw -LiteralPath <oracle> | ConvertFrom-Json
```

Use Kanmer `get_status`, `list_board`, `get_group`, `list_items`, `get_item`, `get_links`, and `get_doc_gates` for live validation. Use read-only Git/GitHub/Azure checks only where needed.

## Failure and deviation rules

Stop and report any source-hash change, missing sample, invalid structured file, unresolved link, duplicate owner, live-board conflict, governing-doc contradiction, or request for an external write. Do not silently reinterpret a prototype fixture as product truth or broaden an allocated ticket. Timestamp any observation that can change.

## Stop condition

Stop when the adjacent pack and approved Kanmer reconciliation are validated and recorded. Do not implement any downstream product ticket, merge a PR, repair Kanmer setup drift, deploy, or perform an Azure/external write.
