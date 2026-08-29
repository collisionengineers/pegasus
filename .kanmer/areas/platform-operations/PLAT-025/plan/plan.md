## Scope

Port `Pages/Administration/Configuration.cshtml(.cs)` onto the PLAT-029 design
system as a re-skin plus a copy trim of the existing two real settings. Do not
add the "Instruction completeness" or "Due work (chase interval)" controls —
research.md establishes there is no backing Core setting for either, and
building one needs a new Core port + migration, out of scope for this wave-2
lane (see Disposition below).

## Steps

1. **Re-skin the page shell.** Wrap the content in `.admin-layout` +
   `<partial name="Shared/_AdminNav" />` with `ViewData["AdminArea"] =
   "configuration"`, following `Pages/Administration/Index.cshtml`'s existing
   pattern. Reuse `panel` / `panel-head` / `section-label` classes from
   `Pages/Operations/Index.cshtml` (both already ported, merged to dev).
   Reuses: `_AdminNav.cshtml` (read-only), `_PageHeader` partial, existing
   `site.css` classes — no new CSS.
2. **Trim the copy.** Remove the `<aside class="notice">` explanatory
   sentence (banned per `docs/design/README.md` §No explanatory copy). Keep
   only the `h2` area label and a factual description/meta line (current
   policy version, e.g.), matching the contract's "content panel (h2 area
   label, description, meta)".
3. **Keep the two real settings under a "Review" heading**, matching the
   contract's "Review (2 checkboxes)" group — these are exactly
   `RequireStaffInstructionReviewBeforeEngineerAssignment` and
   `RequireStaffImageReviewBeforeEngineerAssignment`. Reuse: the unchanged
   `ConfigurationModel` bind properties, `GetWorkflowConfiguration` /
   `UpdateWorkflowConfiguration` Core port, `ExpectedVersion` /
   `OperationKey` optimistic-concurrency + idempotency handling, required
   `Reason` field, `WorkflowConfigurationVersionConflictException` /
   `WorkflowConfigurationOperationConflictException` handling. No business
   rule changes.
4. **Do not render "Instruction completeness" or "Due work" controls** — omit
   entirely (never as disabled/inert placeholders; D7's disabled-seam
   allowance is for named, ticketed integrations, not this).
5. **Labels.** Reuse `OperatorLabels.Admin.Configuration`. Add any new
   page-local label constants needed inside `OperatorLabels.cs` without
   reordering existing members.
6. **Tests.** Add/update a web test file asserting: the admin-layout wrapper
   and nav's current-area marker render, the two real checkboxes and Reason
   field render bound to the real model properties, the Save handler posts to
   the existing `OnPostAsync`, and a non-administrator is denied (matching the
   existing admin authorization test pattern, e.g.
   `AdministrationSearchAccountWebTests.cs`). Never weaken or delete an
   assertion to pass; if an existing test asserts old markup, update it to
   assert the new correct markup.
7. **Catalogue.** Update the existing
   `docs/design/test-ui/catalogue.json` entry for
   `administration-configuration--default` only if its description text is
   now wrong (structural edit only; no snapshot capture run here).
8. **Build/test/commit.** `dotnet build ./Pegasus.slnx --configuration
   Release`; a focused test filter for the new/updated test file; commit in
   small slices `type(scope): summary (PLAT-025)`; push. No PR opened by the
   driven agent — the orchestrator opens it.

## Disposition — the backend gap (dated 2026-08-29)

Per AGENTS.md rule 22 and EPIC-011 D19's preference order:

1. Fix in lane — not applicable: the missing controls require a new Core
   port and a migration, both explicitly out of scope for this wave-2 lane
   (migrations are serialized in wave 3; no package/new-project rule also
   applies to a new store).
2. Fix in another lane's file anyway (one-line) — not applicable, this is not
   a one-line change.
3. Reject / accept risk — not applicable, the contract is real and the gap is
   real; it should not be silently dropped.
