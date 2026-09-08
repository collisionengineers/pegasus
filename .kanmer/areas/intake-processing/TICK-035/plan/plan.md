# Plan — TICK-035: evidenced principal routes

## Objective and starting state

Supported incoming/staff-forwarded instructions identify the actual Principal,
use the existing extraction profile and evidenced requested work type, then
reach the existing Case/Triage/Unidentified destination without duplicate
allocation. A domain-only patch is not completion.

Read origin/dev 1d972f05c0f10c2ecf804f271a4fd3155242f1ef. Inputs:
research/research.md@f3369b0f00533929 and files/files.md@5a028b603b1550b2.
Current operator brief and EPIC-014 supersede historical route deferral.
Root approved generalizing the existing route/classification/selector/match
owners as one coherent bounded change. V1 foundation already seeds all
fifteen Principal codes; do not add a second seed list or schema.

Wait for PLAT-028's announced merge, then obtain the execution packet and
fresh recorded branch/worktree from latest origin/dev, including INTK-061.
No changes in shared checkout or PLAT-028 worktree.

## Governing documents

Update FRD-02 intake/source identity, FRD-09 email-route activation and
current-architecture after production wiring. Preserve separately credentialed
Provider API contract. FRD-04/Settings may state exact mailbox metadata for
YML after PLAT-028 merge. Do not edit operator-notes or claim live deployment.

## Required changes and ordered steps

1. Generalize/rename QdosMailRoutePolicy itself. One immutable active
   identity catalog contains the research's exact domains plus YML's exact
   mailbox; no generic Gmail or QDOS compatibility alias. Retain sender
   cardinality, staff-forward cleaning and original consistency. PCH
   connexus/ensurance entries remain typed intermediaries and require one
   agreeing PCH instruction profile. Repoint provisional sender and Settings
   metadata callers to this same owner.

2. Replace ProcessIntake's fixed extractor binding with the existing
   fifteen-profile InstructionExtractionPolicySelector. Require route/profile
   agreement, not document identity alone. Scope actual current instruction
   content using existing source/locator identity, separately from attached
   reports and historical quoted messages. Retain every other asset for
   existing custody/Audit paths. Do not copy provider extraction grammars.

3. Generalize the existing classification owner/contract to receive
   established principal/profile context. Preserve QDOS's precise generated
   Triage, Inspection, Audit, combined and reply predicates. Additional
   profiles use only researched current request tells: explicit inspect/
   examine or accepted DFD/FW instruction templates; PCH explicit Audit and
   credit-repair requests remain distinct. A generic inspection footer must
   not override the specific Audit request. Unknown/competing work types
   stay Unidentified, not blanket Inspection. Reuse standalone Audit evidence
   evaluation with actual instruction attachment identity rather than a
   QDOS-only title check. Missing ordinary fields leave a valid Case Not ready.

4. Generalize the current match-key owner and pass provider identity through
   its existing contract/projector. Keep QDOS claim-tail grammar only for
   QDOS; use other providers' typed role-labelled fields and full references,
   including FW -01 suffix. Share one normalization path between incoming/
   declared facts and Case index writes. Conflicted fields cannot supply
   reliable keys. Retain provider-scoped query, contradiction eliminators,
   unique association and Created-in-error redirection, not a new matcher,
   score or generic parser. Change the five known projector callers only as
   required by that contract. Root's read-only estate check must establish
   whether old non-QDOS cases lack index rows; if present, agree bounded
   reprojection through this same owner before activation, not speculative
   compatibility machinery.

5. Replace DI and all known constructor/type/metadata callers; preserve
   declared Provider API principal/type and create-only existing-match
   rejection. INTK-061's orchestration owns durable retries. Update canonical
   docs and affected test consumers, including Browser inventory. Search all
   src/tests, not a convenient test subset.

6. Freeze diff and deliver exact final filters to root. Root alone builds,
   tests and captures. Preserve every failure/rerun; no test weakening.

## Expected files and exclusions

files/files.md is the bounded map: existing Core route/classification/
match-key contracts and owners, ProcessIntake/selector, index projector and
five known stores, DI, retained-mail projection, Principal Settings metadata
and relevant canonical docs/tests. Renamed Core owners stay under existing
Intake directories.

