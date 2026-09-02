# Plan — DELIV-040: Record the 2026-09-01 operator interface decisions in the governing documents

**Diff estimate first (plan sizing, `docs/engineering.md` § Plan sizing):** 16 existing Markdown
files, approximately **+210 / −95 lines**, one commit or twelve, no new file, no code, no test, no
generated artifact, no dependency. Twelve steps — one per decision group in the ticket body's
table. There is no "run what CI runs" step: the test-runner role owns the commands below.

## Objective

Edit the sixteen named canonical documents so that the operator decisions **D15–D28**, confirmed
binding on 2026-09-01, are recorded as governing intent — and so that no governing document still
states the opposite. Nothing is implemented and nothing is claimed as delivered.

## Starting state

Verified read-only on `origin/dev` = worktree HEAD `9b8f78a36151313bc6d48625edee7f13a2173127`
(`C:\Users\PGUSER\Documents\github\pegasus-worktrees\deliv-040-governing-docs`, branch
`task/deliv-040-governing-docs`; both `--git-common-dir` values resolve to
`C:\Users\PGUSER\Documents\github\pegasus\.git`).

What the documents say today that the decisions contradict or leave unsaid:

- `frd-11` L131–137 states **"Repair-cost figures are not yet derivable… No accepted formula
  exists"** and L73–81 accepts engineer identity **only as exact signature tuples** (`A Patterson |
  M.Inst.IAEA | andy_patterson`), holding Ed Mawdsley and Neil O'Reilly until a qualification
  completes their tuple. L124–130 defers report-image curation to **UI-15**. L277–278 computes
  `Labour` as "Labour hours × labour rate" and `Paint` as "Paint hours × paint labour rate, plus
  paint materials".
- `capabilities.md` L252 (`EXT-09`) still names **"original-versus-assessed comparison, and
  savings"** as the durable outcome; L267 (`RPT-02`) repeats the not-derivable claim; L145
  (`CASE-18`) is a **"Seven-calendar-day"** schedule; L197 (`OPS-20`) records the 2,000-case
  capacity outcome with no not-run qualification.
- `design/README.md` L700–704 draws **Glass's / Audatex** as disabled seams; L974–989 keeps the
  **Import estimate** control and dialog, the `Labour rate / Paint labour rate` fields and the
  `Target Estimate % slider`; L994 says uploads are **"up to 25 MB each · 10 files"**.
- `engineering.md` L85 bounds uploads at **10 MiB per file** plus a 64 KiB multipart envelope.
- `frd-01` L136 fixes the chase at **seven calendar days**; `runbook.md` L903–904 repeats it.
- `frd-04` has **no administrator password reset** and no rate-card administration.
- `frd-08` L224–226 names a stale state with **no threshold**; `open-decisions.md` L376–384 holds
  the 15-minute threshold and the retention start as **provisional**.
- `frd-10` L59–79 has **no raw estimate-import tool**; `frd-02` L6 has no direct-Case-creation
  receipt; `frd-03` has no History/Files contract; `frd-05` states no numeric size limit.
- `open-decisions.md` L352 holds the rate-card ownership question open; L353 holds betterment,
  the `guide` code and signatory-list ownership open.

Evidence: `files`@this ticket's current version (written in the same planning pass), EPIC-011
`context.md` § 1 and § 2, EPIC-011 `decisions/2026-09-01-work-pack.md`. All three are the pinned
inputs; if any changes, re-read before executing.

Sources: no project source registry applies to a docs-only change; no MCP or llms.txt source was
resolved or consulted.

## Governing docs

The operator confirmed D15–D28 as binding on 2026-09-01 (EPIC-011
`decisions/2026-09-01-work-pack.md`, "Interface decisions confirmed binding on 2026-09-01"). That
confirmation is the **explicit authorization** this plan relies on to *modify* governing documents.

| Ref | Relationship |
|---|---|
| `docs/frd/frd-01-case-identity-and-lifecycle.md` | **Modifies** (authorized 2026-09-01, D23) — completeness set and chase interval. |
| `docs/frd/frd-02-intake-and-source-identity.md` | **Modifies** (D20, D26) — upload-link single submission, grouped decision, direct-Case instruction receipt. |
| `docs/frd/frd-03-triage.md` | **Modifies** (D25) — History and Files contract. |
| `docs/frd/frd-04-parties-accounts-and-access.md` | **Modifies** (D17, D18, D28) — role matrix and the reset action. |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | **Modifies** (D20) — exact intake bounds. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | **Modifies** (D16, D17, D19). |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | **Modifies** (D22). |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | **Modifies** (D16) — one tool row. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | **Modifies** (D17, D18, D19, D24). |
| `docs/frd/frd-12-operator-experience.md` | **Modifies** (D16, D17, D19, D20, D21, D23, D25, D26, D28). |
| `docs/capabilities.md` | **Modifies** (all) — registry and schedule only; the operator authority the allocation rules at L319 require is the 2026-09-01 confirmation. |
| `docs/boundaries.md` | **Modifies** (D21, D27) — new boundary rows. |
| `docs/open-decisions.md` | **Modifies** — D17 and D22 resolve rows; D18, D20 and D24 narrow rows. Resolved content moves to its canonical owner (L7). |
| `docs/design/README.md` | **Modifies** (D15–D25, D28) — working rules within its scope. |

**New ADR: none.** `0037` is reserved for OCR (`TICK-041`/`PLAT-065`). The durable technical shapes
these decisions imply are **owed by the feature tickets, not by this one**: the shared raw-import
Core command with a Web and an MCP caller and its `(Case, source hash)` replay key → `ENG-033` /
`AUTO-016`; the global versioned labour-rate-card aggregate → `TICK-082`; the normalized,
versioned curation snapshot retained by an issued report → `ENG-031`. This plan records only the
behaviour, which is what an FRD owns (`AGENTS.md` L255–258).

Two documents outside the `refs` list are edited under the same authorization because they carry
the same falsified statements: `docs/engineering.md` (tier 10 upload bounds and the capacity tier)
and `docs/runbook.md` (two chase bullets). Both are "working rules within their scopes"
(`docs/index.md` L29–34) and downstream of the FRDs being corrected.

## Required changes

One subsection per group in the ticket body's table. Every statement is **decided governing
intent**; no row may read as delivered.

### D17 — Rate cards, VAT, no comparison or savings, betterment and guide codes as evidence

Multiple **global, versioned labour-rate cards** (id, name, non-paint hourly rate, enabled state,
actor, timestamps) exist as Administrator-managed configuration. Staff select one card for every
new or amended estimate version; disabling a card blocks future selection without changing
history; imported provider versions and their printed totals stay immutable; an Engineer successor
selects a card and can become the accepted/report version. **Non-paint labour = normalized
non-paint hours × the selected card's rate.** Paint labour, paint materials, parts and other costs
are **explicit amounts**, not derived. The **VAT percentage belongs to the estimate version and
applies to the whole subtotal** (this already holds at `frd-11` L280). Betterment figures and
estimate `guide` codes are **retained evidence only** — no semantics are derived from them. **No
original-versus-assessed comparison or savings feature exists**, in the editor or on the report.
Repair-cost figures therefore **cease to be undeliverable**: the `frd-11` L131–137 paragraph and
the matching `RPT-02` clause must go, replaced by the accepted derivation, with readiness no
longer naming "Repair cost figures" as a permanent blocker. Rate-card administration is placed
**inside the Workflow configuration admin area** (see Constraints — recorded assumption 1).

### D18 — Any Engineer issues a report, typed identity only

Any user in the `Engineer` role may issue a report. Reports render **typed Engineer identity
only**; handwritten signature assets and qualification strings are **not required**. The supplied
signature assets stay governed but **inactive**. The exact-matching-tuple gate at `frd-11` L73–81
and the readiness item "an accepted engineer signature tuple" at L112 are replaced by the typed
identity of the issuing Engineer. Generation stays deterministic, versioned, retained and
review-gated, and generation, approval, issue, sending, receipt and Case closure remain distinct
events. The `open-decisions.md` report-wording row keeps only what is genuinely still absent:
salvage Categories A/B/N wording, recovery/storage wording and a final statement of truth.

### D22 — Mail freshness fixed at 15 minutes, no backfill, delete is a move

The mail-workspace stale threshold is a **fixed 15 minutes**. There is **no historical backfill**:
the list shows the genuine retention-start boundary and says the gap exists. **Delete** is a
recoverable move to Deleted Items; **permanent deletion is absent**. The two provisional numbers
leave `open-decisions.md` entirely.

### D23 — Versioned completeness set; configurable chase interval

Completeness (instruction and image) is a **versioned required/not-required set with exact
blockers, never a percentage**; the policy key and version are already recorded per gate
(`frd-01` L59) and the existing prohibition on an opaque aggregate (L55) is strengthened to name
the percentage explicitly. The chase interval is **one global whole-calendar-day value, range
1–365, default 7, calculated in Europe/London**; `Held` preserves the remaining time and release
resumes it. Every fixed "seven calendar days" statement becomes the configured interval with 7 as
its default.

### D24 — AI target estimate optional, 0–100 %, no default, guidance only

The Send-to-Claude target estimate is an **optional 0–100 % request value with no default**. Its
amount is **derived visibly from Engineer's Value** and is **proposal guidance only**. The job is
still refused without an Engineer's Value. The `frd-11` AI-Job-List `Estimate` row's Input cell
and the design README dialog description both carry it; the slider's remaining presentation
questions stay open in `open-decisions.md`.

### D20 — Upload limits, one public submission, durable custody, grouped decision

**100 MB per file**; **approximately 200 MB per multipart request**; the **Provider API envelope
remains 30 MB** (owned by FRD-09 — cite, do not restate). A **public link accepts one successful
submission**; an identical retry reconciles the same result; a different later submission,
revocation or expiry is **refused without Case disclosure**. Authenticated staff `/Upload` remains
available **only with durable production intake/case custody** — no production-local-only store is
accepted. A **grouped upload exposes one submission decision with per-file processing and outcome
details beneath it**. The interim `int-31-interim-v1` values stay the truthful current state;
`INTK-052` owns the Core change.

### D16 — Whole-page pointer drop and MCP raw-artifact import

**One shared Core command** accepts Case id, expected version, edit lease, operation key,
filename, media type, bytes and channel. The **Web caller** accepts one file dropped **anywhere on
the Assessment page**, imports **immediately with no confirmation and no visible picker**, accepts
**only currently registered parser types**, **auto-detects provider/parser and fails closed on
ambiguity**, names Drafts by **provider plus sequence**, and records filename, hash,
provider/parser, actor, channel and outcome. The **MCP caller reuses the same use case** with
`case_id`, `expected_version`, `edit_lease_token`, `operation_key`, `file_name`, `media_type` and
base64 bytes, returning Draft identity/name/status, replay state, source hash, parser/provider and
structured blockers/errors. **Same Case plus same hash is an idempotent replay; a different
artifact creates the next immutable Draft.** The **Import estimate dialog and picker are removed**.
The pointer-only drop is a **documented narrow accessibility exception** — every other route to
import remains keyboard-reachable and the exception is named where the keyboard contract and the
acceptance evidence live, never left implicit.

### D25 — Triage History with append-only notes; Files without upload

`History` merges durable events and **append-only attributable staff notes** in chronological
order. **Corrections are new notes; edit and delete do not exist.** `Files` contains retained
request sources/attachments and **linked vehicle images with view/download**. **No arbitrary
Triage file store or upload action is added.**

### D26 — Direct Case creation with an attributable instruction receipt

Staff enter the required identity and **attach or record the instruction**. Pegasus persists an
**attributable intake receipt**, then **reuses the normal principal and Case/PO allocation
policy**. **No parallel allocation implementation is allowed.**

### D19 — Report images

Image preparation is **non-destructive**: retained source bytes and hashes never change. A report
requires **distinct images designated `Close-up` first and `Overview` second**; optional supporting
images follow in **explicit operator order**. Crop and ordering data are **normalized, versioned,
attributable and protected by expected-version/edit-lease rules**. An **issued report retains the
exact curation snapshot and source hashes** even after later Case-image or curation changes.
Report-image curation therefore **leaves the UI-15 deferral** (`ENG-031` owns it); the rest of the
UI-15 workbench stays `Later`.

### D21 / D27 / D15 — Absent not disabled; capacity tier not run; canonical visual source

**D21:** an excluded capability is **absent from the interface, never drawn as a disabled
control**. The **direct Glass's and Audatex service-launch controls are removed** (`ENG-030`); a
**standalone Images list**, **runtime-managed templates** and **autonomous outbound sending** are
not built (staff-initiated outbound delivery stays in scope under ADR-0036). **Experian and Cazana
remain disabled seams** under the narrowed D7; manual Glass's, Cazana and Engineer valuation
records stay active. **D27:** the 2,000-case capacity tier (tier-10 cohort/soak evidence) is **not
run by this programme and is never represented as passing**; its evidence spike (`PLAT-066`) sits
outside EPIC-011, and per-ticket concurrency tests still run. Live Experian, Cazana and Glass's
provider integrations remain approved exclusions. **D15:** the work-pack
`Pegasus_UI_Frontend_Design_Premium_Full_End_State.html` is the **canonical visual execution
source**; a visual conflict pauses only the affected lane and its dependants.

### D28 — Administrator password reset

An Administrator **enters and confirms a compliant temporary password** for a staff account. The
**existing password policy and hashing apply**; the **existing forced-change state is set**; the
action is **permanently recorded**; the secret is **never emailed, logged, persisted raw or placed
in analytics**. It is an Administrator-only action on the Staff accounts table and appears in the
role matrix's Administrator column.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `docs/frd/frd-01-case-identity-and-lifecycle.md` | D23 completeness set and chase interval. Not generated. |
| Modify | `docs/frd/frd-02-intake-and-source-identity.md` | D20 upload-link and grouped decision; D26 instruction receipt. Not generated. |
| Modify | `docs/frd/frd-03-triage.md` | D25 History and Files. Not generated. |
| Modify | `docs/frd/frd-04-parties-accounts-and-access.md` | D17/D18 role matrix; D28 reset action. Not generated. |
| Modify | `docs/frd/frd-05-documents-extraction-and-custody.md` | D20 exact intake bounds. Not generated. |
| Modify | `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | D16 provenance/replay; D17 evidence-only codes; D19 curation. Not generated. |
| Modify | `docs/frd/frd-08-email-mailbox-and-background-processing.md` | D22 freshness, retention start, delete. Not generated. |
| Modify | `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | D16 MCP import tool row. Not generated. |
| Modify | `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | D17 derivation and figures; D18 typed identity; D19 curation; D24 target percentage. Not generated. |
| Modify | `docs/frd/frd-12-operator-experience.md` | D16/D17/D19/D20/D21/D23/D25/D26/D28 operator behaviour. Not generated. |
| Modify | `docs/capabilities.md` | Registry rows, new `ACC-10`, allocation-summary arithmetic. Not generated. |
| Modify | `docs/boundaries.md` | D21/D27 boundary rows. Not generated. |
| Modify | `docs/open-decisions.md` | Resolve D17 and D22 rows; narrow D18, D20, D24 rows. Not generated. |
| Modify | `docs/design/README.md` | D15–D25, D28 working visual/interaction rules. Not generated. |
| Modify | `docs/engineering.md` | Tier 10 upload bounds and the not-run capacity tier. Not generated. |
| Modify | `docs/runbook.md` | Two release-validation chase bullets. Not generated. |
| Inspect | `AGENTS.md` | Document ownership, routing and the simplification-pass rule. Read only. |
| Inspect | `docs/index.md` | Authority order and the new-Markdown rule. Read only. |
| Inspect | `docs/frd/frd-09-provider-and-intermediary-routes.md` | Owns the unchanged 30 MB provider envelope. Read only. |
| Inspect | `docs/current-architecture.md` | The truthful as-built 10 MiB upload bound. Read only. |
| Inspect | `.github/workflows/ci.yml` | The `documentation` job's three steps. Read only. |

