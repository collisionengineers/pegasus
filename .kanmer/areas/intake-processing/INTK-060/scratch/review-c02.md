---
kind: review-attestation
pr: "none (controller integrates slices; C02 opens no PR)"
head_sha: "494767d30a7f6deaee9a738fc680fb6b25c119ca"
verdict: needs-changes
reviewer: "pegasus-reviewer (INTK-060 C02, wave 32)"
independent: true
plan_hash: "pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md (## C02, ### C02 files, Evidence and extraction invariants)"
ticket_updated: "INTK-060 scratch/c02-notes version eb367a804894ca53"
board_sha: "not read (controller override: no board writes beyond scratch/review-c02)"
expected_reviewers: []
threads_snapshot: []
findings:
  - id: C02-R-1
    severity: blocker
    disposition: open
    summary: "Adapter discards every word (confidence + coordinates) whenever the provider returns lines; plan item 5 requires words with coordinates/confidence. Proved by lane 3's failure of the implementer's own ConfidenceIsCarriedThroughAndIsNeverWhatAcceptsAValue."
  - id: C02-R-2
    severity: major
    disposition: open
    summary: "The provider operation id is never persisted before the result is awaited. A cancelled or crashed attempt leaves the operation Pending with no provider identity, and the next delivery RESUBMITS the same pages. The case named for this scenario does not model it."
  - id: C02-R-3
    severity: major
    disposition: open
    summary: "Every candidate read from an e-mail's quoted history is stamped MessagePart.CurrentBody, because the whole-body fragment carries that locator and wins DistinctBy over the correctly labelled QuotedHistory fragment. ASSUMPTION 2 keeps QDOS output stable but produces false provenance."
  - id: C02-R-4
    severity: major
    disposition: open
    summary: "The intake_ocr external-work outbox row is never advanced. RetryScheduled/Completed are written only to IntakeOcrOperations, so a scheduled retry is never re-dispatched and a completed row never closes. Plan item 7's reuse of the retry/recovery path is unmet, and the identified C-side follow-up names only the enqueue."
  - id: C02-R-5
    severity: major
    disposition: open
    summary: "The DOC/MSG partial, which plan item 2 names explicitly, was not extended. MapMsgDocumentAsync emits its body with no locator and no quoted-history fragment, so an Outlook .msg forward keeps none of the current-body/quoted-history distinction. Undisclosed as a deviation or assumption."
  - id: C02-R-6
    severity: minor
    disposition: open
    summary: "The quoted-history fragment begins with the newline the forwarded-header regex match includes, and its region is off by one. Lane 3 failure at StructuredIntakeSourceReaderTests.cs:79."
  - id: C02-R-7
    severity: minor
    disposition: open
    summary: "ResponseSha256/SourceSha256 are nchar(64) fixed-length and are not trimmed on read, so a short value round-trips space-padded. Lane 3 failure at OcrIntakeRecoveryTests.cs:43."
  - id: C02-R-8
    severity: minor
    disposition: open
    summary: "Provider table cells are mapped with null bounds although the layout model gives each cell boundingRegions; plan items 5 and 6 ask for tables with coordinates."
  - id: C02-R-9
    severity: minor
    disposition: open
    summary: "A 5xx or 408 on the submission POST is recorded non-retryable, contradicting plan item 7's 'timeout/throttle/outage schedules only a safe retry'."
  - id: C02-R-10
    severity: minor
    disposition: open
    summary: "Dead members introduced: IntakeMessagePart.OuterTransport/Attachment, IntakeLocatorKind.Document/Region, IntakeSourceLocator.Sha256/DocumentRole (never set and never serialized), IIntakeOcrOperationStore.FindByOperationKeyAsync, OperationEnvelope.Reserved, InstructionFieldCandidate.SourceValue, and the FragmentRank that SourceStructure.Bind computes then discards."
  - id: C02-R-11
    severity: minor
    disposition: open
    summary: "LocatorEnvelope persists Kind and MessagePart as bare enum ordinals and never validates Version, so reordering either enum would silently re-read stored rows as a different place."
  - id: C02-R-12
    severity: minor
    disposition: open
    summary: "Plan item 7's 'low-confidence ... fails closed to staff review' is answered by a design argument in the report rather than implemented or recorded as an ASSUMPTION on scratch/c02-notes."
  - id: C02-R-13
    severity: minor
    disposition: open
    summary: "tests/Pegasus.Core.Tests/Intake/AnalyzeRetainedInstructionTests.cs is a C01 file outside the C02 map and is not named in the report's Deviations, which discloses only EfIntakeOcrOperationStore.cs."
  - id: C02-R-14
    severity: minor
    disposition: open
    summary: "The report's Evidence status omits two further skips the web lane carries: QdosExtractionCoverageTests.RealInstructionEmailsExtractTheCoreFieldSet and PrincipalIdentificationCorpusEvidenceTests.EveryLocallyPresentOriginalKeepsItsHashAndReachesTheRealReader."
  - id: C02-R-15
    severity: note
    disposition: accepted-risk
    summary: "The 'existing QDOS corpus output stays unchanged' acceptance check is unproven on this head: every genuine-corpus suite skips for absent local originals, and structure now suppresses the flattened reading wherever a label cell binds."
  - id: C02-R-16
    severity: note
    disposition: accepted-risk
    summary: "A document-version-scoped OCR operation is unreachable and fails closed (ASSUMPTION 4, recorded, with an exact C-F02 follow-up named)."
