# Checklist

- [ ] Add the single Core `O` / `0` candidate generator and fail-closed result resolver with stable ordering and an explicit bound.
- [ ] Add durable intake-owned ambiguity work, attempt provenance, replay identity, schema/indexes, model snapshot, and Worker grants.
- [ ] Process each candidate through the existing `IVehicleLookupAdapter` and existing typed result contract with durable retries.
- [ ] Gate single/group image routing on terminal ambiguity resolution and pass through only one uniquely resolved registration.
- [ ] Wire the same policy at [[TICK-041]]'s real document-OCR vehicle-registration boundary without affecting embedded-text or staff values.
- [ ] Add Core and integration coverage for single/multiple positions, bounds, unique/no/multiple matches, unavailable/retry, provenance, idempotency, grouping, and route scope.
- [ ] Run and record the required simplification pass with a disposition for every finding.
- [ ] Run focused tests and the canonical locked restore, Release build, and non-Corpus test commands with exit codes.
- [ ] Verify named production callers, runtime artifact dependencies, schema, and permissions.
- [ ] Refresh `docs/current-architecture.md` only for behavior proved wired and deployed.