## Do not modify

- `docs/operator-notes.md`
- `docs/adr/**`
- `docs/prd/**`
- `docs/current-architecture.md`
- `docs/operations.md`
- `docs/frd/frd-07-eva-and-external-engineering-handoff.md`
- `docs/frd/frd-09-provider-and-intermediary-routes.md`
- `docs/frd/README.md`
- `docs/index.md`
- `AGENTS.md`
- `src/**`
- `tests/**`
- `scripts/**`
- `.github/**`
- `infra/**`
- `corpus/**`
- `docs/design/test-ui/**`
- `docs/json-extraction-parity/**`
- `docs/principal-rules-and-mappings/**`

Justifications are in the `files` document's Do-not-modify table. The two that matter most:
`docs/operator-notes.md` carries no conflicting statement, so nothing is owed there; the PRD
carries no statement any decision contradicts (grep evidence in `files`), so no PRD line is
demanded.

## Constraints

- **No new Markdown file.** `scripts/Test-MarkdownPlacement.ps1` L27–31 allows a new `.md` only
  under `docs/prd|frd|adr|design`, and `docs/index.md` § New Markdown files restricts a new file
  to a PRD, FRD or ADR. Every change edits an existing file.
- **Do not rename or renumber any heading.** `scripts/Test-DocumentationLinks.ps1` checks link
  *paths* only, so a renamed heading breaks cross-document anchors silently. `capabilities.md`
  and `open-decisions.md` both link into FRD headings. Edit bodies, add sub-paragraphs, keep
  heading text byte-identical — except the one heading the resolved-decision pattern requires
  (Step 3), which no document links to.
