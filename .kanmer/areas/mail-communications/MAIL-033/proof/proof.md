---
kind: proof-record
merged_sha: "cc60cffc554ced423c97a86f014f577bc05d382b"
environment: "Detached verification worktree C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-033-cc60cffc554ced423c97a86f014f577bc05d382b (git worktree --detach at PR #641 mergeCommit.oid); .NET build/test evidence from the controller's test runner in that worktree; reachability and caller checks run by the verifier from the primary checkout C:\\Users\\PGUSER\\Documents\\github\\pegasus (read-only) and GitHub (gh) for CI/PR facts."
verified_at: "2026-09-02T03:06:06Z"
result: INCONCLUSIVE
failure_class: inconclusive
attempts:
  - attempted_at: "2026-09-02T03:00:20Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-033-cc60cffc554ced423c97a86f014f577bc05d382b"
    exit_code: 0
    result: PASS
    summary: "Restore succeeded for all 7 projects (locked mode)."
  - attempted_at: "2026-09-02T03:00:27Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-033-cc60cffc554ced423c97a86f014f577bc05d382b"
    exit_code: 0
    result: PASS
    summary: "Build succeeded. 0 Warning(s), 0 Error(s)."
  - attempted_at: "2026-09-02T03:01:12Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-033-cc60cffc554ced423c97a86f014f577bc05d382b"
    exit_code: 0
    result: PASS
    summary: "Passed! - Failed: 0, Passed: 1185, Skipped: 0, Total: 1185, Duration: 650 ms."
  - attempted_at: "2026-09-02T03:01:20Z"
    command: "dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-033-cc60cffc554ced423c97a86f014f577bc05d382b"
    exit_code: 0
    result: PASS
    summary: "Passed! - Failed: 0, Passed: 100, Skipped: 0, Total: 100, Duration: 7 s."
  - attempted_at: "2026-09-02T03:02:10Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"Category!=Corpus&Category!=Browser\""
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-033-cc60cffc554ced423c97a86f014f577bc05d382b"
    exit_code: null
    result: INCONCLUSIVE
    summary: "SQL Server LocalDB is absent on this workstation; SqlException error 52 (Unable to locate a Local Database Runtime installation) on every test requiring the database. Raw process exit code was 1, recorded as INCONCLUSIVE (missing local prerequisite, not a code signal) per controller decision. Substituted by the next attempt (hosted CI evidence at this exact PR head)."
  - attempted_at: "2026-09-02T03:05:00Z"
    command: "gh run view 33525322197 --json headSha,conclusion,status,name,url ; gh pr checks 641 --json name,state,link"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus"
    exit_code: 0
    result: PASS
    summary: "GitHub Actions repository-check run 33525322197 at PR #641 head c6842a8c3a36fe806a3103d067fef207d22651d3 (the pre-merge commit that became the second merge-commit parent): conclusion success. Every job green: unit, browser, sql-integration (1), sql-integration (2), sql-integration (3), sql-integration-coverage, test-ui, changes, documentation, local-development-scripts, reference-data; infrastructure SKIPPED (not required for this diff). This is hosted CI evidence substituting for the locally-unavailable SQL/browser lanes, not a local run in this worktree; the merge introduced no conflicts against this head (merge commit parents: origin/dev 9b8f78a3… and c6842a8c…)."
  - attempted_at: "2026-09-02T03:04:10Z"
    command: "git merge-base --is-ancestor cc60cffc554ced423c97a86f014f577bc05d382b origin/dev"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus"
    exit_code: 0
    result: PASS
    summary: "True: the merge SHA is an ancestor of origin/dev, confirming the reviewed and merged change is present in the integration branch."
  - attempted_at: "2026-09-02T03:04:30Z"
    command: "git show cc60cffc554ced423c97a86f014f577bc05d382b:src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-033-cc60cffc554ced423c97a86f014f577bc05d382b"
    exit_code: 0
    result: PASS
    summary: "The named production caller exists at the merge SHA: GraphApprovedInboxSource.ReadAsync (declared line 603) contains the sparse-item skip path described in the post-implementation report (the ReceivedAtUtc-is-null continue ahead of the MIME fetch); the class is registered as the production IApprovedInboxSource via the existing DI registration, unchanged by this diff."
  - attempted_at: "2026-09-02T03:04:45Z"
    command: "git merge-base --is-ancestor cc60cffc554ced423c97a86f014f577bc05d382b 0b3ec847aae42ee1c1bee4fb99459f9192534dca"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus"
    exit_code: 1
    result: FAIL
    summary: "False, as expected today: the merge SHA is NOT an ancestor of 0b3ec847aae42ee1c1bee4fb99459f9192534dca, the source SHA of production release 37 (docs/operations.md release table). The change has not yet been released; this is Part 2 evidence, not a code defect (exit 1 is git's non-ancestor signal, recorded verbatim per M9)."
  - attempted_at: "2026-09-02T03:05:20Z"
    command: "git merge-base --is-ancestor cc60cffc554ced423c97a86f014f577bc05d382b origin/main"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus"
    exit_code: 1
    result: FAIL
    summary: "False, as expected today: the merge SHA is NOT an ancestor of origin/main (fb3f07acc8cca8d9d8b57db8a431b607772436dc). dev has not yet been promoted to main. Part 2 evidence; not a code defect."
  - attempted_at: "2026-09-02T03:06:00Z"
    command: "manual check: scripts/Invoke-ProductionSmoke.ps1 reference"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus"
    exit_code: null
    result: INCONCLUSIVE
    summary: "The production smoke script exists at scripts/Invoke-ProductionSmoke.ps1 and is the release table's recorded post-deploy check (docs/operations.md line ~766, ~1332; docs/runbook.md lines 934, 1065, 1095, 1127, 1198). It has not been run against a release carrying this SHA because no such release exists yet (release 37, the currently deployed source, predates this change). Pending release 38 or later; not a command this run can execute against unreleased code."
  - attempted_at: "2026-09-02T03:06:00Z"
    command: "manual check: post-implementation-report named canary/production observation"
    cwd: "n/a (get_ticket_doc post-implementation-report)"
    exit_code: null
    result: INCONCLUSIVE
    summary: "The post-implementation report names the canary as the previously-affected mailbox's poll cursor and service-health row: 'the affected mailbox's poll cursor advances past the sparse entry and its service-health row leaves \"Failed\"' — recorded there as 'an observation, not a command an agent runs,' requiring an operator with production access. Not observable from this run: no operator was available (headless, M8) and no production access is available to this role. Pending release and operator observation."
