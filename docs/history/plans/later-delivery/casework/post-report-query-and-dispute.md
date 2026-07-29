# Post-report query and dispute work

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Primary plan: `P-POST`

Feature: `CASE-23` — `Next`/`unallocated`.
Pre-conversion status: **Planned; no post-report Core owner or caller is implemented.**

## Scope, dependency and boundary

Extend the settled case lifecycle for post-report queries/disputes without making an Outlook thread, message move, or external reply the authority for a case transition. Dependencies are the `0.1.0-alpha.1` lifecycle, case identity/action history, source custody, approved operator UI route, and intended Outlook evidence adapter. The named automatic mailbox association policy is additionally gated by the mailbox decision dossier; staff-originated casework need not wait for that gate.

## Resolve post-report queries and disputes

The lifecycle Core owner first needs a direct domain decision defining allowed states/transitions, actors, case/report evidence, correction/reopen interaction, due/chaser interaction, closure, and dispute resolution. Only then may an authenticated Web surface or approved staff MCP tool call that owner; an Outlook/Graph adapter supplies attributable evidence, never a duplicate lifecycle engine.

Fail closed for an unknown case, ambiguous/unauthorised report evidence, stale edit, unsupported transition, or uncertain automatic association: retain the source/evidence for review and create neither a hidden transition nor a new case/reference. Record accepted action history separately from content-safe operational telemetry. Tests progress from Core transition negatives to adapter/caller idempotency, operator-visible recovery and, if approved, exact shared-development evidence. They do not prove mailbox policy, external delivery, production reliability, or acceptance.

Roll out after the accepted lifecycle contract through a limited staff caller; recover by disabling the new transition surface and preserving history/evidence for reconciliation. Approval gates are the domain decision, UI route, external mailbox scope, and operator/release acceptance. No report/dispute schema, automated matching policy, sender, external account, or vendor integration is created now. Stable case, report-evidence and external-message identities are preserved; any irreversible lifecycle transition is governed by the accepted contract.

## Deferred-capability impact

This plan is independent post-report casework, not an email workspace replacement or automatic matcher. Later communication, reporting, finance and EVA work may consume its stable evidence identities only through their own approved contracts and callers.
