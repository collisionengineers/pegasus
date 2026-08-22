# Plan — releases 17 to 20

Filed while the run was in flight, so the plan is short and the proof carries
the weight. The route itself is not re-derived here: it is written once in the
repository release skill (`.agents/skills/pegasus-release/SKILL.md`, mirrored in
`.codex/` and `.claude/`), which is the artefact this run produced for exactly
this purpose.

## Per release

1. Merge the reviewed PR into `dev`.
2. Confirm `git merge-base --is-ancestor origin/main origin/dev`, then one
   atomic exact-SHA push updating `main` and `dev` together. Read both heads
   back; unequal read-back stops the release and is never repaired by a rebase
   or force push.
3. `Build-ReleaseArtifacts.ps1` from a clean tree at that exact HEAD.
4. `Test-AzureDeploymentPlan.ps1 -Mode Artifact`, then push the image with
   `oras cp` from the OCI archive — there is no Docker on this workstation, so
   `az acr login` needs `--expose-token` and `oras login`.
5. Set `PEGASUS_WEB_IMAGE_DIGEST` and `PEGASUS_WEB_REVISION_SUFFIX` in the azd
   environment **before** provisioning. The environment carries the previous
   release's values and will otherwise fail with "revision with suffix … already
   exists".
6. `-Mode PreProvision`, then `azd provision`, then the Worker by
   `az functionapp deployment source config-zip`.
7. Migrations only where there is one — releases 17, 18 and 19 had none;
   release 20 has `20260822044425_GrantWorkerCaseDocuments`.
8. `Invoke-ProductionSmoke.ps1` against the exact source revision.
9. Refresh `docs/operations.md` and `docs/current-architecture.md`.
10. Delete the merged branch locally and remotely, remove the worktree, and
    copy `artifacts/releases/` out before doing so.

## Acceptance

Smoke green at each release's exact SHA; the serving revision carrying 100% of
traffic at that SHA; release 20's migration applied and its grants read back
from `sys.database_permissions`; the current-state documents matching what
shipped; and the git end state back to two worktrees, three local branches,
three remote branches and no open PRs.

## Simplification pass

n/a — this ticket ships no source change. Each release's own tickets recorded
their passes.
