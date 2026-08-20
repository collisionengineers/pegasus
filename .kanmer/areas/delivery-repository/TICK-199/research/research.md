## Research — TICK-199: `.infisical.json`

### Checks run (read-only, in worktree `../pegasus-worktrees/tick-199` on `origin/dev`)

1. `git log --follow -- .infisical.json` — file was added in the repo's root-baseline commit
   (`e65eb2e1`, per CHANGELOG.md entry 1) and only ever touched incidentally by unrelated
   commits (merges/rebases show it in the tree, no commit edits its content path
   specifically). CHANGELOG.md's own account of that commit calls it "an Infisical
   configuration placeholder."
2. `grep -rn "infisical"` (case-insensitive) across the tracked tree found real, current
   references only to the **Infisical CLI as a tool**, never to the file:
   - `docs/runbook.md` — pins CLI version 0.43.104 in the tool table; states the `Baseline`
     local-dev profile requires **no** Infisical; and one policy sentence ("store unavoidable
     third-party secrets in Infisical or Key Vault").
   - `docs/operations.md` / `docs/current-architecture.md` / `docs/capabilities.md`
     (OPS-06) — same policy-level statement (secret custody may use Infisical), not a
     procedure that reads this file.
   - `scripts/Invoke-Doctor.ps1` / `scripts/PegasusPlatform.ps1` — check/install the CLI
     binary and its version; neither reads or writes `.infisical.json`.
   - `docs/adr/0002-dotnet-modular-monolith-on-azure.md` — passing mention of secret-storage
     options.
3. `grep -rn "infisical " ` over `*.ps1`/`*.yml`/`*.yaml` and `.github/workflows/` for an
   actual invocation (`infisical run`, `infisical secrets`, `infisical export`, etc.) —
   **zero matches**. No workflow or script consumes the file.
4. `grep -rn "\.infisical\.json"` (the literal filename) across the whole tracked tree —
   **zero matches** anywhere, including docs, scripts, and CI.
5. Confirmed the file is tracked (not gitignored) and its content is a workspace pointer
   only: `{"workspaceId": "...", "defaultEnvironment": "", "gitBranchToEnvironmentMapping": null}` —
   no secret value, just an Infisical Cloud project id.

### Finding

The Infisical **CLI** is a real, documented, pinned administration tool (Doctor check,
install hint, runbook tool table) reserved for future/optional live secret-custody work —
that tool listing is out of this ticket's scope and is left untouched. But
`.infisical.json` — the CLI's own workspace-linking config, which only matters to commands
like `infisical run`/`infisical secrets` — has **no supported consumer**: no script, no CI
job, no runbook procedure, and no other doc references it, by filename or otherwise. It is
exactly the "configuration placeholder" the CHANGELOG already calls it.

### Decision

Retire the file (`git rm .infisical.json`). No stale references exist elsewhere to clean
up (checked #3/#4 above — none found). No secret value was read, copied, rotated, or
printed; the workspace id above is a project pointer, not a credential.
