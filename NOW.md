# NOW — updated 2026-08-11

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (one line per live claim)

Claim format: `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by
<agent>)`. Nothing is in flight unless it is claimed here on `origin/dev`.

- Accept stable ApprovedMailbox identity and fresh-baseline ADR (branch task/stable-mailbox-identity-adr, taken 2026-08-10, by agent e7e61e75-9ffa-41a1-869a-8b3fb1e55f13)
## Merged, not deployed

The estate serves **release 8** (2026-08-07, revision `ded44fd7…`, image
`sha256:c993eb0e…`, Web revision `pegasus-prod-web-252ow37gij--ded44fd7be0a`),
which carries every source change in `dev` and `main`. PRs 342, 356 and 357 are
deployed; PR 340 is `workspaces/` source no application build compiles. Its
three migrations were applied explicitly before activation and verified against
`__EFMigrationsHistory`. Smoke passed: health, exact version and source-SHA, and
the anonymous `/Cases` redirect to the https sign-in route. The
deployed-evidence record is owned by
[operations § Production environment](docs/operations.md#production-environment).

**Nothing here is live-verified beyond smoke.** No browser journey has exercised
the upload-to-case path, the Inbox, or CASE-27 edit authority against the
deployed estate, and UI-10 is not claimed as accepted. Release 6 is the standing
warning: live verification found six defects local testing could not, because a
count query and a rendered time cannot be proved locally.

Two things are deployed as code without being active, and neither is a
release claim:

- **AI-09 Send to AI round trip and the Automation Actor assessment toolset
  (MCP-06)** (PR 332, merge `5555440`) reached production in release 6, but
  the whole surface is composition-gated off by default
  (`Features:SendToAi`, `Features:AutomationMcp`) and DevelopmentOffline-only,
  so no environment runs it and no activation, navigation-link, or acceptance
  claim is made. Production would additionally need a non-preview transport
  decision. Evidence is tier 2–4 local only — the tier-5 external-client
  round trip is still queued below.
- **`claudeuiverification`**, an enabled Administrator seeded into production
  by release 6 from a credential committed to `appsettings.json`, at the
  operator's request and on their stated risk assessment. **It must be
  removed before go-live**; replacing the `Bootstrap:VerificationAccount`
  block with `{ "Removed": "claudeuiverification" }` deletes it on next
  start.

## Next (ordered queue — take from the top)

- **`Invoke-AzureDatabaseBootstrap.ps1` cannot pass after release 6, so the
  runtime-role effective-permission check did not complete for release 8.** Its
  expected matrix is built from `20260729199000_RuntimeRoleReconciliation`
  alone, so every grant a later migration adds reads as unapproved drift. At
  release 8 all 24 differences were `=>` — extra in the database, none missing
  — and each traces to a reviewed migration (release 6's `AiWorkRequests`,
  `SendToAiControl`, `CaseAssessmentFields`, `CaseEstimateLines`; release 8's
  `RetainedMailboxMessages` and `RetainedMailboxAttachments`, granted at
  `20260805223036_RetainedMailboxMessages:136-145`). The principal creation and
  effective-permission guards ran before the assertion, so the estate is not
  unverified in that respect, but the matrix comparison and everything after it
  were skipped. Build the expected matrix from the full migration set, then run
  the script against production to close the gap this release left open
  (release 8, 2026-08-07).
- Reconcile the local `azd` environment with the estate, or stop trusting it.
  Release 8's provision failed because `.azure/pegasus-prod/.env` still pointed
  the Box secret references at `cespkboxkvv76a47`, a vault soft-deleted on
  2026-08-03 during consolidation — two days before release 7 deployed
  successfully from the same environment. Its recorded image digest and revision
  suffix were still release 3's. The running Container App held the truth: the
  secret versions were unchanged and only the vault host had moved to
  `pegasusprodkv252ow37g`. Either the release route reads the deployed resource
  rather than the local environment, or the environment is refreshed and checked
  as a release step. Also note `cespkboxkvv76a47` and `cespkenrichkvgi62sd` are
  scheduled to purge 2026-08-10 (release 8, 2026-08-07).

- Reduce contention in the intake write path, or decide it does not matter.
  Operator decision 2026-08-07: the `qdos-pressure` write budget was
  re-baselined from 3s to 20s rather than chase this now, because the 3s
  number was written when an upload staged the bytes and returned (~100ms) and
  an upload now extracts, evaluates and allocates the case before answering.
  Under the gate's eight simultaneous staff those run as serializable
  transactions that queue behind each other: 3959, 3959, 3961, 3966, 3967,
  3967, 3967, 8750, 8860, 11677 ms on the dev workstation. **Accepted cost:
  concurrent uploads take about four seconds each, worst case twelve.** A
  single uncontended upload is unaffected and is what staff actually
  experience. This is not a correctness risk — no receipt is lost, no upload is
  misreported, and the Worker finishes anything a request could not. The
  isolation causing the contention is what enforces one receipt per source and
  one reference per case, so it must not be loosened for speed; the real work
  is reducing how much of that transaction an upload holds. Take this if real
  concurrent use appears, and re-measure on CI or production hardware first —
  the figures above are one workstation
  (task/upload-case-creation-and-inbox review, 2026-08-07).
- Thirteen P2 review findings on the upload/Inbox work, none of them fixed in
  PR 357 (task/upload-case-creation-and-inbox review, 2026-08-06): a mailbox
  identity change cannot be recovered by disable-and-add because the address
  stays unique across disabled rows; standalone-Audit inputs are absent from
  the HTML until a first submission fails, because the case-type dropdown has
  no script or reload; retained-mail freshness takes the newest completion
  across mailboxes, so one healthy mailbox hides another that is stale or
  failing; retained threads group on conversation identity alone, so copies of
  one thread in two approved folders merge into a cross-mailbox thread;
  `ImageIntakeRegistered` uploads land on the received-item page instead of
  Image intake; the mailbox filter disappears when only one mailbox has mail,
  losing the per-mailbox scope the capability claims; the estate caps a
  configured Inbox folder identity at 200 characters while
  `GraphApprovedMailboxOptions.Create` still accepts 500, so a 201–500
  character configured fallback fails the whole tick; a staff-corrected draft
  value is labelled `Extracted` because any candidate exists rather than the
  one displayed; negative `VehicleMileage` is not rejected server-side even
  though `CaseDataOperations` rejects it later; an `OcrRequired` receipt
  opened from Received items hides the only link to the create screen;
  `/Administration/Mailboxes` renders both poll times with `:u` instead of
  `OperatorLabels.OfficeTime`, so they read an hour early in BST; Inbox detail
  reports "Not associated with a case" for correspondence linked through
  `IntakeManualAssociation` rather than `CaseIntakeLinks`; and `/Cases/Create`
  without a `receiptId` throws out of `GetIntake` instead of returning the
  styled not-found.
- Production holds no Principal at all, so no QDOS instruction can become a
  case there whatever intake decides. `SELECT COUNT(*) FROM Principals` on
  `pegasus-prod-sql-252ow37gij/pegasus` returned 0 on 2026-08-05, and
  `EfCaseAcceptanceStore` throws "The active principal 'QDOS' does not exist"
  without one. Path step 2 cannot complete until the QDOS Organization and
  Principal exist on the estate: decide whether they are created through
  Administration by the operator or seeded by a bootstrap script, then do it
  and record the evidence. Until then a definitive instruction reaches
  allocation and stops there — `AllocateCaseIfDefinitiveAsync` is deliberately
  non-blocking, so it leaves a receipt behind rather than failing the intake.
  Decide with it what re-drives allocation for a receipt already stranded:
  nothing routine reprocesses a completed work item, and the intake review
  screen offers the create-case form for `Needs sorting` only, so a receipt
  stuck this way stays stuck even once the Principal exists
  (found diagnosing the 2026-08-05 QDOS forward,
  task/qdos-forward-intake-failure).

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
- Restore Standalone Audit case creation end-to-end:
  `EfStandaloneAuditEvidenceStore.cs:73` still gates original-report
  evidence confirmation on the retired `draft_ready` decision code, which
  nothing has written since `9393c98`, and `ProcessIntake.cs:328-333`
  routes every standalone-Audit instruction to `Needs sorting` — so the
  `StandaloneAuditEvidenceId` that `AcceptIntake.cs:61-67` requires can
  never exist and every Audit acceptance is refused. Also migrate the
  remaining legacy `draft_ready` readers (`EfCaseAcceptanceStore.cs:170`,
  `EfOperationsStore.cs:658`), fix the wrong legacy-mapping comment at
  `IntakeContracts.cs:28-30` (the store maps `draft_ready` to
  `CaseCreated`, not `NeedsSorting`), and the stale filter description at
  `IntakeMcpTools.cs:58` (repository audit, 2026-08-06).
- Make intake refusal fail-closed and visible (INT-25 follow-through): no
  `ProcessIntake` branch can emit `BlockedIntake`, although
  `requirements.md:190-193` defines it as the fail-closed outcome with a
  reason and reasoned resolve/retry actions — the named conditions reach
  quarantine, silent allocation failure, or `Needs sorting` instead;
  `IntakeWorkItems` states (pending/dispatching/retry/poisoned) have no
  Web surface at all; poison and quarantine rows project with null retry
  fields so a refused message cannot be re-driven from the product; and
  `DurableIntake.cs:647-653` swallows every allocation failure with
  `catch (Exception)` — no log, no persisted event. Automatic allocation
  also hard-codes `CaseType.Inspection` (`DurableIntake.cs:633`).
  Coordinate with task/qdos-forward-intake-failure: PR 356's "Create the
  case from this instruction" copy points at a form rendered only for
  `Needs sorting` (`Details.cshtml:392`) and a handler that refuses
  anything else (`Details.cshtml.cs:352`) (repository audit, 2026-08-06).
- Triage reserved-meaning edges (INT-21 adjacency):
  `DurableIntake.CreateTriageIfQualifyingAsync:774` requires the
  `CaseCreated` decision — the decision, not an existing Case: it runs
  after the silently-swallowed allocation in the same pass (`:538-540`),
  so it inverts the pre-case property (`operator-notes.md:37`,
  `requirements.md:264`) and can also run for a receipt whose allocation
  failed. Latent while `NoAcceptedIntakeTriageMatcher` is the only
  matcher; fix as an eligibility/ordering decision proven by behaviour
  tests before any matcher is accepted; the manual staff Triage origin
  (`requirements.md:264`) has no Core command; decide the `/Triage`
  route/folder/namespace overload — the Queues screen kept them while
  `docs/design.md#operations-first-shell` claims the route was renamed;
  and rename the
  three general-sense Audit identifiers (`EfIdentityAuditStore`,
  `AutomationMcpAuditor`, migration `MailboxRouteAudit`) (repository
  audit, 2026-08-06).
- Decide and fix the Assessment surface exposure (UI-15/AI-09):
  `/Cases/{id:guid}/Assessment` is routed unconditionally in Production —
  any authenticated staff member on releases 6/7 can open unbound section
  forms with dead `type="button"` Save controls. Close the panel-state
  gaps: `Cancelled` and `Expired` fall through `EvaluatePanelStateAsync`
  and render as `available` (`Index.cshtml.cs:181-192`), the panel never
  returns to `available` after a terminal request so a case can be sent
  exactly once ever, `unavailable` (Administrator switch off, ineligible
  state) is unreachable behind any terminal request, and the send confirm
  posts only via `form.submit()` script that the production CSP discards
  (repository audit, 2026-08-06).
- Send to AI traceability and PAV follow-ups (AI-09/MCP-06, extends the
  PR 332 review items above): bind `workRequestId` on the eleven tools
  that lack it or record why not; enforce or drop the write-only
  `CapabilityScope` and `CaseVersionAtSend` stamps; persist the reply
  timestamp (`requirements.md:964` — parsed then dropped); read connector
  `/status` for the promised diagnostic view or descope it; treat the
  connector's documented `duplicate: true` replay as success rather than
  `Failed`; surface the request id on the completed panel; make the
  ≤500-char operator instruction reachable or remove the mechanism; PAV
  slider: span guide evidence rather than the chosen figures, add the
  equity-in-repair row, gate on a costed repair total, clamp the paired
  numeric input; add the plan's idempotent-create and duplicate
  operation-key tests (repository audit, 2026-08-06).
- Guard `main` without paid branch protection (operator decision
  2026-08-05: no paid GitHub features): add a push-triggered CI job on
  `main` that fails loudly on any direct or non-merge push — detection,
  not prevention. `440ab5c` reached the deployment branch without a PR and
  carried a defect CI would have caught (fixed in PR 335); PR 324 was
  opened against `main` before redirection; the dual-branch history cost
  again at PR 348 with ninety add/add conflicts in a merge that had no
  source conflict. Add CI checks for new-Markdown-file creation and NOW.md
  claim-format/staleness while in the workflow file.

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
  (`_ReasonDialog` focus trap and the Assessment page's three inline
  scripts; the `_FreshnessBanner` inline script is already gone, and the
  Assessment page is routed in Production, not unrouted as this item
  previously said): external script files versus CSP hashes.
  They are silently discarded by the deployed `default-src 'self'` policy
  today — progressive enhancement only, nothing functional breaks
  (release-4 live checks, 2026-08-04).
- Operator decision: the pre-scrub commits of task/qdos-email-classification
  still carry corpus-derived names/references in GitHub PR refs — decide
  whether to request a history purge; and decide handling of the real staff
  addresses and case data in the operator-supplied
  `reference/workproviders-and-repairers/` files
  (task/qdos-email-classification review, 2026-08-03).
- UI polish follow-ups from the design-pass review: Send-confirm focus drop,
  focus-trap escape edge case, sparkle glyph clipping, and the freshness
  banner's London label falling back to a UTC value without IANA data
  (task/ui-alpha-design-pass review, 2026-08-03); also the Access review
  page renders the `0001-01-01 00:00:00Z` sentinel as "Last reviewed"
  beside a `Recorded` state (release-5 live checks, 2026-08-04).
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
  surface, evidence classified per engineering.md and recorded in operations.md,
  before any activation claim
  (task/mcp-automation-actor review, 2026-08-03).
- Promote the settled Automation Actor identity/authentication/tool-inventory
  contract to an ADR — with the temp plan deleted it is owned only by
  architecture.md/operations.md prose
  (task/mcp-automation-actor review, 2026-08-03).
- Bound the approved-mailbox identity against the receipt-token limit:
  `PollApprovedInbox.MaximumExternalReceiptTokenLength = 200` bounds
  `{mailboxId.Length}:{mailboxId}{immutableMessageId}`, so a long
  administrator-entered mailbox identity (now up to 100 characters) shortens
  the headroom before a real Graph message is quarantined as
  `message_identity_too_long`. Pre-existing risk, made reachable by
  administrator input in ADR-0022; decide whether to tighten the identity
  bound, widen the token, or key the receipt token on something shorter
  (task/upload-case-creation-and-inbox part E, 2026-08-05).
- `Administration/Automation/Activity.cshtml`'s pager is silently broken, the
  same way the received-items and mail pagers were: `asp-route-page` does
  nothing, because `page` is the reserved Razor Pages route key and `asp-page`
  overwrites it, so Next emits a link with no page. Both fixed pagers now use
  `pageNumber`; this one was out of scope. Sweep for any other
  `asp-route-page` before assuming these are the last three
  (task/upload-case-creation-and-inbox review, 2026-08-06).
- Finish the operator-language sweep the route rename started. `/Intake` and
  `/ImageIntake` are gone as URLs, but the receipt and vehicle-image pages
  still say "Intake review", "Intake resolution", "Block intake" and "Register
  Image intake" in visible copy, which `docs/operator-notes.md:378` forbids.
  Also missing: an `/Inbox/{id}` accessibility case (the audited routes render
  without a seeded record, so a retained message needs a browser fixture), and
  a guard so an unresolvable `RedirectToPage` target cannot 500 the Upload
  handler — URL generation runs after the handler returns, outside its catch
  (task/upload-case-creation-and-inbox review, 2026-08-06).
- Decide the retained plaintext `EditLeaseToken` column: it sits beside its
  own hash so an exact claim replay can return the opaque token, which makes
  it a secret at rest. Removing it changes the accepted replay contract, so
  it needs a decision, not a patch
  (task/case-edit-lease-continuity, 2026-08-05).
- Identifier and clock debt on the case surfaces, for the queued Cases page
  rework: `_CaseSummary.cshtml` renders `AssignedEngineerId` and the approval
  `SubjectId` as raw GUIDs, and Triage's non-editing timestamps still use
  `ToLocalTime()` rather than the Europe/London wall clock the edit-authority
  copy now uses. Both are banned by
  `docs/ui-work/ui-standards-and-review.md`; the edit-authority panels were
  cleared but the rest of the pages were out of that task's line
  (task/case-edit-lease-continuity review, 2026-08-05).
- After the Operations requests rework: `RecoverableLeaseCaseIds`,
  `LeaseCaseId` and `LeaseLabel` on `Requests.cshtml.cs` may have lost their
  last view caller now that the page shows no editing state — confirm and
  delete what is unwired
  (task/case-edit-lease-continuity merge, 2026-08-05).
- Decide the unbuilt approved UI packages (audit total 17; the recovered
  set carries a page-5 numbering collision, so work from the named list
  rather than the count): the operator-approved
  `docs/ui-work` review (deleted in `4c89669`, recoverable at `4c89669^`)
  shipped 8 of its 31 page packages; pages 8 (receipt review), 9-10
  (image intake), 13 (public-upload body), 5 (administration hub) and
  19-31 (all thirteen administration sub-screens) were never built and
  have no successor claim — queue, re-scope, or descope each explicitly.
  Includes the banned-terms sweep: seventeen shipped `.cshtml` files
  violate `docs/design.md#voice-labels-and-necessary-copy`, led by
  `Intake/Details.cshtml`
  (51 hits) (repository audit, 2026-08-06).
- Absorb the approved UI divergences into the design authority (operator
  decision 2026-08-05: absorb, not revert): `docs/design.md` still
  asserts pre-divergence values — 2px radius against the shipped 6px/5px,
  stale colour, spacing and token claims, "no ledes" while nine pages set
  one — and `site.css:1-12` cites the deleted `docs/ui-work` standards.
  Reconcile the unapproved extras (modal scrim/shadow, 10px/14px spacing
  tokens, the retained eyebrow/lede mechanism, `site.js`, the generic
  `/status/{code}` route) into the authority or remove them, and apply
  the ui-spec state-matrix additions the durable-rules proposal specified
  but never landed (repository audit, 2026-08-06). Restructure wave 3:
  once the reorg lands, the merge target is the new `docs/design.md`.
- Correct the stale documentation claims from the accuracy audit:
  `architecture.md` L87/L307/L503 still describe the removed
  `ReceiveIntake` handler on `/Intake` and its orphaned
  `Program.cs:731-758` gate (coordinate with
  task/upload-case-creation-and-inbox), omit `/diagnostics/version` and
  the `Bootstrap:VerificationAccount` reconciliation from the
  authentication boundary, and lag the UI rework in the implementation
  map; `capabilities.md` header arithmetic says 228 IDs and 199 planned
  against a 230-row table and 201 with targets, INT-25 says "not yet
  deployed" though its merge is inside release 7 (`32feefa`), and INT-01
  says "accepted" while upload creates no case (repository audit,
  2026-08-06).
- Repository hygiene residue after the restructure: keep `.obsidian/`;
  confirm `.infisical.json` is unused before deleting it; add an `infra/`
  validation lane or record why not; and record the build boundary for
  `scripts/email-eval-desktop/`, which stays as the operator's own tool but
  references Core/Infrastructure while no tracked command builds it
  (repository audit, 2026-08-06). The PerformanceTests caller, Python test
  placement/CI, stale attributes/ignores, and four byte-identical
  evidence/runtime logo-signature pairs are resolved by PR 361 and are no
  longer independently claimable.
- CI wall-clock second pass: PR 321's reduction (sharded lanes, per-run
  template database, caches) under-delivered on the operator's intent —
  measure what still dominates (five of six jobs are Windows-pinned, the
  browser lane reinstalls Playwright on every run despite the cache, lane
  structure) and cut further, building on PR 321 rather than repeating it
  (operator, 2026-08-06).
