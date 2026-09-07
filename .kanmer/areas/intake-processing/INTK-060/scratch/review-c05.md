---
verdict: needs-changes
ticket: INTK-060
slice: C05 — extract third-party reports as source evidence
head: 11a306580d50a0f3335f06972db9ed2e784d6f19
review_head: d0daa234098c6932ba4b4a0b3e9226eaae7bfbb5 (slice head merged with Stream C 2b6b5ed37)
base: 36b0d84f1fab924bb5278a8ac6e131ced5b80ff3
diff_reviewed: git diff 36b0d84f1..11a306580 (6 files, +1962/-118)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c05
branch: c05-third-party
ownership: PASS
stop_conditions: none tripped
lanes_seen: 1-build PASS, 2-core PASS, 3-corpus PASS, 4-web PASS-WITH-SKIPS, 5-architecture PASS
majors: 1
minors: 8
independent: true
skill_sha256:
  - file: C:/Users/PGUSER/documents/github/pegasus/.agents/skills/kanmer-review/SKILL.md
    sha256: addf26c9981cefa755a9db3a1ee06383432230708641b076ee336d64a1096741
---

# C05 review — third-party reports as source evidence

## Ownership and frozen contracts — PASS

`git diff --name-status 36b0d84f1..11a306580` touches exactly six paths, all named in
`C-intake.md` "### C05 files":

- `src/Pegasus.Core/Intake/ProcessIntake.cs` (M)
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportExtraction.cs` (M)
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportValidation.cs` (M)
- `tests/Pegasus.Core.Tests/Intake/ThirdPartyReports/ThirdPartyReportExtractionTests.cs` (A)
- `tests/Pegasus.IntegrationTests/ThirdPartyReportCorpusTests.cs` (A)
- `tests/Pegasus.IntegrationTests/ThirdPartyReportProvenanceWebTests.cs` (A)

No A-owned file is touched: no `DependencyInjection.cs`, no migration, no entity, no
`PegasusDbContext`, and `ThirdPartyReportContracts.cs` (frozen at F/G1) and
`ThirdPartyReportProfiles.cs` are byte-identical to the base. `MultiFormatGenuineCorpusWebTests.cs`
is untouched, so no existing assertion was weakened. The one contract change the implementer
wanted (`ThirdPartyReportValuation.Adjustments`) is **requested, not applied** — correct, and the
printed "Urban edition adjustment" is carried as a `valuation.adjustment` source row with its
printed label preserved (`RawWholeMatch`), which is the right way to respect a frozen type.

`ProcessIntake`'s new dependency is a trailing optional constructor parameter, matching the three
optional parameters already there, so no caller and no registration breaks.

## Stop conditions — none tripped

- **Issuer never inferred from principal, folder or filename.** `ThirdPartyReportProfiles.Select`
  reads only `IntakeSourceReadResult.Content` text (`ThirdPartySourcePage.Flat`); the filename
  reaches the reader only through `SourceLabel`, which selection never matches against.
  `TheIssuerIsNeverTakenFromTheFileNameOrTheRetainedPrincipal` proves both directions (a Connexus
  body under a Montgomery filename is Connexus; a Montgomery filename with no signature is not a
  report at all).
- **No negative file becomes a report.** GG/Audatex → `Estimate`, MotorCheck → `VehicleHistory`,
  EVA images → `ImageEvidence`, TonBridge and John R Bell → `TextUnavailableRequiresOcr` (no text,
  so no role is guessed). `NoNegativeOriginalIsGivenAReportVerdict` asserts `Candidate is null`
  and no outcome/repairability/net/gross row for all five non-family originals, on the real PDFs.
- **No source arithmetic is silently repaired.** Every arithmetic result is a
  `ThirdPartyReportFinding`; no code path writes back a computed value. The Connexus derived net
  is emitted as `net-not-printed` with the total in the message and no net row
  (`AConnexusNetIsDerivedAsAFindingAndNeverWrittenBackAsASourceValue`). Money normalization no
  longer forces two decimals, so Laird's printed `£1686.7` keeps its tenth.
- **C writes no Engineer/CE value.** No Case, allocation or decision is written; the web test
  asserts `receipt.AcceptedCaseId` and `CurrentCaseId` are both null after a genuine report upload.
  Reading Laird's printed *"Engineer's Value"* cell into `valuation.pav` is third-party evidence,
  not a CE conclusion (see C05-R-5 for the label-provenance caveat).

## Family cases — all present and proved on the real PDFs

Connexus amendment/base and the initial £2,394.25 / agreed £3,351.95 labour roles; Exclusive EREHR
page-one net against the page-two breakdown; EVA bodyshop by issuer evidence plus the
`ErehrClaimReference` negative rather than generic layout; Laird gated on the Supplementary
heading with the anchored `^Total:` fix so the subtotal is not re-read as the total; Montgomery
26.2 × £90 preserved beside the printed £1,582.20 with `component-sum-reconciles` and
`net-vat-gross-reconciles` standing together; sPrint zero totals, £8,250 contract repair and the
`NOTING ORIGINAL` amounts as three anchored roles; John R Bell's scan-only pages each emitting a
`source.page.requires-human-verification` row with a page locator and no invented value.

Every emitted row carries hash, occurrence, document/party/reference role, page, raw text,
normalized value, unit, currency, reader version, profile version and disposition
(`ThirdPartySourceCandidates.Create`). Ids are derived from
hash+occurrence+field+roles+page+raw+disposition, so replay is stable rather than merely equal.

## The implementer's padding/engine claim — verified

`ThirdPartyReportCorpusTests.ReadCorpusAsync` instantiates the **production**
`MimeKitPdfPigOpenXmlIntakeSourceReader`, verifies each original's recorded SHA-256 before reading
it, and runs all 29 PDFs through it; the Core-side test additionally replays the pack's PyMuPDF
text and a deliberately collapsed variant of each excerpt
(`TheSameValuesAreReadWhetherOrNotTheTextEngineKeepsTheColumnPadding`). Wave 12 lane 3 shows
`Passed: 8, Skipped: 0` — so the eight real-PDF/PdfPig assertions, including the worked values and
the Montgomery contradiction, actually executed. The claim is substantiated by execution, not
only by the offline replay in the report.

## Test honesty

`ReferencePackFactAttribute` skips with a stated "INCONCLUSIVE, not passed" reason and only when
`PEGASUS_REFERENCE_PACK_ROOT` is unset or absent; the pack is present here and lanes 2 and 3 show
`Skipped: 0`, so nothing passed vacuously. `ReadCorpusAsync` asserts 29 results and hash-matches
every file, so a shrunken inventory fails rather than silently narrows.
`EveryClassifiedCorpusReportReadsAtLeastOneUsableField` closes the "classified but read nothing"
hole. The web test composes only the registrations Stream A owns and says so, and uses a document
reader that throws rather than pretending to open anything.

## Findings

### C05-R-1 (major) — every reconciliation finding is discarded on the production path

