# Intake and casework

## Outcome

Authorized intake becomes a reviewable source-backed draft and, only after all
gates pass, a case with an immutable principal and reference. Staff manage QDOS
Inspection, standalone Audit, Inspection + Audit, Triage, work, report evidence,
queries, reopening, matching and terminal history through Core-owned rules.

## Settled requirements

- Email, document, image-led and manual receipt retain stable source identity,
  origin, occurrences and original custody.
- Processing, source limits, principal identity, mandatory fields and
  standalone Audit evidence fail closed before case creation/reference
  allocation.
- `Needs sorting`, `Blocked intake` and pre-case `Triage` remain distinct.
- A principal/reference never changes or returns to the pool after allocation.
  Wrong-principal work closes as `Created in error` and links a replacement.
- Cases are never deleted. Reopening needs a reason and ordinary destination
  gates; `Created in error` never reopens.
- Matching/linking is evidence-backed and reversible with a reason while both
  source origins remain permanently attributable.
- Direct-provider and intermediary email routes have separate rules. The
  applicable route owns provider, instruction-type and case-association
  evidence and precedence.
- A staff-forwarded message retains Collision Engineers transport provenance
  while the proved original sender drives route identification.
- Concurrent editing cannot silently overwrite newer case data.

## Intake halves and request-scoped upload

`INT-31` permits authenticated staff to generate a temporary, revocable,
request-scoped link for a client, bodyshop or storage yard to upload images or
documents. An unauthenticated link user sees only the upload form and immediate
success/failure response—never case identity, request state, uploaded history or
other documents. The link creates no account or public-registration path.
Token generation, expiry/revocation, limits, custody, retry and abuse handling
require a separately accepted implementation contract.

Instruction and image halves retain separate identity, age, completeness and
chase state. `INT-28` owns automatic matching; `INT-32` owns readiness
notification once a definitive pair exists. Both remain `Next`/unallocated and
must not be inferred from `INT-31`.

## Engineering record and EVA coexistence

`CASE-31` defines one accepted structured case/engineering record as the source
for deterministic reports, fee notes, addenda, query documents, invoice inputs
and management measures. It remains `Later`; existing case, document, estimate,
valuation, report, mail and accounting capabilities retain their own authority.

For `0.1.0-alpha.1`, EVA continues named-Engineer assignment, estimating,
valuation and report preparation. Pegasus records the first successful manual
EVA bundle as the existing once-per-case `First sent to Engineer` proxy. A
retry/regeneration records a revision and never duplicates that first event.
`CASE-30` tracks inspection/report stage and EVA handoff without claiming the
engineering workflow has moved.

## Correspondence, queries and sending

- `MAIL-11` owns complete in-app case correspondence and thread history.
- `CASE-23` owns typed post-report queries/disputes and their taxonomy.
- `AI-08` owns a case-grounded response proposal; a named Engineer reviews,
  amends if needed and approves it before sending.
- `MAIL-12` owns general authenticated compose/reply/forward/send and remains
  `Later`/unallocated.
- `MAIL-17` owns the dedicated idempotent report-send transaction: original
  Outlook thread or provider API destination, principal profile CC suggestions,
  delivery preferences and standing notes, report/fee-note send, Box filing,
  completion and management-event recording. It remains `Later`/unallocated.

## Current state and activation

The only mutating caller is the Development-only manual intake Web route. It
creates a reviewable receipt/draft, not a case or reference; the Worker has no
trigger. Every additional slice requires a change record with its real caller,
policy owner, permissions, failure/recovery behavior and acceptance evidence.
The historical casework and mailbox plans do not activate work.
