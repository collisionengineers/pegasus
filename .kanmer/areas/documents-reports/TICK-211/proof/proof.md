# Proof — TICK-211

## Verified state

- Verification branch/worktree is exactly current `origin/dev` at `33f002203b2579529a15e2f8997e0dde45c42167`.
- That commit contains the independently reviewed and merged renderer integration from [[SIMPLI-014]] / [PR #415](https://github.com/collisionengineers/pegasus/pull/415), merge `b548b674e31d05de6f43eeb285a25dedd7d2a768`.
- `git diff --name-only origin/dev...HEAD` is empty. TICK-211 is a decision/analyzer acceptance record and has no repository change or PR of its own.

## Analyzer evidence

Command:

`dotnet msbuild src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj -getProperty:AnalysisLevel -getProperty:TreatWarningsAsErrors -getProperty:NoWarn -getProperty:Product -getProperty:Version -getProperty:RepositoryUrl`

Result:

- `AnalysisLevel = latest-recommended`
- `TreatWarningsAsErrors = true`
- `NoWarn = 1701;1702` (SDK default only)
- `Product = Pegasus.Infrastructure`
- `Version = 0.1.0-alpha.1` (root Pegasus version)
- `RepositoryUrl` is empty; no standalone CollisionRenderer repository identity remains.

Focused project/property searches found only the authoritative root settings. There is no Infrastructure/report-renderer override, CS1591 suppression, broad renderer-specific warning relaxation or standalone CollisionRenderer product/version/repository metadata. The retired `workspaces/report-renderer/Directory.Build.props` is absent.

## Build and CI evidence

- `dotnet build --configuration Release --no-restore` on current dev passed in 7.54s with 0 warnings and 0 errors.
- The earlier clean execution build with restore passed in 55.90s with 0 warnings and 0 errors.
- Upstream SIMPLI-014 CI run `32242081373` is completed/success: unit, browser, three SQL shards, SQL coverage, documentation, reference-data and source-workspaces passed.

## Evidence tier and verdict

PASS at the **repository decision + effective analyzer policy + merged build/CI** tier. The renderer is monolith application code governed by the root analyzer policy; no policy enclave survives. This does not claim deployment or live runtime behaviour, and no cloud or `main` write occurred.

TICK-211 is ready for Done and closeout.