- Complete the canonical-docs accuracy audit: the 2026-08-06 audit
  claim-checked `architecture.md`, `docs/design.md` and the roadmap
  forms only; `requirements.md`, `operations.md`, `operator-notes.md`,
  `open-decisions.md` and `engineering.md` have had no claim-level
  verification against code (artifact critique, 2026-08-06).
- Queue-and-claims hygiene: the report-renderer planning Doing line is
  superseded and closed by its successor line yet still listed; the
  CASE-27 Doing line's branch has already released its claim (`c02d75a` —
  coordinate with open PR 342); remote branch `task/vault-consolidation`
  has no Doing line; and the two live `docs/temp-plans/` files have no
  matching Doing line (staleness ladder applies — confirm ADR-0021
  carries their durable content before deletion) (repository audit,
  2026-08-06).

- Reconcile the report-renderer MCP plan with the merged AI-09 work: PR 332
  landed an `automation.assessment` scope, five Automation Actor tools and the
  Automation Actor ADR, so the renderer plan's assumed 9-tool inventory and its
  recommendation to write that ADR are both superseded. Any later render tool
  joins the inventory PR 332 left behind
  (task/report-renderer-integration, 2026-08-03).
- Capability-inventory questions raised by the renderer planning, each needing
  an operator answer before the work it gates can start: RPT-02 requires a
  four-member outcome enum that no accepted source defines (requirements name
  two Assessment findings); ENG-01's "one canonical repair specification"
  contradicts RPT-03's conservative-and-maximised pair, and resolving it
  changes what ENG-01 must build; four new rows are proposed for the five
  templates that map to no capability, and two templates are proposed for
  retirement; and RPT-03 has no renderer template at all
  (task/report-renderer-integration, 2026-08-03).
