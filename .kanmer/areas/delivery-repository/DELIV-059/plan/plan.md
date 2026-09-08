# Plan — DELIV-059: restore release-39 history to canonical operations

## Objective

Restore a qualified, dated release-39 historical record and the minimal
Retained evidence index correction in `docs/operations.md`, so PR #676 can
later be disposed with its unique operational provenance preserved without
treating the record as a fresh deployment observation.

## Starting state

Evidence: `research/research.md`@`68a5c3e952a357d7`,
`files/files.md`@`8958e49a06a1fe21`, live ticket revision
`rev1:32b3cb93842a2810`; no project research sources are declared. Current
`dev` is `05995d325cc4c1ccd44096bf69d05fd42eeda3d2`; PR #676 historical
operations source is `93255e5c188865e5b0dab95d6c628ab49a902d99`.
`docs/operations.md` currently ends its observed estate at release 38, and
the baseline ledger it links lacks the release-39 record.

## Governing docs

- `docs/index.md` — **Meets** the placement rule: the dated operational
  record belongs in Operations, while source structure stays in Architecture.
- `docs/engineering.md` — **Meets** evidence-tier rules by distinguishing a
  Git source commit, a historical PR narrative, CI receipt facts and fresh
  environment/artifact evidence.
- `docs/adr/0007-direct-terminal-azure-deployment.md` — **Meets** the retained
  direct-terminal/provenance principle. This plan records no new procedure,
  approval or deployment decision and does not modify the ADR.

## Required changes

Add one compact dated historical release-39 section to
`docs/operations.md` for the operator/support audience.

- Correct the Retained evidence section so it no longer calls the older baseline
  ledger the complete prior release ledger: it must index that earlier record
  together with the new current-file release-39 historical entry, without
  duplicating the ledger or claiming a fresh observation.
- Attribute the release source, image/manifest identifiers, migration head,
  failed original Worker ZIP, replacement Worker package/provenance difference,
  partial migration/reset and smoke/telemetry statements to the retained
  unmerged PR #676 operations record, not to a fresh D59 Azure read.
- Preserve the source identity as independently checkable Git history, and
  preserve the GitHub run's narrower independently checkable result: `test-ui`
  failed at that SHA while `browser` succeeded.
- Do not write the old browser-cancelled or two-lane-waiver wording as verified
  fact. If an operator waiver is retained at all, mark it as a PR #676
  historical assertion with no retained authorisation receipt found by D59.
- State the local-artifact limitation: D59 did not read the manifest, original
  or replacement ZIP, deployment receipt, Azure telemetry, reset record or
  current estate. Do not use the historical entry to claim a present deployment
  state.
- Keep exact failures and recovery limits: the original manifest package was
  rejected; the same-SHA replacement was not byte-identical and therefore is
  recorded rather than equated with the manifest; partial migrations and reset
  were historical facts requiring their own past authorisation.
- Do not copy release facts into architecture, change release/runbook guidance,
  add operational commands, secret values or current deployment claims.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `docs/operations.md` | Add the qualified dated historical release-39 record and minimally correct its Retained evidence index; no generated artifact. |

## Do not modify

- `docs/current-architecture.md`
- `docs/runbook.md`
- `.agents/skills/pegasus-release/**`
- `docs/adr/**`
- `scripts/**`
- `src/**`
- `tests/**`
- `.kanmer/**`

## Constraints

- Audience: operators and future recovery/release decision-makers who need an
  honest dated record, not a procedure or approval.
- Source of truth: current documentation ownership, the exact PR #676
  historical record, Git ancestry, and the named CI run only for facts each
  proves. No project sources are declared.
- Version sensitivity: the entry is explicitly dated 7 September 2026 and
  identifies its PR/source provenance; it must say it is not a fresh D59
  observation.
- Do not expose secret values, credentials, connection strings or Key Vault
  material. Existing abbreviated identifiers in the retained historical source
  stay only where necessary to preserve artifact/reset provenance.
- Documentation-only scope: no .NET restore/build/test, artifact package,
  Azure/database inventory, migration, reset, deployment, promotion or smoke
  is authorised or required.
- PR #676, linked-ticket records, source branches/worktrees, artifacts and cloud
  resources are foreign scope. This ticket neither changes them nor supplies
  authority to merge, close, promote or deploy.
- The final historical document merge SHA, together with PR #703 merge
  `6509746913eda16d2c4440add20e7f6793500f0b`, is evidence for root's later
  PR #676 disposition; this ticket does not itself close or merge that PR.

## Ordered steps

### Step 1 — Write the qualified release-39 historical record

- Preconditions: the worker has read current `docs/operations.md`,
  `docs/index.md`, `docs/engineering.md`, D59 research/files, PR #676's
  operations record at `93255e5…`, and CI run `34132950893`.
- Files: `docs/operations.md`
- Change: add the single historical release-39 entry with its precise
  provenance/limits, all D1-required failed-package, replacement-provenance,
  partial-migration and authorised-reset facts, and the CI discrepancy; amend
  Retained evidence so it indexes the new entry plus the earlier baseline rather
  than falsely describing the baseline alone as complete.
