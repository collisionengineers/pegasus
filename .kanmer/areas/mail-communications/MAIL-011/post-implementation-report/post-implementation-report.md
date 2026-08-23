# Post-implementation report

**PR #523**, merged at `7d6a948a`, deployed as release 26.

## What shipped

`StaffForwardBodyCleaner` owns the forwarded-header shape and exposes it as
`ForwardedHeaderPattern`; intake's source reader deleted its byte-identical
copy and reads Core's. The pattern allows `Cc:` and `Bcc:` between `To:` and
`Subject:` — the two lines Outlook writes there.

Each caller keeps its own rule about what a match *proves*: the reader still
demands exactly one forwarded block carrying one external sender, because route
identity is fail-closed evidence; the cleaner still takes the first, because
the outermost forward is the one to display.

## Evidence

- Verified against U34's own retained body read from production **before** any
  code was written: shipped pattern 0 matches, new pattern 1, group `from` =
  `Robin Anderson <randerson@qdosassist.co.uk>`.
- Core: three new `StaffForwardBodyCleanerTests` covering `Cc:`, `Cc:`+`Bcc:`,
  the Cc line belonging to the header rather than the message, and a block with
  no `Subject:` still not being a header.
- Infrastructure: `ACopiedRecipientInTheHeaderDoesNotHideTheOriginalSender`
  maps directly onto U34, and `TwoForwardedBlocksStillProveNoOriginalSender`
  pins the one behaviour change — a body can now match twice, and two blocks
  still fail closed.
- The independent reviewer measured the widened pattern for catastrophic
  backtracking: 8,000 `Cc:` lines in a 96 KB body = 33 ms, linear. Every
  iteration must consume a literal `Cc:`/`Bcc:`, so the count is bounded by
  real Cc lines. (Its sibling [[MAIL-012]] was **not** safe — see there.)
- CI green on `ce4d646c`; Core 937 local.

## Deviations from plan

None. One addition from review: the double-match transition is now pinned by a
test rather than only asserted in prose.
