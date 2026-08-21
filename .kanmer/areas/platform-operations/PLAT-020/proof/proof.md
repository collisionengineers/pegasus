# Proof — PLAT-020 (verified on deployed release 16, 2026-08-21)

Type: command-log. Deployment evidence bundle: [[DELIV-015]] proof.

- Migration `20260821100623_GrantImageIntakeLifecycleUpdates` applied to the production DB by the release-16 efbundle run; live head readback = that migration.
- `sys.database_permissions` readback: `pegasus_web_runtime_role` and `pegasus_worker_runtime_role` each hold GRANT SELECT/INSERT/UPDATE and DENY DELETE on `ImageIntakes`; worker holds GRANT INSERT/SELECT and DENY DELETE on `VehicleLookupRequests` (the companion #493 grant this ticket's acceptance depends on).
- Stranded-work recovery observed live: within one reconcile tick of the deploy, the Worker inserted 3 `VehicleLookupRequests` (V2MTM, MD22DDU, DE23XKP) and all 3 produced real `VehicleLookupObservations` (MERCEDES-BENZ/FORD/AUDI) — no recurring permission failures; the worker inbox poll state stayed fresh throughout.
- Review record: the migration census (IntakePersistenceIntegrationTests) and bootstrap grant matrix were missing this migration; fixed in review (commit bded467e) before merge; CI green (the earlier sql-integration-coverage failure was this real gap, not a flake).
