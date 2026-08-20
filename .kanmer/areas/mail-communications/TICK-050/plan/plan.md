# Plan — MAIL-08

## Chosen approach

Expose one nullable concrete `RetainedMailSuggestedMove` on the existing exact-message detail. `GetRetainedMail` derives it after the landed MAIL-05 recommendation and MAIL-07 current-location/provider eligibility are known: eligible yields one Move advisory; every other state yields none. Razor labels it as a suggested next action and delegates its control to the unchanged MAIL-07 confirmation dialog/POST.

This is smaller and safer than persisting advice or adding an action enum/registry: there is one accepted action, one existing read owner and one existing execution owner.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: preserves exact-message-only actions, separates classification/recommendation from move, offers only the designated destination, and keeps explicit reasoned confirmation in MAIL-07.
- `docs/design/README.md`: reuses the existing shared reason dialog, Confirm/Cancel, focus and validation conventions rather than introducing another control pattern.
- No ADR or FRD change: existing Core/Web boundaries and accepted behavior carry the slice.

## Steps

1. From `origin/dev` `e4d56d9e`, add the smallest Core nullable suggested-Move value and derive it solely from the current landed recommendation. Reuse `GetRetainedMail`; add no I/O or second eligibility rule.
2. Render a “Suggested next action” section only for that projection and move the existing MAIL-07 dialog/control under it. Preserve the separate Uncertain status-check form and all existing server-derived freshness/route fields.
3. Extend the existing Core and authenticated Web tests: one Move when eligible, none when unavailable or already current/provider unavailable, current-state re-derivation, and the button still targets `MoveToRecommendedFolder` without transport identity or view-time writes.
4. Update `docs/capabilities.md` and the existing mail paragraph in `docs/current-architecture.md` only to local source/test evidence. Claim no deployment or live mailbox behavior.
5. Run focused Core/Web tests, Release build, proportional full Core tests, diff checks and the reuse/simplification/efficiency/altitude pass. Record results in the PIR, push one PR to `dev`, and leave Review.

## Acceptance

- Core returns either null or one concrete suggested Move and re-derives it on each read.
- The advice contains no execution/persistence identity and owns no authorization or stale-state rule.
- Razor shows the labelled advice only when eligible and delegates to the existing MAIL-07 confirmation handler.
- Unavailable recommendation, current destination, unavailable writer and Uncertain recovery do not produce a fresh suggested Move.
- Viewing performs no move/history/external write.

## Risks

- **Duplicate eligibility:** derive only from `RetainedMailFolderRecommendation.CanMove`.
- **Freshness leakage:** keep command freshness on the landed recommendation/dossier used by MAIL-07, not the advisory value.
- **Scope growth:** one concrete record, no enum/registry/framework or additional action.

## Simplification pass — 2026-08-20

- **Reuse:** Reused `GetRetainedMail`, `RetainedMailFolderRecommendation.CanMove`, the latest MAIL-07 outcome, the existing message-detail route, shared reason dialog and unchanged `MoveToRecommendedFolder` POST. No second eligibility policy, label table or command owner.
- **Simplification:** Kept one concrete nullable `RetainedMailSuggestedMove` rather than an action enum, registry or general descriptor. Consolidated the previously duplicated recommendation/latest-move detail projection into one `with` expression.
- **Efficiency:** Advice adds no I/O, persistence or provider call; it is one constant-time branch after reads MAIL-05/07 already perform.
- **Altitude:** Core owns the derived advisory, Razor owns its label/presentation, and MAIL-07 continues to own confirmation, authorization, freshness, persistence and mutation. Infrastructure and MCP are unchanged.
- **Applied findings:** suppress a fresh Move advisory while the latest operation is Uncertain; allow it again only after source-folder recovery reaches Failed; combine current-location and Uncertain re-derivation evidence into the existing Core test instead of adding a test framework.
- **Unapplied findings:** none.
