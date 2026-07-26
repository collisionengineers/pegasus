# Report delivery and post-report lifecycle

**Operator decision:** ADR-0008 was rejected for v2 and dealt with on 2026-07-24. Its direct predecessor PLAN-002/TKT-094/095/096 case-done chain was dealt with at the same time.

**Legacy sources dealt with:** ADR-0008 (`../dealt-with/rejected/0008-tool-boundary-ends-at-eva-handoff.md`) and the related case-done lifecycle bundle (`../dealt-with/rejected/0008-case-done-lifecycle/README.md`).

No new legacy finding was accepted. Current v2 requirements, architecture, and delivery plans already cover the valid product boundary. The predecessor terminal model and detector mechanics differ materially and are not adopted.

## Current v2 position

### Product boundary already required

- The [operator process](../../operator-notes/collision-engineers-process/process-overview.md) includes report preparation followed by post-report queries and disputes.
- The [questionnaire](../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md) requires the first release to progress through inspection/report preparation and post-report work. A case closes through post-report completion, provider cancellation, Collision Engineers rejection, or the distinct wrong-principal outcome `Created in error`.
- The [remainder-delivery finish line](../../plans/remainder-delivery/README.md) already carries operators through report and post-report activity. This is broader and more precise than stopping at EVA handoff.
- Triage is optional stored pre-case work that may never become a case. Its states, binary finding, exact reply-chain completion evidence, correction/reopen behavior, and optional later-case association are settled in the questionnaire.
- EVA remains authoritative for Engineer assignment, estimating, valuation, and report generation in the first release. The planned v2 handoff is an operator-approved JSON and image bundle; direct EVA API use and eventual replacement are deferred.

### Current architecture

The accepted [.NET modular-monolith architecture](../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md) requires one Core workflow owner. Web and Worker are thin callers; Graph, Box, and EVA belong behind Infrastructure adapters. An external event may be translated into a Core command, but an adapter cannot independently decide case workflow or terminal outcome.

The planned boundary is:

`authorised Web/Worker evidence caller` -> `Core CaseLifecycle decision` -> `SQL state and permanent action history`

Box is authoritative for original-file custody. SQL is authoritative for workflow, relationships, processing state, permanent action history, and Box identifiers. A report PDF appearing in Box therefore does not automatically prove that it was sent or delivered.

## Differences from the legacy ADR and case-done chain

| Legacy decision | Current v2 treatment |
| --- | --- |
| Product responsibility continues beyond EVA handoff | Already required, but the current boundary continues through report and post-report work rather than ending at a generic delivery event. |
| Report delivery immediately creates terminal `done` | Rejected. Report-sent evidence may enter post-report work; principal and reference are already immutable from reference allocation, and terminal post-report completion is a separate business outcome. |
| One persisted numeric `done` status plus `eva_submitted` and `box_synced` | Rejected. These are predecessor status and persistence choices, not current v2 vocabulary or schema authority. |
| Staff may directly mark a report delivered and close the case | Not adopted. The authenticated action must associate one exact Outlook Sent item through the single Core lifecycle policy and retain actor/time/reason/outcome in permanent action history. A report-sent event enters post-report work rather than closing the case. There is no pre-send report review gate. |
| A Box report-named PDF proves delivery | Rejected as settled policy. Box custody and version identity are planned, but report presence is not currently authoritative proof of sending or delivery. |
| A Sent Items match proves delivery | Partly adopted for sent evidence, not delivery or receipt: one exact item in the Sent Items folder of an Administrator-approved mailbox proves the report-sent event, with Outlook `sentDateTime` authoritative. Automatic matching remains open in the combined mailbox/email research. When it is absent or ambiguous, staff may link the exact item with a required reason. Sent Items access still needs separate security and Graph-scope approval before implementation. |
| EVA polling proves delivery | Not adopted. The first release has a manual EVA export bundle; direct EVA API use, polling, and replacement are deferred. |
| Each detector may trigger the terminal transition | Rejected architecturally. Any later evidence adapter must call one Core lifecycle owner; idempotency at an adapter does not grant it workflow authority. |
| Triage is stored pre-case work that may never become a case | Already current. Its separate mutable workflow and exact reply-chain completion rule are settled; no Triage production caller exists yet. |
| Report assessment and authoring stay outside v2 initially | Already current. EVA remains authoritative for reports until an approved replacement slice exists. |
| Later inbound mail may be linked or reconstructed | Definitive related-correspondence association is required. Retroactive predecessor-case reconstruction was rejected with ADR-0022 (`../dealt-with/rejected/0022-retroactive-case-reconstruction.md`). |

