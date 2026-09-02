# Post-implementation report — DELIV-040

*The report. Not the proof — this is the author's **claim**, written before merge; proof is
**evidence**, gathered after.*

The reviewers' brief: every change and why. Gates **Implementing → Review**.

## Summary

The sixteen canonical documents named in the plan now record the operator interface decisions
**D15–D28**, confirmed binding on 2026-09-01 (EPIC-011 `decisions/2026-09-01-work-pack.md`,
"Interface decisions confirmed binding on 2026-09-01"), and no governing document still states the
opposite. Nothing was implemented and nothing is claimed as delivered: every `docs/capabilities.md`
row is dated 2026-09-01, names its allocated ticket, and reads *decided* or *allocated*, never
*delivered*. Twelve commits on `task/deliv-040-governing-docs`, base `origin/dev`
`9b8f78a36151313bc6d48625edee7f13a2173127`, head `25c14574a9e34c77e977f8a8eb203c2fe85dc13e`,
16 files changed, +333 / −116, no file added and none deleted.

## Changes

One row per file. The decision letters are the D15–D28 numbering in EPIC-011 `context.md` § 2.

| File | Change | Why |
|---|---|---|
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | modified — § Professional engineering findings and correction gains the evidence-only clause; § Canonical repair specifications gains the selected rate-card version in the retained basis and the Case-plus-source-hash replay rule; § Ordinary-image VRM and image analysis gains the report-image curation paragraph | D17 betterment and `guide` codes derive no semantics; D16 replay; D19 non-destructive curation with distinct `Close-up`/`Overview`, ordered supporting images and an immutable issued snapshot |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | modified — the assessment-bundle sentence now names typed Engineer identity; § Initial renderer activation loses the exact-matching-tuple paragraph; the readiness rail loses the signature-tuple and repair-cost blockers; **Repair-cost figures are not yet derivable** is replaced by the accepted derivation; § Photographs loses the UI-15 curation deferral; the AI Job List `Estimate` Input cell and the § Estimate VAT figure table are corrected | D18 any Engineer issues under typed identity, assets governed but inactive; D17 non-paint labour = normalized non-paint hours × the selected card's rate, everything else explicit, VAT on the whole subtotal, no comparison or savings; D19 curation; D24 optional 0–100 % target with no default, guidance only |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | modified — § AI job and estimate tools gains the `pegasus_estimate_import` row and the shared-command paragraph | D16 the MCP caller reuses the same Core command as the Web drop, with the exact parameter and return contract |
| `docs/frd/frd-01-case-identity-and-lifecycle.md` | modified — the blocker paragraph and § Due work, chasing, and action history | D23 completeness is a versioned required/not-required set with exact blockers and never a percentage; the chase interval is global, 1–365 whole calendar days, default 7, Europe/London, `Held` preserving the remainder |
| `docs/runbook.md` | modified — one § Release validation rules bullet | D23; the fixed seven-day literal became the configured interval |
| `docs/frd/frd-02-intake-and-source-identity.md` | modified — § Ways intake starts, § Request-scoped upload links, § Upload confirmation surface, and the INT-32 age paragraph | D26 attributable instruction receipt before the normal allocation, no parallel allocator; D20 one successful submission per link, reconciling retry, non-disclosing refusal, one grouped submission decision above the per-file details; D23 the file's second seven-day literal |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | modified — § Supported source boundary | D20 exact intake bounds, citing FRD-09 for the unchanged 30 MB provider envelope rather than restating it |
| `docs/frd/frd-03-triage.md` | modified — § Normal workflow and completion evidence gains the History and Files contract | D25 chronological merge of durable events and append-only attributable notes; corrections are new notes; no edit, delete or upload |
| `docs/frd/frd-04-parties-accounts-and-access.md` | modified — the role matrix Administrator and Engineer cells; § Staff accounts gains **Reset password** | D17 rate-card administration; D18 report issue under typed identity; D28 administrator-entered temporary password, existing policy and hashing, forced change, permanent record, never emailed |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | modified — the workspace freshness paragraph, the default-view paragraph, and the § Outbound correspondence **Delete** bullet | D22 fixed 15-minute threshold, no historical backfill with the genuine retention-start boundary named, permanent deletion absent |
| `docs/frd/frd-12-operator-experience.md` | modified — § Assessment, § Upload, § Administration, the Triage-detail paragraph, the utility-bar **Add** action, the Case-workspace Overview bullet, the route table, the keyboard contract and § Acceptance evidence | the operator-facing half of D16, D17, D19, D20, D21, D23, D25, D26 and D28 |
| `docs/capabilities.md` | modified — `EXT-09`, `RPT-02`, `CASE-18`, `UI-10`, `INT-31`, `EXT-12`, `MCP-06`, `INT-26`, `UI-15`, `EXT-13`, `ENG-01`, `OPS-20`; new row `ACC-15`; the allocation summary recounted | registry and schedule only; every touched row records the decision as decided/allocated and dated, never delivered |
| `docs/boundaries.md` | modified — four new rows in § Deferred capabilities and preserved seams | D21 the Glass's/Audatex launch controls, a standalone Images list and runtime-managed templates are **absent, not disabled**; D27 the 2,000-case tier is not run and never passing |
| `docs/open-decisions.md` | modified — the rate-card row resolved and removed; § Mail workspace freshness threshold and retention start resolved with its table removed; the assessment-markup, report-wording and Suggestions/slider rows narrowed; the upload-cap note updated | settled content moves to its canonical owner (L7); no unsettled row was deleted |
| `docs/design/README.md` | modified — § Evidence discipline, § Keyboard and dialog contract, the two signature-asset rows, § Absent versus disabled, § Triage, § Assessment, § Upload, § Administration, § Removed surfaces, § Deferred integration and intake surfaces | D15 canonical visual source; D16, D17, D18, D19, D20, D21, D23, D24, D25, D28 as working visual and interaction rules |
| `docs/engineering.md` | modified — tier 10 | D20 the intake bounds replace the 10 MiB pair; D27 the capacity tier is recorded as not run and never passing |

