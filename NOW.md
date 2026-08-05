# NOW — updated 2026-08-05

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (one line per live claim)

Claim format: `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by
<agent>)`. Nothing is in flight unless it is claimed here on `origin/dev`.

- Report renderer integration planning: plan the retirement of the
  `workspaces/report-renderer/` source import into the monolith — locate the
  Core render port seam and Infrastructure adapter placement that RPT-01–05
  and EXT-08 would activate through, fold the workspace's own documentation
  into the canonical docs, plan the renderer's .NET 8 → repository-TFM
  uplift, plan the `docs/reference/rendererref1` blueprint and report-template
  intake, plan promotion of the renderer's pre-existing MCP server as the
  replacement for the current `.mcpb` packaging (MCP-01–04 follow-ups), and
  plan removal of any remaining renderer desktop/UI elements. Draft planning
  documents under `docs/temp-plans/` only: no activation, no `Pegasus.slnx`
  change, no caller, no workspace deletion, and no acceptance in this task —
  every integration stays behind the workspace register's activation
  conditions and needs its own ADR and implementation task (branch
  task/report-renderer-integration, taken 2026-08-03, by claude).
- Report renderer workspace uplift: execute the unblocked, operator-decided
  part of the `report-renderer-integration` plan set, all of it inside
  `workspaces/report-renderer/` — remove the WinUI 3 desktop host and its
  `design/assets/report-renderer/gui/` package assets (keeping
  `PreviewComposer` and every template, stylesheet, logo and signature
  asset), upgrade Scriban 5.12.1 → 7.2.6 against composed-HTML parity
  evidence and retire the `NU1901-1904` suppression, uplift the six
  remaining projects to `net10.0` with the package bumps, repair the
  Dockerfile's non-existent `v1.61.0-jammy` base tag, replace `Format.Today`'s
  machine-local `DateTime.Now` with a `TimeProvider`/Europe-London seam, and
  correct `docs/operations.md`'s wrong Windows-only TFM row. Supersedes and
  closes the planning claim above. No relocation into `src/`, no
  `Pegasus.slnx` change, no Core port, no caller, no MCP consolidation, no
  template work and no capability advanced — those stay blocked on the
  operator questions the plan set records (branch
  task/report-renderer-workspace-uplift, taken 2026-08-05, by claude).
- UI implementation programme, deployment and live verification: `main` now
  carries the whole rebuild. The eight page PRs (338, 339, 341, 343, 344,
  345, 346, 347) merged into `dev`, and `dev` merged into `main` as PR 348 on
  2026-08-05 under the operator's `MERGE AUTH GRANTED`. All ninety merge
  conflicts were add/add on documents — `main` held a direct push of
  `docs/ui-work/` and two `docs/temp-plans/` files, `dev` held the same files
  with later edits — and every one resolved to `dev`'s copy, leaving the
  merged tree byte-for-byte `origin/dev`. No source file conflicted.
  Verified before the merge: Release build clean, Core 441/441, architecture
  73/73, integration 399 passed with 16 skipped, QDOS pressure 3/3,
  documentation links across 208 files. Browser-verified 2026-08-05 against a
  local instance of the merged tree: Dashboard, Inbox, Upload, Queues, Cases,
  Administration and its Accounts/Mailboxes/Automation sub-pages, and the
  standalone 404 surface all render in the refreshed style with zero console
  errors and zero CSP violations, and the Upload form posts and shows its
  real validation error.

  **It is still not deployed**, and nothing on the production estate has been
  touched. The deployment needs the operator's approval for the exact Azure
  targets — subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, resource
  group `rg-pegasus-prod` — before any image push, revision activation, or
  migration. `docs/ui-work/` stays until that release is live-verified,
  because it is the specification the deployed pages are checked against
  (taken 2026-08-05, by claude).
- CASE-27 edit-lease continuity and conflict recovery for both callers
  (MCP-02/MCP-04): close the gaps between
  `docs/requirements.md` "Case edit authority and recovery" and shipped
  behaviour — expired leases must read as free everywhere they are projected
  (Triage and Operations still narrate a past expiry as held), authorised
  non-holders must see the holder and when edit authority frees, a rejected
  editor must keep their proposed values for comparison instead of losing the
  post to a bare redirect, the Automation Actor must be able to renew rather
  than only begin/end, and the triplicated mutation guard collapses to one
  Core-owned implementation with a single lease-token length contract. Staff
  Web and the Automation Actor exercise the same guard and the same
  reacquisition path; no takeover, no force-save, no Administrator bypass, no
  lease vocabulary in operator copy (branch task/case-edit-lease-continuity,
  taken 2026-08-05, by claude).

