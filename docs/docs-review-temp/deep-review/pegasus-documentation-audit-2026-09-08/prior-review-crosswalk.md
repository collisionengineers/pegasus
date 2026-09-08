# Previous-review reconciliation

Current baseline: `26ba4ed408317cccdb354dc1e115b0297f15df94` (`dev`, 8 September 2026).

All **71 observations in 29 groups** from the supplied 5 September audit are accounted for below. “Resolved wording” means the identified documentation defect is repaired at this revision; it is not a fresh production or behavioral test. “Partially resolved” identifies both the repair and the residue. An old path, date, policy version or retired instruction must not be reintroduced to satisfy a historical review.

Disposition counts: 36 resolved wording, 15 partially resolved, 20 still outstanding.

## A01 — Conflicting live agent instructions

### A01.1 — Resolved wording

**Original observation:** The managed block requires .worktrees/<id> and <id>-<slug>, while Repository task workflow requires ../pegasus-worktrees/<slug> and task/<slug> based on origin/dev.

**Current assessment:** The current managed/workflow instructions use recorded ticket worktrees and configured integration delivery. Do not replay the old ../pegasus-worktrees versus task/<slug> instruction as the normal ticket convention.

**Evidence at the pinned revision:** `AGENTS.md — Kanmer operating instructions; Repository task workflow`.

### A01.2 — Resolved wording

**Original observation:** The managed block says proof is written on merged main, but task PRs target dev and the newer kanmer-verify skill binds verification to the exact PR merge SHA, not the moving main checkout.

**Current assessment:** Proof now names the configured integration branch; kanmer-verify binds execution to the exact merged PR SHA in a detached worktree. This is not a claim that every historical proof was repaired.

**Evidence at the pinned revision:** `AGENTS.md — Verification; .agents/skills/kanmer-verify/SKILL.md`.

### A01.3 — Resolved wording

**Original observation:** Agent conduct forbids merging one's own PR, while Allowed operations permits merging one's independently reviewed, green PR into dev. The responsible implementation/review/merge roles are not reconciled.

**Current assessment:** The current workflow separates implementer handoff from independent review/merge and forbids author self-merge. Remaining generic conduct duplication is a different issue.

**Evidence at the pinned revision:** `AGENTS.md — Repository task workflow`.

### A01.4 — Resolved wording

**Original observation:** Workflow step 6 says to remove the worktree/branch and move to the final stage after merge. The newer verification/closeout contract retains the implementation worktree through verification and owns final cleanup separately.

**Current assessment:** Current verification/closeout wording preserves the implementation worktree while disposing the verification worktree separately.

**Evidence at the pinned revision:** `AGENTS.md — Repository task workflow; .agents/skills/kanmer-verify/SKILL.md`.

### A01.5 — Partially resolved

**Original observation:** Two Agent conduct blocks have diverged: the first rule 22 includes obsolete-after-change dispositions, the superseding commit and root-cause grouping; the second retains the older shorter rule. Board/runtime conventions are duplicated too.

**Current assessment:** The managed conduct text contains the newer disposition/root-cause rule. AGENTS still repeats workflow and evidence policy outside the managed block; consolidate the owners rather than preserving two rule sets.

**Evidence at the pinned revision:** `AGENTS.md — Kanmer operating instructions; Pegasus repository instructions`.

### A01.6 — Resolved wording

**Original observation:** Product invariants still describe workspaces as containing buildable imports, although both imports have been retired.

**Current assessment:** AGENTS now identifies workspaces as provenance for retired imports, not active build units.

**Evidence at the pinned revision:** `AGENTS.md — Architecture map`.

### A01.7 — Resolved wording

**Original observation:** The Audit invariant says missing or ambiguous standalone Audit evidence withholds only the later Audit reference. FRD-01 instead withholds Case creation/reference allocation when the required original report or its literal verdict is unresolved.

**Current assessment:** The specific AGENTS/FRD-01 disagreement is repaired: normal Case/PO allocation precedes the evidence-dependent Audit reference. The same old gate survives in FRD-02 and INT-25; see AUD-01.

**Evidence at the pinned revision:** `docs/frd/frd-01-case-identity-and-lifecycle.md; docs/frd/frd-02-intake-and-source-identity.md — Mandatory pre-case gates`.

## A02 — Stale domain glossary

### A02.1 — Resolved wording

**Original observation:** Needs sorting is still defined as the destination for unclassifiable email and Triage requests awaiting a registration; the current Unidentified/U-reference meaning is missing from the glossary.

**Current assessment:** CONTEXT now defines Unidentified and its distinction from Triage and Blocked intake.

**Evidence at the pinned revision:** `CONTEXT.md — Unidentified; Blocked intake`.

### A02.2 — Resolved wording

**Original observation:** Audit says a definitive instruction creates the normal Case/PO first and the a./ap. reference is derived later, contrary to FRD-01's original-report/verdict allocation gate.

**Current assessment:** CONTEXT and current FRD-01 now agree on normal Case/PO followed by the later Audit reference. FRD-02 remains contradictory.

**Evidence at the pinned revision:** `CONTEXT.md — Audit; docs/frd/frd-01-case-identity-and-lifecycle.md`.

### A02.3 — Still outstanding

**Original observation:** The repair-estimate AI Proposal definition describes a separate immutable proposal pending application to the Case; current assessment requirements instead describe attributed working-data writes and named draft estimates. The glossary does not distinguish that current estimate route from genuinely proposal-based future features.

**Current assessment:** The glossary still needs to distinguish an immutable AI Proposal from direct, unconfirmed working-data writes and named draft estimates. Do not remove future proposal semantics indiscriminately.

**Evidence at the pinned revision:** `CONTEXT.md — AI Proposal; Send to AI; docs/frd/frd-10-mcp-automation-and-actor-boundary.md`.

## A03 — Stale repository overview

### A03.1 — Resolved wording

**Original observation:** Describes workspaces/ as containing independently maintained and buildable source imports, despite retirement of both imports.

**Current assessment:** The closing README passage now explicitly calls both imports retired and not active projects or deployment units.

**Evidence at the pinned revision:** `README.md — workspaces paragraph`.

## A04 — Internally conflicting workspace index

### A04.1 — Resolved wording

**Original observation:** The opening calls the directories buildable source imports; the conclusion says both are retired and no live workspace exists.

