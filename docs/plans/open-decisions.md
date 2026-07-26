# Open decisions

Status: **Active material-ambiguity register**

Most product decisions reviewed through 2026-07-25 are settled in `PROJECT_DISCOVERY_QUESTIONNAIRE.md`; allocation is owned by the [feature maturity map](feature-maturity-map.md), V1 gaps are summarised in [remaining requirements](remaining-requirements.md), and dependency order is owned by the [delivery roadmap](delivery-roadmap.md). Conditional and `Unclear` maturity rows are visible activation states, not unanswered current-scope questions.

## Mailbox categorisation and all email matching research

Mailbox categorisation and every automatic email-matching path remain one combined open research decision. Its working evidence and decision questions belong in [the mailbox dossier](mailbox-categorisation-and-email-matching/README.md). The current taxonomy and maturity sequence are settled: V0 uses the local EML evaluator and one Core owner for live provider-specific instruction identification; V1 reuses that owner for staff-forwarded `instructions@` intake and the allocated exact report/Triage matchers; V2 expands across all four mailboxes, the detailed taxonomy, operational routing, folder suggestions/moves, general association, and email management.

The research must still settle inputs, predicates, precedence/ambiguity, correction/reversal, rule-authoring and approval authority, effective dates/versions, rollback, retained evidence, and acceptance cohorts. It must cover V0 instruction identification, V1 intake/report/Triage evidence, and V2 correspondence/association without conflating already-settled outcomes.

Until that research is accepted:

- retain each source with its stable mailbox identity and make it visible without guessing a category or case;
- route uncertainty to `Needs sorting` where the settled workflow requires staff review;
- permit the explicitly settled manual exact-item report link with a required reason and permanent action history; Triage completion still requires the exact reply-chain item found in Sent Items and has no manual-selection fallback;
- do not enable the affected V0, V1, or V2 automatic decision before that slice's predicates and acceptance evidence are approved; and
- do not add a generic rule engine, expression language, rule table, configuration screen, dormant service, or transport-specific second classifier.

This does not defer V1 automatic creation of one incomplete `Not ready` case from independently definitive accepted instructions, change exact Outlook evidence, or move the V1 automatic report/Triage match requirements to V2. It records why those matchers remain blocked until their predicates are accepted.

## V1 operator shell direction

The direction-neutral V1 requirements and exhaustive feature trace have passed independent planning review. Three shell/landing choices remain deliberately unapproved:

- [Operations-first](ui-ux/directions/operations-first.md) starts with shared office queues, due work and day/week outcomes;
- [Worklist-first](ui-ux/directions/worklist-first.md) starts with one bounded case queue; and
- [Case-first](ui-ux/directions/case-first.md) starts with case search/deep work while retaining a complete Operations route.

All three use the same complete Intake, Triage, Case and Administration flows. The user explicitly authorised generation of all three reviewed comparison rasters on 2026-07-26; they are now linked from the [UI/UX route](ui-ux/README.md#current-visual-comparison). The remaining choice blocks final V1 UI handoff only; it does not block domain, data or integration plans. Explicit user selection is still required, and selecting a shell does not approve every raster detail. Any V2/V3/V3+ UI change re-enters the complete UI/UX route rather than inheriting the V1 choice.

Add another entry here only when a material ambiguity remains after applying the repository source-of-truth order. Do not treat deliberately deferred product features or implementation-level contract design as unresolved business policy.

Azure resource ownership and retirement remain separate exact-target decisions under `docs/azure/replacement-and-retirement-plan.md`. They require fresh inventory and explicit approval before any cloud mutation; they are not first-MVP product-scope blockers.