- Preserved behaviour: the 6 September release-38 read remains a dated
  observation; Operations remains the single deployment/support owner;
  Architecture and current release procedure remain unchanged.
- Forbidden: no unqualified current-state assertion, browser-cancelled claim,
  verified-waiver claim, new release command, secret, permission, artifact,
  source-policy or deployment-authority statement.
- Negative cases: facts that lack an independently readable receipt retain
  `PR #676 recorded`/historical wording; no absent local artifact is described
  as revalidated.
- Tests: the author performs only static Git/content inspection against research
  provenance. The sole host verifier runs documentation links and placement
  after a committed exact head is available.
- Commands: perform only the author static inspections below after the edit;
  do not run PowerShell verification scripts.
- Expected output: before commit, the only worktree diff path is
  `docs/operations.md`; after commit, the host verifier observes that same
  single path from the frozen base to the actual committed head. Its scripts
  exit 0, or any obsolete-parser failure is reported rather than papered over.
- Done when: one compact historical entry preserves the required failures and
  explicitly distinguishes PR-recorded claims from independently checked facts.
- Deviation stop: stop if the historical entry needs another file, a fresh
  Azure/artifact read, a hidden secret, an unsupported claim, a procedure
  change, or a decision about PR #676 closure.

### Step 2 — Check the documentation-only delta and prepare review evidence

- Preconditions: Step 1 is complete and the working tree contains only the
  planned Markdown edit.
- Files: `docs/operations.md`
- Change: make no new content change unless required to correct a discovered
  factual/provenance error within the same entry.
- Preserved behaviour: all current source architecture, procedure and release
  authority remains byte-for-byte outside the one file.
- Forbidden: no broad reformatting, history rewrite, check weakening, source
  edit, PR action or live operation.
- Negative cases: a failed link/placement check, a second changed file, or a
  mismatch with the PR/CI evidence blocks handoff.
- Tests: independent semantic review of provenance, the CI contradiction and
  scope boundary; documentation scripts are reserved to the sole host verifier.
- Commands: author runs only static Git inspections; the host verifier runs the
  exact frozen-base/committed-head commands below.
- Expected output: clean static diff checks, then host-verifier-valid
  links/placement and a one-file frozen-base diff whose claims are traceable
  and qualified.
- Done when: the author has recorded command exits and handed the one-file
  diff to an independent reviewer; no merge is attempted.
- Deviation stop: stop if validation demands unrelated parser contracts,
  source/test changes, a new artifact build or live evidence.

## Acceptance checks

- The only intended production-facing caller is the Operations documentation
  reader; no application caller, artifact or schema changes apply.
- `docs/operations.md` holds the dated release-39 history and D1-required
  failure/provenance/reset material, and its Retained evidence section indexes
  the new entry with the earlier baseline rather than asserting the baseline
  alone is complete; no statement upgrades unverified PR claims to fresh D59
  evidence.
- The source SHA and CI `test-ui` result are traceable; the browser-success
  contradiction and missing waiver receipt are explicit.
- Current architecture, release/runbook procedure and all application/build
  files remain unchanged.
- Relevant documentation checks and independent semantic review pass. The
  post-merge exact-SHA proof remains a later Kanmer verification obligation.

## Commands

The author may perform static inspection only, before committing:

```powershell
git diff --check -- docs/operations.md
git diff --name-only
git diff -- docs/operations.md
```

Before the edit, record the exact current integration-base commit in the
execution record. Do not later substitute a moving `origin/dev`. After the
one-file commit exists, the **sole host verifier** runs these commands from its
exact committed-head verification worktree, sequentially:

```powershell
$baseSha = '<recorded frozen integration-base SHA>'
$headSha = (git rev-parse HEAD).Trim()
git rev-parse --verify "$baseSha^{commit}"
git rev-parse --verify "$headSha^{commit}"
git merge-base --is-ancestor $baseSha $headSha
git diff --check "$baseSha..$headSha"
$changed = @(git diff --name-only "$baseSha..$headSha")
if (@($changed | Where-Object { $_ -ne 'docs/operations.md' }).Count -ne 0 -or
    $changed.Count -ne 1) {
    throw 'DELIV-059 changed a path outside docs/operations.md.'
}
pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base $baseSha -Head $headSha
```

This binds scope, name and placement to the same frozen base and actual
committed head, so neither uncommitted content nor later integration-branch
drift can make the evidence pass. Do not run `dotnet restore`, `dotnet build`,
`dotnet test`, release packaging, Azure CLI, azd, migrations, cloud inventory
or smoke commands for this prose-only change.

## Failure and deviation rules

Stop and report failing checks, unavailable PR/CI evidence, a claim whose
provenance cannot be expressed honestly, a second file requirement, any secret
exposure, a request for fresh cloud/artifact verification, or any merge/closure
decision. Do not weaken documentation checks or revise another ticket's
historical evidence to obtain a pass.

## Stop condition

Stop after the one-file documentation delta, its documentation-only evidence
and independent-review handoff are ready. Do not merge, close PR #676, alter a
linked ticket, promote, deploy or begin another ticket.
