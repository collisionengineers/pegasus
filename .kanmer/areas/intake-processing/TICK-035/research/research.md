# Research — TICK-035

## Question and authority

Activate the evidenced top-15 routes through the existing production intake,
not merely review-only extractor suggestions. The 7 September operator brief
and EPIC-014 supersede the historical post-alpha/operator-acceptance deferral
in Astra C03 and the reference corpus. No extra per-domain permission gate:
root confirmed genuine supplied evidence is sufficient. No fabricated mapping,
generic Gmail route, new mailbox onboarding or live provider write.

Baseline read: origin/dev 1d972f05c0f10c2ecf804f271a4fd3155242f1ef.
PLAT-028 currently awaits same-PR remediation/review; implementation must
take its eventual merged dev base before DI/Settings edits. No sources are
declared by the Kanmer sources registry.

## Existing owners and actual blockers

- QdosMailRoutePolicy owns sender consistency, staff-forward unwrapping and
  three exact QDOS domains. Its static ProvisionalEffectiveSender is also used
  by retained-mail projections. Replace/generalize that owner, not a parallel
  generic policy; retain exact matching and conflicting-original fail-closed.
- InstructionExtractionPolicySelector already has fifteen registered profiles.
  ProcessIntake instead injects one fixed IInstructionExtractionPolicy and
  throws if an accepted route principal differs (ProcessIntake:833).
  Wire the selector and require selected profile agreement with route identity.
- IMailClassificationPolicy currently has only QdosMailClassificationPolicy.
  ProcessIntake:961 returns null for every other provider; IntakeAllocation:257
  cannot allocate without a case type. A route/selector-only patch is not done.
  Generalize the existing classification owner, preserving QDOS's accepted
  request/reply/Triage/Audit predicates and current-message source boundaries.
- EvaluateIntakeCaseMatch and CaseMatchIndexProjector share the existing
  eliminator/index contract. Only QdosCaseMatchPolicy is registered.
  Its NNNNN/N claim-tail grammar is QDOS-specific and must not normalize FW,
  PCH or other principal references. Generic activation must use typed
  principal-owned fields, preserve full non-QDOS references, and reuse the
  same read/write key normalization and eliminator, never a second matcher.
- The profile selector concatenates all fragments. Existing negative report
  signatures can suppress a valid instruction accompanied by an original report.
  Selection/extraction must be scoped by existing source/document identity
  where a genuine mixed-document fixture proves this; a report must never
  become the instruction or steal its principal.
- INTK-061 owns durable destination/custody/OCR-analysis retries. Triage formal
  linking and handoff-is-review are assigned elsewhere. No new queue/worker,
  grants, schema, extraction framework or policy flags are justified.

## Accepted route identity evidence

The existing provider-domains.v1.json reference package (Infrastructure/
Persistence/ReferenceData) derives from initial.xlsx Sheet1 (11 rows), source
SHA256 e4bf89b0aeef3f1106bf34ed50f74dffc44c5ed748e0ad0811b66ee099b6cd29.
It is reference evidence, not a second runtime owner.

| Principal | Exact direct identity evidenced |
| --- | --- |
| AX | ax-uk.com |
| BLACK | blackstone-legal.co.uk |
| DFD | dfd-solicitors.co.uk |
| FW | fairwaylegal.co.uk |
| KBS | knightsbridgesolicitors.co.uk |
| MP | montrealprestige.co.uk |
| OAK | oakwoodscotland.co.uk; oakwoodsolicitors.co.uk |
| PCH | pch-ltd.com |
| QCL | qc-law.co.uk |
| QDOS | qdosassist.co.uk; qdosassists.co.uk; qdoslaw.co.uk |
| RJS | robertjameslaw.co.uk |
| ALS | autologistic.co.uk — attributed instruction plus ALS letter |
| BC | bakercoleman.co.uk — original letters and correspondence corroborate |
| SBL | smartbusinesslink.com — three attributed original instruction messages |
| YML | exact mailbox networkhduk@gmail.com only; HDUK document issuer retained |

Reference principal-identification-corpus.v1.json records connexus.co.uk and
ensurance-claims.co.uk as PCH intermediary evidence requiring a unique PCH
instruction fingerprint. They are NOT direct PCH domains, even though the
older flat domain package lists them under PCH. Unknown/shared intermediaries
do not become direct principals. In particular do not infer SBL from
sbl@connexus.co.uk, a raw contact scrape or sbgl.co.uk. Do not activate a
generic gmail.com route. No top-15 direct identity remains unknown after the
following research, but unsupported variants/requests still fail closed.

### Newly corroborated identities and exact source trace