---

# C02 review - structured source provenance and page-restricted OCR

**Verdict: needs-changes**, bound to `494767d30a7f6deaee9a738fc680fb6b25c119ca`
(eight commits over `aa32027467e01066b7536c5fa87048ee6c5ec3d8`; the worktree HEAD
`ca3ec9abb` is a later merge of `task/pegasus-v1-intake` into `c02-provenance`,
and `494767d30` is its ancestor, so the slice diff `aa3202746..494767d30` is what
was reviewed).

Worktree assertions passed before any read: toplevel is
`C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c02`, both
`--git-common-dir` values resolve to `C:/Users/PGUSER/Documents/github/pegasus/.git`,
branch is `c02-provenance`, the tree is clean, and the path is neither
`.worktrees/kanmer` nor the primary checkout. Nothing was edited, pushed or
merged, and this review ran no test command of its own - the lane files below are
the runner's.

## Lanes seen (wave 32)

| lane | result | detail |
| --- | --- | --- |
| 1-build | PASS | `dotnet build ./Pegasus.slnx --configuration Release --no-restore`, 0 warnings, 0 errors |
| 2-core | PASS | 287 passed, 0 skipped |
| 3-reader | **FAIL** | 3 failed, 27 passed |
| 4-web | PASS | 57 passed, 7 skipped |
| 5-architecture | PASS | 100 passed, 0 skipped |

Lane 3's three failures are C02's own new cases and are why a `pass` is
impossible on this head:

- `AzureDocumentIntelligenceOcrTests.ConfidenceIsCarriedThroughAndIsNeverWhatAcceptsAValue`
  - `Assert.Single()` on an empty collection (C02-R-1);
- `StructuredIntakeSourceReaderTests.TheOuterSenderTheCurrentBodyAndTheQuotedHistoryStayThreeSeparateThings`
  - the quoted fragment starts with a newline before `From:` (C02-R-6);
- `OcrIntakeRecoveryTests.ASubmittedOperationCompletesOnceAndReanalysesOnce`
  - `"response-hash-1"` came back space-padded (C02-R-7).

Lane 4's seven skips are all environmental (absent local originals), but they
include the whole genuine-corpus regression set, which is what C02-R-15 records.

## Ownership and frozen contracts

Ownership is clean. The fifteen touched files are the C02 map plus the two C01
files the dispatch named as extendable (`AnalyzeRetainedInstruction.cs`,
`EfRetainedInstructionAnalysisStore.cs`), the disclosed deviation
`EfIntakeOcrOperationStore.cs`, and one C01 test file that was not disclosed
(C02-R-13). No A-owned or B-owned file moved: `ExternalWorkProcessing.cs`,
`ExternalWorkProcessingTests.cs`, `DependencyInjection.cs`,
`WorkerDependencyInjection.cs`, `IntakeFunctions.cs`, the migrations, entities
and model snapshot, and `Vehicle/LookupContracts.cs` are all absent from the
diff. `MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs` is also absent, which
is C02-R-5.

The three frozen contracts are byte-identical on this head:
`SourceFieldCandidate` (`IntakeContracts.cs:1125`), `ISourceCandidateQueries`
(`:1131`) and `IReadLogicalDocumentVersion` (`DocumentContracts.cs:20`). The
locator was added as trailing optional parameters on `IntakeContentFragment`,
`InstructionFieldCandidate` and `RetainedInstructionCandidate`, so no existing
construction site changed.

