## Takeover — 2026-08-19

Agent: claude-code. Release ticket: DELIV-012. Reason: operator decision (handed
off from a prior agent mid-PR #423, review stage). Worktree
`.worktrees/intk-008`, branch `intk-008-image-initiated-lifecycle`.

On pickup: `git status` showed one uncommitted change, `CONTEXT.md` (the Case
and Image intake glossary-entry rewording for Image-initiated Case
terminology). Read it, judged it clearly part of this ticket's own
terminology work (matches the ticket's "reconcile ... CONTEXT.md" scope), and
committed it as-is before merging `origin/dev` (branch was 4 ahead / 7 behind).

## Operator ruling to implement (supersedes current PR wording)

Operator was shown this PR's `docs/operator-notes.md` rewording and answered,
verbatim, 2026-08-19:

> It could be either an image initiated case, OR it could be images being
> received for an existing case. ie if we get images, with a registration that
> doesnt match any existing case, then that creates an image initiated case.
> If they match an existing case (by VRM), then get get attached as evidence
> to that case.

Both branches must be stated explicitly in `docs/operator-notes.md` (protected
— add/clarify, never delete the existing "definitive match / linked manually
by staff" sentence), and the PRD/FRD wording must agree with the same two
branches. Implemented as a new "Two branches for a readable registration"
subsection under the existing 2026-08-19 Image-initiated Case clarification in
operator-notes.md, plus matching paragraphs in `docs/prd/pegasus-product.md`
and `docs/frd/frd-02-intake-and-source-identity.md` (the FRD paragraph also
reconciles the two-branch business framing with the actual mechanism: the
pipeline still allocates the Image Intake Reference and merges in the same
pass when there is a match, so what the operator sees is images already
attached as evidence — not a contradiction, just business outcome vs.
mechanism).

## Mid-task addition — 2026-08-19 (coordinator)

Coordinator added a mandatory release-route step: `scripts/Test-AzureDeploymentPlan.ps1
-Mode Local` now asserts every grant-carrying migration is named in
`scripts/Invoke-AzureDatabaseBootstrap.ps1`'s expected-permission census
(CI enforces this from PR #426 onward). My migration
`20260819112914_ImageInitiatedLifecycle` carries a GRANT, so it needed a
census entry.

Added to `scripts/Invoke-AzureDatabaseBootstrap.ps1` (before the function's
final `return`): four `$expected.Add(...)` lines for
`ImageIntakeLifecycleEvents` — `pegasus_web_runtime_role` GRANT SELECT,
GRANT INSERT, DENY UPDATE, DENY DELETE — matching the migration's SQL
exactly (Web-only; Worker never touches this table, confirmed by
`git grep -n ImageIntakeLifecycleEvent -- src/`).

Verification:
- `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` on the real
  worktree throws at the pre-existing gap for
  `20260819104953_MailClassificationCorrectionHistory` — not my migration,
  confirmed by name; this is expected until PR #426 merges (coordinator
  confirmed).
- To verify my own entry regardless, copied `scripts/` + `infra/` +
  `azure.yaml` + `src/.../Migrations/*.cs` to a disposable scratch
  directory, added a throwaway diagnostic-only stub comment mentioning
  `20260819104953_MailClassificationCorrectionHistory` (not committed
  anywhere, not touching any tracked file, deleted after), and re-ran
  `-Mode Local` there: **passed clean** ("Azure deployment plan validation
  passed"), proving my `ImageIntakeLifecycleEvents` census entry is complete
  and correct and nothing else in this branch regresses the check.
- `pwsh ./scripts/Test-MigrationGrants.ps1` — **does not exist** in this
  worktree or on `origin/dev`. `git log --all --diff-filter=A -- '**/Test-MigrationGrants.ps1'`
  finds it only on `origin/task/deliv-012-grant-and-docs-fixes` (commit
  2843c5b8), a different in-flight task branch. Did not import it (that
  would be touching another task's unmerged work without authorisation).
  Flagging this back to the coordinator rather than silently skipping it.

## QdosAllocationRecoveryTests — CASE-005 (coordinator update)

Coordinator confirmed `DistinctParallelRetriesResolveToOneCaseAggregate` is
pre-existing/intermittent on clean `dev` (filed as CASE-005); no further
bisection needed, only confirm not made worse by the optional
`IImageIntakeStore` parameter on `ImageIntakeCasePairing`. Evidence: ran the
full `QdosAllocationRecoveryTests` class on this branch — 15/15 passed; ran
the specific test standalone twice more — 2/2 passed. No regression
attributable to this branch. Referencing CASE-005 in the final report rather
than re-investigating.
