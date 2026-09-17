# Six-shard SQL CI — proposed review addendum

Review date: 16 September 2026.

**Status: proposed additions, not applied changes or operator approval.** Read alongside the supplied `PLAN.md` and `PROGRESS.md`. This does not replace their agreed implementation or create permanent repository policy.

## Scope and evidence boundary

The proposed approach remains six SQL runners, 15 concrete Case feature classes, a 45-minute job limit, four concurrent test classes per runner, whole-class allocation and `Category!=Corpus`. Do not introduce a duration scheduler, shared mutable fixture, dependency upgrade, production change, timeout increase or duplicate local full-suite run in this slice.

The inspected committed PR head was `e4fc0ea05762aab67dd6ec3c8aaf35a280d5f2aa` on PR #764. The supplied handover describes an additional uncommitted Windows worktree delta. The available Pegasus MCP exposed a different, clean Linux checkout at `32f8679d3695e0dcab8f310a1c20f8b129d20190`, not that Windows worktree. Consequently this review does not certify the uncommitted split, its accessibility changes, compilation, discovery or execution.

Existing CI artifacts from run `35083151345` were downloaded and inspected without running Pegasus. The accompanying `Pegasus_SQL_CI_Baseline_Evidence.json` contains archive/member hashes, counts, result comparisons and limitations.

| Original shard | Assigned cases | Retained result evidence |
| --- | ---: | --- |
| 1 | 860 | 858 passed; two `NotExecuted` result records; result display names match the assignment exactly. The job still ended cancelled. |
| 2 | 757 | Discovery and assignment only; no completed TRX. Execution is unverified. |
| 3 | 732 | 732 passed; result display names match the assignment exactly. |

The three discovery lists are identical and contain 2,349 unique display names. Their assignments form an exact partition. All 188 original CaseDetails results passed; those cases span 127 method names. Six Case display names truncate string arguments. These are original-run observations, not candidate acceptance.

## A. Clarify what each verification step proves

The existing `Invoke-TestShard.ps1 -VerifyPartition` reads discovery and assignment text files, not TRX result files. The execution path compares the TRX `total` counter with the assigned count and returns the test process exit code. Preserve the exit-code handling, but do not describe assignment equality and a total count as complete execution-identity proof.

Proposed acceptance has three separate statements:

1. **Allocation:** each discovered case is assigned exactly once; every shard enumerates the same complete inventory; classes remain intact.
2. **Results:** every assigned case has one corresponding result, with no missing, extra or duplicated identity. Compare multisets, meaning identities and their occurrence counts, rather than counts alone. Use a stable within-run identity; do not assume display names are always unique or complete.
3. **Outcome:** the test process and required jobs succeeded. Account for approved skips separately; an assignment, a completed result record or an uploaded artifact cannot turn an aborted or failed run into a pass.

Keep the valid empty-shard case when classes are fewer than runners. An empty assignment must be proven by the inventory, not inferred from a missing result file. A globally empty discovery remains an error.

Ensure stale local artifacts cannot satisfy a fresh attempt: use an attempt-specific artifact directory and include source/run identity in the manifest. For a non-empty assignment, missing or malformed results fail execution verification.

## B. Make preservation evidence complete and reproducible

Do not implement the plan's “full theory arguments” check as display-name comparison alone. Six of the 188 original Case names contain truncated arguments, including rows of `TheActionsMenuOffersTheItemsTheStatePermits` and `TheRailLayoutAndFoldedPanelsArePaintedFromTheirCookies`.

Retain a source-bound old-to-new mapping with method signature, original owner, intended owner, complete typed theory values, skip/trait metadata and method-body preservation evidence. For inline data, source/compiler-aware extraction can preserve values hidden by display formatting. If another data source is encountered, verify its complete generated values rather than treating its display text as equivalent.

Combine that mapping with actual runner discovery and result evidence. Static preservation alone is not discovery; discovery alone is not execution. Normalizing the owner name is legitimate for this relocation, but must not normalize away changed argument values, assertions or attributes.

For this frozen baseline and a pure split, expect 2,349 non-corpus cases: 188 relocated Case cases and 2,161 unchanged others. Expect 127 Case methods, 15 Case classes and 200 total classes, up from 186. These are migration-specific checks, not permanent fixed suite-size limits. Account explicitly for legitimate changes if the base or candidate changes.

Expose a sanitized, portable copy of the mapping and its generating procedure in retained PR evidence. A path under one worktree's `.git` directory is not an independently reproducible handover by itself.

## C. Prove isolation and actual overlap

The current `IntakeWebApplicationFactory` already creates an instance-specific database, GUID-named temporary directory, time provider and ephemeral data-protection keys. Preserve those protections. The database template optimization already exists; do not replace isolated per-test databases with a shared mutable database to gain speed.

Review helper extraction for mutable static state, singleton recorders shared across tests, process-wide environment/current-directory/default-culture changes, unfinished background work, disposable ownership and cleanup after failure. Immutable shared helper definitions or the existing synchronized template initialization are not equivalent to shared mutable test state.

The integration project already copies `xunit.runner.json` to its output directory. Verify the built copy and effective runner settings rather than adding duplicate project configuration. Keep the four-test conservative scheduling behavior; xUnit v2 normally serializes tests within a collection and uses a separate collection per class. A shared collection could silently serialize the new classes again.

