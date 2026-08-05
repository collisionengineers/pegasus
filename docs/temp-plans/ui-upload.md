# UI Upload — task plan

Branch `task/ui-upload`. Page 2's split-out manual-submission surface.

## The defect this closes (B1)

The upload form rendered `action=""`. The `asp-page-handler="ReceiveIntake"` +
`asp-route-page` combination never generated a handler URL, so the browser
POSTed to `/Intake` with no handler, nothing matched, and Razor Pages silently
re-rendered: HTTP 200, no receipt, no work item, no error shown. **The only
manual submission path in the product was a dead button.**

`/Upload` is a plain page with an unnamed handler, so the form posts to its own
URL and there is no handler name to fail to generate.

## What else changed

- **Vocabulary**: "Receive intake", "Queue instruction" and "Original bytes are
  retained before durable processing" are gone. The first two are pipeline
  words; the third narrates storage at someone who wants to send a file.
- **Accepted types are stated**, not left in an `accept` attribute nobody can
  read, and the limit is stated in MB.
- **Oversize** says what the file is and what the limit is, rather than
  rejecting and leaving the operator to work out why. Files above the transport
  cap are caught by the status-code page from the shell change, so a raw
  HTTP 400 is no longer reachable either.
- **Every outcome names what the file became** and, where there is something to
  open, goes there: a case, or the received item. Queued work stays on the
  Upload page and says it is being processed, because the receipt does not
  exist until the Worker writes it — the old redirect claimed success on a list
  that then read "No intake receipts match this view" (defect M9), and sending
  the operator to a record that is not there yet would be a 404 dressed as one.
- The malformed-receipt-token post is still refused rather than quietly given a
  fresh key, which would turn a replay into a second receipt.

## Verification

- Core 441/441, integration 399 passed / 0 failed
- The test harness posts to `/Upload` and reads the receipt from where the
  upload lands; assertions follow the new copy
- One real accessibility fix: the confirmation card's green-on-green-tint text
  failed the contrast floor. The tick and rule keep the green; the sentence is
  ink, which is right anyway — colour is the second signal, not the message.
