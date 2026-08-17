# Plan — DELIVE-001

## Chosen approach

Stabilize each reported lane at its owning non-UI boundary without weakening product assertions or editing the UI-divergent pressure-test source.

1. Update the Worker deployment-plan validator to require the current one-to-one Web replica envelope, and make the architecture-test subprocess assertion report exit code, stdout, and stderr. Do not retry deterministic contract failures; only retain bounded process timeout handling.
2. Add a test-local bounded retry for SQL Server deadlock victim error 1205 around each deliberately parallel allocation retry. Preserve two genuinely concurrent callers and all single-aggregate assertions; do not change production transaction policy.
3. Remove QDOS pressure from the pull-request repository-check workflow and register a separate Windows workflow with nightly schedule plus manual dispatch. Keep the exact script, 15-minute cap, revision binding, and evidence upload. Update the runbook and operations snapshot to state this is recurring diagnostic evidence, not a PR gate or capacity claim.
4. At the document-extraction parser's resource-limit boundary, check caller control immediately before committing a resource-limit terminal outcome so already-requested cancellation wins. Keep resource limiting unchanged when cancellation is not requested.
5. Add the repository root temporary plan, execute repeated focused tests (20 iterations per formerly flaky contract where locally feasible), run workflow/document validation, then run locked restore and Release build plus focused solution tests.

## Why this approach

It addresses the observed causes directly: stale validation before the rogue-setting assertion, an expected SQL deadlock victim under intentional concurrency, hosted-runner pressure variance, and equal-rank terminal outcomes whose first result currently wins. It avoids production SQL changes, weakening assertions, and the UI-owned `CapacitySoakTests.cs`.

## Governing docs

No PRD, FRD, or ADR currently owns CI mechanics; the ticket is explicitly marked `docs_todo`. This implementation changes repository verification and current operational evidence only. It follows `docs/engineering.md` evidence tiers and updates `docs/runbook.md` plus `docs/operations.md` for the pressure-lane schedule. No product behavior or architectural boundary is introduced.

## Proof

- Worker contract rejects the injected rogue setting and validates the one-to-one replica envelope repeatedly.
- Parallel allocation recovery passes repeatedly against SQL Server while still proving one aggregate.
- Cancellation race passes repeatedly; existing resource-limit tests remain green.
- Workflow syntax and documentation-link checks pass; QDOS pressure is absent from PR CI and present in scheduled/manual workflow.
- Release build and focused suites pass, with exact commands/results in the post-implementation report.

## Risks and mitigations

- Deadlock retry could hide an application defect: retry only SQL error 1205, cap attempts at three, and retain final exception.
- Scheduled pressure could silently disappear: keep manual dispatch, artifact upload on every run, and document its evidence status.
- Cancellation precedence could weaken limits: check control only immediately before recording a limit; an uncancelled request remains resource-limited.
- UI overlap could reappear: recheck both UI copy trees before editing and never modify `src/Pegasus.Web/**`, `design/**`, `.stitch/**`, or `CapacitySoakTests.cs`.
