# Post-implementation report — TICK-035

## Status and source

Implementation is frozen for root-owned compiler and focused runtime checks.
Current combined local head: b47d8cc27aeba391e6cd650db3dc30d382f7e06e.
Root authorized an unverified checkpoint and normal dev merge before the next
build; the worktree is clean. No pushed PR or review PASS is claimed.
Final verification is pending, not PASS. Root's builds and corrected Core
checks passed; the first Integration run failed on two genuine-source paths,
recorded below. The actual acceptance defect and MP OCR fixture are corrected
and frozen for root's targeted rerun. No commit, PR, Review move, merge or live
deployment is claimed. Stop remains independent review after passing evidence.

Worktree: `.worktrees/tick-035`. Branch: `TICK-035-principal-routes`.
Fresh execution base: `522e67f270ab4d6086d9fba04095988db3598888`.
The current resumed packet may report a newer dev tip; this implementation
retains its recorded original base and has not rebased or switched worktrees.
The shared dirty checkout and immutable corpus remain untouched.

## Implemented behavior and actual callers

The existing route owner is renamed PrincipalMailRoutePolicy. It contains
one immutable accepted-identity catalog covering the fifteen existing
principals: exact evidenced domains, YML's exact mailbox, and distinct PCH
intermediary rules requiring an agreeing PCH profile. No general Gmail,
guessed SBL domain, suffix widening or parallel QDOS policy remains.

ProcessIntake now uses the existing fifteen-profile selector. Signals must
belong to one current physical document. A separate report cannot poison the
instruction profile; separate attachments cannot manufacture a profile.
The route's existing sender evidence and StaffForwardBodyCleaner delimit the
proved original in a staff forward, excluding arbitrary or deeper nested
messages and older thread content. Selection never substitutes for route
identity. Conflicting/ambiguous profiles cannot extract, classify or
automatically associate. Non-QDOS instruction allocation requires agreement.
QDOS's evidenced route-bound body/Triage shapes remain reachable when no
competing profile matches.

One PrincipalMailClassificationPolicy implementation is instantiated per
existing extraction registration. QDOS generated predicates stay intact.
Other current instructions require explicit inspect/examine or researched
DFD/FW templates. PCH explicit Audit is distinct from credit repair, not
overridden by a generic inspection footer. Missing work type is Unidentified;
when unreadable scanned content could establish it, the receipt remains
OcrRequired. There is no blanket Inspection default.

PrincipalCaseMatchPolicy reuses each non-QDOS extractor's typed role fields
and preserves full references, including Fairway -01. QDOS retains its
settled claim-tail grammar. Incoming and Case-index writes share the same
normalizer. Existing EvaluateIntakeCaseMatch, index projector and all five
Case-data writers already carry provider-bound policies and remain unchanged.
The root's read-only query found zero Cases in the actual pegasus database,
so no speculative index backfill was added.

Production flow is unchanged in shape:
ReceiveIntake -> ProcessQueuedIntake -> ProcessIntake -> existing
provider association / retained-mail association -> IAllocateIntake.
The fifteen extraction registrations supply both classifier and matcher
instances. No new queue, worker, schema, grants, package, rule engine or
provider API contract was introduced. INTK-061 remains the durability owner.

## Files and known consumers

Core implementation:
- Rename DirectProviders/Qdos/QdosMailRoutePolicy.cs to Intake/PrincipalMailRoutePolicy.cs.
- Rename DirectProviders/Qdos/QdosMailClassificationPolicy.cs to Intake/Classification/PrincipalMailClassificationPolicy.cs.
- Rename DirectProviders/Qdos/QdosCaseMatchPolicy.cs to Intake/CaseMatching/PrincipalCaseMatchPolicy.cs.
- Intake/InstructionExtractionPolicySelector.cs, Intake/ProcessIntake.cs,
  Intake/IntakeContracts.cs and Classification/MailClassificationContracts.cs:
  current-document selection and context wiring.
- DirectProviders/Pch/PchInstructionExtractionPolicy.cs: obsolete QDOS-only
  allocation comment corrected, no extractor grammar change.

Infrastructure:
- DependencyInjection.cs derives classifier/matcher instances from existing
  extraction registrations and removes the fixed QDOS ProcessIntake binding.
- Persistence/EfRetainedMailboxMessageStore.cs uses the renamed same
  provisional sender owner.

