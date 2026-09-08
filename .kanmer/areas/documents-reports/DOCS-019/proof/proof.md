---
kind: proof-record
merged_sha: "493f7460d7728a6576d240d4feb7d0bf2a377ec5"
environment: ".worktrees/verify-docs-019-493f7460d7728a6576d240d4feb7d0bf2a377ec5; Windows PowerShell 7; root verifier"
verified_at: "2026-09-08T04:46:34.0477481Z"
result: PASS
attempts:
  - attempted_at: "2026-09-08T04:46:34.0477481Z"
    command: "Exact-merge identity, one-row diff, resource/caller census, Test-DocumentationLinks.ps1 and Test-UiCatalogue.ps1; full commands below"
    cwd: ".worktrees/verify-docs-019-493f7460d7728a6576d240d4feb7d0bf2a377ec5"
    exit_code: 0
    result: PASS
    summary: "Detached/clean exact HEAD; one row +1/-1; no signature resource; actual supplied-byte caller; 127 Markdown files and 60 routes/67 prototypes/zero broken references."
---

# Exact integrated proof — DOCS-019

[PR697](https://github.com/collisionengineers/pegasus/pull/697) merged into dev
at2026-09-08T04:44:41Z, exact merge above. Independent pack_reconcile review
388069ee019966e8 binds authorc72f0df959de5de07f0ca15cda87c04e3b8879cd
and approved plane4aaec8c71319fed. Root read the full review/report/plan.

## Actual root verification

Root created the named disposable detached worktree from the confirmed merge,
without changing the source/author/board checkout. Assertions required exact
HEAD, symbolic-ref exit1 with no branch, and empty status before work.
All commands ran there; full script exited0 in6.858s. The single timestamp
above is the observed script completion, not invented individual command timing.

```powershell
git rev-parse HEAD
git symbolic-ref --short -q HEAD
git status --porcelain
git diff-tree --no-commit-id --numstat -r HEAD
git diff --check HEAD^ HEAD
rg -n -F "brand.signatures" src
rg -n "SignatureContent|SignatureContentType" src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs
pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1
git status --porcelain
```

- Exact merge identity matched, detached search expected exit1, no dirt.
- diff-tree exit0: only docs/design/README.md, one insertion/one deletion;
  diff-check exit0. The whole row matches the approved plan and reviewed diff.
- Resource search returned no matches, expected exit1; this is absence
  evidence, not a failed test. Caller search exit0 found lines100–101.
- Documentation links exit0,127 Markdown files checked.
- UI catalogue exit0,60 routed sources,67 prototypes,zero broken references.
- Final status exit0 with no changes.

Author report098d00274a28f0b0 independently records the same lightweight
checks at the author head. No failed implementation/test attempt exists.
A preliminary author's ignored-file discovery and reviewer's unsupported
Windows wildcard search were corrected diagnostic reads, not product PASS.
Root's merge orchestration initially treated a still-running native session
as an error; polling that same session87052 confirmed exit0/MERGED. No second
merge command or source repair occurred.

## Acceptance and limitations

The supplied-signature row now agrees with FRD-11/D31 and the existing
account→projection→snapshot→renderer byte/media-type path. No signature
resource is embedded, and no supplied asset, adjacent row, source, test,
snapshot or convention changed. Source inspection is not a new renderer
runtime test; this documentation-only correction warrants no build/capture.

Historical DOCS-012/PLAT-029/CASE-038/PLAT-075/archived PLAT-008 claims and
non-PASS/missing proof remain untouched; root handed off only this row.
No deployment, cloud or live-data change occurred. Ordinary acceptance is
integrated dev; the final converged v1 CI/release remains an EPIC-014 obligation.