`TrackedPegasusSourceHashesHaveNotDrifted`: **no QDOS policy source changed.**
The thirteen tracked `pegasus` snapshots include
`QdosInstructionExtractionPolicy.cs`, `QdosMailRoutePolicy.cs`,
`QdosMailClassificationPolicy.cs`, `QdosCaseMatchPolicy.cs`,
`MailClassificationContracts.cs` and `provider-domains.v1.json`; none appears in
the slice diff. Nothing needs declaring for A's regeneration, and lane 2 (which
runs the `Qdos` filter) is green.

## Item-by-item

### Item 1 - one minimal structured locator, version-2 envelope

Met, with two caveats. `IntakeSourceLocator` is one record with one `Kind`
enumeration covering page, table cell, form field, region and message part; no
parallel document model and no second candidate record appear. `Cell` is
computed once as `T{Table}R{Row}C{Column}`, so a store and a page cannot spell
it differently.

Backward compatibility is sound. `LocatorJson` writes version 1 when no locator
is supplied and version 2 otherwise; `ReadLocator` on a version-1 row finds
`Kind` null and returns `ForPage(page)` - or null when the row recorded no page.
**Every reader of `LocatorJson` in the repository is
`EfRetainedInstructionAnalysisStore` (lines 183 and 237)**, so there is no second
reader to break, and rows already written by C01/C05 still read. C05's
finding-row locator does **not** go through this envelope at all:
`ThirdPartyReportExtraction.Locator` builds `SourceFieldCandidate` values in
memory (`ThirdPartyReportExtraction.cs:819`) and nothing in Infrastructure
persists them, so its round-trip is unchanged by construction.

Caveats: C02-R-11 (enum ordinals persisted, `Version` never checked) and the
`Sha256`/`DocumentRole` members of the locator, which nothing sets and the
envelope does not carry (C02-R-10) - a caller that set them would silently lose
them across storage.

### Item 2 - reader structure

Partly met.

PDF pages now carry `IntakeSourceLocator.ForPage(n)` and filled AcroForm **text**
fields become their own fragments keyed by `PartialName`/`AlternateName` with
page and bounds; checkboxes and radio groups are deliberately skipped with a
stated reason, and the read is marked incomplete past 1024 fields. DOCX
top-level table cells become fragments carrying table/row/column beside the
unchanged flattened paragraph text, capped at 4096. Corrupt, encrypted and
non-renderable documents are refused as before, and a scan-like page still
reports its exact page number in `ScannedPdfPages` with no OCR call - lane 3's
`ACorruptDocumentIsRefusedRatherThanPartlyRead` and lane 4's embedded-text web
cases are green on that.

**The DOC/MSG partial was not touched (C02-R-5).** `MapMsgDocumentAsync`
(`MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs:175-179`) still emits
`"{label}, message body"` with no locator and no quoted-history fragment, so for
an Outlook `.msg` - which lane 4 exercises as
`GenuineMsgIsADirectQdosInstructionThatAllocatesACase` - outer transport, current
body and quoted history are *not* distinguishable. Plan item 2 names the partial
explicitly, and the omission is in neither the Deviations section nor
`scratch/c02-notes`.

**ASSUMPTION 2 does keep the QDOS invariant, and does hide a split (C02-R-3).**
On the mechanics the assumption is right: `ForwardedHeaderRegex` matches a
`From:` block preceded by start-of-text or a line break, so `quotedAt.Index`
lands on a line boundary, the quoted fragment's lines are a suffix of the body's
lines, `FindCandidates` scans every line of the whole body anyway, and
`DistinctBy(Value, OrdinalIgnoreCase)` in `ExtractFields` keeps the body
fragment's copy at its original rank. Extraction output is genuinely unchanged.

But the body fragment is stamped `MessagePart.CurrentBody` with region
`chars 0-{index}` while its `Text` is the whole body including the history. So a
value printed **only** beneath the forwarded header is emitted as a candidate
whose locator says it came from the current message, and the QuotedHistory
fragment's correctly labelled duplicate is the one `DistinctBy` throws away. The
implementer's own reader fixture is exactly this shape - `ForwardedBody` puts
`Claim Number: CLM-9001` only below the header - and
`InstructionFieldExtractionTests.TheCurrentBodyIsPreferredOverTheHistoryQuotedBeneathIt`
avoids it by repeating the value above and below. This is the failure mode the
`IntakeMessagePart` doc comment itself names: a forwarding desk recorded as the
instructing party. The honest minimal fix is to stop claiming a message part on
a fragment that contains both - leave the whole-body fragment's `MessagePart`
`None`, or set it from the matched offset in `FindCandidates` - so only the
QuotedHistory fragment makes a message-part claim.

