# Harden flaky CI tests

Kanmer: DELIVE-001

## Scope and sequence

1. Align the Worker deployment validator with the current one-to-one replica envelope and preserve complete subprocess diagnostics.
2. Retry only SQL Server deadlock-victim error 1205, at most twice after the first attempt, inside the deliberately parallel allocation-recovery test.
3. Move QDOS pressure unchanged from per-PR CI to a nightly/manual evidence workflow; update operating documentation.
4. Give already-requested document-extraction cancellation precedence at the resource-limit decision boundary.
5. Repeatedly run each formerly flaky contract, then run locked restore, Release build, and affected suites.

## Ownership and exclusions

The active UI revamp owns `src/Pegasus.Web/**`, UI tests, `design/**`, `.stitch/**`, and divergent copies of `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs`. None is edited. The planned document-extraction paths were byte-identical in both UI copy trees at implementation start. KANMER-001/KANMER-002 documentation cleanup remains excluded.

## Acceptance

- The rogue Worker setting reaches and satisfies the exact-census rejection assertion.
- Parallel SQL retries still converge to one case aggregate under bounded deadlock recovery.
- QDOS pressure remains scheduled, manually runnable, revision-bound, bounded, and evidenced without blocking PRs.
- Cancellation and resource-limit focused tests are deterministic and retain their distinct uncancelled behavior.
- Workflow/document checks, Release build, and focused suites pass.

## Supporting Kanmer documents

Research, file impact, the executable checklist, and post-implementation evidence are owned by the DELIVE-001 ticket documents.
