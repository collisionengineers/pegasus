# B04 phase 2b-ii/iii-a — Glass's gateway and credential-page corrections (2026-09-07)

## Commits on `task/pegasus-v1-casework`

- `5bb535f3c` — Glass's Repair Estimate gateway: `GlassMvaClient`,
  `GlassRepairEstimateGateway` (Core `IGlassRepairEstimateGateway`),
  `EfGlassRepairEstimateCaseAuthority` (stands on `CaseMutationGuard`),
  `GlassRepairEstimateOptions` (`Glass:*` keys, HTTPS origins only);
  `GlassRepairEstimateGatewayTests` (60). Identities persist before side
  effects; provider state is one protected blob (purpose
  `Pegasus.Glass.Session.v1`); undeterminable outcomes stay `Unknown` on the
  account's live slot; Save & Exit retains XML + PDF as custody occurrences
  `glass-estimate:{sessionId}:xml|pdf` and imports a source-labelled Draft
  through `IImportRawEstimate`. Canonical account key travels unchanged.
- `d5643c537` — A review round 2 (PR 672 comments 5563912142, 5563946503):
  test-side `AccountKey` mirror removed (opaque constants); cross-store key
  proof `TheKeyTheSessionStoreKeepsIsTheOneTheCredentialStoreMinted`; real
  registered-store HTTP proof
  `TheRegisteredStoreReplacesRefusesAStaleVersionAndClears`;
  `GlassCredential.Title` = `Glass's repair estimate credential`.

## Evidence

- Standalone (Windows, PowerShell 7, Release): build 0 warnings / 0 errors;
  Gateway + Persistence + XmlParser suites 140 PASS / 1 FAIL — the failure is
  the cross-store test, which needs A's registered credential store (A04 host
  gap, recorded, not stubbed).
- Shared ref `97849cc73` + this B delta in the isolated `v1-combined` tree
  (`git apply`, never merged, never pushed): build 0/0;
  `GlassCredentialAdministrationWebTests | GlassRepairEstimatePersistenceTests
  | GlassRepairEstimateGatewayTests | ProductionCompositionTests`
  123 PASS / 0 FAIL / 0 SKIP, 2m25s, exit 0, TRX `v1-b-glass-gateway-ref.trx`.
  `025c60dd7` differs from `97849cc73` only in C's intake analysis files.
- Reported on PR 672 (comment 5564291784) with the composition lines for A
  and five shared-contract gaps (verbatim callback query, resume authority,
  estimator URL, no abandon signal, occurrence vs document ids).

## Not composed yet

Registration of the gateway, case authority, options and the `glass.mva`
named client lands with 2b-iii-b (Case page launch/resume handlers and the
callback page), so no registered-but-unreachable service ships.
`DependencyInjection.cs` is Foundation/A-owned: the lines are handed over as
a patch, as for 2b-i.

## Helper

`b-work/glass4` in `../pegasus-worktrees/v1-casework-glass4`, squash-merged;
worktree and branch removed. Older integrated helper worktrees (b02–b07,
blockers, cursor) removed for disk; their branches remain local.