### Item 3 - `InstructionFieldExtraction`

Met. `SourceStructure` indexes form fields and cells once per extraction and
offers exactly two bounded layout rules - a header-row label owns its column
beneath, a body label owns what is beside it on its row and only otherwise the
cell beneath. `FieldDefinition.FormFields` and `.ColumnHeader` let a provider
policy state field identity and party column rather than letting Infrastructure
own it. Where a label admits several value cells every one is returned; all
structured readings share rank 0 (`InstructionFieldExtraction.cs:185`), so
document order can never pick between two cells of one row and the field stays
ambiguous. `FindCandidates` yields nothing for a TableCell or FormField fragment
(`:524`), which is what stops a flattened neighbour's value being read as this
field's. `Normalize` compares whole labels only - no substring match - and
`IsValueCell` refuses a cell that is itself another definition's label. Lane 2's
ten new engine cases cover the OAK header shape, ALS paired columns (both the
declared-party and the ambiguous case, with both raw values and both cell
locators), a DFD form field's own identity, the swap that cannot happen, a label
alone on its row, explicit `Missing`, and a structureless document reading
exactly as before. Role conflicts are not collapsed.

OAK/ALS/DFD expectations are proven against *structural non-domain layouts*, not
against the pack: the report says so, and the pack-backed suites all skip
(C02-R-15). The form-field path in particular is proved at the engine level and
by inspection only (no AcroForm fixture) - honestly stated as INCONCLUSIVE.

The residual risk C02-R-15 records is real and specific: where a label cell
binds, `structured.Length > 0` suppresses the flattened line scan entirely
(`InstructionFieldExtraction.cs:417-421`), so a genuine DOCX or AcroForm PDF
whose flattened reading previously won can now read differently. Only the
skipped genuine-corpus suites can settle it.

### Item 4 - the B projection

Met. No new type and no new aggregate. `EfRetainedInstructionAnalysisStore` now
fills `SourceFieldCandidate.Cell`/`.FormField`/`.Region` from the stored locator
where all three were hard-coded null. The projection is documented in the report
with the exact frozen `GetAsync` signature and the full member list, and the
disposition vocabulary is stated: `Usable`, `Missing` (recorded as a finding),
`Ambiguous` (every candidate row kept, `NormalizedValue` deliberately null),
`Conflicting` reserved for contradiction of a confirmed fact. `RawValue` is never
replaced by normalization.

ASSUMPTION 1 (`HasConflict` becomes `Ambiguous`, not `Conflicting`) is correct
against the invariant "multiple supported interpretations are `Ambiguous`", is
recorded, and the one C01 assertion it falsifies was corrected in place with a
comment naming C02. It does not collide with C05, which keeps `Conflicting` for a
printed contradiction (`ThirdPartyReportExtraction.FindingDisposition`) - the two
owners now agree.

### Item 5 - `IntakeOcr.cs`

States are exactly the six required. `IntakeOcrRequest.Validate` enforces the
same document-version XOR intake-asset exclusive-or as
`CK_IntakeOcrOperations_Source`, one-based distinct pages, and a non-empty page
list. `IntakeOcrPolicy` is the single owner of the bounded schedule (30 s, 2 m,
10 m, 30 m, 2 h; six attempts) and of what a response must prove: a content
hash, the pinned API version, and exactly the submitted pages each once.
**Nothing is accepted or discarded on confidence** - `Validate` reads no
confidence at all, which satisfies item 6's prohibition.

`Unknown` reconciliation without resubmission is correct in the paths it covers:
an operation carrying a provider id is asked about whatever its state
(`IntakeOcr.cs:471-484`), and an operation recorded as sent without one stays
`Unknown` with no retry time. But see C02-R-2 for the path that is not covered,
and C02-R-1 and C02-R-12 for what the result no longer carries.

### Item 6 - `AzureDocumentIntelligenceOcr`

Mostly met. Existing `HttpClient` plus `Azure.Core`/`Azure.Identity`
`TokenCredential` at scope `https://cognitiveservices.azure.com/.default`; no
package added, no key, no second vendor or runtime. The submission is
`POST {endpoint}/documentintelligence/documentModels/prebuilt-layout:analyze`
with `api-version=2024-11-30` and an ascending `pages` list, sent as
`application/octet-stream`, and the bytes are hashed and refused before anything
is sent if they are not the bytes the operation names. `Operation-Location` is
followed only when scheme, host and port match the configured endpoint and the
path names this model's `analyzeResults`; anything else is
`ocr_operation_location_invalid` and is not followed. `succeeded` is mapped only
after `modelId` is checked, `apiVersion` is carried through for Core to refuse,
polygons map to the enclosing rectangle in the page's own named unit, and the
provider's zero-based cell indexes become the locator's one-based ones. Lane 3
proves eleven of the thirteen adapter cases.

