# QDOS forward created no case — diagnose, then fix

A QDOS email forwarded to `instructions@collisionengineers.co.uk` produced no
case. Release 7 (`32feefa…`) already carries `QdosMailRoutePolicy` v3, which
unwraps a staff forward, and worker-side allocation, so neither the old manual
acceptance gate nor a missing forward rule can be the cause. This task finds
the failing link with live read-only evidence and fixes what is defective.

## Diagnosis

Walk the intake chain and stop at the first stage with no successor evidence.

| Stage | Owner | Evidence if it stopped here |
| --- | --- | --- |
| Graph poll | `PollApprovedInbox` | `ApprovedInboxPollStates`, `ApprovedInboxPoisonMessages` |
| Stage | `ReceiveIntake` | `IntakeStagedReceipts`, `transient-intake` blob |
| Dispatch | `PendingWorkDispatchFunction` | `IntakeWorkItems.State`, `intake-work` queue |
| Process | `ProcessIntake` | `IntakeReceipts.Decision`, `IntakeMailRouteDecisions` |
| Allocate | `AllocateCaseIfDefinitiveAsync` | `Cases`, `CaseIntakeLinks` |

Authorised read-only targets, subscription
`e6076573-23a5-46a8-acef-7e22d264e5db`, resource group `rg-pegasus-prod`:
Application Insights `pegasus-prod-appi-252ow37gij`, Function App
`pegasus-prod-worker-252ow37gij`, storage `pegtrans252ow37gij`, the `pegasus`
database, and the newest `instructions@collisionengineers.co.uk` message. Every
production mutation — creating a principal, re-enabling a function, re-driving
the message — stops for separate approval.

The Worker registers classic Application Insights, so Core's
`Pegasus.Core.Intake` activity tags are not exported: telemetry proves
execution and exceptions, never the intake decision. The decision comes from
the persisted receipt.

## Leading hypotheses

1. No active `QDOS` principal in production. `EfCaseAcceptanceStore` throws
   "The active principal 'QDOS' does not exist", and
   `AllocateCaseIfDefinitiveAsync` catches every exception and returns `false`
   with no log, no telemetry and no receipt event — leaving a receipt that
   reads `case_created` while no case exists. The principal is seeded only by
   the DevelopmentOffline initialiser.
2. An inline forward. `AttachedOriginal` sender identity comes only from a
   nested `message/rfc822` part, so an inlined forward fails
   `forward.original-exactly-one` and routes to `Needs sorting`. That is
   correct fail-closed behaviour; the outputs are an operator instruction and,
   if the operator wants inline forwards supported, an open decision — not a
   policy widening.
3. Poll-stage quarantine (`message_too_large` above the 10 MiB envelope,
   `empty_message`, `source_identity_conflict`). Those rows have no Web
   surface at all today.
4. The poll never ran; processing exhausted retries; extraction indeterminate
   or OCR-required; ambiguous match or standalone Audit.

## Changes

Scoped by the evidence, and bounded by three rules: no edits to files
`task/upload-case-creation-and-inbox` owns (`QdosAlphaCaseActivationPolicy`,
the `IIntakeSubmission` binding, the Inbox surfaces, mailbox administration);
no widening of an accepted fail-closed policy to make a refusal pass; the
failing test precedes the fix.

One change is already justified by the code independently of the diagnosis:
the silent allocation swallow in `src/Pegasus.Core/Intake/DurableIntake.cs`
turns a definite failure into a receipt claiming success. A failed allocation
must be logged, telemetered, and visible on the receipt.

Findings that are not code changes edit existing canonical files only — a
`NOW.md` queue line, `docs/open-decisions.md` for an operator question,
`docs/operations.md` where live state contradicts the recorded state.

## Verification

- The failing test first, at the tier that owns the break: Core and
  persistence for a swallowed allocation, a reader or policy fixture for the
  forward shape, Functions/Azurite for the queue path.
- `dotnet restore`, `dotnet build --configuration Release`, the focused
  `dotnet test` selection, then the full run.
- A local green run is tier 2–6 evidence. Proving the fix against the real
  mailbox needs a release and a live re-run, which is a separate authorised
  operation this task does not claim.
