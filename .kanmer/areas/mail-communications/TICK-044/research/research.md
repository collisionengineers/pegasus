# Research — TICK-044: MAIL-02 operational routing

## Question

What policy, caller, persistence boundary, and acceptance evidence already exist for mapping the settled detailed mail taxonomy to Receiving work, Queries, Other, Needs sorting, or the separate Triage workflow, and what remains unresolved before planning?

## Findings

- The settled taxonomy is already Core-owned in `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`: eight Received families, four Sent families, mirrored Reply context, and reasoned `Other`. `MailCategory` deliberately contains no queue, Triage-route, or Outlook-destination property.
- `docs/frd/frd-08-email-mailbox-and-background-processing.md#settled-mailbox-taxonomy-and-correction` is the sole behavior owner and explicitly makes classification, application queue, Triage routing, and Outlook folder destination separate facts. It does not define the MAIL-02 family/subtype-to-destination matrix.
- `docs/operator-notes.md#confirmed-mailbox-categorisation` records the operator-confirmed taxonomy and separation of concerns, but does not confirm the operational-destination mapping. Its meaning is protected by AGENTS.md, so research cannot infer or add that mapping.
- The only active automatic category policy is route-owned QDOS policy `qdos_mail_classification` v3 in `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs`. Its accepted predicates classify automatic replies, QDOS Triage requests, and QDOS Audit/Inspection instructions; zero matches are `Unclassified` and simultaneous matches are `Ambiguous`.
- `tests/Pegasus.Core.Tests/Intake/Classification/MailTaxonomyTests.cs` enforces both the exact taxonomy and the architectural separation from queues, Triage routing, and folder destination. MAIL-02 therefore needs a separate Core policy rather than extending `MailCategory` with mixed concerns.
- Classification decisions are already durably persisted with source/policy/evidence metadata through the intake decision model and exposed on retained-mail detail. `src/Pegasus.Core/Intake/RetainedMail.cs` and `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` expose only classification outcome and route disposition today; there is no operational-destination projection or filter.
- The current mail message page is explicitly read-only (`src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`) and labels only classification outcome, route disposition, and processing outcome. UI-14 and the wider workspace are separately allocated Next / 0.3.0; MAIL-02 should own policy, not duplicate UI query behavior.
- Triage behavior is already separate and fail-closed in `docs/frd/frd-03-triage.md`: an accepted route policy or authorised manual classification may identify Triage, but missing VRM stays Needs sorting. The QDOS intake-to-Triage matcher remains deliberately inactive in `docs/open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display` pending accepted predicates.
- `docs/open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display` keeps unaccepted route predicates, multi-rule precedence/confidence behavior, policy control/re-evaluation, genuine holdout thresholds, and exact Graph scopes inactive. MAIL-02 cannot treat allocation or local tests as activation evidence.
- Git history confirms MAIL-21/22 landed through PRs #391/#392 and earlier QDOS classification work; no subsequent commit introduces a MAIL-02 mapping. `docs/operations.md` records only local QDOS cohort evidence, explicitly not labelled holdout, deployment, live verification, or operator acceptance.
- EPIC-006 requires one canonical Core implementation reused by UI, infrastructure, and Automation Actor callers, and forbids local-alpha Outlook mutation. EPIC-003 adds UI context without moving durable ownership out of the mail domain. TICK-064/MAIL-23 follows this ticket and will separately map the resulting operational policy to designated Outlook folders.

## Implications

- Planning is blocked until the product owner confirms one canonical, exhaustive category/subtype-to-operational-destination matrix, including ambiguity/unclassified behavior. The repository currently supplies no authoritative answer to that business question.
- The simplest compliant shape is a Core-owned mapping policy that consumes the existing classification decision and returns a distinct operational destination; it must not alter the settled taxonomy or put queue/folder facts on `MailCategory`.
- Fail-closed defaults should preserve `Ambiguous` and `Unclassified` as Needs sorting, and Triage must remain a separate workflow with its existing registration and accepted-matcher gates.
- Persistence/projection changes are needed only if the destination must be stored rather than deterministically derived. The plan must choose this from the confirmed correction/history contract and reuse the existing decision/audit owner.
- MAIL-23 may consume MAIL-02's destination result for folder recommendations, but this ticket must not perform or authorize Outlook writes. UI-14 and automation callers must consume the same Core result rather than reproduce the mapping.
- Activation evidence must name the real caller and accepted genuine cohort/holdout; build, registration, local corpus counts, or an unactivated composition are insufficient.

## Open questions

- Product-owner confirmation of the exact mapping and activation boundary is recorded in `open-questions`; it must not be silently assumed by the plan.
