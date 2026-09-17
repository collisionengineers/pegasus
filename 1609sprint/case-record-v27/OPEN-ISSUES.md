# Case record v27 — open and unresolved items

Snapshot: 16 September 2026. Nothing below is decided. § 1 is the sign-off
list exactly as the round raised it (`v27-notes.md` § 7, 11–15); § 2 is what
this re-examination of the reference file, the mockup and the live source
added; § 3 is the decision-first list; § 4 cross-sprint dependencies; § 5
observations on the live code met on the way. Each item is phrased
"Confirm, or …" where a default exists; a recommended default is marked.

## 1. Sign-off letters as raised

### Baseline (notes § 7)

- **A. Fidelity.** Confirm the v27 baseline stands in for the live Case
  record, or name any surface that reads wrong and it is corrected first.
- **B. Fixture.** Confirm MA59BDY / QDOS26214 (total loss, Cat N) carries
  forward, or name the Case shape (repairable, Review, image-initiated,
  Audit). Note J7: the live renderer refuses a total loss that is not
  Category S, so this fixture could not generate a report today.
- **C. Shell scope.** Confirm v27 is a Case record round, or name other
  routes needing a baseline.
- **D. Placement.** Confirm `task/v27-planning` from `origin/dev`, or that
  the round stays on `dev`. (The folder was committed on `dev` at
  `9c4c09eb7` by the operator; the question is now about Stage 2 branches —
  see INTEGRATION-PLAN § 4.)
- **E. Build inputs.** Confirm `v27-build/` stays so the baseline can be
  regenerated when `origin/dev` moves.
- **F. Reference file.** Confirm the reference stays at the folder root.

### Damage selector (notes § 11–12)

- **G.** Confirm one of A Pins / B Brush / C Areas / D Impact arrows (or a
  combination, e.g. D for the impact plus A for secondary damage), or keep
  the live panel clicker. *Recommended: D for the primary impact, A for
  secondary marks — it records what the narrative sentence already says.*
- **G1.** Confirm the mark's point is stored with the derived areas (so the
  report diagram draws the mark), or that only the areas are stored and the
  mark is presentation. *Recommended: store the point.*
- **G2.** Confirm a mark spanning several areas is one damage with several
  areas, or one impact per area. *Recommended: one damage, several areas.*
- **G3.** For D, confirm the arrow length sets the severity and the
  direction is a recorded fact, or the arrow is presentation only.
- **G4.** The roof: nearer side, or its own area / "Roof". *Recommended:
  nearer side, as drawn.*
- **G5.** LH / RH as the operator words for the eight areas (a vocabulary
  change reaching the report narrative and CONTEXT.md), or keep
  Left / Right. See J25.
- **G6.** Underside, Interior and Mechanical stay as chips beside the eight
  areas, or go. *Recommended: stay.*
- **G7.** "Recorded areas" as the list heading, or keep "Recorded zones".

### Brand (notes § 13)

- **H.** Confirm the refined mark replaces `pegasus-lockup.png` (file and
  `wwwroot/images/marks/README.md`), or keep the lockup.

### Reference proposals (notes § 15)

- **I.** For each of the twenty-two switches — `composed`, `cap`, `salvage`,
  `bank`, `estdel`, `offpattern`, `uplift`, `prov`, `compare`, `supp`,
  `abook`, `attach`, `resend`, `feetab`, `badges`, `nine`, `place`,
  `include`, `queries`, `ticks`, `signoff`, `reportdate` — confirm it
  carries into Stage 2 as drawn, name the change, or reject it.