- **`capabilities.md` never holds normative behaviour** (`AGENTS.md` L263–265). A row states the
  durable outcome, horizon, target release, canonical owner and activation/boundary — the
  behaviour itself goes in the FRD.
- **Truthfulness.** Every capability row records these decisions as **decided/planned governing
  intent, dated 2026-09-01**, never as delivered. `Now` still means "current proof and QDOS-alpha
  outcome", not activation (`capabilities.md` L310–312). No row may imply a caller, deployment or
  acceptance that does not exist.
- **`Now` targets only `0.1.0-alpha.1`** (`capabilities.md` L312), so the new `ACC-10` row uses
  that target and the allocation-summary arithmetic must move with it.
- **ADR bodies are immutable.** D20's staff-upload custody clause is written into FRD-05/FRD-12,
  not into ADR-0003; the `open-decisions.md` § Manual upload row stays open.
- **OCR stays out.** `prebuilt-layout`, the no-retained-raw-response rule and the measured
  threshold belong to `TICK-041`/`PLAT-065` and ADR-0037.
- **Design vocabulary.** Where `docs/design/README.md` gains prose, follow its own § Voice and
  § No explanatory copy rules — labels, values and controls, no hint sentences. Consult
  `.agents/skills/razor-pages-ui-design/SKILL.md` only if a design-token or component name is
  needed; none is expected, and the file may not exist on this branch.