---

# Proof — MAIL-033

## Part 1: code evidence at merged_sha (cc60cffc554ced423c97a86f014f577bc05d382b)

Confirmed via `gh pr view 641 --json state,mergeCommit,url`: `state: MERGED`,
`mergeCommit.oid: cc60cffc554ced423c97a86f014f577bc05d382b`. The detached
verification worktree
`C:\Users\PGUSER\Documents\github\pegasus-worktrees\verify-mail-033-cc60cffc554ced423c97a86f014f577bc05d382b`
is at that exact SHA (`rev-parse HEAD`), detached (`symbolic-ref --short -q HEAD`
empty), and clean (`status --short --branch` shows `## HEAD (no branch)` with
no changes).

Restore, build, core-tests and architecture-tests all PASS in that worktree
(runner evidence, attempts above). The SQL/browser integration lane is
INCONCLUSIVE locally (SQL Server LocalDB is absent on this workstation, a
known host quirk, not a code signal); the merged content's own CI run
(GitHub Actions `33525322197`, `repository-check`, PR #641 head
`c6842a8c3a36fe806a3103d067fef207d22651d3`) is green on every required job
including all three `sql-integration` shards, `sql-integration-coverage`,
`browser` and `test-ui`, and the merge commit's other parent is `origin/dev`
at `9b8f78a3…` with no conflicts — this hosted run is the recorded evidence
for that lane, not a local pass.

`cc60cffc…` is an ancestor of `origin/dev` (true), confirming the reviewed
and merged change is present in the integration branch. The named production
caller, `GraphApprovedInboxSource.ReadAsync` in
`src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs`, exists at the
merge SHA with the sparse-item skip described in the post-implementation
report, registered through the pre-existing
`IApprovedInboxSource` DI registration.

**Part 1 holds.**

## Part 2: pending release

`cc60cffc…` is NOT an ancestor of `origin/main` (`fb3f07ac…`) and NOT an
ancestor of `0b3ec847aae42ee1c1bee4fb99459f9192534dca`, the source SHA of the
currently deployed production release 37 (`docs/operations.md` release
table). The change is merged to `dev` only; it has not shipped in any
production release.

`scripts/Invoke-ProductionSmoke.ps1` is the recorded post-deploy check
(`docs/operations.md`, `docs/runbook.md`) but has not run against a release
containing this SHA, because no such release exists yet. The canary named in
the post-implementation report — the previously-affected mailbox's poll
cursor advancing past the sparse entry and its service-health row leaving
"Failed" — is an operator production observation, not yet made; no operator
was available to this headless run (M8) and this role has no production
access.

This is a backend/service fix with no routed Razor page change (per the
post-implementation report: "No screenshots and no snapshot or catalogue
regeneration"), so no UI operator-acceptance record applies.

**Part 2: pending release.**

## Result

`INCONCLUSIVE` (`failure_class: inconclusive`). Part 1 is fully evidenced and
holds. Part 2 cannot hold until this SHA ships in a production release
(release 38 or later) and the smoke script and canary observation can be
run/made against that release. This proof is retryable in place: rerun Part 2
once a qualifying release exists; no code or plan defect is implicated. Stays
in Verifying; not moved to Done.
