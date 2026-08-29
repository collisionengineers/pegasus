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

## Review findings — dispositions (round 2), 2026-08-29

Remediated by Claude (the lane was built by Codex; the fix deliberately gets a
different reasoner). Merged `origin/dev` first — already up to date at
`b92cb9a7`, no conflicts.

### [medium] The page never set `ViewData["AdminAutomationComposed"]`

**FIXED in lane** (disposition 1). `Configuration.cshtml.cs` now carries
`AutomationComposed`, resolved in `LoadAsync` the same way
`Administration/Index.cshtml.cs:24-25` and both parallel admin lanes resolve it
(`HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not
null`), and `Configuration.cshtml` passes it to `_AdminNav`. Both the GET and
the re-render-after-POST path go through `LoadAsync`, so the rail is complete on
every rendered response.

Pinned by a new test, `ComposedAutomationIsListedInThisPageAdministrationRail`.
It composes the ingress with `AutomationMcpTestSupport.WithAutomationMcp`, first
asserts the sibling `/Administration` rail really does list "Automation &amp; AI"
in that host — so the test cannot pass by both rails being equally short — then
asserts this page's rail carries the identical link set. It **failed against the
pre-fix code** in this worktree (`Assert.Contains() Failure: Sub-string not
found` on the automation entry) before passing after it, so it pins the defect
rather than describing it.

### [medium] Scope reduction — one of three §1.12 control groups shipped

**REJECTED as in-lane work, with evidence; the deferral to [[PLAT-062]] stands**
(disposition 3, backed by the already-filed 4). The round instruction was to
build the remaining groups "unless you can show a real blocker". Three, checked
rather than argued:

**A. It needs a schema migration, and the wave plan serialises migrations.**
Both remaining groups are new persisted, administrator-editable settings on
`WorkflowConfigurations`
(`src/Pegasus.Infrastructure/Persistence/AdministrationPolicyModelConfiguration.cs:14-26`),
so both need an EF migration. `waves.md` wave 3 reads "backend (parallel by Core
folder; **one unmerged migration at a time**)" with a named order — AUTO-011 →
TICK-061/058 → ENG-026 → ENG-027 → CASE-028 → PLAT-048 → MAIL-027. PLAT-025 is
a wave-2 page lane and appears nowhere in it. Verified live, not assumed:
`git diff --name-only origin/dev...origin/task/eng-027-case-valuations` right now
lists `20260829095336_CaseValuations.cs`, its designer, **and**
`PegasusDbContextModelSnapshot.cs`. An unmerged migration is in flight this
minute, and every migration rewrites that one shared snapshot file — a second
concurrent one from a page lane is a guaranteed conflict on a file no page lane
owns, plus the rule-16 grants and `scripts/Test-MigrationGrants.ps1` that ride
with it.

**B. The chase interval is a records-integrity decision, not a setting.**
`CaseChaseSchedule` (`src/Pegasus.Core/Tasks/CaseWorkScheduling.cs:74-92`) is a
static, synchronous policy whose `PolicyIdentity` (`case-chase-schedule/v1`) is
stamped into permanent action history (`EfCaseDueChaserStore.cs:178`) and —
decisively — re-derived and compared on write:
`if (transition.NextChaseAtUtc != CaseChaseSchedule.NextChaseAt(transition.ScheduledAtUtc))`
(`EfCaseDueChaserStore.cs:253`). Make the interval editable and every chase
scheduled under the previous interval starts failing that comparison. Doing it
properly means versioning the chase policy identity, an operator decision on
what happens to already-scheduled chases when the interval changes, and
threading configuration through eleven synchronous call sites across five
Infrastructure stores plus `ImageIntakeChaseSchedule` and `RunDueChasers`. That
is wave-3 backend work on any reading.

**C. "Instruction completeness" changes a fail-closed product invariant.** From
the prototype's own final layer
(`Pegasus_UI_Assessment_Refined.html:1546`) the two checkboxes are "Instruction
document required" and "Eligible images required". Today neither is a setting:
`CaseLifecycle.ValidateReviewReadiness` / `ValidateReadiness`
(`src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:555, 573-576`) hard-require
`InstructionsComplete && ImagesComplete` with no configuration term, and
`CaseDataOperations.cs:80-86` does the same. Making them administrator-
disableable would let a case reach an Engineer with no instruction document,
which is a change to CLAUDE.md's "fail closed … when processing, limits, or
principal identity are incomplete" invariant — an operator/PRD decision, exactly
D19 #4's "needs an operator decision" trigger.

No inert placeholders were drawn for the missing groups. D21/D22 permit *drawing*
a disabled seam only for a named, ticketed **integration** (D7); unbuilt settings
are not that, and a disabled checkbox would read as policy while doing nothing.

Recommendation for the orchestrator, not actioned here (it would mutate another
ticket): slot [[PLAT-062]] into the wave-3 migration order rather than leaving it
free-floating, so the epic's finish line moves with the plan instead of the
ticket count.

### [low] The same heading text rendered twice (h1 and h2)

