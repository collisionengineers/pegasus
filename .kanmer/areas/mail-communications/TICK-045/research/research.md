# Research — TICK-045: MAIL-03 shared mailbox classification policy

*The research. Not the files document — this is what you **learned**, not what you will **touch**.*

## Question

What “one shared classification policy across all supported mailboxes” means in the existing architecture, which policy owner and caller already exist, and what remains to make the 0.3.0 mailbox workspace use that policy without violating the route-owned-policy decision.

## Findings

- FRD-08 (`docs/frd/frd-08-email-mailbox-and-background-processing.md`) is the sole product-behaviour owner. Its confirmed Received/Sent taxonomy applies to the target approved-mailbox estate; classification, queue, Triage routing, and Outlook folder destination are separate facts. It requires exact source identity and permanent version/evidence/history for every automated or human decision.
- The shared vocabulary and result shape already have one Core owner in `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`: `MailTaxonomy`, `MailCategory`, `MailClassificationResult`, policy key/version, evidence predicates, ambiguity, and `Other` validation. MAIL-22 ([[TICK-010]]) proved this full taxonomy persists and reloads through the intake receipt store.
- Automatic classification is deliberately **route-owned**, not mailbox-owned. `IMailClassificationPolicy` names a `WorkProviderCode`; `ProcessIntake.EvaluateMailClassification` selects exactly one policy only after an accepted mail route. ADR-0008 (`docs/adr/0008-separate-direct-provider-and-intermediary-email-policies.md`) forbids a universal predicate/rules engine and requires unrelated provider/intermediary predicates to stay isolated.
- Only `QdosMailClassificationPolicy` is currently registered (`src/Pegasus.Infrastructure/DependencyInjection.cs`). MAIL-21 ([[TICK-009]]) delivered and verified that versioned QDOS automatic policy, decision evidence, ambiguity outcome, persistence, and a local volume cohort. It did not activate automatic matching for the remaining taxonomy or every mailbox.
- Approved mailbox configuration (`ApprovedMailboxAdministration.cs`) owns durable mailbox identity, enablement, and the two transport scopes `InboundIntake` and `SentEvidence`. It contains no classification-policy selector. Adding per-mailbox taxonomy tables or mailbox-specific category lists would create a second policy owner and contradict both FRD-08 and the repository's one-list rule.
- The read-only workspace already projects current classification for exact retained messages via `RetainedMail.cs`, `EfRetainedMailboxMessageStore`, and `Pages/Mail/Message.cshtml(.cs)`. The missing real caller is the exact-message classification/correction action allocated to MAIL-04 ([[TICK-046]]); that ticket's research already routes it through the existing Core taxonomy and append-only decision history.
- The epic context requires UI, Core, infrastructure, and Automation Actor callers to reuse one canonical business implementation. Therefore MAIL-03 is the cross-mailbox applicability constraint on that shared Core action/policy; MAIL-04 supplies the first mutation caller, and MCP-05 must later call the same Core owner rather than reproduce validation.
- The operator's 2026-08-18 instruction to complete EPIC-006 designates this allocated post-alpha work for implementation. It does not approve any real Outlook/cloud mutation. Shared-policy acceptance can be proved with local Core/Web/persistence fixtures using at least two distinct approved mailbox identities.

## Implications

- Preserve two layers: one shared taxonomy/manual-decision contract across the approved mailbox estate, plus route-specific automated predicate policies selected by provider/intermediary route. “Shared” must not collapse ADR-0008 route policies into a generic rules engine.
- Search/reuse the MAIL-04 Core command and persistence transaction rather than creating a parallel MAIL-03 classifier. If sequencing requires MAIL-03 first, its smallest useful contribution is the cross-mailbox invariant/contract and tests that the later exact-message command consumes; do not add a wrapper with no caller.
- Acceptance should prove that the same category validation, policy identity/version/evidence requirements, and fail-closed behavior apply to exact retained messages from two different approved mailbox identities, with no mailbox-address branch or duplicated category list.
- Coordinate implementation with [[TICK-046]] because both naturally touch the classification contracts and tests. Queue mapping, folder recommendation/move, case association, and Automation Actor exposure remain their owning tickets.

