# Plan

## Objective

Sterilize Pegasus production intake-generated Blob and SQL data, verify the operator-facing empty state, promote and deploy frozen release candidate `0f0e90ae44ffda7339ca2a460310deeb98121afa` as release 38, then record and independently review the current-state documentation before a final promotion-only update.

## Starting state

- Ticket: `PLAT-067`, Preparing, sole member of `HZN-004`.
- Candidate: `0f0e90ae44ffda7339ca2a460310deeb98121afa`.
- Previously observed main: `fb3f07acc8cca8d9d8b57db8a431b607772436dc`.
- Exact workflow-evidence waiver: `5a40d15762b83a7c18ab431434cca7eba7b9a030` and `9b8f78a36151313bc6d48625edee7f13a2173127` only.
- Primary checkout contains user-owned modifications and untracked files and must remain untouched.
- The wipe script and wipe skill are present only as user-owned untracked files in the primary checkout; use them in place without adding them to the evidence PR.

## Governing docs

No PRD, FRD, or ADR changes are required. Follow `docs/runbook.md` for live-operation approvals and release commands, `docs/engineering.md` for delivery evidence, and update the living snapshots `docs/operations.md` and `docs/current-architecture.md` after deployment.

## Required changes

1. Create and use `task/plat-067-wipe-release-38` at `C:/Users/Alex/Documents/GitHub/pegasus-worktrees/plat-067-wipe-release-38`.
2. Run a fresh read-only wipe inventory, obtain exact approval, execute, verify Blob/SQL state, and verify the authenticated Web UI is empty.
3. Re-fetch and freeze the Git candidate; verify ancestry, the named PR/check evidence, and the two exact direct-commit deviations.
4. Read current Azure/deployment state, including any release performed elsewhere since the last recorded release.
5. Obtain fresh `MERGE AUTH GRANTED`, atomically promote the exact candidate, and require equal remote read-back.
6. Build immutable `0.1.0-alpha.1` artifacts in a disposable detached exact-SHA worktree, validate local/artifact plans, and record manifest, digest, source, Worker package, migration identity, and operations.
7. Obtain exact immutable-manifest Azure-write approval; upload, provision, deploy, read back, smoke, and run focused non-destructive evidence.
8. Retain release artifacts outside the disposable worktree.
9. Update only `docs/operations.md` and `docs/current-architecture.md` in the ticket worktree with precise observed evidence and limitations.
10. Run required validation, record the docs-only simplification disposition, open a PR to `dev`, and stop for independent review/merge.
11. After independent merge, verify the docs-only range, obtain a new `MERGE AUTH GRANTED`, promote only, read back equality, verify on merged main, write proof, and close out.

## Expected files

- `docs/operations.md`
- `docs/current-architecture.md`
- Ticket pipeline documents and HZN-004 automation records through Kanmer MCP only.
- Retained release evidence under the repository's existing ignored artifacts location.

## Do not modify

- User-owned primary-checkout changes: `.codex/config.toml`, `opencode.json`, the existing primary `docs/operations.md` modification, the untracked wipe skill/script, test material, or session-plan file.
- `corpus/`, Outlook, Graph, Box, `authentication-ring`, `box-links`, `pegtrans252ow37gij`, preserved SQL tables, or sequence state.
- Any source, infrastructure, dependency, migration, or runtime configuration in the evidence PR.

## Constraints

- Windows and PowerShell 7 for the complete evidence record.
- Wipe approval, merge authority, and immutable deployment approval are three separate just-in-time boundaries.
- Stop on candidate drift, non-ancestry, preserve-list drift, partial wipe, unexpected migration/database operation, digest mismatch, failed read-back/smoke/focused check, or unknown PR/check/merge state.
- Never rebase/reset shared refs, retry a failed atomic promotion with a different SHA, use an unleased force push, improvise deployment, self-review, or self-merge.
- The direct-commit waiver does not waive build, artifact, smoke, or focused validation.
- Release evidence must account for a deployment that may have occurred on another system.

## Ordered steps

1. Reconcile HZN-004 and take PLAT-067 on the recorded branch/worktree.
2. Run `pwsh ./scripts/Invoke-IntakeDataWipe.ps1`; capture exact counts and preservation/sequence values.
3. Pause for exact wipe-write approval. Then run the script with `-Execute`, require its post-checks, and perform authenticated empty-state verification.
4. Fetch origin; verify the frozen SHA, main ancestry, candidate diff, direct commits, PRs 638/640/641/642/643 and checks.
5. Read Azure account, active Web revision/digest, Worker schedules/activation, migration head, and rollback position.
6. Pause for fresh literal `MERGE AUTH GRANTED`; execute the canonical atomic two-ref promotion and verify both refs.
7. Create a disposable detached worktree at the promoted SHA, copy only `.azure/pegasus-prod`, build and validate immutable release artifacts.
8. Confirm migration identity remains `20260829212237_GrantProviderSubmissionAcceptRecovery` and the plan contains no database/bootstrap write.
9. Pause for exact manifest-bound Azure-write approval; run PreUpload, upload with ORAS, validate digest, validate azd values, set approved digest/suffix, run PreProvision, provision, read back Web, and config-zip deploy Worker.
10. Run exact-release smoke and focused checks: authenticated Inbox preview persistence, normal poll progress/no new Worker failure, and local/artifact QDOS policy/corpus evidence. Do not fabricate live malformed Graph data.
11. Retain artifacts and exact outputs; update the two living current-state documents.
12. Run locked restore/build/non-Corpus tests and documentation checks; no UI snapshots because no routed page changes.
13. Record `n/a — current-state documentation and release evidence only` for simplification, commit, push, open PR, and stop for independent review and merge.
14. After merge, make a fresh promotion-only preflight and authority request, promote without redeploying, verify merged-main evidence, write proof, and close out.

## Acceptance checks

- Wipe: zero transient-intake blobs, zero wiped-table rows, committed constraint-checked SQL transaction, preserved state present, unchanged sequences, untouched excluded systems, and authenticated empty Web UI.
- Release: exact SHA equality on both refs, immutable manifest and remote image digest equality, no unexpected migration/database write, healthy digest-pinned single Web revision at 100% traffic, successful Worker config-zip deployment, exact version/source smoke, and honest focused evidence.
- Documentation: both current-state documents match observed production, validation passes, PR is independently reviewed and merged, final promotion is docs-only, and proof is written against merged main.

## Commands

- `pwsh ./scripts/Invoke-IntakeDataWipe.ps1`
- `pwsh ./scripts/Invoke-IntakeDataWipe.ps1 -Execute` only after exact approval.
- Canonical commands from `.agents/skills/pegasus-release/SKILL.md`.
- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`

## Failure and deviation rules

Every failure is retained and reported. Do not proceed across a failed wipe, Git, artifact, migration, deployment, smoke, focused, test, or review gate. A changed remote or deployed state is reconciled from live read-only evidence and requires a revised frozen candidate/plan when material.

## Stop condition

Stop at each exact operator-only approval boundary. After the documentation PR is open, stop for independent review and merge. Complete only after the final promotion-only update, merged-main verification, proof, and closeout.
