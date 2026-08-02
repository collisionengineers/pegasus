# NOW — updated 2026-08-02

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (max 1)

- Roadmap sequencing session: answer the three questions under "First production journey and release sequencing" in [docs/open-decisions.md](docs/open-decisions.md), then fill in Path below.

## Next (max 3)

- Fix committed-migration test expectation: `CommittedMigrationCreatesTheSqlServerSchema` expects a migration list ending at `20260729199000_RuntimeRoleReconciliation`, but the committed stream now includes `20260730203141_ThirdPartyVehicleEvidenceAndRemoveBootstrap` and `20260730203833_RemoveDormantOpenIddict`.
- Fix `ProjectReferencesFollowTheModularMonolithDirection` (architecture tests): csproj `ProjectReference` paths use backslashes, so the test fails on Linux while passing on Windows CI.

## Waiting (max 3 — each line names its unblock condition)

- Isolated RPO/RTO recovery exercise — blocks any second production release ([operations](docs/operations.md#production-recovery)).
- Obsolete predecessor vault purge — platform-scheduled 2026-08-09, no action unless it fails.

## Path (the current walking skeleton, ordered, ≤10 lines)

- (Defined in the roadmap sequencing session — see Doing.)

---

Roadmap: [docs/capabilities.md](docs/capabilities.md) · Questions: [docs/open-decisions.md](docs/open-decisions.md) · How-to: [docs/operations.md](docs/operations.md)

Rules: this file is touched only in the commit that starts or finishes a piece of work; the caps are enforced by deleting lines, not growing sections; the date is bumped whenever it is touched. No other file may contain a "current status" or "next steps" section.