ALS: principal-identification-corpus evidence-46de51472636f922, original
corpus/qdos-email-corpus/sources/collisionspike/emailevals/to-sort/New Inspection
Instruction.eml, SHA256
46de51472636f9220cd77e2a96d9d9ff72ec95cac8b762fa0006138f4a452c30.
From kalan@autologistic.co.uk; attachment Instruct Engineer - ALS.DOC.
This is one genuine instruction. evidence-e7055f6ae13cc187 is an automatic
reply, not a second instruction. ALS original letters corroborate the
Auto Logistic Solutions issuer. Source-attributed, not inferred from brand.

YML: evidence-0fc086bb3480a614, corpus/qdos-email-corpus/sources/collisionspike/
emailevals/to-sort/FW LETTER OF INSTRUCTION - HD4021.eml, SHA256
0fc086bb3480a614f93b68efb555074d2fdc94b95ea8c5c77f629d05801759d3.
From networkhduk@gmail.com. PRINCIPAL_DOCUMENT_MAPS.md YML/HDUK section
corroborates issuer identity. Exact mailbox plus matching profile only.

BC: pegasus_pack/principal-docs/original-mapper-instruction-corpus/BC 01.DOC,
SHA256 a917a59c8ac78c4f5887f14544497899612b8503f443b7841601e66567f65544,
verified against the original with Get-FileHash. Derived source
pegasus_pack/astra_output/reports/principals/BC/sources/42b02fa3992b.txt:9
prints N.Ellahi@bakercoleman.co.uk. All five BC original letters carry this
contact. Existing corpus correspondence headers (77a6d9c09d10d2ed and
b4b2aa29864ba042) corroborate the domain; do not claim either is a new
instruction. Export contact row email is blank. Acceptance rests on original
instruction issuer contacts plus attributed correspondence, not export email.

SBL: three originals under pegasus_pack/principal-docs/commercial, all hashes
verified against source; all derived headers say Claims@smartbusinesslink.com.

| Original suffix | SHA256 | Astra extraction text |
| --- | --- | --- |
| C.SBL26174/Engineer Instruction - SBL-B0711442.msg | 853ad87368bfc718b7cf3aea7ecb6c35baedd83eb5d28bce05a606a9960778a9 | 29cbd9ce3b8e-Engineer_Instruction_-_SBL-B0711442.msg.txt |
| C.SBL26177/Engineer Instruction - SBL-B0690482.msg | 2b85c39ba910ce7580e46577aa8270f9bdcf8561d8eaa668ffaa5f0796d7d76f | 875e55244107-Engineer_Instruction_-_SBL-B0690482.msg.txt |
| C.SBL26217/Engineer Instruction - SBL-B0826835.msg | 395adb893fc81d656a8ea38ed8fe8442378ab11c0caf002c35305d1c2b802341 | 35aab03167c0-Engineer_Instruction_-_SBL-B0826835.msg.txt |

Derived files are under pegasus_pack/astra_output/extractions/text, header
line 2. Their filename prefixes are extraction IDs, not source hashes.
SBL's generic PDF samples only print a display name and the export email is
blank: neither was used to invent a sender domain. Actual reader-backed
regressions must use the retained MSG originals, not trust derived text alone.

## Requested-work evidence and classification limits

Reuse existing document profiles and their actual original source excerpts.
Do not infer case type from sender or all-purpose words such as report.

- PCH explicitly distinguishes URGENT NEW INSTRUCTION (Connexus Audit Report)
  plus Audit Report needed / original engineers report, from CREDIT REPAIR -
  Engineers Inspection Request. Its generic footer also says arrange inspection;
  that footer must not override an explicit Audit request or create ambiguity
  by counting generic boilerplate as another independent request.
- AX originals title New inspection request for Collision Engineers (51030).
- ALS originals request an urgent desktop inspection; image-based assessment
  is not automatically Triage.
- BC/BLACK/KBS/OAK/RJS/YML original letters explicitly request inspect/examine
  and a report. QCL requests an engineer report plus examination; preserve QC
  Law principal versus Complex Reports addressee.
- MP originals explicitly say arrange to inspect the above vehicle.
- SBL originals say URGENT NEW INSTRUCTION and request inspection/report.
- DFD originals say Engineer instruction request with principal and vehicle
  identity; inspect the accepted profile and source context, do not invent
  Audit/Triage tells that do not exist.
- FW current body says New INSTRUCTIONS. The old Astra .msg text copies
  contain only headers; do not use them to claim body recall. Current
  Top15InstructionCorpusTests.FairwayOriginalsProduceExactCurrentInstructionFields
  reads the real five .msg originals through the production reader, proves
  current-body fields and full -01 references. PRINCIPAL_DOCUMENT_MAPS.md
  FW section explicitly excludes quoted old instructions in current image/
  estimate correspondence.

