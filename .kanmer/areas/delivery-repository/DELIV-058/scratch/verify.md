## Sole-host architecture verification — PASS (2026-09-08)

Verifier: `/root/agent_config_verifier`
Worktree: `.worktrees/deliv-058`
Branch: `DELIV-058-architecture-assertions`
Base HEAD: `a1f0bfe260ea05df531df6e0ca3109141e7697da`
Plan version: `128e84dc19caa5f6`

Preflight at `2026-09-08T15:11:57.3201105Z` found no scoped build/test process. The diff was exactly two approved files (14 insertions/4 deletions), and `git diff --check` exited 0 with only repository LF→CRLF advisories. Frozen blob hashes matched the authorized handoff:
- `DependencyDirectionTests.cs`: `77ca689ea1d91d694429cdcdee74ffd4ab29daf4`
- `StagedArtifactReconciliationFunctionTests.cs`: `80b74ef998d4ba905cd4b6dc05b10cea1945cd87`

### Commands

1. `dotnet restore ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --locked-mode`
   - attempted_at: `2026-09-08T15:12:05.0339360Z`
   - exit_code: **0**
   - result: PASS
2. `dotnet build ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-restore`
   - attempted_at: `2026-09-08T15:12:18.2718458Z`
   - exit_code: **0**
   - result: PASS
   - summary: Build succeeded; 0 warnings, 0 errors.
3. `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary|FullyQualifiedName~FunctionDependsOnTheCanonicalStagedArtifactReconciler" --logger "trx;LogFileName=deliv-058-focused-host-20260908.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T15:14:04.6358562Z`
   - exit_code: **0**
   - result: PASS
   - summary: Failed 0, Passed 2, Skipped 0, Total 2.
   - TRX SHA-256: `3499EFD145874DE8EB4025099EA245CCFAC69222BAC0972DE49FD4AD45B9D95C`
4. `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --logger "trx;LogFileName=deliv-058-full-host-20260908.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T15:14:16.7702851Z`
   - exit_code: **0**
   - result: PASS
   - summary: Failed 0, Passed 116, Skipped 0, Total 116.
   - TRX SHA-256: `8814F71ABC5B06C071542B7AFA1A5B49C844169EAE8BE86820737F3F50E4BF77`

Postcheck at `2026-09-08T15:15:04.1712207Z` retained the same two-file status, clean diff check, and exact blob hashes. Three idle reusable MSBuild nodes remained; no testhost/vstest or active verification command remained.

Disposition: **PASS**. No source write, rerun, D56 action, broader application test, commit, push, PR, merge, or stage mutation was performed.

Root exact-merge setup: PR708 confirmed MERGED 0a6ccca799eb670825e60b614cf24b846cdf4572 into dev; current declared pr.yml/verify/push lookup exits1 HTTP404 absent workflow, all scoped obligations missing under ordinary fallback. Reconcile returned no recommendation. Read plan128e84dc19caa5f6/gates, renewed retained lease3 verifying. Only after receipt lookup fetched and created exact clean detached .worktrees/verify-deliv-058-0a6ccca799eb670825e60b614cf24b846cdf4572; HEAD matched, symbolic-ref empty exit1 as expected for detached, status clean. Sole verifier queued locked Architecture restore, Release build, focused2 and full116 after current D57/D56. No proof/PASS/Done/main/Azure claim.

## Exact-merge fallback — PASS — 2026-09-08

Verifier: `/root/agent_config_verifier`
Exact detached integration SHA: `0a6ccca799eb670825e60b614cf24b846cdf4572`
Merge parent: `3370e58f40de205986fb9642e62b329f06f91a4f`
Plan: `128e84dc19caa5f6`

The declared `pr.yml` receipt remains absent (HTTP 404), and prior optional CI failures remain explicit non-PASS evidence; neither is used as exact-merge PASS.

At `2026-09-08T15:54:04.5839018Z`, `dotnet build-server shutdown` exited 0 and left no scoped verification process. Preflight at `2026-09-08T15:54:19.8548579Z` passed: exact HEAD, detached, clean, no scoped process, and exactly the two approved first-parent paths. Their exact integrated blob IDs match the reviewed freeze:

- `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`: `77ca689ea1d91d694429cdcdee74ffd4ab29daf4`
- `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`: `80b74ef998d4ba905cd4b6dc05b10cea1945cd87`

### Commands

1. `dotnet restore ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --locked-mode`
   - attempted_at: `2026-09-08T15:54:28.2881243Z`
   - exit_code: **0**
2. `dotnet build ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-restore`
   - attempted_at: `2026-09-08T15:54:41.0761891Z`
   - exit_code: **0**
   - result: build succeeded; 0 warnings, 0 errors.
3. `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary|FullyQualifiedName~FunctionDependsOnTheCanonicalStagedArtifactReconciler" --logger "trx;LogFileName=deliv-058-exactmerge-focused-20260908-1556.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T15:56:27.7328141Z`
   - exit_code: **0**
   - result: 2 passed, 0 failed, 0 skipped.
   - TRX: `artifacts/verification/deliv-058-exactmerge-focused-20260908-1556.trx`
   - TRX SHA-256: `65CE41645B4FD6AB04648806E7EB4E291104B37D950ED07116B5F7BC09571EFE`.
4. `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --logger "trx;LogFileName=deliv-058-exactmerge-full-20260908-1556.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T15:56:41.1141344Z`
   - exit_code: **0**
   - result: 116 passed, 0 failed, 0 skipped.
   - TRX: `artifacts/verification/deliv-058-exactmerge-full-20260908-1556.trx`
   - TRX SHA-256: `D9419290A3998CC026811762A7ECC8F22C1B19CE4E09A0A7AB496C9B10ECCC89`.

Postcheck at `2026-09-08T15:57:33.5612580Z` retained exact HEAD, clean status, exactly the two first-parent paths, and both approved blobs. Three idle reusable MSBuild node processes remained; no testhost/vstest or active verification command remained.

Disposition: **PASS** for DELIV-058's exact merged architecture scope. No broader application gate, source write, rerun, proof, Done movement, cleanup, ENG check, release, deployment, or promotion occurred.
