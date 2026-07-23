# Guide to legacy operator-provided material

> **Human review required.** Everything described here came from the old project. Neither the original files nor these guides are implementation authority for CollisionSpike v2.

Use this guide to find concepts worth discussing with an operator. Before using one, confirm that the business meaning is still correct, reconcile it with the current source-of-truth order, and record the decision in a current authoritative document.

## How the comparison labels are used

- **Implemented locally** — a limited current v2 caller exists. This does not mean the old design was adopted.
- **Planned in v2** — current requirements or accepted v2 architecture mention the capability, but it may have no caller yet.
- **Deferred in v2** — current documents deliberately place it beyond the first MVP.
- **Conflicts with v2** — the old rule or architecture contradicts a current decision and must not be copied.
- **Review only** — potentially useful context with no current approval.
- **Predecessor-specific** — describes old code, deployment, data or delivery machinery rather than a reusable product rule.

Words such as `accepted`, `done`, `binding`, `live` and `verified` inside the legacy material describe the old project only.

## Index and category guides

- [Complete file index](./file-index.md) — every original file, its category and its original purpose.
- [Product and workflow](./01-product-and-workflow.md)
- [Architecture, contracts and integrations](./02-architecture-contracts-and-integrations.md)
- [Operations, cloud and security](./03-operations-cloud-and-security.md)
- [Design and dated reviews](./04-design-and-reviews.md)
- [Governance and delivery machinery](./05-governance-and-delivery.md)
- [Legacy tickets: intake, email, parsing and triage](./06-tickets-intake-email-and-parsing.md)
- [Legacy tickets: evidence, Box, vehicle data and workflow](./07-tickets-evidence-box-and-vehicle.md)
- [Legacy tickets: operator interface](./08-tickets-operator-interface.md)
- [Legacy tickets: AI, assistants and integrations](./09-tickets-ai-and-integrations.md)
- [Legacy tickets: platform and other delivery records](./10-tickets-platform-and-other.md)
- [Raw exports and captured EVA evidence](./11-raw-exports-and-eva-evidence.md)

## Broad findings

- Useful business concepts recur around source preservation, conservative matching, reviewable extraction, Case/PO identity, Box custody, EVA handoff, vehicle data, chasing and auditable decisions. Many already appear in current v2 documents, but most are not implemented.
- The old architecture is fundamentally different: TypeScript services, a SPA, Python functions, PostgreSQL/RLS, Entra staff sign-in, separate service packages and extensive feature gates. Current v2 uses a .NET 10 modular monolith, Razor Pages, Azure SQL and application-managed staff accounts.
- The old Case/PO material contains a direct numbering conflict: independent sequences by marker and a four-digit proposal. Current v2 requires one shared three-digit principal/year sequence across all case types.
- The old ticket system is historical delivery evidence, not a backlog to import. Its 308 ticket specifications and supporting files must be considered concept by concept.
- The spreadsheets, screenshots and evidence exports contain real or production-shaped operational data. They are useful for vocabulary and field discovery, but not safe defaults, test fixtures or migration authority.

## Review method

For a concept selected with an operator:

1. Open the original file from the category guide.
2. State the business question in current Collision Engineers language.
3. Compare it with `docs/operator-notes/`, the questionnaire, current accepted ADRs and open decisions.
4. Record whether the concept is accepted, changed, deferred or rejected.
5. Only then create or update a current requirement, ADR or implementation plan.

