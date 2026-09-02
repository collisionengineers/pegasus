---
verification_kind: adjacent-work-pack-and-board
verified_at: 2026-09-01T16:05:52+01:00
repository_change: none
pull_request: none
deployment: n/a
cloud_write: none
---

# Proof — KANMER-009: Organize the EPIC-011 work pack and reconcile its Kanmer map

## Verdict

**PASS.** The approved adjacent-folder and Kanmer reconciliation is complete. This chore changed no Pegasus source or governing document, used no task branch or PR, performed no deployment, and made no Azure, mailbox, or Box write.

## Work-pack result

The pack contains 26 files: the 11 original artifacts and 15 added planning files. `README.md` was intentionally replaced, leaving ten immutable originals: one HTML prototype, one DOCX companion, and eight PDFs.

Added planning layer:

- `artifact-manifest.yml`
- `decisions-and-constraints.md`
- `ticket-map.md`
- `ui-reconciliation.md`
- `acceptance-matrix.md`
- `source-sample-catalog.md`
- `azure-ocr.md`
- eight one-to-one JSON files under `sample-oracles/`

## Artifact and structured-data evidence

- All ten immutable source byte sizes and SHA-256 hashes match `artifact-manifest.yml`.
- The manifest records 11 unique originals, with the README explicitly marked as a mutable pre-organization baseline.
- The eight PDFs total 42 pages and map one-to-one to eight valid JSON oracles.
- Provider split is three Audatex and five Glass's; seven use embedded text and YL69 alone uses `azure_document_intelligence` / `prebuilt-layout`.
- Every oracle source path exists and its source SHA matches.
- Every oracle satisfies `net + VAT = gross`; every supplied section subtotal satisfies its recorded labour/material equation.
- Generated Markdown, YAML, and JSON contain no copied email address, phone number, postcode, party address, contact detail, or full VIN.

## Documentation and coverage evidence

- The local Markdown-link check returned zero unresolved targets.
- Markdown table-shape and trailing-whitespace checks returned zero findings.
- Every Kanmer-like identifier referenced by the pack resolves to a live or archived board item, with EPIC-011 handled as the named group.
- `ui-reconciliation.md` covers the final prototype route dispatcher, all current Razor route families, and grouped actionable outcomes.
- `acceptance-matrix.md` binds those families to governing documents, actual caller boundaries, exact owners, and repository evidence tiers.
- `ticket-map.md` contains the five requested results: related tickets, allocated missing tickets, existing-ticket adjustments, exact no-action/no-new-ticket results, and work-pack document changes/refresh rules.

## Live Kanmer evidence

Allocated and retained IDs:

- `ENG-031`, `ENG-032`, `PLAT-064`, `MAIL-030`, `MAIL-031`, `PLAT-065`, `MAIL-032`, `MAIL-033`, and `KANMER-010`
- `KANMER-009` for this bounded reconciliation

All downstream tickets have valid areas/profiles and a reachable Backlog → Preparing move. Feature/fix tickets carry governing refs; `PLAT-065` intentionally carries `docs_todo` for its ADR/FRD work. Structured dependency checks match the ticket map. Stale blocked-by edges were removed only from already-Done `MAIL-025`, `AUTO-011`, `ENG-026`, and `KANMER-005`; all four now return an empty `blockedBy`. The proof-backed holds on `CASE-012`, `CASE-025`, `ENG-025`, `PLAT-029`, and `PLAT-048` were inspected and retained as real.

EPIC-011 group document `decisions/2026-09-01-work-pack.md` records the settled exclusions, report-image order, sample/OCR rule, field boundary, password-reset behavior, and allocated IDs. Its YL69 wording was independently checked and corrected to “visually valid with an unusable embedded character map.”

## GitHub and Azure observations

Refreshed 2026-09-01 at 15:54 BST:

- PR #639: open draft, conflicting/dirty, `changes` and SQL coverage failing; remain deferred.
- PR #640: open, clean/mergeable, full required run green including `test-ui`; independent ticket review still required.
- PR #641: open and mergeable but unstable because SQL integration shard 1 is cancelled; rerun/review required.

A read-only Azure MCP inventory of the default enabled subscription and `rg-pegasus-prod` returned 18 resources, including the existing Worker user-assigned identity, and no `Microsoft.CognitiveServices/accounts` resource. Microsoft Learn confirms PDF support and model ID `prebuilt-layout`, custom-subdomain use for Entra authentication, and the current `Cognitive Services User` analysis role. No resource or RBAC write was attempted.

## Independent review

A separate agent returned PASS after two findings were corrected:

1. the binding EPIC-011 YL69 classification;
2. one undefined UI-reconciliation classification.

The final independent pass confirmed immutable hashes, page/oracle mapping, local and repository links, all referenced ticket IDs, live relationships/references/proof gaps/blockers, PR dispositions, and the privacy scan.

## What this proof does not claim

- No downstream product feature was implemented or delivered.
- No PR was reviewed, merged, or retitled by this chore.
- Azure Document Intelligence is not provisioned or active.
- `KANMER-010` still owns setup-drift reconciliation.
- Downstream tickets still owe their own live document gates, implementation, independent review, merged-main proof, and deployment evidence where applicable.

## Final bundle and roster closeout

- `ticket-map.md` was refreshed after `KANMER-009` entered Done and matches the live EPIC-011 roster exactly: 97 members — 51 Backlog, 2 Preparing, 0 Implementing, 0 Review, 21 Verifying, and 23 Done.
- The planning bundle comprises the rewritten README plus 15 added files (16 planning files total). Its SHA-256 is `10A79D833679FA21E1DA2521280DBE906B0E8A24E7D596333276AEDCFF42D4E2`.
- `KANMER-009` is Done; its seven checklist items are complete. The remaining setup drift is deliberately owned by `KANMER-010`.
