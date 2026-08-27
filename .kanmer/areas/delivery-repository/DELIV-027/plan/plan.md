# Plan — DELIV-027 (release 34)

Follows `.claude/skills/pegasus-release/SKILL.md` exactly; nothing new.

Preconditions: #567 ([[MAIL-016]]), #566 ([[MAIL-015]]), #562
([[UIIMP-004]]) and #565 ([[DELIV-026]]) merged to `dev`, each with green CI
and an independent review. `dev` contains no other change.

1. Preflight: `git merge-base --is-ancestor origin/main origin/dev`;
   `git log origin/main..origin/dev`; before-state read-back of the active Web
   revision and Worker schedules (the seven-field value is the "before").
2. Promote: `SHA=$(git rev-parse origin/dev)`; `MERGE AUTH GRANTED` was given
   by the operator with the approved plan on 2026-08-27 and is restated in
   the transcript immediately before the atomic `--force-with-lease`
   fast-forward push of `main` and `dev` to `$SHA`; read back both refs.
3. Artifacts: clean `main` checkout, `Build-ReleaseArtifacts.ps1 -Version
   0.1.0-alpha.1 -SourceRevision $SHA`; `Test-AzureDeploymentPlan -Mode Local`
   and `-Mode Artifact`. Expect `migrationIdentity` unchanged from release 33
   → no migration step.
4. `oras cp` image → ACR; digest must equal manifest.
5. `azd env set PEGASUS_WEB_IMAGE_DIGEST` / `PEGASUS_WEB_REVISION_SUFFIX`
   (12 chars); verify `*_SECRET_URI`, `PEGASUS_WORKER_ACTIVATION`,
   `AZURE_RESOURCE_GROUP`; `-Mode PreProvision`.
6. `azd provision --no-prompt` — required: `infra/modules/platform.bicep`
   changed (the schedule). Read back `ApprovedInboxPollSchedule=0 */5 * * * *`
   and all seven functions enabled.
7. Worker: `az functionapp deployment source config-zip` with `worker.zip`.
8. `Invoke-ProductionSmoke.ps1` with `$SHA`; active revision digest check.
9. Copy `artifacts/releases/0.1.0-alpha.1` → `artifacts/releases/release-34-<sha8>`.
10. After [[PLAT-045]] wipes: docs branch `task/deliv-027-release-34-docs`
    from `origin/dev` — release-34 row and paragraph in `docs/operations.md`
    (SHA, digest, revision, manifest hash, "no migration", schedule read-back,
    wipe counts, next case reference), date line in
    `docs/current-architecture.md`; PR → dev, review, merge.

## Simplification pass

n/a — release operations and docs.
