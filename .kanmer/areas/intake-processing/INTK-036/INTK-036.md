---
id: INTK-036
type: ticket
title: Take the instruction date only from instruction-letter evidence
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - review-finding
  - extraction
  - fail-closed
links: []
archived: false
created: '2026-08-24T14:38:31.049Z'
updated: '2026-08-25T06:38:46.182Z'
---

## Why

[[ENG-015]] taught `QdosInstructionExtractionPolicy` to accept a bare `Date`
label so an instruction letter that writes only `Date: 21/08/2026` yields an
instruction date. But `ExtractFields` searches **every** content fragment, so a
`Date:` line in an appended report or an email body can now supply the
instruction date and suppress the receipt-date default that would otherwise
apply.

The prefixed-row guard is not a complete safeguard either: the generated
lookbehind recognises only one whitespace character before `Date`, so
`Report  Date:` or `Accident  Date:` (two spaces — which is how these letters
are commonly laid out) slips past it.

The effect is an ambiguous source silently resolved rather than failed closed,
which is the opposite of the product's stated intake posture.

## Not shipped blind

Raised by automated review on PR #534. Three findings from that review were
fixed there — the mapping version bump, moving the EVA mileage-unit vocabulary
into Core, and the extraction policy version bump to 7. This one was left
because restricting the label needs a notion of "instruction-letter context"
that the policy does not currently carry, which is a design step rather than a
one-line guard.

## What to do

Accept the bare `Date` label only from evidence that establishes
instruction-letter context, and widen the prefixed-row lookbehind to any run of
whitespace. Where context cannot be established, fall back to the receipt date
rather than taking the nearest date.

## Verify

An instruction whose letter carries only `Date:` still extracts it. A receipt
whose letter has no date but whose appended report carries `Report  Date:` falls
back to the receipt date and does not adopt the report's.
