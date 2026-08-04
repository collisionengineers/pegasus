# Page 6 — Email operations (`/Operations/Email`) review

Reviewed from `operations-email.png` and `src/Pegasus.Web/Pages/Operations/Email.cshtml`
(labels in `Email.cshtml.cs`). Under the new IA this screen becomes the Dashboard's
**E-mail activity** drill-down.

## 1. Aesthetics

- The heading stack is eyebrow + h1 + lede: "OPERATIONS" / "Email" / "Bounded
  approved-mailbox Received and Sent processing outcomes." (`Email.cshtml:10-12`). Three
  lines to say "e-mail". The lede is the single worst sentence on the page — "Bounded"
  and "processing outcomes" are compositional jargon narrated at an operator who wanted
  to know whether e-mail worked today.
- The screenshot's default state is two empty full-width panels ("No Received processing
  outcomes are recorded." / "No Sent processing outcomes are recorded.",
  `Email.cshtml:26,96`) under letter-spaced uppercase labels. An entire page whose normal
  appearance is two grey apology boxes.
- When items exist, each is a full-width `article.panel` with an `<h3>` like
  "Received · Failed" (`Email.cshtml:33`) — section name repeated into every card heading,
  then a three-row `<dl>`. Five failures means five stacked cards of near-identical
  chrome. There is no table, no density, no scanning axis.
- Timestamps render with `ToString("u")` (`Email.cshtml:41`): `2026-08-04 15:24:11Z` —
  a machine format with a trailing Z, inconsistent with the `dd MMM yyyy HH:mm` used
  elsewhere in the product.

## 2. Practicality

- The page is orphaned: the only route in is a Dashboard workspace card that itself says
  "Unavailable". An operator cannot discover this screen.
- Links are labelled by mechanism, not destination: "Open Intake receipt", "Open Triage",
  "Open Intake queue" (`Email.cshtml:48,52,60`). "Owner" as the `<dt>` for that link
  (`Email.cshtml:44`) is developer vocabulary — an outcome does not have an "owner" in
  any sense an operator uses.
- Failure detail prints the raw `FailureCode` string with no human explanation
  (`Email.cshtml:65-71`): a code token is the entire failure story.
- "Retry Received processing" (`Email.cshtml:80`) posts immediately on click — no
  confirmation, no designed in-page result state beyond a generic status card at the top
  ("Mailbox processing was scheduled for retry.", `Email.cshtml.cs:76`). After clicking,
  the button is still there looking un-clicked.
- The truncation notice narrates internals: "Showing the latest
  @GetEmailOperations.MaximumItemsPerDirection Received outcomes." (`Email.cshtml:88`) —
  correct idea, but "outcomes" again, and there is no way to see older items at all.
- Mailbox fallback is the bare string "Not recorded" (`Email.cshtml:37`) with no visual
  distinction from a real mailbox name.

## 3. Performance / Design / Good practice

- Received and Sent are two nearly identical 70-line copy-paste blocks
  (`Email.cshtml:22-90` vs `92-163`) differing only in link fallbacks and one Principal
  row. A single partial/loop with a direction parameter would halve the markup and stop
  the two sections drifting.
- A fresh GUID `operationKey` is minted per render for every retry form
  (`Email.cshtml:79`, `Email.cshtml.cs:104`). That is the right idempotency pattern, but
  it means a stale tab silently generates a new key on refresh; the guard is really the
  `expectedFailureCode`/`expectedDueAtUtc` pair — fine, and worth keeping in the rebuild.
- `<time datetime="…">` with ISO round-trip values (`Email.cshtml:41`) is good practice —
  keep it, fix only the visible format.
- `role="status"` on the status card and `aria-labelledby` on sections are correct;
  the retry `<button>` inside a form with hidden state is honest progressive-enhancement
  HTML. The bones are fine; the vocabulary, density, and discoverability are not.
- Whole projection loads on every GET with `NoStore` caching — acceptable for a capped
  list, but the cap plus "no older items" means the page cannot answer "what failed last
  week", which is the one question a retry screen exists to answer.
