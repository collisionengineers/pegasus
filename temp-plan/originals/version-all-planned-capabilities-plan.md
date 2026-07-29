## Context

Assign an exact Semantic Version target to every planned Pegasus capability documented in the repository, preserve the 29 permanent `Not planned` boundaries as deliberately unallocated, and publish complete feature-level coverage to GitHub. The canonical scope is the 229-ID inventory in `docs/product/capabilities.md`: 128 `Now` capabilities already target `0.1.0-alpha.1`, while 32 `Next` and 40 `Later` capabilities require exact release allocation. Use the existing user-owned [Pegasus Delivery Project 3](https://github.com/users/collisionengineers/projects/3) and mirror each of the 200 planned capabilities as one draft item; create a replacement project only if a correctly scoped execution-time read proves Project 3 no longer exists.

## Approach

### 1. Activate one release-planning work identity and inspect the exact GitHub targets

- Use repository `collisionengineers/pegasus` and user-owned Project 3, `Pegasus Delivery`; do not create an organization project or a second tracker. Before writes, read Project 3, its repository link, fields, and all items; read all repository milestones and issues. This session confirmed milestone `0.1.0-alpha.1` exists, is open, and is assigned to issue #3, but the current `gh` token lacks `read:project`.
- On approved execution, run `gh auth refresh -s read:project -s project` for the active `collisionengineers` login; this is the only credential/account scope change in the plan. A missing scope or unauthenticated browser 404 does not prove Project 3 is missing. Require a correctly scoped Project 3 readback before deciding whether the fallback is needed.
- Create one activated repository task titled `[Task]: Allocate every planned capability and populate Pegasus Delivery`, labeled `type:task`, with these issue-form sections:
  - **Required outcome:** assign an exact Semantic Version to all 200 `Now`/`Next`/`Later` capability IDs, preserve all 29 `Not planned` boundaries as unallocated, and mirror the 200 planned IDs in Project 3.
  - **Authority and context:** link `docs/product/capabilities.md`, `docs/roadmap.md`, this accepted execution plan/change record, and issue #3 as the existing `0.1.0-alpha.1` delivery owner.
  - **Acceptance evidence:** exact repository allocation counts, green policy/build/test checks, 12 milestone titles, 200 unique capability draft items, zero `Not planned` items, and Project field/value readback.
  - **Boundaries and dependencies:** planning allocation is not activation or implementation; no dormant route/schema/caller; no historical document rewrite; no Azure/deployment operation.
- Add that task issue to Project 3 with Status `In progress`, Priority `P1 High`, Horizon `Now`, and milestone `0.1.0-alpha.1`. The planning worktree is clean on `document-reconciling`, six commits ahead of `main`; preserve that parent work by creating `task/20260728-version-planned-capabilities` from its exact head. On the child branch, create `docs/changes/2026-07-28-version-all-planned-capabilities.md` as the single `type: task`, `status: in_progress`, `risk: medium` change record linked to the new issue. Record the selected numbering and draft-item model, source inspection, verification, exact external writes, and final readback there. Do not use issue #3 or #6 as the work identity for this separate allocation change.

### 2. Reconcile and allocate the canonical capability inventory

- Treat `docs/product/capabilities.md` as the sole feature identity/allocation owner, `docs/roadmap.md` as the release-order/horizon owner, and GitHub as a mirror of planning/work state—not as a second product inventory. Enumerate exactly 229 unique IDs and preserve every durable outcome, horizon, canonical owner, and activation/boundary statement.
- Resolve the current `TRI-04` conflict to the operator-authoritative wording from `docs/operator-notes/business-process/case-lifecycle.md`: `Two independently optional findings, with at least one required: Roadworthiness = Roadworthy/Unroadworthy; Assessment = Repairable/Total loss; reasoned replacement`. Remove the conflict markers and competing stale row before parsing the table.
- Keep all 128 `Now` rows at `0.1.0-alpha.1`, matching open issue #3 and the accepted first-usable-release scope. Keep all 29 `Not planned` rows at `unallocated`; they receive no milestone and no Project capability item.
- Update the inventory preamble/allocation rules to distinguish the mandatory Project draft mirror from an activated repository issue: each planned ID has one draft card, but only accepted work is converted to an issue. Require any future outcome/horizon/target change to update the capability row, roadmap, matching milestone, Project card, and change evidence together.
- Replace `unallocated` on the 32 `Next` and 40 `Later` rows with this exact first-introduction release map. The IDs in this table are exhaustive; do not move a capability between releases during implementation:

| Target release | Stage and dependency intent | Capability IDs | Count |
| --- | --- | --- | ---: |
| `0.1.0-alpha.1` | Existing complete QDOS alpha; unchanged | every row whose Horizon is `Now` | 128 |
| `0.2.0` | Provider expansion and intake fidelity after QDOS acceptance | `DATA-02`, `INT-04`, `INT-14`, `INT-15`, `INT-16`, `INT-28`, `INT-32`, `AI-05` | 8 |
| `0.3.0` | Four-mailbox classification, association, folder actions, email workspace, and email MCP | `INT-05`, `INT-06`, `INT-07`, `MAIL-01`, `MAIL-02`, `MAIL-03`, `MAIL-04`, `MAIL-05`, `MAIL-06`, `MAIL-07`, `MAIL-08`, `MAIL-09`, `MAIL-10`, `MAIL-11`, `MAIL-13`, `MAIL-23`, `UI-10`, `UI-14`, `MCP-05` | 19 |
| `0.4.0` | Principal-scoped provider API and post-report query/dispute casework | `API-01`, `API-02`, `API-03`, `API-04`, `CASE-23` | 5 |
| `0.5.0` | Extended case types and staff/outbound communication channels | `CASE-05`, `CASE-06`, `MAIL-12`, `MAIL-19`, `EXT-15` | 5 |
| `0.6.0` | Individually approved operator AI assistance | `AI-01`, `AI-02`, `AI-03`, `AI-04`, `AI-06` | 5 |
| `0.7.0` | Optional direct EVA API coexistence before replacement | `EXT-04` | 1 |
| `1.0.0` | Pegasus-owned engineering record/workbench and transfer of EVA assignment, estimating, valuation, and report-preparation authority | `CASE-22`, `CASE-31`, `ENG-01`, `ENG-02`, `UI-15`, `EXT-05`, `EXT-06`, `EXT-07`, `EXT-09`, `EXT-10`, `EXT-12`, `EXT-13` | 12 |
| `1.1.0` | Deterministic report and fee-note rendering | `EXT-08`, `RPT-01`, `RPT-02`, `RPT-03`, `RPT-04`, `RPT-05` | 6 |
| `1.2.0` | Targeted report distribution, accounts/invoicing, and management information | `MAIL-17`, `EXT-11`, `MI-01`, `MI-02`, `MI-03` | 5 |
| `1.3.0` | Vendor-neutral AI work requests, Engineer-reviewed query proposals, and staff-selected AI Assessor | `AI-07`, `AI-08`, `AI-09` | 3 |
| `1.4.0` | Conditional capture and domain outcomes after direct promotion decisions | `EXT-16`, `EXT-17`, `EXT-19` | 3 |

- Preserve the activation gates while assigning targets: `AI-02`–`AI-04` and `AI-06` still require evidence that deterministic rules are insufficient; `0.7.0` is an independent optional release and is not a prerequisite for `1.0.0` if no usable EVA API appears; `1.0.0` transfers report-preparation data/workflow while `1.1.0` separately activates deterministic rendering; `EXT-16`, `EXT-17`, and `EXT-19` remain prohibited from implementation until their direct promotion decisions. A missed optional/conditional release number may be skipped; it never blocks unrelated later work.
- Add a release-count summary beside the horizon summary: 200 planned/allocated and 29 `Not planned`/unallocated. Do not put planned dates on any release; the source says the first target is ASAP and production timing is undetermined.

### 3. Reconcile every active release reference and enforce the new allocation contract

- Rewrite `docs/roadmap.md` as an ordered exact-release roadmap using the stage names, IDs, counts, prerequisites, and independent/conditional behavior above. Keep `Now`/`Next`/`Later` as planning horizons; exact versions refine rather than replace them.
- Update active allocation callsites in `docs/index.md`, `docs/product/index.md`, `docs/product/qdos-alpha-gap.md`, `docs/product/boundaries.md`, `docs/product/areas/*.md`, `docs/architecture/README.md`, `docs/operations.md`, `design/product/requirements.md`, `design/product/ui-spec.md`, `design/product/traceability-matrix.md`, and `design/references/directions/*.md`. Replace only claims that a current `Next`/`Later` capability is unallocated; preserve the meaning that no deferred UI/caller/placeholder is active. In `docs/index.md`, keep the capability inventory authoritative for allocation while recording GitHub as the draft-plan mirror and live work-state owner.
- In `design/product/traceability-matrix.md`, rename the column to `Horizon / target` and render each planned row as `Now / 0.1.0-alpha.1`, `Next / <exact version>`, or `Later / <exact version>`; retain `Not planned / unallocated` for the permanent boundaries. Do not change role, owner/caller, negative rule, or UI destination merely because a version is assigned.
- Preserve every file under `docs/history/`, dated change/agent/evaluation evidence, and accepted ADR decision bodies verbatim. Update the current `docs/decisions/README.md` summaries where they otherwise present an unallocated value as current; do not rewrite prior decision bodies to make history look newly allocated.
- Change `scripts/Test-RepositoryPolicy.ps1` from the current one-target-per-horizon rule to these invariants:
  - `Now` must equal `0.1.0-alpha.1`;
  - `Next` and `Later` must contain a valid exact SemVer from the 11 future releases above and must never equal `unallocated`;
  - `Not planned` must equal `unallocated`;
  - release counts must be exactly `128, 8, 19, 5, 5, 5, 1, 12, 6, 5, 3, 3` in the table order above;
  - horizon counts remain `128 / 32 / 40 / 29`, IDs remain 229 and unique, planned IDs total 200, and the traceability matrix must match both horizon and target for every ID.
- Update the policy success message to report 229 unique capabilities, 200 exact planned allocations across 12 releases, and 29 permanent unallocated boundaries. No generated manifest or second release ledger is committed.

### 4. Create release milestones without dates

- Reuse the existing open `0.1.0-alpha.1` milestone and its existing description. Upsert these 11 additional open milestones in `collisionengineers/pegasus`, with no due date:
  - `0.2.0` — `Additional-provider activation, provider-location evidence, legacy source processing, pairing readiness, and reviewed image assistance.`
  - `0.3.0` — `Four-mailbox classification, association, folder actions, email workspace, and staff MCP email actions.`
  - `0.4.0` — `Principal-scoped provider submission/status/credential APIs and typed post-report query/dispute casework.`
  - `0.5.0` — `Diminution and Commercial case types plus authenticated/outbound mail, chasers, and WhatsApp coexistence.`
  - `0.6.0` — `Individually approved operator AI assistance after deterministic-rule evidence and review gates.`
  - `0.7.0` — `Conditional direct EVA API coexistence, only if a usable vendor operation is accepted.`
  - `1.0.0` — `Pegasus-owned engineering record/workbench, assignment, estimating, valuation, and report-preparation replacement; deterministic rendering follows separately.`
  - `1.1.0` — `Deterministic assessment, Audit, Diminution, addendum, fee-note, and repair-specification rendering.`
  - `1.2.0` — `Targeted report sending, accounting/invoicing, and management measures.`
  - `1.3.0` — `Vendor-neutral Send to AI, Engineer-reviewed query proposals, and staff-selected AI Assessor.`
  - `1.4.0` — `Conditional guided capture, Tractable/Ravin, and custom-domain outcomes after direct activation decisions.`
- Upsert means reuse an exact-title milestone and normalize its description/state/due date; never create a duplicate title. Milestones are release identities. Draft Project items mirror them through a Project field; when a future draft is converted to an activated `type:feature` issue, assign the exact matching milestone at conversion.
- If the milestone preflight finds duplicate exact titles, stop before milestone mutation and record the conflict; do not guess which duplicate to rename, close, or delete.

### 5. Mirror every planned capability into Pegasus Delivery as a draft item

- Reuse Project 3's existing Status options (`Triage`, `Ready`, `In progress`, `In review`, `Done`), Priority options (`P0 Critical`, `P1 High`, `P2 Normal`, `P3 Low`), and Horizon options (`Now`, `Next`, `Later`). Add a text field named `Capability ID` and a single-select field named `Target release` whose options, in order, are `0.1.0-alpha.1`, `0.2.0`, `0.3.0`, `0.4.0`, `0.5.0`, `0.6.0`, `0.7.0`, `1.0.0`, `1.1.0`, `1.2.0`, `1.3.0`, `1.4.0`.
- If either custom field already exists with the correct type, reuse it and preserve unrelated existing values; add the required release options in the specified order and retain any unknown options afterward. If a same-named field has the wrong type, leave it untouched and create `Pegasus Capability ID` or `Pegasus Target release` respectively; record the actual field names in `docs/operations.md` and use them consistently.
- Create or update exactly one Project **draft issue** for each of the 200 planned capability IDs. De-duplicate first by the capability text field, then by a title matching `^\[[A-Z]+-\d+\]`; retain the earliest `createdAt` draft (node-ID lexical order breaks an exact timestamp tie), update it in place, and archive—not delete—additional keyed drafts. Archive any keyed draft for a `Not planned` ID. If an active repository issue or pull request is keyed to a `Not planned` ID, stop and record the product/work-state contradiction instead of closing or altering it. Do not modify unrelated Project items such as unkeyed issues, pull requests, or drafts.
- Draft title algorithm: use `[{ID}] {durable outcome}`; if the candidate exceeds 240 characters, keep its first 239 characters and append `…`. Put the unabridged outcome in the body.
- Use this body shape, populated only from the canonical row:

```markdown
Planning item for `{ID}`; this is not implementation or acceptance evidence.

- **Durable outcome:** {full durable outcome}
- **Canonical owner:** {absolute GitHub link resolved from the row's owner link}
- **Horizon:** `{Now|Next|Later}`
- **Target release:** `{exact version}`
- **Activation/boundary:** {full activation/boundary text}
- **Delivery owner:** {`#3 covers this capability in the active QDOS alpha` for Now; otherwise `Not activated — convert this draft through one accepted issue/change record before implementation`}

Canonical inventory: https://github.com/collisionengineers/pegasus/blob/main/docs/product/capabilities.md
```

- Set Project fields deterministically:
  - `Now`: Status `In progress`, Horizon `Now`, Target release `0.1.0-alpha.1`; the body links issue #3. This means “in the active release scope,” not “implemented.”
  - `Next`: Status `Triage`, Horizon `Next`, exact `0.2.0`–`0.4.0` Target release.
  - `Later`: Status `Triage`, Horizon `Later`, exact `0.5.0`–`1.4.0` Target release.
- Do not infer capability-level Priority from Horizon or release order; no repository authority ranks the 200 individual outcomes. Leave Priority unset on new draft cards and preserve any explicit existing Priority on a matched card. The activated parent task remains `P1 High`.
- Use a temporary PowerShell sync script outside the repository. Read and validate the conflict-free canonical table before any Project mutation, checkpoint item IDs after each successful create, perform writes sequentially, and on retry re-read by Capability ID so a timeout cannot create duplicates. Retry only GitHub rate-limit/transient responses using the returned delay; fail closed on schema, permission, or content mismatch. Do not commit a Project export, generated status ledger, or sync database.
- Update `docs/operations.md` to state that every planned capability has one Project draft item, draft `Target release` mirrors the canonical inventory, repository milestones carry the same release names, and conversion to an issue is the activation boundary. Keep the rule that draft allocation creates no caller, schema, route, credential, or implementation.

### 6. Publish the allocation change and capture exact-head evidence

- After the conflict-free table and policy script pass locally, commit and push `task/20260728-version-planned-capabilities`, then open a stacked draft pull request titled `Allocate planned capabilities to exact releases` with base `document-reconciling`. The pull request contains only this task's change record, allocation authorities, active callsites, and policy check; never merge it from the agent workflow.
- Perform milestone/Project upserts only after the locally validated table is the exact input. Read all fields and keyed items back, record the external write targets/counts and any rate-limit recovery in the change record, then push the evidence update to the same pull request.
- Wait for exact-head repository checks and obtain an independent review of the exact base/head. Remediate every required finding and repeat local/Project readback if the allocation changed. Finish with the change record and parent task issue at `in_review`/Project Status `In review`; leave the 200 capability draft statuses determined by their horizons and leave merging/production acceptance to the repository operator.

## Critical files & anchors

- `docs/product/capabilities.md` — `## Capabilities` and `## Allocation rules`; canonical 229-ID inventory and exact target owner, currently containing the `TRI-04` conflict.
- `docs/roadmap.md` — `## Now`, `## Next`, and `## Later`; canonical dependency/horizon summary that must be rewritten with exact targets.
- `docs/operations.md` — `## GitHub work taxonomy`; records Project 3, current fields, milestone policy, and the capability-to-issue activation boundary.
- `docs/history/plans/delivery-roadmap.md` — `## Dependency shape` through `## Later/...`; historical, non-authoritative evidence for ordering provider activation, parallel Next branches, communications/case types/AI, then EVA/engineering/report/finance work.
- `docs/product/open-decisions.md` — mailbox, EVA, engineering, and Send-to-AI blockers that remain activation gates even after a target version is assigned.

## Verification

- From the repository root in PowerShell 7, with no environment variables or external service credentials required, run:

```powershell
pwsh -NoProfile -File ./scripts/Test-RepositoryLanguage.ps1
dotnet restore ./Pegasus.slnx
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

  The policy check must report 229 unique IDs, horizon counts `128 / 32 / 40 / 29`, 200 exact planned allocations, 12 release targets with counts `128 / 8 / 19 / 5 / 5 / 5 / 1 / 12 / 6 / 5 / 3 / 3`, and 29 `Not planned` rows still unallocated. The build must have zero errors; all non-corpus tests must pass. Do not run or mutate the genuine corpus.
- Search the active owners named in Approach step 3 for `Next`/`unallocated`, `Later`/`unallocated`, and `target unallocated`. Expected result: no current planned capability is described as unallocated. Matches are permitted only in immutable `docs/history/`, dated evidence, preserved accepted-ADR bodies, or explicit explanation of the former state; every `Not planned` row remains `unallocated`.
- With the approved GitHub scopes, read back repository milestones and assert exact set equality with the 12 target titles, unique titles, state `open`, `due_on = null`, and the descriptions in Approach step 4. Confirm issue #3 remains assigned to `0.1.0-alpha.1` and the new allocation task is assigned to the same milestone.
- Read all Project 3 fields and active/archived items with pagination and compare active keyed drafts to the validated canonical table. Required result: 200 unique active capability IDs; 200 active draft contents; zero missing/extra/duplicate planned IDs; zero active `Not planned` IDs; release and horizon counts equal the repository; exact Target release on every active item; `128` Now items at Status `In progress`, `32` Next at `Triage`, and `40` Later at `Triage`. New capability cards have no Priority; any preserved pre-existing Priority is reported separately and is not inferred from allocation. Any archived keyed items are recorded as duplicate/boundary cleanup evidence. Unkeyed existing issues, pull requests, and drafts are ignored, not changed.
- Exercise concrete new mappings end to end:
  - canonical `ACC-01` -> `0.1.0-alpha.1` -> one Project draft at Now/In progress whose body names issue #3;
  - canonical `INT-28` -> `0.2.0` -> one Project draft at Next/Triage;
  - canonical `CASE-31` -> `1.0.0` -> one Project draft at Later/Triage;
  - canonical `RPT-05` -> `1.1.0` -> one Project draft at Later/Triage;
  - canonical `ACC-12` -> `Not planned / unallocated` -> no active capability draft and no milestone assignment.
- Open the authenticated Project 3 UI after API readback, filter by each sample Capability ID, and visually confirm title, Target release, Horizon, and Status are visible and agree with the canonical row. This is Project presentation proof only, not feature implementation or acceptance.
- On the final pull-request head, require green `repository-check` CI and an independent exact-base/exact-head review with no unresolved blocker or required finding. Record the immutable head and Project readback evidence in the change record; do not claim merge, deployment, or product acceptance.

## Assumptions & contingencies

- Version means the first intended release containing the capability. The selected sequence keeps the accepted QDOS scope at `0.1.0-alpha.1`, uses pre-1.0 minors for bounded expansion, and makes EVA authority transfer the `1.0.0` boundary.
- A release assignment is planning, not activation, implementation, caller, deployment, live verification, or acceptance. Existing fail-closed decisions and external-operation approvals remain mandatory.
- Releases have no due dates because repository authority says the first usable release is ASAP and production timing is undetermined. Do not invent calendar commitments in milestones or Project fields.
- If `document-reconciling` has already been merged into `main` before execution, create the same task branch from the updated `main` and target the pull request to `main`. Otherwise keep the documented stacked base; never pull the parent's six commits into this task's diff.
- Project 3 is the chosen tracker. The current token's missing `read:project` scope is an authentication prerequisite, not evidence that the board is absent. If an authorized, correctly scoped `gh project view/list` proves Project 3 exists but is closed, reopen and reuse it; if it exists but access is forbidden, stop for permission rather than creating a duplicate.
- Create a replacement user-owned project named `Pegasus Delivery` only if a correctly scoped read proves Project 3 no longer exists. Link only `collisionengineers/pegasus`, recreate Status/Priority/Horizon plus the two capability fields, capture the returned project number/URL, and update `docs/operations.md` and the change record before syncing. Never keep both boards.
- If `0.7.0` lacks a usable EVA vendor operation, leave that conditional release unshipped and continue the manual handoff until independently accepted replacement slices; do not delay `1.0.0`. If `EXT-16`, `EXT-17`, or `EXT-19` lacks its direct promotion decision, its `1.4.0` planning card remains in Triage and creates no implementation work.
