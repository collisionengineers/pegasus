# Proof — CASE-010 (verified on deployed release 16, 2026-08-21)

Type: command-log. Deployment evidence bundle: [[DELIV-015]] proof.

The ticket's own proof conditions, both met live:

1. **`sys.database_permissions` shows the INSERT grant**: `pegasus_worker_runtime_role` holds GRANT INSERT + GRANT SELECT with DENY DELETE on `VehicleLookupRequests` after the release-16 efbundle applied `20260821095500_GrantWorkerVehicleLookupRequests`.
2. **A row within one reconcile tick**: within one minute of the deploy the CASE-008 sweep inserted 3 `VehicleLookupRequests` rows (V2MTM, MD22DDU, DE23XKP at 14:34:34Z) for the live cases carrying Fact registrations — the rows the deployed estate had silently failed to enqueue for a day. All 3 produced real `VehicleLookupObservations` (MERCEDES-BENZ / FORD / AUDI) and the QDOS26006 assessment page prefilled from the observation.

The narrowed duplicate-key-only catch is deployed with the same SHA; the reconciliation function continued reporting success with the poll state fresh throughout (46 s at readback).
