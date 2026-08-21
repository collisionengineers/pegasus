# Release 16 plan

Approved by the operator 2026-08-21 (plan-mode approval + open-PR decision: review and merge all five).

## Sequence

1. **Phase 0 (done)** — local dev rebased-by-merge onto origin/dev and pushed (82fd4fd3); `docs/principal-rules-and-mappings/` (README + qdos.md, index link, Excel-lock cleanup) committed to dev (e9cdf2b2).
2. **Merge the 5 open PRs** serially on green CI, each independently reviewed by claude-code (implementer was codex): #495 ENG-007 → #497 PLAT-021 → #496 PLAT-020 (review fix added: migration census + bootstrap matrix entries for 20260821100623) → #473 MAIL-004 (dev merged in with census/capabilities conflict resolution; PR-026 visual gate performed and recorded) → #470 AUTO-004/005. CI flake taxonomy: stale-merge-ref `changes` → close/reopen; SQL timeouts → rerun failed.
3. **Lost-work audit** — recorded in research: all 14 pre-existing branches patch-complete in dev; mail-workspace overlap spot-checks pass.
4. **Build** — pin release SHA = origin/dev head; `scripts/Build-ReleaseArtifacts.ps1` → `artifacts/releases/release-16-<sha8>/`; `scripts/Test-AzureDeploymentPlan.ps1` must pass. Check C: free space first.
5. **Promote** — dev→main PR, green CI, then **stop for `MERGE AUTH GRANTED`**, then exact-SHA fast-forward push (DELIV-002 policy).
6. **Deploy** (targets: rg-pegasus-prod, ACR pegasusprodacr252ow37gij, web CA pegasus-prod-web-252ow37gij, worker pegasus-prod-worker-252ow37gij, KV pegasusprodkv252ow37g, sub e6076573-23a5-46a8-acef-7e22d264e5db): oras cp image → set digest/suffix → azd provision (no `azd env refresh`; verify six `*_SECRET_URI` → pegasusprodkv252ow37g; retain `PEGASUS_WORKER_ACTIVATION=approved-live-worker`); efbundle migrations (adds 20260820114412, 20260821095500, 20260821100623); worker config-zip; `Invoke-ProductionSmoke.ps1`; grant readback.
7. **Docs** — refresh current-architecture.md + operations.md on this ticket's branch, PR → dev, merge on green, second promotion (second MERGE AUTH).
8. **Verify + close** — per-ticket live verification grouped by capability; proofs written; deployment set; verifying→done serially; gated-off capabilities stay at verifying with honest notes.
9. **Hygiene** — delete all merged task branches (remote+local) and worktrees; end state 3 branches / 2 worktrees; 0 open PRs.

## Reuse

Release-15 method exactly (artifacts, oras route, efbundle env set, config-zip, smoke script); no new mechanism.
