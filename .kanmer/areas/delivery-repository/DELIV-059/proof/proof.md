---
kind: proof-record
schema: 2
merged_sha: "9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c"
environment: ".worktrees/verify-deliv-059-9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c; Windows PowerShell 7"
verified_at: "2026-09-08T16:56:11.2401111Z"
result: PASS
receipts: []
attempts:
  - attempted_at: "2026-09-08T16:55:08.6296469Z"
    command: "$ErrorActionPreference='Stop'\n$sha='9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c'\n$mergeParent='71a2d27c8836a44b639762469ec950f1c8c82802'\n$author='67b357475433df5fdb09cf7296284b90de516d47'\n$authorParent=(git rev-parse \"$author^\").Trim()\n$top=(git rev-parse --show-toplevel).Trim()\n$commonRaw=(git rev-parse --git-common-dir).Trim()\n$common=[System.IO.Path]::GetFullPath((Join-Path $top $commonRaw))\n$head=(git rev-parse HEAD).Trim()\n$branch=(git symbolic-ref -q --short HEAD)\nif ($LASTEXITCODE -ne 0) { $branch='DETACHED'; $global:LASTEXITCODE=0 }\n$status=@(git status --porcelain=v1)\n$parent=(git rev-parse 'HEAD^').Trim()\n$mergePaths=@(git diff --name-only $mergeParent $sha)\n$authorPaths=@(git diff --name-only $authorParent $author)\n$mergeBlob=(git rev-parse \"$sha`:docs/operations.md\").Trim()\n$authorBlob=(git rev-parse \"$author`:docs/operations.md\").Trim()\n$mergePatch=(git diff --binary $mergeParent $sha -- docs/operations.md | git hash-object --stdin).Trim()\n$authorPatch=(git diff --binary $authorParent $author -- docs/operations.md | git hash-object --stdin).Trim()\n$expectedTop='C:\\Users\\Alex\\Documents\\GitHub\\pegasus\\.worktrees\\verify-deliv-059-9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c'\n$expectedCommon='C:\\Users\\Alex\\Documents\\GitHub\\pegasus\\.git'\nWrite-Output ('UTC=' + [DateTime]::UtcNow.ToString('o'))\nWrite-Output \"TOP=$top\"\nWrite-Output \"COMMON=$common\"\nWrite-Output \"HEAD=$head\"\nWrite-Output \"BRANCH=$branch\"\nWrite-Output ('STATUS_COUNT=' + $status.Count)\nWrite-Output \"PARENT=$parent\"\nWrite-Output \"AUTHOR_PARENT=$authorParent\"\nWrite-Output ('MERGE_PATH_COUNT=' + $mergePaths.Count)\n$mergePaths | ForEach-Object { Write-Output \"MERGE_PATH=$_\" }\nWrite-Output ('AUTHOR_PATH_COUNT=' + $authorPaths.Count)\n$authorPaths | ForEach-Object { Write-Output \"AUTHOR_PATH=$_\" }\nWrite-Output \"MERGE_BLOB=$mergeBlob\"\nWrite-Output \"AUTHOR_BLOB=$authorBlob\"\nWrite-Output \"MERGE_PATCH=$mergePatch\"\nWrite-Output \"AUTHOR_PATCH=$authorPatch\"\nif ([System.IO.Path]::GetFullPath($top) -ne $expectedTop) { throw 'Unexpected worktree root' }\nif ($common -ne $expectedCommon) { throw 'Unexpected common git directory' }\nif ($head -ne $sha) { throw 'Unexpected HEAD' }\nif ($branch -ne 'DETACHED') { throw 'HEAD is not detached' }\nif ($status.Count -ne 0) { throw 'Worktree is dirty' }\nif ($parent -ne $mergeParent) { throw 'Unexpected merge parent' }\nif ($mergePaths.Count -ne 1 -or $mergePaths[0] -ne 'docs/operations.md') { throw 'Merge diff scope mismatch' }\nif ($authorPaths.Count -ne 1 -or $authorPaths[0] -ne 'docs/operations.md') { throw 'Author diff scope mismatch' }\nif ($mergeBlob -ne $authorBlob) { throw 'Reviewed and merged blobs differ' }\nif ($mergePatch -ne $authorPatch) { throw 'Reviewed and merged patches differ' }\nWrite-Output 'IDENTITY_AND_DIFF=PASS'"
    cwd: ".worktrees/verify-deliv-059-9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c"
    exit_code: 1
    result: FAIL
    authority: supporting
    failure_class: plan
    summary: "The read-only verifier wrapper incorrectly joined an absolute common-directory path and failed its own identity assertion. Git reported expected source, parent, scope and identical blobs/patches; this is a wrapper plan error, not a repository defect."
  - attempted_at: "2026-09-08T16:55:48.8249486Z"
    command: "$ErrorActionPreference='Stop'\n$sha='9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c'\n$mergeParent='71a2d27c8836a44b639762469ec950f1c8c82802'\n$author='67b357475433df5fdb09cf7296284b90de516d47'\n$authorParent=(git rev-parse \"$author^\").Trim()\n$top=(git rev-parse --show-toplevel).Trim()\n$commonRaw=(git rev-parse --git-common-dir).Trim()\nif ([System.IO.Path]::IsPathRooted($commonRaw)) { $common=[System.IO.Path]::GetFullPath($commonRaw) } else { $common=[System.IO.Path]::GetFullPath((Join-Path $top $commonRaw)) }\n$head=(git rev-parse HEAD).Trim()\ngit symbolic-ref -q --short HEAD *> $null\nif ($LASTEXITCODE -eq 0) { $branch=(git symbolic-ref -q --short HEAD).Trim() } else { $branch='DETACHED'; $global:LASTEXITCODE=0 }\n$status=@(git status --porcelain=v1)\n$parent=(git rev-parse 'HEAD^').Trim()\n$mergePaths=@(git diff --name-only $mergeParent $sha)\n$authorPaths=@(git diff --name-only $authorParent $author)\n$mergeBlob=(git rev-parse \"$sha`:docs/operations.md\").Trim()\n$authorBlob=(git rev-parse \"$author`:docs/operations.md\").Trim()\n$mergePatch=(git diff --binary $mergeParent $sha -- docs/operations.md | git hash-object --stdin).Trim()\n$authorPatch=(git diff --binary $authorParent $author -- docs/operations.md | git hash-object --stdin).Trim()\n$expectedTop='C:\\Users\\Alex\\Documents\\GitHub\\pegasus\\.worktrees\\verify-deliv-059-9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c'\n$expectedCommon='C:\\Users\\Alex\\Documents\\GitHub\\pegasus\\.git'\nWrite-Output ('UTC=' + [DateTime]::UtcNow.ToString('o'))\nWrite-Output \"TOP=$top\"\nWrite-Output \"COMMON=$common\"\nWrite-Output \"HEAD=$head\"\nWrite-Output \"BRANCH=$branch\"\nWrite-Output ('STATUS_COUNT=' + $status.Count)\nWrite-Output \"PARENT=$parent\"\nWrite-Output \"AUTHOR_PARENT=$authorParent\"\nWrite-Output ('MERGE_PATH_COUNT=' + $mergePaths.Count)\n$mergePaths | ForEach-Object { Write-Output \"MERGE_PATH=$_\" }\nWrite-Output ('AUTHOR_PATH_COUNT=' + $authorPaths.Count)\n$authorPaths | ForEach-Object { Write-Output \"AUTHOR_PATH=$_\" }\nWrite-Output \"MERGE_BLOB=$mergeBlob\"\nWrite-Output \"AUTHOR_BLOB=$authorBlob\"\nWrite-Output \"MERGE_PATCH=$mergePatch\"\nWrite-Output \"AUTHOR_PATCH=$authorPatch\"\nif ([System.IO.Path]::GetFullPath($top) -ne $expectedTop) { throw 'Unexpected worktree root' }\nif ($common -ne $expectedCommon) { throw 'Unexpected common git directory' }\nif ($head -ne $sha) { throw 'Unexpected HEAD' }\nif ($branch -ne 'DETACHED') { throw 'HEAD is not detached' }\nif ($status.Count -ne 0) { throw 'Worktree is dirty' }\nif ($parent -ne $mergeParent) { throw 'Unexpected merge parent' }\nif ($mergePaths.Count -ne 1 -or $mergePaths[0] -ne 'docs/operations.md') { throw 'Merge diff scope mismatch' }\nif ($authorPaths.Count -ne 1 -or $authorPaths[0] -ne 'docs/operations.md') { throw 'Author diff scope mismatch' }\nif ($mergeBlob -ne $authorBlob) { throw 'Reviewed and merged blobs differ' }\nif ($mergePatch -ne $authorPatch) { throw 'Reviewed and merged patches differ' }\nWrite-Output 'IDENTITY_AND_DIFF=PASS'"
    cwd: ".worktrees/verify-deliv-059-9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Corrected read-only identity wrapper passed exact clean detached SHA, common Git directory, actual merge parent, single-file scope and identical reviewed/merged blob and patch."
  - attempted_at: "2026-09-08T16:55:58.5305219Z"
    command: "pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1"
    cwd: ".worktrees/verify-deliv-059-9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "All relative Markdown links resolve; 140 files checked."
  - attempted_at: "2026-09-08T16:56:11.2401111Z"
    command: "pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base 71a2d27c8836a44b639762469ec950f1c8c82802 -Head 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c"
    cwd: ".worktrees/verify-deliv-059-9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Markdown placement passed for exact merge-parent to merge range."