## Governing docs

The 2026-09-01 operator confirmation is the **explicit authorization** to modify these governing
documents; `docs/index.md` § Authority order settles precedence where two disagree.

- **FRD-01/02/03/04/05/06/08/10/11/12** — modified, each within the behaviour it owns.
- **`docs/capabilities.md`** — modified as the schedule and ID registry only; it holds no normative
  behaviour (`AGENTS.md` L263–265). The four allocation figures were recounted together: `Now`
  142→143, `Total: 233 capabilities; 233 unique IDs`→**234 / 234 (mechanical recount 2026-09-01)**,
  and the `0.1.0-alpha.1` target 142→143. Verified mechanically: 234 rows, 234 unique IDs, and
  143 + 27 + 35 + 29 = 234.
- **`docs/boundaries.md`** — modified; it owns deferred and excluded scope.
- **`docs/open-decisions.md`** — two rows resolved, three narrowed, one note updated.
- **`docs/design/README.md`, `docs/engineering.md`, `docs/runbook.md`** — modified as working rules
  downstream of the FRDs being corrected.
- **New ADR: none.** `0037` is reserved for OCR (`TICK-041`/`PLAT-065`). The durable technical
  shapes these decisions imply are owed by the feature tickets, not by this one: the shared
  raw-import Core command with a Web and an MCP caller → `ENG-033`/`AUTO-016`; the global versioned
  labour-rate-card aggregate → `TICK-082`; the normalized versioned curation snapshot → `ENG-031`.
- **Nothing out of scope was touched**: no OCR content, no ADR, no PRD line, no
  `docs/operator-notes.md`, no `docs/current-architecture.md` or `docs/operations.md` current-state
  figure, no FRD-07 or FRD-09, and nothing under `src/`, `tests/`, `scripts/`, `.github/` or
  `docs/design/test-ui/`.

## Planner assumptions carried forward, and two corrections

1. **Rate-card administration inside Workflow configuration** — carried forward as written. FRD-12
   § Administration keeps eight areas and the design README Workflow-configuration bullet gains the
   cards.
2. **D28 registered as a new capability row — id corrected.** The planner named `ACC-10`, but
   `ACC-10` is already allocated (`Separate authentication/security log`), and `ACC-01`–`ACC-14` are
   all taken. Per the plan's Step 12 deviation stop the implementer stopped rather than choosing a
   number; the controller assigned **`ACC-15`** and amended the assumption. The row is registered
   with that id.
3. **The D16 MCP tool is `pegasus_estimate_import`** — carried forward. Checked for collision before
   use: no pre-existing occurrence anywhere in `docs/` or `src/`.
