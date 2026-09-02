---
id: PLAT-010
type: ticket
title: >-
  Strip UI narration estate-wide to the design rule: one H1, no ledes,
  one-sentence consequence guidance
status: done
area: platform-operations
order: 1540
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-19T23:45:41.132Z'
  review: '2026-08-19T23:46:35.334Z'
  verifying: '2026-08-20T00:21:58.536Z'
  done: '2026-08-20T01:29:46.400Z'
labels:
  - ui
  - design
  - copy
  - operator-reported
links:
  - DELIV-012
  - PLAT-009
refs:
  - docs/design/README.md
  - docs/frd/frd-12-operator-experience.md
prs:
  - '431'
deployment: production
archived: false
created: '2026-08-19T23:00:23.570Z'
updated: '2026-09-01T14:44:33.121Z'
---

## What

Bring every operator-facing page into compliance with the binding copy rule the design authority already states (`docs/design/README.md:160`):

> Controls communicate purpose without narrating obvious actions. Screens carry no lede or subtitle: one H1 and the content. Guidance appears only beside a control whose action has a consequence the operator must understand, and is one sentence.

Remove ledes/subtitles, notice-banner narration, and multi-sentence guidance paragraphs; keep one-sentence consequence guidance beside the specific control it governs; move genuinely operational knowledge into `docs/runbook.md`/`docs/operations.md` rather than deleting it. Also re-check surviving copy against the banned-terms rule (`:161` — no queue/lease/version/ingress/adapter/AI-mechanics vocabulary; "intake" never operator-facing).

## Why

Operator, 2026-08-19, verbatim: *"there is an enormous amount of UI narration and copy occurring that should not be. This looks extremely bad and unprofessional."* The rule already exists; the pages drifted from it. Audit counts of prose blocks (`<p>` occurrences) at filing time:

| Page | Blocks |
|---|---|
| `Cases/Assessment/Index.cshtml` | 33 |
| `Cases/Shared/_CaseWorkflow.cshtml` | 24 |
| `Intake/Details.cshtml` | 21 |
| `Triage/Details.cshtml` | 11 |
| `Cases/Create.cshtml` | 8 |
| `Administration/Index.cshtml` | 8 |
| `Administration/Automation/Index.cshtml` | 7 |
| `Cases/Assessment/Suggestions.cshtml` | 6 |
| `ImageIntake/Details.cshtml`, `Administration/Principals/Index.cshtml` | 5 |
| `Operations/Index.cshtml`, `Mail/Message.cshtml` | 4 |

Plus two pages passing `ViewData["Lede"]` (`Administration/Principals/Create`, `Cases/Assessment/Suggestions`) against "screens carry no lede", and the `_PageHeader` partial's lede slot itself.

[[PLAT-009]] owns the Mailboxes page (layout + copy); this ticket owns the rest of the estate and the shared partials.

## Constraints

- Copy and layout only; no handler, model, route or behaviour change.
- Not every `<p>` is narration: validation messages, empty states, honest status statements and one-sentence consequence guidance are approved copy (design README § Voice, labels and necessary copy). The test is the rule, not a word count.
- Knowledge moved out of the UI must land in the runbook/operations section that already owns the topic.
- Browser + AccessibilityTests must stay green; tests asserting removed copy update to the new honest structure.

## Verification

- [ ] No page renders a lede/subtitle; `_PageHeader`'s lede slot removed or unused.
- [ ] No multi-sentence guidance paragraph remains beside any control; consequence guidance is one sentence.
- [ ] Banned-terms grep clean over operator-facing markup.
- [ ] Browser/a11y suites green; visual pass over the worst offenders at 1920.

## Outcome