---

# DELIV-059 exact-merge proof

PR #710 merged on 8 September 2026 at 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c after independent PASS at head 67b357475433df5fdb09cf7296284b90de516d47 (attestation 5400253cdc7736ca). The configured pr.yml/verify/push exact-SHA lookup returned HTTP 404 before verification Git; no receipt exists, so the sole host verifier ran every bounded documentation obligation locally. No dotnet or live operation was required or performed.

The actual merge parent is 71a2d27c8836a44b639762469ec950f1c8c82802. That merge range changes only docs/operations.md; it matches the reviewed author range despite the intervening D56 integration. Both operations blobs are f7fd5677a78f093eb7e415294298f0e72fa5162b and both binary patch hashes are 294f225bf26bd31bdf60b1613c12dacb73f6210f. Final exact-HEAD clean check passed at 16:56:34.7860347Z.

The one failed preflight wrapper and its corrected pass are retained above, not erased or called a source regression. The analogous earlier pre-merge wrapper error and every earlier check remain in scratch/verify.md@40b116481767e7fc.

The restored release-39 entry preserves historical PR #676 claims, including rejected ZIP, nonidentical replacement provenance, partial migrations and separately authorised resets. The source/CI evidence is independently bounded: test-ui failed and browser succeeded; absent artifact, Azure, reset and waiver receipts are not upgraded to fresh proof. Documentation correctness is PASS, not a new deployment or release-39 artifact acceptance.

This exact documentation merge and PR #703 source merge 6509746913eda16d2c4440add20e7f6793500f0b account for D1's required code and historical record. Root separately owns the explicitly authorized PR #676 disposition; this proof does not itself close that PR, alter historical claims or authorize production.