No MailboxIntake, Triage formal-linking, engineer/report workflow, Worker
grant, migration/schema, cloud/credential or source-corpus edits. No package,
flag, rule editor, mailbox onboarding, new extraction service, queue or host.
Original content is never fabricated or committed.

## Acceptance and focused verification

Extend existing fixtures, not a test framework: exact identities for all
fifteen; malformed/spoofed suffix/multiple originals/route-profile conflict
holds; PCH intermediary and instruction/report separation; FW current body
versus quoted request; actual Principal, Case type and ready/not-ready
destination from genuine originals; replay and unique-match association
produce one destination; ambiguous matches remain explicit. Preserve
existing QDOS and Provider API assertions. Selection alone is insufficient.

Root-only locked restore/Release build once frozen, followed by:
Core filter FullyQualifiedName~ProcessIntakeTests|FullyQualifiedName~MailRoutePolicyTests|FullyQualifiedName~MailClassificationPolicyTests|FullyQualifiedName~CaseMatchPolicyTests|FullyQualifiedName~EvaluateIntakeCaseMatchTests|FullyQualifiedName~InstructionExtractionPolicySelectorTests|FullyQualifiedName~DefinitiveIntakeCaseTypeTests.
Supply final names if renamed before root runs.

SQL callers reuse CaseMatchIntegrationTests, InlineForwardedMailRouteTests and
focused destination methods in existing intake fixtures. Original-reader
proof reuses Top15InstructionCorpusTests with PEGASUS_REFERENCE_PACK_ROOT
pointing to the immutable local pack; required originals must not silently
skip. ALS/YML current local corpus originals have verified matching hashes.
Do not run whole corpus or whole Browser merely for this ticket.

If Settings markup changes, root captures only its existing route with its
Web cohort and verifies snapshot/catalogue once. Worker runs git diff --check
and small static checks only. Final combined CI/exact-merge proof remain
root/reviewer owned.

## Failure rules and stop

Stop/report unsupported source grammar, missing genuine fixture, new schema/
grant requirement, shared-file conflict or actual index reprojection need.
No permissive fallback, invented default or relaxed assertion.

Preparation stops when required docs are ready; no take/worktree/source edit
until PLAT-028 merge and execution assignment. Execution later stops at a
frozen diff ready for root focused verification, then independent review.
Never self-review, merge, deploy or mark Done.

## Execution refinement approved by root

Fresh base is 522e67f270ab4d6086d9fba04095988db3598888; PLAT-028 and
INTK-061 are merged, Done and released. Read research version411e3d10a118729b:
live pegasus database has zero Cases, so do not add any index backfill.

The existing classifier and match interfaces already bind WorkProviderCode.
Use one generalized implementation of each, instantiated per existing
registered extraction profile; derive composition from those registrations,
not a second provider list. Their evaluator/projector/five store callers may
remain unchanged when the existing contract suffices. Route metadata remains
one Core identity owner. This is a simpler equivalent implementation of the
planned context propagation, not a parallel matcher.

QDOS's document profile does not cover accepted body-only Triage and all
current route-anchored extraction shapes. Retain its evidenced existing
extractor/classification behavior when no competing document profile matched;
a conflicting or ambiguous profile fails closed. Additional principals still
require an agreeing selected profile before automatic instruction extraction.
Tests must preserve body-only QDOS and cross-provider-conflict coverage.
Root explicitly approved this refinement; no blanket Inspection default.

## Actual-caller correction approved by root

The initial integration attempt failed on genuine ALS allocation because the
existing CaseDataSnapshotFactory joined the typed draft to QDOS display labels.
Move the existing CaseDataFieldNames vocabulary to Core; expose one pure method
on InstructionReviewField that resolves its current accepted field binding to
that vocabulary. Remove Factory's redundant extraction-display-name parameter;
retain original review names/candidates/source spans and the exact typed draft
values. Missing or ambiguous provenance still prevents allocation. Existing
address-to-mode and explicit-mileage-unit derivations retain their exact source
field. PCH mobile/home contact selection must join uniquely to the typed draft
value, not choose an ambiguous first source or duplicate its priority policy.
No second canonical namespace, parser, mapping service, schema or JSON member.

