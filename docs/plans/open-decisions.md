# Open decisions

Most decisions reviewed through 2026-07-24 are settled in `PROJECT_DISCOVERY_QUESTIONNAIRE.md`; implementation requirements are summarised in `docs/plans/remaining-requirements.md`.

## Mailbox categorisation and all email matching research

Mailbox categorisation and every automatic email-matching path remain one combined open research decision. Its working evidence and decision questions belong in `docs/plans/mailbox-categorisation-and-email-matching/README.md`.

The research must settle inputs, category and matching predicates, precedence and ambiguity handling, correction/reversal behavior, rule-authoring and approval authority, effective dates and policy versions, rollback, retained decision evidence, and the acceptance cohort. It must cover intake, case correspondence, sent-report evidence, and Triage reply-chain and later-case association without conflating their already-settled business outcomes.

Until that research is accepted:

- retain each source with its stable mailbox identity and make it visible without guessing a category or case;
- route uncertainty to `Needs sorting` where the settled workflow requires staff review;
- permit the explicitly settled manual exact-item report link with a required reason and permanent action history; Triage completion still requires the exact reply-chain item found in Sent Items and has no manual-selection fallback;
- do not enable automatic category or email-match decisions; and
- do not add a generic rule engine, expression language, rule table, configuration screen, dormant service, or transport-specific second classifier.

This does not defer first-MVP automatic case creation from an independently definitive accepted instruction, nor does it change the exact Outlook Sent-item evidence required for a manually linked report or Triage reply.

Add another entry here only when a material ambiguity remains after applying the repository source-of-truth order. Do not treat deliberately deferred product features or implementation-level contract design as unresolved business policy.

Azure resource ownership and retirement remain separate exact-target decisions under `docs/azure/replacement-and-retirement-plan.md`. They require fresh inventory and explicit approval before any cloud mutation; they are not first-MVP product-scope blockers.
