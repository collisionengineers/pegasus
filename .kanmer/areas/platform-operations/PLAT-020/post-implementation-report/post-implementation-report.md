# Changes

- Added an append-only SQL Server migration granting Web and Worker UPDATE on ImageIntakes.
- The migration validates both runtime roles, revokes only its own grants on downgrade, and leaves DELETE denied.
- Added exact effective-permission assertions for both roles.
- Preserved concurrently merged PR #493, which already supplies Worker VehicleLookupRequests INSERT and the 2601/2627 exception filter.

# Governing docs

FRD-05 is met by enabling deployed image lifecycle/custody callers. FRD-06 remains satisfied through the already-merged vehicle grant. ADR-0007 deployment approval remains unchanged.

# Verification

Release build passed; AzureSqlRuntimeRoleMigrationTests passed 10/10. Concurrent full-suite execution hit unrelated shared LocalDB timeouts.

# Risks and follow-ups

Production migration and exact stranded-work replay remain approval-gated and are not performed by this PR.

# Verify on merged main

Run migration-role tests, Release build, then—after approval—verify sys.database_permissions and production backlog recovery.
