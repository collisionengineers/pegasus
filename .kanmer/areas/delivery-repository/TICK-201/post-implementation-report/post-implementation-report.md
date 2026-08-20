## Post-implementation report — TICK-201

**Change:** `docs/operations.md` — corrected the "Approved Box custody root"
section's claim that secret values resolve "only inside the Worker through Key
Vault references." That claim contradicted the same document's own later
Secrets record (both Worker and Web resolve their own Box secrets since
release 3) and the live estate facts (Web has exactly two Box secret
references via its own managed identity, matching the doc's own "two Web"
grant count). Corrected to state both hosts resolve their own copy
server-side, each via its own identity, with a link to the Secrets record.

**Audit scope:** ticket body named no specific claims, so per the run's
targeted-pass instruction I audited `docs/current-architecture.md` and
`docs/operations.md`'s factual claims against a read-only production
diagnostics snapshot (2026-08-20, release 13 = `2325ed4a`). Full claim-by-claim
trail is in `research.md`. Only one claim required correction; everything else
checked (release facts, Box custody root id, image-intake/Box boundary, VRM
acceptance threshold, Worker health) already matched. The release table in
`docs/operations.md` was left untouched (historical record).

**Out of scope, recorded for follow-up (not a doc-claim contradiction):** the
diagnostics surfaced live operational defects — VRM group fan-out
inconsistency, Unidentified items never closing, Not-Ready badge/list source
mismatch, dashboard email counter counting `manual_upload` receipts, and a
Worker SIGABRT/`PollSentEvidence` spurious-rejection pair. No documentation
claim in either canonical doc asserts that behaviour currently works, so there
was nothing to "correct" — these are candidates for a separate bug ticket.

**Verification against ticket checklist:**
- Each reviewed claim traced to `docs/operations.md` as authority owner
  (deployed/runtime state) per `docs/index.md`.
- The one contradiction was corrected without inventing stronger evidence than
  the document's own Secrets record and the diagnostics support.
- `./scripts/Test-DocumentationLinks.ps1` — "All relative Markdown links
  resolve (205 files checked)."
- `./scripts/Test-TestMarkdownPlacement.ps1` — "Markdown placement regression
  tests passed."
- `docs/operator-notes.md` untouched; no unresolved operator decision to park.

**Commit:** `48413f1e` on `task/tick-201-doc-claims`.
**PR:** https://github.com/collisionengineers/pegasus/pull/444 (base `dev`).

**Simplification pass:** n/a — docs-only, single-paragraph factual correction,
no code touched.
