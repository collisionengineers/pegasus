# Post-implementation report — DELIV-012 (release 12)

**Status: in progress.** Sections 1–5 are final; the release-execution and
verification sections are appended when the deployment completes. Written as
the work happened, not reconstructed afterwards — the running evidence is in
`scratch/notes` and the per-PR reviews in `scratch/review`.

## 1. What the quality review found (research phase)

Three research documents (`research/current-estate.md`, `research/codebase-evidence.md`,
`research/recent-tickets.md`) established, with every fact tagged verified or
assumed:

- Production serves release 10 (`d8de29cb`, 2026-08-18); no deployment since.
  The held release 11 left no trace in Azure.
- `dev` was 42 commits ahead with 3 pending migrations; 5 open PRs (4 owned by
  another agent) with red or absent CI, 34 unaddressed reviewer comments, and
  two protected-document edits needing operator confirmation.
- **Five hard defects that would have broken production or the release:**
  1. `CaseRepairSpecifications` created with no runtime GRANT while the live
     Web assessment-save path writes it (TICK-093, merged).
  2. The same GRANT-omission class in four more migrations across the open PRs.
  3. `EvaHandoffDownloadOperations` with **zero permission rows in production
     right now** — the EVA download path broken in the deployed release 10
     (verified against `sys.database_permissions`).
  4. The release gate itself (`Test-AzureDeploymentPlan -Mode Local`) **failing
     on clean `dev`** — a grant-carrying migration unaccounted for in the
     bootstrap census. Release 12 could not have run at all.
  5. Three surfaces shipped dark: the report renderer (no caller, no Chromium
     in the image), `MailOperationalDestinationPolicy` (tests only, against its
     own ticket's recorded operator ruling), `IRepairSpecificationStore` (not
     in DI).
- One pre-existing intermittent concurrency defect in parallel case allocation
  (deadlock/assertion, two symptoms) — filed as [[CASE-005]], evidenced on
  clean `dev` before any release-12 branch existed.
- No overwritten work found in the merges since release 10 (line-by-line
  presence check of every merged PR's additions against `dev`).
- Board contradictions: TICK-011 cited two unreachable commits and claimed
  `not-deployed` for shipped code; PLAT-001 lacked its `production` field.

## 2. Operator decisions obtained (all recorded verbatim in `open-questions/`)

Q1 take over the four INTK PRs and finish them · Q2 the image-intake two-branch
ruling (match → evidence on the existing Case; no match → Image-initiated Case)
· Q3 Unidentified replaces Needs sorting, invariant updated · Q4 Sent-evidence
polling approved for the mailbox · Q5 make all three dark surfaces live ·
Q7 repair costs are imported (Audatex/Glass's, AI via MCP, drag-and-drop) — no
fabrication; filed as [[ENG-002]] · Q8 Web container raised to 1.0 vCPU / 2 GiB.

## 3. Remediation delivered (nine PRs, all independently reviewed, all merged)

| PR | Delivered |
|---|---|
| #426 | Release gate repaired; `CaseRepairSpecifications` grant; new migration `20260819180000_GrantEvaHandoffDownloadOperations` fixing the live production defect; `scripts/Test-MigrationGrants.ps1` guard; `-Mode Local` added to the always-on `changes` CI job (verified executing, run `32263089802`); current-state doc drift fixed |
| #425 | `IRepairSpecificationStore` registered and given a production caller; single owner for the draft/accepted queries; proven on LocalDB 7/7 |
| #422 | TICK-045 rebuilt: real classifier driven from DI (falsifiability demonstrated by break/revert), fabricated mailbox removed, `MailOperationalDestinationPolicy` wired into `/Inbox/{id}` — the caller TICK-044's operator ruling demanded |
| #427 | Chromium-capable Web image via `ContainerBaseImage` (single-sourced Playwright version), `oras` layer evidence, renderer 6/6 against real Chromium; container raised to 1.0 vCPU / 2 GiB with the deployment-plan assertion moved with it |
| #416 | INTK-005: ordinal-0 token fix, one-member-group redirect, grants + census, batch limit, auto-refresh, retry reuse, duplicate-notice gap found beyond the brief |
| #417 | INTK-006: group decision now binds the per-member path (ambiguity can no longer associate), token-shape bug that would hang every multi-file group found and fixed, recognition runs once per member, FRD decision table written |
| #423 | INTK-008: lifecycle wired for manual links, validators enforced, dead custody seam removed **and** its protected-doc sentence with it, backfill corrected, operator two-branch ruling written verbatim into operator-notes/PRD/FRD |
| #424 | INTK-007: Unidentified queue with `U<n>` references; retryable failures no longer burn references; backfill fingerprint fixed; all 14 reviewer comments dispositioned; the `Needs sorting` → `Unidentified` replacement completed across code, labels, protected docs and the AGENTS.md invariant; least-privilege grants trimmed to named callers |
| #428 | Report-draft operator entry point: Core projection with enumerated readiness, custody-confirmed Photos/Sources, design-compliant panel; honestly disabled pending estimate import (ENG-002) |

Cross-branch defects caught only because the merges were sequenced and
re-verified: INTK-005's migration id silently eaten by a clean merge (restored;
every later merge then diffed the list against the folder — final check 53/53);
INTK-006's member-lookup incompatible with INTK-005's token fix (would have
hung every multi-file group); TICK-045's switch arm referencing INTK-007's
pre-rename enum (would not have compiled); the 423×424 FRD semantic conflict
merged by hand so both operator-confirmed vocabularies survived.

## 4. Merge order executed

#426 → #425 → #422 → #427 → #416 → #417 → #423 → #428 → #424 (last, owning the
vocabulary migration). Both release gates re-verified green on `dev` after each
merge. `dev` history: every first-parent commit since release 10 is a PR merge;
`main` remains a strict ancestor throughout.