QDOS's existing precise Triage and Inspection/Audit/InspectionAndAudit rules
remain authoritative. There is no evidence that every other principal sends
the same generated Triage templates. Unknown or competing current requests
stay Unidentified, while technical/OCR failure remains retryable.

## Focused proof

Existing Top15InstructionCorpusTests and provider Core policy suites provide
hash-bound originals and exact expected fields. Extend caller-backed tests
to route + extraction + matching + allocation, asserting principal, same
destination on replay, ready/not-ready outcome, unknown/conflict holds and
PCH original-report separation. Preserve existing QDOS tests. Root alone owns
build/test execution; no tests, scripts, cloud writes or corpus edits occurred
during this preparation.

## Local source-path reconciliation

The nested ALS/YML paths above are evidence-registry locations, not current
filesystem paths. Read-only inventory locates the same named originals at
corpus/New Inspection Instruction.eml and
corpus/FW LETTER OF INSTRUCTION - HD4021.eml. Use those existing immutable
files for the actual-reader tests, subject to the recorded SHA256 assertion;
never copy/recreate an email to satisfy a historical path. The first nested
Test-Path checks returned false; this was corrected by rg --files, not treated
as missing evidence or a permission blocker.

## Live existing-case check — 7 September 2026

Root performed a permitted read-only estate SQL query and confirmed the actual
application database is `pegasus`, not `dbpegasus`. It returned no Cases.
This is root-observed live evidence, not an inference from seed data or an
agent-run test. There are consequently no existing non-QDOS Cases requiring
case-match index reprojection for this activation. Do not add a backfill,
migration, alternate read path or compatibility machinery for absent rows.
Future allocated Cases use the one generalized existing index projector.

## Actual acceptance caller failure — 7 September 2026

Root's first real-source SQL run reached accepted ALS route, typed draft and
Inspection classification, then allocation returned no Case. TRX stdout names
InvalidDataException at CaseDataSnapshotFactory.AddExtractedValue: accepted
field Claim number has no unambiguous source provenance. The factory joins
review fields by QDOS display names, whereas thirteen current profiles bind
Claim reference and Incident date to the same typed draft fields. MP/YML bind
Vehicle make and model to draft.VehicleMake. All fifteen profile draft
constructors and ProviderInstructionPolicy.ReviewFields were inspected.
PCH additionally binds claimant mobile telephone, else home telephone, to
ClaimantContactNumber. This is a production acceptance defect, not a reason
to weaken the case-origin/provenance checks or change the original fixture.

InstructionFieldEngine.FieldDefinition owns printed names and party/reference
roles; IInstructionFieldRoles carries those roles into retained analysis.
Neither defines a canonical case-data join. CaseDataFieldNames is the single
existing canonical key vocabulary, currently internal in Infrastructure.
Root authorized moving that owner to Core and a pure InstructionReviewField
method resolving current fields to those keys (no serialized derived member).
Factory must consume that identity, keep source candidates/locators unchanged,
and refuse unresolved/conflicting/duplicate provenance. It must not introduce
another label table in Infrastructure or default to staff confirmation.

The same run's MP representative is the original MP PDF 01.pdf, SHA256
79097baeec1eac46bb9a34afe67945d398df93a621857179c793f2cff5d5d3f4.
The scan has no embedded profile; it needs OCR. Supplied source
astra_output/reports/principals/MP/sources/6ca905773ea2.txt names that exact
source SHA and contains page-1 OCR text; text-file SHA256 is
bf3ebed1dbca26fd20fe4b6ffa15737da8d6844ba91bf10deab859f1c47748d6.
Use that immutable hash-bound OCR output through the existing Core OCR-read
mapping, explicitly attributed as supplied corpus evidence, not a fresh Azure
provider response. Preserve the scan and all expected fields. No original or
OCR file is modified or committed; no synthetic email wrapper is introduced.


## Focused second-attempt diagnosis — 7 September UTC

Root correction build passed (56.38s, zero warnings/errors), Core mapping9
passed; Integration5 had3 PASS/2 FAIL. MP RequiresOcr was false and genuine
ALS initial state was Review rather than fixture's NotReady. No assertion was
removed to turn these into passes. The same bytes were inspected read-only
with the already-built reader/PdfPig under PowerShell .NET10, no build/test.

