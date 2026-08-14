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

## What the diagnosis found

The message reached the mailbox at 2026-08-05 19:48:42Z and was refused four
seconds later: 17,496,501 bytes against a 10 MiB bound, quarantined as
`message_too_large` into `ApprovedInboxPoisonMessages` with its bytes retained
at `sha256/74/748D76E9…`. It never became a receipt, so nothing downstream ran.

Everything else was healthy. All nine Worker functions are enabled, the poll
completed at 20:18:45Z with no failure code, and the poll's own state has never
carried one. Application Insights hit its 0.1 GB daily cap at 14:32:49Z, so
there is no telemetry for the event at all — the incident was diagnosed from
the database. That cap is out of scope by operator decision.

Two further findings, neither the cause:

- `Principals` is empty on the production estate, so allocation would have
  failed after processing regardless. Queued in `NOW.md`.
- The 2026-08-04 message before it is the `needs_sorting` receipt reading "A
  staff-forwarded message requires exactly one consistent attached original
  sender" — an inline forward, working as designed.

## Leading hypotheses (as written before the evidence)

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

Bounded by two rules: no edits to files `task/upload-case-creation-and-inbox`
owns (`QdosAlphaCaseActivationPolicy`, the `IIntakeSubmission` binding, the
Inbox surfaces, mailbox administration), and the failing test precedes the fix.
The size bound is an operator decision, taken 2026-08-05: raise it to 750 MB.

1. **Separate the two size bounds.** `IntakeEnvelopeLimits` gains
   `MaximumMailboxContentLength` at 750 MiB. The 10 MiB figure is documented as
   the upload form's one-file bound (`current-architecture.md`), and the mailbox path
   had been reusing it; a received instruction carries the whole job. The
   upload form, the multipart request and the MCP tool keep 10 MiB — a 750 MB
   multipart body through a 0.5 vCPU / 1 GiB container is not a thing to
   allow. `ReceiveIntake` picks its bound by channel.

2. **Say what happened to a refused message.** The Failed view already listed
   it; the sentence was "The last message from this mailbox could not be
   processed" — a claim about the mailbox, made about one message, naming
   neither reason nor size. Every refusal code gets its own sentence, and a
   refused message carries its byte length (`EmailOperationProjection.SourceLength`)
   so the row identifies itself.

3. **Stop the receipt claiming a case that does not exist.** Allocation is
   non-blocking by design and was silent: no log, no telemetry, no marker. It
   now records the failure on the invocation activity, and the receipt view
   heads a definitive receipt with no case as "Case not created" rather than
   "Case created".

The bound is a parameter on `PollApprovedInbox` and `LocalApprovedInboxOptions`
so both sides of the boundary stay covered by a test without writing 750 MB to
disk. Nothing configures it; production takes the default.

Findings that are not code changes edit existing canonical files only — the
`NOW.md` queue line for the missing Principal, and `docs/current-architecture.md` for
the limits table.

## Verification

- `dotnet build --configuration Release` clean, then the full `dotnet test`
  run: 921 passed, 0 failed, 16 skipped (corpus-gated).
- `IntakeEnvelopeLimitsTests` pins the incident by byte count: the refused
  17,496,501-byte instruction is above the upload bound and at or below the
  mailbox bound, and the upload bound stays at 10 MiB.
- `MailFailureSentenceTests` proves no refusal code renders as the mailbox's
  "last message", and that a too-large message names both its size and the
  limit.
- `MailboxIntakeIntegrationTests` still exercises both sides of the envelope
  boundary and the quarantine/cursor mechanics, now against the injected test
  bound.

What this does not prove: the 750 MiB boundary itself is never written to
disk, so the shipped number is covered by the parameterised mechanism and the
byte-count assertions rather than by a message of that size. Nothing here is
live evidence — the fix reaches the mailbox only through a release, which is a
separate authorised operation this task does not claim. The refused message's
bytes are retained in production and can be re-driven once it ships.