Keep all four genuine email originals. Add accepted Case origin/source-hash,
field-value and exact candidate-source attribution assertions, plus contextual
allocation failure messages. Keep the same first MP scan; assert OCR need and
consume its supplied page-1 OCR text only after checking both original and text
hashes via the existing internal OCR-read mapper. Integration friend access is
limited to this existing test assembly; no new public production API. This is
supplied reference OCR evidence, not a newly executed Azure OCR call.

After author static checks, root reruns only the two failed integration cases,
new pure mapping cases and affected existing acceptance/provenance regressions.
The earlier 224/12 Core and 21/2 integration results remain in the report; no
repeat of unchanged 224 passing Core tests is needed.


## Second focused correction approved by root

1. Replace existing reader Coverage geometry, not scan thresholds: use the
   installed PdfPig crop visible/display bounds and rectangle normalization/
   intersection. Expose only the existing geometric calculation internally for
   direct non-domain quarter-turn/outside-crop probes; actual MP reader test
   still must qualify the unchanged hash-bound original before supplied OCR.
   PdfOcrQualification is Type3-only and remains untouched.
2. Remove unused canonical-source conflict veto. Select exact typed source
   uniquely; keep selected conflict/missing candidates/duplicate-source refusal.
   Use direct CaseDataSnapshotFactory tests in the existing fixture class.
3. Route QDOS body/document/Audit classification through the existing
   CurrentInstructionContent boundary, preserving generated grammar and
   reply/chaser/body-only Triage behavior. Add proved nested original and
   arbitrary nested negatives; no domain-data envelope fabrication.
4. Pin ALS/YML/FW/SBL original selected image counts4/18/0/0 and respective
   Review/Review/NotReady/NotReady outcomes; assert persisted factual
   completeness and existing source provenance, not merely a different string.
5. Freeze after static checks. Root runs only failed2 integration originals,
   new geometry/provenance probes, affected QDOS classifier cases and existing
   unreadable-source boundary. Preserve all previous results; no repeated
   unchanged224 Core cohort or live OCR call.


## Starting state — accepted dev integration before next verification

Root authorized committing the current frozen source as an unverified author
checkpoint, then a normal no-edit merge from freshly fetched origin/dev into
the same recorded TICK-035-principal-routes branch. This is not a PASS, PR,
Review move, release or permission to absorb other tickets.

Original author parent: 522e67f270ab4d6086d9fba04095988db3598888.
Fetched integration input: 19e6f523bf6760cab39104b4dca3674b0ac8a512,
including PLAT-072 d442366787d452da22d36719272d4eb79dc1afde and current
PLAT-065 keyless Worker OCR infrastructure. Workspace/branch/common Git
identity matches the lease. Board/worktree census shows this sole TICK-035
workspace; historical foreign claims and merged/verifying peers are preserved.

Retain accepted dev source through normal merge. Resolve only mechanically
clear overlap and change new completeness fixture constructors to the accepted
two-fact shape. Stop and report ambiguous conflicts or unplanned scope. No
rebase/reset/force, no foreign workspace edit, no heavy command. Record author
checkpoint and resulting merge parents after Git completes; root tests once
on the combined frozen source, retaining all earlier failure evidence.


## Combined-source minimal correction

Root's combined verification found a real YML signature-boundary defect and a
hash-casing-only fixture defect. In the existing YML Fields iterator, prove Dear
then search the issuer closing signature after that boundary; preserve missing
closure refusal. Same HDUK01 original must yield its exact labelled identity;
a structural probe without its closing signature must produce no match keys.
Compare original/snapshot SHA bytes and exact receipt/snapshot persisted hash,
without changing the production representation. Root reruns only the two failed
original-source tests, then focused Settings capture. No broader build/test
cohort or new source sample. Retain all earlier failed attempts.


## Genuine-mail expectation and ALS caller correction

Root approved the actual-currentness correction: retain HD4021 as a genuine
YML report-correspondence negative, not a new instruction mined from quoted
history. Parameterize the original four-input test using ReferencePackTheory,
so each original reports independently and one failure cannot hide another.
Each still runs two occurrences and replay. YML must prove accepted route,
Unclassified/no draft/no Case and zero allocations; ALS/FW/SBL must create one
Inspection Case and uniquely associate the next occurrence, retaining pinned
images/readiness/origin/field provenance.

