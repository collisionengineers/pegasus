---
id: INTK-046
type: ticket
title: 'Port Triage, Unidentified, Received and the image-record pages'
status: done
area: intake-processing
assignee: zcode
profile: feature
stageEntered:
  preparing: '2026-08-28T18:33:22.013Z'
  implementing: '2026-08-28T18:41:16.131Z'
  review: '2026-08-28T21:35:36.002Z'
  verifying: '2026-08-29T09:19:38.119Z'
  done: '2026-08-29T10:00:51.637Z'
labels:
  - ui
  - wave-2
  - triage
  - unidentified
groups:
  - EPIC-011
links:
  - UIIMP-012
  - PLAT-061
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - 6dfa674d94e66b76a04184dbff2b96270ac7534b
  - 702833aef42a3e15c1cf97549645369fa635c6ea
  - fc24dc65169ee89aa770cf7503dcfe53ba42e66d
  - d1591f24b775879962610dcd7f36650c9cf2990d
  - 72addf22dd75e83ea35fb5119717e41916c8dd2a
  - 0578835e114018ed20d57871209dfc55326ac57a
  - d39db01672bad8ddb6005a76b1c2bd292f925990
prs:
  - '#605'
archived: false
created: '2026-08-28T08:35:23.884Z'
updated: '2026-08-30T20:24:27.502Z'
---

## What

Wave 2 lane C2 of [[EPIC-011]]. Port `Pages/Triage/Details` (§1.5: determinations panel saving both findings through the existing `OnPostActionAsync` record_finding path, source panel, notes panel; the other transitions stay reachable through dialogs where a handler exists), `Pages/Unidentified/Details` (§1.6: retained source panel, history, resolve dialog with destination select), `Pages/Intake/Details` (Received workbench restyled, handlers unchanged) and `Pages/ImageIntake/Details` (the image record per D1, gallery retained, back link to `/Cases?tab=not_ready`).

> §1.5's "notes panel" ships under the name `dev` already used and
> `QdosTriageIntegrationTests.cs:477` pins — "Permanent history". §1.5's
> entry shape (Date/Time/ID + text) is unchanged. The name divergence is
> [[UIIMP-012]]'s to settle; it cannot be closed inside this lane without
> breaking a pre-existing assertion in a file this lane does not own.

## Owns

`src/Pegasus.Web/Pages/Triage/Details.*`, `Pages/Unidentified/Details.*`, `Pages/Intake/**`, `Pages/ImageIntake/Details.*`, tests `TriageEvidenceImagesWebTests.cs`, `QdosIntakeWebTests.cs`, `GroupedIntakeWebTests.cs`, `ImageIntakeWebTests.cs`, `ImageViewingWebTests.cs`.

## Blocked by

[[PLAT-029]].

## Raised out of this lane

[[UIIMP-012]] — §1.5's panel name and D7's disabled-control clause vs merged code.
[[PLAT-061]] — `.gated::after` empty tooltip pill; `site.css` is PLAT-029's file.

## Verification

- [x] Every button posts an existing handler; no inert control. — audited 2026-08-29 over all four owned pages: every `data-dialog-open` target resolves to a declared dialog, every form posts a handler that exists on its page model, and the one disabled control (Complete) posts the same `complete` action in both states. Audit recorded in the plan's round-2 dispositions.
- [ ] No clipped text/overflow at 1580/1100/760. — the orchestrator's browser walk (UIIMP-010) per EPIC-011; not this lane's to claim.
