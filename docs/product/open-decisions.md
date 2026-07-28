# Open decisions

Status: **Active material-ambiguity register**

Most product decisions reviewed through 2026-07-25 are preserved in the historical [questionnaire](../history/product/project-discovery-questionnaire.md) and reconciled into the canonical product areas; allocation is owned by the [capability inventory](capabilities.md), V1 gaps are summarised in [the V1 gap](v1-gap.md), and dependency intent is owned by the [roadmap](../roadmap.md). Conditional and `Unclear` source rows are visible activation states, not unanswered current-scope questions.

## Mailbox categorisation and all email matching research

The architecture for instruction email interpretation is settled: direct
provider routes and intermediary routes are separate Core-owned,
code-versioned policies. A provider can use both. The direct-provider policy
uses that provider's evidence; the intermediary policy uses the intermediary's
message shape to determine provider, instruction type, and case association.
There is no universal association order or transport-specific second
classifier. Staff forwards preserve the outer transport but route using the
proved original sender.

Remaining research is evidence-specific, not an architecture choice. Each
direct-provider or intermediary policy still needs genuine examples, exact
sender/content predicates, provider/type/case precedence and ambiguity tests,
correction/reversal behavior, and an acceptance cohort. Its pre-conversion
working evidence is retained in [the mailbox dossier](../history/plans/mailbox-categorisation-and-email-matching/README.md). V2 still expands operational categorisation across all four mailboxes, folder suggestions/moves, general correspondence association, and email management.

QDOS direct sender identity is settled: an address ending exactly
`@qdosassist.co.uk` identifies QDOS. That does not classify message type or
associate a case until extraction and the QDOS direct-route policy have run,
and it does not apply when an identified intermediary sent the message.

Until that research is accepted:

- retain each source with its stable mailbox identity and make it visible without guessing a category or case;
- route uncertainty to `Needs sorting` where the settled workflow requires staff review;
- permit the explicitly settled manual exact-item report link with a required reason and permanent action history; Triage completion still requires the exact reply-chain item found in Sent Items and has no manual-selection fallback;
- do not enable the affected V0, V1, or V2 automatic decision before that slice's predicates and acceptance evidence are approved; and
- do not add a generic rule engine, expression language, rule table, configuration screen, dormant service, or transport-specific second classifier.

This does not defer V1 automatic creation of one incomplete `Not ready` case from independently definitive accepted instructions, change exact Outlook evidence, or move the V1 automatic report/Triage match requirements to V2. It records why those matchers remain blocked until their predicates are accepted.

## V1 operator shell detail

The direction-neutral V1 requirements and exhaustive feature trace have passed independent planning review. Operations-first is selected for the V1 shell. The retained alternatives remain comparison evidence only:

- [Operations-first](../../design/references/directions/operations-first.md) starts with shared office queues, due work and day/week outcomes;
- [Worklist-first](../../design/references/directions/worklist-first.md) starts with one bounded case queue; and
- [Case-first](../../design/references/directions/case-first.md) starts with case search/deep work while retaining a complete Operations route.

All three use the same complete Intake, Triage, Case and Administration flows. The user selected Operations-first on 2026-07-27. Selection approves its landing and navigation strategy, not every raster detail. Any V2/V3/V3+ UI change re-enters the complete design route rather than inheriting the V1 choice.

Add another entry here only when a material ambiguity remains after applying the repository source-of-truth order. Do not treat deliberately deferred product features or implementation-level contract design as unresolved business policy.

Azure resource ownership and retirement remain separate exact-target decisions under `docs/azure/replacement-and-retirement-plan.md`. They require fresh inventory and explicit approval before any cloud mutation; they are not first-MVP product-scope blockers.