MP source has no embedded text, /Rotate270, a3507x2480 full-page raster and
crop841.68x595.2. PdfPig reports its rotated image Top0/Bottom841.68. Existing
Coverage reads those non-axis-aligned properties and compares to unrotated
CropBox: resulting coverage0. Reuse installed PdfPig CropBox.GetVisibleBounds
and GeometryExtensions.Normalise/Intersect; keep80-character/0.8 thresholds.
[PdfPig CropBox source](https://raw.githubusercontent.com/UglyToad/PdfPig/v0.1.15/src/UglyToad.PdfPig/Content/CropBox.cs)
and [geometry source](https://raw.githubusercontent.com/UglyToad/PdfPig/v0.1.15/src/UglyToad.PdfPig/Geometry/GeometryExtensions.cs)
verify their coordinate contracts. Current dev PdfOcrQualification owns only
anonymous Type3 text-map evidence, explicitly not scan geometry. It has no
second scan helper to reuse; reader file is unchanged between this base and
dev. Keep that distinct qualification owner untouched.

Actual reader plus InstructionEvidenceImages.Select on the four immutable
originals yielded ALS4, YML18, FW0, SBL0 selected assets. ALS has images-cvd.pdf;
YML has vehicle/report photographs; FW only inline signatures; SBL only a
1128x191 banner. Pin exact selected counts and Review/Review/NotReady/NotReady
in actual allocation assertions, and inspect persisted completeness as well.
One diagnostic command used an incorrect custody enum namespace and printed
invalid empty counts; it was discarded, corrected to Core.Intake, and rerun
with Stop-on-error. The4/18/0/0 counts are from the successful corrected call.

Independent pre-review identified two actual correction gaps: a conflict on
PCH's unused phone alternative must not veto the already-selected unique typed
source; and QDOS's old blanket nested-message classifier exclusion contradicts
the now-proved original boundary. Root authorized both bounded corrections.
Keep selected-source conflicts/duplicate matching-source refusals. QDOS must
reuse CurrentInstructionContent across classification and Audit evidence,
retaining body-only/Triage and chaser/reply predicates. The intermediary OCR
question was resolved without change: durable processing always begins OCR
from retained ScannedPdfPages, regardless of initial route disposition.


## Combined-source failure diagnosis — 8 September 2026

At combined head b47d8cc27aeba391e6cd650db3dc30d382f7e06e, root build
passed (117.28s, zero warnings/errors), classifier Core53 passed, Integration14
had12 PASS/2 FAIL. These are not repeats of earlier source failures.

The genuine YML HDUK01 PDF's reader output has an isolated uppercase issuer
header and a closing mixed-case issuer signature. Existing YML SignatureRegex
is case-insensitive and Match(text) takes the HEADER, which precedes Dear, so
Fields yields nothing. Actual read-only invocation of the already-built reader
and extractor showed all typed fields absent both at current time and UnixEpoch;
there is no matcher clock issue. The older Astra-derived text puts the header
on an email-address line, hiding this difference from its five Core fixtures.
Fix only the existing YML closing-signature search to start after Dear. Keep
that same genuine PDF and assert exact labelled identity plus no match when
the closing signature is removed by a structural negative probe.

The other failure is SHA text representation: ProcessIntake.cs:90 persists
Convert.ToHexString (uppercase), while the fixture pins original bytes with
ToHexStringLower. Decode both hexadecimal values for byte equality and also
assert exact persisted receipt-to-snapshot hash equality. No production hash
normalization or storage change is warranted.


## Genuine mail currentness and ALS row boundaries — 8 September 2026

Root's next correction build passed (23.09s, zero warnings). Focused five
checks had four PASS and one FAIL: all fifteen original typed-key profiles,
QDOS/YML Settings and actual pegasustest Administration capture passed; the
four-email loop then failed on YML InstructionDraft at line68. Its route was
correctly Accepted/YML, not null. The unchanged HD4021 message currently asks
for comments on a third-party report. Attached PDFs are a fee note and reports;
the original instruction exists only two messages deep in quoted history.
Selector NotApplicable, Unclassified and no new Case are therefore correct.
Root approved correcting this false-positive fixture, retaining that original
as a negative. Do not promote quoted history or fabricate an envelope. A local
.eml text search found only this YML message; pack's47 email filenames and the
five original HDUK PDFs provide no separate initial YML envelope. YML genuine
mail allocation is not yet proved; the fifteen-original PDF proof is separate.

Read-only calls to the already-built production reader, selector, selected-
content classifier and match extractor confirmed ALS/FW/SBL each Accepted,
Selected and Inspection with usable claim keys. FW and SBL yield their labelled
identity. ALS's current DOC has client Name and vehicle label rows separated
by double tabs in its main text; the existing newline-only ALS predicates miss
them. The binary reader already documents its cell/row mark convention and
last-paragraph-only cell limitation. Do not broaden that reader. Extend only
ALS's existing row starts to accept newline or the double-tab row boundary,
never a single tab into the owner/third-party column. Its original distinguishes
Mr Martin Neilly/K40NLY/Vauxhall/Mokka X Elite Nav Ecotec S/S from owner Kathleen
Neilly and third-party PX11OJA/Skoda. Preserve source labels and candidates.
Pin these actual fields, not conditional null-skipping assertions. Retain the
same original/hash. No new parser, framework, role fallback or provider call.