- **`governing_docs` lock.** Those five path groups belong to DELIV-040 alone while it is open.
- **Recorded planner decisions (headless, per the assumption rule).** Three decisions the
  governing documents did not settle were taken and logged to this ticket's `open-questions`:
  1. **Rate-card administration is placed inside the Workflow configuration admin area** as its
     own labelled group, keeping eight admin areas. Most consistent with FRD-12 § Administration
     (which fixes eight areas and, under D2, folds related administration into an existing area
     rather than adding one), with `docs/design/README.md` § Administration and § Removed surfaces
     (Organisations, Access review and Roles were folded away), and with `ACC-07` "Application and
     workflow configuration managed by Administrators". Alternative rejected: a ninth
     "Rate cards" area.
  2. **D28 gets a new capability row `ACC-10`** rather than widening `ACC-03`, because
     `capabilities.md` is the ID registry and `ACC-03`'s "Required and accepted before
     `0.1.0-alpha.1`" note would otherwise falsely cover an undelivered action. Alternative
     rejected: extend `ACC-03`'s durable outcome.
  3. **The D16 MCP tool is named `pegasus_estimate_import`**, matching the existing
     `pegasus_estimate_save` / `pegasus_estimate_list` convention under `automation.assessment`.
     A trivial default; `AUTO-016` may rename it when it composes the tool.

## Ordered steps

Steps are independent except where a precondition says otherwise; Step 3 must follow Steps 1 and
2, and Step 12 must be last. Each step's `Files` entries all appear in Expected files and none
appears in Do not modify. `Symbols` is omitted throughout: these are prose documents, file-scoped
reconciliation is the complete bound.

### Step 1 — D17: rate cards, VAT, no comparison or savings
- Preconditions: none.
- Files: `docs/frd/frd-06-vehicle-and-engineering-evidence.md`, `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, `docs/frd/frd-12-operator-experience.md`, `docs/frd/frd-04-parties-accounts-and-access.md`, `docs/capabilities.md`, `docs/design/README.md`
- Change: FRD-06 § Professional engineering findings and correction — betterment and guide codes are retained evidence only. FRD-06 § Canonical repair specifications — the selected rate-card version joins the retained calculation basis. FRD-11 § Report-draft entry point — **delete** the "Repair-cost figures are not yet derivable" paragraph and its readiness item, replacing them with the accepted derivation and the statement that a report version records the card it used. FRD-11 § Estimate VAT on the rendered report — correct the `Labour` row to normalized non-paint hours × the selected card rate, make `Paint` (paint labour + paint materials), `Parts` and `Other` explicit amounts, keep `VAT = Subtotal × VAT %`, and state that no comparison or savings figure exists and that imported provider versions and printed totals are immutable. FRD-12 § Assessment — the estimate set names the selected labour-rate card, the estimate-version VAT applied to the whole subtotal, and no savings figures. FRD-12 § Administration — rate-card administration inside Workflow configuration. FRD-04 role matrix — Administrator may administer rate cards. `capabilities.md` `EXT-09`: remove "original-versus-assessed comparison, and savings" from the durable outcome and record the accepted derivation as decided 2026-09-01, allocated to `TICK-082`, not yet delivered; `RPT-02`: drop the not-derivable clause the same way. `design/README.md` § Assessment — replace the `Labour rate, Paint labour rate` fields with the card selection and explicit paint amounts, drop comparison/savings from the totals list; § Administration — the Workflow-configuration bullet gains rate cards.
- Preserved behaviour: the four report outcomes and their headline figures; the D9 rule that the Current estimate's VAT % overrides the built-in repairer rule; every heading name.
- Forbidden: writing a WU÷10 formula, a sundry percentage or a material band; claiming rate cards exist in code; adding a savings or comparison figure anywhere.
- Negative cases: a search for `savings`, `original-versus-assessed`, `not yet derivable` across `docs/` (excluding `docs/json-extraction-parity/`) must return nothing in these files afterwards.
- Tests: none (docs-only).
- Commands: see § Commands — run by the test-runner role.
- Expected output: `git diff` touches only the named sections.
- Done when: no governing document still says repair-cost figures are underivable, and no document offers a comparison or savings feature.
- Deviation stop: if removing the readiness item would leave `frd-11` § Report-draft entry point self-contradictory in a way this step cannot fix within the section, stop and report.

### Step 2 — D18: any Engineer, typed identity only
- Preconditions: none.
- Files: `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, `docs/frd/frd-04-parties-accounts-and-access.md`, `docs/capabilities.md`, `docs/design/README.md`
- Change: FRD-11 L19 assessment bundle — "the statement and the typed Engineer identity". FRD-11 § Initial renderer activation — replace the exact-matching-tuple paragraph: any Engineer-role user may issue; the report renders typed identity only; signature assets and qualification strings are not required; the supplied assets remain governed but inactive; generation stays deterministic, versioned, retained and review-gated; generation, approval, issue, sending, receipt and closure remain distinct. FRD-11 § Report-draft entry point readiness — remove "an accepted engineer signature tuple". FRD-04 role matrix — the `Engineer` row records report issue. `capabilities.md` `RPT-02` — replace "the accepted engineer-signature check" with the typed-identity rule, dated 2026-09-01. `design/README.md` § Web and renderer boundary (Supplied engineer signatures row) and § Source and runtime map (Engineer signatures row) — retained, governed, inactive; not Web decorative imagery.
- Preserved behaviour: the fail-closed rule for missing or substituted wording; the master-logo and template asset boundaries; the "Not planned — digital signatures" line, which is document signing and unrelated.
- Forbidden: implying a signature asset is now rendered; deleting the governed assets from the asset tables.
- Negative cases: no document may still require a qualification string to issue a report.
- Tests: none.
- Commands: see § Commands.
- Expected output: the tuple gate is gone; the asset rows still exist, marked inactive.
- Done when: typed Engineer identity is the only identity requirement stated anywhere.
- Deviation stop: if an ADR is found to own the tuple gate, stop and report rather than editing the ADR.

