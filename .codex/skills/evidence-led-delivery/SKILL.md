---
name: evidence-led-delivery
description: Deliver repository changes from authoritative requirements through a real caller to independent evidence. Use for implementation plans, cross-cutting changes, bug fixes, validation design, reviews, or any task where file presence and broad green checks could be mistaken for working behavior.
---

# Evidence-led delivery

Build the smallest change whose behavior can be observed at the boundary that actually matters.

## Workflow

1. **Name the authority.** Cite the requirement, incident, contract, or direct user decision. Separate facts from assumptions.
2. **Trace the caller.** Map the real entry point to the code that decides behavior. Registration, dependency injection, and a test-only caller do not count.
3. **Search before adding.** Find the current owner, parallel models, adapters, and copies. Stop if the change would create a third implementation.
4. **Plan one observable slice.** State inputs, output, failure behavior, exclusions, changed boundary, proof, and rollback.
5. **Implement narrowly.** Keep policy in its owner and translation at edges. Preserve unrelated work.
6. **Validate in layers.** Compile or parse, focused negative and positive checks, integration boundary, then real-shaped input through the actual caller.
7. **Review independently.** A separate reviewer compares literal authority to behavior and challenges evidence precedence.
8. **Report honestly.** Distinguish implemented, called, locally verified, deployed, live-verified, and accepted.

## Stop conditions

- The authoritative rule is materially ambiguous.
- There is no identifiable production caller.
- The proposed change creates a second source of truth or third copy.
- A destructive or external mutation lacks explicit authority and exact targets.
- The only available proof is a mock, registration, documentation, or broad repository check.

## Guard policy

A new automated guard is justified only by a named invariant or observed failure. Add a deliberate negative fixture, run it to see the guard fail, apply the fix, then run the same guard to see it pass. If this cannot be demonstrated, use a review note instead of another permanent check.

Read [verification.md](references/verification.md) when designing evidence or a final report.
