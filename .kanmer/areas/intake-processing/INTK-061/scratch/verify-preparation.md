# Exact-merge verification preparation — INTK-061

Prepared 2026-09-07 21:46 UTC. No new build or test execution.
Do not mark Done or clean/release before root's exact-head results.

PR #679 is MERGED at 2026-09-07T21:12:02Z, merge
783b537f189ead88553f940d03df0d1f9558ef75, reviewed author
3d58d8c58fe57aac5af6091f7ebf0e0b81b5b560. Independent review
895481a6c184fd34 is PASS; original report ada2fff40a1790d6 preserves
all compiler/fixture failures and root's corrected focused results.

## Read-only reconciliation

reconcile_ticket returned no recommendation: expired retained lease, absent
proof, author commit unreachable after squash and required-check availability
inconclusive. No reconciliation applied, no retake or force.
Renewed recorded lease caff30a5-8e3d-4009-820d-f471bec4d716 revision 12,
phase verifying, expiry 2026-09-07T22:16:40.700Z. Same implementation workspace.

## Checks actually run

All Git commands started from the normal source checkout unless noted.
Exact verification directory:
.worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75.

- gh pr view 679 --json state,mergeCommit,mergedAt,url,headRefOid,baseRefName:
  exit0, MERGED to dev with exact merge SHA above.
- git fetch origin 783b537f189ead88553f940d03df0d1f9558ef75:
  exit0, exact commit fetched.
- Initial unquoted PowerShell git rev-parse SHA^{tree} invocation exited1
  before Git comparison because braces were parsed by PowerShell. Corrected
  quoted revision invocation exited0; do not erase the failed command attempt.
- git rev-parse '783b537f189ead88553f940d03df0d1f9558ef75^{tree}'
  '3d58d8c58fe57aac5af6091f7ebf0e0b81b5b560^{tree}': exit0, NOT equal.
  Merge tree b3d49005cd317389a9ec5382714aced7606d2ee1.
  Author tree 7aed1b06bdb2da06651c8c08136a1fce6c969785.
- Test-Path of deterministic verification directory returned false, exit0;
  git worktree add --detach at that exact SHA completed exit0.
- In detached directory, git rev-parse HEAD exit0 equals merge SHA;
  git symbolic-ref --short -q HEAD returns empty with expected exit1;
  git status --short --branch exit0 returns only HEAD (no branch).
- Per-file Git blob census of the author's 31 changed files: exit0;
  30 are identical at merge, including every changed production file and
  both FRDs. PollApprovedInboxTests differs because MAIL-036 added exact
  notification handling tests. Full tree also includes 15 other MAIL-036/
  documentation/operating changes. This is NOT full-source equality.
- In detached directory: git diff --check 783b537f^ 783b537f exit0.
- In detached directory:
  pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1
  -Base '783b537f189ead88553f940d03df0d1f9558ef75^'
  -Head 783b537f189ead88553f940d03df0d1f9558ef75:
  exit0, Markdown placement passed.

## Retained artifacts, provenance only

Get-FileHash SHA256 exited0 over these original implementation-worktree
Release/net10.0 binaries. They were not copied, rebuilt or executed now.
No merged-head binary exists yet, so artifact equality is NOT proved.

| Original binary | SHA256 |
| --- | --- |
| src/Pegasus.Core/bin/Release/net10.0/Pegasus.Core.dll | 5f68eec5d30440f343b18192330965b51f3106023f63a6e03ac1e6a5854cba3a |
| src/Pegasus.Infrastructure/bin/Release/net10.0/Pegasus.Infrastructure.dll | 5086d1792ec6200926043d75fdd774b61e5817a37aa8ea03750cb35ca8704bc4 |
| tests/Pegasus.Core.Tests/bin/Release/net10.0/Pegasus.Core.Tests.dll | dab703995028dbd290404c5203ba841deadcd0bb843eced9965141144354d834 |
| tests/Pegasus.IntegrationTests/bin/Release/net10.0/Pegasus.IntegrationTests.dll | 323a30d5e6338da619df0f5652f09bb855d0e05d1e606804f3904cf14d113de0 |

## Proposal / handoff

An exact-source-reuse PASS would be false. Root accepted this evidence gap
and will build once in the detached exact merge workspace, then run focused
real intake/custody/OCR/destination callers to cover the MAIL-036 interaction.
Keep all original report failures and passes plus these checks in the eventual
whole proof record; never call old author tests new merged-head execution.
Proof remains absent pending actual root results. Closeout checklist,
traceability replacement with merged SHA, Done, cleanup and release have not
started. Ready for exact-head proof once root supplies command/exit evidence.