### Step 3 — Resolve and narrow the open-decisions rows D17, D18, D22 and D24 settle
- Preconditions: Steps 1 and 2 are complete, so the settled content already lives in its canonical owner; Step 4 supplies the D22 owner text if it runs first, otherwise re-run this step's D22 part after Step 4.
- Files: `docs/open-decisions.md`
- Change: **Resolve** the `Rate-card ownership and accepted derivation formulas (EXT-09)` row — remove it and point to FRD-11 § Estimate VAT on the rendered report and FRD-06 § Canonical repair specifications. **Resolve** the whole `## Mail workspace freshness threshold and retention start` section using the pattern at L286: rename the heading to `## Mail workspace freshness threshold and retention start — resolved 2026-09-01`, add a bold **Resolved.** paragraph (fixed 15 minutes; no backfill; genuine retention-start boundary), keep the settled bullets, delete the table. **Narrow** the `Assessment markup ambiguities` row: betterment semantics, the estimate `guide` code meaning and approved signatory-list ownership are settled — leave only the fee-field placement and the valuation-figure storage questions. **Narrow** the `Report wording outside the approved assessment baseline` row: the incomplete identity tuples are no longer a blocker; salvage Categories A/B/N wording, recovery/storage wording and the statement of truth stay absent. **Narrow** the Suggestions/PAV-slider row: the target percentage is settled (optional, 0–100 %, no default, guidance only); the Suggestions screen's fate, the `.send-action` contrast shortfall and the ratio basis stay open. **Update** the L128 note to record 100 MB per file and approximately 200 MB per request as the decided target owned by `INTK-052`, leaving `int-31-interim-v1` as the current interim.
- Preserved behaviour: every row this ticket does not settle, including § Later operator UI capabilities and § Manual upload in a deployed environment; the L11 settled-clause links.
- Forbidden: deleting a question without its answer existing in a canonical owner; resolving a row on this ticket's own authority rather than the 2026-09-01 confirmation.
- Negative cases: no resolved row may leave a dangling reference from another document.
- Tests: none.
- Commands: see § Commands.
- Expected output: five row edits and one section resolution.
- Done when: every register row the decisions settle is resolved or narrowed, and each resolution names its new owner.
- Deviation stop: if a row's settled content cannot be found in a canonical owner, stop — the earlier step is incomplete.

### Step 4 — D22: mail freshness, retention start, delete
- Preconditions: none.
- Files: `docs/frd/frd-08-email-mailbox-and-background-processing.md`, `docs/capabilities.md`
- Change: FRD-08 (workspace refresh paragraph) — the stale threshold is a fixed 15 minutes. FRD-08 (default workspace view paragraph) — no historical backfill; the list shows the genuine retention-start boundary and says the gap exists. FRD-08 § Outbound correspondence **Delete** bullet — add that permanent deletion is absent. `capabilities.md` `UI-10` — record the fixed threshold, the no-backfill rule and recoverable delete as decided 2026-09-01 (`MAIL-031`, `TICK-054`), not as delivered.
- Preserved behaviour: the existing read-only Deleted Items search scope; the no-auto-refresh-while-reading rule; the reason requirement on Delete.
- Forbidden: presenting a reconstructed history; claiming a hard delete exists.
- Negative cases: no document may still call 15 minutes provisional.
- Tests: none.
- Commands: see § Commands.
- Done when: the threshold, the retention boundary and the delete semantics are stated once each, in FRD-08.
- Deviation stop: a conflicting threshold elsewhere in `docs/` — report it, do not silently change a file outside Expected files.

### Step 5 — D23: versioned completeness set and configurable chase interval
- Preconditions: none.
- Files: `docs/frd/frd-01-case-identity-and-lifecycle.md`, `docs/frd/frd-12-operator-experience.md`, `docs/capabilities.md`, `docs/runbook.md`, `docs/design/README.md`
- Change: FRD-01 § Lifecycle closure and correspondence — the completeness gates are a versioned required/not-required set with exact blockers, never a percentage; strengthen the existing opaque-aggregate prohibition to name a percentage. FRD-01 § Due work, chasing, and action history — replace the fixed seven days with the configured global interval (1–365 whole calendar days, default 7, Europe/London); `Held` preserves the remainder and release resumes it. FRD-12 § Case workspace outstanding-requirements text and § Administration — the same set, and the chase interval as Workflow configuration. `capabilities.md` `CASE-18` — the durable outcome becomes the configurable whole-calendar-day chase schedule (1–365, default 7); activation notes the change as decided 2026-09-01, allocated to `PLAT-062`, not delivered. `runbook.md` § Release validation rules — the two chase bullets take the configured interval with 7 as the default. `design/README.md` § Administration Workflow-configuration bullet — completeness as versioned required/not-required rules with exact blockers, and the chase-interval field with its range and default.
- Preserved behaviour: the Europe/London day boundary; the Monday-to-Monday week; the rule that a save of unrelated data never resets readiness.
- Forbidden: introducing a completeness percentage anywhere; leaving a bare "seven calendar days" in any edited file.
- Negative cases: a search for `seven calendar days` across the six files must return only text where 7 is named as the default.
- Tests: none.
- Commands: see § Commands.
- Done when: completeness is a versioned set everywhere and the interval is configurable everywhere.
- Deviation stop: a seven-day literal in a file outside Expected files — report it.

