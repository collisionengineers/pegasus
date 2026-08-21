---
id: MAIL-007
type: ticket
title: >-
  Suppress signature, disclaimer and wrapper content in the displayed message
  body
status: done
area: mail-communications
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-21T12:16:45.658Z'
  review: '2026-08-21T12:49:27.096Z'
  verifying: '2026-08-21T13:03:18.920Z'
  done: '2026-08-21T15:06:13.955Z'
labels:
  - ui
  - web
  - core
  - design-approved
links:
  - MAIL-006
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
deployment: production
archived: false
created: '2026-08-21T08:24:54.993Z'
updated: '2026-08-21T15:06:13.955Z'
---

## What

Cut the extracted rubbish out of the message body an operator reads on
`/Inbox/{id}`: the original sender's trailing signature block, corporate
disclaimer, link footer and wrapper lines. The target rendering is the letter
shown in the approved design at
`docs/design/references/mockups/inbox-message-page/Main.dc.html` — the
provider's message and nothing else.

## Why

The operator called this out as the point of the redesign: the page should
show the letter, not everything the extractor scraped off the end of it.

What already happens, so this ticket does not redo it —
`src/Pegasus.Core/Intake/StaffForwardBodyCleaner.cs`:

- strips leaked inline-image `cid:` tokens in all three bracket forms;
- on a staff forward, drops everything above the first forwarded header, which
  is the CE forwarder's own preamble and signature;
- normalises line endings and collapses runs of three or more newlines to two.

What it does not do, and this ticket adds: anything below the *original*
sender's sign-off. The forwarder's signature is removed; the provider's is
not. That is what still reaches the page.

Paragraph rendering and the quoted-header treatment are [[MAIL-006]]; this
ticket is only about what text survives.

## Approach

- Research first, against real retained bodies — the rule cannot be written
  from one sample. `corpus/` is readable (never modified, never uploaded), and
  the deployed instance has real retained mail. Establish which trailing
  shapes actually occur before choosing a rule.
- Extend `StaffForwardBodyCleaner`, not a second cleaner. It is already the
  one owner of this policy and a pure text function with no MIME dependency;
  a second implementation is a stop condition under `CLAUDE.md`'s one-Core-owner
  rule.
- Keep the sign-off. `Yours faithfully` / `Neil Duncombe` is the letter; the
  company address block, the disclaimer paragraph and the link footer beneath
  it are not. The boundary is the thing to get right and the thing to test.
- Fail open, never closed: if no boundary is confidently found, show the body
  unchanged. Truncating a real instruction is far worse than showing a footer,
  and the operator cannot see what was hidden.
- Note the coupling recorded in the file: the forwarded-header regex is kept
  byte-identical to `MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex`
  so display and classification agree on where a forward begins. If that
  pattern is touched, both change together.
- Classification reads the same text. Confirm whether trimming changes any
  classification predicate outcome before shipping; if it does, that is a
  behaviour change needing its own decision, not a silent side effect.

## Verification

- [ ] Unit tests over real body shapes taken from the corpus, including at
      least one where no boundary is found and the body is shown whole.
- [ ] No test body loses a line of the provider's actual instruction.
- [ ] Classification outcomes are unchanged across the sampled bodies, or the
      change is recorded and accepted.
- [ ] The rendered page matches the letter in the approved artboard.
- [ ] `dotnet test` green for the Intake and Mail suites.
