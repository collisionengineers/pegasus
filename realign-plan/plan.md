# PR23-Owned Pegasus Consolidation and Remediation Runbook

## 1. Objective and fixed decisions

Execute one long-running consolidation on `pegasus-realign` and leave it locally verified and push-ready. If the exact GitHub-write approval in R004 is later supplied, also leave PR23 green at the pushed exact head and ready for human review.

The following decisions are final. The executing agent must not revisit them:

1. PR23 and branch `pegasus-realign` own the consolidated delivery.
2. PR24, every named dirty worktree, the report-renderer branch, and the AI Centre RAG branch are folded into PR23.
3. PR23 owns product requirements, operator truth, documentation structure, design authority, capability allocation, open decisions, and accepted ADR precedence.
4. PR24 supplies implementation evidence and code, not authority to rewrite PR23 product behavior.
5. PR24's `ADR-0014-qdos-alpha-implementation-contract.md` is rejected and removed. It is not renamed, retained as active history, or allowed to override PR23 ADR-0013.
6. The standalone email evaluator becomes ADR-0014 under `docs/adr/` and remains an independent local Windows tool outside the application solution and deployment.
7. PR24's Core/domain implementation is the initial application-policy baseline. Main-worktree UI work is adapted to that single Core owner; duplicate Core policies, stores, and migrations from the main worktree are not retained as parallel implementations.
8. PR23's visual authority remains controlling. Incoming UI code must be adapted to `design/`, not the reverse.
9. Renderer reference material is retained as raw evidence without altering supplied files.
10. Collision Brain remains an independently buildable, non-caller source workspace. It is never added to `Pegasus.slnx`, application references, application deployment, or application CI.
11. Collision Brain `to-ingest/`, generated extraction material, page images/text, source corpus, caches, model output, and imported nested agent instructions are excluded from PR23.
12. Existing migrations on `main` are immutable. Unmerged PR24/main-worktree migrations may be replaced by one coherent final PR-local migration sequence before PR23 is delivered.
13. Source branches are preserved after transfer. The sole exception is clean, redundant local branch `pr-20` and worktree `C:\Users\Alex\.omp\wt\20-423136b`, which are removed after the checks below pass.
14. No source branch is pushed. Only `pegasus-realign` may be pushed, and pushing or editing PR23 requires explicit approval for that exact GitHub write.
15. No PR is merged to `main`. `MERGE AUTH GRANTED` has not been supplied.
16. No Azure, Outlook, Box, DVLA/DVSA, credential, deployment, account, or destructive external operation is performed.
17. No stash, reset, clean, force operation, rebase, force-push, or merge abort is permitted.
18. The work completes only when the final exact head passes every applicable local check and an independent requirements review. Exact-head PR checks are additionally required when R004 push authority is supplied.

## 2. Evidence vocabulary

Use only these states in commits, change records, PR text, and the final report:

- **Planned:** required by an accepted authority but not implemented.
- **Implemented:** connected source exists and compiles.
- **Caller-proved:** the genuine entry point exercised the intended boundary.
- **Locally verified:** the named local checks passed for the stated matrix.
- **Deployed:** the named artifact was deployed to the named target.
- **Live verified:** authorised evidence exercised enumerated live states.
- **Accepted:** the authorised owner accepted the named result.
- **Absent:** no implementation exists in the final tree.
- **Deferred:** intentionally unbuilt and awaiting its stated activation evidence.

Never use registration, a file, a mock, a structural test, or a deployment as caller proof.

## 3. Worktree and branch identities

Use these exact source identities:

| Purpose | Worktree | Branch | Starting head |
| --- | --- | --- | --- |
| Dirty staff/case/Triage/UI work | `C:\Users\Alex\Documents\GitHub\pegasus` | create `fold/20260730-main-staff-case-workspace` | `429c9704b26e8b4bc7f288c226fff8f993406c85` |
| Redundant PR20 worktree | `C:\Users\Alex\.omp\wt\20-423136b` | `pr-20` | `f77e1492b25abdd5a14725f4c15129333482b743` |
| PR24 plus dirty continuation | `C:\Users\Alex\.omp\wt\qdos-alpha-423136b` | `workflow/20260729-deliver-qdos-alpha` | `2d6f4a7c227ba5e5168ba4297af3dca2f34c36d0` |
| Collision Brain | `C:\Users\Alex\Documents\GitHub\pegasus-ai-centre-rag-pipeline` | `pegasus-ai-centre-rag-pipeline` | `e402fca91ec9b98fdcab0d0115d7cbaa18ab175f` |
| Standalone evaluator | `C:\Users\Alex\Documents\GitHub\pegasus-email-eval` | `pegasus-email-eval` | `429c9704b26e8b4bc7f288c226fff8f993406c85` |
| Consolidation owner | `C:\Users\Alex\Documents\GitHub\pegasus-realign` | `pegasus-realign` | `1ca5f69118c6c65bc3cde39bc3fb06a50c4c3e2c` |
| Renderer reference work | `C:\Users\Alex\Documents\GitHub\pegasus-report-renderer` | `pegasus-report-renderer` | `29e80c2ce059fd9a45701a000710f968fda24e5a` |

Before executing any later phase, re-read the repository root `AGENTS.md`, `docs/index.md`, `docs/engineering.md`, the nearest nested `AGENTS.md`, and the selected merge-conflict skill. A later instruction may tighten safety but may not silently change the fixed product decisions above.

## 4. Global stop conditions

Stop the affected step without undoing completed work when any of the following occurs:

