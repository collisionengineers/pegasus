# Plan — DELIV-037

Release 37 followed `.agents/skills/pegasus-release/SKILL.md` as a **full
release** (code, infrastructure, runtime configuration and migrations all
changed), then a **promotion-only** pass for the documentation.

1. Full local gate on final `dev`.
2. Fresh `MERGE AUTH GRANTED`; atomic exact-SHA promotion; read both refs back.
3. Build artifacts in a detached worktree at the promoted SHA; `Local`,
   `Artifact` and `PreUpload` gates; record the manifest SHA-256.
4. `oras cp` the image; the fetched digest must equal `manifest.webImage.digest`.
5. **Migrate before packaging** — `PreMigration`, `efbundle.exe` from
   `src/Pegasus.Web` with the full Production process environment, then
   `Invoke-AzureDatabaseBootstrap.ps1`, then verify the live head.
6. `azd provision`; read the active revision and digest back.
7. Worker by `az functionapp deployment source config-zip`. **Never
   `azd deploy worker`.**
8. `Invoke-ProductionSmoke.ps1`, then the diagnostics review.
9. Retain the artifacts outside the disposable worktree.
10. Documentation by reviewed PR to `dev`, then a second freshly authorised
    promotion-only pass.

## Stop predicates

Any preflight mismatch, failed gate, upload digest mismatch, migration head
mismatch, provision or deploy error, smoke failure, or any Azure write not
listed above → stop, leave state as-is, report exactly what ran.

None fired.

## Deviations from the plan, and why

- **The snapshot verify was not re-run locally before promotion.** The tree of
  the promotion SHA is byte-identical to the branch head CI's `test-ui` job had
  already verified (`git rev-parse <sha>^{tree}` equal), so re-deriving the same
  result would have cost 25 minutes for no additional evidence.
- **`efbundle` failed on its first invocation.** `ConnectionStrings__Pegasus`
  was omitted from the process environment — the runbook lists it and it was
  missed. The host failed to construct, so **no migration was applied** and the
  retry was clean rather than partial.
- **The canonical suite was killed twice before it completed.** Both were
  resource exhaustion on this workstation, not test failures: 15 stray `dotnet`
  processes from the evening's parallel lanes had left 7.7 GB free of 32 GB, and
  the test host was killed ~1-2 minutes into the integration suite with
  `MSB4166`. `dotnet build-server shutdown` plus reaping one stale test host
  freed it, and the suite then ran to completion.
- **The `web-image.tar.gz` was not retained** (1.4 GB). The image is in the ACR
  under its source-SHA tag, which is what a rollback pulls.

## Simplification pass

n/a — release operations and documentation.
