# Checklist — DELIV-011 (release 11)

- [x] PR 409 (PLAT-006) merged to `dev` → `feda958f`
- [x] Promotion PR 410 (dev → main) open, all checks SUCCESS on `feda958f`
- [x] Release worktree `../pegasus-worktrees/deliv-011-release-11` at `feda958f`, azd env `pegasus-prod` copied (vault `pegasusprodkv252ow37g` confirmed in every `*_SECRET_URI`)
- [x] `dotnet restore --locked-mode` / `build -c Release`: 0 warnings, 0 errors; `Test-AzureDeploymentPlan -Mode Local` passed
- [x] `Build-ReleaseArtifacts -Version 0.1.0-alpha.1 -SourceRevision feda958f…` → `artifacts/releases/0.1.0-alpha.1/` (web-image.tar.gz, web.zip, worker.zip, efbundle.exe, manifest); image digest `sha256:88a15b297b41abb5728190620d0f2d6d5d18f41baccea659fad57c951db63631`; migrationIdentity `20260814092852…DropBoxFileRequests` unchanged (no pending migration; `git diff d8de29cb feda958f -- Migrations infra` empty)
- [x] `-Mode Artifact` passed; manifest SHA-256 `B9D51070E03512408B79579A804C3E4AB124850D497B0E999878A5749D934A6D`
- [ ] **HELD by operator (2026-08-19 08:20Z)** — `MERGE AUTH GRANTED` not given; no push to `main`, no Azure write performed
- [ ] Atomic fast-forward `main = dev = feda958f`; main-push guard green
- [ ] `azd env refresh`; `-Mode PreUpload`; `oras cp`; digest check; `-Mode PreMigration`; `-Mode PreProvision`
- [ ] `azd provision --preview` / `azd provision`; readback; worker `config-zip`; smoke
- [ ] Production visual evidence; docs refresh PR; proof; closeout

## Progress notes

- 2026-08-19: Everything local is done and retained in the worktree; resuming needs only the operator's `MERGE AUTH GRANTED` + the three named Azure write targets. If `dev` moves before then, re-cut the artifacts at the new head (the lease-checked push refuses a stale SHA anyway).
