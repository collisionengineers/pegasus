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

## Additional research from a previous implementation

### Provenance and scope

The following material comes from a **previous implementation** reviewed as operational evidence. Its taxonomy and Outlook folder tree are not Pegasus authorities. It is useful for classification criteria, ambiguity behaviour, routing separation, and acceptance examples only.

### Historical detailed taxonomy and routing evidence

The previous implementation used a deterministic classifier with a versioned taxonomy (`version 4`). Its top-level categories included receiving work, query, billing, non-actionable, other, case update, cancellation, pre-instruction and website enquiry. Detailed subtypes included existing-provider instruction/audit/diminution, new-client work, existing-work query, new enquiry, website enquiry, billing request, payment remittance, case summary, acknowledgement, images received, general update, cancellation and pre-instruction directions.

Its Outlook destinations were more granular than Pegasus's intended `Receiving work / Query / Other / Needs sorting / Triage` model: instructions, audits, diminution, new clients, case queries, enquiries, billing, pre-instructions, no-action, images, cancellations, case updates and other each had separate folder destinations.

This is evidence that Pegasus should keep **detailed classification** separate from **operational destination**. The historical folder hierarchy should not be copied into Pegasus as taxonomy truth.

### Suggested Pegasus translation for product-owner review

The prior operational behaviour supports the following **research recommendation**, subject to the canonical Pegasus mapping being explicitly accepted:

| Pegasus destination | Detailed classifications that naturally fit |
| --- | --- |
| **Receiving work** | instructions, audits, diminution, new-client work; potentially case updates/images when Pegasus wants them on the principal work queue |
| **Query** | existing-work queries, new enquiries, website/contact enquiries |
| **Other** | acknowledgements, summaries/no-action and genuinely unidentified `Other`; billing/payment only if Pegasus intentionally has no dedicated operational lane |
| **Needs sorting** | unresolved/ambiguous provider, unresolved or multiple case candidates, unknown classification, conflicting reference/VRM evidence |
| **Triage** | cancellation proposals, pre-instruction directions, high-risk correlation cases and other known workflows requiring an explicit human decision rather than simple filing |

The most useful distinction is: **Needs sorting = insufficient or contradictory evidence; Triage = a known workflow whose next step requires human judgement.** Keeping those meanings separate should improve queue reporting and prevent `Needs sorting` from becoming a catch-all human-work bucket.

### Classification safety criteria worth carrying over

The previous implementation deliberately failed closed:

- weak evidence, forwarded chains, auto-replies and uncertain messages were not silently promoted into work;
- the default abstention path was a safe `Other`-style outcome rather than guessing;
- replies were detected from `In-Reply-To` / `References`, with leading `RE:` only as fallback;
- `FW:` / `FWD:` was deliberately not treated as a reply because a forward can contain genuinely new work;
- a reply without fresh-work evidence leaned toward an existing-work query, while a reply containing genuinely fresh work could still be promoted;
- cancellation had precedence over instruction detection so quoted/attached old instructions did not turn a cancellation into new work;
- website enquiries required multiple independent fingerprints rather than trusting a display name or one phrase.

Case lookup was kept outside the pure text classifier. The text layer emitted reference/VRM candidates; database-aware orchestration decided whether those candidates matched live cases. This is a useful boundary for MAIL-02 because operational destination should consume a settled classification/correlation result, not reproduce live-case matching inside the mapping policy.

### Folder movement remained a separate, gated mutation

The previous implementation kept the classification-to-folder mapping in a central domain function and did not trust a browser/client to provide an arbitrary folder. Actual Outlook movement was a separate staff-initiated/gated operation with an explicit mutation kill-switch and success/failure recording.

For Pegasus, MAIL-02 should therefore remain a **pure Core destination policy**. It should not perform Outlook mutation and should not accept arbitrary UI-provided destinations. Folder recommendation/movement should consume the canonical MAIL-02 result in their owning tickets.

### Additional acceptance implications

Useful regression cases for the eventual MAIL-02 test suite include:

- ambiguous provider -> `Needs sorting` rather than arbitrary queue selection;
- multiple/conflicting case candidates -> `Needs sorting`;
- known cancellation/pre-instruction workflow -> `Triage` when that mapping is accepted;
- forwarded new work remains eligible for `Receiving work`;
- reply with no fresh-work evidence -> `Query` where taxonomy confirms that subtype;
- classifier abstention/unknown outcome -> deterministic safe destination, not an inferred folder;
- one central mapping result reused by UI, automation and folder recommendation callers.

These examples supplement the existing Pegasus research; they do **not** resolve the outstanding product-owner requirement for the exact exhaustive mapping matrix.
