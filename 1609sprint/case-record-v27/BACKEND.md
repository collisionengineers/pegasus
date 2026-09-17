# Case record v27 — back-end changes

Snapshot: 16 September 2026, `dev` at `45a011165`. One section per feature,
in the work-package order of [INTEGRATION-PLAN.md](INTEGRATION-PLAN.md).
Each names the owner that changes (Core policy and ports first,
Infrastructure adapters second), the migration if any, and the tests that
prove it. "No back end" means the feature is presentation only and lives in
[FRONTEND.md](FRONTEND.md). Nothing is written until the feature's letter
is settled ([OPEN-ISSUES.md](OPEN-ISSUES.md)).

Conventions the changes keep: Core owns policy and ports; Infrastructure
implements them; Web composes. Assessment facts are `CaseAssessmentFields`
rows keyed by `AssessmentVocabulary` path (the path check constraint is
now length-only, so a new path needs no migration). Every Case mutation
goes through the lease and version guard (`CaseMutationGuard`), and an
assessment write marks the current report generation Stale
(`CaseReportStaleReasons`).

## 1. Damage marks (WP1; letters G–G7, J1–J5)

### Core — `src/Pegasus.Core/Assessment`

`AssessmentContracts.cs`

- Add `DamageMark(string Id, DamageMarkKind Kind, DamagePoint Point,
  IReadOnlyList<DamagePoint>? Path, decimal? Radius, int? Direction,
  IReadOnlyList<string> Areas, string Severity, string Note)`.
  `Kind` is `Pin | Stroke | Disc | Arrow` (only the chosen variant's kinds
  are accepted after G; the record keeps the enum so a later variant is
  additive). `Point` is in `DamageDiagramGeometry.ViewBox` units, rounded
  to one decimal. `Path` only for Stroke (bounded, J2), `Radius` only for
  Disc, `Direction` (0–359) only for Arrow and only if G3 records it.
- Keep `AssessmentImpact(Zone, Severity, Note)` as the legacy shape for
  reading (J1); it is no longer produced by the record.
- `AssessmentVocabulary`: add `DamageAreas` — the eight broad codes with
  their operator display words (G5: "LH Front" … or "Left front" …) — as
  a view over `BroadDamageZones`; the `ImpactLocation` code list is already
  the broad set plus `multiple`, unchanged. Raise
  `Definitions[DamageImpacts].MaximumLength` only if J2 says so.

`DamageAreaGeometry.cs` (new, beside `DamageDiagramGeometry.cs`)

- The partition of the silhouette into the eight areas in ViewBox units
  (`20 0 200 390`, shared with the live diagram), as the mockup's `areaAt`
  has it (`case-record.js:482`): front band `y < 150`, rear band
  `y > 300`, side band between; left `x < 100`, right `x > 140`, centre
  between. Centre-front → `front`, centre-rear → `rear`, corners →
  `left_front` / `right_front` / `left_rear` / `right_rear`, side band →
  `left_side` / `right_side`; the side band's centre (roof) → `left_side`
  when `x < 120`, else `right_side` (G4). The bands coincide with the
  diagram's structural lines at y = 150 and y = 250 only at the front;
  confirm the rear cut (300, the quarter-panel line, versus 250) before
  fixing it.
- `AreaAt(DamagePoint)`; `AreasOf(DamageMark)` (Pin/Arrow: the point;
  Stroke: every path point; Disc: centre plus eight ring points);
  `IsOnVehicle(DamagePoint)` — point-in-polygon against a polygonal
  approximation of `BodyPath` plus the four wheel discs (mirrors count
  as vehicle); `SeverityForLength(decimal)` — five bands for Arrow (D).

`AssessmentPolicy.cs`

- `ParseMarks(string?)`: reads the new array shape; when an element has a
  `zone` member instead of `point`, converts the legacy impact to a Pin at
  `DamageDiagramGeometry.Markers[zone]` (chips Underside / Interior /
  Mechanical stay chips, not marks) with `Areas = [DamageZones[zone]
  .ImpactLocation]` (J1). `SerializeMarks` writes only the new shape.