4. **Defer to a new ticket — applied.** File a follow-up ticket (owner:
   `Pegasus.Core` / workflow-configuration area) to add an administrator-
   configurable instruction/image completeness policy and chase-interval
   setting, including the Core port, persistence, migration, and the
   operator decision on what the toggles/interval should actually control
   (none of that is specified beyond the two-line contract sentence — it
   needs its own research/plan, matching D19's "needs an operator decision"
   and "large enough to need its own plan" criteria for this last resort).

## Verification

- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0.
- Focused test filter for the new/updated Configuration web test class —
  real pass count, re-run independently by the orchestrator (not just
  codex's reported numbers).
- `git diff origin/dev...HEAD -- tests/` reviewed for any weakened/deleted
  assertion.
- Every changed file confirmed inside this ticket's owned-files list above;
  anything outside reverted and reported.

## Simplification pass

Recorded under a dated heading once the diff exists (post-implementation).

## Disposition record — 2026-08-29

- **Backend gap (Instruction completeness / Due work chase interval):**
  deferred to [[PLAT-062]] ("Add administrator-configurable instruction/image
  completeness and chase-interval settings"), linked both ways. This is the
  D19 last-resort case — the work needs a new Core port + migration (out of
  scope for this lane) and an operator decision the two-line contract
  sentence does not supply.
- **`_AdminNav.cshtml` omits Service health/Action Logs/Reports** (flagged by
  the driven agent as an out-of-scope observation): **rejected as a finding**
  — `_AdminNav.cshtml`'s own doc comment already states those three join
  "when their pages land (wave 4), never as dead links". This is documented,
  intentional wave-4 gating, not a defect. No ticket filed.

## Simplification pass — 2026-08-29

Ran over the branch's own diff (`git diff origin/dev...HEAD`), three files:
`Configuration.cshtml`, `OperatorLabels.cs` (append), `WorkflowConfigurationWebTests.cs`
(new).

- Reuse: confirmed every class used (`admin-layout`, `panel`, `panel-head`,
  `panel-body`, `stack`, `cluster`, `field`, `req`, `field-error`,
  `validation-summary`, `notice`/`notice--success`, `btn`/`btn--primary`,
  `panel-title-meta`) and both icons (`icon-check`, `icon-save`) already exist
  in `site.css` / `_LucideSprite.cshtml` — nothing invented, `site.css` not
  touched.
- Reuse: the non-administrator-denial test pattern
  (`useIntegrationTestAuthentication: true` + `X-Test-Roles` header) matches
  the existing convention used across the suite (e.g.
  `ApprovedOutlookCategoryAdministrationWebTests.cs`), not a new pattern.
- No dead code, no new abstraction, no speculative test found. The removed
  read-only `<dl>` "Current configuration" block was a redundant duplicate of
  the same two values the edit form's checkbox state already shows — dropping
  it is page-economy, not a functionality loss (`PolicyKey`/`PolicyVersion`
  are not in the design contract's drawn surface; `PolicyVersion` survives in
  the new meta line).
- No findings requiring a fix beyond what the driven agent already applied
  (the CA1875 build fix during its own build/test loop).

## Verification — run 2026-08-29 (orchestrator, independent of the driven agent)

- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0, 0
  warnings, 0 errors (after clearing one stale MSBuild node file-lock in this
  worktree; unrelated to the change).
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~WorkflowConfigurationWebTests"` — exit 0, 3 passed, 0
  failed, 0 skipped.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~AdministrationSearchAccountWebTests"` (pre-existing
  regression check, this page's route is asserted there) — exit 0, 6 passed,
  0 failed, 0 skipped.
- `git diff origin/dev...HEAD -- tests/` reviewed line by line: the test file
  is new, no existing assertion was weakened, deleted, or inverted.
- `git diff --stat origin/dev...HEAD`: exactly the three files this ticket
  owns — `Configuration.cshtml`, `OperatorLabels.cs` (append-only, no
  reordering), `WorkflowConfigurationWebTests.cs` (new). Nothing outside the
  owned-files list.