## Merged, not deployed

Everything below is in both `dev` and `main` as of PR 348 (2026-08-05).
`main` being the deployment branch is not deployment: no release has been
built or activated from it, so nothing here runs anywhere.

- **The whole UI implementation programme** — the refreshed shell and design
  system, Dashboard, Inbox, Upload, Queues, Cases and Administration, with
  the Core and Infrastructure changes each page needed. Local verification
  and browser checks are recorded in the `Doing` claim above. **It is not
  deployed.** The live estate still serves release 5 (revision `c6571f7…`),
  so no operator has seen any of this.
- **AI-09 Send to AI round trip and the Automation Actor assessment toolset
  (MCP-06)** merged into `dev` 2026-08-05 (PR 332, merge `5555440`), all nine
  checks green, and carried into `main` by PR 348. **It is not deployed**, no
  environment runs it, and no activation, navigation-link, or acceptance
  claim is made. The whole surface is composition-gated off by default
  (`Features:SendToAi`, `Features:AutomationMcp`) and DevelopmentOffline-only;
  production would additionally need a non-preview transport decision.
  Evidence is tier 2–4 local only — the tier-5 external-client round trip is
  still queued below.

## Next (ordered queue — take from the top)

- Send to AI work-request integrity (PR 332 review): the reasoned `Cancelled`
  outcome the plan requires has no caller at all — `ICancelAiWorkRequest` is
  registered and unit-tested but nothing in the application invokes it, so
  add the reason-taking action and the visible cancelled state. With it, the
  outcomes the transport cannot currently tell apart: a lost or malformed
  `/send` response is recorded as a definite `Failed` ("Nothing was sent")
  and retry mints a fresh request id, so one case can be forwarded twice;
  a `/events` transport failure is indistinguishable from an empty reply and
  the panel states "No reply has been recorded yet" — both need a typed
  uncertain/unavailable outcome on the Core port rather than a false
  business-facing claim. Also: replay is not attempted before the in-flight
  guard rejects; the in-flight check and the insert are not one transaction,
  so two sends with different operation keys can both reach `/send`; a
  timed-out request is stepped over rather than transitioned to `Expired`,
  leaving it permanently non-terminal; and reconcile expires on wall-clock
  without first reading whether the reply arrived in time.
- Record the Send to AI eligibility decision (PR 332 review): the shipped
  gate admits every `NotReady`/`Review`/`ReportPreparation` case with no
  readiness condition, which silently resolves the channels plan's open
  decision 5 — which outstanding requirements block the panel — in the most
  permissive direction. The rewritten `open-decisions.md` section no longer
  carries the question. The permissive default may well be right; it needs
  recording as a decision rather than as an implementation detail.
- Automation round-trip evidence view (PR 332 review): filtering
  `/Administration/Automation/Activity` by a work-request id does not show
  the round trip the plan promised. The lifecycle rows (created, handed off,
  completed, failed) carry the sending staff actor while the activity
  projection filters strictly on `ActorKind.Automation`, so only the later
  ingress rows appear; and the `case_assessment_saved` row that holds the
  per-field before/after evidence is correlated by operation key even when a
  request id is bound. Extend the projection to the `ai_work_request`
  aggregate and correlate the detailed row by the bound request id.
- Assessment toolset correctness follow-ups (PR 332 review): replay returns a
  fresh current projection with no `IsReplay` marker, so a retry after an
  intervening save reports the later fields — the toolset plan specifies the
  original result plus a replay signal. `MapCaseOwned` falls back to
  `CaseDataCodes.Suggestion`, so an unconfirmed extraction can be served by
  `pegasus_assessment_get` as accepted case data and satisfy readiness.
  `pegasus_case_update_details` and `pegasus_eva_bundle_generate` accept any
  well-formed `workRequestId` as an audit correlation without the existence
  and case-ownership check `pegasus_assessment_update` applies. A partial
  `pegasus_case_update_details` edit re-stamps every omitted field's
  confirmation metadata with the automation actor, because the merge reads
  the current confirmed values and writes the whole record back. Required-when
  pairings are validated only when the governing field is in the save, so
  clearing the dependent value alone persists an invalid merged state.
  Estimate prices and work units accept decimals wider than the mapped
  `decimal(18,2)`/`decimal(9,1)`, reaching a database overflow instead of a
  deterministic refusal. Estimate-line results expose only actor kind and a
  confirmation boolean, dropping the `RecordedBy`/`ConfirmedBy` provenance
  the record retains and scalar fields return.
