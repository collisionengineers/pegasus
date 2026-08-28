# Checklist — KANMER-005

*Each box is independently observable and is completed in the ticket's
recorded worktree after CASE-024 clears the dependency.*

- [ ] [pre-review] Confirm PR 581 is merged, [[CASE-024]] is Done/unblocked,
  obtain the execution packet, and start KANMER-005 from fresh `origin/dev` in
  its one recorded branch/worktree.
- [ ] [pre-review] Add failing Core tests, then make holder identity match on
  `ActorKind` plus `SubjectId`, make issued leases carry kind, and replace
  GUID-shape holder inference with the typed descriptor.
- [ ] [pre-review] Persist, parse, project, replay, heartbeat, require, and
  clear `EditLeaseHolderKind` through the existing
  `EfCaseWorkflowStore`/`CaseMutationGuard` path without changing its lock,
  isolation, token, version, or retry behavior.
- [ ] [pre-review] Generate and review the `CaseEditLeaseActorIdentity`
  migration: nullable no-default column, exact operation-history backfill,
  unmatched full-tuple clear, column-only `Down`, model snapshot, and exact
  committed-migration census.
- [ ] [pre-review] Prove migration, rollback shape, no-holder, exact-match,
  unmatched, null/unknown-kind fail-closed recovery, and existing Web/Worker
  runtime-role permissions; add no unnecessary GRANT or constraint.
- [ ] [pre-review] Adapt CASE-024's merged shared Details/Assessment handlers
  and Triage holder display to the Core identity matcher, keeping competing or
  unidentified active leases read-only with no new copy or control.
- [ ] [pre-review] Add real SQL tests for both Staff/Automation directions,
  exact state preservation after rejected claim/write, holder renew,
  save-consumes-lease, and release-without-save.
- [ ] [pre-review] Add the synchronized separate-connection claim race and
  same-subject/different-kind valid-token negative test; assert exactly one
  winner and surface every concurrency result.
- [ ] [pre-review] Exercise real Web and MCP callers for Automation-held Staff
  refusal and Staff-held Automation begin/write refusal while preserving MCP
  schemas and CASE-024 heartbeat semantics.
- [ ] [pre-review] Run the focused Core and integration filters from `plan.md`
  after a Release build; retain every command, exit code, and first-failure
  result.
- [ ] [pre-review] Run locked solution restore, Release build, and the full
  non-Corpus solution tests with exit code `0`; inspect generated artifacts,
  migration SQL, permissions, secrets, paths, and branch-only scope.
- [ ] [pre-review] Run the independent simplification lenses, apply or
  disposition every finding in a dated `plan.md` section, write the
  post-implementation report, record reachable commits/PR, and open the PR to
  `dev` for independent review.
- [ ] [pre-review] Stop with the PR open: do not merge, deploy, write proof, or
  start another ticket.

## Progress notes

Append implementation evidence; do not rewrite completed history.
