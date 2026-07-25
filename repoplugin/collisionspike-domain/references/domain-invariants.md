# Domain invariants

`Audit` below means Collision Engineers' business work type only. Accountability and technical evidence use permanent action history, action events, security logs, or content-safe telemetry.

## Inputs and identities

- Reference allocation requires a principal with a principal code, the applicable two-digit year, and an atomic principal/year sequence.
- A principal code becomes immutable when it is used for the first time.
- Vehicle registration identifies image-led work before allocation; it never substitutes for the eventual case reference.
- Standalone Audit allocation also requires an unambiguous repairable or total-loss assessment from the original Engineer's report.
- Business Triage is a separate pre-case record. An active Triage requires a vehicle registration; otherwise its source remains in `Needs sorting`.
- Sent-report and completed-Triage reply evidence requires one exact Outlook Sent item from an Administrator-maintained allowlist of approved shared and individual staff mailboxes.

## Ordered decisions

### Allocate a case reference

1. Reject allocation if principal identity/code is absent.
2. Allocate atomically from the shared sequence for that principal and year; Inspection and Audit work do not have separate counters.
3. Apply `a.` or `ap.` only for the applicable Audit business rule.
4. Persist the case principal/reference as immediately immutable.

### Replace a principal code legitimately

1. Create a linked replacement principal; never edit the used code.
2. Atomically deactivate the predecessor at cutover.
3. In the cutover year, initialise the replacement from the predecessor's next number.
4. In each later year, initialise the replacement sequence at `001`.

### Correct wrong-principal case allocation

1. Require a reason.
2. Close the erroneous original as the distinct terminal outcome `Created in error`.
3. Create a new replacement case under the corrected principal and link both cases.
4. Never reuse either reference and never reopen the erroneous original.

### Reopen a closed case

1. Reject reopening for `Created in error`.
2. Require an authorised staff actor, an entered reason, and an otherwise-valid nonterminal destination.
3. Exclude `Held`; entering it is a separate action.
4. Enforce every normal gate for the selected destination and retain the reopen action in permanent history.

### Schedule and pause chasers

1. On entry to `Not ready`, schedule the first chase for the same Europe/London local clock time seven calendar days later.
2. Continue the seven-calendar-day cadence while required material remains missing.
3. On entry to `Held`, preserve the prior state and any remaining local-clock interval while leaving `Due by` visible.
4. Offer only the prior state or `Review` on release. A return to `Not ready` resumes the preserved remainder; `Review` ends the missing-information chase.
5. Stop future chasers when required material arrives or the case terminates.

### Record a sent report

1. CollisionSpike detects evidence; it never automatically sends a report and applies no pre-send report review gate.
2. Accept only one exact Sent item from an approved mailbox. Automatic matching is not yet authorised.
3. If evidence is absent or ambiguous, allow an authorised staff user to link the exact item with an entered reason.
4. Use Outlook `sentDateTime` as the authoritative business time; retain discovery and link times separately.
5. Permit any staff role to unlink/relink with a reason, then recompute dependent events and dashboard counts.
6. Once confirmed, keep the sent event final even if Outlook later moves or deletes the message.

## States and terminal outcomes

- Case terminal outcomes are post-report completion, provider cancellation, Collision Engineers rejection, and wrong-principal `Created in error`.
- `Not ready` is incomplete work being chased. `Review` is complete work awaiting the pre-Engineer-assignment approval. `Held` is a reasoned manual pause that stops progression and chasers while due dates remain visible.
- `Blocked intake` is a manual inbox filter with a required reason, not a case state. It retains the source and creates no case/reference until resolution and retry.
- Triage states are `Open` or `Awaiting information`, then `Finding recorded`, then `Completed`. `Cancelled` is the only end without a finding. A Triage has an optional assignee, no due date, and no chasers.
- Administrator, Engineer, and User roles may record a binary Triage finding of `Roadworthy` or `Unroadworthy`.
- Triage completion requires the exact reply-chain Sent item. Before send, finding replacement requires a reason. After send, store a superseding finding, require a new response, and preserve history. Reopening always returns to `Open`.

## Permanent action history

- Include business mutations, downloads/exports, material denied or failed business actions, automated business results, and external information actually accepted, linked, or used.
- Include account creation, role changes, disabling, and credential administration; sign-ins belong in the separate security log.
- Exclude routine views, searches, refreshes, polling, retries, leases, heartbeats, and adapter mechanics; send these to content-safe telemetry where operationally useful.
- Each permanent action stores structured before/after field values, actor, time, entered reason when required, and outcome. It never stores secrets or file/message bodies.
- Require an entered reason for hold/release, cancellation, rejection, reopening, corrections, reversals/unlinks, principal/reference replacement changes, logical removal, overrides, and account/configuration changes.

## Integration and linking invariants

- Box folder/file identity uses the accepted EVA/reference convention.
- External adapters translate; they do not independently decide case workflow, categorisation, or matches.
- An external failure must be retryable or visible without duplicating a business action.
- Secrets are referenced by name or identity and never included in domain data or logs.
- Triage-to-case association is automatic only after the combined research accepts a definitive shared match; otherwise staff confirm it. Keep Triage as a separate linked record. Each Triage links to at most one case; a case may link multiple Triage records. Any staff role may unlink/relink with a reason.

## Dashboard invariants

- Calendar days use Europe/London midnight-to-midnight boundaries; calendar weeks run Monday midnight to the following Monday midnight.
- `In today` counts cases created in the current London calendar day.
- `Sent to Engineer` has today/week totals and counts once per case. The first MVP uses first successful EVA JSON/image export generation as an explicit proxy; it does not prove EVA receipt. A future EVA replacement records actual assignment.
- `Reports sent` has today/week totals and counts every successfully sent report.

## Completeness

Instruction and image completeness are separate staff judgements. When the configurable gate is enabled, both operator-confirmed flags are required before Engineer assignment. Continue to show missing and contradictory values, but do not enforce a principal-specific or universal field matrix. There is no pre-send report review gate.

## Examples

- If predecessor principal `OLD` has issued `OLD26004`, a legitimate replacement principal first issues its own-code 2026 reference ending `005`; its first 2027 reference ends `001`.
- If case `QDOS26005` was allocated under the wrong principal, close it as `Created in error`, create and link a new case under the corrected principal, and reuse neither number.
- If a case enters `Not ready` at 14:30 Europe/London on Tuesday, its first chase is due at 14:30 the following Tuesday. If it enters `Held` with two days remaining, release back to `Not ready` leaves two days remaining.
- Two successfully sent reports for one case count as two `Reports sent` events but the case counts only once in `Sent to Engineer`.

## Open question

Mailbox categorisation and all automatic email matching remain one combined research decision at `docs/plans/mailbox-categorisation-and-email-matching/README.md`. Until accepted, do not infer categories or matches from predecessor labels, subjects, vehicle registrations, or corpus examples.
