# Open questions — SIMPLI-007

- [x] **Delete the C# gate or move it to a tooling project?** Decided 2026-08-17 (planner, claude-code): **delete**. The interface has no user, the registration has no consumer, the only "caller" is a test that proves the registration, and the script already performs the manifest-kind/schema, evidence-hash, dataset-hash and source-revision checks. A tooling project would add a solution/project for a stale checklist. Reviewer to confirm.
- [x] **Keep the capability roster as an executable check?** Decided 2026-08-17: **yes, derived — not hard-coded**. The Core roster (117 IDs) is stale in both directions against `docs/capabilities.md` (demands retired `DOC-06`; misses 15 alpha rows: AI-09, EVAL-01–05, INT-28, INT-32, MAIL-20, MCP-01–04, MCP-06, OPS-22; the register has 131 rows at `0.1.0-alpha.1`). The script reads the register's rows for the target version and checks the manifest covers them; the register stays the single source. `ExternallyCompletedCapabilityIds`/`ReleaseGateIds` semantics (OPS-10/24/25 as external evidence; release gate names) are carried over as script data with the stale OPS-24 carve-out corrected against `capabilities.md:196` ("Required and accepted").

## Parked (explicitly deferred)

- Stale evidence file list at `Invoke-QdosAlphaAcceptance.ps1:505-514` (three non-existent test files hashed) — pre-existing; fixed in passing only if it sits inside a function this ticket already edits, otherwise noted in the PR.