`src/Pegasus.Core/Intake/ProcessIntake.cs:333-349` (`RecordThirdPartyReportSourceAsync`) records
`ThirdPartyReportAnalysis.ToCandidates(extraction, …)` and nothing else.
`ThirdPartyReportExtractionResult.Findings` and `.Candidate` are never read outside tests
(`grep` for `.Findings` and `IThirdPartyReportCandidateQueries` over `src/` returns only Triage's
unrelated findings and the frozen interface's own declaration, which has no implementation).
`RetainedInstructionAnalysis` has no findings collection and `IntakeSourceCandidateEntity`
(`src/Pegasus.Infrastructure/Persistence/V1FoundationEntities.cs:178-200`) has no column for one,
so the Montgomery `labour-hours-rate-mismatch`, the sPrint `zero-totals-with-contract-repair`, the
Connexus `net-not-printed`, the Laird `supplement-without-proved-base` and every
`*-reconciles` result are computed and thrown away at the only production call site. Field-level
conflicts survive as `Conflicting` rows and the scan-page rows do persist, so the loss is exactly
the arithmetic/cross-field contradictions the plan's family cases are written around.

The gap is also undeclared: `c05-report.md` states the Montgomery contradiction is "raised as a
conflict *beside* the component total and net+VAT that both reconcile" and lists the DI handoff for
A as "the same ones C01 is already waiting on, plus nothing" — neither mentions that no store,
query or screen can reach a finding. ASSUMPTIONs 1-5 on `scratch/c05-notes` do not cover it either.
`ThirdPartyReportValidation.PolicyVersion` (`ThirdPartyReportValidation.cs:67`) is unreferenced for
the same reason: nothing persists a finding, so nothing stamps its version.

Fix (no redesign, no A-owned edit): record the decision as an ASSUMPTION on
INTK-060 `scratch/c05-notes` and add the finding-persistence item to the C-F02 handoff in
`c05-report.md`, naming what A must add (a findings table or a findings column on the analysis row,
plus the finding policy version) and what the Received screen should render. Correct the report's
family-case wording so it claims what shipped. If a cheaper interim is wanted, the finding codes
could be emitted as `SourceFieldCandidate` rows under a `finding.*` field prefix, which needs no
schema change — but that is a design choice for the controller, not something to slip in silently.

### C05-R-2 (minor) — an absent amount is read as a printed zero when a contract repair exists

`src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportValidation.cs:319-321`:
`(estimate.Net?.Value ?? 0m) == 0m && (estimate.Gross?.Value ?? 0m) == 0m &&
(estimate.LabourAmount?.Value ?? 0m) == 0m`. A role that simply does not print those three
amounts is therefore reported as "the ordinary repair totals are zero", which is the one
distinction this slice exists to keep — a printed `0.00` is evidence, an unprinted amount is
unavailable (`C-intake.md`: "absent evidence is unavailable"). It cannot fire wrongly on today's
corpus (only sPrint declares a `Contract Repair` rule and both sPrint originals print explicit
zeros in the ordinary table plus a labour amount in the note), and the line predates this slice's
diff, but the slice ships it. Fix: require the values to be *present* and zero —
`estimate.Net?.Value == 0m && estimate.Gross?.Value == 0m && estimate.LabourAmount?.Value == 0m`
(or `is { } v && v == 0m` per component) so a missing figure cannot manufacture a conflict.

### C05-R-3 (minor) — dead members left behind, contrary to the report's "no dead code" claim

Unreferenced anywhere in `src/` or `tests/`:
`ThirdPartyEstimateRoles.Parse` (`ThirdPartyReportExtraction.cs:123`),
`ThirdPartyReportExtraction.ExtractableFamilies` (`:627`),
`ThirdPartyReportFields.BaseReportReference` (`:24`),
`ThirdPartyReportFields.Diagram` (`:109`),
`ThirdPartyReportValidation.PolicyVersion` (`ThirdPartyReportValidation.cs:67`).
`c05-report.md` states "Nothing was deleted as dead — the drafts had no dead code", which is not
accurate for these five. Fix: delete `Parse`, `ExtractableFamilies` and `BaseReportReference`;
keep `Diagram` and `PolicyVersion` only if C05-R-1's handoff gives them a consumer, otherwise
delete them too. Correct the report's claim either way.

### C05-R-4 (minor) — one member carries two `<summary>` blocks and the rule table's own comment is orphaned

`ThirdPartyReportExtraction.cs:571-581`: the summary describing the per-family rule table (and the
deliberately empty John R Bell entry) sits immediately above `private const string
NarrativeLabels`, followed by a second `<summary>` for `NarrativeLabels` itself. So
`NarrativeLabels` has two doc comments and the `Rules` dictionary (`:604`) has none — the
explanation a reader needs for the empty John R Bell rule list is filed on the wrong member. Fix:
move the first block onto the `Rules` dictionary.

### C05-R-5 (minor) — the printed label is not recoverable for any row but the Montgomery adjustment

`ThirdPartyReportExtraction.cs:806` sets `region: "label"`/`"section"` and `cell` is never passed,
and neither survives persistence: `RetainedInstructionCandidate` and `IntakeSourceCandidateEntity`
have no cell/region/form-field column (`LocatorJson` carries source label and page only). Because
`Money` and `Text` rules capture only the value, a persisted row reads
`valuation.pav = 1686.7` with no trace that Laird printed it under **"Engineer's Value"**
(`:401`), and Montgomery's `valuation.trade` / `valuation.retail` lose the `Glasses` guide column
they were read from. The plan's invariant asks for the "smallest useful layout locator … table/cell
… source label" beside every candidate. The mechanism already exists and is used once
(`RawWholeMatch`, Montgomery `valuation.adjustment`). Fix: set `RawWholeMatch` on the rules whose
printed label differs materially from the field name (at minimum Laird `Engineer's Value`,
Montgomery `Valuation`/`Glasses` trade and retail), or add the cell locator to the C-F02 handoff
so the region C05 already computes stops being discarded.

### C05-R-6 (minor) — the two swallowed exceptions leave no trace

`ProcessIntake.cs:352` (`catch (RetainedInstructionAnalysisConflictException)`) and `:359`
(`catch (Exception) when (IntakeExceptionPolicy.IsRecoverable(exception))`) both return silently.
The house pattern tags the activity in the same situation
(`src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs:377-380` sets
`image_intake.outcome`, `failure_type` and an error status). As written, an environment where the
report reading always fails is indistinguishable from one with no third-party reports, and
`ReprocessingTheSameRetainedBytesDoesNotWriteASecondSetOfCandidates` passes identically whether the
second write genuinely replayed or threw a conflict that was swallowed. Fix: tag the intake
activity with the outcome in both catches (the `ActivitySource` is already on the class), and
assert the replay path positively in the test.

### C05-R-7 (minor) — the corpus determinism test excludes the candidate id it is meant to prove

`tests/Pegasus.IntegrationTests/ThirdPartyReportCorpusTests.cs:309` (`Describe`) omits `row.Id`,
so `ReadingTheWholeCorpusTwiceProducesTheIdenticalRecord` compares content only. The ids derive
solely from hash, occurrence, field, roles, page, raw value and disposition — none of which
changes between the two reads even though the test generates fresh receipt and asset guids — so
adding `row.Id` to `Describe` would pass today and is the assertion that actually proves
deterministic replay on the real PDFs. The Core test already asserts ids
(`ThirdPartyReportExtractionTests.cs:467-470`); the harsher test does not.

### C05-R-8 (minor) — the 29-file recorded classification is duplicated verbatim in two projects

`ThirdPartyReportCorpusTests.cs:37-66` and `ThirdPartyReportExtractionTests.cs:502-528` each carry
the same 29-entry expected-classification dictionary. One recorded fact, two owners, and nothing
keeps them in step. Fix: read the expectation from the pack inventory (which already lists each
source), or keep it in one project and let the other assert only what its own text shape can.

### C05-R-9 (minor) — the image-evidence signature's negative list is asymmetric with the others'

`ThirdPartyReportProfiles.cs:248-256`: `image-evidence/1` denies `REPAIRABLE REPORT`,
`Vehicle Assessors`, `Assessment Report`, `Full Estimate Report`, `Vehicle History Check`,
`Consulting Motor Engineers` and `Automotive Claims Assessors`, but not `Supplementary Report` and
not `laird-assessors\.com` — both of which `invoice/1` does deny. A Laird **supplementary** report
that prints an appended image filename would therefore match `laird/1` and `image-evidence/1`
together, become `Ambiguous`, and emit no candidate at all. No corpus original triggers it (all
four Laird files classify as Laird on the real PDFs), so this is latent. Fix: add
`laird-assessors\.com` and `Supplementary\s+Report` to `image-evidence/1`'s negatives.

## Test evidence (wave 12, lanes 1-5, all present)

| lane | result | detail |
| --- | --- | --- |
| 1-build | PASS | `dotnet build ./Pegasus.slnx -c Release --no-restore`, exit 0, 0 warnings. Attempt 1 exit 1 was a stale MSBuild node-reuse file lock (MSB3027/MSB3021), kept for record and resolved with `dotnet build-server shutdown`; no source or git change. |
| 2-core | PASS | 24 passed, 0 skipped, 0 failed. Pack-gated facts ran. |
| 3-corpus | PASS | 8 passed, 0 skipped, 0 failed — all 29 originals through `MimeKitPdfPigOpenXmlIntakeSourceReader`. |
| 4-web | PASS-WITH-SKIPS | 7 passed, 5 skipped, 0 failed. See below. |
| 5-architecture | PASS | 100 passed, 0 skipped. |

Lane 4's five skips are recorded as a deviation from the expected skip reason: they are **not** the
absent-pack INCONCLUSIVE, they read "The ignored local genuine corpus has no `.doc`/`.msg`/`.jpg`/
`.png`/`.docx` source at or below the 10 MB Web limit". All five are in
`MultiFormatGenuineCorpusWebTests`, a pre-existing class this slice does not touch, and concern the
multi-format genuine corpus rather than the third-party report pack. `4-web.log` confirms the skip
list contains no `ThirdPartyReportProvenanceWebTests` entry, so all three of C05's web tests ran
and passed. This does not undermine C05's evidence and is not attributed to it, but it does mean
lane 4 is not a clean PASS and the C09 whole-stream review should settle whether that corpus gap
is expected on this machine.

## Verdict

**needs-changes** at head `11a306580`. Ownership and the four stop conditions are clean, every
family case is implemented and proved against the real PDFs through the production reader, and all
five wave-12 lanes are green. The single major is that the reconciliation findings — the only
representation of the printed contradictions the plan names — are computed and discarded on the
production path with no store, query, screen or declared handoff, while the implementation report
describes them as recorded. Fixing C05-R-1 (declare the gap and correct the report) plus the eight
minors, none of which needs an A-owned file, should return this to `pass`.

---

# SUPERSEDING ATTESTATION — C05 correction round 1

```yaml
verdict: needs-changes
ticket: INTK-060
slice: C05 — extract third-party reports as source evidence
head: 7b632169bd759de58281f61bbcfc44ffd132c921
supersedes: 11a306580d50a0f3335f06972db9ed2e784d6f19 (needs-changes, C05-R-1…R-9)
base_of_correction: 975bf107b
diff_reviewed: git diff 975bf107b..7b632169b (7 files, +705/-151, 7 commits)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c05
branch: c05-third-party
ownership: PASS
frozen_contracts: PASS (ThirdPartyReportContracts.cs blob d024366c8 at 36b0d84f1, 15518699c and 7b632169b)
stop_conditions: none tripped
lanes_seen: 1-build PASS, 2-core PASS, 3-corpus FAIL, 4-web FAIL-WITH-SKIPS, 5-architecture PASS
majors_open: 3
minors_open: 1
notes_open: 3
independent: true
review_round: 1
skill_sha256:
  - file: C:/Users/PGUSER/documents/github/pegasus/.agents/skills/kanmer-review/SKILL.md
    sha256: addf26c9981cefa755a9db3a1ee06383432230708641b076ee336d64a1096741
```

## Verdict

**needs-changes** at `7b632169b`. Eight of the nine prior findings are genuinely fixed and
C05-R-1's remediation is the right shape — but two of the five lanes are **red at this head**,
and both failures are in the code this round added. A `pass` requires every lane green; lane 3
fails `EveryRecordedFindingIsPersistedAsItsOwnSourceRow` and lane 4 fails
`ReprocessingTheSameRetainedBytesDoesNotWriteASecondSetOfCandidates`. Neither is a harness
problem: each names a real defect (C05-R-10 and the open half of C05-R-6).

## Ownership, frozen contracts, scope — PASS

`git diff --name-status 975bf107b..7b632169b` touches exactly seven paths, every one of them in
`C-intake.md` "### C05 files":

- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportExtraction.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportProfiles.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportValidation.cs`
- `tests/Pegasus.Core.Tests/Intake/ThirdPartyReports/ThirdPartyReportExtractionTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportCorpusTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportProvenanceWebTests.cs`

`ThirdPartyReportProfiles.cs` is newly touched this round and is C-owned by the same map, so this
is not scope creep. `ThirdPartyReportContracts.cs` is the identical blob (`d024366c8`) at the
stream base, the C branch tip and this head — the frozen contract is untouched. No
`DependencyInjection.cs`, no migration, no entity, no `PegasusDbContext`, no `Details.cshtml`, no
`OperatorLabels.cs`, no `MultiFormatGenuineCorpusWebTests.cs`.
`IRetainedInstructionAnalysisStore.RecordAsync` already returned `(Analysis, IsReplay)`; the new
deconstruction adds no contract change.

## C05-R-1 — the finding-persistence mechanism, verified in detail

**Disposition: fixed** (with residual raised as C05-R-10 and C05-R-11 below).

- `Extract` now funnels both return paths through one `Complete(...)`, which calls
  `ThirdPartyReportValidation.Check` on the rows read **before** any finding row is appended.
  A finding therefore cannot be an input to a reconciliation. Verified by reading
  `ThirdPartyReportExtraction.cs` `Complete`/`FindingRows`, not by the report's description.
- Each finding becomes one `SourceFieldCandidate` under `finding.<code>`, with the statement as
  raw text, the code as normalized value, `Conflicting` for `ThirdPartyFindingKind.Conflict` and
  `Ambiguous` otherwise — never `Usable`, never `Missing`.
- The rows reach production storage: `ToCandidates` maps `result.Candidates` (which now contains
  the finding rows) and `ProcessIntake.RecordThirdPartyReportSourceAsync` records exactly that
  list. `RetainedInstructionCandidate` carries `row.PolicyVersion`, so
  `third-party-report-validation/1` is stamped and `PolicyVersion` has a real consumer.
- `IntakeSourceCandidateEntity.RawValue` has no `HasMaxLength`
  (`V1FoundationModelConfiguration.cs:90-97`), so a sentence-length statement is `nvarchar(max)`
  and cannot truncate.

**No printed value was altered.** `Observe` computes `Raw` and `Normalized` independently:
`Raw: rule.Rule.RawWholeMatch ? match.Value : Captured(match)` while
`var normalized = Normalize(rule.Rule.Kind, value)` always runs on the **captured group**
(`ThirdPartyReportExtraction.cs:893-900`). Deduplication is on `Normalized`, so widening the raw
text changes neither a value, a disposition, nor how many rows a field emits. No production code
parses `RawValue` — the only four references are the two mapping projections and the EF write.
The web lane confirms this end to end: `26.20`, `90.00` and `1582.20` still read back from SQL
Server, no `estimate.labour.amount` row holds `2358.00`, and the finding row states both printed
figures.

**Replay determinism is intact.** `DeterministicId` is a pure function of
sha+occurrence+field+partyRole+referenceRole+page+raw+disposition, and raw is `Collapse(...)`ed
(whitespace runs to one space, then `.Trim()`), so a padded and a collapsed text engine derive the
same id. Lane 3's `ReadingTheWholeCorpusTwiceProducesTheIdenticalRecord` now leads its comparison
with `row.Id` (C05-R-7) and **passed** at this head — so the id derivation itself, not merely the
content, is proved on the real PDFs. Ids do change relative to the previous head because 36 rules
now carry a different raw value; nothing reads an id across runs and the operation key prevents a
rewrite, so this is a version boundary, not a defect.

**The four enumerated findings.** `RecordedFindings` in the corpus suite names eleven (file, code)
pairs covering Montgomery hours×rate, sPrint zero-totals-vs-contract-repair, Connexus
`net-not-printed`, Laird `supplement-without-proved-base` and John R Bell's OCR pair.
`NoNegativeOriginalCarriesAFindingAboutRepairFigures` (passed) proves the three text-bearing
negatives raise **no** finding at all and the two scan-only ones raise only the OCR and
page-verification findings. `ThePersistedFindingRowsAreExactlyTheFindingsRaised` (passed) proves
the row list and the finding list are one-for-one and in order. The Montgomery case is proved
end to end through SQL Server by the passing web test. The remaining pairs are **consistent with**
the evidence but not proved, because `EveryRecordedFindingIsPersistedAsItsOwnSourceRow` aborts at
the first failing assertion (C05-R-10) before it finishes the list.

**Rendering — acceptable for this slice.** `src/Pegasus.Web/Pages/Intake/Details.cshtml:627-641`
renders every candidate unfiltered as `<strong>@candidate.Field: @candidate.RawValue</strong>`
plus `OperatorLabels.SourceCandidateDisposition(...)`, the source label and the page, so a finding
lands on `/Received/{id}` as `finding.<code>: <statement>` with "Conflicting statements". The web
test asserts that exact HTML and it passed. `_Provenance.cshtml` renders a provenance icon and
`_EvidenceViewer.cshtml` is the image/PDF overlay — neither renders a candidate row, so the
"Finding" chip genuinely cannot be built inside C05's file map without editing C04's
`Details.cshtml` and C08's `OperatorLabels.cs`. **I accept the deferral.** ASSUMPTION 7 on
`scratch/c05-notes` records the decision, both rejected alternatives, and the one-line change each
downstream slice needs (`ThirdPartyReportFields.IsFinding(candidate.Field)` in C04's list, one
label in C08). That is a properly declared handoff, not a silent gap. ASSUMPTION 6 records the
persistence shape, the field-key spellings kept over the dispatch's illustrative ones, and the
A-owned findings-table alternative not taken.

## Prior findings — dispositions

| id | severity | disposition | evidence |
| --- | --- | --- | --- |
| C05-R-1 | major | **fixed** | `Complete`/`FindingRows`; `ToCandidates` maps `result.Candidates`; web lane reads the Montgomery finding back from SQL Server and finds it in the served HTML. Residual: C05-R-10, C05-R-11. |
| C05-R-2 | minor | **fixed** | `ThirdPartyReportValidation.cs:319-323` is now `estimate.Net?.Value is 0m && estimate.Gross?.Value is 0m && estimate.LabourAmount?.Value is 0m` — a null no longer satisfies it. `AnUnprintedTotalIsNotReadAsAPrintedZeroBesideAContractRepair` covers it and lane 2 is green. |
| C05-R-3 | minor | **fixed** | `ThirdPartyEstimateRoles.Parse`, `ExtractableFamilies`, `BaseReportReference`, `Diagram` deleted; grep over `src/` and `tests/` for each returns nothing. `PolicyVersion` kept and now genuinely consumed by `FindingRows`. The report's "no dead code" claim is corrected under "### Deleted". |
| C05-R-4 | minor | **fixed** | The rule-table summary now sits directly above the `Rules` dictionary; `NarrativeLabels` keeps one summary. |
| C05-R-5 | minor | **fixed** | 36 rules gained `RawWholeMatch` (37 in the file including the pre-existing adjustment), covering every case the finding named: Laird `Engineer's Value`, Montgomery `Glasses` trade/retail and `Valuation`, the supplement's `Subtotal:`/`Total:` pair, and the `Repair Cost … exc/inc VAT` pair whose only distinguishing token is the printed label. `APrintedLabelThatNamesSomethingOtherThanTheFieldIsKeptInTheRawText` asserts label **and** exact normalized value together. |
| C05-R-6 | minor | **open** | The tagging half is done and correct (`intake.third_party_report.outcome` / `.failure_type` on all four paths, tagged before `RecordTelemetry`). The assertion half is **red** — see below. |
| C05-R-7 | minor | **fixed** | `Describe` leads with `row.Id`; `ReadingTheWholeCorpusTwiceProducesTheIdenticalRecord` passed at this head. |
| C05-R-8 | minor | **fixed** | The 29-entry dictionary now has one owner (the integration corpus suite). The Core replacement, `EveryCorpusOriginalClassifiesTheSameWhicheverWayTheTextIsSpaced`, is a better test than the copy it replaces: it proves padding independence over all 29 and fails any original that matches two document signatures. |
| C05-R-9 | minor | **fixed** | `laird-assessors\.com` and `SupplementaryReportTitle` added to `image-evidence/1`'s negatives, and the printed heading now has one owner (`ThirdPartyReportProfiles.SupplementaryReportTitle`, read by the Laird signature, the image-evidence negatives and `ThirdPartyReportExtraction.SupplementaryHeading`). `ALairdSupplementThatNamesAnAppendedImageIsStillOnlyALairdReport` asserts `Assert.Single(result.Selection.Matches)`. |

### C05-R-6 (minor, still open) — the replay assertion is red and shows the replay never happens

Lane 4:

```
Assert.Contains() Failure: Item not found in collection
Collection: ["no_report_signature", "recorded"]
Not found:  "recorded_reading_stands"
ThirdPartyReportProvenanceWebTests.cs:313
```

The re-evaluate pass emitted **no** third-party outcome at all — not `replayed`, not
`recorded_reading_stands`, not `not_recorded`. So `IReevaluateIntake.ExecuteAsync` does not run
`RecordThirdPartyReportSourceAsync` on the retained bytes, and
`Assert.Equal(first.Count, second.Count)` passes because the second pass wrote nothing rather than
because a replay was correctly refused. This is exactly the ambiguity C05-R-6 was raised to close,
and the new tag is what exposed it — the fix is working as a diagnostic even though the test is
red. Two things to settle, and either may be the answer:

1. If re-evaluation is *supposed* to re-read the source, the reading has to be reachable from that
   path (or the test has to drive the path that is).
2. If it is not, the test's claim and name are wrong and should say what they prove — that the
   first recording is not disturbed — with the outcome assertion narrowed to `recorded`.

Separately, `ActivitySource.AddActivityListener` is process-global: the `no_report_signature`
entry did not come from this test's two intakes, so the comment "Only this class composes the
analysis store, so only this class's intakes tag a report-reading outcome" is not a safe
assumption under parallel collections. Filter the listener by the receipt or the activity the test
started.

## New findings

### C05-R-10 (major) — a persisted finding row can carry no source label at all; lane 3 is red

```
Assert.False() Failure — Expected: False, Actual: True
ThirdPartyReportCorpusTests.cs:325   →  Assert.False(string.IsNullOrWhiteSpace(row.SourceLabel));
Failed: 1, Passed: 10, Skipped: 0
```

`FindingRows` takes its locator from `finding.Evidence[0]`, falling back to `selection.Issuer`
(`ThirdPartyReportExtraction.cs`, `FindingRows`). For a source with no readable pages,
`ThirdPartyReportProfiles.Select` returns early and `Verdict` builds the issuer row with
`sourceLabel: evidence?.SourceLabel ?? string.Empty` (`ThirdPartyReportProfiles.cs:398-407`) —
there is no signature evidence, so the label is `""`. The `source-requires-ocr` finding's evidence
is `[selection.Issuer]`, so its row inherits the empty label. The scan-only originals
(`JohnRBell1.pdf`, `TonBridgeAccidentRepair1.pdf`) are precisely the ones with no pages.

That is a real provenance defect, not a strict test: a persisted row that states a contradiction
and names no source at all is the one thing the slice's own invariant forbids. The Montgomery row
is fine (the web lane asserts
`Assert.All(candidates, row => Assert.False(string.IsNullOrWhiteSpace(row.SourceLabel)))` and
passes), so this is confined to the no-evidence fallback.

Fix: give the fallback a real label — `readResult.ScannedPdfPages[0].SourceLabel`, or the read
result's own source label — rather than the issuer row's empty one. Because the assertion aborts
the loop, the remaining `RecordedFindings` pairs are unverified until this is green; please re-run
lane 3 rather than assume the rest pass.

### C05-R-11 (major) — a scan-only source records nothing in production, so its page rows and its OCR finding are still computed and discarded

`ThirdPartyReportAnalysis.IsRecordable(selection)` is `selection.Matches.Count > 0`, and
`ThirdPartyReportProfiles.Select` returns `NotApplicable(TextUnavailableRequiresOcr)` with
`matches: []` for a source with no readable pages. So at the only production call site
(`ProcessIntake.cs:337-342`) a scan-only PDF tags `no_report_signature` and returns before
`RecordAsync` — and John R Bell's per-page `source.page.requires-human-verification` rows, its
`source-requires-ocr` finding and its `page-requires-human-verification` finding never reach
storage, a screen or a query.

This is the same defect class as C05-R-1 — computed, then discarded at the production boundary —
for the rows the plan's John R Bell family case is written around, and both this round's new test
name (`EveryRecordedFindingIsPersistedAsItsOwnSourceRow`) and my previous attestation ("the
scan-page rows do persist") assert a persistence that does not happen. The corpus suite inspects
`ThirdPartyReportExtractionResult.Candidates`, never the store, so it cannot see the gap; the web
suite only uploads a Montgomery report.

Resolve it one of two honest ways, both inside C05's files:

1. Make a source with `ScannedPdfPages` recordable even with no signature — that is the case the
   "writing an empty analysis for every unrelated attachment would bury the ones that matter"
   comment does not cover, because a scan-only page is a positive statement that a person must
   read the original; or
2. Scope the claim: record it as an ASSUMPTION with the C-F02 handoff, rename the test so it says
   what it proves about the extraction result, and correct the family-case wording in
   `c05-report.md` and in this attestation's predecessor.

### C05-R-12 (minor) — two identical findings would collide on one candidate id and lose the whole analysis

`DeterministicId` keys on sha+occurrence+field+partyRole+referenceRole+page+raw+disposition and
carries no per-finding ordinal, while `FindingRows` emits one row per finding with no dedupe. Two
findings sharing a code, a resolved reference role, a page, a message and a disposition therefore
produce the same `Guid`, and `EfRetainedInstructionAnalysisStore.RecordAsync` `Add`s both into one
`SaveChangesAsync` — a duplicate-key failure that is caught by the recoverable catch and drops
**every** candidate for that source, not just the duplicate. No corpus original triggers it today
(`Conflicts` groups uniquely and every other rule yields at most one finding per role), so this is
latent, and the same-message-different-party-role case is the nearest live path. Cheapest guard:
fold the finding's index into the id key, or dedupe finding rows by id before returning.

### C05-R-13 (note) — the report's rule count is wrong

`c05-report.md` says "30 rules now carry the printed label in raw value". The file carries 37
`RawWholeMatch: true` rules — 36 added this round plus the pre-existing `valuation.adjustment`.
The substance is right and better than claimed; the number is not.

### C05-R-14 (note) — a UTF-8 BOM was added to all seven files

Every file in the diff gained `EF BB BF` on its first line, an encoding change unrelated to the
corrections, which makes each file's first line show as modified. The repo is already mixed
(`IntakeContracts.cs` has a BOM, `ThirdPartyReportContracts.cs` and `ImageIntakeAutomation.cs` do
not) and there is no `.editorconfig` charset rule, so this violates nothing — but it is avoidable
diff noise and worth not repeating.

### C05-R-15 (note) — the untagged early return

`RecordThirdPartyReportSourceAsync` returns untagged when the store is not composed or
`readResult.Status != IntakeSourceReadStatus.Readable`. The first is right (the feature is not
registered). The second means a source the *reader* failed on is still indistinguishable from one
that carries no report, which is a narrower version of what C05-R-6 asked to close. Not blocking;
intake's own read telemetry covers the reader failure.

## Test evidence (wave 19, lanes 1-5, all present)

| lane | result | detail |
| --- | --- | --- |
| 1-build | PASS | exit 0, 0 warnings. Attempt 1 failed MSB3027/MSB3021 on a stale node-reuse lock (pid 12044), resolved with `dotnet build-server shutdown`; kept for the record, no source or git change. |
| 2-core | PASS | 27 passed, 0 skipped, 0 failed (24 → 27: three cases added, one replaced). Pack-gated facts ran. |
| 3-corpus | **FAIL** | 10 passed, **1 failed**, 0 skipped. `EveryRecordedFindingIsPersistedAsItsOwnSourceRow` at line 325 — C05-R-10. |
| 4-web | **FAIL** | 9 passed, **1 failed**, 5 skipped. `ReprocessingTheSameRetainedBytesDoesNotWriteASecondSetOfCandidates` at line 313 — C05-R-6. The five skips are the known `MultiFormatGenuineCorpusWebTests` absent-pinned-sample skips and are not attributed to C05. |
| 5-architecture | PASS | 100 passed, 0 skipped — the new telemetry shape on `ProcessIntake` breaks no rule. |

The two red lanes are the reason for the verdict. Lane 4's skip list contains no
`ThirdPartyReportProvenanceWebTests` entry, so all three of C05's web cases ran and only the
reprocess one failed.

## Residual risk accepted

The "Finding" chip is deferred to C04/C08 with a written handoff (ASSUMPTION 7) and the finding
still renders legibly and provably in the meantime. The `valuation.adjustment` contract change
remains requested-not-applied (ASSUMPTION 3). Every candidate id changed relative to the previous
head, which is a version boundary rather than a defect and is declared in the report.

---

# SUPERSEDING ATTESTATION — C05 correction round 2

```yaml
verdict: needs-changes
ticket: INTK-060
slice: C05 — extract third-party reports as source evidence
head: 868e7a5ea
review_head: 868e7a5ea (reviewed in the worktree at merged head b506c3b8d)
supersedes: 7b632169b (needs-changes, C05-R-6 half / R-10 / R-11 / R-12 open)
base_of_correction: 7b632169b
diff_reviewed: git diff 7b632169b..868e7a5ea (6 files, +282/-43, 2 commits)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c05
branch: c05-third-party
ownership: PASS
frozen_contracts: PASS (ThirdPartyReportContracts.cs blob d024366c8 at 975bf107b, 7b632169b and 868e7a5ea)
stop_conditions: none tripped
lanes_seen: 1-build PASS, 2-core PASS, 3-corpus FAIL, 4-web FAIL-WITH-SKIPS, 5-architecture PASS
majors_open: 2
minors_open: 2
notes_open: 4
independent: true
review_round: 2
test_evidence: wave 22, C:/Users/PGUSER/AppData/Local/Temp/claude/C--Users-PGUSER-documents-github-pegasus/e752479c-0f90-4a5e-bc40-b525ea3bf932/scratchpad/wave1/wave22-tests/
skill_sha256:
  - file: C:/Users/PGUSER/documents/github/pegasus/.agents/skills/kanmer-review/SKILL.md
    sha256: addf26c9981cefa755a9db3a1ee06383432230708641b076ee336d64a1096741
```

## Verdict

**needs-changes** at `868e7a5ea`. Three of the four open findings are genuinely fixed —
C05-R-10 (every finding row names a source), C05-R-11 (a scan-only source reaches storage,
a readable non-report still records nothing) and C05-R-12 (the finding ordinal in the derived
id) — and each is proved by a case that ran, not by the report's description. But two of the
five lanes are still **red at this head**, and both reds are in this round's own new code:

- Lane 3 fails the **new** `AScanOnlyOriginalIsRecordedRatherThanDiscardedAtTheGate`, and what
  it catches is real: now that a scan-only source is recordable, its `identity.issuer` row is
  persisted with an **empty source label** (C05-R-16, major). The finding rows are fixed; the
  issuer row beside them is not.
- Lane 4 still fails `ReprocessingTheSameRetainedBytesDoesNotWriteASecondSetOfCandidates` —
  now on `Assert.Equal(2, recorded.Count)`, **Actual: 1**. Only one of the two passes tagged a
  third-party outcome for this receipt, so C05-R-6's open half is **not closed**: the claim
  that the re-evaluation re-reads the retained bytes and reaches `recorded_reading_stands`
  remains unproven, and the implementer's round-2 report states it as fact.

Nothing was weakened. No assertion was deleted or relaxed anywhere in the diff; two corpus
`Assert.False` calls gained failure messages, the web case gained a receipt filter and a
`Assert.Equal(2, …)` count it did not have, and the suites grew 27→29 (core) and 11→13 (corpus).

## Ownership, frozen contracts, scope — PASS

`git diff --name-status 7b632169b..868e7a5ea` touches exactly six paths, every one in
`C-intake.md` "### C05 files":

- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportExtraction.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportProfiles.cs`
- `tests/Pegasus.Core.Tests/Intake/ThirdPartyReports/ThirdPartyReportExtractionTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportCorpusTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportProvenanceWebTests.cs`

`ThirdPartyReportContracts.cs` is the identical blob `d024366c8` at `975bf107b`, `7b632169b`
and `868e7a5ea` — untouched, as required. No `DependencyInjection.cs`, no migration, no entity,
no `PegasusDbContext`, no `Details.cshtml`, no `OperatorLabels.cs`, no
`MultiFormatGenuineCorpusWebTests.cs`. `ThirdPartyReportValidation.cs` was not needed this
round and was not touched. A BOM grep over the diff returns **0** — C05-R-14 was not repeated.
Worktree is clean (`git status --porcelain` empty) and the worktree's `--git-common-dir`
resolves to the primary `C:/Users/PGUSER/Documents/github/pegasus/.git`.

## Prior findings — dispositions

| id | severity | disposition | evidence |
| --- | --- | --- | --- |
| C05-R-6 | major (raised from minor) | **still open** | Lane 4 red on `Assert.Equal(2, recorded.Count)`, Actual **1**. See below. |
| C05-R-10 | major | **fixed** | `FindingRows` now resolves its locator through `Locator(finding, issuer, rows)` — the first *evidence* row that names a source, then the issuer if it names one, then the first row of the document that does. Lane 3's `EveryRecordedFindingIsPersistedAsItsOwnSourceRow` (the case that failed at line 325 last round) **passed**, so all eleven `RecordedFindings` (file, code) pairs are now verified rather than merely consistent. The new corpus case's `Assert.All` reaches the finding rows of both scan-only originals without failing on one — the single failing row is `identity.issuer`, not a finding. |
| C05-R-11 | major | **fixed** | `IsRecordable` now takes the whole `ThirdPartyReportExtractionResult` and is `Selection.Matches.Count > 0 \|\| Findings.Count > 0`; `ProcessIntake.cs:338` passes `extraction`. Both halves are proved: `Assert.True(IsRecordable(result))` at `ThirdPartyReportCorpusTests.cs:442` **passed** for `JohnRBell1.pdf` and `TonBridgeAccidentRepair1.pdf` on the real PDFs, and `Assert.Contains(recorded, IsFinding)` at `:450` passed, so the page rows and both OCR findings do now reach `ToCandidates`. The negative half is `AReadableDocumentThatIsNoReportAndSaysNothingAboutItselfIsNotRecorded` (core lane green): no match, no finding, `IsRecordable` false. I checked the widening is genuinely narrow — with `Candidate is null`, `ThirdPartyReportValidation.Check` can raise only `source-requires-ocr`, `page-requires-human-verification` and `document-signature-ambiguous`, and the last already implies `Matches.Count > 1`. So the only newly recordable class is a source with scan-only pages, which is exactly the intent. |
| C05-R-12 | minor | **fixed** | `ThirdPartySourceCandidates.Create` takes an `ordinal` (0 for every printed row, `ordinal + 1` for a finding) and `DeterministicId` folds it into the key. Proved twice: `TwoFindingsThatStateTheSameSentenceDoNotShareAnIdentifier` (core, green) asserts both distinctness and reproducibility, and the new `NoTwoRecordedRowsOfOneOriginalShareAnIdentifier` (corpus, green) proves no two rows of any of the 29 originals collide and no id is `Guid.Empty`. `ReadingTheWholeCorpusTwiceProducesTheIdenticalRecord` still passes, so the ordinal did not break replay determinism. A related latent bug was fixed in passing: `FindingRows` was a lazy `IEnumerable` being `AddRange`d into the same `List` it now reads, and it is eager at this head. |
| C05-R-13 | note | **accepted** | The report's round-1 rule count is corrected in "Correction round 2". |
| C05-R-14 | note | **not repeated** | Verified: zero BOM bytes in the round-2 diff. |
| C05-R-15 | note | **not taken — and now material** | The untagged early return is one of the two live candidate explanations for lane 4's missing second outcome. See C05-R-6 below. |

### C05-R-6 (major, still open) — the re-evaluation pass is still silent for this receipt

Lane 4, `ThirdPartyReportProvenanceWebTests.cs:338`:

```
Assert.Equal() Failure: Values differ
Expected: 2
Actual:   1
```

The listener is now correctly keyed by `intake.receipt_id` (a `Guid` tag set by
`ProcessIntake.RecordTelemetry`, `ProcessIntake.cs:1179`, on the same activity and after the
outcome tag, so ordering is fine), and the process-global-listener contamination C05-R-6 named
is genuinely closed. What the count now shows is that **exactly one** of the two passes tagged
a third-party outcome for this receipt, so the corrected test proves less than the report
claims, not more. The test aborts before `Assert.Contains("recorded_reading_stands")`, so the
log does not say **which** pass was silent — that is itself a gap in the diagnostic.

The `Assert.Equal(1, await dispatcher.ExecuteAsync(1, …))` above it passed, so a work item was
dispatched and the immediate enqueuer ran `ProcessQueuedIntake` — the pass ran and still tagged
nothing. Three reachable ways for that to happen:

1. `RecordThirdPartyReportSourceAsync` returns **untagged** when
   `retainedInstructionAnalysisStore is null`, when `readResult.Status != Readable`, or when
   `IntakeFileIdentity.SourceAsset(receipt) is null` (`ProcessIntake.cs:315-325`). That is
   C05-R-15, and on the re-evaluation path any of the three would produce exactly this result.
2. `ProcessQueuedIntake.ExecuteAsync` has a `claimed is null` branch (`DurableIntake.cs:584-…`)
   that replays association and allocation from the completed evaluation and never calls
   `ProcessIntake` at all.
3. The queued pass tagged a **different** receipt id, in which case the analysis was written
   under a receipt the `first`/`second` query never looks at.

Please add the diagnostic before the fix: assert the observed outcome **sequence** rather than
only its count (or put `recorded` and the unfiltered queue into the failure message), and tag
the three early returns at (1). Then the next round has a fact instead of three hypotheses.

**On the mechanism, which I did verify by reading:** the `recorded_reading_stands` branch *is*
honest where it is reached. `EfRetainedInstructionAnalysisStore.RecordAsync` opens a
serializable transaction, probes the (receipt, asset, key) triple, and throws
`RetainedInstructionAnalysisConflictException` **before** any `Add` and before
`SaveChangesAsync`; the transaction is disposed without a commit. So the conflict path writes
no second candidate set and no partial row — this is not a swallowed conflict that lost data,
and the reader demonstrably ran (the exception is only reachable after `Extract` produced
candidates). Two caveats are recorded as C05-R-18 and C05-R-19 below. Note too that the test's
`first`/`second` comparison is filtered to the *first* asset id, so on its own it cannot see a
second set written under a new asset — the outcome assertion is what closes that, which is
another reason it has to actually pass rather than be narrowed away.

## New findings

### C05-R-16 (major) — the `identity.issuer` row of a scan-only source is persisted naming no source; lane 3 is red

```
Assert.All() Failure: 1 out of 6 items in the collection did not pass.
[0]: RetainedInstructionCandidate { Field = identity.issuer, …, SourceLabel = , Page = ,
     Disposition = Missing }
     Error: JohnRBell1.pdf: a identity.issuer row names no source.
ThirdPartyReportCorpusTests.cs:453   —   Failed: 1, Passed: 12, Skipped: 0
```

C05-R-10 was fixed one row short. `ThirdPartyReportProfiles.Verdict` builds the issuer row with
`sourceLabel: evidence?.SourceLabel ?? string.Empty` (`ThirdPartyReportProfiles.cs:398-407`),
and a source with no readable page matches no signature, so `evidence` is null and the label is
`""`. Until this round that row never reached storage, because `IsRecordable` discarded the
whole reading; **the C05-R-11 fix is what makes it persist**. So the round-2 corrections turned
a computed-and-discarded blank row into a stored one, and the implementer's own new test is the
thing that caught it. That is the right test doing its job — but it is red, and the row it
names is a persisted provenance record that names no part of the document it is about, which is
the invariant this slice exists to hold.

Fix, all inside `ThirdPartyReportProfiles.cs` / `ThirdPartyReportExtraction.cs`: give `Verdict`
a real fallback label for the no-evidence case (the read result's own source label, or
`readResult.ScannedPdfPages[0].SourceLabel`), rather than relaxing the assertion. Note that the
issuer row is built inside `Select` before the scanned page rows exist, so the label has to come
from the read result rather than from `rows`.

### C05-R-17 (minor) — the "every finding names a source" guarantee rests on an unasserted reader invariant

`Locator(...)` ends `?? issuer`, so it can still return a blank-label row when a reading has
`RequiresOcr: true`, **no** `ScannedPdfPages`, and an issuer with no signature evidence. That
combination is unreachable only because `MimeKitPdfPigOpenXmlIntakeSourceReader` sets
`RequiresOcr` as `OcrCandidates.Count > 0` (line 1093) and `ProviderApiIntakeSourceReader` sets
it `false` — i.e. the guarantee is held by a producer invariant that no test states. A
hand-built `IntakeSourceReadResult` (a future reader, or a test) breaks it silently. Cheapest
guard: one core case that constructs exactly that read result and asserts the finding row still
names a source, or make the last fallback a label rather than a row.

### C05-R-18 (minor) — `recorded_reading_stands` conflates three different conflict causes

`RetainedInstructionAnalysisConflictException` carries no reason, and
`IRetainedInstructionAnalysisStore.RecordAsync`'s own contract says it is raised for a row found
under the key "for a DIFFERENT receipt, asset **or** expected receipt version".
`EfRetainedInstructionAnalysisStore` has two throw sites: the version mismatch on the matching
triple, and `AnyAsync(item => item.OperationKey == key)` for a key already bound elsewhere.
`ProcessIntake` catches both and tags the same `recorded_reading_stands` — which would be a
**false** statement in the second case, where nothing was recorded for this receipt at all. It
is not reachable today (the key embeds `asset.Id`, so a cross-binding needs one asset id on two
receipts), but the tag exists precisely to make outcomes distinguishable, and this one merges an
honest replay with a corruption signal. Cheapest guard: give the exception a reason, or probe
`FindByOperationKeyAsync` in the catch and tag `recorded_reading_stands` only when the stored
row names this receipt and asset.

### C05-R-19 (note) — the `"replayed"` branch is dead on the path the comment describes

The operation-key comment at `ProcessIntake.cs:352-354` says the key is "derived from the asset,
so re-processing the same retained bytes replays the record instead of writing a second set of
candidates" — but a re-evaluation always moves `receipt.Version`, so `RecordAsync` can never
return `IsReplay: true` on that path; the *conflict* is the normal control flow and
`isReplay ? "replayed" : "recorded"` can only ever say `"recorded"` there. Using an exception as
the ordinary re-evaluation outcome is worth a sentence in the comment so the next reader does
not go looking for the replay that never happens.

### C05-R-20 (note) — finding ids are position-dependent, so inserting a rule renumbers later findings

The ordinal is the finding's index in the raised order, so adding a finding rule *before* an
existing one changes the derived id of every finding after it for every affected source. That is
the same version boundary the report already declares for this head and nothing reads an id
across runs, so it is acceptable — but it is a second, quieter id-churn trigger than the
raw-value one, and the `DeterministicId` comment should say so.

### Note on finding numbering

The dispatch asked for new findings "from C05-R-13". `C05-R-13`, `C05-R-14` and `C05-R-15` are
already bound to the round-1 notes in this document, and reusing them would make the ids
ambiguous across rounds. This round's new findings therefore start at **C05-R-16**, so every id
in this attestation still names exactly one finding.

## Test evidence (wave 22, lanes 1-5, all present)

| lane | result | detail |
| --- | --- | --- |
| 1-build | PASS | exit 0, `Build succeeded. 0 Warning(s), 0 Error(s)`, 20.08 s. |
| 2-core | PASS | 29 passed, 0 failed, 0 skipped (27 → 29: two cases added, one extended). Pack-gated facts ran. |
| 3-corpus | **FAIL** | 12 passed, **1 failed**, 0 skipped (11 → 13). `AScanOnlyOriginalIsRecordedRatherThanDiscardedAtTheGate` at line 453 — C05-R-16. `EveryRecordedFindingIsPersistedAsItsOwnSourceRow`, `NoTwoRecordedRowsOfOneOriginalShareAnIdentifier` and `ReadingTheWholeCorpusTwiceProducesTheIdenticalRecord` all passed. |
| 4-web | **FAIL** | 9 passed, **1 failed**, 5 skipped. `ReprocessingTheSameRetainedBytesDoesNotWriteASecondSetOfCandidates` at line 338 — C05-R-6. The five skips are the known absent-pinned-sample `MultiFormatGenuineCorpusWebTests` skips and are not attributed to C05; the skip list contains no `ThirdPartyReportProvenanceWebTests` entry, so all three C05 web cases ran. |
| 5-architecture | PASS | 100 passed, 0 skipped. |

Two red lanes is the verdict. `pass` requires all five green with only the known web skips.

## What round 3 needs

1. **C05-R-16** — give the no-evidence issuer row a real source label (`ThirdPartyReportProfiles.Verdict`).
2. **C05-R-6** — make the re-evaluation pass observable: tag the three untagged early returns
   (C05-R-15), assert the observed outcome sequence rather than only its count, and then either
   show the pass reaches the reader or rename the case to what it actually proves.
3. C05-R-17, C05-R-18 as cheap guards; C05-R-19, C05-R-20 are comment-only.

Nothing here needs an A-owned file, a contract change, or a redesign.

---

# SUPERSEDING ATTESTATION — C05 correction round 3

```yaml
verdict: pass
ticket: INTK-060
slice: C05 — extract third-party reports as source evidence
head: eb46b7a7d
review_head: eb46b7a7d (reviewed in the worktree at merged head 7467190b1)
supersedes: 868e7a5ea (needs-changes, C05-R-16 / C05-R-6 open majors)
base_of_correction: b506c3b8d
diff_reviewed: git diff b506c3b8d..eb46b7a7d (6 files, +302/-49, 3 commits)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c05
branch: c05-third-party
reviewer: pegasus-reviewer (subagent; not the implementer of this slice)
independent: true
ownership: PASS
frozen_contracts: PASS (ThirdPartyReportContracts.cs blob d024366c8 at b506c3b8d and eb46b7a7d)
stop_conditions: none tripped
lanes_seen: 1-build PASS, 2-core PASS, 3-corpus PASS, 4-web PASS-WITH-KNOWN-SKIPS, 5-architecture PASS
majors_open: 0
minors_open: 3
notes_open: 2
review_round: 3
test_evidence: wave 28, C:/Users/PGUSER/AppData/Local/Temp/claude/C--Users-PGUSER-documents-github-pegasus/e752479c-0f90-4a5e-bc40-b525ea3bf932/scratchpad/wave1/wave28-tests/
skill_sha256:
  - file: C:/Users/PGUSER/documents/github/pegasus/.agents/skills/kanmer-review/SKILL.md
    sha256: addf26c9981cefa755a9db3a1ee06383432230708641b076ee336d64a1096741
```

## Verdict

**pass** at `eb46b7a7d`. Both open majors are closed, all five lanes are green, and the two cases
that were red at `868e7a5ea` are the two that now prove the fixes:

- **C05-R-16** is fixed in the way the last round asked for — a real document-level locator on the
  no-evidence issuer row — and not by relaxing the assertion. Lane 3's
  `AScanOnlyOriginalIsRecordedRatherThanDiscardedAtTheGate`, the case that failed at line 453 last
  round, passes and now additionally pins the row's label, its `Missing` disposition and its absent
  value on both scan-only originals. I checked the file name is a locator and never evidence:
  `context.SourceLabel` has exactly one read site in the tree, it is a `sourceLabel:` argument, and
  it is absent from `DeterministicId`'s key.
- **C05-R-6** is closed the right way. The implementer did not narrow the case to make it green; it
  root-caused the missing second outcome to a real, pre-existing, A-owned product defect, and every
  step of that chain checks out against source I read myself. The test now asserts what actually
  happens — including the durable work item's exact failure code, which is a tripwire that goes red
  when A fixes the path — and the replay-guard mechanism the old case only claimed is proved
  separately against the real store and SQL Server.

Three minors and two notes are open. None of them is a data, security or correctness risk: two are
tests that do not state a guarantee the code does hold (C05-R-21, C05-R-22) and one is a one-line
defensive guard on the input the C05-R-16 fix consumes (C05-R-23). They are recorded as residual
risk rather than a fourth round.

## Ownership, frozen contracts, scope — PASS

`git diff --name-status b506c3b8d..eb46b7a7d` touches exactly six paths, every one of them in
`pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md` "### C05 files":

- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportExtraction.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportProfiles.cs`
- `tests/Pegasus.Core.Tests/Intake/ThirdPartyReports/ThirdPartyReportExtractionTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportCorpusTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportProvenanceWebTests.cs`

`ThirdPartyReportContracts.cs` is the identical blob `d024366c8` at `b506c3b8d` and `eb46b7a7d` —
untouched, as required. No `DurableIntake.cs`, no `EfIntakeWorkStore.cs`, no `ProcessIntakeTests.cs`,
no `DependencyInjection.cs`, no migration, no entity, no `Details.cshtml`, no `OperatorLabels.cs`,
no `MultiFormatGenuineCorpusWebTests.cs` — and the A-owned defect this round root-caused sits in
exactly the first two of those, which were correctly left alone. The round-3 diff contains zero BOM
bytes (C05-R-14 not repeated). Worktree clean (`git status --porcelain` empty), `--git-common-dir`
resolves to the primary `C:/Users/PGUSER/Documents/github/pegasus/.git`, `branch --show-current`
is `c05-third-party`.

**Nothing was weakened.** The only assertions removed anywhere in the diff are the four inside the
one renamed web case — `Assert.Equal(2, recorded.Count)`, `Assert.Contains("recorded", …)`,
`Assert.Contains("recorded_reading_stands", …)` and `Assert.DoesNotContain("not_recorded", …)` —
replaced by `Assert.Equal("recorded", Assert.Single(recorded))`, which is strictly stronger than
all four, plus three assertions the case did not have before (`Assert.NotEmpty(first)`, the durable
work item's `Failed` status, and its exact `FailureCode`). Case counts: core file 25 → 26, corpus
file 13 → 13 with three assertions added, web file 3 → 4. No case deleted; two added.

## Prior findings — dispositions

| id | severity | disposition | evidence |
| --- | --- | --- | --- |
| C05-R-16 | major | **fixed** | Verified by reading (below) and by lane 3, where `AScanOnlyOriginalIsRecordedRatherThanDiscardedAtTheGate` was the red case at `868e7a5ea` and is green here. |
| C05-R-6 | major | **closed — root-caused to an A-owned product defect** | Attribution verified from source, not from the report. ASSUMPTION 8 and PR 673 comment `5560823100` both exist and are accurate. Detail below. |
| C05-R-17 | minor | **fixed** | `AReadingWithNoPageAtAllStillNamesTheSourceOnEveryRowItRecords` (core) builds exactly the reading C05-R-17 named — `Readable`, no pages, no scan-only pages, `RequiresOcr: true` — and asserts `Assert.All(result.Candidates, row => Assert.Equal("uploaded report.pdf", row.SourceLabel))`, every row rather than only the finding row. It also asserts `IsRecordable(result)`, so the rows it checks are the ones that reach storage. The producer invariant is now a stated case. |
| C05-R-18 | minor | **fixed in code, unproven by test** | The catch now calls `ConflictOutcomeAsync`, which reads the row back via `FindByOperationKeyAsync` and returns `recorded_reading_stands` only when `stored.ReceiptId == receiptId && stored.IntakeAssetId == assetId`, else `analysis_key_bound_elsewhere`, else `recorded_reading_unverified` on a recoverable probe fault. I checked the mapping against the store's two throw sites: `EfRetainedInstructionAnalysisStore.RecordAsync` throws first on the matching `(receipt, asset, key)` triple with a moved `ExpectedReceiptVersion` — where the stored row *does* name this receipt and asset — and second on `AnyAsync(item => item.OperationKey == key)`, where by construction it does not. The mapping is right. The probe takes its own `DbContext` from the factory, so the failed serializable transaction cannot affect it. Residual: C05-R-22. |
| C05-R-13 | note | **accepted** | Corrected in the round-2 report section; no further change needed. |
| C05-R-14 | note | **not repeated** | Zero BOM bytes in the round-3 diff; encodings and CRLF endings preserved. |
| C05-R-15 | note | **taken** | The three early returns are now tagged `not_composed`, `source_not_readable` and `no_single_source_asset`, so no path through `RecordThirdPartyReportSourceAsync` is silent. |
| C05-R-19 | note | **fixed** | The operation-key comment now says the conflict, not the replay, is the ordinary outcome of a second pass over one asset. |
| C05-R-20 | note | **fixed** | The `DeterministicId` comment records the ordinal's renumbering behaviour and why it is the same version boundary a changed raw value crosses. |

### C05-R-16 — fixed, and the file name really is a locator

`ThirdPartyReportSourceContext` gained a trailing optional `string SourceLabel = ""`;
`ProcessIntake.cs:359` supplies `asset.FileName`; `ThirdPartyReportProfiles.Verdict` now uses
`evidence?.SourceLabel is { Length: > 0 } label ? label : context.SourceLabel`.

**Is the file name a locator and never evidence?** Yes, and structurally rather than by intent.
`context.SourceLabel` has exactly **one** read site in the whole tree
(`ThirdPartyReportProfiles.cs:430`), and it is the `sourceLabel:` argument of the issuer row. It is
never passed to `CompiledSignature.Matches`, `Describe`, `Compile`, any `ThirdPartyRegex`, any
field rule, `rawValue` or `normalizedValue`. The corpus case asserts the row it lands on is
`Missing` with a null `NormalizedValue` — a locator on a row that states no issuer at all.
`TheIssuerIsNeverTakenFromTheFileNameOrTheRetainedPrincipal` is untouched and green in lane 2.
It is also **not** in the derived id: `DeterministicId`'s key is
`(Sha256, Occurrence, field, partyRole, referenceRole, page, rawValue, disposition, ordinal)` with
no `sourceLabel`, so the report's claim that candidate ids are unchanged this round is true.
The new parameter is trailing and optional, and the record has only three construction sites — all
three in C05 files — so nothing outside the slice is source-affected.

**Is any Missing row left unlabelled?** I enumerated every `ThirdPartySourceCandidates.Create`
call site and the reachability of an empty `sourceLabel` at each:

- issuer row (`Profiles.cs:428`) — was the defect; now falls back to `context.SourceLabel`.
- scan-only page rows (`Extraction.cs:880`) — `$"{page.SourceLabel}, page {n}"`, never blank.
- finding rows (`Extraction.cs:783`) — `Locator(...)`, whose final `?? issuer` now carries the
  document-level locator; this is exactly what C05-R-17's new case states.
- declared-but-unobserved `Missing` field rows (`Extraction.cs:972`) and `Describe`'s cross-page
  fallback (`Profiles.cs:479`) — both `pages.Count > 0 ? pages[0].SourceLabel : string.Empty`.
  The empty arm is unreachable: both sit on paths that require a matched signature, and a signature
  matches only against text flattened from readable pages, so `pages` cannot be empty there.
- `Media` photograph rows (`Extraction.cs:897`) — `asset.SourceLabel`, `Usable` rather than
  `Missing`, pre-existing and unchanged, and unreachable alone because `IsRecordable` needs a match
  or a finding.

So every reachable production path now names its source. The one residual dependency is that
`context.SourceLabel` itself be non-empty — C05-R-23.

### C05-R-6 — the root cause is correct, the attribution is correct, nothing is fabricated

I verified every load-bearing claim from source rather than from the report:

1. `ProcessQueuedIntake.ExecuteAsync`'s first stage `artifact_read_and_retain` does
   `artifactStore.ReadAsync(stagedReceipt.StorageKey, ct) ?? throw new IntakeArtifactIntegrityException()`
   (`DurableIntake.cs:680-681`).
2. A successful first pass calls `TryDeleteCompletedStagingAsync(stagedReceipt.StorageKey, ct)`
   (`DurableIntake.cs:759`), which marks the staged artifact `Completed` and calls
   `artifactStore.DeleteCompletedStagedAsync` (`:1032`). Both the file-system and the Azure Blob
   store implement that delete, so this is core behaviour rather than a harness artefact.
3. `EfIntakeWorkStore.ScheduleReevaluationAsync` sets `item.State = pending` for **any** work item,
   guarding only against one already `processing` under a live lease (`EfIntakeWorkStore.cs:502-525`).
   A `completed` item is therefore made claimable again.
4. `IIntakeWorkStore.FindStagedReceiptIdForReceiptAsync`'s own doc comment states the invariant
   being broken, verbatim: a completed work item must never be made claimable again, because that
   "would force a re-claim through the artifact-reading path, whose staged copy is already deleted
   once a receipt has completed once" — and it says it "Mirrors the join
   `EfIntakeMutationStore.ScheduleReevaluationAsync` performs inline for the staff-facing
   reevaluation command" (`DurableIntake.cs:249-257`).
5. `TerminalInputFailureCode(IntakeArtifactIntegrityException) => "staged_artifact_integrity_failure"`
   (`DurableIntake.cs:1071`), reached by the `catch (…) when (TerminalInputFailureCode(exception) is { } failureCode)`
   at `:727`, which calls `FailProcessingAsync(terminal: true, …)` and returns
   `QueuedIntakeProcessingOutcome.Failed`. So `Failed` plus that exact code is the predicted durable
   state, not a guess.

The chain is exactly as the implementer described. The defect is real, pre-existing, and lives in
`DurableIntake.cs` and `EfIntakeWorkStore.cs`, neither of which is a C05 file. The attribution is
correct.

**Is the deviation from the dispatch's `Equal(2, …)` honest?** Yes. The dispatch asked for a
sequence that cannot occur: no path in this codebase runs `ProcessIntake` twice over one asset, so
`Assert.Equal(2, recorded.Count)` could only ever have been made green by faking the second pass.
Asserting it would have been the dishonest option. ASSUMPTION 8 is recorded on `scratch/c05-notes`
in the required form with the decision, the reason and three named-and-rejected alternatives; M8 is
satisfied and no second dependent decision was taken on top of it.

**Does the new case really prove the reading is left standing under the only re-evaluation path
that exists?** Yes, and it proves it the honest way.
`AQueuedReevaluationLeavesTheRecordedReadingExactlyAsItWas` drives the real `IReevaluateIntake`
command and a real `DispatchPendingIntakeWork` over `IntakeWebDriver.CreateProcessor(services)`,
asserts the dispatcher claimed exactly one item, then asserts `first.Count == second.Count` and the
ordered id sets are equal, that the durable work item is `Failed` with
`FailureCode == "staged_artifact_integrity_failure"`, and
`Assert.Equal("recorded", Assert.Single(recorded))` over the receipt-keyed outcome list. What it
proves is narrower than the old name claimed and exactly as wide as the new name claims: the
recorded reading is undisturbed, and the reason is read from the durable record rather than
inferred. The failure-code assertion is a deliberate tripwire that goes red the moment A's fix makes
re-evaluation re-read; both the report and the PR comment say so.

**Is the replay guard proved separately?** Yes, at the boundary that enforces it.
`RecordingTheSameReadingAgainReplaysItAndAMovedVersionIsRefused` runs against the real
`IRetainedInstructionAnalysisStore` and SQL Server, on the rows a real report produced: the identical
request returns `IsReplay: true` with the stored id unchanged; the same request with
`ExpectedReceiptVersion + 1` throws `RetainedInstructionAnalysisConflictException`; and the candidate
id set is unchanged after both. That refusal is precisely the exception `ProcessIntake`'s catch
converts to `recorded_reading_stands`, and every re-evaluation moves the version, so it is the
branch a second pass would take. The one link it does not exercise is `ProcessIntake`'s catch
itself — C05-R-22.

**Is it acceptable for the slice with the A handoff posted?** Yes. I read PR 673 comment
`5560823100` (posted 2026-09-06T17:11:52Z by `collisionengineers`): it names the defect, the two
owning files, the exact failure code, cites `FindStagedReceiptIdForReceiptAsync`'s doc comment as
the codebase's own statement of the rule, and says what C did and what C will re-point when A fixes
it. Every claim in it matches the source I read. C05's own guarantee — a report reading reaches
storage, names its source on every row, and is never written twice or overwritten — is fully
delivered inside C05's files and proved by cases that ran. Nothing in the slice depends on the A
defect being fixed first.

## New findings (round 3)

### C05-R-21 (minor) — the "never from the file name" case does not exercise the new file-name channel

Round 3 introduced a genuinely new input into extraction: `ThirdPartyReportSourceContext.SourceLabel`,
carrying the uploaded file's own name. The case whose whole job is to hold that boundary,
`TheIssuerIsNeverTakenFromTheFileNameOrTheRetainedPrincipal`, plants its decoy issuer in the **page**
source label (`Readable(…, label: "uploaded MontgomeryRepairable1.pdf, page 1")`) and takes
`Context()`, which now supplies `SourceLabel: "uploaded report.pdf"` — a name with no issuer in it.
So the case does not put a misleading file name through the channel that was just added, and would
not catch a future change that fed `context.SourceLabel` into signature or field matching. The
invariant does hold today — I verified `context.SourceLabel` has exactly one read site and it is not
a matching input — but this is again a guarantee resting on something no test states, which is the
same shape as C05-R-17. Cheapest fix, entirely inside a C05 file: give that case a second `Extract`
whose context carries `SourceLabel: "MontgomeryRepairable1.pdf"` over the Connexus body and assert
the family is still Connexus, and over the non-report body assert it is still `NotApplicable`.

### C05-R-22 (minor) — `ConflictOutcomeAsync` and all six new outcome tags are unasserted

C05-R-18's fix and C05-R-15's three early-return tags are new production code with no test anywhere:
`not_composed`, `source_not_readable`, `no_single_source_asset`, `analysis_key_bound_elsewhere` and
`recorded_reading_unverified` appear only at their `SetTag` sites, and `recorded_reading_stands`
appears elsewhere only in a doc comment. The one tag any test asserts is `"recorded"`. The
`ConflictOutcomeAsync` branch selection is therefore verified by reading only — which I did, and it
is correct — but a change to `EfRetainedInstructionAnalysisStore`'s throw ordering would flip
`recorded_reading_stands` to `analysis_key_bound_elsewhere` silently, and that tag is what this
slice's telemetry story rests on. Note also that ASSUMPTION 8's rejected alternative (b) is slightly
overstated: the constraint is not visibility. `ExecuteRetainedAsync` is `internal` and
`Pegasus.Core.csproj` carries `<InternalsVisibleTo Include="Pegasus.Core.Tests" />`, so it is
reachable from `ThirdPartyReportExtractionTests.cs`, which **is** a C05 file in that assembly. What
actually makes such a case expensive is `ProcessIntake`'s dependency graph, not the file map. That
is a fair reason to defer, but it should be the stated one. Not blocking: the branch is correct as
written, and what it most affects is telemetry rather than data.

### C05-R-23 (minor) — the C05-R-16 fix rests on a `context.SourceLabel` nothing guarantees

`ThirdPartyReportSourceContext.SourceLabel` defaults to `""`, and when it is empty the scan-only
issuer row is unlabelled again and `Locator`'s final `?? issuer` returns a blank-label row for
findings too — C05-R-16 reappears exactly. Production supplies `asset.FileName`, which
`ProcessIntake` derives at `:88` as `Path.GetFileName(source.FileName)` **after** the
`ArgumentException.ThrowIfNullOrWhiteSpace(source.FileName)` at `:81`, and `Path.GetFileName`
returns `""` for a value ending in a separator — so the guard does not cover the value that is
actually stored and passed here. Low likelihood, and the consequence is a poor locator rather than
lost or wrong data, but the fix is one line inside a C05 file: validate the record's `SourceLabel`
non-empty where it is constructed, or make `Verdict`'s fallback resolve to something stable when the
label is blank.

### C05-R-24 (note) — the corpus-wide "names a source" assertion still covers only two originals

`Assert.All(recorded, … !IsNullOrWhiteSpace(row.SourceLabel))` runs over every recorded row of the
two scan-only originals (`ThirdPartyReportCorpusTests.cs:453-457`) and over the eleven recorded
finding rows (`:331-333`), but not over every recorded row of all 29 originals. The uncovered rows
are closed by construction — a signature-matched original necessarily has readable pages, so both
`pages[0].SourceLabel` fallbacks are non-empty — so this is not a gap in the guarantee, only in what
states it. Hoisting that one `Assert.All` into the existing whole-corpus loop would state it for
every original at no extra runtime.

### C05-R-25 (note) — a non-recoverable fault inside the conflict handler now escapes the method

`ConflictOutcomeAsync` is awaited from inside `catch (RetainedInstructionAnalysisConflictException)`,
so an exception it raises is not seen by the sibling
`catch (…) when (IntakeExceptionPolicy.IsRecoverable(exception))` that exists precisely to stop
supplementary report evidence failing an already-stored receipt. Before this round the conflict path
could not throw at all. In practice the escaping set is only `OperationCanceledException`,
`OutOfMemoryException` and `AccessViolationException` (`IntakeContracts.cs:592-595`), and letting
those propagate is the codebase's deliberate policy everywhere else — so this is recorded as an
observation rather than a defect.

## Test evidence (wave 28, lanes 1-5, all present, all green)

| lane | result | detail |
| --- | --- | --- |
| 1-build | PASS | exit 0, `Build succeeded. 0 Warning(s) 0 Error(s)`, 17.65 s. |
| 2-core | PASS | 30 passed, 0 failed, 0 skipped (29 → 30: `AReadingWithNoPageAtAllStillNamesTheSourceOnEveryRowItRecords` added for C05-R-17). Pack-gated facts ran. |
| 3-corpus | PASS | 13 passed, 0 failed, 0 skipped. `AScanOnlyOriginalIsRecordedRatherThanDiscardedAtTheGate` — red at line 453 last round, the C05-R-16 case — is green, as are `EveryRecordedFindingIsPersistedAsItsOwnSourceRow`, `NoTwoRecordedRowsOfOneOriginalShareAnIdentifier` and `ReadingTheWholeCorpusTwiceProducesTheIdenticalRecord`. |
| 4-web | PASS-WITH-KNOWN-SKIPS | 11 passed, 0 failed, 5 skipped (15 → 16 cases: the fourth C05 web case added). The five skips are exactly the known absent-pinned-sample `MultiFormatGenuineCorpusWebTests` skips; the skip list contains no `ThirdPartyReportProvenanceWebTests` entry, so all four C05 web cases ran — including both new ones, `AQueuedReevaluationLeavesTheRecordedReadingExactlyAsItWas` and `RecordingTheSameReadingAgainReplaysItAndAMovedVersionIsRefused`. |
| 5-architecture | PASS | 100 passed, 0 failed, 0 skipped. |

The web lane passing is what makes the C05-R-6 disposition evidence rather than argument: the
`Failed` / `staged_artifact_integrity_failure` assertion is a claim about the real durable path
under a real re-evaluation, and it held.

## Residual risk accepted

- C05-R-21, C05-R-22, C05-R-23 (minors) and C05-R-24, C05-R-25 (notes) above, dispositioned and not
  blocking.
- The A-owned defect itself: a staff re-evaluation of any completed receipt fails as
  `staged_artifact_integrity_failure` rather than re-reading the retained source. Pre-existing,
  outside C05's file map, handed off on PR 673 comment `5560823100`, and recorded as ASSUMPTION 8
  plus a HANDOFF line on `scratch/c05-notes`. C05 does not depend on it being fixed; when it is,
  `AQueuedReevaluationLeavesTheRecordedReadingExactlyAsItWas` goes red and names the change.
- The "Finding" chip deferred to C04/C08 (ASSUMPTION 7) and the `valuation.adjustment` contract
  change requested-not-applied (ASSUMPTION 3), both carried from earlier rounds and unchanged here.
- Candidate ids are unchanged relative to `b506c3b8d`: no raw value, disposition, page, role or
  ordinal moved, and `sourceLabel` is not part of the derived key. Verified by reading
  `DeterministicId`, and consistent with `ReadingTheWholeCorpusTwiceProducesTheIdenticalRecord`
  staying green.

## C05 seam review — 35cc17c66340ec51253bb4393853b9bcf0d0815b — PASS

kind: review-attestation | ticket: INTK-060 | slice: C05 third-party report reconstruction seam
reviewer: pegasus-reviewer (controller-dispatched slice review) | independent: true | verdict: **PASS**
head_sha: `35cc17c66340ec51253bb4393853b9bcf0d0815b` | branch: `c05-third-party` | base: `aa5e669d76ad2f7cc24783f8076644c439509feb`
worktree: `C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c05`
findings: 8 (0 blocker, 1 major, 4 minor, 3 info) | merged: false | no git writes, no file edits, no dotnet test run by the reviewer
full attestation: `C:/Users/PGUSER/AppData/Local/Temp/claude/C--Users-PGUSER-documents-github-pegasus/5adc2fb3-f15d-4145-84ed-948eb9fde4e4/scratchpad/takeover/c05-seam-review.md`
wave results bound: `.../scratchpad/takeover/wave36-tests/`

### Bound gate
Core build 0W/0E; Core.Tests build 0W/0E; filter `ThirdPartyReport` 36 pass / 0 fail / 2 skip (both `[ReferencePackFact]` corpus-gated); full Core.Tests 1562 pass / 1 fail / 14 skip. The single failure is `PrincipalIdentificationCorpusTests.TrackedPegasusSourceHashesHaveNotDrifted` (`tests/Pegasus.Core.Tests/ReferenceData/PrincipalIdentificationCorpusTests.cs:318`, expected `1f20a025…`, actual `c686ff96…`) — **confirmed cross-stream**: `git show --stat 35cc17c66` changes exactly two files (`src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportExtraction.cs`, `tests/Pegasus.Core.Tests/Intake/ThirdPartyReports/ThirdPartyReportExtractionTests.cs`; 427 insertions, 4 deletions), touching neither `QdosInstructionExtractionPolicy.cs` nor any corpus JSON. Matches A's acknowledgement on PR 673 comment 5563506283. Integration tests NOT RUN (A-owned `EfCaseArtifactCustody` standalone-compile block) — INCONCLUSIVE, not PASS.

### Stream A's request
All six clauses met. One `public static` entry on the existing owner; no new type, no new file, nothing under `src/Pegasus.Infrastructure/`, no migration, no column, no IO. Input shape corrected upward to `RetainedInstructionCandidate` (carries `PolicyKey` and `Locator`, which the `SourceFieldCandidate` projection drops) — A's existing private `EfRetainedInstructionAnalysisStore.Map(IntakeSourceCandidateEntity)` already produces it, verified at `EfRetainedInstructionAnalysisStore.cs:233-256`.

### Plan C05 invariants — all PASS
Issuer selected only by `row.Field == F.Issuer` over persisted rows (`ThirdPartyReportExtraction.cs:786`); no signature re-run, no folder/principal/label inference. No arithmetic anywhere in the new code; `Restore` (`:799`) copies `Disposition` verbatim and `Lookup.Usable` is unchanged, so Missing/Ambiguous/Conflicting come back in state with no chosen winner. Finding rows ride through and are excluded from the projection exactly as `Observe` excluded them. `Reconstruct` is a pure function — no command, no store, no Engineer value.

### Lenses
1. **Reuse — PASS.** `Project` (`:1150`), `Lookup`, `Estimates`, `HasValue`, `ObservedFields`, `FieldKey` unchanged; the only production edit is `Project`'s first parameter, and the old body's sole use of `selection` was `new Lookup(observed, selection.Issuer)`. The two new helpers are inverses, not duplicates: `Restore` (`:799`) inverts `ToCandidates` (`:1541`) — argument order checked member-by-member against `SourceFieldCandidate` (`IntakeContracts.cs:1125-1130`), including the `"" ⇄ null` role convention; `Observed` (`:838`) regroups into the shape `Observe` built, and the regrouping is faithful because `Extract:719` appends `observed.Order.SelectMany(...)` (keys contiguous, declared order) and every declared key persists at least a Missing placeholder (`:1108-1117`).
2. **Round trip — PASS, assertions read.** `[Theory]` at test `:847` over `ConnexusCosts`, `ExclusiveErehrHeader`, `LairdSupplement`, `MontgomeryCosts` — the four named. `Members`/`Walk` (`:1005`) is a genuine member-wise ordered `List<string>` comparison over the projection's property graph: each fact's value plus the whole row (field, both roles, raw, normalized, unit, currency, page, disposition, id, source label, region, cell, form field, reader/policy version, document role, sha256, occurrence, all four identity Guids), lists walked element-wise with counts. Non-vacuity guard present.
3. **Negatives — PASS.** Audatex non-report role and scan-only (`:867`); empty, instruction-profile-only and mixed sets (`:905`); both null guards (`:929`). Independently confirmed the null rule at source: `ThirdPartyReportProfiles.Verdict` writes a `Usable` issuer only on the `Selected` branch (`ThirdPartyReportProfiles.cs:364-373`), the same branch `Extract` requires — so "usable issuer ⟺ extraction produced a candidate" holds by construction.
4. **Extraction behaviour — PASS.** Locator change traced through the real path: `RecordAsync:135` → `LocatorJson(SourceLabel, Page, Locator)` → `Map`/`GetAsync` → `ReadLocator`. `DeterministicId` does not hash the region, so **no candidate id churns**. No assertion weakened or removed (+252 test lines, zero deleted assertions; the four production deletions are the `Project` signature).
5. **`ThirdPartyReportSourceContext` — PASS, no finding.** It is **not new** — pre-existing at `ThirdPartyReportProfiles.cs:83-91`, the same record `Extract` already takes; the commit adds no type at all. `RetainedSourceIdentity` and `LogicalDocumentVersion` do not exist in `src/`; `IntakeSourceIdentity` (`IntakeContracts.cs:210-212`) is a channel/token pair, unrelated.
6. **Consumer note — NEEDS CORRECTION (F-01).** Items 1, 2, 4, 5 verified correct; item 3 has an edge-case defect (F-04) and the Ordering guidance is materially wrong (F-01).

### Findings
- **F-01 MAJOR (documentation, not code) — the ordering guidance handed to A is wrong.** The report's "Practical guidance" (`c05-seam-report.md:103-106`) says `MapAsync`'s `OrderBy(Field).ThenBy(Occurrence)` is "stable and adequate". It is not, and it contradicts the report's own preceding paragraph. `ThirdPartySourceCandidates.Create` stamps `context.Occurrence` on every row (`ThirdPartyReportProfiles.cs:535`) and `ProcessIntake.cs:351` always passes `Occurrence: 0`, so `Occurrence` is **constant across an analysis** and `.ThenBy(Occurrence)` (`EfRetainedInstructionAnalysisStore.cs:220`) is no tiebreak at all; LINQ stability preserves the input order, and the input is an untied SQL result. Consequences under `Reconstruct`: `Lookup.First` (`:1453`) cites an arbitrary row of a Conflicting field; `TextList`/`NumberList` (`:1436-1448`) return damage zones and valuation deductions in an arbitrary order; the photograph list (`:1228`) likewise. Contiguity is not the issue — `Observed` regroups through a dictionary — the loss is **within-key** order, which has no persisted tiebreak. Disposition: **corrected in the attestation**; hand A the corrected note below, not the author's paragraph. No change to `35cc17c66` required — `Reconstruct`'s own order-in/order-out contract is stated correctly on the method (`:749-758`).
- **F-02 MINOR — no test exercises A's actual call shape.** `Recorded` (test `:971`) feeds rows in reading order only. Add a case reconstructing from `Recorded(result).OrderBy(row => row.Field, StringComparer.Ordinal).ThenBy(row => row.Id)` and asserting the scalar projection (issuer, registration, PAV, estimate count) survives while naming the list-order caveat. AGENTS rule 19.
- **F-03 MINOR — `IsObservedField` (`:863-865`) is a second statement of one concept.** The primary statement is structural: `Extract` appends the issuer (`:705`), `ScannedPages` (`:711`), `Media` (`:720`) and `Complete`'s `FindingRows` (`:893`) outside `observed`. A new non-observed row kind added to `Extract` would be silently grouped as an observed field. Correction: move the predicate onto `ThirdPartyReportFields` as `IsObservedValue(string field)`, or add a guard test asserting `result.Candidates.Where(r => !IsObservedValue(r.Field))` equals issuer + scanned-page + media + finding rows. AGENTS rules 7/8.
- **F-04 MINOR — the note's context snippet throws on a supported call.** `Occurrence: rows[0].Occurrence` (`c05-seam-report.md:45-48`) indexes an empty list, which item 5 of the same note and `Reconstruct([])` (test `:906`) explicitly support, and takes an instruction row's occurrence on a mixed set. Use `rows.FirstOrDefault(r => r.PolicyKey == ThirdPartyReportAnalysis.PolicyKey)?.Occurrence ?? 0`.
- **F-05 MINOR — the `ReaderVersion` agreement is coincidental in the test.** `Create` stamps `context.ReaderVersion` (`ThirdPartyReportProfiles.cs:549`); `ToCandidates` stamps its own argument; `Restore` reads the row. The theory passes because `Context()` sets `ReaderVersion: "1"` (test `:1078`) and `Recorded` passes the literal `"1"` (`:973`). Production is safe — `ProcessIntake.cs:353` and `:394` both use `readResult.ReaderVersion`. Author disclosed it as OQ4 (honest disposition). Correction: pass `Context().ReaderVersion` in `Recorded`.
- **F-06 INFO — report OQ2 overstates the blast radius.** `grep -rn "Region" src/Pegasus.Web/` returns nothing; no provenance chip reads `SourceFieldCandidate.Region` today. Only readers are `EfRetainedInstructionAnalysisStore.GetAsync:206` and `Map:256`.
- **F-07 INFO — version bump safe; instruction rows untouched.** `LocatorJson` derives the version (`AnalyzeRetainedInstruction.cs:721`) — computed, never passed. The only version comparison is the allowlist `is not (1 or 2 or 3)` at `:739`; every other `envelope.Version` in `src/` belongs to an unrelated envelope. The new `Locator` helper is `private` to `ThirdPartyReportAnalysis`, whose `ToCandidates` has exactly one production caller, `ProcessIntake.cs:391`, on the third-party path — `AnalyzeRetainedInstruction` builds its own rows unchanged, so **nothing changes for instruction (non-report) rows**. The version-2 region round trip is already covered pre-existing at `AnalyzeRetainedInstructionTests.TheLocatorRoundTripsThroughItsOwnEnvelope:567-570`.
- **F-08 INFO — integration proof of the region write is INCONCLUSIVE.** `ThirdPartyReportCorpusTests` and `ThirdPartyReportProvenanceWebTests` did not run; the two corpus-gated Core tests are the run's SKIPs. I read both integration files — neither asserts on `Locator` or `Region` — so residual risk is low, but re-run both once A's compile block clears.

### Exact signature
```csharp
// Pegasus.Core.Intake.ThirdPartyReports.ThirdPartyReportExtraction
public static ThirdPartyReportCandidate? Reconstruct(
    IReadOnlyList<RetainedInstructionCandidate> rows,
    ThirdPartyReportSourceContext context);
```
`ThirdPartyReportSourceContext` (existing, `ThirdPartyReportProfiles.cs:83`): `(Guid ReceiptId, string Sha256, int Occurrence, Guid? DocumentId = null, Guid? DocumentVersionId = null, Guid? IntakeAssetId = null, string ReaderVersion = "unspecified_reader", string SourceLabel = "")`.

### Consumer note for A — hand over verbatim (5 lines)
1. Pass the persisted candidates of **one** retained analysis (`RetainedInstructionAnalysis.Candidates`, i.e. your existing private `Map(IntakeSourceCandidateEntity)` output — `RetainedInstructionCandidate`, because it carries `PolicyKey` and `Locator` that `SourceFieldCandidate` drops). One analysis row = at most one report candidate; never merge two analyses into one call. Rows of another policy are ignored, not rejected, so the whole candidate set is fine.
2. Build the identity as `new ThirdPartyReportSourceContext(analysis.IntakeReceiptId, analysis.SourceSha256, Occurrence: rows.FirstOrDefault(r => r.PolicyKey == ThirdPartyReportAnalysis.PolicyKey)?.Occurrence ?? 0, IntakeAssetId: analysis.IntakeAssetId)`, adding `DocumentId` / `DocumentVersionId` only where you actually hold them (`RecordAsync` writes `DocumentVersionId = null` today). `ReaderVersion` and `SourceLabel` on the context are unused by reconstruction — every row carries its own.
3. `null` means "these rows record no third-party report": no row carries `ThirdPartyReportAnalysis.PolicyKey` ("third-party-report"), or the persisted issuer row is not `Usable` (ambiguous document, explicit non-report role, scan-only source). That is the same answer `Extract` gave for those documents — skip them, never synthesise an empty candidate.
4. Order the rows by `(Field, ReferenceRole, PartyRole, Id)`. `Occurrence` is constant within one analysis, so `MapAsync`'s `.ThenBy(Occurrence)` is not a tiebreak and without `Id` the database chooses. Even with it this is a reproducible order, **not** the printed order: a conflicting field's typed fact cites whichever row sorts first, and the damage-zone, valuation-deduction and photograph lists follow that order. Printed order needs a persisted ordinal column — your schema call, not C's.
5. Do the auth filter (`StaffAccessRight.PerformCasework`) and the receipt / documentVersionId / intakeAssetId narrowing before calling. `Reconstruct` opens no bytes, calls no store, re-runs no signature, infers no issuer and repairs no arithmetic — a value persisted Missing, Ambiguous or Conflicting comes back in exactly that state.
