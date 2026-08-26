---
id: PLAT-006
type: ticket
title: Centre the operator shell's content region and redesign the Upload screen
status: done
area: platform-operations
order: 1450
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-19T07:15:39.013Z'
  review: '2026-08-19T07:45:40.074Z'
  verifying: '2026-08-19T08:08:14.794Z'
  done: '2026-08-20T01:29:40.398Z'
labels:
  - ui
  - design
links:
  - PLAT-001
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/design/README.md
commits:
  - 737fefce
  - '50151330'
prs:
  - '409'
deployment: production
archived: false
created: '2026-08-19T07:13:42.039Z'
updated: '2026-08-26T14:34:45.218Z'
---

## What

Two visual defects reported by the operator against release 10 (the first release carrying [[PLAT-001]]'s Claude Design shell):

1. **The content region hugs the rail.** `.app-rail-main` is capped at 1280px and left-aligned in the grid column, so on any viewport wider than ~1520px the page content is shoved against the rail and the right of the screen is a void. Operator decision (2026-08-19): centre the bounded content region in the space beside the rail — symmetric gutters at wide widths, no change under ~1500px, forms stay narrow inside it. Not a naive stretch.
2. **The Upload screen looks poor.** A small card floats in the void with a raw native `Choose file | No file chosen` control sitting inside a dashed "Drag a file here" box that does not actually accept a drop, and nothing tells the operator what happens after Upload.

## Why

FRD-12 requires the operator surfaces to be usable dense desktop layouts at 1280px and wider; the design authority (`docs/design/README.md`) fixes the rail shell and the 24px gutters but is silent on what happens beyond the content cap. The Claude Design prototype (`screens/shared.jsx`, `screens/Upload.html`) matches what shipped, so this is a refinement of the shell and one screen, not a departure from the design system.

## Approach

- Shell: centre `.app-rail-main` (`margin-inline: auto`) inside the rail grid column; keep the 1280px cap and the 24px gutters; leave the ≤1023px reflow untouched.
- Upload: make the whole dropzone the target (progressive enhancement in `site.js` — drop assigns the files to the real input; a styled `Choose file` button drives the native picker; the chosen file name and size are read back); keep the native input as the no-script fallback; state the accepted formats; add a short honest "What happens next" panel derived from the real flow (retained → processed → status page → case or receipt).
- Visual sweep of every family (Dashboard, Inbox, Queues, Cases, Case detail, Assessment, Administration, Operations, Upload status, New case) at 1440 and 1920 after the change; fix what is plainly wrong, file what is not.

## Verification

- [ ] `AccessibilityTests` / Browser suite green (no inline styles, landmarks intact).
- [ ] Web integration suite green.
- [ ] Screenshots at 1440 and 1920 of Upload, Dashboard, Cases before/after.
- [ ] Deployed and confirmed on production.

## Outcome