- Validation: kind accepted for the chosen variant; point on the vehicle;
  areas non-empty, accepted, equal to `AreasOf(mark)` (the server
  recomputes; a client value that differs is rejected); severity accepted;
  note ≤ 200 without control characters; ids unique; at most 24 marks;
  serialised length within the cap. The zone-uniqueness rule is removed
  (J3).
- `DeriveImpactValues`: location = the one distinct area, else `multiple`;
  severity = highest rank over marks. Unchanged signature so
  `AssessmentFieldWriter.cs:96–109` needs no edit.
- `ParseImpacts` is retired once every caller reads marks; the
  `RecordEditScope` / `CaseWorkspace` copy path (`CaseWorkspace.cs:593`)
  serialises marks.

`CaseWorkspace.cs`: `CaseWorkspaceDamage.Impacts` → `Marks`
(`IReadOnlyList<DamageMark>?`).

### Core — `src/Pegasus.Core/Reports`

`AssessmentReportProjection.cs` `BuildDamage`: `ReportImpact` becomes
`ReportMark(int Number, string Kind, DamagePoint Point, Path, Radius,
Direction, IReadOnlyList<string> AreaNames, string Severity, string
Note)`; `AssessmentReportPresentation.DamageZone` → `DamageArea(code)`
for the eight. The snapshot hash changes shape; earlier generations stay
valid (a hash is only compared within one Case's current facts).

### Infrastructure — `src/Pegasus.Infrastructure/Reports/AssessmentReportLayout.cs`

- `DiagramSvg`: draw marks per G1 — pins as a numbered circle at the
  point; strokes as a polyline with round caps; discs as a translucent
  circle; arrows as a line with a head; severity colour from the live
  legend. When G1 says "areas only", shade the area polygons from
  `DamageAreaGeometry` instead. Keep the body, glass and structural lines.
- `ImpactTable`: columns Number / Area(s) / Severity / Note.
- No change to "Nature of Incident" or "Impact Magnitude" beyond the area
  display word (G5).

### Persistence

None. `damage.impacts` stays the JSON field. If J2 raises the cap beyond
the column, one additive migration altering `CaseAssessmentFields.Value`.

### Tests

- `tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs`: parse
  new shape; parse legacy shape to pins with the right areas; reject a
  point off the vehicle, a mismatched area list, 25 marks, a repeated id;
  derive location/severity for one area, several areas, two marks in one
  area.
- New `DamageAreaGeometryTests.cs`: a sample point per area, the roof
  rule, each wheel, a point outside; disc ring and stroke area unions;
  arrow length bands.
- `Reports/AssessmentReportProjectionTests.cs`: marks projected with
  area names. `AssessmentReportRenderingTests.cs`: the rendered PDF's SVG
  contains a mark per recorded mark (parse the SVG string; do not compare
  bytes).
- Integration `CaseDamageAndViewerWebTests` (the split class per J26):
  save marks through the page, read a Case seeded with legacy impacts and
  see pins, derived cells on the page match Core.

## 2. Brand mark (WP2; letter H)

No back end. Asset swap only (FRONTEND § 2).

## 3. One narrative owner for composed sentences (WP3; `composed`, J6, J9)

### Core — new `src/Pegasus.Core/Reports/AssessmentNarrative.cs`

Static, pure, exact strings; the renderer and the record both call it so
the sentence on the page is the sentence on the report:

| Method | Sentence (today's renderer text where it exists) |
| --- | --- |
| `Matter(claimant, incidentDate)` | "Road Traffic Accident: {claimant}: {date}" (`AssessmentReportLayout.cs:160`) |
| `VehicleLocatedAt(method, address)` | "Vehicle located at: {Image Based Assessment \| address}." (from `Introduction`) |
| `Mileage(source)` | the six `MileageSentence` strings, moved here |
| `Condition(condition)` | "The vehicle is considered to be in {condition} condition for its age and type." |
| `NatureOfIncident(severity, location)` | "The vehicle has suffered {severity} collision/impact damage to the {location}." |
| `StorageCharges(recovery, perDay)` | three variants + "No charges entered" — **new copy, J9** |
| `VatDefault(repairerVatStatus, overridden)` | two sentences + "Overridden on this estimate" — **new copy, J9** |
| `ReportCarries(source, retail, trade, engineerValue, disclosed)` | "{guide} — Retail £x · Trade £y · Engineer's value £z." / "Source not disclosed — …" — **new copy, J9** |

`AssessmentReportLayout.cs` replaces its inline strings with these calls
(`Introduction`, `MileageSentence`, lines 197–199, 225–227, 159–160,
357–359). Rendering tests keep their expected text, which proves the move
is behaviour-preserving.

`narrative.nature_of_incident`: retire the field definition (J6) — remove
from `AssessmentVocabulary`; no data exists to migrate (nothing writes it).

### Tests

New `Reports/AssessmentNarrativeTests.cs`: one exact-string case per
method and per variant; a rendering test asserting the record-facing and
report-facing sentence are the same call.

## 4. CAP guide source (WP4; `cap`, J11)

- `Valuations.cs`: `ValuationSource.Cap`. Persistence stores the member
  name and the check constraint `CK_CaseValuations_Source` is generated
  from the enum — one additive migration regenerating that constraint.
- `GuideValuationProviders.cs`: no change; `FetchGuideValuation` already
  throws `GuideValuationProviderUnavailableException` for a source with no
  provider, which the card shows as the notice.
- `ValuationCalculations.cs`: the basis selection accepts `Cap` wherever
  `Glasses | Brego | SuperCap` are accepted (search for the source
  switch).
- Tests: `ValuationTests` (supported sources), `ValuationCalculationTests`
  (Cap as basis), persistence test for the constraint.
- Documentation: FRD-06 § Valuation sources (D40 gains CAP), FRD-12 § Case
  workspace, CONTEXT.md (CAP vs Super CAP).

## 5. Salvage slider (WP5; `salvage`)

No back end. The slider is a client control over `assessment.salvage_value`
against the read Engineer's Value; the posted value is the £ amount.

## 6. Unroadworthy reason bank (WP5; `bank`, J12)

### Core — new `src/Pegasus.Core/ReferenceData/UnroadworthyReasonPhrases.cs`

- `UnroadworthyReasonPhrase(Guid Id, string Text, int Order, bool Enabled,
  string RecordedBy, DateTimeOffset RecordedAtUtc, long Version)`.
- `IUnroadworthyReasonPhraseStore` (list enabled; list all; add; save).
- `UnroadworthyReasonPhraseAdministration`: `Add(text, actor)` — Engineer
  or Administrator with casework rights; text 3–200 characters, trimmed,
  no control characters, unique case-insensitively; `Disable`, `Reorder`
  — Administrator. Adding from the record and from Administration are the
  same operation. Records an operator action-log entry (the existing
  action log the Administration pages read).

### Infrastructure

- EF entity + `UnroadworthyReasonPhrases` table; additive migration.
- `EfUnroadworthyReasonPhraseStore`.
- Seed: none in code; the operator's starter phrases are entered through
  Administration (necessary-copy rule).

### Tests

Policy tests (validation, uniqueness, rights); persistence test; Web
tests for the Administration page and the record's Save to bank.

## 7. Delete all lines and Undo (WP6; `estdel`, J22)

- Delete all = the existing Draft save with an empty line list; Core
  already accepts it. Web adds the confirm.
- Undo = a Draft save that re-inserts the removed line at its position
  with its `Origin`, `SourceDocumentIdentity`, `SourceDocumentVersionId`,
  `SourceDocumentSha256`, `SourceRowIdentity` and `AmendedBy` as they were.
  `EstimateLineInput` already carries these. Core rule in
  `AssessmentOperations` (the Draft save): a posted line may carry an
  origin only when the same origin (document identity + row identity)
  was present on a line of this estimate's previous version or of any
  version of this estimate; otherwise it is rejected — so an origin can be
  restored but never invented.
- Tests: `EstimateLineAmendmentTests` — restore keeps origin; a fabricated
  origin is rejected.

## 8. Off-pattern cells (WP6; `offpattern`)

- `Estimates.cs` `EstimateTotals.Compute` records anomalies only for a
  unit amount on a non-part line (`OffPatternAmount`). Add the two the
  mockup shows: panel hours on Paint / Blend, paint hours on a line that is
  not Paint / Blend — each retained in the same treatment it has today
  (nothing re-bucketed) and listed in `OffPattern` with `Field` = `"panel
  hours"` / `"paint hours"` and the reason sentence.
- Tests: `EstimateTests` — one case per anomaly kind with the totals
  unchanged.

## 9. Regional uplift (WP6; `uplift`, J10)

### Core

- `LabourRateCards.cs`: `LabourRateCard.RegionalUpliftPercent`
  (decimal, 0–50, default 15) administered with the card; `SaveLabourRateCardRequest`
  gains it.
- `Estimates.cs` `EstimateDetails`: `bool RegionalUplift`;
  `HourlyRate` = card rate × (1 + percent/100) when on, rounded to pence
  (the reference rounds to the nearest 50 p only when scaling — not here);
  "keep entered rate" applies the same percent. `EstimateTotals.Compute`
  and the frozen `RecordedTotals` carry the uplifted rate; the totals
  strip already shows the rate used.
- New `src/Pegasus.Core/Address/PostcodeRegions.cs`: `OutwardCode(string
  postcode)` (UK outward-code parse), `IsLondonOrHomeCounties(outward)`
  over a Core-held set (the reference's areas plus its district lists for
  SG / OX / RG / CM), `UpliftSuggestion(repairer, claimant, storage)` →
  the first hit with its label ("Repairer (CR0)").
- The vocabulary flag `rates.regional_uplift` is retired (the fact lives
  on the estimate, which is what the rate belongs to) — or kept as the
  Case-level default if the operator prefers; J10.

### Infrastructure

Additive migration: `LabourRateCards.RegionalUpliftPercent`,
`CaseRepairSpecifications.RegionalUplift` (bit, default 0).

### Tests

`EstimateTests` (uplifted rate, totals, frozen totals unchanged on an
accepted version), new `PostcodeRegionsTests` (outward parse, London
areas, Home Counties districts, a miss), rate-card administration tests.

## 10. Provenance chips (WP6; `prov`)

No back end. `RepairSpecificationSource.Route` on the version and
`SourceDocumentIdentity` per line already say which import a line came
from.

## 11. Compare with per-line diff (WP6; `compare`)

### Core — new `src/Pegasus.Core/Assessment/EstimateComparison.cs`

- `Compare(RepairSpecificationVersion from, RepairSpecificationVersion to)`
  → `EstimateComparison(Added, Removed, Changed, Same, decimal GrossDelta)`
  where each Changed entry names the differing fields (type, quantity,
  unit £, hours, paint hours, materials).
- Matching: by `SourceRowIdentity` when both lines carry one from the same
  document; else by operation + normalised description (lower-case,
  punctuation collapsed — the mockup's `diffLines` key).
- Money from `EstimateTotals.ForProjection` on each side, never
  re-derived.
- Tests: new `EstimateComparisonTests` — identity match, description
  match, a rename counts as removed + added, delta sign.

## 12. Supplementary (WP6; `supp`, J8)

- Reuse § 11 for the diff. Baseline default: the estimate pinned by the
  latest generation that has `ReportSentEvidence`
  (`CaseReportGenerationRecord.Snapshot.CurrentEstimateId`); when none,
  the Current estimate.
- New vocabulary paths (no migration): `report.supplementary_explain`
  (Flag), `report.supplementary_reason` (Enumerated:
  `supplementary_estimate | dismantling | further_inspection |
  further_images`), `report.supplementary_baseline` (Text: the version
  id). `ValidateMergedState`: explain on requires a reason and a baseline
  that exists on the Case.
- `AssessmentNarrative.Supplementary(comparison, reason, from, to)` —
  the paragraph, once accepted (J8).
- `AssessmentReportProjection`: `SupplementaryText` in the snapshot when
  explain is on; `AssessmentReportLayout`: a "Supplementary Damage"
  section after "Damage". `CaseReportStaleReasons.ReportContentChanged`
  already covers the switch.
- Tests: projection (on/off; missing reason rejected), rendering (section
  present), narrative strings.

## 13. Address book (WP7; `abook`, J13)

- `CaseReportDeliveryPreparation.cs`: `ReportRecipientSuggestions` gains
  `IReadOnlyList<SuggestedRecipient> CaseContacts` (role, name, address —
  claim source, repairer, storage, claimant when recorded) and
  `IReadOnlyList<SuggestedRecipient> Staff`; `SuggestionFingerprint`
  hashes them too so a changed contact stales a prepared delivery;
  `Address()` unchanged (To still defaults from the Principal only;
  nothing else is copied implicitly).
- Infrastructure `IReportRecipientSuggestionQueries` implementation reads
  the Case's contacts (`ContactDirectory`, Case data fields) and enabled
  staff accounts with an e-mail.
- Tests: `CaseReportDeliveryPreparationTests` — fingerprint changes with
  a contact; To unchanged; a Cc chosen from the book validates as a plain
  address.

## 14. Attachments (WP7; `attach`, J14)

- `CaseReportArtifactKind`: `EstimateDocument` (from
  `../estimate-generator/PLAN.md` Phase 2) and `ImageSheet` (new).
- `PrepareCaseReportDeliveryRequest.AttachmentKinds`
  (`IReadOnlySet<CaseReportArtifactKind>`, Report always present);
  `CaseReportDeliveryPolicy.Attachments` filters to the requested kinds
  and still requires every requested artifact Confirmed; the preparation
  record freezes the chosen kinds; `SendPreparedCaseReport`'s re-check
  compares the same set.
- Generation produces the extra artifacts only when asked (a generation
  option like `IncludeFeeNote`), through the existing freeze → render →
  custody → confirm path; `ImageSheet` needs an `IImageSheetRenderer`
  (QuestPDF, the report's photo grid at six per page) behind a Core
  contract — and an accepted design first.
- Tests: `CaseReportDeliveryPreparationTests`, `CaseReportGenerationTests`
  (artifact set per request), custody confirmation per kind.

## 15. Re-send naming and message (WP7; `resend`, J15)

- `CaseReportGeneration.FileNameOf` → `{REF} {VRM} {Title}.pdf` with the
  title from `AssessmentReportPresentation.Title`; characters outside
  letters, digits, space, hyphen and dot replaced; a re-send appends
  ` (n)` where n is the count of earlier Sent generations + 1 (J15 decides
  dot vs suffix). Box custody receives the same name.
- New `Reports/CaseReportCoveringMessage.cs`: `Compose(snapshot,
  previousSentDate?)` returning the accepted covering text and its
  "supersedes our report dated {date}" variant; `SendPreparedCaseReport`
  passes it as `Body` instead of `string.Empty`. The text is an
  operator-supplied correspondence template (FRD-11) — no build before
  acceptance.
- Tests: file-name cases; body variants; the transport receives the body.

## 16. Report / Fee tabs (WP7; `feetab`, J16)

If J16 chooses a Principal fee table: `PrincipalReportGenerationPolicy`
gains `DefaultFee(decimal Amount, string Description)`; Case creation and
Correct principal seed `fee.agreed_fee` / `fee.description_lines` when
empty; additive migration on the principal policy table; administration
on the Principal page; tests for seeding and non-overwrite. Otherwise no
back end.

## 17. Sign-off follows the Engineer (WP7; `signoff`, J17)

- `Lifecycle/CaseLifecycle.cs` (the `IAssignCaseEngineer` implementation)
  and the Hand to Engineer path: after assignment, when the assigned
  account is flagged Sign-off Engineer and the Case's current
  `SignOffEngineerId` equals D31's default computed before the change
  (i.e. it was never chosen explicitly), set `SignOffEngineerId` to the
  assigned account and record the same event `SetSignOffEngineer`
  records. Otherwise leave it.
- Tests: lifecycle tests — follows when default, keeps an explicit choice,
  ignores an unflagged Engineer.

## 18. Report date on generate (WP7; `reportdate`, J18)

- As drawn, generation would write `report.report_date` after the freeze;
  an assessment write marks the generation Stale
  (`AssessmentFactsChanged`), so the freshly generated report would be
  stale at once. If kept, the write must happen inside the generation
  command before the snapshot's Case version is pinned, in the same
  transaction, and must not raise a stale mark — a special case in
  `CaseReportGeneration` and `EfCaseReportGenerationStore`. That is the
  reason the recommendation in J18 is to drop the switch: the generation
  facts line already shows the date.

## 19. Report wording (WP8; `wording`, I2, J7, J8)

Only after I2 changes FRD-11 and ADR-0050.

### Core — new `src/Pegasus.Core/Reports/ReportWording.cs`

- `ReportWordingBlock(string Key, string Title, ReportWordingKind Kind,
  string? Text, int Order, bool Removed)`; `Kind` = `Composed | Edited |
  PassThrough | Manual`. Keys: `nature`, `engineers_comments`,
  `supplementary`, `valuation_commentary`, `unrelated_damage`,
  `history_check`, `condition`, `settlement`, `salvage`, and `custom-{n}`.
- `ReportWordingPolicy.Compose(snapshotFacts)` → the default blocks from
  `AssessmentNarrative` and the settlement / salvage composers (moved out
  of the renderer into Core: `SettlementText` already lives in
  `AssessmentReportPresentation`; the Salvage paragraph per category needs
  J7's accepted texts).
- `Merge(stored, composed)`: Composed blocks take the fresh text; Edited
  keep theirs; Removed are dropped; Manual kept; order from the stored
  list with new blocks appended. `Validate`: title 1–80, text ≤ 4,000, no
  control characters; `nature` and `settlement` cannot be removed;
  `supplementary` present only when explain is on.
- Storage: one new table `CaseReportWording` (CaseId, Key, Title, Kind,
  Text, Order, Removed, Version) rather than a JSON field — blocks are
  large and edited one at a time. Additive migration. Writes go through
  the lease and version guard and mark the generation Stale
  (`ReportContentChanged`).

### Projection and renderer

- `AssessmentReportProjection` puts `Blocks` on the snapshot;
  `AssessmentReportLayout` renders the narrative sections from the blocks
  in order, keeping the non-narrative anchors fixed (the Damage diagram
  and tables after `nature`; Tyres and Seat Belts after the Damage tables;
  the settlement value box and rows inside `settlement`; Vehicle Data and
  the cost table on page 2 as now). The template version string changes.
- `Validate` on the snapshot: the same rules as the policy, so a stored
  block that no longer fits the facts fails closed.

### Tests

Policy tests (compose, merge, validate, order), projection, rendering
golden (block order and titles in the PDF text), integration tests for
the panel's edit, rename, remove, reorder, recompose, new paragraph.

## 20. Click to include (WP5; `include`, J19)

No back end: the Images tab posts the existing report-role change
(`CaseAssetPreparation`, `CaseAssetReportRole.Supporting | NotUsed`).

## 21. Queries panel (WP5; `queries`, J20)

If J20 lists correspondence: a read query over the Case's correspondence
rows after `ReportSentEvidence`, in the existing Case queries owner
(`Cases/CaseQueries.cs`). Otherwise none.

## 22. Ribbon badges, nine sections, placements, tick rows (WP5)

No back end. `place` posts the Sign-off select through the existing
`ISetCaseSignOffEngineer` and the report content switches through the
existing assessment save — only the form's home changes.

## 23. Decision-first features

Not planned. Each would be its own Core policy change (lease take-over in
`RecordEditScope`, identity edits in `CaseLifecycle`, Case type
mutation, a second Engineer's Value writer, mileage defaulting, import
overwrite, self-rescaling in `Estimates`, a WhatsApp transport, implicit
reopen) and each contradicts an FRD sentence named in OPEN-ISSUES § 3.
