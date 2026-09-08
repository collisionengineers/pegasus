Root exact-merge setup: PR706 confirmed MERGED at d76de2534ec6651c1a434a55f76593b7b140bf1c. Current declared pr.yml/verify/push lookup exits1 HTTP404 absent workflow; obligations missing under ordinary local fallback, not CI PASS. Reconciliation produced no recommendation and records unavailable required-check/reachability facts; no recovery mutation applied. After receipt lookup, created clean exactdetached .worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c; HEAD/exact/detached checks confirmed. Lease renewed revision6 verifying. No postmerge verification command started: solehost currentlyD56. Root additionally found erroneous command spellings/frozenSHA in premerge scratch/report; actualexecution receipts are being audited and corrected explicitly, not silently overwritten or rerun. No proof/Done claim yet.

## Exact-merge fallback attempt — stopped non-PASS — 2026-09-08

Authorized detached integration target: `d76de2534ec6651c1a434a55f76593b7b140bf1c`; merge parent: `a1f0bfe260ea05df531df6e0ca3109141e7697da`. This attempt did not regenerate or write the tracked package and does not claim full-original regeneration.

### Preflight

- `2026-09-08T15:37:32.0775140Z`: `git rev-parse HEAD`, `git rev-parse HEAD^`, `git symbolic-ref --short -q HEAD`, `git status --porcelain=v1 --untracked-files=all`, `git rev-parse --git-common-dir`, and scoped process census — exit 0; exact HEAD and parent above, detached, clean, common Git directory `C:/Users/Alex/Documents/GitHub/pegasus/.git`. The census found three idle MSBuild node-reuse `dotnet` processes (PIDs 7460, 18340, 30128; each command line was `dotnet.exe ...MSBuild.dll /noautoresponse /nologo /nodemode:1 /nodeReuse:true /low:false`), not an active test invocation.
- `2026-09-08T15:38:01.4276462Z`: `dotnet build-server shutdown` — exit 0; MSBuild and compiler servers shut down, and the scoped verification-process census was empty.

### Commands completed before the stop

1. `dotnet restore ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --locked-mode`
   - attempted_at: `2026-09-08T15:38:10.9274326Z`
   - exit_code: **0**
2. `dotnet build ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-restore`
   - attempted_at: `2026-09-08T15:38:20.2923516Z`
   - exit_code: **0**
   - output: build succeeded; 0 warnings, 0 errors.
3. `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PrincipalIdentificationCorpusTests" --logger "trx;LogFileName=intk-065-exactmerge-d76de253-20260908-1538.trx" --results-directory ./artifacts/verification`
   - attempted_at: `2026-09-08T15:38:57.6543208Z`
   - exit_code: **0**
   - result: 7 passed, 0 failed, 0 skipped.
   - retained TRX: `artifacts/verification/intk-065-exactmerge-d76de253-20260908-1538.trx`
   - retained TRX SHA-256: `83AEA0E4A0DB98D6C1174A10FA48201C03FBBF16F7AB0CEEAF07E8FB083CEA6E`.
4. `python -m unittest discover -s scripts/reference_data/tests -p test_build_principal_identification_corpus.py`
   - attempted_at: `2026-09-08T15:39:09.7589368Z`
   - exit_code: **0**
   - result: 2 tests ran; OK.
5. `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1`
   - attempted_at: `2026-09-08T15:39:18.3555778Z`
   - exit_code: **0**
   - result: all relative Markdown links resolve; 140 files checked.
6. `pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1`
   - attempted_at: `2026-09-08T15:39:29.1358160Z`
   - exit_code: **0**
   - result: Markdown placement regression tests passed.
7. `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base a1f0bfe260ea05df531df6e0ca3109141e7697da -Head d76de2534ec6651c1a434a55f76593b7b140bf1c`
   - attempted_at: `2026-09-08T15:39:48.2645880Z`
   - exit_code: **0**
   - result: placement passed for the actual merge-parent range.

### Stop event

8. `python -B ./artifacts/intk-065-exactmerge-verify.py`
   - attempted_at: `2026-09-08T15:42:38.5240248Z`
   - exit_code: **1**
   - retained output: `AssertionError: Expected 220 evidence references, found 1538`.
   - disposition: **verifier-harness interpretation failure; exact-merge result remains non-PASS**. The ignored one-off harness counted every nested `evidenceRefs` occurrence, whereas the independent review states “all 220 referenced ids resolve.” It reached this assertion after generator syntax, canonical-byte/hash, seven historical-object equality versus the merge parent, and five-current-snapshot checks, but no partial success is promoted to PASS. The harness was removed after the attempt. Under the explicit stop-on-first-failure/no-retry rule, it was not corrected or rerun; a fresh root grant is required for any harness-only correction.

Read-only post-stop census at `2026-09-08T15:43:13.6885691Z`: HEAD remained exact; tracked/untracked status remained clean; no scoped verification process remained; tracked package SHA-256 remained `494E0A0F42CED164AAB97CD50EBB497C1479C09EAF9F0A4DB177949BBDC7C251`. No source fix, new framework, regeneration, full rail, proof, Done movement, or ticket-worktree cleanup occurred.
