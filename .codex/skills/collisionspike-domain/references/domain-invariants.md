# Domain invariants

## Identity and references

- Principal code is required before allocating a principal reference.
- Sequence allocation is atomic and concurrency-safe per principal and two-digit year.
- The three-digit sequence is shared by inspections and audits; prefix does not create a second counter.
- References are unique and immutable once accepted, subject to an explicit future reassignment decision.
- Vehicle registration is an intake identifier, not a substitute for the eventual case reference.

## Lifecycle

- A state change records actor, timestamp, prior state, new state, and reason or context.
- Case records and their audit histories are retained.
- Reopening is explicit and auditable.
- Terminal business outcomes are distinct from transient technical failure and unknown classification.
- A missing or ambiguous instruction is not silently interpreted as a rejection or cancellation.

## Integration

- Box folder/file identity uses the accepted EVA/reference convention.
- External adapters translate; they do not independently decide case workflow.
- An external failure must be retryable or visible without duplicating a business action.
- Secrets are referenced by name or identity and never included in domain data or logs.

## Completeness

Instruction and image completeness can be filtered separately. Provider-specific required fields remain configurable. Do not hard-code an invented universal completeness matrix.