- **I1.** `nine` and `badges` change settled frame rules (D29/D30 ten
  sections; FRD-12's Figures aside): confirm the rule changes, or keep them
  as comparison-only strip variables. *Recommended: reject both as drawn —
  the costs recorded in notes § 15 (ribbon truncation; nested panels losing
  their head tools) are real at 1580 px.*
- **I2.** `wording` crosses FRD-11's template rule and ADR-0050's fixed
  paragraphs. Confirm the rule changes so the report's narrative blocks are
  composed from the record and may be edited, reordered, renamed and added
  to before generation (FRD-11 + ADR-0050 change; the QuestPDF template
  takes blocks), or keep it as a strip variable. J7 and J8 are inside this
  decision.
- **I3.** `ticks`, `signoff`, `reportdate` (added last; the differences
  file had read them as "Same"): confirm or reject each with I. J17–J18
  qualify `signoff` and `reportdate`.

## 2. Issues found in this re-examination (J)

### Damage record

- **J1. Legacy impacts.** Every Case recorded so far holds zone impacts
  (`{zone, severity, note}` over the 23 panels, 8 broad zones and 3 chips;
  `AssessmentPolicy.ReadImpacts`). Confirm the reading rule: a legacy zone
  impact stays readable and is shown as a mark at that panel's marker centre
  (`DamageDiagramGeometry.Markers`) with its area from the parent map
  (`DamageZones[zone].ImpactLocation`), and the next Save re-serialises it
  as a mark. *Recommended: read both shapes, no data migration.* No
  automation path writes `damage.impacts` (grep: only Core, the writer, the
  projection and `_CaseDamage.cshtml` read it), so the record page is the
  only writer to change.
- **J2. Field size.** `damage.impacts` is capped at 4,000 characters
  (`AssessmentVocabulary.Definitions`, `SerializeImpacts`). Points and area
  lists fit for pins and arrows; a brush stroke's path or a disc's geometry
  may not. Confirm the cap is raised for the JSON field (check the `Value`
  column type on `CaseAssessmentFields` first) or that stroke geometry is
  simplified to a bounded point count (≤ 24). *Recommended: bound the
  geometry and keep the cap.*
- **J3. Uniqueness.** `ReadImpacts` rejects a repeated zone. With marks, two
  marks in the same area are two damages. Confirm the uniqueness rule goes
  (uniqueness becomes the mark id).
- **J4. Direction vocabulary (G3).** If direction is recorded: degrees from
  the vehicle's forward axis (integer 0–359), or eight compass words.
  *Recommended: degrees; the narrative can say "from the rear offside" from
  the area anyway.*
- **J5. Report diagram (G1).** QuestPDF draws the diagram from an SVG
  string (`AssessmentReportLayout.DiagramSvg`). Marks need: pins (circle +
  number), strokes (polyline), discs (circle r), arrows (line + head). The
  accepted report design (`rendererref1`) shows a shaded-panel diagram;
  marks are a visible change to a supplied template asset, so FRD-11's
  "supplied … design … is evidence" needs the operator to accept the marked
  look. Confirm.

### Report wording and copy

- **J6. `narrative.nature_of_incident` has no writer.** The vocabulary
  defines it (Text, 2000), `_CaseDamage.cshtml:290` shows it as a derived
  cell that script composes client-side, the renderer composes its own
  sentence from severity and location, and nothing in `src/` writes the
  field. With `composed`, one Core owner must compose it (BACKEND § 3) and
  the field either becomes that owner's output or is retired. Confirm
  retire.
- **J7. Category S only.** `AssessmentReportSnapshot.Validate` refuses a
  total loss unless `SalvageCategory == "S"`, and the Salvage paragraph is
  fixed Category S text ("… Category S (structural damage) and can be sold
  as repairable salvage …"). Accepted wording exists for S alone. The
  wording well's per-category Salvage block, the `ticks` row offering
  A / B / S / N, and the Cat N fixture all presume A, B and N paragraphs.
  Confirm the operator supplies and accepts the A, B and N paragraphs
  (evidence under `reference/`), or that total loss stays S-only and the
  other categories remain unavailable on the report.
- **J8. Supplementary paragraph.** "Following receipt of a supplementary
  estimate …" is new report wording under FRD-11's template rule. Confirm
  the operator accepts the paragraph (and its three variants: added /
  revised / no longer required, and "increased" / "reduced"), or `supp`
  stays a record-only diff with no printed block.
- **J9. Record-only composed sentences.** Four of the seven `composed`
  cells are sentences the report prints today (matter, assessment method,
  mileage, condition). Three are not: Recovery and storage charges (the
  report prints table rows), Drives the calculation, What the report
  carries. Under the necessary-copy rule these need approved wording.
  Confirm the three sentences as drawn on shots 76, 78 and 79, or show
  those three as figures only.

### Estimate

- **J10. Regional uplift policy.** `rates.regional_uplift` exists as a flag
  and nothing applies it. Confirm: +15 % fixed; applies to the selected
  rate card's hourly rate and to "keep entered rate"; the postcode set is
  London postcode areas plus the Home Counties as the reference's list
  (with its district lists for SG / OX / RG / CM), held as Core reference
  data not admin data; the suggestion reads the repairer, claimant and
  storage postcodes in that order. *Recommended: a rate-card-level
  "regional uplift %" administered with the card, so the percentage is
  data; the postcode set as Core data.*
- **J11. CAP.** Confirm the label "CAP" beside "Super CAP" (both CAP HPI
  products; CONTEXT.md must say which is which), that CAP is a fourth
  `ValuationSource` rather than a rename, and that no provider is connected
  until its own decision (FRD-06 D40 cites ADR-0031).
- **J22. Undo remove.** Remove line is a form post that rewrites the Draft.
  Undo needs a Core restore that keeps the line's `Origin` and
  `SourceDocumentIdentity` (a re-added manual line would lose them).
  Confirm a restore operation within the Draft (one remove event, one
  restore event), or a client-side six-second deferral of the post.
  *Recommended: Core restore.*
- **J23. Compare print.** The printable sheet is the browser's print of
  the dialog; no artifact is retained. Confirm no PDF is wanted.

### Settlement

- **J12. Reason bank governance.** Confirm: any Engineer or Administrator
  may save a phrase; removal and ordering on an Administration page
  (`Administration/…` beside Valuation presets); saving records an
  operator event; the seven starter phrases are seeded by the operator, not
  invented.
- **J21. Ticks beside proposals.** When an AI proposal exists the Decisions
  strip shows a Proposed column with Accept / Accept all. Confirm the tick
  rows sit under the Recorded column and Accept still writes the select, or
  that ticks are off while a proposal is pending.

### Report and delivery

- **J13. Address book sources.** Confirm the To dropdown lists: the
  Principal's configured addresses and the original instruction sender
  (today's `CaseReportDeliveryPolicy.Address`), this Case's recorded
  contacts with an e-mail (claim source, repairer, storage, claimant), and
  CE staff accounts; the Cc suggestion buttons are the Principal handler
  and the claim source; the claim source is never added without a click
  (the policy's "never copied implicitly" stays true). The suggestion
  fingerprint must cover the new lists so a prepared delivery goes stale
  when they change.
- **J14. Attachments.** Breakdown is the estimate document
  (`../estimate-generator/PLAN.md`), whose Phase 2 (retained artifact
  through custody) is gated on this very letter. Images is a new contact
  sheet artifact needing an accepted design. Confirm both artifact kinds,
  or Report + Fee note only for now. *Recommended: Report + Fee note +
  Breakdown; Images deferred until a design is supplied.*
- **J15. Re-send naming and message.** Today the attachment is
  `{REF}_assessment.pdf` (`CaseReportGeneration.FileNameOf`), the subject
  is the Case reference and the body is empty (`Body: string.Empty`). The
  reference's "one more dot per re-send" is a version tell that Box custody
  would also carry. Confirm: the file name becomes
  `{REF} {VRM} {Title}.pdf`; a re-send is marked by a version suffix rather
  than dots; the covering message and its "supersedes" variant are
  operator-supplied correspondence templates (FRD-11) accepted before
  build.
- **J16. Fee tab source.** No Principal fee table exists; the agreed fee is
  typed per Case (D42, `fee.agreed_fee`). Confirm a Principal fee table
  (Administration data that seeds the agreed fee, with a source line on the
  tab), or that the Fee tab only re-homes the existing fee fields and
  preview.
- **J17. Sign-off follows Engineer.** If an operator has chosen a different
  Sign-off Engineer on purpose, must a later hand-off overwrite it? Confirm
  the rule: follow the hand-off only while the Sign-off equals what D31's
  default would have been before the hand-off; never overwrite an explicit
  choice.
- **J18. Report date on generate.** The report prints the generation date
  unless the override is on; the generation facts line already shows it.
  Stamping `report.report_date` adds a record-visible copy that a later
  regeneration would not move — and an assessment write after the freeze
  marks the generation Stale (`CaseReportStaleReasons.AssessmentFactsChanged`),
  so as drawn the stamp would stale the report it was stamped for unless
  the write is folded into the generation transaction (BACKEND § 18).
  Confirm stamp-on-first-generate inside the generation, stamp-on-every-
  generate, or drop the switch. *Recommended: drop; the fact is already on
  the record.*
- **J19. Click to include.** Close-up and Overview are single-role. Confirm
  a click on those tiles opens the viewer as today and only Supporting ↔
  Not used toggles.
- **J20. Queries panel.** The reference's panel is an empty state. Confirm
  what rows it lists — the Case's query correspondence after Report sent —
  or that the panel is only the empty-state sentence (which is new copy).
- **J24. Nine sections and badges (I1).** If `nine` is accepted: the
  `?section=` keys, `EngineerSectionKeys`, `SectionAvailability`, the
  `/Cases/{id}/Assessment` redirect and the per-section lease sentences all
  change, and Damage and Valuation lose their own head tools. Listed so the
  cost is visible before the letter.
- **J25. LH / RH (G5).** The report says "Left front" and CONTEXT.md keeps
  N/S / O/S for panels. Confirm the report narrative also says LH / RH, or
  the record says LH / RH and the report keeps Left / Right.

### Process

- **J26. Test placement.** New integration tests must land in the
  per-feature classes of `../ci-six-shards/PLAN.md` (DamageAndViewer,
  Valuation, EstimateHeader, ReportApproval …), not in the monolithic
  `CaseDetailsWebTests` that plan splits.
- **J27. Mockup-only fixtures.** The strip's Repairer switch (Bootle /
  Croydon) and the synthetic address book are mockup furniture; Stage 2
  uses real Case contacts and postcode data.

## 3. Decision-first list (built only on a rule change)

Take over / Ask to release (FRD-01); inline padlocks (FRD-01); product type
select (FRD-01, ADR-0051); two-place Engineer's value (FRD-06); average
mileage default (FRD-06, ADR-0012); import overwrite with confirm (D16);
target-% rescaling and contract-sum rescale (FRD-11); WhatsApp channel
(FRD-05/08); implicit reopen after send (FRD-01); assigned engineer select
(FRD-01/12). Each needs the operator to say the rule changes before it is
drawn, let alone built.

## 4. Cross-sprint dependencies

| Sprint item | Relation |
| --- | --- |
| `../estimate-generator/PLAN.md` | `attach` Breakdown = its Phase 2 retained artifact; its decision A (operator label) and this round's `attach` letter gate each other |
| `../import-test-and-fix/` | `prov` and `offpattern` fixtures come from real Audatex imports; parser fixes change what off-pattern cells appear |
| `../ci-six-shards/PLAN.md` | test class placement (J26); CI runtime budget for the new SqlServer tests |
| `../performance-pr-764/PLAN.md` | release ordering; PR 764 needs fresh promotion approval |
| `../vehicle-type-autofill-plan.md` | touches the Vehicle section the `composed` and `place` switches also touch; merge order |

## 5. Observations on the live code (not v27 asks)

- `rates.regional_uplift` is a defined flag nothing reads or writes.
- `narrative.nature_of_incident` is a defined field nothing writes (J6).
- `EstimateTotals.OffPattern` is computed and never surfaced on the page.
- The Salvage paragraph and `Validate` agree on Category S only; the
  Settlement select still offers A, B and N, so a Cat A/B/N total loss
  cannot generate a report and the readiness list does not say why.
- The record's damage narrative is composed twice (client script and the
  renderer); any wording drift between them is invisible today.