**Current assessment:** The current workspace index opens with retirement and states that no live workspace exists. Its future import-admission rules do not reactivate an import.

**Evidence at the pinned revision:** `workspaces/README.md — opening; Source provenance`.

## A05 — Stale engineering procedure

### A05.1 — Resolved wording

**Original observation:** Still refers to NOW.md claim-line maintenance although the current claim mechanism is a taken Kanmer ticket and NOW.md is absent from the reviewed tree.

**Current assessment:** The reviewed engineering guide no longer requires NOW.md claim-line maintenance. Claims are on Kanmer. Expired DELIV transition instructions remain a separate cleanup.

**Evidence at the pinned revision:** `docs/engineering.md — Branches and delivery`.

### A05.2 — Resolved wording

**Original observation:** The evidence ladder describes DOC/MSG as later approved formats even though FRD-05 and the integrated extractor decision now include them.

**Current assessment:** The evidence ladder now explicitly includes EML/PDF/DOCX/DOC/MSG.

**Evidence at the pinned revision:** `docs/engineering.md — Required evidence tiers`.

## A06 — Unreconciled competing design guide

### A06.1 — Resolved wording

**Original observation:** Presents old navigation and optional shell directions as current guidance rather than deferring to the selected integrated workspace.

**Current assessment:** The Stitch guide now has a historical-comparison warning and points to current design authority.

**Evidence at the pinned revision:** `.stitch/DESIGN.md — opening status`.

### A06.2 — Resolved wording

**Original observation:** Its blanket bans/prescriptions, including no gradients and old compact record layout guidance, have not been reconciled with the selected design and D29/D30 scrolling Case record.

**Current assessment:** The new status warning explicitly subordinates conflicting layout to the accepted v3 design and later decisions. Do not rewrite its historical body into a second active guide.

**Evidence at the pinned revision:** `.stitch/DESIGN.md — opening status`.

### A06.3 — Resolved wording

**Original observation:** Its explanatory hints and disabled-action guidance conflict with newer no-explanatory-copy/component rules. Some of the old record-layout guidance is also still in operator-notes, so this is a cross-document authority conflict, not merely a reason to ignore the operator.

**Current assessment:** The historic guide is no longer presented as binding. Current operator-notes/FRD copy and layout conflicts remain separate active findings.

**Evidence at the pinned revision:** `.stitch/DESIGN.md — opening status; docs/operator-notes.md — Interface language`.

## B01 — Stale as-built architecture

### B01.1 — Resolved wording

**Original observation:** The absent-callers list says automated DOC/MSG extraction is absent, while the integrated extraction contract and later architecture material include it.

**Current assessment:** The absent-callers section now says DOC/MSG readers are implemented.

**Evidence at the pinned revision:** `docs/current-architecture.md — Implemented production targets and absent callers`.

### B01.2 — Partially resolved

**Original observation:** The same list calls Provider API deferred and the Operations caller paragraph calls it separately planned. Operations records its endpoint mounted in production from release 37.

**Current assessment:** The production-target paragraph acknowledges Provider API ingress activation, but another caller paragraph still calls it planned. Remove the remaining mixed-time description.

**Evidence at the pinned revision:** `docs/current-architecture.md — Staff Web callers; Implemented production targets and absent callers`.

### B01.3 — Resolved wording

**Original observation:** The same list says Automation MCP is gated off outside DevelopmentOffline; Operations records production activation from release 9.

**Current assessment:** The relevant architecture production-target paragraph now acknowledges the enabled ingress flags. Other files still contain the old local-only claim.

**Evidence at the pinned revision:** `docs/current-architecture.md — Implemented production targets and absent callers`.

### B01.4 — Partially resolved

**Original observation:** The stated 33-tool MCP inventory and scope list omit the newer AI-job tools and automation.jobs scope present in code and FRD-10.

**Current assessment:** The old 33-tool figure has been replaced by a 43-plus-import development-assembly claim, but the scope/inventory narrative still needs reconciliation with automation.jobs and the integrated candidate. A fresh executable inventory was not run in this audit.

**Evidence at the pinned revision:** `docs/current-architecture.md — v1 development assembly; Provider API and Automation MCP; docs/frd/frd-10-mcp-automation-and-actor-boundary.md`.

### B01.5 — Partially resolved

**Original observation:** Manual upload is described as a one-file path, with old request/redirect assumptions. UploadModel accepts a file array and routes multi-member submissions to UploadGroupStatus; the shared maximum batch count is 20.

**Current assessment:** Accepted local inputs now state 20 files, 100 MiB per file and 200 MiB aggregate. Exact current group-status redirect behavior was not re-executed; do not certify all entry-point prose from the corrected limit table alone.

**Evidence at the pinned revision:** `docs/current-architecture.md — Accepted local inputs; Local development procedure`.

### B01.6 — Partially resolved

**Original observation:** The workspace inventory continues to describe two live imports despite their retirement.

**Current assessment:** The workspace list says the imports are retired, but remaining statements about independent builds/current imports still need removal or explicit future-only scope.

**Evidence at the pinned revision:** `docs/current-architecture.md — Workspaces; Source and generated-material roles`.

### B01.7 — Still outstanding

**Original observation:** The diagram labels Graph, Blob/queues, Box and DVLA/DVSA as target connections, despite the same document identifying deployed adapters. Local SQL is also labelled only LocalDB despite the documented Linux container route.

**Current assessment:** The diagram still uses target edges for integrations that later prose describes as deployed, and its local database label omits Linux SQL Server containers.

**Evidence at the pinned revision:** `docs/current-architecture.md — System shape diagram`.

## B02 — Stale operational inventory and activation wording

### B02.1 — Partially resolved

**Original observation:** The Automation MCP narrative still says 33 tools and omits automation.jobs from the scopes it enumerates.

**Current assessment:** The operational narrative still enumerates scopes without automation.jobs and mixes historical tool counts with current-sounding descriptions. Preserve the release-9 fifteen-tool observation as dated evidence; do not overwrite it with a dev inventory.

**Evidence at the pinned revision:** `docs/operations.md — Automation MCP is implemented and enabled in production`.

### B02.2 — Still outstanding

**Original observation:** The Automation MCP evidence table lists separately approved activation as outstanding without distinguishing the already-completed ingress activation from genuinely outstanding external-client/transport evidence.