**FIXED in lane** (disposition 1). §1.12 puts the area label on the content
panel's h2, so the h2 stays "Workflow configuration" and the page heading becomes
the administration area itself — `ViewData["Title"] = OperatorLabels.Nav.Administration`,
matching `plat-026`'s remediated shape and the prototype's own document title,
which is `'Administration'` for every `/admin/<area>` route
(`Pegasus_UI_Assessment_Refined.html:1885`). The eyebrow, which would then have
duplicated the new h1, goes with it. Pinned by
`Assert.Contains("<h1>Administration</h1>", …)` plus a heading-count assertion
that exactly one h1/h2 reads "Workflow configuration".

Noted for honesty: the prototype's *page header* at line 1147 does stack the area
label on both, so this is a page-economy correction over the prototype rather
than a transcription fix. Both admin siblings avoid the stack; §1.15 does not
list it, so it is a judgement call, recorded here as one.

### [low] `Meta` hardcoded the setting count as a string literal

**FIXED in lane** (disposition 1). `Meta(int policyVersion)` is now
`$"Version {policyVersion}"`. The count is a fact the form itself expresses, so
the literal was a second copy of it in the one page PLAT-062 will extend. The
version survives because it is the operator-meaningful half and the identity the
action history records. The test assertion moved with it, and got tighter — from
a bare `Contains("2 settings")` substring to a regex pinning
`<div class="panel-title-meta">Version <n></div>`.

### [low] New page-scoped label constants duplicate words other admin pages inline

**ACCEPTED, no change** (disposition 3). `WorkflowConfiguration.Reason` /
`.Review` are the right direction of travel — EPIC-011 says labels live in
`OperatorLabels.cs` — and the finding itself concedes that. Consolidating the
cross-page `Reason` belongs to the lanes that own `Accounts/Edit.cshtml`,
`Accounts/Index.cshtml` and `MailCategories.cshtml`, none of which is this one;
touching them would be the "never absorb another ticket's scope" breach. Rule 8
is not violated today: one concept, one home per page that has one.

### [low] Bookkeeping — unticked checklist item, understated CI position

**FIXED** (disposition 1). The final checklist item is ticked: PR #622 exists,
is open against `dev`, is recorded in the ticket frontmatter, and its 11 checks
were already green (run 33247767486) when the report said it "needs CI green".

### Disclosed capability loss — `PolicyKey` no longer rendered

**ACCEPTED, no change** (disposition 3). It is not in §1.12's drawn surface
(h2 area label, description, meta), it is a technical slug (`case-workflow`)
rather than operator language, and the version — the half that identifies which
configuration a decision was taken under — survives in the meta line.

### Verifier's observation — the negative assertions pin the reduced scope

**ACCEPTED, kept as they are** (disposition 3). `DoesNotContain("Instruction
document required" / "Eligible images required" / "Chase interval")` are true
statements about the page as shipped, and they are the guard that stops anyone
drawing those three as inert controls. Loosening them now would be a weakening
with no failing test to justify it. When PLAT-062 lands the controls it changes
the behaviour, so it updates the assertions to the new correct behaviour — that
is ordinary test maintenance, not tampering.

## Verification — run 2026-08-29 (round 2, Claude, in the lane worktree)

Windows + PowerShell 7, one platform, `pwsh -NoProfile`.

- `dotnet build ./Pegasus.slnx --configuration Release` — **exit 0**, "Build
  succeeded. 0 Warning(s) 0 Error(s)".
- `dotnet test … --filter "FullyQualifiedName~WorkflowConfigurationWebTests"` —
  **exit 0, Failed: 0, Passed: 4, Skipped: 0, Total: 4** (was 3; the fourth is
  the new rail-parity test).
- Intermediate run, recorded because a failure is never erased by a later pass:
  the first run of that filter after adding the rail test but **before** the rail
  fix was compiled in was **exit 1, Failed: 1, Passed: 3** — the evidence that the
  new test actually pins the defect.
- `dotnet test … --filter "FullyQualifiedName~AdministrationSearchAccountWebTests"`
  — **exit 0, Failed: 0, Passed: 6, Skipped: 0, Total: 6** (pre-existing
  regression check on this route).
- `dotnet test … --filter "FullyQualifiedName~TestUiSnapshotTests"` — **exit 0,
  Passed: 1** (its `administration-configuration--default` state matcher is
  `"Workflow configuration"`, still satisfied by the panel h2).
- `git diff --numstat origin/dev...HEAD -- tests/` — `211 0` on one new file:
  **zero deleted or modified lines anywhere under `tests/`**.
- `git diff --numstat origin/dev...HEAD -- src/Pegasus.Web/Presentation/OperatorLabels.cs`
  — `13 0`: still append-only, still one nested class, nothing reordered.
- `git diff --stat origin/dev...HEAD` — four files, all lane I2's:
  `Configuration.cshtml`, `Configuration.cshtml.cs`, `OperatorLabels.cs`
  (append-only), `WorkflowConfigurationWebTests.cs` (new). No package added, no
  top-level directory, no migration, `dev`/`main` untouched, PR not merged.

Not touched, deliberately: `docs/design/test-ui/pages/administration-configuration--default.html`
is now further stale. `waves.md` regenerates snapshots "once per merge on the
merging branch only" and this lane was told not to run the capture script;
merged sibling PLAT-023 (6bf5f789) set the same precedent.
