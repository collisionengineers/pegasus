# Plan — TICK-010: MAIL-22 settled taxonomy

*The plan. Not the checklist — this is the **reasoning**; the checklist is the executable distillation of it.*

Written FROM the ticket's `research` and `files` documents — if either is missing or stale, fix that first.

## Approach

The settled taxonomy is already in Core and the decision table. This ticket does not add a confirmation UI or new QDOS rules. It closes the persistence hole: `Other` and Sent categories can be stored by the existing mapper but are untested. Add LocalDB round-trips next to the classified-Received and Ambiguous tests. Alternatives rejected: (1) Inbox confirmation page — UI-10 / 0.3.0; (2) automating remaining families — `boundaries.md`; (3) a new migration — columns already exist.

## Governing docs

**Required.** How this plan meets each linked PRD/FRD/ADR (`refs`). For each:
- **Meets** — which requirement/acceptance-criterion each step satisfies; or
- **Modifies** (only with explicit user authorization) — what changes in the doc and why; or
- **New ADR** — the design decision this introduces, written via `kanmer-docs` and linked.

`kanmer-review` checks this section holds against the diff.

- **FRD-08** § Settled mailbox taxonomy and correction — **Meets** the encoded families/subtypes (already locked by `MailTaxonomyTests`); **Meets** `Other` requiring name and reasoning (factory + persist/reload); **Meets** Reply as context on the underlying Sent/Received category; **Meets** category/destination separation (no new destination fields). Does not implement staff correction/reversal (MAIL-04) or folder destination (MAIL-05).
- No FRD/ADR is modified. No new ADR.

## Steps

1. Add `OtherMailClassificationDecisionReloadsNameAndReasoning` for Received `Other` through `IIntakeReceiptStore` / `IIntakeReceiptQueries`.
2. Add `SentOtherMailClassificationDecisionReloads` for Sent `Other`.
3. Add `SentFamilyClassificationReloadsWithAndWithoutReplyContext` for a settled Sent family (`query-sent` or `Report sent`).
4. Assert incomplete Other still cannot be constructed (`MailCategory.Other` already throws; do not add a corrupt-row writer).
5. Run focused `MailboxIntakeIntegrationTests` and `MailTaxonomyTests`.

## Verification

- Focused `dotnet test` on those two classes (`Release`).
- No migration, no policy change, no Razor handler.

## Risks / open questions

- Risk: reviewers expect Inbox confirmation. Mitigation: plan and PR state MAIL-22 Now is the taxonomy contract; UI-10 owns the surface.
- Open question resolved: no staff UI in this ticket (`open-questions`).
