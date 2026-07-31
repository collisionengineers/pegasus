# ADR-0008: Separate direct-provider and intermediary email policies

- Date: 2026-07-27
- Status: accepted
- Owners: Collision Engineers product owner and Pegasus development team
- Supersedes: ADR-0006's single-policy selection and no-provider-registry/table limits; preserves its provider-neutral transport, provenance, storage, and fail-closed boundaries

## Context

Work instructions may arrive directly from a provider or through an
intermediary. The same provider may use both routes. Although both routes can
lead to the same provider and case workflow, their emails do not have the same
sender traits, document shapes, references, or case-association evidence.

The current proof has one QDOS extraction policy selected after reading a
source. Nested forwarded-message senders are not currently exposed as sender
evidence. It has no provider registry, intermediary policy, live Worker caller,
case model, or general policy selector.

## Decision

1. Provider identity is separate from message-route identity. A message route
   is either a direct-provider route or a named intermediary route.
2. A provider can have a direct policy and can also be an outcome from one or
   more intermediary policies. An intermediary may resolve more than one
   provider when its evidence supports them.
3. Direct-provider policies and intermediary policies are separate,
   code-versioned Core owners. They are organized by stable provider or
   intermediary identity so a fault can be located and changed without editing
   unrelated policies.
4. For direct mail, accepted sender traits identify the provider route. The
   source reader then extracts attachments, email body, and subject before that
   provider's direct policy determines instruction type and case association.
5. For intermediary mail, sender traits identify the intermediary route. The
   reader extracts attachments, body, and subject before the intermediary's
   policy determines the underlying provider, instruction type, and case
   association. The message is not reinterpreted as a direct provider email.
6. A Collision Engineers staff forward retains its outer sender and message as
   transport provenance, but the proved original forwarded sender drives route
   identification. Ambiguous or malformed forwarding evidence fails closed.
7. Each route policy owns its own evidence precedence and case-association
   rules. There is no universal association hierarchy. A Collision Engineers
   Case/PO may only be a lowest-priority fallback where the applicable route
   explicitly supports it.
8. Shared code owns source normalization, extraction, route selection,
   evidence/version recording, and fail-closed orchestration. It does not own
   provider- or intermediary-specific predicates.
9. A policy version is selected at the first successful evaluation. Idempotent
   retries and replays retain that recorded version; later policy changes do
   not silently reinterpret an accepted evaluation.
10. For routing, the normalized source sender is the proved original sender of
    a Collision Engineers staff forward, or otherwise the direct message
    sender. A collision in which direct-provider and intermediary sender traits
    both match is ambiguous and fails closed.
11. QDOS direct-provider identity is a normalized source-sender address ending
    exactly `@qdosassist.co.uk`. This proves the provider route only; extraction
    and QDOS direct-policy evidence still determine type and case. A QDOS
    instruction received through an intermediary uses that intermediary's
    policy instead.

## Consequences

- Suggested implementation is one explicit catalog and discoverable folders,
  for example `Core/Intake/InstructionIdentification/DirectProviders/Qdos/`
  and `.../Intermediaries/<StableCode>/`, with tests mirrored by route owner.
- No generic rules engine, expression language, rule table, admin editor,
  fallback provider, or empty provider-policy placeholders are introduced.
- A route is activated only when genuine evidence establishes its predicates
  and an acceptance cohort proves its positive, negative, ambiguous, retry,
  and version-pinning behavior.
- Provider reference records may exist before their case workflow is activated.
  Reference-data presence is not a live policy, caller, or permission to create
  cases for that provider.
- Provider and inspection-location reference identities are persisted. Direct
  sender traits, intermediary identities, and route-to-provider predicates are
  code-owned by the route-policy catalog, not database-authored configuration.
  Every evaluation persists route kind, stable policy key/version, resolved
  provider code, classification/case outcome, and supporting evidence on the
  receipt; retries reuse that immutable evaluation identity.

## Deferred-capability impact

The alpha activates QDOS case creation only. It establishes the exercised
route-policy catalog and stable provider/intermediary identities needed by
later provider activation, without creating dormant policies, routes, secrets,
mailbox actions, or resources. `Next`/`unallocated` general mailbox categorisation and email
management remain separate. Direct provider API submission is not an email
route and remains independently gated.

The separation of provider identity from route identity is the material data
choice. It is intentionally durable because merging them would misrepresent a
provider that sends both directly and through an intermediary. Policy content
remains replaceable and versioned.
