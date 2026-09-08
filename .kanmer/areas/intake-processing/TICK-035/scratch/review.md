---
kind: review-attestation
pr: "692"
head_sha: "006b556ff3e995c5a2aac0cdb2a4d508ada5be23"
verdict: needs-changes
reviewer: "pack_reconcile"
independent: true
plan_hash: "a29f6fa6041932cc"
ticket_updated: "2026-09-08T01:41:18.145Z"
board_sha: "4d49eff654aac814882475437be2dd4a2c88c9ec"
expected_reviewers: ["pack_reconcile"]
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Prior preflight F-P01: retained receipt candidates lost Locator and RawValue."
    disposition: fixed
  - id: F-002
    severity: major
    summary: "Prior preflight F-P02: equal table numbers from separate physical documents collided."
    disposition: fixed
  - id: F-003
    severity: major
    summary: "Non-QDOS automatic email matching extracts and consumes keys even when no agreeing instruction profile was selected."
    disposition: open
---

# TICK-035 consolidated independent review

NEEDS CHANGES at the exact PR692 head above. Root assigned pack_reconcile as
sole expected reviewer; this reviewer's exact-head public COMMENT
[5136452778](https://github.com/collisionengineers/pegasus/pull/692#pullrequestreview-5136452778)
settles that set. Author intake_audit and reviewer are distinct agent roles,
despite sharing a GitHub account. No author/self-review, build/test or merge.

Read the whole current packet: research e916e4ac7aa58fd6,
plan a29f6fa6041932cc, files e9ca25221233b6ab,
report fdc4ceb245498e5c and checklist d71e2989ca0790d1; live gates,
EPIC-014, governing FRD-02/09 and the changed FRD-01 paragraph.
No open-questions document exists and the live resolved-questions gate passes.
Read original preflight7074751553d263d8; it remains untouched and is not
retrospectively labelled PASS. This is the first consolidated exact-PR review,
not a repeat of that bounded source preflight.

## Source scope and accepted work

Inspected the 58-path final diff against accepted dev3a5ce645c, including all
production changes, known consumers, canonical docs and actual-source tests.
The normal merge636555159 joins verified author7fcd4c662 and accepted
CASE-049 dev3a5ce645c; final006b556 changes only the FRD-01 paragraph.
The report's 52 TICK runtime paths and 22 CASE runtime paths are disjoint;
the source-scoped earlier evidence is not a newly executed final-head test.

One renamed Core route owner supplies the exact fifteen-principal identities
and Settings metadata. YML remains one exact mailbox, not Gmail; PCH's two
intermediary domains require an agreeing profile. Sender cardinality, exact
domain equality and proved-forward consistency remain. The selector groups
current physical documents, rejects profile mixing and excludes historical
or unproved nested content. Separate report negatives do not veto a current
instruction. DI derives classifiers/matchers from existing extractors.

Explicit work-type classification remains separate from identity. QDOS's
generated Triage/Inspection/Audit/combined/correspondence predicates survive;
non-QDOS requires supported inspect/examine or DFD/FW tells. PCH Audit is not
overridden by its generic inspection footer, and competing credit-repair/Audit
requests fail closed. No blanket Inspection, generic correspondence grammar,
new seed list, matching framework, provider host, schema, grants or dependency.

Canonical case-field vocabulary was moved, not copied. The pure field-name
join preserves original candidate evidence and refuses selected conflicts and
duplicate matching attribution; unused PCH phone alternatives do not veto the
typed selected source. The installed PdfPig visible/rotated rectangle APIs fix
the original MP scan's coverage without lowering thresholds or duplicating
the separate Type3 qualification owner.

The narrow Razor review found no material UI issue: the existing semantic
detail-list now says accepted e-mail identities and reads Core's catalog.
No actions, form authority, CSS/JS/library or focus behavior changed. Root's
actual Settings tests and scoped snapshot/catalogue evidence are recorded;
no manual visual or all-principal visual capture is claimed.

## F-001 and F-002 — fixed, prior history preserved

Both are fixed by7fcd4c662c5457024d1d20c5fe0c02c842e98a63 and unchanged
in final006b556. F-001 maps existing Locator/RawValue through both directions
of EfIntakeReceiptStore's same candidate JSON record. Two located/unlocated
roundtrips assert exact candidate equality and printed SourceValue. The
original persisted ALS column2 locator assertion remains unchanged.

F-002 keys SourceStructure's existing dictionary by the existing
DocumentIdentity plus Table, preserving actual locators without renumbering.
The ALS structural two-document probe retains equal table1/row4/column2,
distinct physical source labels and contradictory supplied registrations;
both candidates survive, HasConflict is true and typed registration is null.
Missing/duplicate client cells and missing paired header still refuse values;
owner/third-party columns cannot fill claimant vehicle facts.

## F-003 — open; root-settled bounded remedy

ProcessIntake.cs:789-809 distinguishes conflicting profiles from no profile.
When selection is NotApplicable for a non-QDOS accepted sender,
extractionPolicy is null, but the code passes CurrentInstructionContent and
the accepted route to caseMatchEvaluator before its later no-profile refusal.
PrincipalCaseMatchPolicy.cs:30-44 invokes that provider's Extract directly,
which is an extractor, not the signature selector. DurableIntake.cs:985-1015
then applies a recorded UniqueMatch without requiring a draft or allocation.

A concrete source-derived structural example is the genuine ALS instruction
with the required Vehicle Model: profile signal absent while its separate
Our Reference/client/registration fields remain. The selector refuses the
profile but the typed extractor can still produce unique existing-Case keys.
This review did not run that probe and does not claim an observed wrong
association in the genuine corpus. New Case allocation remains refused.
The defect is the bypass of this ticket's accepted selected-profile boundary
before automatic non-QDOS instruction extraction/matching, not proof that all
possible typed correspondence association is inherently unsafe.

Root inspected the same caller chain and settled the intended remedy:
require an agreeing selected profile before automatic non-QDOS email matching.
Preserve the existing QDOS correspondence exception and declared Provider API
matching. Use the existing ProcessIntake selection/guard; do not invent a
universal correspondence grammar, another matcher or a new policy layer.

Acceptance for this one remediation class: one real existing-Case destination
negative retaining usable unique typed keys but missing the profile must
produce no automatic association/allocation; selected-profile positive and
existing QDOS/declared API behaviors remain proved. Reuse existing focused
fixtures; no broad rerun or new test infrastructure. Plan/files alignment
precedes author edits. No second material finding was found in the full census.

## Runtime evidence and honest limits

Reviewer ran no tests. Root is sole heavy verifier. Independently read six
retained TRXs and recomputed hashes; exact failure counts remain:
combined classifier53/53 PASS; combined Integration12/14 with2 FAIL;
YML/hash/Settings4/5 with1 FAIL; four actual mails3/4 with1 ALS FAIL;
final structural Core10/10 PASS; final Integration exactly3/3 PASS
(one genuine ALS plus two JSON roundtrips), not five.

Final hashes:
- tick-035-table-identity.trx:
  B7F1EDEBC41DA2DBA0D48DE10A0561A18BEFA8959C3B0BC390CF53517AF117FA.
- tick-035-persisted-provenance.trx:
  20DF014FB223C516DFA34CB2F80F14DEC08F41BB967E717981B836CBF926508A.
- prior four-mail failed attempt:
  75482A9ACDCDEAB73B744027E237497744C22AA35F2244487CE08F90E90F432D.

The current report already records exactly three final Integration cases.
Root reported final Release build49.53s/zero warnings/errors and source-scoped
earlier original/profile/route/Settings passes. No repeated whole rail,
fresh final-merge test, hosted-CI green, live Azure/Glass/mail or deployment is
inferred. Earlier twelve Core inventory/version failures, ALS provenance,
MP geometry, YML closing-boundary and hash-casing failures are retained.

ALS/FW/SBL genuine emails prove one Inspection Case and repeat association/
replay with pinned readiness and exact origin/field facts. YML HD4021 is
genuine later report correspondence: accepted mailbox, no current draft/type/
Case and zero allocation. It must not mine its two-deep quoted instruction.
The fifteen standalone original profile/type/key pass is separate; the MP OCR
text is supplied hash-bound Astra evidence, not a new Azure response.
No genuine initial YML envelope allocation is claimed.

## Live facts and handoff

PR OPEN/MERGEABLE/CLEAN; same repository, head TICK-035-principal-routes to dev,
exact006b556. Dev is unprotected; active rules[], required contexts[]/checks[],
head check_runs[]/statuses[]. Aggregate pending with zero contexts is not a
required failed/pending check and is not a CI-green claim. Full GraphQL thread
page is empty with hasNextPage:false. Bot summary IC_kwDOThBrk88AAAABTHZo_g is
running with mergeGateEnabled:false, no actual finding; acknowledged as
non-gating status, not an expected reviewer or security PASS. Later actual
threads require a fresh whole-file attestation. Bound board tip was pushed
ahead0/behind0. No author lease heartbeat was performed.

Return the same ticket/PR/branch/worktree to Implementing for root-authorized
F-003 remediation. Preserve all fixed findings, failed attempts and original
preflight. No merge or proof rewrite. Delta review then covers this finding,
the correction and direct callers/tests; it does not reopen unrelated source.
