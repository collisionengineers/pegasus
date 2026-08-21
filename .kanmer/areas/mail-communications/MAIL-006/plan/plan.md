# Plan — MAIL-006 (+ MAIL-008, PLAT-019 on the same branch)

One branch (`task/mail-006-inbox-message-page`, worktree
`../pegasus-worktrees/mail-006`, base origin/dev) delivers the page and its two
dependencies — the labels and the dialog copy exist only for this page's
design, so splitting the diff would ship raw slugs or dead copy in between.
PR to dev; the three tickets move together.

1. **Core** — `StaffForwardBodyCleaner.SplitForwardedHeader` (reuses the
   existing `ForwardedHeaderRegex`; one owner for the boundary).
2. **Labels (MAIL-008)** — `OperatorLabels.MailClassification(MailCategory)`
   (family label + " · " + humanized subtype, design's proposal);
   `MailClassificationSelection.Options` and `MessageModel.DecisionLabel`
   resolve through it, so picker, Decision card, corrections and the Index
   outcome cell share one list. The folder-move reason row is **omitted**
   (rows render only when populated) until the operator settles its wording.
3. **Dialog partial (PLAT-019)** — `_ReasonDialog` loses the default
   consequence sentence, the `Required.` hint and the placeholder; a passed
   consequence still renders (destructive actions keep their one sentence).
   Its inline script moves to `site.js` (CSP: inline scripts are dead in
   Production — latent defect fixed for all instances).
4. **Presentation** — `MailBodyPresentation.Present` (quoted header +
   run-on paragraph shaping) reusing the Core split.
5. **Page** — `Message.cshtml` rebuilt per the artboards; `Message.cshtml.cs`
   gains the `case` section (auto-selected when a case query/target rides the
   URL) and association redirects land on the Case tab. Move dialog keeps the
   existing contract: expected versions in the action URL, reason-only body.
6. **Index** — From cell loses the desk address subline (operator screenshot);
   the preview excerpt derives from the cleaned original body via the receipt's
   search text, falling back to the cleaned stored excerpt
   (`EfRetainedMailboxMessageStore`).
7. **site.css** — `.decision`, `.mail-*`, `.form-column`, facts pin,
   `record__head h1.wrap`; **site.js** — reason-dialog binder + Other-field
   toggle.
8. **Tests** — `MailWorkspaceWebTests` assertions moved to the new design
   semantics (same intents: no policy keys, exact-version correction, no
   transport identity in the move POST, association journeys); cleaner facts
   for the header split.

Deliberate departures recorded: no action bar (design README departure from
docs/design/README.md:176, per the approved design); one PR for three tickets
(interlocked diff — noted here and in each ticket).

## Simplification pass — 2026-08-21

- Dialog binding: partial's inline script deleted rather than duplicated;
  site.js is the single owner (also the CSP fix). Applied.
- Labels: one map in `OperatorLabels`; the selection list and every renderer
  resolve through it — no second label table. Applied.
- `SplitForwardedHeader` reuses the existing regex rather than a second
  boundary pattern (the reader/cleaner mirror-comment still holds). Applied.
- Decision-card rows kept single-line to match both the artboards and the
  test idiom; no new partial extracted for one page's card. Considered a
  `_DecisionCard` partial — rejected, single caller.