What it drops: **word-level confidence and coordinates (C02-R-1)**. `Pages`
computes `words` (`AzureDocumentIntelligenceOcr.cs:326-338`) and then, whenever
`lines` is non-empty - which is every real layout response - builds each
`IntakeOcrLine` with an empty `Words` list (`:339-346`) and never uses `words`
again. `IntakeOcrLine` carries no confidence of its own, so a completed result
carries **no confidence anywhere**. Plan item 5 requires
"words/lines/tables with coordinates/confidence" and the expected outputs
require confidence retained; the implementer's own case asserts it and lane 3
fails it. Table cells likewise get `null` bounds (`:403`) although
`boundingRegions` is available (C02-R-8).

### Item 7 - the durable arm, and the A handoff

The order `ProcessIntakeOcr` owns is right on paper and right in the paths the
lanes exercise: a terminal operation returns immediately so a redelivered
message has no second side effect; the recorded hash is re-checked against the
receipt's asset before the source is opened; an operation with a provider id is
reconciled and never resent; a failure to *open* is safely retryable because
nothing was sent while a failure during the *send* is `Unknown`; completion
validates then stores response hash, provider identity and page output
atomically with `Completed` (`CompleteAsync` in one serializable transaction)
before re-entering `AnalyzeRetainedInstruction` exactly once under
`ocr:<operation key>`; timeout and throttle schedule a bounded retry; malformed,
inconsistent, unattributable or wrong-version output is `Failed` with no page
output and no candidate. Every store write is optimistic on the recorded version
under serializable isolation, and lane 3's
`AStaleWriterLosesRatherThanOverwritingTheRecordedOutcome` proves it.

Two things are wrong.