### Step 6 — D24: optional AI target estimate
- Preconditions: none.
- Files: `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, `docs/design/README.md`
- Change: FRD-11 § AI Job List — the `Estimate` kind's Input becomes direction text plus an optional 0–100 % target percentage with no default, whose amount is derived visibly from the recorded Engineer's Value and is proposal guidance only; still refused without an Engineer's Value. `design/README.md` § Assessment Send-to-Claude dialog — the optional target percentage with no default (drop "slider" as the fixed mechanism only if the design contract allows; otherwise keep the control and add the range, absence of default and guidance-only meaning).
- Preserved behaviour: the closed job-kind catalogue; the refusal without an Engineer's Value; the staff-confirmation column.
- Forbidden: giving the percentage a default; letting it drive an accepted figure.
- Negative cases: no document may state or imply a default percentage.
- Tests: none.
- Commands: see § Commands.
- Done when: the optional, defaultless, guidance-only contract is stated in both places.
- Deviation stop: none expected.

### Step 7 — D20: upload limits, one public submission, durable custody, grouped decision
- Preconditions: none.
- Files: `docs/engineering.md`, `docs/frd/frd-05-documents-extraction-and-custody.md`, `docs/frd/frd-12-operator-experience.md`, `docs/frd/frd-02-intake-and-source-identity.md`, `docs/capabilities.md`, `docs/design/README.md`
- Change: `engineering.md` tier 10 — replace the 10 MiB file limit and the 10 MiB-plus-64-KiB envelope with 100 MB per file and approximately 200 MB per multipart request (the Provider API envelope stays 30 MB, owned by FRD-09). FRD-05 § Supported source boundary — state the same three bounds, citing FRD-09 for the provider envelope. FRD-12 § Upload — the limits, the one grouped submission decision with per-file detail beneath it, and that staff `/Upload` is available only with durable production intake/case custody. FRD-12 route table `/Uploads/{token}` context and FRD-02 § Request-scoped upload links — one successful submission per link; identical retry reconciles; a different later submission, revocation or expiry is refused without Case disclosure. FRD-02 § Upload confirmation surface — one grouped submission decision above the per-file details. `capabilities.md` `INT-31` — record the single-successful-submission rule and the raised limits as decided 2026-09-01 (`INTK-050`, `INTK-052`), with the interim values still in force. `design/README.md` § Upload — the dropzone string's "up to 25 MB each · 10 files" becomes the decided per-file bound with the request bound named.
- Preserved behaviour: the non-disclosing error classes; the token's security-sensitive handling; the existing per-file confirmation decision table; the interim `int-31-interim-v1` values in `open-decisions.md` and the as-built figures in `current-architecture.md`.
- Forbidden: changing FRD-09's 30 MB envelope; editing ADR-0003; restating the interim limits as the decided ones or vice versa.
- Negative cases: a `10 MiB` or `25 MB` upload bound must not remain in any edited file.
- Tests: none.
- Commands: see § Commands.
- Done when: the three bounds and the single-submission rule read identically in every edited file, and current state is still separable from intent.
- Deviation stop: a fourth, different bound found in a governing document — report it.

### Step 8 — D16: whole-page pointer drop and MCP raw-artifact import
- Preconditions: none.
- Files: `docs/frd/frd-12-operator-experience.md`, `docs/frd/frd-10-mcp-automation-and-actor-boundary.md`, `docs/frd/frd-06-vehicle-and-engineering-evidence.md`, `docs/capabilities.md`, `docs/design/README.md`
- Change: FRD-12 § Assessment — remove `Import estimate (\`EXT-12\`)` as a control and state the whole-page drop: one file dropped anywhere on the page, imported immediately, no confirmation and no visible picker, registered parser types only, provider/parser auto-detected and fail-closed on ambiguity, Drafts named by provider plus sequence, filename/hash/provider/actor/channel/outcome recorded. FRD-12 § Acceptance evidence, and the keyboard-contract region — the pointer-only drop is a documented narrow accessibility exception, with every other import route keyboard-reachable. FRD-10 § AI job and estimate tools — add `pegasus_estimate_import` under `automation.assessment` with the parameter and return contract, and state that it reuses the same Core command as the Web caller. FRD-06 § Canonical repair specifications — same Case plus same source hash is an idempotent replay; a different artifact creates the next immutable Draft. `capabilities.md` `EXT-12` and `MCP-06` — record the shared command and the two callers as decided 2026-09-01 (`ENG-033`, `AUTO-016`), not delivered. `design/README.md` § Assessment (record bar and dialog list) — remove the Import estimate control and its dialog; § Keyboard and dialog contract — the narrow pointer-only exception; § Removed surfaces — the Import estimate dialog and picker.
- Preserved behaviour: the Automation Actor's authentication, operation-key and expected-version rules; the ADR-0031 kill switch; the tool-tranche evidence rule.
- Forbidden: inventing a parser or provider list; implying the tool is composed; adding a second import route.
- Negative cases: no document may still describe an Import estimate dialog or picker as present.
- Tests: none.
- Commands: see § Commands.
- Done when: the drop contract, the tool row and the replay rule agree, and the accessibility exception is documented rather than implied.
- Deviation stop: if the tool name collides with an existing tool, stop and report rather than choosing a third name.

### Step 9 — D25: Triage History and Files
- Preconditions: none.
- Files: `docs/frd/frd-03-triage.md`, `docs/frd/frd-12-operator-experience.md`, `docs/design/README.md`
- Change: FRD-03 § Normal workflow and completion evidence — History merges durable events and append-only attributable staff notes chronologically; corrections are new notes; no edit or delete; Files holds retained request sources/attachments and linked vehicle images with view/download; no arbitrary file store or upload action. FRD-12 Triage-detail paragraph — the same, in operator terms. `design/README.md` § Triage — the Notes/History panel semantics and the Files view.
- Preserved behaviour: the Triage states, the finding dimensions, the reply-chain completion evidence, the existing server-side transitions.
- Forbidden: adding an upload control to Triage; allowing a note edit or delete.
- Negative cases: no document may offer a Triage upload action.
- Tests: none.
- Commands: see § Commands.
- Done when: History and Files are specified once each, consistently.
- Deviation stop: the shipped "Permanent history" heading rename belongs to `UIIMP-012` — do not rename a UI label here; report if the documents require it.

### Step 10 — D26: direct Case creation with an instruction receipt
- Preconditions: none.
- Files: `docs/frd/frd-02-intake-and-source-identity.md`, `docs/frd/frd-12-operator-experience.md`, `docs/capabilities.md`
- Change: FRD-02 § Ways intake starts — staff enter the required identity and attach or record the instruction; Pegasus persists an attributable intake receipt, then reuses the normal principal and Case/PO allocation policy; no parallel allocation implementation. FRD-12 utility-bar **Add** action and the Create Case entries — the receipt is recorded before allocation. `capabilities.md` `INT-26` — record the receipt requirement as decided 2026-09-01 (`PLAT-059`), not delivered.
- Preserved behaviour: `INT-25` automatic creation; the Case/PO allocator as the only allocator; the Image-initiated projection rules.
- Forbidden: a second allocation path; implying the receipt is implemented.
- Negative cases: no document may describe direct creation that skips the receipt or the normal allocation policy.
- Tests: none.
- Commands: see § Commands.
- Done when: the receipt-then-normal-allocation sequence is stated once, in FRD-02, and referenced from FRD-12.
- Deviation stop: none expected.

### Step 11 — D19 report images, and D21/D27/D15 boundaries
- Preconditions: none.
- Files: `docs/frd/frd-06-vehicle-and-engineering-evidence.md`, `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, `docs/frd/frd-12-operator-experience.md`, `docs/capabilities.md`, `docs/boundaries.md`, `docs/design/README.md`, `docs/engineering.md`
- Change: **D19** — FRD-06 (report-image selection paragraphs) and FRD-11 § Photographs and source evidence: non-destructive preparation, distinct Close-up first and Overview second, optional ordered supporting images, normalized/versioned/attributable crop and order data under expected-version and edit-lease rules, and an issued report retaining its exact curation snapshot and source hashes; report-image curation leaves the UI-15 deferral. FRD-12 § Assessment evidence rail — the curation surface. `capabilities.md` `RPT-02` and `UI-15` — curation is decided 2026-09-01 and allocated to `ENG-031`; the rest of UI-15 stays `Later`. **D21** — `boundaries.md` gains rows for the absent-not-disabled exclusions (direct Glass's/Audatex launch controls, a standalone Images list, runtime-managed templates, autonomous outbound sending with staff-initiated delivery in scope under ADR-0036); `capabilities.md` `EXT-13` and `ENG-01` record the launch controls as absent and the manual valuation records as active; `design/README.md` § Absent versus disabled removes the `Glass's, Audatex` seam row and states that an excluded capability is absent, § Removed surfaces gains the launch controls, § Deferred integration and intake surfaces is aligned. **D27** — `capabilities.md` `OPS-20` records the 2,000-case tier-10 cohort/soak evidence as **not run by this programme and never represented as passing**, with the evidence spike (`PLAT-066`) outside EPIC-011; `engineering.md` tier 10 says the same; `boundaries.md` gains the matching row. **D15** — `design/README.md` § Evidence discipline names `Pegasus_UI_Frontend_Design_Premium_Full_End_State.html` as the canonical visual execution source and records that a visual conflict pauses only the affected lane and its dependants.
- Preserved behaviour: the Experian and Cazana disabled seams under the narrowed D7; per-ticket concurrency tests; the § Not planned permanent absences.
- Forbidden: presenting the capacity tier as passing or as merely pending; deleting a seam that D7 still permits; adding a new admin area or route.
- Negative cases: no document may draw a Glass's or Audatex launch control, and none may imply the 2,000-case tier passed.
- Tests: none.
- Commands: see § Commands.
- Done when: exclusions read as absent, the capacity tier reads as not run, and the canonical visual source is named.
- Deviation stop: if removing the seam row leaves a design section describing a control with no handler, stop and report.

### Step 12 — D28 administrator password reset, and the capabilities arithmetic
- Preconditions: every earlier step is complete, because this step recounts `capabilities.md`.
- Files: `docs/frd/frd-04-parties-accounts-and-access.md`, `docs/frd/frd-12-operator-experience.md`, `docs/capabilities.md`, `docs/design/README.md`
- Change: FRD-04 § Staff role access matrix (Administrator column) and § Staff accounts — Reset password: the Administrator enters and confirms a compliant temporary password, the existing policy and hashing apply, the existing forced-change state is set, the action is permanently recorded, and the secret is never emailed, logged, persisted raw or placed in analytics. FRD-12 § Administration — the Accounts area's Reset password action. `design/README.md` § Administration Accounts bullet — the same action. `capabilities.md` — add row **`ACC-10`** ("Administrator-initiated staff password reset using an administrator-entered temporary password and the existing forced-change state"), horizon `Now`, target `0.1.0-alpha.1`, canonical owner FRD-04 § Staff role access matrix, activation/boundary: decided 2026-09-01 (D28), allocated to `PLAT-064`, not yet delivered. Then **recount**: `## Allocation summary` `Now` 142 → 143, `Total: **233 capabilities; 233 unique IDs**` → 234/234 with the recount date `2026-09-01`, and the target-release table's `0.1.0-alpha.1` 142 → 143.
- Preserved behaviour: `ACC-03`'s existing accepted scope; the allocation rules; every other count.
- Forbidden: emailing, logging or persisting the temporary secret in any described flow; reusing an existing capability ID; leaving a count stale.
- Negative cases: the four arithmetic figures must be internally consistent — `Now` + `Next` + `Later` + `Not planned` = the stated total.
- Tests: none.
- Commands: see § Commands.
- Done when: the reset is specified in FRD-04, surfaced in FRD-12 and the design README, registered as `ACC-10`, and the summary arithmetic adds up.
- Deviation stop: if `ACC-10` is already used anywhere in the repository, stop and report rather than picking another number.

## Acceptance checks

- **Every row of the ticket body's decision table carries its decision.** For each of the twelve
  rows, every file and section it names has been edited (or is explicitly recorded as already
  correct — FRD-08's Delete bullet and FRD-11's VAT-on-subtotal row are the two known cases).
- **No governing document still states the opposite.** After the diff, a repository search over
  `docs/` (excluding `docs/json-extraction-parity/`, `docs/current-architecture.md`,
  `docs/operations.md`, `docs/adr/`) finds no surviving instance of: "not yet derivable",
  "original-versus-assessed", "savings" as a Pegasus feature, an exact-signature-tuple gate, a
  fixed "seven calendar days" that is not named as the default, a 10 MiB or 25 MB upload bound, an
  Import estimate dialog or picker, a Glass's or Audatex launch control, a provisional 15-minute
  threshold, or a completeness percentage.
- **`docs/open-decisions.md` rows the decisions settle are resolved.** The rate-card row and the
  mail-freshness section are resolved with their content present in a canonical owner; the
  assessment-markup, report-wording and slider rows are narrowed to what is genuinely still open;
  no unsettled row was deleted.
- **Truthfulness of `docs/capabilities.md`.** Every touched row still reports its real state: each
  new statement is dated 2026-09-01 and named as decided/allocated, and no row claims a caller,
  deployment, live verification or acceptance that does not exist. The allocation summary
  arithmetic is internally consistent.
- **No new Markdown file, no renamed heading** other than the one resolved-decision heading in
  Step 3, and no file outside Expected files is touched.
- **Link and placement checks pass** (§ Commands), including the CI `documentation` job's three
  steps.
- **Independent review** confirms no unauthorized scope: no OCR content, no ADR, no PRD line, no
  `docs/operator-notes.md` edit, no code.

## Commands

Docs-only: **no `dotnet build` is needed or run.** The **test-runner role runs these**; the
implementer records them as owed and does not run tests itself. Working directory for all of them:
`C:\Users\PGUSER\Documents\github\pegasus-worktrees\deliv-040-governing-docs`.

Repository rail — the CI `documentation` job (`.github/workflows/ci.yml` L71–90), the one lane
every change set runs, Windows:

```
pwsh ./scripts/Test-TestMarkdownPlacement.ps1
pwsh ./scripts/Test-DocumentationLinks.ps1
pwsh ./scripts/Test-UiCatalogue.ps1
```

Focused proof that no new Markdown file was added (the placement validator invoked directly; CI
exercises it only through its regression harness above):

```
pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD
```

Scope evidence, for the report and the PR body:

```
git -C C:/Users/PGUSER/Documents/github/pegasus-worktrees/deliv-040-governing-docs diff --stat origin/dev...HEAD
git -C C:/Users/PGUSER/Documents/github/pegasus-worktrees/deliv-040-governing-docs diff --name-only --diff-filter=A origin/dev...HEAD
```

The second must print nothing: no file is added.

Post-merge/environment checks: none. This change deploys nothing and alters no runtime behaviour.

## Failure and deviation rules

Stop and report — do not improvise — on any of these:

- a repository check above fails, or is inconclusive (inconclusive is not a pass);
- a required sentence cannot be written without contradicting another governing document, or two
  documents disagree in a way the authority order in `docs/index.md` L29–38 does not settle;
- a file outside **Expected files** would have to change, including an obvious neighbouring fix;
- an edit would need a new Markdown file, a new ADR, a PRD line, or an `docs/operator-notes.md`
  change;
- a capability row cannot be made truthful without claiming delivery;
- `ACC-10` or the chosen tool name is already in use;
- OCR, `prebuilt-layout`, the raw-response rule or the confidence threshold appears to be in
  scope;
- the `governing_docs` lock appears to be held elsewhere, or a merge with `origin/dev` conflicts in
  one of the locked files.

A deviation is reported in the post-implementation report with the observed text and the line, and
in the ticket's `open-questions` where a decision was taken. Deviations are never silent
redesigns. Refresh only with `git merge --no-edit origin/dev`; never rebase.

## Simplification pass

**2026-09-02 — n/a — docs-only.** No code changes on this branch, so the reuse, simplification,
efficiency and altitude lenses have no diff to run over (`AGENTS.md` step 4: "a docs-only task
records 'n/a — docs-only'"). The equivalent quality check for this ticket is the Acceptance-checks
sweep above: one statement per decision, in the document that owns it, with no restatement in a
second file where a citation does the job.

## Stop condition

Stop at **PR_OPEN**. The implementer's boundary is: the sixteen files edited on
`task/deliv-040-governing-docs` in
`C:\Users\PGUSER\Documents\github\pegasus-worktrees\deliv-040-governing-docs`, committed and
pushed with `git push -u origin task/deliv-040-governing-docs`, and one pull request opened
against `dev` titled exactly:

`Record the 2026-09-01 operator interface decisions in the governing documents (DELIV-040)`

with the footer line `Kanmer: DELIV-040` in the PR body, and the ticket moved
`implementing` → `review` (one gated boundary).

Do not merge the PR. Do not promote anything to `main`. Do not move the ticket past `review`. Do
not start, take or dispatch another ticket. Do not run tests — the test-runner role does that.
