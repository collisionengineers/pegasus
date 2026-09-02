---
kind: review-attestation
pr: "643"
head_sha: "25c14574a9e34c77e977f8a8eb203c2fe85dc13e"
verdict: needs-changes
reviewer: "claude-code/20260901T215000Z-claude-controller/reviewer-a1"
independent: true
plan_hash: "203f2182d5bac060"
ticket_updated: "2026-09-02T02:52:13.537Z"
board_sha: "c238075125bab873c4986277a0d30c70eed4eca0"
expected_reviewers:
  - "claude-code/20260901T215000Z-claude-controller/reviewer-a1"
threads_snapshot:
  - source: github
    id: "PRRT_kwDOThBrk86eWYkR"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-005
  - source: github
    id: "PRRT_kwDOThBrk86eWYkT"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-009
  - source: github
    id: "PRRT_kwDOThBrk86eWYkU"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-003
  - source: github
    id: "PRRT_kwDOThBrk86eWYkW"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-001
  - source: github
    id: "PRRT_kwDOThBrk86eWYkZ"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-004
  - source: github
    id: "PRRT_kwDOThBrk86eWYkb"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-002
  - source: github
    id: "PRRT_kwDOThBrk86eWYkc"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-007
  - source: github
    id: "PRRT_kwDOThBrk86eWYke"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-008
  - source: attestation
    id: "reviewer-a1-acc15-order"
    author: "claude-code/20260901T215000Z-claude-controller/reviewer-a1"
    resolved: false
    finding: F-006
findings:
  - id: F-001
    severity: major
    summary: "FRD-11 still states the opposite of D18 inside the section this PR rewrote for D18, and the design README edits this PR made now contradict it: L70-71 says Infrastructure renders with the governed template, stylesheet, logo and signature resource, and L311 says signatures embedded in governed renderer documents are provenance-sensitive assets, while L75-76 says a signature asset is not rendered and the design README says signatures are never embedded by Infrastructure and no asset is embedded."
    disposition: open
  - id: F-002
    severity: major
    summary: "capabilities.md RPT-02 deleted the clause 'plus the accepted engineer-signature check' from its description of already-delivered behaviour although that check still ships (src/Pegasus.Core/Assessment/AssessmentPolicy.cs:255, src/Pegasus.Core/Reports/AssessmentReportProjection.cs:117-129), and the row's D18 sentence carries no 'not delivered' or allocation marker while the D17 and D19 sentences in the same row do."
    disposition: open
  - id: F-003
    severity: minor
    summary: "design README section Removed surfaces gains three bullets under the lead 'deleted by their wave tickets' while the Assessment Import estimate control and dialog and the disabled Glass's/Audatex buttons still ship (src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:207,215,220,223,562-597) and this PR's own EXT-12, EXT-13 and ENG-01 rows say ENG-033 and ENG-030 are not delivered; the section's existing pattern qualifies a pending removal."
    disposition: open
  - id: F-004
    severity: minor
    summary: "FRD-11 section Estimate VAT on the rendered report says paint labour, paint materials, parts and other costs are 'amounts entered on the estimate version', which is narrower than D17's 'explicit amounts' and is in tension with docs/operator-notes.md section Repair estimates ('Repair cost figures are not typed into Pegasus by hand')."
    disposition: open
  - id: F-005
    severity: minor
    summary: "FRD-12 section Assessment and design README section Keyboard and dialog contract justify the D16 pointer-only drop by naming the MCP tool and manual line entry as keyboard-reachable routes to 'the same import' and conclude 'no capability is pointer-only'; manual line entry performs no import, hash retention or parser detection and MCP is an automation boundary, so the equivalence claim is unsupported even though documenting the exception is exactly what D16 requires."
    disposition: open
  - id: F-006
    severity: note
    summary: "capabilities.md places the new ACC-15 row between ACC-10 and ACC-11 rather than in id order; the allocation arithmetic itself verifies clean (234 rows, 234 unique ids, 143 Now + 27 Next + 35 Later + 29 Not planned = 234, target 0.1.0-alpha.1 = 143)."
    disposition: accepted-risk
    reason: "Cosmetic row placement in a table keyed by capability id; no count, link or anchor depends on row order, and reordering the row would enlarge the diff without changing meaning."
  - id: F-007
    severity: note
    summary: "design README section Evidence discipline names Pegasus_UI_Frontend_Design_Premium_Full_End_State.html as the canonical visual source with no repository path or link."
    disposition: rejected-with-reason
    reason: "D15 as confirmed names the adjacent work-pack HTML, which lives outside the repository; adding a repository copy or link would be a new non-Markdown asset outside this ticket's Expected files and outside the decision as recorded."
  - id: F-008
    severity: note
    summary: "FRD-11 readiness now names 'the issuing Engineer's identity' while generation, approval and issue remain distinct recorded events, so the identity binding point at render time is unspecified; D18 does not settle it and no implementation ticket is allocated for D18 anywhere in the decision record."
    disposition: accepted-risk
    reason: "The ticket records decided intent only; the binding point is an implementation design question for the owning feature ticket. No D18 ticket exists yet, which is reported to the controller for allocation rather than invented here."
  - id: F-009
    severity: note
    summary: "AGENTS.md is untouched although the PR narrows the absent-versus-disabled UI rule and adds a documented pointer-only exception."
    disposition: rejected-with-reason
    reason: "Rule 24 covers repository commands and agent conventions; AGENTS.md L255-283 routes UI behaviour to the FRDs and docs/design/README.md, which is exactly where both rules landed. No command or agent convention changed, so no AGENTS.md edit is owed (and rule 24 would forbid one)."
