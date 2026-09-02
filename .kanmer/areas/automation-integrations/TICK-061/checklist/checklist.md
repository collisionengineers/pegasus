# Checklist — TICK-061

- [x] Core `PrincipalCredentials.cs`: record, state, policy, ports, four commands.
- [x] Infrastructure entity, model configuration, `EfPrincipalCredentialStore`, DbSet, DI.
- [x] Migration `PrincipalApiCredentials` + `GrantPrincipalApiCredentials` + bootstrap census; both scripts pass.
- [x] `docs/capabilities.md` API-01/API-04 reallocated; summary recounted.
- [x] Core tests, integration store tests, migration census names.
- [x] Release build green; simplification pass recorded; post-implementation report; PR opened.

## Progress notes

- 2026-08-28: commits 4b9e4df0 (Core), 8c1a4643 (Infra + migrations + census), docs, tests, b5c09412 (simplification). `dotnet build ./Pegasus.slnx --configuration Release` exit 0; `Test-MigrationGrants.ps1` exit 0 (80 files); `Test-AzureDeploymentPlan.ps1 -Mode Local` exit 0. Tests not run by the implementer.

## Closeout — TICK-061

- [ ] PR merge verified (`gh pr view --json state,mergedAt`)
- [ ] proof.md finalised (PR URL + merge date appended)
- [ ] Moved to final stage
- [ ] Outcome recorded in ticket body (PR link, follow-ups)
- [ ] cd out of worktree; `git worktree remove` recorded workspace
- [ ] `git branch -d task/tick-061-provider-credentials` (`-D` only if required)
- [ ] `git fetch --prune` + `git worktree prune`
- [ ] `take_ticket action: "release"`
