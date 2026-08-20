## Plan — TICK-022 (EXT-03) — retrospective backfill

**Approach:** reconcile the board with the shipped implementation; no new source change. Run the existing contract test, confirm the two real callers (staff UI, MCP), confirm the release-12 grant fix, and record the operator drag-and-drop residual honestly rather than blocking on it.

**Steps taken:**
1. Verified `EvaBundleSchema` 13-field ordered schema and deterministic manifest against `EvaBundleContractTests` (7/7 passed, 2026-08-20).
2. Confirmed both real callers (`Download.cshtml.cs`, `AssessmentMcpTools.cs`) use the identical Core path.
3. Confirmed the grant migration is present at production release 13's SHA (`2325ed4a`).
4. Recorded the operator drag-and-drop acceptance as a named residual, not a blocking gap, per the capability's own text in `docs/capabilities.md`.

No governing document is modified; FRD-07 already owns this behaviour.