1. A worktree head, branch, or dirty-path count differs from the expected source and the difference cannot be explained by the work already completed in this runbook.
2. A credential, bearer token, private key, connection string, operational case material, mailbox data, Box data, or ignored `corpus/` content appears in a staged diff.
3. A protected package under `workspaces/ai-centre/skills/` would be modified.
4. A new top-level directory, application project, runtime, store, migration stream, or deployment unit would be required without an accepted ADR.
5. PR23 authority contradicts `docs/operator-notes.md` on material business meaning.
6. A third implementation of a business rule, classifier, allocator, parser, workflow transition, or external effect is found.
7. A required external target or credential is missing. Record the capability as absent or deferred; do not create a placeholder target or fallback.
8. A destructive or external write is required without exact approval.

For an authority contradiction, record one `DOC-CON-NNN` in the existing canonical conflict route and request direct user resolution. Do not invent the answer.

## 5. Phase A — establish the immutable baseline

### A001 — prove the consolidation worktree is untouched

From `C:\Users\Alex\Documents\GitHub\pegasus-realign`:

```powershell
git status --short --branch
git rev-parse HEAD
git branch --show-current
```

Required result:

- branch is `pegasus-realign`;
- head is `1ca5f69118c6c65bc3cde39bc3fb06a50c4c3e2c` unless this runbook has already created later commits;
- the only pre-existing untracked path is `realign-plan/`;
- there are no staged or tracked modifications.

Do not stage `realign-plan/` as part of a source merge. Commit it only as an intentional PR23 planning/runbook change.

### A002 — capture current source facts in the terminal output

Run, without writing a generated ledger:

```powershell
git worktree list --porcelain
git branch --all --verbose --no-abbrev
git log --oneline --decorate --graph --all -n 100
```

Use the output for the current session only. Do not add a status JSON, reconciliation CSV, or second workflow database.

### A003 — verify each worktree status independently

Run `git status --short --branch`, `git diff --stat`, and `git diff --cached --stat` separately in every worktree in section 3.

Required result before preservation:

- `pegasus-realign`, `pegasus-report-renderer`, and `pr-20` are clean;
- the main, QDOS, evaluator, and Collision Brain worktrees contain only their already identified scopes;
- no worktree has staged changes.

### A004 — prove the pre-transfer heads

Run `git rev-parse HEAD` in each worktree and compare the result with section 3. Keep this evidence in the current terminal/session output until the source preservation commits exist. The merge ancestry and the final QDOS change-record update in O006 become the durable evidence.

Do not mutate PR23 during this step and do not create another change record or reconciliation ledger.

## 6. Phase B — preserve dirty source work

No preservation commit is pushed. Each is a local, recoverable transfer input for PR23.

### B001 — create the main-worktree transfer branch

From `C:\Users\Alex\Documents\GitHub\pegasus`:

```powershell
git switch -c fold/20260730-main-staff-case-workspace
git branch --show-current
```

Required result: the branch is `fold/20260730-main-staff-case-workspace` and all dirty files remain present.

### B002 — stage only the main-worktree product scope

Stage only:

```powershell
git add -- design src tests
```

Explicitly leave unstaged:

- `.agents/`, because PR23 already owns installed agent skills;
- `temp_acceptance_store.cs`, because it is a temporary root artifact;
- every path outside `design/`, `src/`, and `tests/`.

Run:

```powershell
git diff --cached --check
git diff --cached --name-status
git status --short
```

Required result: staged changes contain only staff access, action history, Cases, Triage, UI shell, persistence, and their tests.

### B003 — scan and commit the main-worktree transfer

Search the staged diff and staged path names for secrets, connection strings, tokens, private data, `bin/`, `obj/`, `.vs/`, and generated artifacts. Resolve every hit before committing.

Commit with:

```powershell
git commit -m "feat: preserve staff case and triage workspace for consolidation"
```

After the commit, leave `.agents/` and `temp_acceptance_store.cs` untouched and uncommitted in this source worktree.

### B004 — stage the QDOS dirty continuation

From `C:\Users\Alex\.omp\wt\qdos-alpha-423136b`, confirm the branch is `workflow/20260729-deliver-qdos-alpha`.

Stage only these roots:

```powershell
git add -- .azure Pegasus.slnx azure.yaml design docs infra scripts src tests
```

Do not stage ignored files, local settings, secrets, corpus content, build output, release artifacts, or files outside those roots.

### B005 — validate and commit the QDOS transfer

Run:

```powershell
git diff --cached --check
git diff --cached --name-status
git status --short
```

Scan the entire staged diff for credentials, operational data, destructive commands, live targets, and protected package paths. Remove any such material from the index without deleting the source worktree copy.

Commit with:

```powershell
git commit -m "feat: preserve qdos alpha continuation for PR23 consolidation"
```

This commit is source-transfer evidence only. Do not describe it as verified, accepted, deployed, or production-ready.

### B006 — stage the evaluator transfer

From `C:\Users\Alex\Documents\GitHub\pegasus-email-eval`, confirm branch `pegasus-email-eval` and stage exactly:

```powershell
git add -- .gitignore
git add -- docs/changes/README.md
git add -- docs/changes/2026-07-29-minimal-desktop-email-evaluator.md
git add -- docs/decisions/README.md
git add -- docs/decisions/ADR-0010-standalone-desktop-email-evaluator.md
git add -- scripts/email-eval-desktop
git add -- src/Pegasus.Infrastructure/Intake/LocalEmailDisplayReader.cs
git add -- src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs
git add -- src/Pegasus.Web/Pages/Intake/EmailEvaluation.cshtml
git add -- src/Pegasus.Web/Pages/Intake/EmailEvaluation.cshtml.cs
git add -- src/Pegasus.Web/Pages/Intake/Upload.cshtml
git add -- tests/Pegasus.IntegrationTests/EmailEvaluationWebTests.cs
git add -- tests/Pegasus.IntegrationTests/LocalIntakeAccessTests.cs
```

The deleted Web files and tests must remain staged as deletions.

### B007 — validate and commit the evaluator transfer

Run staged diff checks and secret/data scans, then commit:

```powershell
git diff --cached --check
git commit -m "feat: add standalone local email evaluator"
```

