# Version every planned Pegasus capability

Status: **Hardened execution plan — run only after the documentation-centralization exact head is green and accepted as this plan's base.**

## 1. Outcome and authority

Assign an exact first-introduction Semantic Version target to every planned Pegasus capability, preserve all permanent boundaries as deliberately unallocated, and mirror all 229 capability identities in the existing user-owned [Pegasus Delivery Project 3](https://github.com/users/collisionengineers/projects/3): 200 planned delivery cards and 29 explicitly `Not planned / unallocated` boundary cards. Boundary cards are exclusion records only: they receive no milestone or activation issue and only the explicit `Not planned` status—never an implementation lifecycle state.

After documentation centralization:

- `docs/capabilities.md` is the sole owner of capability identity, durable outcome, horizon, exact target, canonical owner and activation/boundary wording.
- `docs/requirements.md` owns the ordered release/dependency narrative and timing qualifiers. It is not a second ID/target ledger.
- `docs/open-decisions.md` owns unresolved activation gates.
- `docs/operations.md` owns GitHub Project, milestone, activation and synchronization semantics.
- `design/product/traceability-matrix.md` mirrors horizon/target and design evidence; it never writes back product truth.
- GitHub milestones and Project fields/cards mirror planning/work state; they never become product authority.

Allocation, issue activation, implementation, caller proof, deployment, live verification, Project presentation and operator/management acceptance remain distinct evidence states. A release assignment creates no route, caller, schema, credential, external write, UI placeholder, dormant service or acceptance evidence.

The input precondition is one conflict-free canonical table with exactly 229 rows and 229 unique IDs: 128 `Now`, 32 `Next`, 40 `Later`, 29 `Not planned`. `TRI-04` must already contain the accepted centralization wording once, with no conflict markers; this plan does not resolve that business rule again.

## 2. Activate one work identity from the accepted prerequisite head

1. Record the exact green documentation-centralization PR base/head and review/check evidence. Use that head as this plan's immutable parent whether the PR has already merged or this work must remain stacked. Never use the old `467284f`, a branch name, commit count or fixed worktree path as an execution base.
2. In `collisionengineers/pegasus`, create one `type:task` issue titled `[Task]: Allocate every planned capability and populate Pegasus Delivery`. Its body must include:
   - **Required outcome:** exact targets for all 200 planned IDs; 29 permanent boundaries remain unallocated; 229 keyed Project items, using drafts except where one accepted activated issue/PR owns a planned ID.
   - **Authority:** links to the exact accepted prerequisite head's `docs/capabilities.md`, `docs/requirements.md`, this accepted plan/checksum and issue #3 as the `0.1.0-alpha.1` delivery owner.
   - **Evidence:** repository counts, policy/build/test results, 12 milestone identities, Project readback and UI presentation check.
   - **Boundary:** planning is not activation/implementation; no Azure, deployment, mailbox, Box, EVA, provider, WhatsApp or other external-service operation is authorized by allocation.
3. After milestone preflight, assign the task issue to `0.1.0-alpha.1`. After Project field preflight selects the physical logical-field owners, add the task to Project 3 with Delivery status `In progress`, Priority `P1 High`, Horizon `Now`, and Target release `0.1.0-alpha.1`.
4. Create a unique child branch/worktree from the exact prerequisite head and add `docs/changes/2026-07-29-version-all-planned-capabilities.md` as the sole `type: task`, `status: in_progress`, `risk: medium` change record linked to the new issue. Record decisions, exact external writes, verification and readback there. Do not reuse issue #3 or #6 as this change identity.
5. If the prerequisite PR is unmerged, open this change as a stacked PR against its exact branch. If it is merged, branch from the exact accepted `main` merge lineage and target `main`. Never pull prerequisite commits into this task by merge, reset, force-move or hidden rebasing.

Before any Project operation, inspect the active GitHub account and scopes. The current observed token lacks `read:project`. Changing credential scopes is a separate exact credential/account write: obtain explicit approval for the named GitHub account and `read:project`/`project` scopes before running `gh auth refresh`. A missing scope or unauthenticated browser 404 is not evidence that Project 3 is absent.

## 3. Exact SemVer release order

`Target release` means the first intended release containing the capability. This is an ordered, dependency-qualified release sequence, not a calendar schedule. No due dates or invented release dates are permitted; retained source evidence says first usable release `ASAP` and production timing `Undetermined`.

| Order | Target release | Stage and dependency intent | Capability IDs | Count |
| ---: | --- | --- | --- | ---: |
| 1 | `0.1.0-alpha.1` | Now/QDOS-alpha target scope; allocation unchanged, not a completion claim | every canonical row whose Horizon is `Now` | 128 |
| 2 | `0.2.0` | Provider expansion and intake fidelity after QDOS acceptance | `DATA-02`, `INT-04`, `INT-14`, `INT-15`, `INT-16`, `INT-28`, `INT-32`, `AI-05` | 8 |
| 3 | `0.3.0` | Four-mailbox classification, association, folder actions, email workspace and email MCP | `INT-05`, `INT-06`, `INT-07`, `MAIL-01`, `MAIL-02`, `MAIL-03`, `MAIL-04`, `MAIL-05`, `MAIL-06`, `MAIL-07`, `MAIL-08`, `MAIL-09`, `MAIL-10`, `MAIL-11`, `MAIL-13`, `MAIL-23`, `UI-10`, `UI-14`, `MCP-05` | 19 |
| 4 | `0.4.0` | Principal-scoped provider API and post-report query/dispute casework | `API-01`, `API-02`, `API-03`, `API-04`, `CASE-23` | 5 |
| 5 | `0.5.0` | Extended case types and staff/outbound communication channels | `CASE-05`, `CASE-06`, `MAIL-12`, `MAIL-19`, `EXT-15` | 5 |
| 6 | `0.6.0` | Individually approved operator AI assistance | `AI-01`, `AI-02`, `AI-03`, `AI-04`, `AI-06` | 5 |
| 7 | `0.7.0` | Optional direct EVA API coexistence before replacement | `EXT-04` | 1 |
| 8 | `1.0.0` | Pegasus-owned engineering record/workbench and transfer of EVA assignment, estimating, valuation and report-preparation authority | `CASE-22`, `CASE-31`, `ENG-01`, `ENG-02`, `UI-15`, `EXT-05`, `EXT-06`, `EXT-07`, `EXT-09`, `EXT-10`, `EXT-12`, `EXT-13` | 12 |
| 9 | `1.1.0` | Deterministic report and fee-note rendering | `EXT-08`, `RPT-01`, `RPT-02`, `RPT-03`, `RPT-04`, `RPT-05` | 6 |
| 10 | `1.2.0` | Targeted report distribution, accounts/invoicing and management information | `MAIL-17`, `EXT-11`, `MI-01`, `MI-02`, `MI-03` | 5 |
| 11 | `1.3.0` | Vendor-neutral AI work requests, Engineer-reviewed query proposals and staff-selected AI Assessor | `AI-07`, `AI-08`, `AI-09` | 3 |
| 12 | `1.4.0` | Conditional capture and domain outcomes after direct promotion decisions | `EXT-16`, `EXT-17`, `EXT-19` | 3 |

Exact totals: 200 planned/allocated and 29 `Not planned`/unallocated. The 72 explicit future IDs above must equal the canonical `Next ∪ Later` set in both directions, with no missing, extra or duplicate ID. The 29 boundary IDs must remain `Not planned / unallocated`, occur in no release row or milestone assignment, and exist only as clearly marked non-activatable Project boundary cards.

### Dependency and activation semantics

Record these edges in the ordered section of `docs/requirements.md`; they clarify sequencing and do not add capability scope:

- accepted `0.1.0-alpha.1` evidence precedes activation of later releases;
- `INT-04 -> INT-05, INT-06, INT-07`;
- `INT-28 -> INT-32` within `0.2.0`;
- accepted `CASE-31`/`ENG-01`/`ENG-02` data and workflow precede `EXT-08`/`RPT-01`–`RPT-05` rendering;
- accepted report events/rendering precede `MAIL-17` and the `MI-*` consumption path;
- within `1.3.0`, `AI-09` transport/lease/recovery is proved before any AI proposal caller, and `AI-07` remains blocked on assignment authority;
- `AI-02`–`AI-04` and `AI-06` remain blocked until evidence shows deterministic rules are insufficient;
- `0.7.0`/`EXT-04` is an optional, non-blocking branch and is not a prerequisite for `1.0.0`;
- `EXT-16`, `EXT-17` and `EXT-19` remain non-blocking Triage cards and prohibited from implementation until their direct promotion decisions.

All mailbox, WhatsApp, EVA, Box, provider, AI and other source-specific approval gates remain mandatory. A target release never authorizes an external read/write, credential, vendor contract or product caller.

## 4. Update canonical and mirrored repository owners

1. Update `docs/capabilities.md` only in its target-release cells and allocation summary/rules. Preserve every ID, durable outcome, horizon, canonical-owner link and activation/boundary statement byte-for-byte except link normalization already required by centralization.
2. Add one ordered release/dependency section to `docs/requirements.md` using the exact table/counts/semantics above. Keep horizons as a second classification. Do not recreate a separate roadmap file or any second release ledger.
3. Update `docs/open-decisions.md` only where a current gate needs its exact target label; assigning a version does not close a decision.
4. Update `docs/operations.md` with the one-way synchronization contract and this exact selected Project Delivery-status rule: on a keyed planned-capability draft, `In progress` means “included in the active release scope”; implementation progress begins only through its accepted owning issue/change record. `Triage` remains the state for deferred planned drafts; `Not planned` is reserved for permanent boundary cards and never means backlog.
5. Update `design/product/requirements.md`, `design/product/ui-spec.md` and `design/product/traceability-matrix.md` without changing owner/caller/negative-rule/UI meaning. Rename the matrix value to `Horizon / target`; render planned rows as `Now / 0.1.0-alpha.1`, `Next / <exact version>` or `Later / <exact version>`, and boundaries as `Not planned / unallocated`. Preserve the ten pre-alpha execution checkpoints as separate evidence qualifiers, not release targets. Record Operations-first as selected.
6. Update `docs/decisions/README.md` only as the mutable current summary: provider API `0.4.0`; broader classified-email/MCP work `0.3.0`; staff MCP remains `0.1.0-alpha.1` intake/case/document scope. Never rewrite immutable ADR/decision/change bodies.
7. Do not recreate or reference any path retired by the accepted documentation-centralization disposition manifest. Do not scan, hash, inspect or cite the exact `docs/reference/imp-docs/` prefix; prune it before path enumeration, content read, stat, hash, link or anchor work.

Extend `scripts/Test-RepositoryPolicy.ps1` to reject conflict markers and validate:

- 229 rows, 229 unique IDs and six nonempty canonical fields;
- horizon counts `128 / 32 / 40 / 29`;
- exactly 200 planned targets and 29 `Not planned / unallocated` boundaries;
- exact allowed releases `{0.1.0-alpha.1, 0.2.0, 0.3.0, 0.4.0, 0.5.0, 0.6.0, 0.7.0, 1.0.0, 1.1.0, 1.2.0, 1.3.0, 1.4.0}` and exact SemVer 2.0 syntax;
- exact release counts `128 / 8 / 19 / 5 / 5 / 5 / 1 / 12 / 6 / 5 / 3 / 3`;
- `Now = 0.1.0-alpha.1`; every `Next`/`Later` target is in the future allowlist and never `unallocated`; `Not planned = unallocated`;
- exact two-way ID, horizon and target equality between `docs/capabilities.md` and the traceability matrix;
- no active references to deleted owners outside the documented immutable baseline-only exceptions.

The policy success message reports 229 unique capabilities, 200 exact allocations across 12 releases and 29 permanent unallocated boundaries. No generated manifest or second release ledger is committed.

## 5. Milestone preflight and exact identities

Read every repository milestone with pagination before mutation. Duplicate exact titles, a contradictory state, or an unrelated milestone carrying one of these exact titles blocks writes and requires recorded resolution; never guess which object to rename, close or delete.

Reuse the existing open `0.1.0-alpha.1` milestone without changing its accepted identity or issue #3 assignment. Upsert these 11 additional open milestones in `collisionengineers/pegasus`, all with `due_on = null`:

| Milestone | Exact description |
| --- | --- |
| `0.2.0` | Additional-provider activation, provider-location evidence, legacy source processing, pairing readiness, and reviewed image assistance. |
| `0.3.0` | Four-mailbox classification, association, folder actions, email workspace, and staff MCP email actions. |
| `0.4.0` | Principal-scoped provider submission/status/credential APIs and typed post-report query/dispute casework. |
| `0.5.0` | Diminution and Commercial case types plus authenticated/outbound mail, chasers, and WhatsApp coexistence. |
| `0.6.0` | Individually approved operator AI assistance after deterministic-rule evidence and review gates. |
| `0.7.0` | Conditional direct EVA API coexistence, only if a usable vendor operation is accepted. |
| `1.0.0` | Pegasus-owned engineering record/workbench, assignment, estimating, valuation, and report-preparation replacement; deterministic rendering follows separately. |
| `1.1.0` | Deterministic assessment, Audit, Diminution, addendum, fee-note, and repair-specification rendering. |
| `1.2.0` | Targeted report sending, accounting/invoicing, and management measures. |
| `1.3.0` | Vendor-neutral Send to AI, Engineer-reviewed query proposals, and staff-selected AI Assessor. |
| `1.4.0` | Conditional guided capture, Tractable/Ravin, and custom-domain outcomes after direct activation decisions. |

Milestones are release identities. A Project draft has no milestone. Conversion of a draft through an accepted `type:feature` issue/change record assigns the matching milestone; GitHub never changes the canonical target.

## 6. Deterministic fail-closed Project synchronization

### Preflight

With correctly scoped authorization, read Project 3, its repository link, fields, options, active and archived items, and all repository issues/PRs using pagination. Prove it is the existing user-owned `Pegasus Delivery` tracker before writes. Ensure repository issue #3 is present once as an unkeyed delivery item; add it if absent, but never set its Capability ID because it intentionally owns the 128-card alpha cohort. The allocation task issue is likewise an unkeyed change item. If access is forbidden, stop for permission. Reopen/reuse it only if an authorized read proves it exists but is closed. Create a replacement user-owned project only if an authorized, correctly scoped read proves Project 3 no longer exists; record the returned number/URL and never leave two trackers.

Validate keys only against the 229-ID canonical set. Stop before mutation when:

- a card's `Capability ID` field and `[ID]` title disagree;
- a planned ID is already keyed to an active repository issue/PR without matching activation/change evidence;
- a `Not planned` ID is keyed to an active issue/PR;
- a required field name exists with the wrong type and the documented prefixed fallback also exists with the wrong type;
- required single-select option names are duplicated or cannot be ordered deterministically.

### Fields

Use these logical fields and record the selected physical field name/ID. Reuse an existing field only when its type and required option values are compatible and it can be left without dropping/renaming any unrelated option or assignment; otherwise leave it untouched and create the exact prefixed fallback. If both preferred and fallback are incompatible, stop before card mutation.

| Logical field | Preferred existing field | Safe fallback | Required type/options |
|---|---|---|---|
| Delivery status | `Status` | `Pegasus Delivery status` | single-select: `Triage`, `Ready`, `In progress`, `In review`, `Done`, `Not planned` in this order |
| Priority | `Priority` | `Pegasus Priority` | single-select: `P0 Critical`, `P1 High`, `P2 Normal`, `P3 Low` in this order |
| Horizon | `Horizon` | `Pegasus Horizon` | single-select: `Now`, `Next`, `Later`, `Not planned` in this order |
| Capability ID | `Capability ID` | `Pegasus Capability ID` | text |
| Target release | `Target release` | `Pegasus Target release` | single-select with the 12 exact release options in section 3 order, followed by `unallocated` |

A compatible reused single-select may retain unrelated options after the required ordered set. Never rewrite the built-in `Status` field merely to obtain these semantics: choose `Pegasus Delivery status` when its current options/assignments cannot be preserved exactly. All rules below refer to the selected logical field regardless of its physical name. Record actual names, node IDs, option IDs/order and any preserved extras in the change record and `docs/operations.md`.

### Card reconciliation

End with exactly one unarchived keyed Project item for each of all 229 IDs: 200 planned delivery items and 29 permanent-boundary items. A boundary item is always a draft and cannot be converted while its canonical row remains `Not planned`. A planned item is a draft unless an existing repository issue/PR is already bound to that one ID through an accepted issue/change record. In that activated case, add/reuse the issue/PR item, preserve its repository title/body, set canonical Horizon/Target release, and derive Delivery status only from recorded lifecycle evidence: accepted-not-started=`Ready`, `in_progress`=`In progress`, exact-head review=`In review`, and merged/accepted completion=`Done`. Ambiguous, conflicting or multi-ID per-capability activation blocks mutation. Existing QDOS issue #3 remains the unkeyed release owner referenced by all 128 `Now` drafts; it does not substitute for or duplicate any keyed capability item.

De-duplicate first by the validated logical Capability ID field, then by a validated title matching `^\[[A-Z]+-\d+\]`. Prefer one valid activated issue/PR over drafts. If only drafts exist, retain earliest `createdAt`; lexical node ID breaks an exact timestamp tie. Archive—never delete—only extra keyed drafts; retain exactly one keyed boundary draft for each permanent boundary. Never archive, close or rewrite an issue/PR automatically; duplicate issue/PR bindings are a stop condition. Do not modify unrelated unkeyed drafts, issues or PRs.

For each retained/new **draft** only:

Title: `[{ID}] {durable outcome}`. If over 240 characters, retain the first 239 and append `…`; the full outcome remains in the body.

Body:

```markdown
Planning item for `{ID}`; this is not implementation or acceptance evidence.

- **Durable outcome:** {full durable outcome}
- **Canonical owner:** {absolute immutable GitHub link resolved from the row owner at AllocationInputSha}
- **Horizon:** `{Now|Next|Later|Not planned}`
- **Target release:** `{exact version|unallocated}`
- **Activation/boundary:** {full activation/boundary text}
- **Delivery owner:** {`#3 covers this capability in the active QDOS alpha` for Now; `Not activated — convert this draft through one accepted issue/change record before implementation` for Next/Later; `Permanent boundary — no activation issue or implementation until canonical product authority changes this row` for Not planned}

Canonical inventory: {absolute immutable GitHub link to AllocationInputSha/docs/capabilities.md}
```

Field rules for unactivated drafts:

- `Now`: Delivery status `In progress`, Horizon `Now`, Target release `0.1.0-alpha.1`. Here `In progress` means included in active alpha scope, not implemented.
- `Next`: Delivery status `Triage`, Horizon `Next`, exact `0.2.0`–`0.4.0` target.
- `Later`: Delivery status `Triage`, Horizon `Later`, exact `0.5.0`–`1.4.0` target.
- `Not planned`: Delivery status `Not planned`, Horizon `Not planned`, Target release `unallocated`; it has no milestone and cannot be converted to an issue while canonical authority is unchanged.
- Do not infer Priority. Leave it unset on new drafts; preserve and report an explicit existing Priority on a matched item.

Use a temporary PowerShell sync script outside the repository. Snapshot and validate `AllocationInputSha`, the exact pushed repository head containing allocations but not external-write evidence, before mutation. Writes are sequential and idempotent; after each create, checkpoint only the returned item ID in ignored temporary state. On retry, re-read by Capability ID before creating. Retry only returned transient/rate-limit failures using the server delay. Fail closed on schema, permission or content mismatch. Commit no Project export, sync database or status ledger.

### Saved release-sequence view

After API readback, use the authenticated Project UI to create or reconcile one saved board view named `Release sequence`. Its column field is the selected logical Target release; columns follow the exact 12-option order in section 3. Apply `has:"<physical Capability ID field name>"` and exclude `Horizon=Not planned` so the view contains exactly the 200 keyed planned-capability items; display Capability ID, Horizon, Delivery status and Priority. Preserve unrelated views. If a same-named view has incompatible semantics, leave it untouched and create `Pegasus release sequence`; if both names conflict, stop for resolution rather than overwrite.

This view is an ordered dependency-qualified release sequence, not a calendar forecast. Do not add iteration/date fields or milestone due dates merely to make a roadmap layout. Also create or reconcile a saved table view named `Permanent boundaries`, filtered to keyed `Horizon=Not planned` items and showing Capability ID, outcome/title, Horizon, Target release and Delivery status. Preserve or safely fall back on name conflicts using the same rule. Verify all 12 release columns, four planned samples and the `ACC-12` boundary sample visually after saving both views.

## 7. Publish and verify

After repository allocation/policy checks pass, commit and push the child branch, record that commit as `AllocationInputSha`, and open **PR 2** as a draft titled `Allocate planned capabilities to exact releases`; **PR 1** is the documentation-centralization prerequisite. Base it on the documentation prerequisite branch if stacked, otherwise `main`. Perform milestone/Project/view writes only from that immutable validated input; read everything back and record exact field/item/view/milestone IDs, targets, counts and results in the same change record. Then commit/push only that evidence update, rerun checks, mark the PR ready and obtain independent exact-base/exact-head review. Finish the change record and task issue at `in_review` and set its Project Delivery status to `In review`; never merge from the agent workflow.

Run from the repository root:

```powershell
pwsh -NoProfile -File ./scripts/Test-RepositoryPolicy.ps1
pwsh -NoProfile -File ./scripts/Test-RepositoryLanguage.ps1
dotnet restore ./Pegasus.slnx
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

Required repository result:

- exact horizon counts `128 / 32 / 40 / 29`;
- exact release counts `128 / 8 / 19 / 5 / 5 / 5 / 1 / 12 / 6 / 5 / 3 / 3`;
- 229 unique IDs and keyed Project items, comprising 200 planned allocations and 29 unallocated permanent boundaries;
- valid exact SemVer and exact two-way capability/matrix horizon+target equality;
- no conflict markers, deleted-owner references or planned `unallocated` values;
- no enumeration/read/hash/stat/link/anchor operation beneath the exact `docs/reference/imp-docs/` prefix.

Read back milestones with pagination: each of the 12 required release titles occurs exactly once, is open, has `due_on = null` and the exact description; preserve unrelated milestone titles unchanged. Issue #3 remains on `0.1.0-alpha.1`, and the allocation task is on that milestone.

Read back Project fields/items/views with pagination: 229 unique unarchived keyed IDs across drafts plus any valid activated planned issue/PR items; zero missing/extra/duplicate IDs; exactly 29 boundary drafts and zero boundary issue/PR or milestone bindings; exact field counts (Horizon `128/32/40/29`, Delivery status `In progress=128/Triage=72/Not planned=29`, Target release section-3 counts plus `unallocated=29`); required option order; preserved Priority exceptions; no capability draft milestone; no unrelated-item/view mutation; one saved 12-column release-sequence view filtered to the 200 planned keyed capabilities, and one saved permanent-boundaries view filtered to the 29 boundary drafts. Validate every retained draft body against `AllocationInputSha`. Exercise mappings:

- `ACC-01 -> 0.1.0-alpha.1 -> Now / In progress / issue #3 in body`;
- `INT-28 -> 0.2.0 -> Next / Triage`;
- `CASE-31 -> 1.0.0 -> Later / Triage`;
- `RPT-05 -> 1.1.0 -> Later / Triage`;
- `ACC-12 -> Not planned / unallocated -> one boundary draft, no activation issue or milestone`.

After API readback, visually inspect authenticated Project 3's saved release-sequence view for all 12 ordered columns and the four planned samples; inspect `ACC-12` in the separate 29-item permanent-boundaries view. `ACC-12` must be absent from the release-sequence view and present only as a `Not planned / unallocated` boundary draft. This proves presentation only, not implementation or acceptance. Record `AllocationInputSha`, final evidence head and exact Project/view/field/milestone readback in the change record; leave the ready PR open and report every issue, milestone, Project field/item/view, credential-scope and status write.