- **UI implementation programme** (operator decision 2026-08-04, settling the
  earlier "decide what `docs/ui-work/` is for" question): the folder is
  adopted as the specification for a whole-application UI rebuild, then
  deleted. Every page folder's alteration plan is implemented, every entry in
  `docs/ui-work/additions-hidden-features.md` and
  `docs/ui-work/defects-and-non-functional.md` is made visible and functional,
  `docs/ui-work/ui-standards-and-review.md` becomes the enforced presentation
  contract, and the implemented pages match the **refreshed** mockups. The
  work lands as one PR per main-navigation page — sub-pages fold into the
  navigation page that owns them — reviewed and merged into `dev` in
  sequence, then `dev` into `main`.

  **All eight page PRs are merged into `dev`** (338 shell and design system,
  339 Dashboard, 341 the acceptance gate and INT-25, 343 Inbox, 344 Upload,
  345 Queues, 346 Cases, 347 Administration), **and `dev` is merged into
  `main`** as PR 348 on 2026-08-05. Nothing is deployed. What remains is the
  operator's to authorise — see the `Doing` claim above — and three decisions
  the work could not take for itself:
  - **Two provenance glyphs.** The Lucide sprite is a checksummed sixteen-glyph
    asset and `design/README.md` records that no glyph was added, removed or
    redrawn. Seven provenance words share those sixteen, so **E-mail** and
    **AI** lean on their tooltip rather than a distinct envelope and spark.
    Adding two glyphs re-checksums an approved asset and needs authorisation.
  - **The `claudeuiverification` password is committed** to
    `appsettings.json`, Production-profile only, at the operator's request and
    on their stated risk assessment. Replace the block with
    `{ "Removed": "claudeuiverification" }` to delete the account on next
    start. It must not survive into a real production deployment.
  - **Two things were not shipped rather than faked.** The Dashboard's
    "Queries outstanding" tile needs a reply link that hangs off a Triage
    record no composition can create (defect B2), so **Blocked** takes its
    slot; and Automation activity's failure reasons need a stable writer-side
    reason code before the UI can label them, which the register itself says.
- Protect `main` against direct pushes. `440ab5c` reached the deployment
  branch without a PR, so CI never gated it — it carried a defect that
  failed the documentation check the moment it was put in front of CI (fixed
  in PR 335). PR 324 was likewise opened against `main` before being
  redirected. It cost again at PR 348: the same files existing independently
  on both branches produced ninety add/add conflicts in a merge that had no
  source conflict at all. A branch-protection rule stops this being caught by
  audit.

- Record releases 4 and 5 in operations.md deployed evidence: release 4
  (2026-08-04, revision `8e34078…`, digest `sha256:ae2cc7b8…`, the four
  2026-08-03 migrations applied with the runtime-role matrix re-verified,
  ADR-0020 premise verified — zero accepted cases, CaseMatchIndex shipped
  empty) surfaced the production-CSP blank-band defect; release 5
  (2026-08-04, revision `c6571f7…`, digest `sha256:29d4fcff…`, no new
  migrations) shipped the PR 333 hotfix — live-verified: all 21
  authenticated routes render from the viewport top with zero inline
  styles, zero console errors, zero exceptions/sev3+ traces, smoke passed.
  Also record the 2026-08-04 read-only Box custody-root inventory: the
  pegasus folder `405543781910` has zero children, so no legacy
  `{reference}-{caseId}` folders exist and the Case/PO fail-closed gate is
  satisfied (release-5 live checks, 2026-08-04).
- Assemble the operator-reviewed extraction cohort + untouched holdout and
  accept the per-field thresholds (INT-21, open-decisions) — blocks Path
  step 3.