Do not renumber or reroute the ADR in the source branch. That transformation occurs during PR23 integration.

### B008 — commit the two Collision Brain documentation edits

From `C:\Users\Alex\Documents\GitHub\pegasus-ai-centre-rag-pipeline`, stage exactly:

```powershell
git add -- workspaces/ai-centre/services/collision-brain/README.md
git add -- workspaces/ai-centre/services/collision-brain/docs/provider-evaluation.md
git diff --cached --check
git commit -m "docs: complete collision brain provider evaluation guidance"
```

Do not stage any other Collision Brain file in this preservation commit.

### B009 — confirm the renderer branch needs no preservation commit

From `C:\Users\Alex\Documents\GitHub\pegasus-report-renderer`:

```powershell
git status --short --branch
git rev-parse HEAD
```

Required result: clean branch `pegasus-report-renderer` at `29e80c2ce059fd9a45701a000710f968fda24e5a`.

## 7. Phase C — remove the redundant PR20 worktree and branch

### C001 — repeat the safety proof

From the PR20 worktree:

```powershell
git status --porcelain
git rev-parse HEAD
git branch --show-current
```

Required result:

- status output is empty;
- branch is `pr-20`;
- head is `f77e1492b25abdd5a14725f4c15129333482b743`.

From the PR23 worktree:

```powershell
git merge-base --is-ancestor pr-20 main
```

Required result: exit code `0`.

### C002 — remove only the exact PR20 worktree

From the PR23 worktree:

```powershell
git worktree remove "C:\Users\Alex\.omp\wt\20-423136b"
git worktree list --porcelain
```

Required result: the exact path is absent and every other worktree remains registered.

### C003 — delete only the redundant local branch

```powershell
git branch -d pr-20
git branch --list pr-20
```

Required result: the second command returns no branch. Never use `-D`.

## 8. Phase D — commit this runbook on PR23

From `C:\Users\Alex\Documents\GitHub\pegasus-realign`:

```powershell
git add -- realign-plan/plan.md
git diff --cached --check
git commit -m "docs: atomize PR23 consolidation runbook"
```

Do not stage any other path in this commit.

## 9. Phase E — merge PR24 and its continuation into PR23

### E001 — start the PR24 merge

From the clean PR23 worktree:

```powershell
git merge --no-ff --no-commit workflow/20260729-deliver-qdos-alpha
```

Do not abort the merge. Resolve forward until it can be committed.

### E002 — restore PR23 authority before resolving implementation

Restore the pre-merge PR23 versions of these exact files:

```powershell
$Pr23AuthorityPaths = @(
  '.azure/deployment-plan.md',
  'design/README.md',
  'design/product/requirements.md',
  'docs/architecture.md',
  'docs/changes/2026-07-27-qdos-alpha-reference-corpora.md',
  'docs/history/plans/remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md',
  'docs/history/plans/remainder-delivery/integrations/staff-mcp.md',
  'docs/open-decisions.md',
  'docs/operations.md',
  'docs/requirements.md',
  'temp-plan/qdos-full-alpha-delivery-plan.md'
)
git restore --source=HEAD --staged --worktree -- $Pr23AuthorityPaths
```

These files are updated later from the final implementation, never from PR24's stale claims.

### E003 — remove the rejected decision structure

Remove the incoming old decision tree and rejected ADR:

```powershell
git rm -r --ignore-unmatch -- docs/decisions
git rm --ignore-unmatch -- docs/adr/ADR-0014-qdos-alpha-implementation-contract.md
```

Required final state:

- `docs/decisions/` does not exist;
- PR23 `docs/adr/0013-qdos-alpha-implementation-contract.md` remains unchanged;
- no active document links to PR24 ADR-0014.

### E004 — resolve all remaining PR24 conflicts using fixed ownership

Apply these rules without exception:

1. `Pegasus.Core` owns all business rules and ports.
2. Infrastructure adapts Core; it does not duplicate decisions.
3. Web and Worker retain only composition and caller translation.
4. Existing main-branch migrations are kept byte-for-byte.
5. PR24-local migration files may remain temporarily but are not final until phase N.
6. Missing external configuration fails closed; no local-success fallback is introduced.
7. Existing evaluator routes are not treated as QDOS product callers.

List unresolved paths:

```powershell
git diff --name-only --diff-filter=U
```

Resolve every listed file, then require the command to return no paths.

### E005 — inspect clean merges for semantic conflicts

Search the merged tree for these known contradictions:

```powershell
rg -n "LastAllocatedSequence >= 999|In today|StaffMcpActorResolver|docs/decisions|ADR-0014-qdos-alpha" src tests docs design
rg -n -i "ImageIntake|image intake|vehicle history|market valuation|Automation Actor" src tests
```

Do not fix the product gaps in the merge commit. Record their exact paths for phases K through M. The merge commit must preserve source faithfully while rejecting stale authority.

### E006 — commit the completed PR24 merge

Run:

```powershell
git add -A
git diff --cached --check
git diff --cached --name-status
git diff --name-only --diff-filter=U
```

Required result: no unmerged paths, no whitespace errors, no forbidden material, and no old decision tree.

Commit:

```powershell
git commit -m "merge: fold PR24 QDOS alpha implementation into PR23"
```

## 10. Phase F — merge the main staff/case/Triage/UI work

### F001 — start the merge

```powershell
git merge --no-ff --no-commit fold/20260730-main-staff-case-workspace
```

### F002 — fix subsystem ownership before resolving hunks

Use these exact resolution decisions:

