# Research — INTK-063

## Question and authority

The current operator brief and FRD-02 require image-first and instruction-first
automatic linking where one eligible Case is proved, with manual correction
preserved. This is a supplemental EPIC-014 fix, not a transfer of the historical
[[TICK-042]], [[INTK-039]], [[INTK-060]] or [[INTK-061]] claims. Those records
and the original 218-ticket roster remain unchanged. Root owns a frozen,
one-ticket supplemental run; this worker prepares documents only.

Read-only audit baseline baafa29e0f7002b8235aa43bf333f5d9bb172828; relevant
paths rechecked on accepted dev 19e6f523bf6760cab39104b4dca3674b0ac8a512.
TICK-035's current branch incorporates that dev input but remains unreviewed;
execution must start after its merge/release and a fresh overlap census.

## Actual caller defects

- Core/Intake/AcceptIntake.cs:121 skips pairing on duplicate acceptance and its
  recoverable catch discards failure. The formal Case already persists.
- Core/ImageIntake/ImageIntakeCasePairing.cs:60 lists registered images and
  pairs per item; its per-item catch silently discards failure. Existing
  SyncMergeAfterLinkAsync already retries the deterministic merge/custody work.
- ImageIntakeAutomation.cs:88 returns from already-registered processing after
  receipt-decision synchronization. Its TryAssociateAsync catch records only
  Activity tags; registration survives but the failed association is not retried.
- Worker/IntakeFunctions.cs:178 StagedArtifactReconciliationFunction retries
  grouped *unregistered* images, custody and Unidentified work, not registered
  Awaiting-instruction pairing. EfIntakeSubmissionGroupStore requires multiple
  members and NeedsSorting, so cannot recover these single/registered cases.
- EfImageIntakeCaseCandidates is inside Persistence/EfImageIntakeStore.cs:1296.
  It reads immutable InstructionDrafts.VehicleRegistration. Accepted current
  corrections update CaseDataFields and CaseMatchIndex (EfCaseDataStore), so the
  old registration remains matchable while the actual current one is missed.
  Image candidate lacks principal identity although ImageIntakeRecord already
  stores optional PrincipalId and its summary exposes PrincipalCode.
- EfIntakeMutationStore.AutoLinkAsync already uses ExecuteSystemWork, a
  serializable transaction, current Case version, active-staff-lease yield and
  ImageIntakeLifecycleRules.IsCaseEligibleForAssociation. Any existing
  ManualAssociation, including inactive staff-unlinked history, blocks automatic
  relinking. Preserve this and enforce current identity within the same write.
  A read-side unique match alone cannot prevent a newly competing Case race.

## Reuse and bounded shape

Extend IImageIntakeCasePairing/its existing implementation and store queries,
not a new queue or processing framework. CaseMatchIndex holds current accepted
VRM/provider identity; reuse its adapter/query ownership and existing Core
eligibility/VRM comparison. Keep exact reverse-direction VRM matching;
the existing recognition near-miss rule must not silently become reverse
pairing. Known conflicting principal refuses; unknown image principal is not
invented. Existing deterministic auto-link and merge operation identities,
custody outbox and immutable histories remain owners.

Add bounded oldest-first eligible recovery to the existing timer. Query
eligibility before the cap: exclude merged/staff-closed, deliberately unlinked
and no-match rows; include already-linked Awaiting items needing merge. A
permanent nonmatch must not starve later actionable rows. Single and grouped
origins both participate. Report failure counts/reasons through existing
results/logging while continuing unrelated items; no empty catch.

## Evidence needed

Existing ImageIntakeCasePairingTests directly prove matching but not wake-up
recovery. Extend existing ImageIntakePersistenceTests and grouped fixtures for
both orders, lost link/merge wake-up recovered by only the scheduled sweep,
duplicate acceptance and registered replay, corrected VRM, known-principal
contradiction, ambiguity, staff unlink, live lease, stale version, post-report
and closed states. Prove actual restricted Worker calls with existing SQL
role fixture; no broad grants. No build/test or live provider call ran here.