- Lifecycle defect found while planning the renderer consumer chain:
  correcting a sent report requires reopening to `ReportPreparation`, which is
  exactly the state in which `UnlinkReportEvidence` becomes permitted, so a
  correction makes final send evidence unlinkable — contradicting the rule
  that the report-sent event stays final. Nothing hits it today because no
  workflow returns a post-report case to report preparation. Sits inside the
  CASE-23 open decision (task/report-renderer-integration, 2026-08-03).
- Renderer container and `.mcpb` proofs the uplift could not run: build
  `workspaces/report-renderer/Dockerfile` on a Docker-capable host to prove the
  corrected `v1.61.0-noble` base actually publishes and runs a `net10.0` render,
  and build and launch the `.mcpb` bundle once under .NET 10 to prove the
  single-file Playwright driver still resolves. Both are configuration fixes
  today, not proven builds. Moving jammy → noble also shifts Ubuntu 22.04 → 24.04
  with its font and ICU versions, and there is no valid "before" image because
  the `v1.61.0-jammy` tag never existed — so the first noble render is a new
  baseline, not a comparison
  (task/report-renderer-workspace-uplift, 2026-08-05).
- Renderer analyzer strictness and lock files, both deferred by the uplift and
  recorded in workspace ADR-0014: the workspace still sets its own relaxed build
  properties and inherits nothing from the repository root, so
  `TreatWarningsAsErrors` is off and an estimated low-hundreds of diagnostics
  wait behind relocation; and the workspace has no `packages.lock.json` files, so
  its CI lane correctly cannot use `--locked-mode`. Decide both before, not
  during, any move into `src/`
  (task/report-renderer-workspace-uplift, 2026-08-05).
