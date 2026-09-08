# Plan — INTK-063

## Objective and starting state

Recover the already-required image-to-formal Case association, in both arrival
orders, with current identity and no lost retry. Current read-only baseline is
accepted dev 19e6f523bf6760cab39104b4dca3674b0ac8a512; execution takes fresh
dev only after TICK-035 ownership clears and root approves this plan. Root owns
a frozen one-ticket supplemental run; do not expand the original 218 roster.

## Governing documents

FRD-02 image lifecycle, source identity and reasoned reversal; EPIC-014 context
and current operator brief. Historical owners remain linked, not absorbed.

## Implementation

1. Replace original-draft candidate reads with current CaseMatchIndex identity.
   Carry candidate principal alongside VRM and enforce no known-principal
   contradiction. Reuse Core image eligibility and existing VRM rules; reverse
   pairing still requires exact normalized registration.
2. Add bounded oldest eligible pending queries in the existing image store.
   Include single/grouped Awaiting rows and linked-but-unmerged rows. Exclude
   dynamically nonmatching/manual-unlinked/merged/staff-closed rows before the cap so old
   ineligible rows cannot starve actionable work. Do not persist a permanent
   no-match exclusion: a later Case or corrected identity must make the row
   eligible again.
3. Extend the existing pairing owner to sweep those candidates. Recheck complete
   current candidate uniqueness/identity inside automatic-write transaction,
   using existing query ownership context-bound if needed. Preserve current
   version, Staff lease, archival/post-report guards and deterministic operation
   keys. Do not convert a deliberate unlink into another automatic association.
   The link and merge are separate operations: also recheck the CURRENT receipt
   association equals the intended Case, known principal and Case eligibility
   inside the existing merge transaction. An image lifecycle version alone
   does not detect a staff unlink/relink between automatic link and merge.
4. Wire registered-image replay, acceptance replay and existing staged-artifact
   reconciliation timer to the same owner. Reuse SyncMergeAfterLinkAsync and
   pending custody dispatch. Report recoverable failures and continue unrelated
   rows, leaving durable eligible state for the next existing sweep.
5. Update canonical FRD/as-built statements and extend existing fixtures only.

## Verification

Root alone runs locked restore/build and focused Core/SQL filters after freeze.
Tests prove both orders; unique/exact VRM, current correction, known-principal
conflict, ambiguous candidates, closed/post-report, active lease/stale version;
manual unlink/relink between link and merge (no stale-target custody); failure
  after link before merge recovered by a timer tick alone;
duplicate acceptance/registered replay; single/grouped eligibility and oldest
nonmatch starvation; restricted Worker actual association/merge transaction.
No new test host, soak, full corpus or live email/provider call.

## Risks and stop

Case-currentness must be checked in the write transaction, not inferred from a
pre-query or only target version. Permission/schema changes require concrete
evidence and root approval. Preserve original foreign claims/corpus. Preparation
stops here for root plan review: no take/branch/worktree/source edits. Execution
later stops at frozen focused checks, then independent review.