- `design/README.md`: keep PR23.
- Overlapping `Pegasus.Core` files: keep the PR24-derived implementation. Port missing main-worktree behavior later into that owner; do not retain duplicate Core classes.
- Overlapping persistence entities, `PegasusDbContext`, model configuration, snapshot, and migration files: keep the PR24-derived side temporarily. Final schema work occurs in phase N.
- Unique main-worktree Core capabilities under Access, Action History, Cases, and Triage: retain only when no PR24 Core owner already exists.
- When a unique main-worktree type duplicates a PR24 concept under another name, delete the incoming duplicate and port its missing test scenario to the PR24 owner.
- Razor Pages, shared partials, images, and CSS: retain the main-worktree presentation as the implementation candidate, then conform it to PR23 design authority in phase M.
- `Program.cs` and dependency registration: keep the PR24-derived composition, then manually add only registrations needed by retained main-worktree callers.
- Overlapping tests: keep the PR24-derived test file and port every unique main-worktree scenario into it. Never keep two tests that merely assert two conflicting implementations.

### F003 — prevent a second migration stream

Remove the incoming `20260730101145_StaffTriageCaseWorkspaceV1.cs` and `20260730101145_StaffTriageCaseWorkspaceV1.Designer.cs`. Restore the pre-merge PR23 version of `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`. Retain the schema intent in entities, configuration, and tests, then generate the final PR-local migration in phase L.

### F004 — complete and commit the merge

Require no unmerged paths, run staged checks and sensitive-path scans, then commit:

```powershell
git commit -m "merge: fold staff case and triage workspace into PR23"
```

## 11. Phase G — merge and reroute the standalone evaluator

### G001 — start the evaluator merge

```powershell
git merge --no-ff --no-commit pegasus-email-eval
```

### G002 — create the final evaluator ADR identity

Move the incoming decision to exactly:

```text
docs/adr/0014-standalone-local-desktop-email-evaluator.md
```

Change its title to `ADR-0014 — Standalone local desktop email evaluator` and retain its substantive decision body.

Delete the incoming `docs/decisions/` directory and update:

- `docs/adr/README.md`;
- `docs/changes/2026-07-29-minimal-desktop-email-evaluator.md`;
- `docs/changes/README.md`;
- all first-party links to this decision.

### G003 — enforce the evaluator boundary

Required final state:

- `scripts/email-eval-desktop/` exists and is independently buildable;
- it is absent from `Pegasus.slnx`;
- no application project references it;
- no deployment manifest includes it;
- the old Razor email-evaluation page and page model are deleted;
- the page-only integration test is deleted or replaced only by a test proving the route is absent;
- `.gitignore` excludes every `emailevallocal` output tree;
- the source `.eml` is copied, never moved or modified;
- Outlook, Box, Azure, databases, model providers, and production stores are never called.

### G004 — commit the evaluator merge

After link, path, staged-diff, and secret checks:

```powershell
git commit -m "merge: fold standalone email evaluator into PR23"
```

## 12. Phase H — merge renderer reference evidence

### H001 — start the renderer merge

```powershell
git merge --no-ff --no-commit pegasus-report-renderer
```

### H002 — retain only renderer-owned additions

Keep:

- `docs/reference/rendererref1/` supplied files exactly as committed;
- provenance needed to explain the supplied reference set.

The renderer branch contains no new renderer workspace implementation to transfer. Restore PR23 versions of every branch-touched path outside `docs/reference/rendererref1/`, then make only the reference-manifest and current workspace-README edits required by H003.

Do not edit, normalize, render over, or rename supplied PDFs, JSON, images, or the design specification.

### H003 — route renderer evidence

Add the retained reference set to `docs/reference/README.md` with source/evidence status. Update `workspaces/report-renderer/README.md` only with current build/runtime facts verified against the workspace.

Do not promote renderer samples into product requirements and do not add the renderer to application deployment.

### H004 — commit the renderer merge

```powershell
git commit -m "merge: fold report renderer reference evidence into PR23"
```

## 13. Phase I — merge the Collision Brain workspace

### I001 — start the RAG merge

```powershell
git merge --no-ff --no-commit pegasus-ai-centre-rag-pipeline
```

### I002 — exclude prohibited material

Remove from the merge result every incoming path under:

```text
workspaces/ai-centre/services/collision-brain/to-ingest/
workspaces/ai-centre/services/collision-brain/corpus/
workspaces/ai-centre/services/collision-brain/logs/
```

Also remove:

- generated extraction pages and intermediate model comparisons wherever located;
- model weights, caches, local environment files, and private datasets;
- `workspaces/ai-centre/services/collision-brain/AGENTS.md`, because upstream nested agent instructions are not imported into Pegasus;
- nested `.git` metadata;
- generated package or build output.

Do not open or inspect excluded operational/corpus images to decide whether to retain them. Their path and role are sufficient.

### I003 — retain the independently buildable workspace

Keep only:

- solution/project files;
- source code;
- unit/contract tests;
- lock files and secret-free example configuration;
- Docker/Compose development files that belong solely to the workspace;
- current workspace README, architecture, operations, accepted workspace ADR, and provider-evaluation contract;
- `.github/workflows/ai-centre.yml`, with path filters limited to the Collision Brain workspace and a single workspace job running its locked restore, Release build, and Release tests from `workspaces/ai-centre/services/collision-brain`.

Restore PR23's `.github/workflows/ci.yml`; Collision Brain must not be added to application CI.

### I004 — enforce non-caller boundaries

Verify by search that:

- `Pegasus.slnx` does not mention Collision Brain;
- no application `.csproj` references it;
- no application source dynamically loads or calls it;
- application deployment files omit it;
- AI Centre owns experimentation only and no Collision Brain code owns case policy or human approval.

### I005 — update workspace provenance

Update `workspaces/README.md` and `workspaces/ai-centre/README.md` with the exact retained commit identity, content manifest, independent build commands, non-caller status, and exclusions.

### I006 — commit the RAG merge

```powershell
git commit -m "merge: fold collision brain source workspace into PR23"
```

## 14. Phase J — build the authoritative gap inventory

Use the existing QDOS change record as the only delivery/evidence register. Do not add another matrix file.

### J001 — enumerate the 128 alpha capabilities