---

# Review — DELIV-040, PR #643 at `25c14574a9e34c77e977f8a8eb203c2fe85dc13e`

Independent review. The implementer of record is
`claude-code/20260901T215000Z-claude-controller/implementer-a1`; this reviewer is
`claude-code/20260901T215000Z-claude-controller/reviewer-a1`, a separately dispatched
agent role, so `independent: true` is truthful. Round 0, consolidated: every finding this
reviewer will raise on this PR is recorded here.

## What the PR does

Sixteen existing Markdown files under `docs/`, +333/−116, no file added or deleted, no code,
no test, no generated artifact. It records the fourteen operator interface decisions D15–D28
(EPIC-011 `decisions/2026-09-01-work-pack.md`, "Interface decisions confirmed binding on
2026-09-01") in the documents that own them.

## Question 1 — did the plan miss anything the ticket implies?

Substantially no. Every row of the ticket body's twelve-row decision table maps onto a plan
step, and the plan adds the two files the ticket's table implies but does not name
(`docs/engineering.md` tier 10 for D20/D27, `docs/runbook.md` for D23), with the governing-doc
authorization and the `governing_docs` lock recorded. Two gaps are visible only in hindsight:

- the plan's Step 2 told the implementer to replace one clause in `capabilities.md` `RPT-02`
  but, unlike Steps 4, 5, 7, 8, 10 and 11, did not require the D18 sentence to read
  "not delivered"; that omission is the direct cause of F-002;
- the plan's Step 2 named the `frd-11` exact-tuple paragraph and readiness item but not the
  two other signature clauses in the same file (L70-71 and L311), which is the direct cause
  of F-001. The plan's own Step 1 deviation stop anticipated exactly this failure mode
  ("if removing the readiness item would leave the section self-contradictory … stop").

The plan's premise for the one permitted heading rename was false, which the implementer
caught: `docs/current-architecture.md:527` does link
`open-decisions.md#mail-workspace-freshness-threshold-and-retention-start`. Verified here;
ASSUMPTION 4 was correct and no heading in any of the sixteen files was renamed (checked
mechanically over every added and removed ATX heading line — the set is empty).

## Question 2 — did the implementation miss anything in the plan? D15 to D28

- **D15** canonical visual source — recorded, `design/README.md` § Evidence discipline. See F-007.
- **D16** whole-page drop and MCP raw import — recorded in FRD-12 § Assessment (no control, no
  dialog, immediate, registered parsers, fail-closed detection, provider-plus-sequence naming),
  FRD-10 § AI job and estimate tools (`pegasus_estimate_import`, full parameter and return
  contract, shared Core command, allocated ENG-033/AUTO-016, not delivered), FRD-06 § Canonical
  repair specifications (Case-plus-hash replay), `capabilities.md` EXT-12 and MCP-06, design
  README. Complete. The accessibility exception is documented as D16 requires; see F-005.
- **D17** rate cards, VAT, no savings, evidence-only codes — recorded in FRD-11 (§ Report-draft
  entry point derivation, the figure table, the immutability and no-savings paragraph), FRD-06
  (both sections), FRD-12 § Assessment and § Administration, FRD-04 Administrator column,
  `capabilities.md` EXT-09 and RPT-02, `open-decisions.md` (row resolved), design README.
  The "not yet derivable" paragraph and the readiness blocker are gone. See F-004.
- **D18** any Engineer, typed identity only — recorded in FRD-11 § Initial renderer activation
  and the readiness rail, FRD-04 Engineer row, `capabilities.md` RPT-02, design README both
  signature rows, `open-decisions.md` report-wording row narrowed. **Incompletely reconciled**:
  F-001 and F-002.
- **D19** report images — recorded in FRD-06, FRD-11 § Photographs (curation has left the UI-15
  deferral), FRD-12 evidence rail, `capabilities.md` RPT-02 and UI-15 (allocated ENG-031, not
  delivered). Complete.
- **D20** upload bounds, one submission, custody, grouped decision — recorded in
  `engineering.md` tier 10, FRD-05 § Supported source boundary (citing FRD-09 rather than
  restating the 30 MB envelope), FRD-02 (§ Request-scoped upload links and § Upload confirmation
  surface), FRD-12 § Upload and the route table, `capabilities.md` INT-31, `open-decisions.md`
  L128 note, design README § Upload. The interim `int-31-interim-v1` values and the as-built
  figures in `current-architecture.md` are untouched and still separable. Complete.
- **D21** absent not disabled — recorded in `boundaries.md` (three rows), `capabilities.md`
  EXT-13 and ENG-01, design README § Absent versus disabled (the Glass's/Audatex seam row
  removed), § Removed surfaces, § Deferred integration and intake surfaces. Experian and Cazana
  seams preserved under D7 as required. See F-003.
- **D22** mail freshness, no backfill, recoverable delete — recorded in FRD-08 (three places),
  `capabilities.md` UI-10, and `open-decisions.md` § Mail workspace freshness resolved with the
  table removed and the canonical owner named. Complete.
- **D23** versioned completeness set and configurable chase interval — recorded in FRD-01
  (blocker paragraph and § Due work), FRD-02 (the second seven-day literal at INT-32), FRD-12
  (Overview bullet and § Administration), `capabilities.md` CASE-18, `runbook.md`, design README
  § Administration. No bare "seven calendar days" survives anywhere in `docs/`. Complete.
- **D24** optional AI target percentage — recorded in FRD-11 § AI Job List and design README
  Send-to-Claude dialog; `open-decisions.md` slider row narrowed to the presentation questions
  that stay open. Complete.
- **D25** Triage History and Files — recorded in FRD-03, FRD-12 Triage detail, design README
  § Triage. Complete.
- **D26** direct Case creation receipt — recorded in FRD-02 § Ways intake starts, FRD-12 utility
  bar Add, `capabilities.md` INT-26 (allocated PLAT-059, not delivered). Complete.
- **D27** capacity tier not run — recorded in `capabilities.md` OPS-20, `engineering.md` tier 10,
  `boundaries.md`, each saying not run and never represented as passing, with PLAT-066 outside
  EPIC-011. Complete.
- **D28** administrator password reset — recorded in FRD-04 § Staff role access matrix and
  § Staff accounts, FRD-12 § Administration, design README Accounts bullet, and `capabilities.md`
  as the new row ACC-15 (decided 2026-09-01, allocated PLAT-064, not yet delivered). The
  controller's amendment from ACC-10 to ACC-15 was applied correctly: ACC-10 is genuinely taken
  by the authentication/security log. See F-006.

## Question 3 — did the simplification pass run honestly?

Yes. The plan carries the dated `## Simplification pass` heading twice: the docs-only "n/a"
that `AGENTS.md` step 4 prescribes, and an implementer entry that states it was run over the
real committed diff (12 commits, `25c14574`, 16 files, +333/−116) rather than anticipated.
Its claims are checkable and check out: each decision is stated once in the document
`AGENTS.md` gives it, FRD-05 and FRD-12 cite FRD-09 for the unchanged 30 MB envelope instead
of restating it, the `open-decisions.md` resolutions name their canonical owner, `capabilities.md`
carries allocation only, and four of the edits are pure deletions (the not-yet-derivable
paragraph, the signature-tuple gate, the Import estimate dialog entry, the Glass's/Audatex seam
row). The disposition is honest, not a formality.

## Acceptance checks verified by this review

- 16 files, all under `docs/`; no file added or deleted; nothing under `src/`, `tests/`,
  `scripts/`, `.github/`, `infra/`, `corpus/`, `docs/design/test-ui/`; `docs/operator-notes.md`,
  `docs/adr/**`, `docs/prd/**`, `docs/current-architecture.md`, `docs/operations.md`, FRD-07 and
  FRD-09 untouched. **PASS**
- No new Markdown file, no renamed heading anywhere in the diff. **PASS**
- `capabilities.md` arithmetic: 234 rows, 234 unique ids, 143 Now + 27 Next + 35 Later +
  29 Not planned = 234, `0.1.0-alpha.1` target column counts 143. **PASS**
- Every new cross-document link target and anchor resolves (13 anchors checked by heading
  slug, plus `current-architecture.md`). **PASS**
- Contradiction sweep over `docs/` excluding `json-extraction-parity/`, `current-architecture.md`,
  `operations.md` and `adr/`: no surviving "not yet derivable", "original-versus-assessed" or
  "savings" except as negations, no `M.Inst.IAEA` tuple gate, no unqualified "seven calendar
  days", no 25 MB bound, no 10 MiB bound outside FRD-09's unchanged provider envelope, no
  provisional 15 minutes, no completeness percentage except as a prohibition. **PASS**
- `docs/open-decisions.md`: rate-card row and mail-freshness section resolved with their content
  present in a canonical owner; assessment-markup, report-wording and slider rows narrowed to
  what is genuinely still open; § Later operator UI capabilities and § Manual upload deliberately
  untouched. **PASS**
- Required checks on this head: `documentation`, `changes`, `local-development-scripts`,
  `reference-data` SUCCESS; `infrastructure`, `unit`, `sql-integration`, `browser`, `test-ui`,
  `sql-integration-coverage` path-skipped for a docs-only change. `mergeStateStatus: CLEAN`.
  **PASS**

## Verdict and what remediation needs

`needs-changes` on two major findings, both in the D18 cluster, both surgical:

1. **F-001** — reconcile the two surviving signature clauses in
   `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` (L70-71 and L311) with the
   D18 paragraph four lines below them. As it stands the canonical FRD both asserts and denies
   that Infrastructure renders with a signature resource, and the design README rows this PR
   changed assert the denial against the higher-authority FRD — which `docs/index.md`
   § Authority order requires be fixed in the same commit.
2. **F-002** — restore the truthful description of delivered behaviour in `capabilities.md`
   `RPT-02` (the accepted engineer-signature check still ships) and mark the D18 sentence as
   decided and not delivered, the way the D17 and D19 sentences in that same row already are.
   `AGENTS.md` L220-222 forbids documenting a capability as delivered before a real caller.

F-003, F-004 and F-005 are minor and do not block, but the same remediation pass has these
files open and should take them.

## Residual risk

- No implementation ticket exists anywhere in the decision record for D18 report issuance, so
  `RPT-02` cannot name an allocation the way the D17, D19, D20, D22, D23, D26 and D28 rows do.
  The controller should allocate one; the reviewer did not invent a ticket id (F-008).
- D20's raised bounds are decided intent; the accepted `int-31-interim-v1` values and the
  as-built 10 MiB figures in `current-architecture.md` remain the truthful current policy. The
  two readings stay separable in the merged text — checked.
- The eight Codex threads on this head are dispositioned above but are left open on GitHub: the
  dispatch for this review directs that a `needs-changes` verdict leave the PR untouched, and
  `mergeStateStatus` is `CLEAN`, so unresolved conversations are not a merge gate on this
  repository. The controller or the implementer should close them out with the remediation.
- No merge was performed and no board stage was moved. The ticket stays in Review with its
  branch, worktree, PR and claim intact.
