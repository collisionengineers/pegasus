2026-08-21 visual pass: rendered the redesigned page through the pinned-Chromium
browser harness at 1280×800 (temporary capture fact, deleted after use) with an
EREF24-shape seeded message. Matches the Main artboard: record head with
wrapping subject + amber "Not yet processed" chip, tabs with attachment count,
sender headline + muted route, quoted forwarded header, letter as tight run-on
paragraphs with paragraph breaks, Decision card with only populated rows,
everything above the fold. Two defects found and fixed during the pass:

1. The legacy `.mail-body { white-space: pre-wrap }` rule leaked into the new
   paragraph div (whitespace between <p> tags rendered as blank lines) — rule
   scoped to `pre.mail-body`.
2. The receipt-less excerpt fallback showed the raw forwarded-header block —
   the fallback now skips it via the same `SplitForwardedHeader`.

Screenshots: scratchpad `mail006-message-main.png`, `mail006-inbox-list.png`
(session scratchpad; copies attach to proof at verifying). Browser lane
(Category=Browser) 47/48 with the one failure a parallel-run flake in the
custody/EVA journey — passes in isolation. Full-rig local run
(Invoke-LocalDevelopment) hit a pre-existing func-host packaging quirk ("no job
functions found" with --no-build); visual QA used the browser harness instead —
quirk noted, not chased in this ticket.
