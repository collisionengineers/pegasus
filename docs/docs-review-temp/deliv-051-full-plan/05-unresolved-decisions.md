# Operator decisions — resolved

Temporary working reference for DELIV-051. Amended from the operator answers on 2026-09-08. Canonical documents now carry the requirements; this folder is not an additional authority.

All fourteen answers have been actioned. No unanswered item remains. The operator requested no separate record for Q-13.

| Answer | Accepted disposition | Durable owners |
| --- | --- | --- |
| Q-01 | FRD-04 D15: generated temporary password visibly revealed to Administrator; no automatic email. | FRD-04; FRD-12 |
| Q-02 | Triage completes by deciding its outcome. Optional Reply with outcome opens an editable preset email and retains correspondence. | FRD-03; FRD-08; FRD-12; glossary; PRD; TRI-05 |
| Q-03 | Valuation button creates MarketResearch job. External Claude Cowork and connector obtain and attach research files; Automation Actor marks Completed. | FRD-06; FRD-10; FRD-11; ADR-0035 |
| Q-04 | Triage uses the regular Case result shape with T-reference. | FRD-09 |
| Q-05 | Audit and Inspection + Audit are active and in scope, using the shared renderer contract. | PRD; FRD-01; FRD-11 |
| Q-06 | Provider coordinates and mailbox-scoped business duplicate identity remain separate; preserve each receipt. | FRD-08; ADR-0024/0044 |
| Q-07 | Box custody is separate from derived caches/logical reads, using existing stores and verified handoff. | FRD-05; ADR-0029/0045 |
| Q-08 | No pre-Engineer valuation prerequisite can depend on an Engineer-only edit. | FRD-01; FRD-06 |
| Q-09 | No terminally closed Case. Completed enters Query on query receipt/attachment and returns to Completed on reply. | FRD-01; FRD-05/06/08/11/12; design; glossary; PRD |
| Q-10 | Accepted route rules must not collide. Unexpected overlap is a visible fail-closed defect, not a scoring/precedence choice. | FRD-08; FRD-09 |
| Q-11 | Templates have already been supplied; use retained assets without inventing wording. | FRD-11 |
| Q-12 | Warm configuration is operator choice; current test data is disposable. No instruction to mutate the estate. | Operations; runbook; ADR-0015/0030 |
| Q-14 | Accept scoped per-Engineer SQL/Data Protection credentials and key-ring recovery contract. | FRD-04/06; ADR-0002/0043 |

All new skills are vetoed. Useful procedures remain in the runbook and existing release/wipe/Razor owners. Application implementation and live proof are not created by changing specifications.
