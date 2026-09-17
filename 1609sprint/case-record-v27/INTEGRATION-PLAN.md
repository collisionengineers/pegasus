# Case record v27 — integration plan

Snapshot: 16 September 2026, `dev` at `45a011165`. Plan only. This is the
Stage 2 plan the `razor-html-mockup-creation` skill requires before
conversion starts; conversion itself follows `razor-html-mockup-conversion`.
Companion files: [FEATURES.md](FEATURES.md) (what),
[OPEN-ISSUES.md](OPEN-ISSUES.md) (what is undecided),
[BACKEND.md](BACKEND.md) and [FRONTEND.md](FRONTEND.md) (exactly which
code changes).

## 1. Aim

Carry the v27 round from a mockup with every idea drawn as a switch into
the product, in packages that each leave `dev` releasable, without
touching a settled rule until the operator has changed it, and with the
Case record's live behaviour (leases, roles, readiness, the one report
owner) intact underneath every addition.

## 2. Inputs and their state

| Input | State on 16 September |
| --- | --- |
| Reference file `pegasus_case_dashboard_2026-09-15.html` | Read in full; inventoried; differences classified Same / Different / Absent / Conflicts |
| v27 baseline mockup `pegasus_case_record_v27.html` | Built from `origin/dev` `cdbe014a6` (two commits behind `45a011165`; their `src/` changes are intake-side — third-party report profiles, retained intake files, Worker DI — and touch none of the record's inputs); self-check 680/0; 94 shots; coverage audit 494 refs, 18 accounted for |
| Proposals on the mockup | Damage selector A–D + areas; refined mark; 22 reference switches + `wording`; all reversible (`proposals=none` is the baseline) |
| Sign-off | None settled. Letters A–F, G–G7, H, I, I1–I3 open; J1–J27 added here |
| Governing rules | FRD-01, FRD-06 (D39/D40/D45), FRD-11 (template rule, delivery), FRD-12 (D29/D30/D31, Case workspace), ADR-0050, `docs/design/README.md`, CONTEXT.md |
| Live code | `src/Pegasus.Core/Assessment`, `Core/Reports`, `Infrastructure/Reports/AssessmentReportLayout.cs`, `Infrastructure/Persistence/AssessmentFieldWriter.cs`, `Web/Pages/Cases/**`, `Web/wwwroot/{js,css}/case-workspace.*` |

## 3. Gate 0 — decisions before any code

Stage 2 does not start until the letters are read out and settled
(sign-off protocol). The gate is split so packages can start
independently as their letters close:

| Letters | Unlocks |
| --- | --- |
| A–F | the whole of Stage 2 (baseline fidelity, fixture, scope, branches, build inputs, reference placement) |
| G, G1–G7, J1–J5 | WP1 Damage |
| H | WP2 Brand mark |
| I for `composed`; J6, J9 | WP3 Narrative owner |
| I for `cap`; J11 | WP4 CAP |
| I for `salvage`, `bank`, `ticks`, `include`, `queries`, `place`; J12, J19–J21; I1 for `badges`, `nine`; J24 | WP5 Settlement, Files, Notes and frame |
| I for `estdel`, `offpattern`, `uplift`, `prov`, `compare`, `supp`; J8, J10, J22, J23 | WP6 Estimate |
| I for `abook`, `attach`, `resend`, `feetab`; I3 for `signoff`, `reportdate`; J13–J18 | WP7 Report and delivery |
| I2; J7, J8 | WP8 Report wording |

Each settlement is recorded in `v27-notes.md` and the affected
`how-it-should-work.md` under a dated "Decided" heading before its package
branches. A rejected switch is removed from the mockup's default set
(`S.p`) in the same commit so the mockup keeps matching the decision.

## 4. Work packages

Order is by dependency and by cost of being wrong; each package is one PR
unless noted, and each leaves `dev` releasable.

### WP2 — Brand mark (first; smallest)

- Scope: FEATURES § 2. Asset swap, marks README.
- Depends on: H.
- Evidence: the routed shell at 1580 and with the rail collapsed against
  shots 72–73; no build evidence needed beyond the Web build the PR's CI
  runs (an embedded asset changed).
- Docs: `wwwroot/images/marks/README.md`; `docs/design/README.md` only if
  it names the lockup file.

### WP3 — One narrative owner (before WP1 and WP6)

- Scope: BACKEND § 3, FRONTEND § 3. `AssessmentNarrative` in Core, the
  renderer re-pointed at it, seven derived cells (or four, per J9),
  `narrative.nature_of_incident` retired.
- Depends on: I (`composed`), J6, J9.
- Why early: it removes the second composition of the damage narrative
  before WP1 rewrites that cell, and WP6's supplementary paragraph and
  WP8's blocks both compose through it.
- Evidence: Core tests (exact strings; rendering tests unchanged),
  integration tests for the cells, conformance shots 75–79.
- Docs: FRD-12 § Case workspace (one sentence per section that gains a
  derived cell); FRD-11 no change (the report prints the same words).

### WP1 — Damage marks

- Scope: BACKEND § 1, FRONTEND § 1. Core mark model, area geometry,
  parse/serialise both shapes, derivation, renderer diagram, record
  editor for the chosen variant.
- Depends on: G–G7, J1–J5; WP3 for the narrative cell.
- Two PRs: (1) Core + Infrastructure + projection + renderer, with legacy
  read, behind no flag (the page keeps posting zones until PR 2 — the
  parser accepts both); (2) the Web editor and the page's save. This
  keeps the renderer change reviewable on its own and lets PR 1 release
  without a visible change.
- Evidence: Core tests, rendering tests (SVG parsed), integration
  `CaseDamageAndViewerWebTests`, conformance shots 66–71 at three widths,
  and one real report generated on the test estate from a Case with
  marks and one with legacy zones.
- Docs: FRD-06 § Damage record (rewrite the region sentences: eight areas,
  the point, the mark unit), FRD-12 § Case workspace (the clicker
  sentence), FRD-11 § outcomes (the diagram sentence), CONTEXT.md (LH / RH
  per G5), `docs/current-architecture.md` only if a new Core file changes
  the structure list.

### WP4 — CAP guide source

- Scope: BACKEND § 4, FRONTEND § 4. Enum member, regenerated check
  constraint (additive migration), the card.
- Depends on: I (`cap`), J11.
- Evidence: Core tests, persistence test for the constraint, integration
  `CaseValuationV26WebTests`, conformance shot 78.
- Docs: FRD-06 § Valuation sources (D40 + CAP), FRD-12 § Case workspace,
  CONTEXT.md.

### WP5 — Settlement, Files, Notes and frame presentation

- Scope: FRONTEND § 5; BACKEND § 6 (reason bank), § 20–22. `salvage`,
  `bank` (additive migration + Administration page), `ticks`, `include`,
  `queries`, `place`; `badges` and `nine` only if I1 changes the rules.
- Depends on: the I letters listed in § 3; J12, J19–J21; J24 if I1.
- Split: one PR for `bank` (it has a migration and an admin page); one PR
  for the rest; `nine`/`badges` their own PR if accepted, since they
  touch the frame every other package renders inside — and in that case
  they go last in the sprint, not first.
- Evidence: Core policy tests for the bank, persistence test, integration
  tests per control, conformance shots 82, 85, 86, 92 (75, 77, 78 for
  `place`; 87–89 for `nine`).
- Docs: FRD-12 § Case workspace (placements; the Decisions strip; the
  Images tab; Notes); FRD-01 D31 placement sentence if the Sign-off
  select moves; if I1: FRD-12 D29/D30 and the Figures aside.

### WP6 — Estimate

- Scope: BACKEND § 7–12, FRONTEND § 6. `estdel`, `offpattern`, `prov`
  (no back end or Core-only), `uplift` (additive migration), `compare`
  (Core comparison), `supp` (fields + paragraph, gated on J8 for the
  printed block).
- Depends on: the I letters; J8, J10, J22, J23; WP3 for the paragraph;
  `../import-test-and-fix/` for real Audatex fixtures (the parser fixes
  change which off-pattern cells appear).
- Split: PR (a) `prov` + `offpattern` + `estdel` (no migration); PR (b)
  `uplift` (migration, rate-card admin); PR (c) `compare` + `supp` (the
  comparison owner, the dialog, the panel, the fields; the printed
  section only once J8 is accepted).
- Evidence: Core tests (`EstimateTests`, `EstimateComparisonTests`,
  `PostcodeRegionsTests`, `EstimateLineAmendmentTests`), integration
  `CaseEstimateHeaderWebTests` and the Details class, rendering test for
  the supplementary section, conformance shots 79–81b, 93–94.
- Docs: FRD-11 § Estimate (uplift in the rate sentence; comparison;
  supplementary explanation on the report), FRD-12 § Case workspace.

### WP7 — Report and delivery

- Scope: BACKEND § 13–18, FRONTEND § 7.1–7.5. `abook`, `attach`, `resend`,
  `feetab`, `signoff`, `reportdate`.
- Depends on: the I / I3 letters; J13–J18; `attach` Breakdown on
  `../estimate-generator/PLAN.md` Phase 2 and Images on an accepted
  contact-sheet design; `resend` on accepted correspondence templates;
  `feetab` on J16.
- Split: PR (a) `abook` (suggestions + fingerprint + UI); PR (b) `attach`
  + `resend` (artifact kinds, file name, covering message — only with the
  accepted template); PR (c) `feetab` + `signoff` + `reportdate` (small,
  each one Core rule; `reportdate` only if J18 keeps it, and then inside
  the generation transaction).
- Evidence: `CaseReportDeliveryPreparationTests`,
  `CaseReportGenerationTests`, lifecycle tests, integration
  `CaseReportApprovalWebTests`, conformance shots 83–84, and a real send
  from the test mailbox to a disposable recipient (the only permitted
  external write; per the repository's alpha rule).
- Docs: FRD-11 § delivery (recipients, attachments, file name, covering
  message), FRD-01 § Sign-off Engineer (the hand-off rule), FRD-12.

### WP8 — Report wording (last; only after I2)

- Scope: BACKEND § 19, FRONTEND § 7.6. Blocks in Core, the table, the
  renderer taking blocks, the panel.
- Depends on: I2 (FRD-11 and ADR-0050 amended first, in their own docs
  PR); J7 (accepted salvage paragraphs for A/B/N, or S-only kept); J8;
  WP3; WP6 (c) if the supplementary block is part of it.
- Split: PR (a) docs (FRD-11 § Assessment-report outcomes and template
  rule, ADR-0050 supersession note, FRD-12); PR (b) Core policy + table +
  projection + renderer with a rendering golden; PR (c) the panel.
- Evidence: policy tests, projection tests, rendering golden (block
  order and titles in the PDF text), integration tests for every panel
  action, conformance shots 90–91, and one real generation on the test
  estate with an edited, reordered, renamed set of blocks.

## 5. Branch and PR shape

- Branches `task/v27-<package>` from `origin/dev`, each in its own
  worktree under `../pegasus-worktrees/<slug>`, upstream unset after
  branching (the repository's branch convention). One PR per package or
  split as above; stacked PRs only where § 4 names a dependency
  (WP1 PR 2 on PR 1; WP8 (b) on (a)).
- Each PR carries its FRD/ADR/CONTEXT edits — a rule and its code land
  together — and passes `scripts/Test-MarkdownPlacement.ps1 -Base
  origin/dev -Head HEAD` and `scripts/Test-DocumentationLinks.ps1`.
- Host test work is serialised behind one host-slot owner per the
  repository instructions; integration suites run one at a time (they
  share LocalDB), chunked by class with the full log kept. CI on the PR
  is the delivery evidence; exact-head CI evidence is reused rather than
  re-run locally.
- Merge order follows § 4: WP2, WP3, WP1, WP4, WP5, WP6, WP7, WP8. WP4 and
  WP5 may merge in either order; WP6 (a) may precede WP1.
- Nothing is committed on `dev` directly; the sprint folder and the
  `v27_planning` folder are the operator's to commit.

## 6. Verification matrix

| Package | Core tests | Integration (SqlServer) | Rendering | Docs gates | Conformance shots | Live check |
| --- | --- | --- | --- | --- | --- | --- |
| WP2 | — | — | — | placement, links | 72–73 | shell at 1580, rail collapsed |
| WP3 | narrative strings | Details cells | unchanged text | placement, links | 75–79 | — |
| WP1 | policy, geometry, projection | DamageAndViewer | SVG marks | placement, links | 66–71 | one report with marks, one with legacy zones |
| WP4 | valuation | ValuationV26 | — | placement, links | 78 | — |
| WP5 | reason bank | Details, admin | — | placement, links | 82, 85, 86, 92 (+75/77/78; 87–89) | — |
| WP6 | estimate, comparison, postcodes | EstimateHeader, Details | supplementary section | placement, links | 79–81b, 93–94 | one import → compare → supp |
| WP7 | delivery, generation, lifecycle | ReportApproval | — | placement, links | 83–84 | one prepared send from the test mailbox |
| WP8 | wording policy, projection | Details (panel) | golden | placement, links | 90–91 | one generation with edited blocks |

Every PR: `dotnet build` and the affected test projects on the host slot,
architecture tests when a Core port or Infrastructure adapter is added
(WP1, WP5 bank, WP6 uplift/compare, WP7, WP8), and the coverage audit
re-run against the routed page's HTML for label completeness.

## 7. Documentation impact

| Document | Packages | Change |
| --- | --- | --- |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | WP1, WP4 | § Damage record rewritten for areas and marks (D39/D45 superseded by a dated D-number); § Valuation sources gains CAP (D40) |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | WP1, WP6, WP7, WP8 | diagram sentence; supplementary explanation; delivery recipients / attachments / file name / covering message; the template rule and outcomes section if I2 |
| `docs/frd/frd-12-operator-experience.md` | WP1, WP3, WP4, WP5, WP6, WP7 | § Case workspace bullets per section; D29/D30 and the Figures aside only if I1 |
| `docs/frd/frd-01-case-identity-and-lifecycle.md` | WP5, WP7 | Sign-off Engineer placement and the hand-off rule |
| `docs/adr/0050-questpdf-report-renderer.md` | WP8 | superseded-in-part note: blocks instead of fixed paragraphs |
| `CONTEXT.md` | WP1, WP4 | LH / RH (G5); CAP beside Super CAP |
| `docs/design/README.md` | WP2 | only if it names the lockup asset |
| `src/Pegasus.Web/wwwroot/images/marks/README.md` | WP2 | the mark's row |
| `docs/current-architecture.md` | WP1, WP6, WP8 | only if new Core files change the structure list |
| `docs/operations.md` | each release | the dated deployment record |
| `design/planning-and-old-designs/v27_planning/**` | every package | "Decided" headings; the retained folder's README says provenance, not requirements, at close-out |

## 8. Release

- Every migration in this round is additive (reason-phrase table,
  rate-card percent, specification uplift bit, valuation source
  constraint, wording table, principal fee column) — the normal release
  route, no destructive window, no Worker stop. The Worker is not
  otherwise affected.
- Release per package or per two packages, in § 4 order, each with its
  `docs/operations.md` entry and the release skill's checks; PR 764's
  pending promotion and any import-fix release go first if they are
  ready, so the estimate fixtures WP6 relies on are live.
- Smoke after each release on the test estate: open a Case with legacy
  damage (WP1), generate a report (WP1, WP6, WP8), prepare a delivery
  (WP7).

## 9. Risks

| Risk | Mitigation |
| --- | --- |
| A package touches a rule the letter did not settle (e.g. `signoff` overwriting an explicit choice) | J-items are answered before the package branches; the rule sentence is in the PR |
| Damage geometry differs between script and Core | one partition, ported once, asserted equal by a sampled test (FRONTEND § 1) |
| Stroke geometry breaks the 4,000-character field | bounded point count; test the worst case at 24 marks |
| Legacy zone Cases render wrongly | read-both parser with a fixture of every legacy zone code; a live check on the test estate |
| New copy slips in unapproved (storage sentence, VAT sentence, "what the report carries", queries empty state, covering message, salvage A/B/N) | J7–J9, J15, J20 list each sentence; none is built before acceptance; the coverage audit shows every string |
| `reportdate` stales its own generation | dropped, or folded into the generation transaction (J18) |
| `nine` / `badges` destabilise every other package's frame | last in the sprint if accepted; recommended rejected |
| Integration suite runtime grows past the CI budget | tests go into the six-shard classes (J26); no duplicated end-to-end walks |
| `attach` Breakdown waits on another sprint item | WP7 (b) is split so `abook` and the rest ship without it |

## 10. Close-out

- After the last package: the operator says whether
  `design/planning-and-old-designs/v27_planning/` is retained (README
  reworded to "provenance, not requirements", as v26's) or removed, in
  the final Stage 2 PR.
- This sprint folder is updated with a PROGRESS.md as packages land
  (branch, PR, CI run, release), in the shape of
  `../ci-six-shards/PROGRESS.md`.
- The mockup's default proposal set is reduced to what shipped, so the
  file still opens as the record that exists.

## 11. Sequence at a glance

| # | Package | Gate | Migration | PRs |
| --- | --- | --- | --- | --- |
| 1 | WP2 Brand mark | H | — | 1 |
| 2 | WP3 Narrative owner | I `composed`, J6, J9 | — | 1 |
| 3 | WP1 Damage marks | G–G7, J1–J5 | — (J2 may add one) | 2 |
| 4 | WP4 CAP | I `cap`, J11 | constraint | 1 |
| 5 | WP5 Settlement / Files / Notes / frame | I …, I1, J12, J19–J21, J24 | reason phrases | 2 (+1 if I1) |
| 6 | WP6 Estimate | I …, J8, J10, J22, J23 | rate card, specification | 3 |
| 7 | WP7 Report and delivery | I …, I3, J13–J18 | principal fee (J16) | 3 |
| 8 | WP8 Report wording | I2, J7, J8 | wording table | 3 |
