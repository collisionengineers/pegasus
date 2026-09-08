---
kind: proof-record
merged_sha: "3c1d04781719f92c86a190f510c1924fc9df6328"
environment: ".worktrees/verify-case-031-3c1d04781719f92c86a190f510c1924fc9df6328; Windows x64, PowerShell 7, .NET 10, LocalDB"
verified_at: "2026-09-08T03:31:10.4845582Z"
result: PASS
attempts:
  - attempted_at: "2026-09-08T03:28:05Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-case-031-3c1d04781719f92c86a190f510c1924fc9df6328"
    exit_code: 0
    result: PASS
    summary: "Session29817 start marker; all seven projects restored, maximum2.51s."
  - attempted_at: "2026-09-08T03:28:05Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-case-031-3c1d04781719f92c86a190f510c1924fc9df6328"
    exit_code: 0
    result: PASS
    summary: "Same guarded session, after restore;125.35s, zero warnings/errors. Marker is session start, not separately captured process start."
  - attempted_at: "2026-09-08T03:30:21.1343846Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~EvaApiMappingTests|FullyQualifiedName~EvaSubmissionPolicyTests|FullyQualifiedName~EvaBundleContractTests' --logger 'trx;LogFileName=case-031-merged-core.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-case-031-3c1d04781719f92c86a190f510c1924fc9df6328"
    exit_code: 0
    result: PASS
    summary: "57/57 PASS,101ms; zero failed/notExecuted/inconclusive. TRX start time."
  - attempted_at: "2026-09-08T03:30:24.2349480Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~EvaApiTransportTests|FullyQualifiedName~CustodyOutboxIntegrationTests.EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange' --logger 'trx;LogFileName=case-031-merged-integration.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-case-031-3c1d04781719f92c86a190f510c1924fc9df6328"
    exit_code: 0
    result: PASS
    summary: "13/13 PASS,44s; zero failed/notExecuted/inconclusive. Actual SQL/manual caller executed; no corpus skip."
---

# CASE-031 exact merged verification

## Identity and authority

GitHub PR694 confirms MERGED into configured integration branch dev at
2026-09-08T03:27:01Z, merge3c1d04781719f92c86a190f510c1924fc9df6328.
Root independently reviewed author intake_audit's exact ten-file
9863dd4264440ef228a0766d3e2949faf6a4e12b; whole review0651f801dc7e91b2,
public review5136946957, planecb1dab6abfa296b, files77b89acba62aface,
report1b2a5c54c15b392b, checklist79cf52b91e83aaa6 (4/4). All were read in full.
Fresh gates preceded Review to Verifying. No self-review or self-merge.

The first freshness guard observed a changed bot comment and stopped before
merge. Root read its completed exact-head no-findings status. A second guard
also stopped before merge: PowerShell converted updated_at to DateTime and
the comparison was against a string. Read-only type inspection established
that cause; -DateKind String preserved exact source timestamps on the
successful guard. Both shell attempts exited1 and changed no Git state.
The actual normal squash merge used --match-head-commit, no admin/bypass.

Exact commit fetched and a fresh SHA-named detached worktree created. HEAD
matched exactly, symbolic-ref exited1 with no branch, and porcelain status
was empty before and after checks. The six files differing from the author
tree are the already accepted UIIMP-017 Health correction, not these ten
EVA files. Shared dev, other worktrees, board Git and corpus were not changed.
Root was the only heavy verifier; session29817 completed with exit0.

## Actual behavior proved

Existing EvaSubmissionStore manual caller sends the accepted canonical
claimant address unchanged as ClmAdd, independently of inspection address.
Core owns accepted-value precedence and validation; the API-only mapping is
version2. Missing/unaccepted/invalid current address refuses before image
reads, transport, attempts, history or Case mutation. Known replay returns the
original outcome even if current address later becomes unusable.

All old ZIP/image, first-send/resend, outcome, mode/Engineer, Case version,
lease and race assertions remain and executed. No readiness button, automatic
EVA path, schema, extraction or UI change was introduced.

## Earlier failed author-stage attempt retained

Report1b2a5c54c15b392b records the complete chronological author-stage commands.
Root3667: locked restore/build PASS127.88s;57 Core PASS; Integration
12 PASS/1 FAIL35s, exit1, expected CaseCreated versus NeedsSorting before
address behavior. This original failed attempt is not reclassified PASS.

