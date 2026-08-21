# Plan

Committed in `7198c1c2`. A four-line diff; this plan is deliberately shorter than most.

## The three defects and their fixes

| Line | Was | Now | Rule |
| --- | --- | --- | --- |
| 137 | `No Deleted Items in the bounded approved scope matched "…".` | `No Deleted Items matched "…".` | banned word `bounded`; scope narration the folder navigation already shows |
| 123 | `Enter a search term to read accepted Deleted Items within the selected approved mailbox scope.` | *(removed)* | "A field is a label and a control, nothing more. No hint sentence under a field." |
| 115 | `Search includes retained messages in their current Outlook folders.` | *(removed)* | "A page never describes its own mechanics, workings, derivations." |

## Why nothing was written to replace them

The approved necessary-copy list is closed and operator-owned. Every change here removes
text; none adds any. Where a sentence was doing real work — telling the operator the
Deleted Items view needs a search term — the control itself carries that: the field is
`required` and the results area is simply empty until one is entered, which is what
"only populated, relevant sections render" asks for.

## Judgement recorded

The mailbox and folder `<nav>` elements at lines 19 and 35 were **checked and cleared**.
They look like pill rows, but they are navigation between mailbox and folder scopes with
`aria-label`s, not table filters standing in for a dropdown — and the actual view
filtering already uses a labelled `select` in the `filterbar` form. Reporting them would
have been a false positive.

The remaining `empty-state` paragraphs on the page are search results ("no mail matched
X"), not empty-state panels for sections with nothing recorded. Left alone.

## Acceptance

- The three strings are gone. ✅
- The banned-word scan over `Pages/**/*.cshtml` returns only Razor comments and C#
  identifiers. ✅
- `Pegasus.Web` builds clean. ✅
- Live: the Deleted Items search before, during and after a search — Phase 6.

## Simplification pass

2026-08-21. The change *is* the simplification. No findings deferred.
