# Checklist — DELIV-040

*One independently tickable box per ordered plan step, then the checks and the report. Append
progress notes; do not rewrite boxes.*

- [x] Step 1 — D17 rate cards: FRD-06 (§ Professional engineering findings, § Canonical repair specifications), FRD-11 (§ Report-draft entry point — the "not yet derivable" paragraph and its readiness item deleted; § Estimate VAT figures table corrected), FRD-12 (§ Assessment, § Administration), FRD-04 role matrix, `capabilities.md` `EXT-09`/`RPT-02`, design README (§ Assessment fields, § Administration) all carry the card selection, non-paint-labour derivation, VAT on the whole subtotal, evidence-only betterment/guide codes, and no comparison or savings.
- [x] Step 2 — D18 typed identity: FRD-11 (L19 bundle sentence, § Initial renderer activation tuple gate, readiness item), FRD-04 Engineer row, `capabilities.md` `RPT-02`, design README signature-asset rows (retained, inactive) all state that any Engineer-role user may issue with typed identity only.
- [x] Step 3 — `docs/open-decisions.md`: the rate-card row resolved; `## Mail workspace freshness threshold and retention start` resolved 2026-09-01 with the table removed; the assessment-markup, report-wording and Suggestions/PAV-slider rows narrowed to what is still open; the L128 upload-cap note records 100 MB / ~200 MB as the decided target with `int-31-interim-v1` unchanged. No unsettled row deleted.
- [x] Step 4 — D22 mail: FRD-08 states the fixed 15-minute stale threshold, the no-backfill retention-start boundary, and that permanent deletion is absent; `capabilities.md` `UI-10` records all three as decided 2026-09-01, not delivered.
- [x] Step 5 — D23 completeness and chase: FRD-01 (§ Lifecycle closure bullets and blocker paragraph, § Due work) states the versioned required/not-required set with exact blockers and never a percentage, and the configured interval (1–365, default 7, Europe/London, `Held` preserves); FRD-12, `capabilities.md` `CASE-18`, `runbook.md` § Release validation rules and design README § Administration agree.
- [x] Step 6 — D24 target estimate: FRD-11 § AI Job List `Estimate` row and the design README Send-to-Claude dialog state an optional 0–100 % value with no default, visibly derived from Engineer's Value, guidance only, still refused without an Engineer's Value.
- [x] Step 7 — D20 uploads: `engineering.md` tier 10, FRD-05 § Supported source boundary, FRD-12 § Upload, FRD-02 (§ Request-scoped upload links, § Upload confirmation surface), `capabilities.md` `INT-31` and design README § Upload carry 100 MB per file, ~200 MB per request, the unchanged 30 MB provider envelope (cited to FRD-09), one successful public submission with reconciling retry and non-disclosing refusal, durable-custody-only staff upload, and one grouped decision with per-file detail.
- [x] Step 8 — D16 import: FRD-12 § Assessment describes the whole-page immediate one-file drop with no confirmation and no picker, registered parser types only, fail-closed provider auto-detection, provider-plus-sequence Draft naming and the recorded facts; the pointer-only accessibility exception is documented in FRD-12 and the design README keyboard contract; FRD-10 has the `pegasus_estimate_import` row with its parameters and returns; FRD-06 states the same-Case-plus-same-hash replay; `capabilities.md` `EXT-12`/`MCP-06` record it as decided, not delivered; the Import estimate dialog and picker are removed from the design README and listed under § Removed surfaces.
- [x] Step 9 — D25 Triage: FRD-03, FRD-12 Triage detail and design README § Triage state the chronological merge of durable events with append-only attributable notes, corrections as new notes, no edit or delete, and a Files view of retained sources/attachments and linked vehicle images with no upload action.
- [x] Step 10 — D26 direct Case creation: FRD-02 § Ways intake starts and FRD-12's Add/Create Case entries state the attributable instruction receipt followed by the normal principal and Case/PO allocation policy with no parallel allocation; `capabilities.md` `INT-26` records it as decided, not delivered.
- [x] Step 11 — D19/D21/D27/D15: report-image curation (non-destructive, distinct Close-up first and Overview second, optional ordered supporting images, versioned attributable crop/order data, immutable issued snapshot) is in FRD-06, FRD-11 § Photographs and source evidence and FRD-12, and has left the UI-15 deferral in `capabilities.md`; exclusions read as absent not disabled in `boundaries.md`, `capabilities.md` (`EXT-13`, `ENG-01`) and the design README (§ Absent versus disabled seam row removed, § Removed surfaces, § Deferred integration surfaces); the 2,000-case tier-10 evidence is recorded as not run and never passing in `capabilities.md` `OPS-20`, `engineering.md` tier 10 and `boundaries.md`; the design README § Evidence discipline names the work-pack HTML as the canonical visual source.
- [x] Step 12 — D28 reset and the recount: FRD-04 (§ Staff role access matrix Administrator column, § Staff accounts), FRD-12 § Administration and the design README Accounts bullet describe the administrator-entered temporary password, existing policy and hashing, forced change, permanent record and never-emailed secret; `capabilities.md` has the new `ACC-10` row (`Now` / `0.1.0-alpha.1` / FRD-04 owner / decided 2026-09-01, `PLAT-064`, not delivered) and the allocation summary reads `Now` 143, total 234/234 recounted 2026-09-01, `0.1.0-alpha.1` 143.
- [x] No new Markdown file and no file outside the plan's Expected files: `git diff --name-only --diff-filter=A origin/dev...HEAD` prints nothing and `git diff --name-only origin/dev...HEAD` lists only the sixteen `docs/` files.
- [x] No heading renamed anywhere except the one resolved-decision heading in Step 3 (cross-document anchors are not checked by the link script, so a rename breaks them silently).
- [x] Every `docs/capabilities.md` row touched still states its real state — dated 2026-09-01, decided/allocated, never delivered — and the summary arithmetic is internally consistent.
- [x] Contradiction sweep clean: no surviving "not yet derivable", "original-versus-assessed", Pegasus savings/comparison feature, exact-signature-tuple gate, unqualified "seven calendar days", 10 MiB or 25 MB upload bound, Import estimate dialog or picker, Glass's/Audatex launch control, provisional 15-minute threshold, or completeness percentage in the edited files.
- [x] Nothing out of scope touched: no OCR content, no ADR, no PRD line, no `docs/operator-notes.md`, no `docs/current-architecture.md` or `docs/operations.md` current-state figure, no FRD-07/FRD-09, no `src/`, `tests/`, `scripts/`, `.github/` or `docs/design/test-ui/`.
- [ ] Test-runner role ran the CI `documentation` rail — `pwsh ./scripts/Test-TestMarkdownPlacement.ps1`, `pwsh ./scripts/Test-DocumentationLinks.ps1`, `pwsh ./scripts/Test-UiCatalogue.ps1` — plus `pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD`, with exact commands, cwd and exit codes recorded. The implementer did not run them.
- [ ] Simplification pass recorded in the plan under the dated `## Simplification pass` heading as "n/a — docs-only" before the PR opened.
- [ ] Post-implementation report written: files and sections changed per decision, the three recorded planner assumptions carried forward or corrected, every command with cwd/exit/result, any deviation with its observed text and line, and the PR URL plus head SHA.
- [ ] Stopped at the boundary: PR opened against `dev` titled "Record the 2026-09-01 operator interface decisions in the governing documents (DELIV-040)" with the `Kanmer: DELIV-040` footer, ticket moved `implementing` → `review`, nothing merged, no other ticket started.