**C02-R-2 - identity before the HTTP call is not what is actually achieved.**
`BeginAsync` records the operation before anything is sent, which is correct as
far as it goes. But the *provider's* identity is recorded only in `ApplyAsync`
(`IntakeOcr.cs:630-637`), after `AnalyzeAsync` has already returned - there is no
persistence point between "the provider accepted" and "the result is awaited",
because `AnalyzeAsync` submits and polls inside one call and the adapter has no
store. So the doc comment on `RecordSubmittedAsync` ("Written before the result
is awaited, so an interrupted wait leaves something to look up") is false, in
both interruption shapes:

- *cancelled mid-poll.* `PollAsync` guards only `Task.Delay`; the poll `GET`'s
  `SendAsync` is unguarded, so a cancelled attempt throws through
  `SubmitAsync`'s `OperationCanceledException` rethrow filter and out of
  `ExecuteAsync` with **no outcome recorded**.
- *graceful bounded-attempt exhaustion.* `PollAsync` returns
  `Unknown("ocr_operation_pending", providerOperationId)` from its `Task.Delay`
  catch - a catch whose filter means the token is already cancelled - and
  `ApplyAsync` then calls `store.RecordSubmittedAsync` passing that same
  cancelled token, which throws before it writes.

Either way the row stays `Pending` with `ProviderOperationId` null. The next
delivery reads `Pending`, finds no provider id, does not match the
`Unknown or Processing` branch, and falls through to `SubmitAsync` - **the same
pages are sent to the provider a second time**, which is the second charged side
effect item 5 forbids.
`OcrIntakeRecoveryTests.AHostThatDiedAfterSubmittingLeavesAnOperationThatIsLookedUpAndNotResent`
does not model this: it has `AnalyzeAsync` *return normally* with a provider id
on a live token, so `ApplyAsync` persists it. Its comment ("a restart, or a
bounded attempt running out") asserts an equivalence the code does not have. The
fix wants a persistence hook the provider can call the moment the operation
location is read - or an `AnalyzeAsync` that returns the accepted identity before
polling - plus a non-cancellable write for the terminal outcome.

**C02-R-4 - the external-work outbox is never advanced.**
`EfIntakeOcrOperationStore` writes only `IntakeOcrOperationEntity`. The
established pattern on this table, `EfVehicleLookupWorkStore`, writes back to the
A-owned `ExternalWorkItems` row: state `pending` for a scheduled retry, the new
`DueAtUtc`, and `AttemptCount` incremented on claim
(`EfVehicleLookupWorkStore.cs:214-219`, and the `ExternalWorkStatePersistence`
vocabulary). C02's arm does none of it, so `IntakeOcrState.RetryScheduled` with
its `RetryAtUtc` has no effect on when the dispatcher redelivers, and a
`Completed` operation's outbox row is never closed. Plan item 7's "reuse the
existing external-work outbox, dispatcher, retry/recovery and attribution path"
is met for the table and unmet for the path.

**The deliberate non-enqueue is the right call, and the handoff hunks are minimal
and correct.** `ExternalWorkKinds.IntakeOcr` genuinely lives in an A-owned file,
and enqueuing a kind the router would refuse is worse than not enqueuing. The
four quoted hunks are each the smallest edit that works: one constant, one
optional constructor parameter plus one `when intakeOcr is not null` case that
matches the EVA precedent's fail-closed comment exactly, the DI block guarded on
`DocumentIntelligence:Endpoint` so a host without one has no handler, and the two
Worker registrations. `IntakeFunctions.cs` correctly needs nothing, because the
Worker reaches OCR through `IProcessQueuedExternalWork`.

**The C-side follow-up they need is only partly identified.** The report names
the enqueue precisely (one method on `EfIntakeOcrOperationStore` writing the
`ExternalWorkItemEntity` and the `IntakeOcrOperationEntity` in one serializable
transaction, called from `ProcessIntake` when `ScannedPdfPages` is non-empty). It
does **not** name the outbox *completion and rescheduling* write-back that
C02-R-4 requires, without which the enqueue would produce rows that fire once and
never retry. That belongs in the same follow-up.

## Honesty of the INCONCLUSIVE items

Good, with one omission. The report states provider correctness INCONCLUSIVE
(no genuine Document Intelligence response on this machine; the adapter cases use
a structural fake of the 2024-11-30 contract with invented non-domain text, and
assert the contract rather than the reading) and PDF AcroForm locators
INCONCLUSIVE (no form fixture; proved at engine level and by inspection). No OCR
text or result is presented as evidence, and no corpus file was read, written,
renamed or embedded - lane 4's corpus suites skip in place. The omission is
C02-R-14: two further skips the web lane carries are not listed in the evidence
status, and one of them
(`QdosExtractionCoverageTests.RealInstructionEmailsExtractTheCoreFieldSet`) is
directly the QDOS-unchanged check C02-R-15 depends on.

## One owner per rule, doc comments, dead code

One owner holds each rule: the retry schedule and the acceptance check live only
in `IntakeOcrPolicy`; the provider/model/API triple only in
`IntakeOcrProviderIdentity`; the cell string only in `IntakeSourceLocator.Cell`;
the forwarded-header boundary is reused from `StaffForwardBodyCleaner` rather
than rewritten; the layout rules live only in `SourceStructure`, and
`FieldDefinition.FormFields`/`ColumnHeader` keep the provider grammar in the
policy rather than in Infrastructure. `AnalyzeRetainedInstruction.PageFrom`
survives as a documented fallback that the locator overrides, which is
acceptable.

Doc comments are dense and mostly explain *why*, but coverage is not one per
member: `IntakeSourceLocator` documents `Table`, `Region` and `Occurrence` and
leaves `Kind`, `Page`, `Row`, `Column`, `FormField`, `MessagePart`, `Sha256` and
`DocumentRole` undocumented, and the
`IntakeOcrWord`/`IntakeOcrLine`/`IntakeOcrCell`/`IntakeOcrTable` records carry
none. Dead code is C02-R-10.

## What a `pass` would need

1. C02-R-1 fixed and lane 3 green.
2. C02-R-2 fixed: a provider identity that survives a cancelled attempt, and a
   `Pending`-with-no-identity row that cannot be resubmitted.
3. C02-R-3 fixed: no fragment claiming a message part it does not exclusively
   contain.
4. C02-R-4 fixed, and the outbox write-back added to the identified follow-up.
5. C02-R-5 either implemented in the DOC/MSG partial or recorded as an
   ASSUMPTION on `scratch/c02-notes` with its alternatives.
6. C02-R-6 and C02-R-7 fixed so lane 3 passes.
7. C02-R-12, C02-R-13 and C02-R-14 recorded honestly.

Minors C02-R-8 through C02-R-11 are residual risk and would not on their own
hold the slice.
