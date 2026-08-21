# Question

Which deployed callers lack SQL authority, and what is the narrowest durable repair?

# Findings

- `20260729199000_RuntimeRoleReconciliation` grants Worker only SELECT on `VehicleLookupRequests`, while `EfVehicleWorkflowStore.EnqueueAutomaticAsync` inserts a request and external-work row. Live evidence showed INSERT denial and three due cases.
- `20260803071539_ImageIntakeRegistration` explicitly treated `ImageIntakes` as immutable and granted SELECT/INSERT, but later lifecycle and custody implementations update those rows from both Web and Worker. Live evidence showed UPDATE denial and one expired custody lease.
- `EnqueueDueAsync` catches every `DbUpdateException`, so permission failures are misclassified as benign concurrent duplicates.
- Runtime grants are append-only migrations and exhaustively asserted by `AzureSqlRuntimeRoleMigrationTests`.
- DELETE denials are an invariant and need no relaxation.

# Implication

Add one append-only reconciliation migration, narrow SQL duplicate detection to 2601/2627, and prove the actual role-backed caller paths. Production migration and replay remain explicit-approval actions.
