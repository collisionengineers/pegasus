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