## Current plans and decision gates

The [lifecycle and work-management plan](../../plans/remainder-delivery/casework/lifecycle-and-work-management.md) names planned Core `CaseLifecycle` and `CaseWork` policies, guarded Web actions, and future Worker callers. It records `Report sent` only from an explicitly associated exact Sent item and distinguishes it from later post-report completion. Principal/reference immutability is already required at reference allocation.

The exact sent-evidence source and time are settled. The combined [mailbox categorisation and email-matching research](../../plans/mailbox-categorisation-and-email-matching/README.md) must still determine:

- automatic sent-item matching predicates and exclusions;
- precedence and ambiguity rules;
- policy versioning, evidence, correction, and rollback; and
- the acceptance cohort and operational thresholds.

The [Outlook plan](../../plans/remainder-delivery/integrations/outlook-and-background-processing.md) keeps Sent Items access outside its current inbound-Inbox implementation slice until the separate permission boundary is approved. The [Box plan](../../plans/remainder-delivery/integrations/box-case-files.md) owns custody, not report-completion policy. The [EVA plan](../../plans/remainder-delivery/integrations/vehicle-data-and-eva-export.md) provides a future manual download bundle and deliberately emits no direct EVA adapter task until its mapping and readiness procedure are accepted.

## Evidence state and real callers

The lifecycle boundary is **Planned**: a reviewed sequence, owners, exclusions, and acceptance criteria exist. Report-sent and post-report transitions have a settled business contract but are not implemented or called.

The only currently **Called** intake path remains:

`POST /Intake/Upload` -> [`UploadModel.OnPostAsync`](../../../src/CollisionSpike.Web/Pages/Intake/Upload.cshtml.cs) -> [`ProcessIntake`](../../../src/CollisionSpike.Core/Intake/ProcessIntake.cs).

It creates a reviewable pre-case receipt/draft, not a lifecycle case. There is no current called lifecycle, Triage, report-sent, post-report, Box, Sent Items, or EVA path. This implementation status does not change the authoritative and planned product scope above.

## Evidence required before implementation

The report-sent path must prove through its real authorised caller that it associates one exact Outlook Sent item with exactly one case, records evidence provenance and Outlook `sentDateTime`, handles duplicate/late/conflicting signals idempotently, records the resulting action in permanent action history, and delegates the transition to Core. The manual path requires an entered reason when automatic matching is absent or ambiguous. It must distinguish report sent from post-report work complete and preserve the four current terminal outcomes. CollisionSpike detects the sent evidence; it does not send the report automatically.

Evidence for a Box file, Sent Items message, EVA response, or staff action would prove only that selected source and matching contract. It would not automatically prove external receipt, post-report completion, production deployment, or operator acceptance.

## Deferred-capability impact

Automatic Sent Items matching, broader mailbox coverage, direct EVA API use and eventual EVA replacement remain possible behind the current Graph/EVA adapters and the single Core lifecycle command after explicit product, scope, licence, security, and evidence decisions. Another report-evidence source could be considered only after its business meaning is accepted; Box presence alone is not proof.

No predecessor numeric status, `mark-done` route, Box webhook workflow rule, Sent Items subscription, EVA poller, Durable orchestration, Python client, dark feature flag, Completed/Archive status model, migration, queue, or service is introduced by this disposition.
