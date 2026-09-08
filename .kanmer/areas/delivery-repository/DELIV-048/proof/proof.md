---
kind: proof-record
merged_sha: "1c1d7a0a45555604bafd3e732bd606bf083b804a"
environment: ".worktrees/verify-deliv-048-1c1d7a0a45555604bafd3e732bd606bf083b804a; Windows x64, PowerShell 7"
verified_at: "2026-09-08T06:44:58Z"
result: FAIL
failure_class: implementation
attempts:
  - attempted_at: "2026-09-08"
    command: "$ErrorActionPreference = 'Stop'\n$expectedSha = '1c1d7a0a45555604bafd3e732bd606bf083b804a'\nif ((git rev-parse HEAD) -ne $expectedSha) { throw 'Exact merge mismatch' }\ngit symbolic-ref --short -q HEAD\nif ($LASTEXITCODE -ne 1) { throw 'Expected detached worktree' }\nif (git status --porcelain) { throw 'Expected clean worktree' }\n. ./scripts/PegasusPlatform.ps1\n$hint = Get-PegasusRepairHint -Id 'oras'\nWrite-Output $hint\nif ($hint -match 'authorised release terminal is Linux') { throw 'Windows ORAS repair hint still requires a Linux-only release terminal, contradicting ADR-0039 and DELIV-048 acceptance.' }"
    cwd: ".worktrees/verify-deliv-048-1c1d7a0a45555604bafd3e732bd606bf083b804a"
    exit_code: 1
    result: FAIL
    summary: "Command07e9d2,1.512s. Actual Windows Get-PegasusRepairHint oras says the authorized release terminal is Linux. Exact merge/detached/clean assertions passed; no build/provider operation."
---

# DELIV-048 exact-merge residual

PR681 is MERGED at1c1d7a0a45555604bafd3e732bd606bf083b804a.
The retained implementation report and prior review preserve the original
59.58s build,17 focused architecture tests and script checks; this first
post-merge proof does not erase or relabel that author evidence.

Planf52800f76a333e02 explicitly requires no remaining active Linux-only
rule. scripts/PegasusPlatform.ps1:983 still provides a Windows ORAS repair
hint demanding Linux. This is a real implementation omission, not a platform
transient. The native release route remains otherwise accepted; the final
integrated artifact build and provider/deployment acceptance have not run.

Reconcile_ticket returned no recommendation: legacy required-check
availability is inconclusive, and recorded author SHA is unreachable because
PR681 was squash-merged. Use the actual merge SHA for integrated traceability;
do not treat that historical author pointer as proof of integration.

Route Verifying back to Implementing on this same recorded branch/worktree
for only the existing ORAS Windows hint and existing platform-script regression.
No new convention, dependency, shell installer, cloud action or broad test.
The original acceptance and final converged artifact validation remain owed.
