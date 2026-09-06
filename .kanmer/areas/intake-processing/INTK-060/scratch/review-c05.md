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
