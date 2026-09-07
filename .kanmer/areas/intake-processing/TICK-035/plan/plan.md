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
