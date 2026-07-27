# Historical/unapproved concept 3: case workspace

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: Retained historical concept and superseded as an active candidate by the direction-neutral [Case-first candidate](../../../../design/references/directions/case-first.md). It does not select a direction, authorise image generation, or set requirements.

![Case workspace](mockups/concept-03-case-workspace.png)

## Intent

Create one durable home for the case after intake, keeping identity, next action, permanent action history, Box, and related Audit work visible.

## Keep

- Persistent `QDOS26001` header and a visible reopened state.
- Overview, Documents, Images, Report, and Action history tabs.
- Timeline with actor and reason.
- Manual seven-day chaser with Copy message.
- Related `a.QDOS26001` shown without replacing the parent reference.
- No delete control.

## Change before implementation

- Sample case details and work type are illustrative, not requirements.
- Completeness should use separate instruction/image dimensions rather than a decorative percentage unless a transparent scoring rule is approved.
- Close case must present four named terminal outcomes—post-report completion, provider cancellation, Collision Engineers rejection and `Created in error`—and capture reason/actor.
- Related Audit UI must also handle `ap.` and Inspection + Audit creation rules.
- Keep principal/reference read-only after allocation. Offer a reasoned `Created in error` replacement action that leaves the original terminal, links both cases, and never presents it as an edit or reopen.
- Reopen asks for a reason and an otherwise-valid nonterminal destination; `Held` remains a separate action. A report needs no pre-send review gate. When automatic evidence is absent/ambiguous, any staff role links the exact approved-mailbox Sent item with a reason; the UI separates authoritative `sentDateTime` from discovery/link times and supports reasoned unlink/relink with recomputed activity while retaining history.
- Box folder state needs missing, pending creation, inaccessible, and conflict states.

## Deferred-capability impact

The [UI planning impact register](README.md#deferred-capability-impact) applies. This concept preserves immutable case/reference, linked replacement, related Audit work, source/document identity and exact external evidence so later EVA, email, finance or later case-type adapters can extend named actions without rewriting history. It does not define the V1 exact report matcher or authorise message sending, EVA API, estimates/valuation/invoices, external accounts, AI assistance, or permanent deletion.