Run the agreed 15-class focused check together on the granted host slot. Retain timestamps demonstrating overlap between different new classes; a serial pass does not exercise their new concurrency. Verify the explicit SQL trait on each owner: `Category!=Corpus` alone would not detect a missing `SqlServer` trait.

## D. Make six-shard success enforceable

Confirm the merge requirements cover all six SQL jobs and partition verification, or use a stable required aggregate check that fails unless all required work succeeds. Preserve legitimate path-based skips, but do not accept failed, cancelled or unexpectedly skipped SQL work for a build-relevant change.

The repository ruleset listing returned an empty list, while classic `dev` branch-protection inspection returned HTTP 403. This is **not** proof that the branch is unprotected. Required-check enforcement remains an operator verification item; no protection settings were changed.

## E. Add focused diagnostics and regression coverage

The old shard-2 artifact and log do not establish which remaining class caused the timeout. Keep its cause unresolved rather than assigning it to CaseDetails, which ran on shard 1.

Consider enabling xUnit diagnostic messages and `longRunningTestSeconds` for the next candidate run. This reports long-running work; it is not a hard termination setting. Keep logs bounded and record which database setup path was used. `LocalDbTemplateDatabase` already falls back to per-test migration after template failure or for the external-data-source path; a fallback must not be mistaken for a scheduler regression.

Extend the proposed three/six-shard regression with negative cases: inconsistent inventories, missing artifacts, duplicate/missing/extra assignment identities, equal-count wrong result identities, incomplete results and stale-attempt evidence. Exercise six-runner snake allocation with enough classes to cross both direction changes: 13 or more fixture classes covers two full rows and a partial third. Include tied class sizes and reordered input.

Add a cheap consistency assertion for the three workflow shard-count owners. This is sufficient for the present change; a new dynamic-matrix framework is unnecessary.

## F. Record performance without broadening the experiment

Retain the 45-minute job limit. Budget setup, discovery, execution and evidence retention separately; a test-step deadline should leave time before the enclosing job deadline. Retain discovery/assignment evidence before long execution where practical, with final result retention separate. Post-cancellation upload is useful when it succeeds, but cannot manufacture a completed TRX.

Report per-shard queue delay, setup/build, test and upload time; longest SQL job; whole required-check completion time; total SQL runner-minutes; outcomes and slowest observed classes. Runner-minutes are a resource-use measure, not a quoted monetary bill. State missing timing evidence rather than substituting zero.

The composite build action already caches NuGet packages and performs locked restore plus a full Release solution build on each runner. Six runners repeat that setup six times. Measure this overhead before proposing build-once/artifact distribution as a separate follow-up. Defer duration-weighted allocation until complete, comparable timings show it is necessary.

## Documentation corrections and handover

Use “188 discovered test cases” rather than “188 parameterized rows”; the set includes ordinary non-parameterized tests. Keep the distinction between original-run evidence, author-reported static preservation and unverified candidate execution.

Refresh the PR body: the previous run completed with cancellation/timeouts, not a pending status. Preserve its separate, outstanding application-performance acceptance items; faster CI is not evidence of faster production pages.

Resolve the ownership note for `SendPageRendersItsChoiceInReviewAndWithEngineer` against its assertions; Workflow is the plan-consistent owner for lifecycle/EVA behavior. Do not alter assertions just to fit a folder. The original `SendToEvaRendersInReviewAndWithEngineer` comment also still says Review-only despite its broader test data; reconcile the comment with the approved behavior, not the other way around.

Record PR head, base, checked-out SHA, run/attempt, test-assembly hash and manifest hashes. The original run checked out synthetic merge `fae2100585e133ea2b8218db4a103e8c82b8c878`; GitHub comparison found no file differences from the PR head. This is a provenance clarification, not an original-run source mismatch.

Do not treat this addendum or the handover's idle-slot snapshot as a workload grant. Re-read current coordination state, obtain the canonical grant, freeze the reviewed source, perform the agreed focused verification, then use remote CI for the full suite. Leave `1609sprint/` uncommitted unless separately authorized and preserve the Markdown placement gate.

## Primary evidence references

Repository files below were inspected at commit `e4fc0ea05762aab67dd6ec3c8aaf35a280d5f2aa`:

- `.github/workflows/ci.yml`
- `scripts/Invoke-TestShard.ps1`
- `scripts/Test-TestShard.ps1`
- `tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj`
- `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`
- `tests/Pegasus.IntegrationTests/LocalDbTemplateDatabase.cs`
- `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`
- `.github/actions/dotnet-build/action.yml`

PR: `https://github.com/collisionengineers/pegasus/pull/764`

Original CI run: `https://github.com/collisionengineers/pegasus/actions/runs/35083151345`

Official framework/platform documentation consulted on 16 September 2026:

- xUnit parallel execution: `https://xunit.net/docs/running-tests-in-parallel`
- xUnit runner configuration: `https://xunit.net/docs/config-xunit-runner-json`
- GitHub Actions workflow syntax: `https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax`

Artifact identifiers, SHA-256 hashes and analysis results are in the accompanying evidence JSON. No new Pegasus test run, repository mutation, merge, release or deployment was performed for this review.