Read every `0.1.0-alpha.1` row in `docs/capabilities.md` and update the existing capability-evidence section in `docs/changes/2026-07-27-qdos-alpha-reference-corpora.md`.

For each row record exactly one current state from section 2 and link the actual Core owner, genuine caller test, or explicit absence/deferment. Allocation alone remains **Planned**.

### J002 — apply ADR-0013 clause by clause

Record remediation entries for all fourteen ADR-0013 decisions:

1. Image Intake remains pre-Case.
2. Vehicle checks are global progression gates.
3. Instruction, image, and staff-review gates are mandatory.
4. Cancellation remains manual.
5. Box recovery is staff initiated.
6. Dashboard wording and semantics are `New cases today`.
7. Case sequences expand through `9999`.
8. EVA exports all eligible images in deterministic order.
9. AI-05 remains deferred to `1.0.0`.
10. MCP uses one non-human Automation Actor.
11. The future action is `Send to AI`.
12. Login protection uses transient throttling, not persistent lockout.
13. The local evaluator remains separate.
14. Exact Azure targets remain unresolved and unexecuted.

### J003 — classify the confirmed PR24 drift

Record these current findings without softening them:

- no Image Intake identity or association model was found;
- both allocators exhaust at `999` rather than `9999`;
- dashboard copy says `In today`;
- MCP resolves staff actors instead of one Automation Actor;
- configurable readiness permits mandatory gates to be removed;
- global vehicle identity/history/valuation progression gates are absent;
- PR24 ADR-0014 and `docs/decisions/` contradict PR23 authority;
- PR24's `qdos-pressure` check is failing;
- external/live alpha requirements remain unimplemented or unverified.

### J004 — identify built-but-uncalled and duplicate surfaces

Search source, registration, tests, and real entry points for every Web page, Worker function, MCP tool, adapter, store, classifier, allocator, and workflow owner added by the folded work.

For each surface:

- retain it only when a genuine caller is implemented in this delivery;
- otherwise delete it if it is dangerous, duplicate, or an unapproved dormant capability;
- keep it absent and record the deferred seam when activation evidence is missing.

Do not retain code merely because it compiles or is registered.

## 15. Phase K — remediate Core policy and contracts

Implement tests first for each numbered item, then the minimum Core behavior needed to pass them.

### K001 — implement Image Intake identity

Add one Core-owned pre-Case aggregate with:

- immutable Image Intake ID and human-visible Image Intake Reference;
- retained source occurrence and normalized VRM when available;
- `Needs sorting` outcome when no usable normalized VRM exists;
- awaiting-instruction state when a usable VRM exists;
- no Principal, Case/PO, or case-sequence allocation;
- append-only state and action history.

### K002 — implement Image Intake association

Allow association only to exactly one eligible instructed Case before report delivery.

Automatic association requires:

- exact normalized VRM match;
- no contradictory accepted identity evidence;
- exactly one eligible Case.

Otherwise require an authorised staff actor, reason, evidence, and timestamp. Preserve both identities and source histories.

### K003 — implement reversible Image Intake correction

Before report delivery, authorised staff may unlink or reassociate with a reason. The Image Intake returns to awaiting instruction, the instructed Case recomputes readiness, and no identity, source fact, or relationship event is deleted or reused.

After report delivery, reject association reversal.

### K004 — correct case-allocation gates

Allocate a Case/PO once all identity-critical gates pass:

- safe source persistence and processing receipt;
- unambiguous Principal;
- unambiguous Case type;
- applicable route identity/policy;
- processing and size/format limits;
- standalone Audit evidence when applicable;
- no unresolved wrong-Principal, duplicate-occurrence, receipt-integrity, or custody ambiguity.

Do not require ordinary missing business details, images, or external vehicle checks before allocation. Allocate once, then retain the Case as `Not ready` until progression gates pass.

### K005 — make readiness gates mandatory

Model these as three independent Core decisions with actor, time, evidence, policy/version, and current status:

1. instruction completeness;
2. image completeness;
3. staff review.

Provider policy may define accepted evidence for each gate but may not disable, merge, or imply one gate from another.

### K006 — add global vehicle progression gates

Every instructed Case must retain separate results for:

1. vehicle identity/specification;
2. vehicle-history/risk;
3. market valuation.

Each result records source identity, provider, version, time, status, accepted data, and provenance. A missing/inapplicable result passes only through an authorised staff exception with a named reason and permanent history.

Block staff review acceptance and Engineers-queue eligibility until all three results or exceptions exist.

### K007 — preserve field provenance

For each current Case datum, retain whether it came from staff entry, deterministic extraction, AI prefill/proposal, provider API, or another external vehicle/estimate source. Keep provenance separate from confirmation status. Derived values identify accepted inputs and calculation version.

### K008 — correct reference allocation

Change the normal allocator and linked-replacement allocator to:

- start at `001`;
- format `1` through `999` with a three-digit minimum;
- continue as `1000` through `9999` without truncation;
- fail closed before allocating `10000`;
- never wrap, reuse a value, or fabricate a year.

Use one shared Core constant/policy for the maximum; do not duplicate `9999` across allocators.

### K009 — keep cancellation manual

Mailbox classification may propose or associate a cancellation message but may not mutate Case state. Only an authorised staff Core command may choose hold, confirm `Provider cancelled`, release after recategorisation, unlink, or reassociate. Record all classification, association, correction, actor, time, reason, and evidence.

### K010 — make Box recovery staff initiated

After immutable Case/PO allocation, a Box custody failure:

- retains the Case and reference;
- leaves it `Not ready`;
- records the target, attempt, terminal/transient/unknown classification, and exact outcome;
- exposes one idempotent authorised-staff retry command;
- never schedules an automatic business retry;
- never rolls back or reallocates the Case reference.

Transport-level transient retry may protect one in-flight technical operation only; it may not become an unattended business retry loop.