- Renderer density reaches one template only: `HtmlComposer` passes the density
  through to `market-valuation-evidence` and not to the evidence pack, fee note
  or expert report, so eleven of the twelve identifiers compose byte-identically
  at Normal, Compact and UltraCompact. Observed while proving render parity;
  pre-existing, not introduced. Decide whether auto-fit is meant to apply to
  those bodies before density enters any issued-artifact contract
  (task/report-renderer-workspace-uplift, 2026-08-05).

## Waiting (each line names its unblock condition)

- Report-renderer relocation into the monolith — blocked on three operator
  decisions recorded in the plan set: whether the `.mcpb` stdio host is frozen
  as a built artefact, kept in a reduced workspace, or republished, since
  parity-first and workspace retirement cannot both be executed at once; where
  rendering executes in production, given there is no Web Dockerfile and the
  default `aspnet:10.0` base has neither Chromium nor the Liberation fonts and
  `PublishContainer` cannot install them; and whether unaccepted report wording
  and the three provenance-sensitive signature images may ship in the production
  assembly behind a closed gate. The Core render contract, the ADR and the
  staged route are drafted and waiting. The workspace-local uplift that had to
  precede any of this is done
  (task/report-renderer-workspace-uplift, 2026-08-05).
- Report rendering capabilities RPT-01–05 and EXT-08 — blocked on accepted
  CASE-31, ENG-01 and ENG-02 data, which requirements sequences ahead of them
  and none of which exists, and on the open report-wording decision. With
  `DESIGN_SPEC` superseded by the 2026-08-03 operator decision, the RPT
  specification must now come from those three capabilities plus operator
  answers; the field-level contract the renderer will demand of them is
  drafted (task/report-renderer-integration, 2026-08-03).

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

Roadmap: [docs/capabilities.md](docs/capabilities.md) · Questions: [docs/open-decisions.md](docs/open-decisions.md) · How-to: [docs/runbook.md](docs/runbook.md)

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
