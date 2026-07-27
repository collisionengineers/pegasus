# Communications automation

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Primary plan: `P-COMMS`

Features: `MAIL-19`; `EXT-15` — V3 release work; `MAIL-17` — V3+ release work.
Pre-conversion status: **Planned activation contracts; no outbound/WhatsApp caller exists.**

This umbrella coordinates common external-message safeguards only. It does not create one sender engine: each slice has its own Core owner, adapter, approval, idempotency and recovery contract.

## Automate chasers

Feature: `MAIL-19` — V3 release work.

After the settled manual chaser/lifecycle contract, a dedicated lifecycle Core use case may determine eligibility and durable outbox intent; an intended Worker/approved channel adapter delivers only an accepted template/action. Direct decisions must settle content authority, recipients, timing, consent, correction/cancellation, retries and evidence. Refuse Held, closed, stale, unauthorised, duplicate or uncertain cases before outbound activity. Prove Core/outbox negatives, exact delivery idempotency and caller-visible recovery; a test transport is not delivery proof. Roll out one approved channel/cohort and disable it to recover while retaining intent/outcome history.

## Automate WhatsApp intake and coexistence

Feature: `EXT-15` — V3 release work.

This is a separate inbound channel with its own Core source/custody decision and intended Worker adapter. It needs a direct channel/coexistence, access, retention, identity, association, failure and operator-review contract. Unknown/ambiguous material remains reviewable with no case/reference allocation or auto-association. Evidence requires source identity, replay and scope negatives, then one exact approved non-production/shared-development caller path. It does not authorise WhatsApp content transfer, account access, a generic channel engine, or changing manual WhatsApp material handling until approved.

## Automate report sending

Feature: `MAIL-17` — V3+ release work.

Report release and exact report evidence remain distinct from delivery. A report Core owner and separate delivery adapter require direct approval for report version/finality, recipient/authorisation, delivery evidence, retries, correction/withdrawal and operator recovery. Refuse absent/ambiguous report authority, recipient, finality, or delivery scope before sending. A successful provider response does not prove recipient receipt. Roll out one approved report path with durable idempotency evidence; recover by disabling delivery and reconciling retained outcomes.

## Shared proof and deferred impact

Every slice needs privacy/security/licence/cost approval, exact external account/target and data scope, content-safe telemetry, permanent action history, focused/integration evidence, and operator/release acceptance. No in-app compose/reply/forward/send surface is introduced (`MAIL-12` is Never). Stable channel/message, outbox-intent, report-version and delivery-outcome identities are retained; no outbound account, adapter, queue, template policy, or feature flag is built now. Activation is per slice and per approved channel, never implied by this document.