**Current assessment:** Separate the already-observed ingress activation from still-unproved external-client acceptance and v1 certificate rollout. Generic activation-pending wording remains misleading.

**Evidence at the pinned revision:** `docs/operations.md — Local and live evidence boundaries`.

### B02.3 — Resolved wording

**Original observation:** StorageWorker still says to activate only with the first real storage adapter and Worker trigger, although those adapters and trigger callers already exist.

**Current assessment:** The StorageWorker profile now describes exercising the existing adapters and triggers.

**Evidence at the pinned revision:** `docs/operations.md — Evidence profiles`.

## B03 — Incorrect runtime prerequisite guidance

### B03.1 — Resolved wording

**Original observation:** Says Playwright browser binaries are a browser-acceptance prerequisite, not an application-runtime dependency. The report renderer actually creates Playwright and launches Chromium, and the production Web image carries Chromium for that purpose.

**Current assessment:** The runbook now explicitly states that integrated report rendering needs pinned Chromium at application runtime. Package presence is still distinct from browser execution.

**Evidence at the pinned revision:** `docs/runbook.md — Offline development profile`.

## B04 — Contradictory inventory totals

### B04.1 — Partially resolved

**Original observation:** The introductory active-ID total is 233 while the summary total is 234. They cannot both describe the same inventory.

**Current assessment:** The exact 233/234 discrepancy is gone: there are 244 unique rows. New internal errors remain: 205 versus 215 planned, 153/27 versus 154/26 horizons, and two v1 targets inconsistent with the exact-SemVer rule. See metrics.json.

**Evidence at the pinned revision:** `docs/capabilities.md — Historical allocation provenance; Allocation summary; INT-16; EXT-12`.

## B05 — Settled or changed questions still recorded as open

### B05.1 — Partially resolved

**Original observation:** Provider API tenancy/wire-contract row still says to keep the API absent and asks for routes, headers, encoding, limits and administration that FRD-09 now specifies.

**Current assessment:** The row now acknowledges API-01, but still asks a broadly already-settled wire-contract question. Retain only genuinely new tenancy/client choices; move issuance and live proof to delivery work.

**Evidence at the pinned revision:** `docs/open-decisions.md — External data, submission, and report contracts; docs/frd/frd-09-provider-and-intermediary-routes.md`.

### B05.2 — Still outstanding

**Original observation:** DVLA/DVSA row still directs keeping live lookup disabled and treats provider/caller selection as unresolved, despite the deployed adapter record.

**Current assessment:** Keep-live-disabled wording remains, even while the question concedes the adapters are selected and composed. Distinguish source contract, credentials, last observed deployment and new acceptance evidence.

**Evidence at the pinned revision:** `docs/open-decisions.md — DVLA/DVSA vehicle and MOT lookup`.

### B05.3 — Partially resolved

**Original observation:** AI-job catalogue/lifecycle questions have not been reconciled with ADR-0035, the FRD-11 job model and implemented MCP job tools.

**Current assessment:** The opening now says the catalogue/lifecycle are settled by ADR-0035. Implementation and real-client evidence still reside in this decision register and should move out.

**Evidence at the pinned revision:** `docs/open-decisions.md — Future AI Operations boundary`.

### B05.4 — Resolved wording

**Original observation:** The interim telemetry cap remains 0.1 GB/day in this register, whereas the current architecture records release 35 raising both component and workspace caps to 0.5 GB.

**Current assessment:** The register now records the 0.5 GB/day deployed setting. A full-day workload and any further increase remain separate evidence/decision questions.

**Evidence at the pinned revision:** `docs/open-decisions.md — QDOS alpha activation details; App Insights daily cap`.

### B05.5 — Still outstanding

**Original observation:** The resolved EVA API section retains an unqualified at-most-once submission rule; D36 now permits explicit manual re-send while keeping automatic submission at most once.

**Current assessment:** The resolved section still says at-most-once. ADR-0038 and FRD-07 now retain only staff-initiated sends and permit an explicit With Engineer re-send; do not preserve the older automatic-send rationale as current.

**Evidence at the pinned revision:** `docs/open-decisions.md — EVA API activation; docs/adr/0038-manual-only-eva-api-submission.md`.

### B05.6 — Partially resolved

**Original observation:** The assessment-markup questions retain fee placement and valuation storage questions after the later Report-section and valuation requirements settled at least those portions.

**Current assessment:** Split the old broad question into already-specified fee/valuation placement and any genuinely uncontracted vendor-field semantics. The former is not an open decision.