- Investigate the one failed staged intake artifact
  (`staging/f7d2bdb8…`, 10,378,983 bytes, first seen 2026-08-01 23:56Z):
  it remained `Failed` after the 2026-08-04 Worker re-enable and two
  reconciliation sweeps — decide staff-review disposition. Context: the
  operator ordered all nine Worker functions re-enabled on 2026-08-04
  (they had carried `AzureWebJobs.<name>.Disabled = true` since
  2026-08-03 ~01:08, set under the shared identity during the live
  vault-consolidation window; releases 4 and 5 preserved the flags).
  Re-enable verified live: six timer/dispatch functions executed with
  zero failures and zero exceptions, the Inbox poll succeeded at
  2026-08-04 08:41:45Z, and the one waiting inbox email processed into
  `Needs sorting`; the three queue/poison functions correctly idle with
  no messages. Alert rules and the operations action group were checked
  enabled; nothing else in `rg-pegasus-prod` was disabled
  (worker re-enable live checks, 2026-08-04).
- Decide the production-CSP strategy for the inline `<script>` blocks
  (`_FreshnessBanner` refresh feedback, `_ReasonDialog` focus trap, the
  unrouted Assessment artifacts): external script files versus CSP hashes.
  They are silently discarded by the deployed `default-src 'self'` policy
  today — progressive enhancement only, nothing functional breaks
  (release-4 live checks, 2026-08-04).
- Operator decision: the pre-scrub commits of task/qdos-email-classification
  still carry corpus-derived names/references in GitHub PR refs — decide
  whether to request a history purge; and decide handling of the real staff
  addresses and case data in the operator-supplied
  docs/reference/workproviders-and-repairers files
  (task/qdos-email-classification review, 2026-08-03).
- UI polish follow-ups from the design-pass review: Send-confirm focus drop,
  focus-trap escape edge case, sparkle glyph clipping, and the freshness
  banner's London label falling back to a UTC value without IANA data
  (task/ui-alpha-design-pass review, 2026-08-03); also the Access review
  page renders the `0001-01-01 00:00:00Z` sentinel as "Last reviewed"
  beside a `Recorded` state (release-5 live checks, 2026-08-04).
- Remove the manual case-acceptance gate and the `DraftReady` decision, and
  implement INT-25/CAP-008. `docs/requirements.md:251` requires that
  "definitive authorised intake creates exactly one instructed Case
  idempotently" and that "the allocation decision adds no universal manual
  acceptance gate"; `docs/operator-notes.md:204` sends only ambiguous
  provider, instruction-type, or case evidence — and any unidentified e-mail
  — to `Needs sorting`. Shipped behaviour is the opposite: `IAcceptIntake`
  has one caller, the staff form handler at
  `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs:534`, so every case in
  Pegasus waits on a human pressing "Accept and allocate case reference",
  and `IntakeDecision.DraftReady` exists only to name that wait. Definitive
  intake must allocate at processing time, entering `Not ready` when
  ordinary detail is thin; incomplete detail is not a bar to allocation.
  `Needs sorting` keeps the ambiguity path, including staff-resolved manual
  creation (INT-26); genuine fail-closed conditions (limits, principal
  identity, standalone Audit evidence) stay `Blocked intake` with a reason.
  Scope: Core policy and the acceptance/receipt stores, the Worker/automation
  path, the intake and dashboard surfaces, and a repo-wide correction pass —
  `DraftReady` is referenced across `src/`, `tests/`, `design/README.md:374`,
  `docs/capabilities.md`, and the intake filter/route token `draft_ready`.
  Also correct `EfIntakeReceiptStore.GetCountsAsync:152-164` and
  `ListAsync:166-192`, which never exclude receipts that produced a case, so
  every intake count is cumulative for all time. `Review` and "Ready to
  review" are the Case stage before the report is with an Engineer
  (`CaseWorkflowContracts.cs:15`, `requirements.md:295`) and must never
  label an intake state (operator decision 2026-08-04, superseding the
  2026-08-03 "ship as-is, relabel later" line — the tile was never a
  labelling defect).
- Prove the per-run template database's server-side BACKUP/RESTORE against a
  PEGASUS_TEST_SQL_DATASOURCE container (Linux workstation or a CI job) and
  lift the review gate that disables the template for external servers
  (task/repository-check-speed review, 2026-08-03).