Web:
- Pages/Administration/Principals/Settings.cshtml.cs reads the one catalog.
- Settings.cshtml labels domains/mailboxes as accepted e-mail identities,
  not a domain-only label that would misrepresent YML. No controls/auth change.
  The Razor implementation skill led to the existing semantic detail-list
  element and no new UI component. Root owns the scoped capture.

Docs:
- FRD-02 and FRD-09 own current routing/profile/type/matching behavior.
- current-architecture records the actual composition without a deployment claim.
- principal-rules-and-mappings/qdos.md updates replaced owner paths/key stamps;
  historic evaluation counts remain history, not newly executed evidence.
- ADR-0020 changes only its relocated FRD link, not its decision.

Core tests:
- InstructionExtractionPolicySelectorTests: physical-document separation,
  isolated report negatives, preserved actual forwarded original, arbitrary
  nested/deeper messages and old history exclusions.
- ProcessIntakeTests: cross-principal fail closed and meaningful current
  work-type/OCR fixture expectations while preserving draft/route assertions.
- QdosMailRoutePolicyTests: exact direct/forward identities, shared mailbox and
  spoofed-domain rejection, typed intermediary/profile agreement.
- QdosCaseMatchPolicyTests: full non-QDOS reference preservation.
- QdosMailClassificationPolicyTests, QdosInstructionExtractionPolicyTests,
  DefinitiveIntakeCaseTypeTests, ReconcileUnidentifiedDestinationsTests:
  known owner rename/constructor/key consumers.

Integration tests:
- Top15InstructionCorpusTests: one hash-bound original per principal proves
  actual reader -> profile -> extraction -> explicit type -> match keys.
  Controlled reply-context probes do not claim additional genuine emails.
- QdosAllocationRecoveryTests: ALS/YML/FW/SBL untouched original email bytes
  exercise receipt -> dispatch -> real processing -> allocation -> replay ->
  unique existing-Case association. Asserts principal, type, actual Case and
  workflow state plus exactly four destinations for eight source occurrences.
  Existing retaining-mail test helper is made repeatable for this multi-message
  fixture; no production retained-message fabrication is introduced.
- OrganizationDirectoryWebTests and ProductionCompositionTests: shared identity
  metadata and registrations; RetainedMailPersistenceTests: provider-bound lookup.
- Mechanical owner/constructor/key consumers: CaseMatchIntegrationTests,
  InlineForwardedMailRouteTests, IntakeWebTestSupport, MailboxIntakeIntegrationTests,
  MultiFormatGenuineCorpusWebTests, ProviderDomainReferenceIntegrationTests,
  QdosEmailCohortTests, QdosExtractionCoverageTests, QdosIntakeWebTests,
  QdosMappingExtractionTests, QdosTriageCaseAssociationIntegrationTests,
  QdosTriageIntegrationTests and TriageFromIntakeIntegrationTests.

## Evidence and attempts

7 September 2026 UTC:
- Workspace identity, branch and common-repository readback matched the claim.
- Repeated git diff --check: exit 0; only normal LF/CRLF notices.
- Known-consumer search found no replaced production type or broken old FRD
  anchor after updates. The two remaining AcceptedDomains test-method names
  describe QDOS's actual domain subset; no obsolete property remains.
- No restore/build/test/capture run by this agent. Root is sole heavy owner.
- Exploratory reads of guessed filenames returned ordinary file-not-found
  errors; paths were located with rg and corrected. These are research lookup
  failures, not runtime tests or PASS evidence.
- Frozen read-through found the new SQL fixture's workflow string expectation
  must be NotReady rather than Cases.InitialState's not_ready. Root was alerted
  before altering the freeze. Root authorized that fixture correction together
  with the following Core failures; production source did not change.
- Root locked restore PASS, exit 0. Root full Release build PASS, exit 0,
  zero warnings/errors, 66.00 seconds.
- Root initial focused Core cohort FAIL, exit 1: 236 total, 224 PASS and
  12 FAIL, 573 ms. Eleven failures were the obsolete inventory theory
  rejecting the now-evidenced additional principal domains. The twelfth was
  the inline-forward fixture expecting old route version 4 rather than 1.
  Integration did not run after that failure.