**Evidence at the pinned revision:** `docs/open-decisions.md — Send-to-AI transport and assessment toolset; docs/frd/frd-06-vehicle-and-engineering-evidence.md; docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.

### B05.7 — Resolved wording

**Original observation:** Upload-link one-time-versus-reuse remains listed as open, although FRD-02 D20 now specifies a fixed 15-minute add/replace session beginning with the first accepted file.

**Current assessment:** The session question is settled; the register now also records the v1 100 MiB/20-file/200 MiB limits. Move this accepted policy to the FRD and repair its surviving 10 MiB clause.

**Evidence at the pinned revision:** `docs/open-decisions.md — INT-31 upload-link limits; docs/frd/frd-02-intake-and-source-identity.md — Request-scoped upload links`.

## C01 — Unreconciled operator-authority text

### C01.1 — Still outstanding

**Original observation:** The active interface-language section still requires one-record screens with tabs rather than stacked sections and core content visible without scrolling. FRD-12 D29/D30 explicitly requires an eleven-section scrolling Case record.

**Current assessment:** The old tabs/no-scroll rule remains in the alleged binding authority beneath a newer v1 override. Migrate current Case layout to FRD-12/design and preserve the old statement as dated provenance only.

**Evidence at the pinned revision:** `docs/operator-notes.md — Interface language; Current v1 decisions`.

### C01.2 — Still outstanding

**Original observation:** The interface-language definition limits Unidentified to unmatched email, while the earlier Unidentified received material section covers retained documents, images, messages and groups.

**Current assessment:** Its interface section still narrows Unidentified to email despite its earlier all-material definition.

**Evidence at the pinned revision:** `docs/operator-notes.md — Unidentified received material; Interface language`.

### C01.3 — Still outstanding

**Original observation:** The August 19 estimate commentary says none of the external/AI/drop routes is built. The code now exposes pegasus_estimate_save and the FRDs specify the AI-draft estimate route.

**Current assessment:** The August estimate passage still says no routes exist. Label the observation historical rather than allowing it to define current source status.

**Evidence at the pinned revision:** `docs/operator-notes.md — Repair estimates`.

### C01.4 — Still outstanding

**Original observation:** The absolute statement that repair-cost figures are not entered by hand conflicts with the later manual line-entry estimate requirements. The quoted operator statement itself says generally imported; the blanket derived prohibition needs an explicit reconciliation.

**Current assessment:** Preserve the original generally-imported statement and distinguish it from the stronger derived prohibition. Reconcile the allowed manual estimate interactions in the owning FRD; do not silently rewrite the quotation.

**Evidence at the pinned revision:** `docs/operator-notes.md — Repair estimates; docs/frd/frd-06-vehicle-and-engineering-evidence.md`.

### C01.5 — Still outstanding

**Original observation:** CAP-010 and the EVA system row still say the API path is non-functional while waiting on EVA developers. FRD-07 records a built contract and distinguishes the outstanding first Pegasus-to-EVA call and live-credential change.

**Current assessment:** The vendor-waiting claims remain in CAP-010 and the EVA row. Current FRD-07 already specifies the API contract; lack of a first accepted live call is a different fact.

**Evidence at the pinned revision:** `docs/operator-notes.md — Required product capabilities; External systems`.

## C02 — Internal Triage destination conflict

### C02.1 — Resolved wording

**Original observation:** The opening and automatic-creation paragraph send a Triage request with no registration to Unidentified; the normal-workflow paragraph still says it remains Needs sorting.

**Current assessment:** The normal Triage workflow now uses Unidentified. Remaining non-email completion semantics are a separate coverage question.

**Evidence at the pinned revision:** `docs/frd/frd-03-triage.md — Normal workflow and completion evidence`.

## C03 — Obsolete vehicle-lookup activation limitation

### C03.1 — Partially resolved

**Original observation:** Still says no approved source selects the live DVLA/DVSA provider/API, response fields, credentials, limits, target or caller proof. That blanket pre-selection state is incompatible with the documented deployed adapters.

**Current assessment:** The broader problem remains the conflation of selected adapters with outstanding live credentials/response acceptance. Do not describe the provider as unselected where current source and operational records select it. The exact live service was not queried by this audit.

**Evidence at the pinned revision:** `docs/frd/frd-06-vehicle-and-engineering-evidence.md — Vehicle data and MOT enrichment; docs/operations.md — Local and live evidence boundaries`.

## C04 — Conflicting EVA workflow requirements

### C04.1 — Resolved wording

**Original observation:** Export and API-submission paragraphs say the Case state/version do not change. FRD-01 and FRD-12 D44 say Send to EVA is the implicit Review action that moves the Case to With Engineer.

**Current assessment:** The current FRD-07 acknowledges the Review-to-With Engineer handoff.

**Evidence at the pinned revision:** `docs/frd/frd-07-eva-and-external-engineering-handoff.md — Focused EVA manual handoff`.

### C04.2 — Still outstanding

**Original observation:** Saving Case data is said to invalidate the prior completeness confirmation and return the Case to Not ready; this is not reconciled with D44 removing staff-review flags and FRD-01 prohibiting unchanged/unrelated saves from resetting readiness or lifecycle.

**Current assessment:** The blanket save-invalidates-readiness claim still conflicts with the D44 distinction for unchanged/unrelated saves. Replace it with the authoritative affected-field/state rule.

**Evidence at the pinned revision:** `docs/frd/frd-07-eva-and-external-engineering-handoff.md; docs/frd/frd-01-case-identity-and-lifecycle.md`.

### C04.3 — Still outstanding

**Original observation:** External boundary still describes the EVA API route as awaiting a usable contract, although the same file documents the built August 27 contract.

**Current assessment:** The external-boundary tail still retains future/unaccepted-contract descriptions after the same document specifies the accepted API.

**Evidence at the pinned revision:** `docs/frd/frd-07-eva-and-external-engineering-handoff.md — External boundary`.

## C05 — Internally stale Provider API and QDOS contract

### C05.1 — Resolved wording

**Original observation:** The Source limitation paragraph says routes, headers, schema, encoding, limits and administration are undefined; the Accepted API-01 submission contract immediately below defines them.

**Current assessment:** API-01 now supplies the named contract rather than leaving the former generic undefined-contract sentence as the governing limitation.

**Evidence at the pinned revision:** `docs/frd/frd-09-provider-and-intermediary-routes.md — Provider API principal and contract boundary`.

### C05.2 — Resolved wording

**Original observation:** The QDOS route policy is labelled v3 and the Triage classification contract v4, whereas the reviewed code constants are route v4 and classification v5.

**Current assessment:** The document now uses the principal-wide version-1 policy owners. Do not update it mechanically to the old review’s v4/v5 target: that target is itself historical.

**Evidence at the pinned revision:** `docs/frd/frd-09-provider-and-intermediary-routes.md; docs/principal-rules-and-mappings/qdos.md`.

## C06 — Internally conflicting operator-experience contract

### C06.1 — Still outstanding

**Original observation:** The Case workspace says another editor's holder and expiry are shown. FRD-01 says non-holders are never given a time because the holder can keep renewing the lease.

**Current assessment:** Expiry-display wording still conflicts with a continuously renewed edit session for which non-holders receive no reliable availability time.

**Evidence at the pinned revision:** `docs/frd/frd-12-operator-experience.md — Case workspace; docs/frd/frd-01-case-identity-and-lifecycle.md — Case edit authority and recovery`.

### C06.2 — Still outstanding

**Original observation:** The route description still sends users to image records from Not-ready Image-initiated rows. Its own later D38 rule puts those rows in Awaiting instruction and reserves Not ready for formal instructed Cases.

**Current assessment:** The old Not-ready Image-initiated route wording remains inconsistent with Awaiting instruction and formal-Case-only Not ready.

**Evidence at the pinned revision:** `docs/frd/frd-12-operator-experience.md — Shell and routes; Cases queues`.

### C06.3 — Still outstanding

**Original observation:** Explicit display examples include Image intake registered and Blocked intake chips, contrary to the operator-facing ban on intake and the Blocked display mapping.

**Current assessment:** Internal intake labels remain in staff-facing examples despite CONTEXT and design mappings.

**Evidence at the pinned revision:** `docs/frd/frd-12-operator-experience.md — display examples; CONTEXT.md — Interface vocabulary`.

### C06.4 — Still outstanding

**Original observation:** The blanket rule permits disabled controls only for ticketed integration seams, while the same FRD requires disabled real actions for missing Engineer's Value, absent address values and per-Principal API permission. Operator-notes also requires visible disabled future-permitted actions.

**Current assessment:** Separate an unfinished integration preview from an implemented action disabled for eligibility, permission or missing values. The blanket sentence conflicts with its own required real controls.

**Evidence at the pinned revision:** `docs/frd/frd-12-operator-experience.md — disabled-control rule`.

## C07 — Cross-document Audit allocation conflict

### C07.1 — Resolved wording

**Original observation:** Says missing/ambiguous standalone Audit evidence withholds only the later Audit reference, referring to CLAUDE.md. FRD-01 requires the original report and determinable verdict before that email-route Audit can allocate a Case/reference.

**Current assessment:** The QDOS companion and FRD-01 now both allocate the normal Case/PO before the evidence-dependent Audit reference. FRD-02 is the remaining conflicting owner, not this old pair.

**Evidence at the pinned revision:** `docs/principal-rules-and-mappings/qdos.md — Case type; docs/frd/frd-01-case-identity-and-lifecycle.md`.

## D01 — Incomplete partial-supersession index

### D01.1 — Resolved wording

**Original observation:** Defines current architecture as the accepted set, but lists ADR-0004 staff-MCP authentication and ADR-0007 deployment without their later partial supersession.

**Current assessment:** The index now identifies partial supersession for ADR-0004 and ADR-0007.

**Evidence at the pinned revision:** `docs/adr/README.md`.

### D01.2 — Partially resolved

**Original observation:** ADR-0002's table entry points to ADR-0032, although the same index marks 0032 superseded by 0033.

**Current assessment:** The ADR-0002 pointer still requires a current transitive view through ADR-0032 to ADR-0033, not a claim that the intermediate record is active.

**Evidence at the pinned revision:** `docs/adr/README.md; docs/adr/0002-dotnet-modular-monolith-on-azure.md`.

## D02 — Partially superseded ADR with misleading current scope

### D02.1 — Still outstanding

**Original observation:** Remains accepted with original App Service, environment, MCP and activation assumptions that subsequent decisions changed; existing partial-supersession guidance does not map all changed clauses to the current decisions.

**Current assessment:** ADR-0002 is still a mixed bundle. Its active clauses must be distinguishable from replaced hosting/environment/identity/scheduling assumptions without rewriting its historical rationale.

**Evidence at the pinned revision:** `docs/adr/0002-dotnet-modular-monolith-on-azure.md — Status; Decisions`.

### D02.2 — Still outstanding

**Original observation:** The trigger/polling supersession pointer stops at ADR-0032 rather than its replacement ADR-0033.

**Current assessment:** Follow the scheduling successor chain through ADR-0033 in the current-decision view. A historical link to ADR-0032 may remain as provenance.

**Evidence at the pinned revision:** `docs/adr/0002-dotnet-modular-monolith-on-azure.md; docs/adr/0033-warm-unified-work-queue-for-five-second-intake.md`.

## D03 — Unmarked partial supersession of staff MCP

### D03.1 — Resolved wording

**Original observation:** Still presents the per-staff MCP authentication model as an accepted current decision with no superseding metadata, despite ADR-0011 explicitly replacing that boundary with the Automation Actor.

**Current assessment:** ADR-0004 now marks the staff-MCP boundary as superseded by ADR-0011. Its separate Provider API decision survives.

**Evidence at the pinned revision:** `docs/adr/0004-provider-api-and-staff-mcp-authentication.md`.

## D04 — Unmarked partial supersession of deployment model

### D04.1 — Resolved wording

**Original observation:** Retains the three-environment development/test/production model even though ADR-0014 selects local development plus production.

**Current assessment:** The current partial-supersession note identifies ADR-0014 as the environment successor.

**Evidence at the pinned revision:** `docs/adr/0007-direct-terminal-azure-deployment.md`.

### D04.2 — Resolved wording

**Original observation:** Retains App Service/F1-B1/Web ZIP assumptions replaced by the Container Apps/OCI Web-host decision.

**Current assessment:** The partial-supersession note identifies the Container Apps/OCI successor. Release workstation policy subsequently reaches ADR-0039 through ADR-0037.

**Evidence at the pinned revision:** `docs/adr/0007-direct-terminal-azure-deployment.md; docs/adr/0039-windows-and-linux-release-workstations.md`.

### D04.3 — Still outstanding

**Original observation:** Its original alpha recovery-proof requirement is not reconciled with the current PRD marking OPS-09 deferred and not a release gate.

**Current assessment:** The surviving recovery/alpha-gate implication must be scoped against the current PRD’s explicitly non-blocking OPS-09. Do not resurrect it through a legacy acceptance runner.

**Evidence at the pinned revision:** `docs/adr/0007-direct-terminal-azure-deployment.md; docs/prd/pegasus-product.md — Quality; scripts/Invoke-QdosAlphaAcceptance.ps1`.

## D05 — Supersession metadata disagrees with decision prose

### D05.1 — Resolved wording

**Original observation:** ADR-0011 declares that it replaces the earlier staff-MCP boundary, ADR-0014 changes the earlier environment policy, and ADR-0015 changes the earlier Web hosting/deployment policy; their supersedes arrays remain empty.

**Current assessment:** The three successor records now populate their supersedes relationships. Other missing or overbroad relationships remain elsewhere in the ADR graph.

**Evidence at the pinned revision:** `docs/adr/0011-restrict-mcp-to-automation-actor.md; docs/adr/0014-local-to-production-deployment.md; docs/adr/0015-host-web-on-container-apps-consumption.md`.

## E01 — Obsolete executable session plan

### E01.1 — Resolved wording

**Original observation:** Still queues PR #592 as unmerged work. GitHub records that PR as merged on 2026-08-28, so the plan cannot be used as a current resume instruction. Its old branch/board snapshot has no clear historical-only status.

**Current assessment:** The resume plan now explicitly says historical session evidence, retired from execution, and forbids replaying its worktrees/merges as authority.

**Evidence at the pinned revision:** `.zcode/plans/plan-sess_18294ee2-592f-4647-910c-54c5a5e0a0ab.md — opening`.

## E02 — Completed fix still presented as a plan

### E02.1 — Resolved wording

**Original observation:** Proposes changing Kanmer's cwd to .worktrees/kanmer, but the reviewed .zcode/config.json already contains that corrected configuration. The plan has no completed/historical disposition.

**Current assessment:** The configuration plan now has the same explicit retirement warning. It correctly does not claim a fresh process restart was verified.

**Evidence at the pinned revision:** `.zcode/plans/plan-sess_485ec446-cbcd-46a2-bb5a-511a9255e9c5.md — opening`.

## F01 — Copied Kanmer skill contains foreign repository assumptions

### F01.1 — Still outstanding

**Original observation:** Says this repository uses docs/product/prd/**, docs/functional/frd/** and docs/architecture/adr/**, while Pegasus uses docs/prd/, docs/frd/ and docs/adr/.

**Current assessment:** The copied skill still says this repository uses the Kanmer-source product/functional/architecture subtrees. Keep only resolved target-board globs and Pegasus navigation.

**Evidence at the pinned revision:** `.agents/skills/kanmer-docs/SKILL.md — Where the documents live`.

### F01.2 — Partially resolved

**Original observation:** Refers to this project's FRD-014 R2/R8b shaping history and to docs/contributing/doc-structure.md as present, importing Kanmer-source context into the Pegasus consumer.

**Current assessment:** The FRD-014 R2/R8b foreign-project example remains. The doc-structure asset is now target-neutral; do not mistake its generation template for an assertion that the target file already exists.

**Evidence at the pinned revision:** `.agents/skills/kanmer-docs/SKILL.md — Granularity test; .agents/skills/kanmer-docs/assets/doc-structure.md`.

### F01.3 — Partially resolved

**Original observation:** States a fixed leaving-Backlog link-or-create gate without qualifying it by the current profile-resolved model; also calls board.yml the source of truth where the managed instructions specifically warn that its profiles block is not the effective requirement set.

**Current assessment:** The main requirements paragraph is profile-resolved, but the description and Backlog workflow still imply every ticket must link/create a document. Remove that residue upstream and reconcile mirrors.

**Evidence at the pinned revision:** `.agents/skills/kanmer-docs/SKILL.md — description; Workflow; Governing-document requirements`.

## F02 — Copied documentation template retains wrong proof target

### F02.1 — Resolved wording

**Original observation:** Describes proof as evidence gathered on merged main, contradicting exact-PR-merge-SHA verification on the configured integration target.

**Current assessment:** The proof row now uses the exact configured integration-branch SHA after review and merge.

**Evidence at the pinned revision:** `.agents/skills/kanmer-docs/assets/doc-structure.md — Ticket documents`.

## F03 — Obsolete proof templates

### F03.1 — Resolved wording

**Original observation:** All three proof templates say merged main rather than the reviewed PR's exact merge SHA.

**Current assessment:** All three templates now use the configured integration branch rather than requiring main.

**Evidence at the pinned revision:** `.agents/skills/kanmer-execute/assets/proof-template.md; proof-test-template.md; proof-visual-template.md`.

### F03.2 — Partially resolved

**Original observation:** They omit the versioned proof-record frontmatter and attempt history required by the current verification skill: kind, merged_sha, environment, verified_at, result and attempts.

**Current assessment:** All three now add identity and attempt instructions in prose, but none provides the required versioned proof-record YAML frontmatter. Align them with kanmer-verify without creating a second format.

**Evidence at the pinned revision:** `.agents/skills/kanmer-execute/assets/proof-*.md; .agents/skills/kanmer-verify/SKILL.md — Whole-file proof record`.

## F04 — Inconsistent verification-waiver wording

### F04.1 — Resolved wording

**Original observation:** Some clauses allow only PASS to reach Done and say never move a non-PASS ticket to Done. Other clauses and the handoff explicitly permit the operator-only WAIVED_BY_OPERATOR outcome to reach Done.

**Current assessment:** WAIVED_BY_OPERATOR is now explicitly a non-PASS human disposition; terminal retirement stays Verifying and is archived. Only PASS reaches Done.

**Evidence at the pinned revision:** `.agents/skills/kanmer-verify/SKILL.md — Whole-file proof record; Terminal retirement; final handoff`.

## Undated earlier review — 30 observations

The undated review contains terse and sometimes incomplete observations against an older tree. The following dispositions preserve that limitation rather than inventing missing assertions.

### U01 — Partially resolved

**Original:** Production is both “not deployed” and deployed. /C:/Users/PC/Documents/GitHub/pegasus/docs/architecture.md:41, but later says the production route ran on 2026-08-02. /C:/Users/PC/Documents/GitHub/pegasus/ docs/architecture.md:454 The same stale section says no production trigger/provider call ran, conflicting with /C:/Users/PC/Documents/GitHub/pegasus/docs/operations.md:813 and /C:/Users/PC/Documents/ GitHub/pegasus/.azure/deployment-plan.md:82.

**Assessment:** Old docs/architecture.md is not the current owner. Current architecture acknowledges deployment but still mixes historical and source snapshots; retain the scope-qualified findings in the new audit.

### U02 — Resolved wording

**Original:** The Box folder is simultaneously a production custody root and a disposable test-only target. /C:/Users/PC/Documents/GitHub/pegasus/docs/operations.md:234 and again records it as production custody, while its own approval matrix confines it to /C:/Users/PC/Documents/GitHub/pegasus/docs/operations.md:651. NOW and open decisions agree it is not approved for real cases: /C:/Users/PC/Documents/GitHub/pegasus/ NOW.md:7, /C:/Users/PC/Documents/GitHub/pegasus/docs/open-decisions.md:41.

**Assessment:** The current operations file separates production root 405543781910 from controlled test boundary 392761581105; neither is standing write authority.

### U03 — Resolved wording

**Original:** The active Box decision requires a superseded mechanism. Requirements say the in-house upload route /C:/Users/PC/Documents/GitHub/pegasus/docs/requirements.md:154, but the open decision and NOW still require a “Box File Request template.” The current Web UI also still offers /C:/Users/PC/Documents/GitHub/pegasus/src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml:142.

**Assessment:** The current FRD-02 and architecture retire Box File Requests in favor of the bounded in-house upload route. No live UI execution was performed here.

### U04 — Partially resolved

**Original:** Alpha acceptance gates disagree. NOW says only OPS-23/OPS-25 close alpha and calls MCP-01–04 and MAIL-14/16 non-blocking, while the capability inventory makes /C:/Users/PC/Documents/GitHub/pegasus/docs/ capabilities.md:114 and /C:/Users/PC/Documents/GitHub/pegasus/docs/capabilities.md:178 required before alpha acceptance.

**Assessment:** The old NOW.md comparison is obsolete; the current acceptance script still equates alpha allocation with mandatory evidence and requires 2,000 cases. That is a fresh, concrete coupling to fix.

### U05 — Partially resolved

**Original:** Current authentication state is contradictory. Architecture and design describe authenticated Intake actions, but /C:/Users/PC/Documents/GitHub/pegasus/docs/architecture.md:330 and /C:/Users/PC/Documents/ GitHub/pegasus/design/README.md:324. Current source supports the authenticated description.

**Assessment:** Old paths are retired and current docs describe authenticated intake. This audit did not rerun access-control tests; conflicting historical manual-upload custody reasoning remains in open-decisions.

### U06 — Resolved wording

**Original:** The approval matrix bans the documented production release route. /C:/Users/PC/Documents/GitHub/pegasus/docs/operations.md:653, despite the same document recording only local plus production environments and an executed production release.

**Assessment:** The current approval matrix explicitly permits separately authorized production deployment under the local/production-only model.

### U07 — Resolved wording

**Original:** Azure SQL bootstrap both did and did not run. /C:/Users/PC/Documents/GitHub/pegasus/docs/operations.md:258; the /C:/Users/PC/Documents/GitHub/pegasus/.azure/deployment-plan.md:53.

**Assessment:** Current operations contains dated bootstrap execution records; retain those observations rather than treating an earlier implementation-only statement as current.

### U08 — Partially resolved

**Original:** Telemetry is both absent and live/correlated. /C:/Users/PC/Documents/GitHub/pegasus/docs/operations.md:771, while its production section and deployment plan claim live monitoring and /C:/Users/PC/ Documents/GitHub/pegasus/.azure/deployment-plan.md:46. The documented deployed Web revision has no corresponding Web telemetry registration.

**Assessment:** Operations records Web telemetry activation at release 19. Full-working-day retention remains unproved; this is not the old missing-exporter defect.

### U09 — Not fully reverified

**Original:** The “current” deployment validator cannot execute. Operations names it as a release validator, but /C:/Users/PC/Documents/GitHub/pegasus/scripts/Test-AzureDeploymentPlan.ps1:22 and retired scripts. This is documentation-to-current-worktree drift.

**Assessment:** The deployment validator was not executed in this audit. Its old worktree-specific failure cannot be certified fixed or reproduced from a dated review; current CI references the validator.

### U10 — Partially resolved

**Original:** Repository process directly conflicts with itself. /C:/Users/PC/Documents/GitHub/pegasus/AGENTS.md:14, while Operations prescribes a full /C:/Users/PC/Documents/GitHub/pegasus/docs/operations.md:957. The deployment plan also says /C:/Users/PC/Documents/GitHub/pegasus/.azure/deployment-plan.md:76 release work.

**Assessment:** The old NOW/process snapshot is obsolete, but AGENTS, engineering and release skills still duplicate procedure and authority. Current recommendations address those remaining owners.

### U11 — Partially resolved

**Original:** A deferred AI capability is specified as alpha UI behavior. /C:/Users/PC/Documents/GitHub/pegasus/design/product/ui-spec.md:112 in the alpha Case flow, but /C:/Users/PC/Documents/GitHub/pegasus/design/ product/traceability-matrix.md:195, explicitly with no alpha surface.

**Assessment:** Old design/product paths are no longer the active UI contract. Current AI job behavior is governed by FRD-10/11 and ADR-0035; unfinished activation is not the same as an excluded product capability.

### U12 — Resolved wording

**Original:** /C:/Users/PC/Documents/GitHub/pegasus/docs/open-decisions.md:63 still assumes isolated Azure Development and Production targets; /C:/Users/PC/Documents/GitHub/pegasus/docs/adr/0014-local-to-production- deployment.md:13 prohibits Azure development/test environments.

**Assessment:** The current runbook and ADR-0014 use isolated local development and production only.

### U13 — Not fully reverified

**Original:** /C:/Users/PC/Documents/GitHub/pegasus/docs/adr/0002-dotnet-modular-monolith-on-azure.md:275, while /C:/Users/PC/Documents/GitHub/pegasus/docs/adr/0013-qdos-alpha-implementation-contract.md:22.

**Assessment:** The original bullet gives only two locations and omits the actual disputed assertion. ADR-0002/0013 were reviewed, but a specific unnamed historical discrepancy cannot be independently reconstructed from this bullet.

### U14 — Partially resolved

**Original:** /C:/Users/PC/Documents/GitHub/pegasus/docs/architecture.md:282, then says Linux uses a SQL Server container. /C:/Users/PC/Documents/GitHub/pegasus/docs/architecture.md:480

**Assessment:** Current architecture describes Linux SQL Server containers in prose; its system diagram still labels only LocalDB for local SQL.

### U15 — Resolved wording

**Original:** /C:/Users/PC/Documents/GitHub/pegasus/CONTEXT.md:71, but requirements make it reversible. /C:/Users/PC/Documents/GitHub/pegasus/docs/requirements.md:204

**Assessment:** CONTEXT now makes prereport association reversible with a reason and preserved history.

### U16 — Not fully reverified

**Original:** Design’s logo, icon, and colour-token records each disagree internally: /C:/Users/PC/Documents/GitHub/pegasus/design/README.md:159, /C:/Users/PC/Documents/GitHub/pegasus/design/README.md:193, and /C:/ Users/PC/Documents/GitHub/pegasus/design/README.md:84 versus the /C:/Users/PC/Documents/GitHub/pegasus/design/product/traceability-matrix.md:20.

**Assessment:** A fresh visual/token audit of every design asset was not performed. Current authority is docs/design/README.md and FRD-12, not the old comparison paths.

### U17 — Resolved wording

**Original:** NOW refers to “two failing integration tests above,” but its Next list names only a Linux architecture-test fix. /C:/Users/PC/Documents/GitHub/pegasus/NOW.md:21

**Assessment:** NOW.md is not the current work queue or claim owner; do not recreate its expired next-work list.

### U18 — Resolved wording

**Original:** Document-extraction architecture says CFB traversal is pending, while its own /C:/Users/PC/Documents/GitHub/pegasus/workspaces/document-extraction/docs/compatibility/feature-matrix.md:15 marks traversal locally verified. /C:/Users/PC/Documents/GitHub/pegasus/workspaces/document-extraction/docs/architecture.md:121

**Assessment:** The document-extraction workspace is retired. Its historical implementation matrix is not a current application requirement; preserve import provenance.

### U19 — Resolved wording

**Original:** That workspace also calls retired /Intake/Upload the current Pegasus path. /C:/Users/PC/Documents/GitHub/pegasus/workspaces/document-extraction/docs/architecture.md:71

**Assessment:** The workspace’s old route guidance is retired with the import. Current manual upload is routed through the application docs and scripts.

### U20 — Not fully reverified

**Original:** Renderer templates claim GIF support, but its architecture and validator permit PNG/JPEG/WebP only. /C:/Users/PC/Documents/GitHub/pegasus/workspaces/report-renderer/docs/TEMPLATES.md:381

**Assessment:** The standalone renderer workspace is retired, making this an obsolete workspace instruction. Current supported renderer media types were not re-executed; no new GIF-support claim is made.

### U21 — Resolved wording

**Original:** AI Centre’s Collision Brain operations doc calls 2026-07-29 the latest evidence while /C:/Users/PC/Documents/GitHub/pegasus/workspaces/ai-centre/services/collision-brain/docs/provider-evaluation.md:3.

**Assessment:** The old AI-centre workspace is not a current application owner. Retain dated research as evidence rather than refreshing its historical latest statement into current authority.

### U22 — Not fully reverified

**Original:** Protected skill packages claim a self-contained set but require a missing vehicle-valuation package. /C:/Users/PC/Documents/GitHub/pegasus/workspaces/ai-centre/skills/README.md:22, /C:/Users/PC/Documents/ GitHub/pegasus/workspaces/ai-centre/skills/vehicle-history-check/SKILL.md:20

**Assessment:** The old protected AI-centre package set is not an active Pegasus workspace. No independent package-completeness verification of that retired set was performed.

### U23 — Not fully reverified

**Original:** vehicle-history-check directs output to a renderer, then says it performs no PDF rendering. /C:/Users/PC/Documents/GitHub/pegasus/workspaces/ai-centre/skills/vehicle-history-check/SKILL.md:83, /C:/Users/ PC/Documents/GitHub/pegasus/workspaces/ai-centre/skills/vehicle-history-check/SKILL.md:88

**Assessment:** This concerns a retired AI-centre skill, not an active renderer owner; no fresh runtime test of that skill was performed.

### U24 — Partially resolved

**Original:** EVA handoff report says no serializer/export/caller exists; the current worktree has the deterministic handoff path. /C:/Users/PC/Documents/GitHub/pegasus/docs/reference/reports/eva-api-preference-and- focused-qdos-alpha-json-handoff.md:47

**Assessment:** Treat the reference report as dated provenance, not a current no-caller assertion. Current FRD-07 owns the supported handoff; the reference corpus was not exhaustively rewritten or link-validated.

### U25 — Partially resolved

**Original:** Raw EVA notes tell readers to treat a full vendor API as the current specification, contrary to the manual-handoff/permission boundary. /C:/Users/PC/Documents/GitHub/pegasus/docs/reference/eva_information/ eva_information.md:15

**Assessment:** A vendor schema is source evidence, not operation authority. Current FRD-07/ADR-0038 specify the narrow staff action; retain the raw vendor note without importing its broader claims.

### U26 — Partially resolved

**Original:** Chaser and several intake/case/Triage reports still claim absent or unsettled callers that now exist. The affected reports include /C:/Users/PC/Documents/GitHub/pegasus/docs/reference/reports/manual- chaser-action-history-and-channel-boundaries.md:19, /C:/Users/PC/Documents/GitHub/pegasus/docs/reference/reports/parser-boundary-and-version-provenance.md:20, and /C:/Users/PC/Documents/GitHub/pegasus/ docs/reference/reports/ui-required-fields-options-and-settings.md:5.

**Assessment:** Historical reports should not become active status owners. Current FRDs now define these workflows; their residual contradictions are listed separately in this audit.

### U27 — Partially resolved

**Original:** Two reference reports permit image-led Case/Principal allocation or Blocked intake, conflicting with the settled pre-Case Image Intake model. /C:/Users/PC/Documents/GitHub/pegasus/docs/reference/reports/ historical-correspondence-without-case-reconstruction.md:13

**Assessment:** Current PRD/FRD-01/02 own image-origin and allocation semantics. Quarantine older report interpretations behind explicit provenance; do not edit source evidence into a new requirement.

### U28 — Partially resolved

**Original:** Reference UI labels conflict with current names such as Post-report complete and New cases today. /C:/Users/PC/Documents/GitHub/pegasus/docs/reference/reports/ui-required-fields-options-and-settings.md:35

**Assessment:** Current UI labels are routed through CONTEXT, design and FRD-12. Repair current internal-label examples, not every historical label in supplied references.

### U29 — Partially resolved

**Original:** A reference report authorises raw-image submission to a multimodal assistant, conflicting with the explicit approval/corpus-egress rules. /C:/Users/PC/Documents/GitHub/pegasus/docs/reference/reports/ repository-data-authority.md:10

**Assessment:** A reference report cannot authorize corpus egress. Keep explicit source-data and exact-target approval boundaries in the engineering/operation owner.

### U30 — Not fully reverified

**Original:** /C:/Users/PC/Documents/GitHub/pegasus/docs/reference/workproviders-and-repairers/emailevalsaddresses.md:1 says it is “not committed,” but it is tracked.

**Assessment:** The historic not-committed claim was not reverified at its migrated reference location. A tracked raw note may retain that original statement as historical evidence; an active inventory may not repeat it.