## Open questions

No unresolved user-only question remains. See `open-questions` for the explicit activation and scope disposition.

## Additional research from a previous implementation

### Provenance and scope

The following evidence comes from a **previous implementation** and is being used only as a reference for shared-policy behaviour. Its provider list, taxonomy labels and folder names are not Pegasus authorities.

### Shared policy did not mean one undifferentiated heuristic

The previous implementation had a central deterministic classifier and a central routing function, but it still separated distinct evidence sources:

- sender/provider recognition;
- attachment/document provider recognition;
- pure text classification;
- live case correlation;
- operational folder routing.

This separation supports the current Pegasus research conclusion: one shared mailbox policy should mean **one canonical contract and one canonical implementation path**, not one monolithic rules engine that merges provider-specific predicates, live-case lookup and folder mutation.

### Reusable cross-mailbox provider identity rules

Sender-to-provider identification followed the same fail-closed rule set across intake:

1. exact full e-mail address before domain;
2. exact domain only, case-normalised;
3. active providers only;
4. multiple matches -> `Ambiguous`;
5. no match -> `Unmatched`;
6. no fuzzy or substring matching.

This gives Pegasus a useful invariant for any shared provider-identification component: a generic domain must never be treated as enough evidence to identify a provider, and observing a new sender/domain does not by itself authorise creating a provider identity.

Attachment/document recognition remained an independent provider-evidence source. A forwarded instruction is the key acceptance scenario: mailbox-specific sender identity may point to staff, while the attached document identifies the originating provider. The shared policy should preserve both evidence sources rather than branching on mailbox address or overwriting one with the other.

### Shared deterministic classifier behaviours

The prior classifier was explicitly versioned and deterministic. Several behaviours are strong candidates for cross-mailbox invariants:

- uncertainty abstains rather than guesses;
- `In-Reply-To` / `References` are stronger reply signals than subject prefixes;
- `FW:` / `FWD:` is not automatically a reply because forwards can contain new work;
- cancellation/closure intent can take precedence over quoted old instruction content;
- the classifier emits candidate references/VRMs, while database-aware orchestration performs live case resolution;
- ambiguity is a first-class result rather than an exception to hide.

Those behaviours are mailbox-independent and therefore suitable for one shared Core contract. Provider/intermediary-specific automatic predicates can still remain route-owned, consistent with the existing Pegasus ADR and research.

### Regression corpus evidence

The previous implementation contained a sizeable human-labelled `.eml` evaluation corpus spanning many provider/insurer senders. Its historical labels included concepts such as new instruction, case correspondence, general query, payment/finance, automated and irrelevant.

That corpus is useful as a **regression/acceptance source**, not as a taxonomy source. If reused conceptually for Pegasus tests, each example should first be remapped to the current settled Pegasus taxonomy and policy version; old labels should not be imported into the shared contract.

### Additional MAIL-03 acceptance implications

The shared-policy implementation should be tested with at least two approved mailbox identities and should prove:

- identical taxonomy validation and evidence requirements regardless of mailbox address;
- no duplicated category list or mailbox-specific classification table;
- identical ambiguity/unmatched semantics across mailboxes;
- forwarded instructions preserve sender and document identity as separate evidence;
- route-specific automatic predicates remain isolated while producing the same shared result contract;
- exact-message/manual correction callers reuse the same Core policy owner rather than reimplementing validation;
- classifier/correlation boundaries remain stable: text emits candidates, orchestration resolves live business identity.

This previous-implementation evidence strengthens the existing Pegasus direction but does not activate any cloud/mailbox mutation or override the current FRD/ADR ownership model.