- Corrected the inventory theory to assert each exact accepted principal,
  retaining its two intermediary NoMatch cases and all other negative tests;
  corrected the route version and workflow string fixture. Re-frozen after
  git diff --check exit 0. No production policy changed to satisfy tests.
  Root rerun will select 14 cases: the 12 failed examples plus the two
  preserved intermediary negatives in the same theory, not the unchanged
  224-test cohort. New method: EvidencedInventoryDomainsResolveTheirPrincipalButIntermediariesNeedAProfile.
  Rerun outcome and the first Integration cohort remain pending.

Root requested commands, from this recorded worktree:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ProcessIntakeTests|FullyQualifiedName~MailRoutePolicyTests|FullyQualifiedName~MailClassificationPolicyTests|FullyQualifiedName~CaseMatchPolicyTests|FullyQualifiedName~EvaluateIntakeCaseMatchTests|FullyQualifiedName~InstructionExtractionPolicySelectorTests|FullyQualifiedName~DefinitiveIntakeCaseTypeTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~GenuinePrincipalEmailsAllocateOnceAndAssociateRepeatedInstructions|FullyQualifiedName~OneGenuineInstructionPerPrincipalProvesSelectedWorkTypeAndMatchKeys|FullyQualifiedName~CaseMatchIntegrationTests|FullyQualifiedName~InlineForwardedMailRouteTests|FullyQualifiedName~ProductionProfileDrivesTriageFromTheAcceptedRouteClassification|FullyQualifiedName~QdosPrincipalSettingsDefaultToImageBasedAssessmentAndShowAcceptedDomains|FullyQualifiedName~YmlSettingsShowTheExactMailboxNotASharedDomain"
```

Root must set PEGASUS_REFERENCE_PACK_ROOT and PEGASUS_CORPUS_ROOT to the
existing immutable local roots and require no skip for the two new genuine
source tests. Do not run the full 81-original corpus/volume soak. Settings
capture scope is administration-principal-settings; the existing snapshot
owner is TestUiSnapshotTests. Actual commands/exits will replace this pending
section while preserving every failed attempt.

## Coverage boundaries and residual risks

All fifteen documents have exact recorded hashes, but original envelopes do
not exist for all fifteen in this bounded fixture. The four genuine email
families and structural route predicates are separate evidence tiers. ALS
and YML EML root senders are external/direct; a FW subject is not proof of a
CE staff forward. Existing QDOS real-forward tests plus the generic current
original Core probes cover different layers, not an invented generic email.
No synthetic MIME envelope has been wrapped around a genuine standalone
document, and no original has been rewritten or committed.

Provider APIs remain create-only and reject an existing Case according to
their existing contract. Triage/formal linking, engineer handoff, Glass,
OCR provisioning, mailbox onboarding and deployment belong to their separate
tickets. Exact-merge proof is still required after independent review/merge;
this pre-merge implementation report is not that proof.

## Actual-caller remediation and retained attempts

Root's corrected Release build PASS, 0 warnings/errors, 20.79 seconds.
The targeted 14 Core cases PASS, exit 0, 123 ms; unchanged 224 passing cases
were not repeated. The first Integration run then FAILed, exit 1: 23 total,
21 PASS, 2 FAIL, zero skips, 64 seconds. Its local TRX is
 tests/Pegasus.IntegrationTests/TestResults/tick-035-integration.trx.
One failure was the same first MP scan not matching without OCR. The other
was first ALS allocation returning no Case. TRX stdout identified
InvalidDataException: Claim number has no unambiguous source provenance, from
CaseDataSnapshotFactory.AddExtractedValue through AcceptIntake/AllocateIntake.
No easier original or new expected acceptance was substituted.

Root approved research8bf1c099ada9f0e9, files6034ec179a40c9c2 and
planc8592b394784a49d amendments before this correction:
- Move the existing CaseDataFieldNames unchanged from Infrastructure/Persistence
  into Core/Cases as the sole public key vocabulary. Three existing persistence
  consumers add using Pegasus.Core.Cases only (CaseMatchEntities,
  EfIntakeMutationStore, EfVehicleWorkflowStore).
- InstructionReviewField.ToCaseDataFieldName is a pure method, not a serialized
  new property. It maps the current typed bindings only; original names and
  candidates remain intact. No second case-key vocabulary or Infra alias table.
- CaseDataSnapshotFactory removes its extraction display-name parameter and
  joins canonical keys. PCH's existing mobile/home choice joins to the unique
  matching typed draft value; no duplicate phone priority or ambiguous first
  source. Existing exact-source address/mode and mileage/unit derivations stay.
  Missing/conflicting/duplicate attribution continues to reject allocation.
- Nine pure Core mapping cases cover current name variants and unknown roles,
  and verify no new derived JSON member. The same four real email paths now
  assert actual Case source hash/origin, typed facts and exact retained candidate
  source label/policy. Allocation failure messages name principal/file/delivery.
- The MP PDF remains original SHA79097baeec1eac46bb9a34afe67945d398df93a621857179c793f2cff5d5d3f4.
  Its supplied Astra page-1 OCR file is hash-bound separately
  (bf3ebed1dbca26fd20fe4b6ffa15737da8d6844ba91bf10deab859f1c47748d6),
  verifies its stated source path/hash, and flows through the existing internal
  AnalyzeRetainedInstruction.CreateOcrReadResult mapping. The existing friend
  assembly mechanism adds IntegrationTests, not a public test API. Attribution
  explicitly says supplied-corpus/astra-ocr/reference-v1: no Azure request,
  provider operation, response or extraction run is falsely claimed.
- FRD-02 records canonical identity plus preserved provenance. No source corpus,
  original OCR evidence, schema, provider implementation or cloud state changed.

Pending root rerun after the correction:
Core filter FullyQualifiedName~InstructionReviewFieldTests.
Integration filter selects the two failed genuine-source tests plus
CaseDataCompletenessPersistenceTests.AcceptanceSnapshotsTypedSourceProvenanceWithAutoAddedValues
and ProviderApiCaseDataSnapshotPersistenceTests to preserve the directly
changed QDOS and authenticated/staff Provider API provenance paths. New rerun
outcomes are not yet known. No unchanged 224-Core/21-Integration cohort rerun
is requested solely to repeat prior evidence. Settings capture remains pending.


## Second focused verification and source correction

Root's next Release build PASS, exit0, zero warnings/errors,56.38 seconds.
InstructionReviewFieldTests9 PASS. Integration filter5 returned3 PASS/2 FAIL
(no skip), recorded in artifacts/verification/tick-035-provenance-integration.trx.
The MP test failed its actual-reader RequiresOcr assertion. ALS now allocated
an actual Case, but its correct Review state differed from the fixture's
blanket NotReady assumption. These failures are retained, not reclassified as
passes. Root authorized read-only diagnosis then bounded corrections.

The original MP PDF has no text, /Rotate270 and one full-page3507x2480 raster.
Already-built PdfPig inspection showed its image Top0 and Bottom841.68, while
the CropBox is unrotated841.68x595.2. The old Coverage returned0. Existing
PdfPig GetVisibleBounds(rotation), Normalise and Intersect now replace only
that faulty geometry.80-character and0.8 coverage gates remain unchanged.
Current dev's separate PdfOcrQualification is solely anonymous Type3 mapping
qualification; it contains no competing scan geometry, and remains untouched.
Seven direct geometric probes cover all quarter-turns and images outside the
visible crop. No generated domain PDF or new renderer/coordinate framework.
The same hash-bound MP original still must qualify via the real reader before
its supplied OCR evidence enters the existing mapper. This is not Azure proof.
The PDF skill caused read-only text/image/rotation inspection, not PDF edits.

Already-built production reader and InstructionEvidenceImages.Select measured
ALS4, YML18, FW0 and SBL0 qualifying images. ALS includes images-cvd.pdf;
YML includes photographs in the attached vehicle reports, FW only inline
signature graphics, SBL only a wide banner. The genuine-email SQL fixture now
pins those exact counts plus persisted InstructionComplete/ImagesComplete and
Review/Review/NotReady/NotReady initial and workflow states. Originals and all
source provenance/identity/replay assertions remain unchanged.

Independent pre-review found two code defects, both fixed under root approval:
- PCH's unused home/mobile alternative conflict no longer vetoes its already
  selected unique typed source. The selected source's conflict/candidates still
  fail closed, and two equal canonical source bindings remain ambiguous. Four
  direct Factory tests cover both unused alternatives and both refusal cases.
- QDOS classification and standalone Audit evidence now use the same existing
  CurrentInstructionContent boundary as selection. Proved originals remain
  actionable while arbitrary/deeper nested chasers and old history are excluded.
  Generated QDOS body/Triage/reply/Audit predicates are retained. Three added
  structural policy cases cover Inspection, Audit with original report and
  unproved carry; none claims a fabricated genuine envelope.
The reviewer's intermediate OCR scheduling question was resolved without a
change: DurableIntake unconditionally begins OCR from retained scan candidates.

Research b00e638d0db7ea0d, files dbfee47055d146e0 and plan db5aba70a6afb19d
were written before these source edits. No restore/build/test was run by the
author; PowerShell reflection invoked already-built reader/PdfPig/selection
only on four actual originals and the MP metadata. One diagnostic used the
wrong custody enum namespace, producing invalid empty counts; that output was
discarded and corrected under Stop-on-error. Final valid counts are above.
Static read-through corrected an entity property target and an enum spelling
before freeze; git diff --check exit0. These are not test execution claims.

Source re-frozen at8 September2026 approximately00:09 UTC; no commit/PR yet.
Root rerun request (actual outcomes pending):
- Core FullyQualifiedName~PrincipalMailClassificationPolicyTests.
- Integration FullyQualifiedName~GenuinePrincipalEmailsAllocateOnceAndAssociateRepeatedInstructions|FullyQualifiedName~OneGenuineInstructionPerPrincipalProvesSelectedWorkTypeAndMatchKeys|FullyQualifiedName~FullPageRasterCoverageUsesRotatedVisibleCoordinates|FullyQualifiedName~RasterCoverageClipsImagesOutsideTheVisibleCrop|FullyQualifiedName~TypedPhoneSource|FullyQualifiedName~ACorruptDocumentIsRefusedRatherThanPartlyRead.
  Expected14 cases:2 originals,7 geometry,4 provenance,1 corrupt-source boundary.
Settings focused capture/verify/catalogue remains root-owned and pending.


## Accepted dev integration before final focused check

Root requested current dev integration before another build so tests target the
accepted two-fact completeness model, not obsolete confirmation booleans.
Unverified checkpoint2ac4696b7006527def9bff08e45d67c208ca690e retains the
source on original parent522e67f270ab4d6086d9fba04095988db3598888.
Normal merge9556650430a4c4e778af5b1ab8d0fd907f2638e0 joined that checkpoint
and freshly fetched dev19e6f523bf6760cab39104b4dca3674b0ac8a512 without
conflicts. Final local commitb47d8cc27aeba391e6cd650db3dc30d382f7e06e changes
only the new provenance probe constructor to new(true, false).

Starting-state plan263fee3359d70b73 preceded the Git operations; scratch
records exact parents and root authority. Board/worktree census was checked;
foreign historic claims and other peer worktrees were untouched. No rebase,
reset, force, push or cloud write. Source matches dev exactly in infra, scripts,
Core CaseContracts/IntakeOcr, PdfOcrQualification and migrations, preserving
accepted PLAT-072, TICK-041 and PLAT-065. git diff --check exit0; clean worktree;
origin/dev is an ancestor; no unresolved files. Runtime checks remain pending
at this combined source, and earlier failures remain above as attempt history.


## Combined head verification and final two-source correction

Root verified b47d8cc27aeba391e6cd650db3dc30d382f7e06e: Release build PASS,
exit0, zero warnings/errors,117.28 seconds; PrincipalMailClassificationPolicyTests
53 PASS. Integration14 had12 PASS/2 FAIL, retained at
artifacts/verification/tick-035-combined-integration.trx; matching Core TRX is
artifacts/verification/tick-035-combined-core.trx. The previous two failing
Integration rounds and earlier Core failures remain in this report.

The two current failures differ from those earlier causes:
- YML's genuine HDUK01 original supplied no match keys. Actual read-only calls
  to the already-built reader/extractor exposed the isolated uppercase issuer
  letterhead before Dear and the later closing issuer. SignatureRegex's first
  case-insensitive match took the header, so the existing Fields guard yielded
  no values. Current time and UnixEpoch both returned empty drafts; matcher
  clock was not the cause. Older derived Astra text has the header joined to an
  email-address line and did not expose this real reader shape.
- ALS now has the actual Case, correct images/completeness and Review state.
  Its source-hash assertion compared lowercase fixture text with uppercase
  persisted hexadecimal. ProcessIntake intentionally uses Convert.ToHexString;
  this is an assertion representation defect, not changed source bytes.

Root-authorized minimal correction (research71d44b8b2b63786e,
filesf3285c16d57a40cc, plan40c75d5b7720e6e2 amended before edits):
YmlInstructionExtractionPolicy now locates its closing issuer only after the
proven Dear boundary. No label/identity parser is duplicated. The same original
now must satisfy exact labelled YML name/reference/VRM/incident/instruction date;
a structural probe removing its closing signature still must produce no keys.
The genuine-email test compares decoded SHA bytes and separately exact
receipt.SourceHash to snapshot.OriginSourceHash. No production hash change,
new original, relaxed provenance assertion or additional test infrastructure.

Only three existing files changed after the combined head (24 additions,
2 removals). git diff --check exit0. No author build/test or external writes.
Source frozen for root rerun of the two failed genuine-source methods only;
focused Settings capture/verify/catalogue remains pending. Independent
read-only correction review of b47d8cc found both prior findings fixed and no
new material delta finding, but is not a formal exact-PR attestation.


## Current-envelope correction and ALS fields — 8 September 2026

Root's next Release correction build PASS: exit0,23.09s, zero warnings/errors.
Focused five checks: four PASS/one FAIL, recorded in
artifacts/verification/tick-035-yml-hash-settings.trx. PASS includes the genuine
fifteen-principal original typed-key test (including corrected YML), QDOS and
YML Settings, and actual pegasustest Administration default capture. The mail
loop failed at line68 on YML InstructionDraft, not its accepted sender route.
ALS reached its successful Case, source-hash and provenance checks first.
This failure and all prior rounds remain in this report.

Separate Settings evidence from root, without recapture: scoped update2 PASS;
verify2 PASS (6s); catalogue60 routes/67 prototypes/zero broken, session14491
exit0. Root-owned snapshot outputs remain in this worktree; author preserved
them and did not run a capture/build/test.

Read-only inspection of all four exact emails through already-built reader,
selector, selected-content classifier and typed match extraction proved:
- ALS, FW and SBL: accepted direct sender, matching selected profile, Inspection
  request, usable claim key. FW and SBL typed identity is populated.
- YML HD4021: accepted exact direct sender networkhduk@gmail.com, but current
  body requests comments on a third-party report. PDFs are fee note/reports;
  the initial instruction is two messages deep in quoted history. No matching
  current instruction profile/draft/type is correct, not a routing defect.

Root approved retaining that exact YML email/hash as the negative outcome,
not manufacturing a positive by reading historical instruction. A bounded
local .eml text search found no separate YML initial envelope; the pack47
email inventory and original five HDUK PDFs do not provide one. Genuine YML
mail allocation is NOT claimed. Its original PDF/profile proof remains PASS.
Two exploratory diagnostic invocations used an omitted classifier selected-
content argument or wrong result property; those incomplete observations were
discarded and corrected. A broad reflection GetTypes probe failed on unloaded
ASP.NET shared dependencies; it supplied no evidence and changed nothing.
Only the successful explicit reader/profile/current-content calls support
the observations above. No author SQL/provider operation or test ran.

Further actual-source gap: ALS binary DOC's table text flattens row markers
into tabs, hiding claimant/vehicle values from newline-only extraction.
The old conditional provenance test could pass with null claimant/VRM/make.
Root approved fixing the existing ALS owner and pinning actual source facts.
The shared binary reader documents last-paragraph-only cell limitations and
is unchanged. Claimant's current text boundary accepts the actual paired-
header row before Name, not the owner's Name across a single adjacent tab.
Vehicle values instead use existing structured cells: prove one Clients
Vehicle header beside Third Party Vehicle, bind column1 labels to column2
values, preserve original value-cell locators, and reuse the existing shared
field engine. An initial double-tab vehicle-regex proposal was rejected during
the empty-cell check because adjacent empty cells are ambiguous. No such
vehicle regex widening remains. Missing/duplicate client cells or missing
client header do not borrow columns3/4 or fall back to flattened vehicle text.

Plan4ace80f09f19ad4a records this refinement before the structured-source edit;
filescd54f2533b15aca0 gives its final same-file mapping. Existing four-original
test is now a ReferencePackTheory with ALS/YML/FW/SBL separately reported.
Every original still has two distinct occurrences and duplicate replay:
ALS/FW/SBL each require exactly one Inspection Case, unique association on the
next occurrence, pinned4/0/0 images and Review/NotReady/NotReady, actual origin
hash and exact field provenance. Name/reference/VRM/incident/instruction date
and make are asserted against supplied identities, not nullable draft output.
ALS model and distinct owner are pinned, vehicle source cells must be column2,
and empty client values, duplicate client cell and missing header are negative
probes over the same original's decoded content. YML requires accepted route,
Unclassified/no draft/no case type/no Case/no allocation on both occurrences
and after replay; its18 selected images are retained but do not make an
instruction.

Current source: b47d8cc27aeba391e6cd650db3dc30d382f7e06e plus four author files
(ALS/YML policies and the two genuine-source integration fixtures), alongside
root's Settings snapshot outputs. Author git diff --check exit0. No new
build/test, PR, push, Review or PASS claim for these corrections. Source is
frozen for root to build and run only the four separately reported cases with
FullyQualifiedName~QdosAllocationRecoveryTests.GenuinePrincipalEmailsAllocateOnceAndAssociateRepeatedInstructions.
Already-passed53 classifier Core, fifteen-original PDF and Settings captures
are not requested again. Prior attempts remain intact.


## Final genuine-mail run and provenance corrections — 8 September 2026

Root's next Release build PASS, exit0, zero warnings/errors,114.31 seconds.
The four separately reported genuine-mail cases returned3 PASS (FW, SBL and
YML report-correspondence negative),1 FAIL (ALS), zero skips,57 seconds.
The failed line180 dereferenced the retained vehicle candidate's Locator
after successful ALS typed facts, Case allocation, readiness and origin checks.
TRX: artifacts/verification/tick-035-genuine-four.trx;
SHA256:75482A9ACDCDEAB73B744027E237497744C22AA35F2244487CE08F90E90F432D.
These are root-executed results, not author execution. All earlier failed
rounds, passed cohorts and separate Settings evidence remain above.

Independent source review confirmed two actual provenance defects. Root
authorized one final bounded correction batch, and files e36d604170139e60 /
plan6e4e202f8c1c4d9f were written before the edits:

- EfIntakeReceiptStore's existing candidate JSON mapping serialized only
  Value/Source/SourceLabel, dropping Core's existing Locator and RawValue.
  Both directions and the same private record now retain all five members.
  No schema, JSON-version migration, second serializer or compatibility layer.
  The exact persisted ALS value-cell assertion remains unchanged. Two direct
  located/unlocated receipt roundtrip cases in the existing
  CaseDataCompletenessPersistenceTests assert every field/candidate member,
  exact record equality and printed SourceValue.
- SourceStructure indexed table number alone. ALS correctly grouped physical
  instructions, but flattening them into the one existing field engine meant
  a second attachment's table1/row4/column2 overwrote the first before conflict
  handling. The same dictionary now keys by the existing selector's
  DocumentIdentity plus Table. Original source labels and locators are unchanged.
  The ALS original's structural negative probe combines its decoded content
  with a second physical source label and the supplied third-party PX11OJA as
  a conflicting client value, alongside original K40NLY. This is explicitly
  a structural probe, not a second genuine envelope. It requires both distinct
  values and physical sources, identical table/row/column locators, HasConflict,
  no SuggestedValue and no typed registration. No corpus bytes were changed.

The source is frozen on b47d8cc27aeba391e6cd650db3dc30d382f7e06e plus the seven
mapped author files; root's Settings snapshot outputs are preserved. Author
read the final diff and existing structural tests and ran git diff --check
only, exit0. No author restore/build/test, new PR, push or delivery claim.

Pending root verification: build this final frozen source, rerun only the
failed ALS theory case in
QdosAllocationRecoveryTests.GenuinePrincipalEmailsAllocateOnceAndAssociateRepeatedInstructions
(principalCode: ALS), and the two new
CaseDataCompletenessPersistenceTests.IntakeFieldCandidatesRetainProvenanceAcrossReceiptPersistence
cases. InstructionFieldExtractionTests is the affected existing small Core
structural-binding cohort (10 cases). Root owns the exact commands and may
select that cohort to cover the shared dictionary change. Do not repeat already
passed FW/SBL/YML cases,53 classifier cases, fifteen PDFs or Settings capture
merely to repeat evidence. Actual outcomes remain pending; stop is source
review and root checks, then independent exact-head PR review.