4. **ASSUMPTION 4, new this attempt (accepted by the controller).** The plan's Step 3 prescribed
   renaming the mail-freshness heading to the `— resolved <date>` form on the premise that no
   document links it. That premise is false: `docs/current-architecture.md` L527 links
   `open-decisions.md#mail-workspace-freshness-threshold-and-retention-start`, and that file is in
   this ticket's Do-not-modify list. The repository link check verifies paths, not anchors, so the
   rename would have broken a live cross-document link silently — the exact hazard the plan's own
   Constraints cite. The heading is therefore byte-identical and the resolution is recorded as a
   bold **Resolved 2026-09-01.** lead; the table is removed and the canonical owner is named, as
   Step 3 requires. **No heading in any of the sixteen files was renamed**, verified by diffing
   every ATX heading line against `origin/dev`.

## Risks / follow-ups

- **Ten tickets read these documents as their premise** and leave Backlog only after this merges:
  `ENG-031`, `ENG-033`, `INTK-052`, `INTK-054`, `MAIL-030`, `MAIL-031`, `PLAT-059`, `PLAT-062`,
  `PLAT-064`, `TICK-082`. `AUTO-016` may rename the MCP tool when it composes it.
- **Intent versus current state.** D20's raised bounds are decided intent; the accepted
  `int-31-interim-v1` limits in `open-decisions.md` and the as-built 10 MiB figures in
  `current-architecture.md` remain the truthful current policy and were deliberately left alone.
  A reviewer should confirm the two readings stay separable.
- **`open-decisions.md` § Later operator UI capabilities and § Manual upload in a deployed
  environment** were deliberately left open: the ticket's decision table assigns neither, and
  resolving them needs evidence this ticket does not have.
- **`docs/frd/frd-12` § Edge cases** ("an integration without a composed caller shows its named
  disabled seam") was checked and needs no edit: D7 still permits the Experian and Cazana seams, and
  D21 removes only the Glass's/Audatex row.
- **Environment, not product.** Round 1 of this attempt was blocked by a false positive in the shell
  guard's rule 8 — it judged the session working directory rather than the `-C` worktree. The
  controller corrected the hook at 01:41Z. No repository file was involved.

## Verification hand-off

Merged-`dev` checks for `kanmer-verify`; this change deploys nothing and alters no runtime
behaviour, so there is no post-merge environment check.

1. The CI `documentation` job's three steps, plus the placement validator invoked directly with
   `-Base origin/dev -Head HEAD`. All four already passed pre-merge on `25c14574` (runner evidence
   in the controller run directory: docs-links PASS, markdown-placement PASS, no-new-markdown PASS,
   scope-only-docs PASS). Expect the same on the merge commit.
2. A name-only diff of the merge range filtered to added files prints nothing — **no Markdown file
   was added**, which `scripts/Test-MarkdownPlacement.ps1` would otherwise reject.
3. The same diff unfiltered lists only the sixteen `docs/` files above.
4. `docs/capabilities.md` arithmetic on the merged file: 234 rows, 234 unique IDs, and
   `Now` + `Next` + `Later` + `Not planned` = 234, with the `0.1.0-alpha.1` target reading 143.
5. Contradiction sweep over `docs/` (excluding `docs/json-extraction-parity/`,
   `docs/current-architecture.md`, `docs/operations.md`, `docs/adr/`): no surviving "not yet
   derivable", "original-versus-assessed", Pegasus savings or comparison feature, exact-signature
   tuple gate, unqualified "seven calendar days", 10 MiB or 25 MB upload bound, Import estimate
   dialog or picker, Glass's or Audatex launch control, provisional 15-minute threshold, or
   completeness percentage. Every surviving occurrence of those phrases should be a negation.
6. No screenshot is owed: no UI was built.

## Remediation round 1

Reviewer `claude-code/20260901T215000Z-claude-controller/reviewer-a1` returned **needs-changes** on
PR #643 at head `25c14574a9e34c77e977f8a8eb203c2fe85dc13e` (attestation `scratch/review`
v `b9bcd6ad94d49f55`). Coverage of D15–D28 was found complete; two majors blocked. All five
actionable findings are closed in one commit, **`357f143b5c8c00cd5ec8981944b8684c4606b659`**, which
touches `docs/capabilities.md`, `docs/design/README.md`, `docs/frd/frd-11-…` and `docs/frd/frd-12-…`
— all four already in this ticket's Expected files. No new file, no heading renamed, no widened
scope.