## Progress notes

Append with `set_ticket_doc(doc: "checklist", append: true)`.

### 2026-09-02 — implementer attempt 1 (Claude Code, run 20260901T215000Z-claude-controller)

Steps 1–11 worked; Step 12 stopped on its own deviation rule. All sixteen Expected files are
edited in `C:\Users\PGUSER\Documents\github\pegasus-worktrees\deliv-040-governing-docs` on
`task/deliv-040-governing-docs`, **uncommitted** — see the two blockers below.

Evidence for the ticked verification boxes (nothing is committed, so the `origin/dev...HEAD`
forms could not run; the working-tree equivalents did):

- `status --porcelain` lists exactly the sixteen Expected files, all ` M`, no `??` and no `A`;
  `diff --name-only` lists the same sixteen. No new Markdown file, nothing outside scope.
- Heading check: every ATX heading line in all sixteen files is byte-identical to `HEAD`. No
  heading was renamed anywhere — including the mail-freshness heading (ASSUMPTION 4).
- Relative-link check over all sixteen files: every non-anchor relative target resolves.
- Contradiction sweep over `docs/` (excluding `json-extraction-parity`, `current-architecture.md`,
  `operations.md`, `adr/`): no remaining "not yet derivable"; the only "savings" and
  "original-versus-assessed" hits are the new negations; no `10 MiB` or `25 MB` upload bound; the
  only "Import estimate" hits state its removal; no "seven calendar days" that is not the named
  default; no signature-tuple gate; no "provisional" mail threshold; no completeness percentage
  except the new prohibition.