The actual ALS DOC revealed newline-only row predicates skipping visible
claimant/vehicle values. Extend only existing ALS Party/Vrm/Make/Model/Category
row-start predicates to accept newline or two tabs; never single tab into the
adjacent owner/third-party column. Keep the shared reader unchanged. Pin exact
supplied claimant, reference, registration, make/model and party separation,
including a structural probe removing the claimant column while retaining the
owner and third-party. No inferred fallback or fabricated domain data. The
other current originals' typed fields also become explicit expectations.

Author performs only bounded source checks, then freezes. Root reruns these
four independently reported original cases only; already-passed53 Core,
fifteen-original PDF and Settings captures are not repeated. Record missing
independent YML initial-envelope coverage honestly. Preserve every failed run.


### ALS refinement: structured vehicle columns, not ambiguous tab inference

The empty-client-cell negative shows two tabs can also mean adjacent empty
cells. Therefore do not extend vehicle row regexes to that ambiguous boundary.
Use existing table locators and InstructionFieldEngine.SourceStructure: prove
one Clients Vehicle header beside Third Party Vehicle, bind only label column1
to value column2, preserve the original value cell and rename the label using
the existing ALS vehicle field bindings. Missing/duplicate client value cells
produce no candidate; columns3/4 never supply it. Existing labelled newline
patterns remain for actual plain/PDF instruction documents without table cells.
For a document with structured cells, those cells exclusively own vehicle
reading. No shared binary reader changes. Only claimant Party's existing
boundary accepts the proved double-tab before the Name paragraph; its adjacent
owner Name remains excluded. Root approved this refinement before source edits.


## Final bounded provenance correction approved by root

Root's four-case run had3 PASS (FW/SBL/YML) and1 FAIL (ALS persisted locator),
after a114.31s clean build. EfIntakeReceiptStore's current three-member
PersistedFieldCandidate discards Core Locator and RawValue. Retain these same
nullable members through its existing JSON mapping and record; keep the
persisted ALS locator assertion. Add the two located/unlocated serializer
roundtrip cases in existing CaseDataCompletenessPersistenceTests.

Independent source review also proved multiple physical instruction documents
sharing table1 collide in SourceStructure's Dictionary<int,...>, overwriting
one document's cell before field ambiguity handling. Root approved changing
only that existing key to (DocumentIdentity, Table), reusing the selector's
physical-document identity function. No table renumbering, new parser, new
index or new vocabulary. The same ALS genuine test adds a structural conflict
probe with distinct current supplied VRMs, equal table/row/column numbers and
distinct physical source labels. Both candidates must survive as conflict and
no typed registration. Original source files/hashes stay immutable.

Freeze both corrections together for root source review and focused verification;
no author builds/tests. Preserve previous3 PASS, every prior failure and exact
TRX identity. No repeated unaffected cohorts or new PR until root results.


## Final canonical paragraph and accepted-dev integration

CASE-049 is Done and its claim/worktrees released. Root authorizes normal
merge of accepted dev3a5ce645cfc0872d7a4324c6818497360c39cca4 into the
clean verified checkpoint7fcd4c662c5457024d1d20c5fe0c02c842e98a63.
Preserve the accepted native engineer handoff and all its source/tests/docs;
stop on ambiguous conflicts rather than resolving another owner's behavior.

Only additional author edit is FRD-01's incoming-cancellation paragraph:
remove the obsolete focused-alpha/QDOS-only association limit. Describe the
actual principal-scoped matching owner: existing QDOS correspondence predicates
remain; non-QDOS matching relies on supported current instruction profiles and
unambiguous typed keys, not blanket correspondence support. Cite current FRD-02
as behavior owner. Preserve current-envelope boundaries, QDOS cancellation
recognition, manual authorized Case-state changes and permanent history.
No new product behavior, source, test, configuration or policy is authorized.

Check that TICK-035 runtime sources/tests match the verified checkpoint, that
all accepted CASE-049 source remains identical to dev, and that the final author
increment contains only this paragraph. No new builds/tests for that doc-only
increment. Record merge parents/equality scope honestly, not a full-tree test
reuse claim. Then commit [skip ci], push one PR to dev, update ticket/report,
check fresh gates and move Implementing to Review for an independent reviewer.
