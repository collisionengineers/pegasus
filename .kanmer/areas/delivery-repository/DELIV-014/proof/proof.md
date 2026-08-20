# Proof — DELIV-014 (command-log)

Release 15 executed end-to-end on 2026-08-20, verifiable now:

## Merge queue
PRs #476, #478, #479, #481, #482, #483, #484, #485, #487, #488 each merged to dev only on green CI (flake handling: two close/reopen refreshes for the stale-merge-ref `changes` job, two `--failed` reruns of runner-cancelled documentation jobs and one SQL-timeout browser shard — all substantive failures fixed, never rerun-to-green). Final dev head for the cut: `6d04f89d`.

## Deploy (runbook route, immutable artifacts)
- `Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.1 -SourceRevision 6d04f89d…` from a clean worktree at the exact HEAD; manifest SHA-256 `3D652838…`, migration identity `20260820144004_RetainedMailFolderMoves`, image digest `sha256:07c05faa…`.
- Validation: Artifact ✔, PreUpload (manifest-SHA-gated) ✔, PreProvision (enabled estate: desired and live both `approved-live-worker`) ✔, PreMigration ✔.
- `azd provision --preview` byte-compare vs `artifacts/releases/release-14-d91fd7d7/azd-preview.txt`: sole substantive delta is `revisionSuffix d91fd7d7835a → 6d04f89d4d30` (plus timing/CLI-notice lines) — no infra drift.
- Image pushed to `pegasusprodacr252ow37gij` via oras with the exact digest preserved; both new migrations applied by `efbundle.exe` (DB head `20260820144004_RetainedMailFolderMoves`); `azd provision` activated Web revision `--6d04f89d4d30`; Worker deployed by `config-zip` ("Deployment was successful").
- `Invoke-ProductionSmoke.ps1` full mode: **passed** (health, exact version `0.1.0-alpha.1` + source SHA `6d04f89d…`, anonymous denial, https redirect, Worker `approved-live-worker`).
- Artifacts retained at `artifacts/releases/release-15-6d04f89d/` in the main checkout; azd `.env` (new digest/suffix) copied back.

## Post-deploy steps
- DOCS-005 Box cleanup: eight legacy binding JSONs deleted (204s) from `a.QDOS26001`–`a.QDOS26004` under root `405543781910`.
- Live checks (signed-in browser): Queues = new dropdown/merged-table surface, wiped-empty, badges 0; Inbox honest empty state; Upload healthy; staff sign-in works. Data-dependent flows (upload one-unit card, extraction facts, auto lookup, case/assessment pages) are covered by the merged suites and left for the operator's next real traffic to keep the wiped estate sterile.

## Docs and promotion
- Docs PR #489 (operations release-15 row + narrative; current-architecture custody/lookup updates; runbook custody-validation line) merged on green.
- Promotion: `git push --atomic --force-with-lease` — **main = dev = `f0b01f39`**, exact-SHA fast-forward, non-force, under the operator's standing MERGE AUTH.

## Closeout
PLAT-016, INTK-020, INTK-021, CASE-005, CASE-007, CASE-008, INTK-022, ENG-006, MAIL-005, DOCS-005, PLAT-017 all carry proofs and reached done. Release-scope worktrees/branches removed; codex-mcp-client lanes untouched (PRs #470, #473 and their worktrees/branches). TICK-102/TICK-104 remain held at verifying on the closed `Features:SendToAi` gate as before.