The fixture's literal invalid synthetic PDF was not evidence for its assumed
work type. Root approved the existing corpus resolver/hash convention with
the supplied EREF10 original email and real attached letter. Source SHA256
3063FF9ECB31878F582FB439047D999A41A7C6FE5B978CFBEE5C7E7F277553B4 was independently
checked again before merged verification. Genuine headers/body/PDF remain;
two existing in-memory test JPEGs make an expressly derived export probe.
Actual InspectionAndAudit/AMA/47857/1 replaces an unsupported fixture assumption.
CaseCreated and every downstream assertion remain mandatory.

Root73724: corrected Integration/dependency build PASS42.27s and sole actual
caller PASS1/1,37s, zero skipped, script0. No production intake behavior was
changed to pass that test. The first failure and its artifacts remain.

## Retained artifacts

Author .worktrees/case-031/artifacts/verification:

- case-031-core.trx:
  F0859E4685461865D0376F0562F33F2F3E146345E4F187A9B3F60133B0D9DC2A.
- case-031-integration.trx (original FAIL):
  E2AC1683BA4DDFEA610B7F808148CC89AE8886DB19FDB6555E5D88AE21CCEF94.
- case-031-caller-corrected.trx:
  CE49FFEF3436A0FC7051737DDC0B8B271BD950E82A9D285FF5F9155046DF22AD.

Exact detached artifacts/verification:

- case-031-merged-core.trx:
  236083698AF9F93E3D007FF3C1EE9DAC1530A7EF5907E3618FFFF18AE1851347.
- case-031-merged-integration.trx:
  D61F8322759694F10A5BAED638BAC39131CA99DDB75D450CDAB9FA6B81B2EBF0.

Main independently read every merged TRX counter and SHA256. Integration
finished2026-09-08T03:31:10.4845582Z. Copy and hash-verify all five artifacts
under pegasus_pack/current/proofs/CASE-031 before removing either owned
temporary worktree. No original failure or supplied evidence may be deleted.

## Boundary and handoff

This bounded acceptance is PASS, not full-suite CI, live EVA, email or
deployment acceptance. Root-authorized skip-ci per-ticket work retains the
single converged solution/release obligation under EPIC-014. Empty GitHub
checks are not CI PASS. FRD-07 describes the implemented API prerequisite;
no deployed-state claim changed.

After whole-proof readback and fresh gates, move Verifying to Done.
kanmer-closeout then owns proof retention, exact owned Git cleanup and claim
release last. Preserve every unrelated/foreign workspace and branch.

## Closeout retention and safety audit — 2026-09-08

Root approved PASS/Done before closeout. PR694 was re-read as MERGED at the
same exact SHA. The merge is reachable from origin/dev (ancestor exit0).
All five TRXs were copied before cleanup, byte lengths/SHA256 rechecked
against the records above, and their XML counters independently read. The
original integration record remains 12 PASS/1 FAIL, not a later PASS.
Retained files and manifest: pegasus_pack/current/proofs/CASE-031/manifest.json.

Both resolved cleanup roots are exact direct children of this repository's
.worktrees directory, not the source, board or another ticket root. Author
HEAD9863dd4264440ef228a0766d3e2949faf6a4e12b is on the recorded branch;
verification HEAD3c1d04781719f92c86a190f510c1924fc9df6328 is detached.
Both share this source repository's .git and have empty porcelain status.
The full live/archived claim census has no other claim using either target
or the author branch. Remote author branch still points to that exact author
HEAD. Only those validated Git targets are authorized for cleanup.

Ticket traceability now records the reachable merge, PR694, integrated/dev
and not-deployed; Outcome is populated. Git removal and release follow.

Closeout Git commands completed with exit0: normal worktree remove for each
exact validated root, normal branch -d, and scoped remote branch deletion.
Git noted the squash author branch was merged to its upstream rather than
the stale shared HEAD; prior PR/merge evidence established the squash merge.
No force, reset, broad prune or other workspace/branch deletion was used.
Readback confirms both directories/registrations and local/remote author
branch are absent. All five retained TRXs still match the manifest. Shared
source remains on dev at3284f93fc3ea9fd3bbbea9405ec92dc7818378f2.
Only temporary checkout/build artifacts were removed; source remains in the
merge and test evidence is retained. Claim release is the final cleanup step.

Claim released last at2026-09-08T03:46:53.341Z. Final item readback is Done,
unarchived, integrated/dev at the exact merge, not-deployed, with no taken_at,
branch, worktree or lease. The manifest SHA256 is
4C7889816DF1F1212C5EA606F9E19C65AD2B2F5BE8F9CFF98CBC5700D349044C.
Closeout is complete; no new runtime verification or deployment was performed.
