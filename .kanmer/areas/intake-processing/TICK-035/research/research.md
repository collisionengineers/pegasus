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