| Finding | Severity | Disposition | What changed |
|---|---|---|---|
| **F-001** | major | **fixed** | Confirmed: FRD-11 L70–71 still said Infrastructure renders "with the governed template, stylesheet, logo, and signature resource" four lines above the new D18 paragraph saying a signature asset is not rendered, and L311 called signatures "embedded in governed renderer documents", while this PR's design README rows asserted "never embedded by Infrastructure" against the higher-authority FRD. Both FRD-11 statements are corrected — Infrastructure renders with the governed template, stylesheet and logo, and no signature resource is embedded in a rendered report; the trailing sentence records the assets as retained, governed and inactive with the typed identity rendered instead. The losing statements were fixed in the same commit, per `docs/index.md` § Authority order. FRD-11, the design README and D18 now agree; verified by sweeping every "signature" occurrence in the three files for a render or embed claim. |
| **F-002** | major | **fixed** | Confirmed against source: the accepted engineer-signature check still ships (`src/Pegasus.Core/Assessment/AssessmentPolicy.cs:255`, `src/Pegasus.Core/Reports/AssessmentReportProjection.cs:117-129`, read at this head). `RPT-02`'s account of *delivered* behaviour reads "plus the accepted engineer-signature check" again, and its D18 sentence now carries the same **not delivered** marker its D17 and D19 siblings carry, adding that no D18 implementation ticket is allocated yet and that the shipped check stays in force until one lands. This was a real truthfulness defect: the row described undelivered intent in the tense of shipped behaviour. |
| **F-003** | minor | **fixed** | The three bullets this PR added to § Removed surfaces sat under the lead "deleted by their wave tickets" while the Import estimate dialog and the disabled Glass's/Audatex buttons still ship (`src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml`). They now take the section's existing pending-removal qualifier and name the owing tickets (`ENG-033`, `ENG-030`); the never-built exclusions are marked as such. |
| **F-004** | minor | **fixed** | "amounts entered on the estimate version" was narrower than D17's "explicit amounts" and rubbed against `docs/operator-notes.md` § Repair estimates ("Repair cost figures are not typed into Pegasus by hand"). The clause now reads explicit amounts carried on the estimate version by whichever route supplied it, never derived from the card — which is what D17 says and makes no hand-entry claim. `operator-notes.md` itself was not touched. |
| **F-005** | minor | **fixed** | The justification did overstate: manual line entry performs no import, retains no source artifact, hash or parser provenance, and MCP is an automation boundary rather than a staff keyboard route, so "no capability is pointer-only" was unsupported. FRD-12 § Assessment, the FRD-12 keyboard contract, FRD-12 § Acceptance evidence and the design README keyboard contract now record the drop as an accepted exception and a **real gap** for keyboard-only operators, which is what D16 asks to be documented rather than explained away. |
| **F-006** | note | accepted-risk (reviewer's own disposition, unchanged) | `ACC-15` sits between `ACC-10` and `ACC-11` rather than in id order. Cosmetic; no count, link or anchor depends on row order and the arithmetic verifies clean. Re-verified after this commit: 234 rows, 234 unique ids, 143 + 27 + 35 + 29 = 234, `0.1.0-alpha.1` 143. |
| **F-007** | note | rejected-with-reason (reviewer's own, agreed) | D15 as confirmed names the adjacent work-pack HTML, which lives outside the repository; adding a copy or link would be a new non-Markdown asset outside Expected files. |
| **F-008** | note | accepted-risk, **escalated** | The identity binding point at render time is an implementation design question D18 does not settle. The reviewer notes no D18 implementation ticket exists anywhere in the decision record. `RPT-02` now states that absence explicitly rather than leaving it implied, and the controller is asked to allocate one; no ticket was invented here. |
| **F-009** | note | rejected-with-reason (reviewer's own, agreed) | Rule 24 covers commands and agent conventions; `AGENTS.md` routes UI behaviour to the FRDs and the design README, which is where both rules landed. No `AGENTS.md` edit is owed. |

Re-verified after the correction commit, at head `357f143b`: no heading added, removed or renamed in
any changed file; every relative link target resolves; `docs/capabilities.md` arithmetic consistent;
the contradiction sweep still clean; and against `origin/dev` the diff is exactly the sixteen
Expected `docs/` files, +345 / −120, with no file added and none deleted. The four docs lanes are
owed to the test-runner role at this new head — the implementer ran none of them.
