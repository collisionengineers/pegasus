---
id: ADR-0078
status: accepted
date: 2026-10-16
supersedes: [ADR-0008]
superseded_by: []
related_capabilities: [INT-04]
related_frd: [frd-04, frd-09]
tags: [email, routing, contacts]
---

# ADR-0078: Intermediaries are linked Claim Sources

## Status

Accepted on 15 October 2026 by operator decision, with the operator's rulings
of 16 October 2026 on shared mail domains and on a sender that matches both a
direct identity and an intermediary.

This record replaces, in part:

- [ADR-0008](0008-separate-direct-provider-and-intermediary-email-policies.md):
  the Consequences clause that intermediary identities are "code-owned by the
  route-policy catalog, not database-authored configuration". Direct sender
  traits and route-to-provider predicates stay code-owned. The rest of
  ADR-0008 stands, and its body stays as filed apart from that clause.

[FRD-09](../frd/frd-09-principal-and-intermediary-routes.md#accepted-principal-email-routes-and-automatic-association)
owns the mail routes and
[FRD-04](../frd/frd-04-parties-accounts-and-access.md#parties-principals-organisations-accounts-and-access)
the Contacts directory.

## Context

ADR-0008 kept intermediary identities in code, and the route policy held the
two PCH intermediary domains, `connexus.co.uk` and `ensurance-claims.co.uk`,
as a literal list.
[ADR-0076](0076-reviewed-register-is-the-contact-directory-data-contract.md)
made an intermediary a Claim Source Contact, linked to Principals on
evidence. The rulings:

- "Build the intermediary registry" (15 October 2026).
- "intermediary is claim source now. also need to link these correctly to
  principles based on evidence. also some of these can also be principles
  e.g. sixways" (15 October 2026).
- "mailbox-only matching for a claim source on a shared mail domain (full
  address), exact-domain matching otherwise"; "a new ADR partially replacing
  ADR-0008's clause"; a sender matching both a direct identity and an
  intermediary is "NeedsSorting"; "`PrincipalMailRoutePolicy.Version` stays
  1" (16 October 2026).

## Decision

1. **An intermediary is a linked Claim Source.** An active Claim Source with
   an e-mail is an intermediary for each active Principal it is linked to.
   Owner: `IIntermediaryRegistry`, read on each evaluation.
2. **Matching.** On a shared mail domain the Claim Source's full mailbox
   address matches; otherwise the exact domain of its e-mail does. No
   subdomain or suffix widening. Owner: `IntermediaryDomains`, which holds the
   one shared mail domain list.
3. **An agreeing profile.** Intermediary mail is accepted only with one
   selected instruction profile of the linked Principal; an intermediary is
   not a direct identity. Owner: `PrincipalMailRoutePolicy`.
4. **Overlap.** A sender that matches both a direct identity and an
   intermediary needs sorting, as ADR-0008 decision 9 states. Owner:
   `PrincipalMailRoutePolicy`.
5. **Version.** `principal_mail_route` stays version 1.

## Consequences

- The two PCH domains leave the code; PCH intermediary mail is accepted
  through the Claim Sources the Contacts directory links to PCH.
- The Worker reads `ContactPrincipalLinks`
  (`20261018080000_GrantWorkerContactPrincipalLinksRead`) and never writes a
  link.
- A directory edit applies from the next evaluation; a recorded route
  decision is not re-derived (ADR-0008 decision 8).
- ADR-0008 lists this record in `superseded_by`, and its index row names the
  intermediary-identity clause as the replaced scope.

## Links

- [FRD-09 — Accepted principal email routes](../frd/frd-09-principal-and-intermediary-routes.md#accepted-principal-email-routes-and-automatic-association)
- [FRD-04 — Parties, principals, organisations, accounts, and access](../frd/frd-04-parties-accounts-and-access.md#parties-principals-organisations-accounts-and-access)
- [ADR-0008](0008-separate-direct-provider-and-intermediary-email-policies.md)
- [ADR-0076](0076-reviewed-register-is-the-contact-directory-data-contract.md)
