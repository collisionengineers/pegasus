# Plan — TICK-042: INT-28 automatic image/instruction matching

## Approach

Reconcile the ticket with the matching implementation already shipped on `dev`; do not add a duplicate matcher or loosen its fail-closed predicates. The reviewed forward and reverse paths already enact the linked FRD: one eligible pre-report Case, preserved identities, exact-only reverse matching, and staff control for every ambiguity.

## Governing docs

- **Meets `docs/frd/frd-02-intake-and-source-identity.md`** — the reviewed paths require a unique eligible candidate, preserve Image-intake and Case identities, record an automatic association through the durable mutation store, and abstain for ambiguity, contradictions, report-delivered cases, staff leases, and existing associations.
- **Respects `docs/frd/frd-06-vehicle-and-engineering-evidence.md`** — scan-time near-match completion is constrained to registration; reverse pairing is exact-only once the Image-intake identity is immutable.
- No governing document is modified and no new ADR is required.

## Steps

1. Verify the current forward and reverse pairing callers and their durable one-shot write against the linked FRDs.
2. Run the focused ImageIntake Core regression suite and retain its result; do not represent the bounded integration timeout as a pass.
3. Reconcile this ticket as already shipped rather than creating a no-op worktree, empty commit, or PR. If independent review is requested, supply the research and test result as the review brief.

## Verification

- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~ImageIntake" --verbosity minimal` must pass.
- Source inspection confirms both directions: `ImageIntakeAutomation` handles qualifying image scans and `AcceptIntake` invokes `ImageIntakeCasePairing` after new Case acceptance.
- No code diff is produced solely to satisfy ticket mechanics.

## Risks / open questions

- The wider ImageIntake/Vrm integration subset exceeded the 120-second local bound without a final result. It is follow-up verification only; this ticket must not claim it passed.
- Broadening matching, applying near-match completion after registration, or auto-resolving ambiguous/contradictory records is expressly out of scope.