### K011 — correct EVA handoff policy

Generate one deterministic bundle containing every eligible custody-confirmed Case-vehicle image. Exclude staff-confirmed third-party vehicle evidence. A recognizer suggestion alone cannot exclude an image. Expose no alpha image-selection or ordering control. EVA continues to own named-Engineer assignment.

### K012 — implement one Automation Actor boundary

Replace staff MCP actor resolution with one named, non-human, vendor-neutral Automation Actor.

The boundary must have:

- separate client identity and authentication;
- explicit resource/audience and scopes;
- Core authorization per tool/action;
- rate limits;
- permanent actor/client/action attribution;
- revocation without impersonating or borrowing staff identity.

Ordinary staff receive no MCP credentials or access. MCP tools call the same Core use cases as Web/Worker and own no business policy.

### K013 — preserve deferred capability boundaries

Do not implement or register alpha callers for:

- AI-05;
- `Send to AI`;
- AI query proposals;
- EVA replacement;
- provider API;
- broad classified-email workspace or email MCP;
- AI Centre model calls;
- unresolved live adapters or Azure targets.

Delete any folded dormant implementation of these capabilities unless an already accepted, caller-backed contract independently requires it.

## 16. Phase L — persistence, adapters, and migrations

### L001 — consolidate persistence ownership

Use the existing Pegasus database and `PegasusDbContext`. Add no second store or migration stream.

Persist:

- Image Intake identity/state/history;
- Image Intake-to-Case relationship events;
- three independent readiness decisions;
- three vehicle gate results/exceptions;
- field provenance;
- Automation Actor/client attribution;
- staff-initiated Box retry state.

Apply optimistic concurrency and append-only history consistently with existing Case policy.

### L002 — remove duplicate persistence implementations

Compare folded PR24 and main-worktree entities/stores semantically. Keep one implementation per concept. Delete replaced entities, DbSets, configuration, DI registrations, migrations, and duplicate tests in the same slice.

### L003 — rebuild only the PR-local migration sequence

Identify migrations present on `main` and retain them byte-for-byte. Remove only unmerged migrations introduced by PR24 or the main transfer branch when they no longer match the final model.

Generate a coherent PR-local migration sequence from the final model. Update the snapshot from that sequence. Do not edit an already-main migration to make tests pass.

### L004 — validate database invariants

Add integration coverage for:

- transactionally allocating Case/reference and enqueueing custody;
- no sequence consumption for Image Intake;
- rollback before identity allocation on identity-critical failure;
- no rollback/reuse after Box failure;
- concurrent allocation at `999`, `1000`, `9999`, and exhaustion;
- linked principal replacement continuing the correct lineage;
- relationship reversal retaining history;
- mandatory readiness and vehicle gates surviving restart;
- idempotent staff Box retry;
- stale concurrency rejection.

## 17. Phase M — real callers and operator surfaces

### M001 — Web intake and Image Intake callers

Use authenticated Razor Pages to expose the existing intake queue and detail route. Add Image Intake state, missing/contradictory predicates, eligible association candidates, and authorised associate/unlink/reassociate actions. PageModels translate requests into Core commands; they contain no policy.

### M002 — Case readiness surface

Display the three readiness gates and three vehicle gates independently with status, provenance, actor, time, reason, and exception evidence. Do not present extraction or provenance as staff confirmation.

### M003 — dashboard semantics

Change the exact label to `New cases today`. Query Europe/London midnight through current time. Include instructed Cases created in the interval even if later closed. Exclude Image Intakes, Triage, `Needs sorting`, and `Blocked intake`. Keep it distinct from `Due today`.

### M004 — manual cancellation surface

Show associated cancellation evidence and authorised actions to hold, confirm cancellation, release after correction, unlink, or reassociate. Do not expose an automatic-confirm action.

### M005 — Box failure and retry surface

Show failed target/outcome and one staff-initiated retry action with stale/idempotency protection. Do not add a scheduled retry control or hidden background loop.

### M006 — EVA surface

Expose deterministic bundle generation and revision history only. Remove image-selection/order controls and any claim that Pegasus assigned a named Engineer.

### M007 — MCP caller

Exercise the real HTTP MCP transport under the Automation Actor identity. Remove staff MCP login/impersonation routes and ordinary-staff MCP UI or credentials. Verify success, scope denial, Core denial, validation failure, rate limiting, revocation, and permanent history.

### M008 — Worker callers

Exercise the actual local Functions host for mailbox receipt, persisted outbox dispatch, intake work, due-work sweep, sent-evidence polling, and visible failure handling. Queue messages carry stable identities, not source bytes. Worker handlers call Core and acknowledge only after a durable outcome.

### M009 — conform retained UI to design authority

Map every retained main-worktree page and component to `design/README.md`, `design/product/requirements.md`, `design/product/ui-spec.md`, and the traceability matrix. Remove controls for deferred or prohibited capabilities. Verify desktop layout, keyboard behavior, accessible names, error summary, stale state, freshness, provenance, and reason dialogs.

## 18. Phase N — remove unintended or stale directions

### N001 — remove old documentation topology

Require all of the following:

- no `docs/decisions/` directory;
- no first-party links to `docs/decisions/`;
- no reference to rejected PR24 ADR-0014;
- every current ADR routes through `docs/adr/README.md`;
- history remains source-labelled and subordinate.

### N002 — remove staff MCP

Search for `StaffMcpActor`, `StaffMcpActorResolver`, staff-token MCP authorization, staff MCP UI, and tests asserting staff impersonation. Remove or rewrite every active occurrence to the Automation Actor boundary. Historical evidence may retain source-labelled wording.

### N003 — remove obsolete evaluator application surfaces

Require the old Web evaluator route, navigation, PageModel, and product-route tests to be absent. Retain only the standalone tool, its focused tests, its ADR/change record, and shared advisory policy calls.

