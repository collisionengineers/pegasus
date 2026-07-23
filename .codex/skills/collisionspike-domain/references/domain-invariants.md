# Domain invariants

## Identity and references

- Principal code is required before allocating a principal reference.
- Sequence allocation is atomic and concurrency-safe per principal and two-digit year.
- The three-digit sequence is shared by inspections and audits; prefix does not create a second counter.
- A standalone Audit uses the original Engineer's repairable/total-loss assessment to select `a.` or `ap.`. Missing or ambiguous source evidence prevents case creation and reference allocation.
- Inspection + Audit starts with the normal Inspection reference and creates its later Audit reference from Collision Engineers' own assessment.
- Before Collision Engineers sends its first report for a case, a principal correction allocates the next reference for the corrected principal and correction year on the same case. The prior reference remains a searchable alias and neither sequence number is reused.
- Reconcile only the external artefacts that already use the prior reference: if a Box folder exists, require a separate audited confirmation of its manual update and provide its link; if EVA contains the old reference, require a separate audited confirmation of its manual update. Block progression until every applicable confirmation is complete. The application does not reconcile either system automatically.
- After Collision Engineers sends any report for the case, the principal/reference does not change. Record the discovered error as an audit note.
- Vehicle registration is an intake identifier, not a substitute for the eventual case reference.

## Lifecycle

- A state change records actor, timestamp, prior state, new state, and reason or context.
- Administrator, Engineer, and User roles may perform case transitions and review gates. Only Administrators manage accounts, principals, and configuration.
- Case records and their audit histories are retained.
- Reopening is explicit and auditable.
- Terminal business outcomes are distinct from transient technical failure and unknown classification.
- A missing or ambiguous instruction is not silently interpreted as a rejection or cancellation.
- `Not ready` means incomplete and being chased. `Review` means complete and awaiting approval. `Held` is a reasoned manual pause that stops progression and chasers while due dates remain visible.
- `Blocked intake` is a manual inbox filter with a required reason, not a case state. It creates no case/reference and retains the source for resolution and retry.

## Integration

- Box folder/file identity uses the accepted EVA/reference convention.
- External adapters translate; they do not independently decide case workflow.
- An external failure must be retryable or visible without duplicating a business action.
- Secrets are referenced by name or identity and never included in domain data or logs.

## Completeness

Instruction and image completeness are separate staff judgements. When the configurable gate is enabled, both operator-confirmed flags are required before Engineer assignment. Continue to show missing and contradictory values, but do not enforce a principal-specific or universal field matrix.
