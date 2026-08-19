# Open questions — DELIV-012

Every question below was put to the operator with the question tool on
2026-08-19 and answered the same day. Answers are quoted, not paraphrased.

## Resolved

- [x] **Q1 — What happens to the four open PRs owned by another agent?**
  #416 (INTK-005), #417 (INTK-006), #423 (INTK-008), #424 (INTK-007) are taken
  by `Codex` with checklists at 7/33, 26/41, 8/29 and 22/36, red or absent CI,
  and two of them edit protected operator truth.
  **Answer: "Take over and finish all four."** DELIV-012 therefore force-takes
  those tickets, completes their implementation and checklists, fixes their CI,
  and merges them before the release. End state stands: 0 open PRs.

- [x] **Q2 — Is the rewritten image-only-arrival statement in #423 confirmed
  operator truth?**
  **Answer (verbatim): "It could be either an image initiated case, OR it could
  be images being received for an existing case. ie if we get images, with a
  registration that doesnt match any existing case, then that creates an image
  initiated case. If they match an existing case (by VRM), then get get attached
  as evidence to that case."**
  Resolution: the operator did not accept #423's wording as complete. Both
  branches must be stated in `docs/operator-notes.md`: a readable VRM that
  matches no existing case creates an **Image-initiated Case**; a readable VRM
  that matches an existing case attaches the images to that case as
  **evidence**. The Image-initiated Case is the no-match branch only, and the
  existing "linked automatically only on a definitive match, or linked manually
  by staff" sentence is preserved. [[INTK-006]] already owns "associate each
  vehicle-image group or create one Image-initiated Case", so the two must agree.

- [x] **Q3 — Is "Unidentified replaces Needs sorting" confirmed operator truth?**
  **Answer: "Confirmed — Unidentified replaces Needs sorting."** The
  `docs/operator-notes.md` section from #424 merges as written, and the
  `CLAUDE.md` product invariant is updated so `Needs sorting` is recorded as
  replaced by **Unidentified** for that meaning. `Audit`, `Triage` and
  `Blocked intake` keep their settled distinct meanings.

- [x] **Q4 — What to do about `SentEvidencePollFunction` failing every minute in
  production** (enabled, but `ApprovedMailboxes.AllowSentEvidence = 0`, ~1,440
  failed invocations/day)?
  **Answer: "Approve Sent-evidence polling for the mailbox."** This authorises a
  production data write setting `AllowSentEvidence = 1` (and whatever supporting
  Sent folder identity the policy requires) for
  `instructions@collisionengineers.co.uk`, applied through the release.

- [x] **Q5 — How far to go on the three dark/orphaned items** (report renderer
  registered with no caller and no Chromium in the image; `MailOperationalDestinationPolicy`
  with only tests; `IRepairSpecificationStore`/`EfRepairSpecificationStore` not
  in DI)?
  **Answer: "Make all three live in this release."** The release therefore adds
  the Chromium/fonts layer to the Web container image, gives the renderer a real
  operator entry point, wires the destination policy into the live classification
  path, and registers the repair-specification store behind a real caller. This
  absorbs the work [[PLAT-007]] and [[DOCS-001]] were holding.

- [x] **Q6 — Release authority and Azure writes.** Not yet asked; it is asked
  immediately before the promotion and again before each Azure write, per
  `CLAUDE.md`. Recorded here so the requirement is not lost: the `dev` → `main`
  exact-SHA fast-forward needs `MERGE AUTH GRANTED` for the exact SHA, and the
  ACR push, `azd provision`, Worker `config-zip` and the SQL writes each need
  approval for their exact targets. Q4's answer pre-approves the
  `AllowSentEvidence` data write only.

## Parked (explicitly deferred)

- The `docs/runbook.md` sentence "`SentEvidencePollFunction` stays disabled
  unless separately approved" versus `scripts/Invoke-ProductionSmoke.ps1`
  asserting all nine functions enabled: Q4 resolves the estate (polling is
  approved), so the runbook sentence is corrected in this release's docs refresh
  rather than being a separate question.
- Log Analytics daily cap (0.1 GB) reaching `OverQuota` around 11:50 UTC daily,
  which blinds telemetry-based verification after that hour. Deferred: it
  constrains *when* the post-release watch can read App Insights, not whether the
  release is correct. A capacity decision belongs to its own ticket.
- The stale `Azure Service Bus Data Sender` role assignment on queue
  `intake-work` held by the Web identity, and the inert azd variable
  `BOX_ROOT_FOLDER_ID=392761581105`. Deferred: neither is a functional
  dependency and removing a live role assignment is an unrelated write.

## Resolved — second round (2026-08-19, during execution)

- [x] **Q7 — How should the report renderer reach a usable state, given that
  repair costs have no source of truth?** The report-draft entry point was
  built and is reachable, but `AssessmentReportSnapshot.Validate()` requires
  labour hours, an hourly rate, parts, paint materials and specialist/other,
  and I verified none of those is persisted: `rates.card` and `rates.class` are
  free-text assessment fields, `HourlyRate` exists only inside the report
  contract (`AssessmentReportRendering.cs:88`) and nowhere in persistence, and
  EXT-09 — which owns estimate formulas — is scheduled Later/1.0.0 with the note
  *"formulas and permissions require accepted authority"*. Fabricating figures
  was refused: they would be wrong money in an engineering report.

  **Answer (verbatim): "these are imported through other means generally ie
  external estimating systems: auxatex, glasses etc. Or an AI performs an
  estimate and sends via MCP connector. We also need to be able to drag+drop an
  estimate in"**

  Resolution: repair costs are **imported, not typed**. Three routes are named —
  external estimating systems (Audatex, Glass's), an AI estimate delivered
  through the MCP connector, and drag-and-drop of an estimate file. None of the
  three exists yet. This is new operator truth and a new capability; it is
  captured as its own ticket rather than improvised inside the release.

  Consequence for release 12: the entry point ships **reachable and honest** —
  it lists "Repair cost figures" among the outstanding readiness items and stays
  disabled for a live case until an estimate is imported. The renderer is no
  longer dark (it has a production caller), and no invented figures ship. Worth
  noting for whoever picks up the import work: `EstimateLineInput` /
  `CaseEstimateLineRecord` already carry `Type`, `WorkUnits` and `Price`
  (`AssessmentContracts.cs:164-196`), so an imported estimate has somewhere real
  to land; what is missing is the import path and the accepted derivation from
  those lines to the report's cost buckets, which is EXT-09's authority to
  settle, not this release's.

- [x] **Q8 — Web container sizing for in-process Chromium.** The renderer runs
  inside the Web Container App, currently 0.5 vCPU / 1 GiB with a single
  always-warm replica (min = max = 1); Azure Container Apps hard-OOM-kills
  rather than throttling, so a render could take the site down. ACR capacity was
  checked and is not a constraint (1.3 GB used of 10 GB).
  **Answer: "Raise to 1.0 vCPU / 2 GiB."** Approved, roughly $16 → $32 per
  month. This edits `infra/modules/platform.bicep`, so it changes what
  `azd provision --preview` should show; the preview is reviewed against exactly
  two expected changes (the new digest-pinned Web revision and this sizing
  change) before `azd provision` runs, and anything else stops the release.
