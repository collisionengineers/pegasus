# Open decisions

Most decisions reviewed on 2026-07-23 are settled in `PROJECT_DISCOVERY_QUESTIONNAIRE.md`; implementation requirements are summarised in `docs/plans/remaining-requirements.md`. The following material workflow ambiguities must be resolved before their named slices proceed.

## Chase timing around `Not ready` and `Held`

Settled authority requires recurring seven-day chasers while information is missing and requires `Held` to pause progression and chasers while due dates remain visible. It does not settle:

- the event from which the first seven-day chase is measured; or
- whether leaving `Held` resumes the remaining interval, starts a fresh interval, or uses another rule.

This blocks timer scheduling, persistence and cadence acceptance tests. It does not block displaying due dates or implementing the reasoned `Held` state without an active scheduler. Record the operator answer in the questionnaire before implementing the timer rule.

## Reopen destination

Settled authority allows an authorised staff user to reopen a closed case and requires a reason in permanent audit history. It does not state the destination state or whether staff select from constrained destinations.

This blocks the reopen transition and its state-machine tests. It does not block retaining closed cases or their history. Record the operator answer in the questionnaire before implementing reopen.

## Leaving `Held`

Settled authority defines `Held` as a reasoned pause that stops progression and chasers while due dates remain visible. It does not say whether `Held` overlays and returns to the prior state or has constrained operator-selected destinations.

This blocks the `Held` exit transition and its state-machine persistence contract. It does not block entering `Held`, retaining the prior state as evidence, or displaying due dates. Record the operator answer in the questionnaire before implementing an exit action.

## Dashboard activity meanings

Settled labels include `In today`, `Submitted today`, and `Cleared this week`, but authority does not define their source events or Europe/London boundary rules. `Submitted` could mean EVA submission or report sending; `cleared` could refer to inbox work or a terminal case outcome.

This blocks those three activity queries and acceptance tests. It does not block defined case/inbox queues, search, counts, refresh or last-updated time.

## Business Triage workflow

Operator truth defines `Triage` as optional stored pre-case roadworthiness work which may never become a case. The first-release scope requires active support but does not define its operator actions, outcomes, completion rule, audit events or later-case association.

This blocks a Triage state model and mutable caller. It does not block reserving the term or storing an unclassified source in `Needs sorting`.

## Mailbox categorisation and correction

The first release must categorise every ingested mailbox item as Receiving work, Queries, Other, Needs sorting or the real Triage flow. Authority does not define complete category predicates, correction/reversal actions or ambiguity rules beyond conservative `Needs sorting` behavior.

This blocks automatic category policy and its acceptance cohort. It does not block exact-scope durable Outlook receipt with every item initially visible and unclassified.

## Principal code after first issuance

Authority requires stable principal identity and preserved issued references, but does not say whether a principal code becomes immutable after first use or may change prospectively with an alias/history rule.

This blocks changing a used principal code. It does not block creating the initial QDOS principal or editing its name, active state and other independently authorised metadata.

## Permanent business audit catalogue

Authority requires permanent audit for user and automated actions, while protocol reads, refreshes and lease heartbeats can be high-volume technical events. The boundary between permanent business history and content-safe telemetry is not yet explicit.

This blocks finalising an exhaustive audit-event catalogue. It does not block permanent audit for mutations, business decisions, material denials and external side-effect outcomes.

## Authoritative sent-report evidence and time

Settled authority requires the pre-send review gate and prevents principal/reference reassignment after Collision Engineers sends any report. It does not identify:

- whether an audited staff action, Outlook evidence, or another event proves that a report was sent;
- the exact sending mailbox and folder scope if Outlook is used;
- the matching and ambiguity rules; or
- the authoritative timestamp when evidence arrives late or conflicts.

This blocks Sent Items ingestion, automatic `Report sent` recording and the trigger that freezes principal/reference. It does not authorise access beyond the first-MVP `instructions@collisionengineers.co.uk` Inbox. Record the operator answer in the questionnaire and update the Graph scope decision before implementing this path.

Add another entry here only when a material ambiguity remains after applying the repository source-of-truth order. Do not treat deliberately deferred product features or implementation-level contract design as unresolved business policy.

Azure resource ownership and retirement remain separate exact-target decisions under `docs/azure/replacement-and-retirement-plan.md`. They require fresh inventory and explicit approval before any cloud mutation; they are not first-MVP product-scope blockers.