## 5. Git hygiene

Fifteen branches (remote and local) deleted after per-branch verification of
`0` commits ahead of `origin/dev`; thirteen worktrees removed after per-worktree
clean checks; one stray review branch (`pr417check`) removed; `.worktrees/kanmer`
and the board branch never touched. Final state is recorded in the proof.

## 6. Release execution

*(appended after the deployment)*

## 7. Verification

*(appended after the deployment: browser evidence for every shipped UI change,
endpoint/CLI evidence for every backend change, migration head, worker census,
Sent-evidence exception stream stopped)*

---

## 6. Release execution (final; full transcript identities in `proof/`)

Operator authorisations obtained through the question tool immediately before
E3: **MERGE AUTH GRANTED** for `ed3be51c95bc2a055606e5210131d37de9de2dd1`, and
all five Azure writes approved for their exact targets.

| Step | Result |
|---|---|
| E1 | PR #410 lane set **11/11 SUCCESS** on the promotion head |
| E3 | Atomic fast-forward `d8de29cb..ed3be51c`; readback `main == dev == ed3be51c` |
| E4 | Main-push run `32309456172` concluded **success**; the "Require main history to be contained in dev" guard passed |
| E5 | Artifacts at the exact SHA; digest `sha256:6dcf3ca1…`; manifest SHA-256 `86360226…`; Local + Artifact + PreUpload + PreMigration + PreProvision all pass |
| E8 | Image pushed; ACR digest readback equals the manifest digest |
| E9 | `efbundle` applied **all 8 migrations**; head readback `20260819180000_GrantEvaHandoffDownloadOperations`; both grant fixes verified in `sys.database_permissions` |
| E10 | Bootstrap census: **496 catalogued permission/denial rows verified** |
| E12 | Provision preview diffed against release 10's stored preview: **byte-identical except the revision suffix** — stop condition satisfied with evidence |
| E13 | `azd provision` success (77 s); revision `--ed3be51c95bc`, image digest-pinned, **1.0 vCPU / 2 GiB**, Healthy, 100 % traffic |
| E14 | Worker config-zip: new active deployment `4ac36bca` (22:44:24–22:45:32Z, success); release-10 deployment inactive |
| E15 | `Invoke-ProductionSmoke.ps1` **exit 0** (version+SHA match, https 302, nine-function census `approved-live-worker`) |
| W5 | Sent-evidence approval applied through `/Administration/Mailboxes` as a signed-in administrator (version 3→4, Sent folder bound, reason recorded) |

Route facts surfaced and recorded in the runbook: `azd env get-value` for a
nonexistent key returns the CLI update-notice text; the efbundle
`Box__ConfigJson` placeholder must parse as Box JWT JSON.

## 7. Verification (final)

**Backend, all against production:**
- `/diagnostics/version` → `{"version":"0.1.0-alpha.1","sourceSha":"ed3be51c…"}`; `/health/live` and `/health/ready` 200.
- Migration head and the two repaired grant sets read back from `sys.database_permissions` — including `EvaHandoffDownloadOperations`, which had zero permission rows before this release.
- **The Q4 defect is gone**: `ApprovedSentPollStates.LastCompletedAtUtc` advanced to `2026-08-19T22:52:15Z` (previously stuck at 7 Aug) and `LastFailureCode` cleared (was `sent_mailbox_not_approved`); the inbox poll advances on the new Worker (`22:52:45Z`). This is direct-state evidence, deliberately used instead of App Insights because the Log Analytics daily cap makes telemetry blind at this hour.

**Browser (signed in to production as the seeded verification account):**
- Dashboard at 1920: centred shell, the new **Unidentified** nav entry, and an E-mail activity card counting **5 open Unidentified items**.
- `/Upload`: the redesigned centred page now reading "Drag files here, or choose them" with "up to 20 files per submission" — PLAT-006 + INTK-005 live.
- `/Unidentified`: **U1–U5 allocated by the backfill from real retained receipts** (12–19 Aug), each carrying the "No usable identification" reason and its receipt identity — the operator-visible proof the reference allocator and queue work against real data.
- Case assessment page: the **Report draft** panel present and honestly fail-closed, enumerating 22 outstanding readiness items including "Repair cost figures".
- `/Inbox/{id}` on a real classified e-mail: **"Operational destination: Receiving work — Destination policy: mail_operational_destination version 1"** — the formerly dark MAIL-02 policy computing live.
- `/Administration/Mailboxes`: both route scopes checked and the Sent folder identity bound for the approved mailbox.

Screenshots in the session scratchpad: `release12-dashboard-1920.png`,
`release12-upload-1920.png`, `release12-unidentified-1920.png`,
`release12-assessment-reportdraft.png`, `release12-inbox-destination.png`,
`release12-mailboxes-sentevidence.png`.

**Docs refresh:** PR #429 (docs-only), validated by `Test-DocumentationLinks`
(205 files); merges on green `documentation` lane.

## Simplification pass — 2026-08-19

Run per constituent PR over each PR's own diff before its review (dispositions
recorded in the owning tickets and in `scratch/review`); this orchestration
ticket's own repository diff is the docs refresh, for which the pass is
n/a — docs-only.