### N004 — remove automatic Box business retry

Search timers, queue handlers, retry schedulers, backoff fields, outbox processors, and recovery services. Delete any path that autonomously retries failed Box business custody after the recorded failure. Retain only the explicit staff command and bounded in-flight technical handling.

### N005 — remove configurable gate bypasses

Delete configuration that disables instruction completeness, image completeness, or staff review. Provider policy may select acceptable evidence but not a boolean bypass.

### N006 — remove alpha EVA image selection

Delete selected-image IDs, ordering controls, tool parameters, PageModel fields, and tests that allow staff/MCP to choose an alpha EVA subset or order.

### N007 — remove dangerous unresolved external activation

Keep Bicep or adapter code only when it expresses an accepted, non-executable target contract and remains disabled without exact configuration. Delete hard-coded subscriptions, resource groups, mailboxes, Box roots, identities, recipients, migration identities, credentials, deployment commands, and destructive predecessor actions.

No local fallback may report success when an external capability is absent.

## 19. Phase O — documentation reconciliation

Update documentation only after final code, callers, and tests settle.

### O001 — requirements and operator truth

Keep PR23 `docs/requirements.md` and `docs/operator-notes.md` materially unchanged except for direct conflict resolution authorised during this task. Do not rewrite intended behavior to match incomplete code.

### O002 — capabilities

Keep the 229 capability identities and PR23 release allocation. Update only links or boundary text required by the final ADR/path structure. Allocation never becomes implementation status.

### O003 — architecture

Rewrite affected current-state sections in `docs/architecture.md` from actual final callers and dependencies. State separately what is implemented, caller-proved, absent, deferred, and externally gated. Remove PR23's now-stale pre-PR24 implementation snapshot.

### O004 — operations

Update `docs/operations.md` with commands that were run successfully against their owning project. State what each command proves and does not prove. Record the repository-policy verifier as skipped/deferred, never passed.

### O005 — design mapping

Update source/runtime mappings and traceability for retained Web surfaces. Do not change durable visual rules merely because incoming code differed.

### O006 — QDOS change record

Update the existing change record with:

- exact merged source heads;
- final implementation and caller evidence;
- all 128 capability states;
- corrected ADR-0013 clause evidence;
- checks run and exact results;
- explicit absent/deferred live work;
- no claims of deployment, live verification, operator acceptance, or management acceptance.

### O007 — externally gated alpha work

List these as absent or unverified until separately authorised and proved:

- accepted route cohorts and all 88 provider dispositions;
- live Graph mailbox caller;
- approved Box enterprise/identity/root;
- selected DVLA/DVSA/VRM contracts;
- exact Azure subscription/resource group/region/identities;
- deployment, restore, rollback, and recovery;
- predecessor retirement;
- operator acceptance;
- Collision Engineers management approval.

### O008 — link and command validation

Follow every changed first-party Markdown link and anchor. Verify every command against the executable/project that owns it. Do not open forbidden corpus destinations to validate them.

## 20. Phase P — focused test implementation

Add or update focused tests before running the full ladder.

### P001 — Core rule tests

Cover at minimum:

- Image Intake with and without normalized VRM;
- no Case/reference allocation for Image Intake;
- automatic association success, ambiguity, contradiction, post-report rejection, unlink, and reassociation;
- identity-critical allocation gates versus ordinary `Not ready` detail;
- three independent mandatory readiness gates;
- three vehicle gates and named exception history;
- provenance distinct from confirmation;
- `001`, `999`, `1000`, `9999`, and exhaustion;
- linked-replacement sequence continuation;
- manual-only cancellation;
- staff-only Box retry and no autonomous retry;
- deterministic all-eligible-image EVA bundle;
- Automation Actor authorization and no staff impersonation;
- Europe/London `New cases today` boundaries and exclusions.

### P002 — architecture tests

Prove:

- Core has no Infrastructure/Web/Worker/workspace dependency;
- one implementation per business rule;
- evaluator and workspaces are absent from `Pegasus.slnx` and application references;
- Collision Brain is not an application caller or deployment input;
- MCP policy resides in Core and uses the Automation Actor boundary;
- no old `docs/decisions` path or staff MCP active source remains.

### P003 — integration and genuine-caller tests

Exercise real local Web, database, Worker/Functions, MCP HTTP, file/custody, and evaluator boundaries. Do not substitute direct service calls where the acceptance criterion names a caller.

### P004 — independent literal rule review

Have a reviewer who did not derive behavior from the implementation compare literal outputs and mapping values with ADR-0013 and `docs/requirements.md`. Correct the implementation or tests when they disagree with authority.

## 21. Phase Q — verification ladder

Run the smallest applicable check first and fix forward. Do not abort merges or reset completed work.

### Q001 — main solution restore and build

From the repository root:

```powershell
dotnet restore .\Pegasus.slnx --locked-mode
dotnet build .\Pegasus.slnx --configuration Release --no-restore
```

### Q002 — direct test projects

```powershell
dotnet test .\tests\Pegasus.Core.Tests\Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test .\tests\Pegasus.ArchitectureTests\Pegasus.ArchitectureTests.csproj --configuration Release --no-build
dotnet test .\tests\Pegasus.IntegrationTests\Pegasus.IntegrationTests.csproj --configuration Release --no-build
```

Run the performance project introduced by the folded dirty continuation, then run the exact PR24 pressure command from the repository root:

```powershell
.\scripts\Invoke-QdosAlphaAcceptance.ps1 -Profile CiPressure -SourceRevision (git rev-parse HEAD)
```

The previously failing pressure check must pass at the final head and write only content-safe evidence beneath `artifacts/qdos-alpha-acceptance/`.

### Q003 — full solution

```powershell
dotnet test .\Pegasus.slnx --configuration Release --no-build
```

Record counts as dated exact-head evidence only, not evergreen documentation.

### Q004 — standalone evaluator