- After task/repository-check-speed merges, observe the hardened
  abandoned-database sweep on a shared LocalDB instance: every sweep failure
  is now swallowed, so confirm abandoned Pegasus_Test_* databases and .bak
  files still get reclaimed rather than accumulating
  (task/repository-check-speed review, 2026-08-03).
- Record the MCP Automation Actor tier-5 evidence: a real external client
  (Claude Code over a bearer token) against the locally enabled `/mcp`
  surface, evidence recorded per operations.md, before any activation claim
  (task/mcp-automation-actor review, 2026-08-03).
- Promote the settled Automation Actor identity/authentication/tool-inventory
  contract to an ADR — with the temp plan deleted it is owned only by
  architecture.md/operations.md prose
  (task/mcp-automation-actor review, 2026-08-03).

## Waiting (each line names its unblock condition)

- Obsolete predecessor vault purge — five soft-deleted `uksouth` vaults on two
  platform-scheduled dates: `cespk-pg-kv-dev`, `cespkevakvufa3ci`, and
  `cespklockva7tzj2` on 2026-08-09, then the two consolidation predecessors
  `cespkboxkvv76a47` and `cespkenrichkvgi62sd` on 2026-08-10 (verified
  read-only 2026-08-04; the earlier single 2026-08-09 date covered only the
  first three). No action unless a purge fails; the watch is not clear until
  both dates pass.

## Path (decided 2026-08-02: full QDOS cutover — every new QDOS instruction is worked in Pegasus through to the EVA handoff; EVA keeps engineering and reports. Box custody root decided 2026-08-02: all case folders under the pegasus folder `405543781910` only.)

1. Green `main` through a PR with a passing `repository-check` run.
2. Prove the spine on one genuine QDOS email in production: mailbox intake → custody → extraction draft → principal → Case/PO minted → Box folder (INT-02/08/09/19/22/25, CASE-07, DOC-01/02) — needs the composition fix deployed.
3. Accept extraction thresholds from the reviewed cohort + holdout (INT-21); zero false case creation.
4. Production document content store live (DOC-02), then staff review path live: completeness gates and Review/Not ready/Held queues (CASE-13/14/15/16, UI-02/08).
5. EVA bundle from a real case: exact 13-key JSON + images + SHA-256 manifest (EXT-03), the `First sent to Engineer` proxy event (CASE-21), operator accepts every field mapping via a real drag-and-drop run.
6. Chasing live: due-by, 7-day chase schedule, copyable chasers (CASE-17/18, MAIL-18).
7. Web telemetry exporter (OPS-07) and minimum cutover alerts (Box custody failure, intake poison, chaser sweep), then the cutover date: all new QDOS instructions enter Pegasus; watch alerts and telemetry daily for the first week.
8. Record operator acceptance and management approval (OPS-23, OPS-25) — this is what closes `0.1.0-alpha.1`.

Explicitly NOT on the path (allocated but non-blocking): MCP-01–04, INT-17 VRM reading, INT-31 upload links, the EVAL evaluator cluster, live DVLA/DVSA adapters (approved replay/`Unavailable` is fine), MAIL-14/16 report-sent detection (post-report tracking starts manual via MAIL-15), and OPS-09 recovery proof (removed as a release gate 2026-08-03).

---

Roadmap: [docs/capabilities.md](docs/capabilities.md) · Questions: [docs/open-decisions.md](docs/open-decisions.md) · How-to: [docs/operations.md](docs/operations.md)

Rules: the claimable unit is a task line — goal text first, capability IDs
when they apply, several small features may share one line; one task = one
worktree = one PR. The authoritative copy of this file is the one on
`origin/dev` after a fetch. Claiming, releasing, abandoning, and stale-claim
removal happen through the task workflow owned by
[engineering](docs/engineering.md#task-workflow); claim and maintenance
pushes to `dev` touch only this file and `docs/temp-plans/` deletions.
Staleness ladder, removable by anyone: a claim whose `task/<slug>` branch was
never pushed within 48 hours; a `Doing` line older than 14 days with no
branch activity; an orphaned `docs/temp-plans/` file with no matching `Doing`
line. The date above is bumped whenever this file is touched. No other file
may contain a "current status" or "next steps" section; transient task plans
under `docs/temp-plans/` are the one exception.