- `capabilities.md`: no row added, so the allocation-summary arithmetic is unchanged and still
  internally consistent (`Now` 142, total 233/233, `0.1.0-alpha.1` 142). Every touched row is
  dated 2026-09-01 and reads "decided / allocated / not delivered".

Two blockers, both reported to the controller rather than worked around:

1. **Step 12 — `ACC-10` is already in use.** `docs/capabilities.md` L93 is
   `ACC-10 | Separate authentication/security log`. `ACC-01`–`ACC-14` are all allocated; the next
   free id is `ACC-15`. The plan's Step 12 deviation stop and § Failure and deviation rules both
   say to stop and report rather than pick another number, so no row was added and no count was
   changed. D28's *behaviour* is recorded (FRD-04 § Staff role access matrix and § Staff accounts,
   FRD-12 § Administration, design README Accounts bullet); only the registry row and the recount
   are outstanding. ASSUMPTION 2 in `open-questions` needs re-dispositioning by the controller or
   the operator.
2. **The shell guard denies every Git mutation from this worker.** Recorded in
   `scratch/execution.md`. Cause: `hooks/pegasus-guard.ps1` L122 treats the *session* working
   directory as the target, and a Claude Code subagent inherits the primary checkout as its cwd, so
   rule 8 fires regardless of the `-C` worktree argument. No commit, push or PR was possible.

The MCP tool name `pegasus_estimate_import` was checked for collision before use: no pre-existing
use anywhere in `docs/` or `src/`.


### 2026-09-02 — implementer attempt 1, controller round 2

Both blockers cleared by the controller and the work committed.

- Guard rule 8 corrected at 01:41Z; staging and committing from the worktree verified working.
- Step 12 completed with the controller-assigned id: `ACC-15` (`Now` / `0.1.0-alpha.1` / FRD-04
  § Staff role access matrix / decided 2026-09-01, `PLAT-064`, not delivered), inserted with the
  other `Now` account rows. Allocation summary recounted to `Now` 143, total 234 capabilities and
  234 unique IDs (mechanical recount 2026-09-01), `0.1.0-alpha.1` 143. Verified mechanically: 234
  rows, 234 unique ids, and 143 + 27 + 35 + 29 = 234.
- ASSUMPTION 4 accepted and ticked by the controller; the mail-freshness heading stays unchanged.

Twelve commits on `task/deliv-040-governing-docs`, HEAD `25c14574a9e34c77e977f8a8eb203c2fe85dc13e`
(`48cb2816`, `33811713`, `d0527096`, `d161ae1e`, `399f1ac8`, `4525afcc`, `436b38dc`, `4089599e`,
`bd140c7c`, `e670d4fd`, `1441550f`, `25c14574`). Not pushed — READY_FOR_TESTS.

Committed-state scope evidence, cwd the worktree:

- `diff --name-only --diff-filter=A origin/dev...HEAD` → empty (no file added).
- `diff --name-only --diff-filter=D origin/dev...HEAD` → empty (no file deleted).
- `diff --name-only origin/dev...HEAD` → exactly the sixteen Expected `docs/` files.
- `diff --stat origin/dev...HEAD` → 16 files changed, 333 insertions, 116 deletions.
- `status --porcelain` → clean.

Owed to the test-runner role: the CI `documentation` job's three steps plus the placement validator
invoked directly with `-Base origin/dev -Head HEAD`, exactly as the plan's § Commands lists them.
The implementer ran none of them and ran no build (docs-only).