```powershell
dotnet restore .\scripts\email-eval-desktop\Pegasus.EmailEvaluation.Desktop.csproj
dotnet build .\scripts\email-eval-desktop\Pegasus.EmailEvaluation.Desktop.csproj --configuration Release --no-restore
dotnet test .\scripts\email-eval-desktop\tests\Pegasus.EmailEvaluation.Desktop.Tests.csproj --configuration Release
```

Perform one local-copy-only smoke run. Do not access Outlook, Box, Azure, or operational data.

### Q005 — Collision Brain workspace

From `workspaces\ai-centre\services\collision-brain`:

```powershell
dotnet restore .\CollisionBrain.slnx --locked-mode
dotnet build .\CollisionBrain.slnx --configuration Release --no-restore
dotnet test .\CollisionBrain.slnx --configuration Release --no-build
```

Use memory/in-process adapters only. Do not start Docker, migrate an external database, ingest source material, or call a provider.

### Q006 — report renderer workspace

From `workspaces\report-renderer`:

```powershell
dotnet restore .\CollisionRenderer.sln
dotnet build .\CollisionRenderer.sln --configuration Release --no-restore
dotnet test .\tests\CollisionRenderer.Core.Tests\CollisionRenderer.Core.Tests.csproj --configuration Release --no-build
dotnet test .\tests\CollisionRenderer.Mcp.Tests\CollisionRenderer.Mcp.Tests.csproj --configuration Release --no-build
```

Run one deterministic supplied JSON-to-PDF render into repository `artifacts/`. Verify command success and output existence. Do not claim visual, application-caller, deployment, or operator acceptance from that render.

### Q007 — repository/documentation checks

Run the repository's current language, link, workspace, and policy commands exactly as documented. Record the policy verifier as skipped/deferred if it exits through its accepted no-op path.

### Q008 — staged delivery checks

Before the final consolidation commit:

```powershell
git diff --cached --check
git diff --cached --name-status
git status --short
```

Scan staged content for secrets, operational data, protected packages, corpus paths, generated output, stale decision paths, and unapproved external targets.

## 22. Phase R — exact-head review and PR preparation

### R001 — exact-base/head review

Review `origin/main...pegasus-realign` and the exact PR23 base/head. Cover:

- every ADR-0013 clause;
- every `0.1.0-alpha.1` capability row;
- deleted and renamed documentation paths;
- duplicate Core/business implementations;
- built-but-uncalled surfaces;
- migrations and snapshot consistency;
- workspaces and protected packages;
- secrets and local/operational data;
- deferred capability activation;
- real callers versus registration;
- test assertions against literal business rules.

### R002 — verify source ancestry and retained intent

Confirm the final PR23 history contains merge ancestry for:

- `workflow/20260729-deliver-qdos-alpha`;
- `fold/20260730-main-staff-case-workspace`;
- `pegasus-email-eval`;
- `pegasus-report-renderer`;
- `pegasus-ai-centre-rag-pipeline`.

Confirm every source worktree remains intact except PR20. Confirm no source branch was pushed or deleted.

### R003 — prepare the PR23 description

Prepare, but do not publish without exact GitHub-write approval, a PR23 title/body that states:

- PR23 owns documentation realignment and consolidation;
- PR24 implementation was folded and remediated against PR23 authority;
- the source worktrees included;
- implemented, caller-proved, and locally verified outcomes separately;
- remaining absent/deferred external alpha work;
- exact checks and final head;
- no cloud, mailbox, Box, deployment, credential, or destructive external write occurred;
- PR24's previously failing pressure check is fixed and green at the final exact head; if it is not green, the task remains incomplete and the PR is not described as review-ready;
- no operator or management acceptance is claimed.

### R004 — approval-gated push and PR edit

This is an authority gate, not an implementation choice. Stop here until a prompt explicitly approves pushing branch `pegasus-realign` and editing PR23.

After that exact approval, push the already reviewed head:

```powershell
git push origin pegasus-realign
```

Set the PR title exactly to `Consolidate Pegasus documentation and QDOS alpha implementation`. Set the PR body to the evidence prepared in R003 without adding a deployment, live-verification, operator-acceptance, or management-acceptance claim. Use `gh pr edit 23 --repo collisionengineers/pegasus` only after the final body has been displayed to and approved by the operator.

### R005 — exact-head PR checks after an approved push

After an approved push, require every PR23 check to pass at the exact pushed head, including validation, source-workspaces, and `qdos-pressure`. A check on an older head is not evidence. Without push approval, report this gate as not run and leave the branch locally verified and push-ready rather than calling PR23 green.

## 23. Completion boundary

The long-running task is complete only when all of the following are true:

1. PR23 contains all five source merges and the atomized runbook commit.
2. PR23 documentation structure and authority are intact.
3. Rejected PR24 ADR-0014 and `docs/decisions/` are absent.
4. Standalone evaluator ADR-0014 is correctly routed and the evaluator is outside the application.
5. The confirmed PR24 requirement defects are corrected with focused and genuine-caller tests.
6. One Core owner exists for every business rule.
7. Base migrations are unchanged and the final PR-local migration sequence matches the final model.
8. Renderer evidence is routed without altering supplied files.
9. Collision Brain is independently buildable, non-calling, and free of committed intake/corpus/generated material.
10. All applicable main, evaluator, Collision Brain, renderer, documentation, and pressure checks pass locally.
11. The existing QDOS change record accurately distinguishes all 128 capability evidence states.
12. Independent literal requirements review is complete.
13. No external/cloud write, deployment, secret exposure, protected-package mutation, or unrelated worktree alteration occurred.
14. PR23 is locally verified and ready for an approved push and human review; after an approved push, its exact-head PR checks are green.

The task does not include merging PR23 or PR24 to `main`. Stop at the review-ready boundary unless a later prompt explicitly contains `MERGE AUTH GRANTED`.
