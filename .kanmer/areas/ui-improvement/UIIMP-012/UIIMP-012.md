---
id: UIIMP-012
type: ticket
title: >-
  Reconcile EPIC-011 §1.5 "Notes panel" and D7's disabled-control clause with
  shipped Triage markup
status: backlog
area: ui-improvement
assignee: ''
profile: chore
labels:
  - ui
  - epic-contract
  - operator-decision
groups:
  - EPIC-011
links:
  - INTK-046
docs_todo: true
archived: false
created: '2026-08-29T08:29:54.971Z'
updated: '2026-08-29T08:29:54.971Z'
---

## Why

Two clauses of `.kanmer/groups/EPIC-011/context.md` are contradicted by
already-merged `dev` code and by `dev`'s own pinned assertions. Neither can be
settled inside a member lane: `context.md` is the epic owner's document, and the
test that pins the current behaviour is outside any single lane's owned files.
Raised out of [[INTK-046]] round-2 remediation.

## 1. §1.5 names the Triage history panel "Notes"; `dev` calls it "Permanent history"

§1.5 reads: `Notes panel (entries Date/Time/ID + text)`.

Evidence that the contract, not the code, is the stale side:

- `origin/dev:src/Pegasus.Web/Pages/Triage/Details.cshtml:348` already read
  `<h2 id="history-title" class="section-label">Permanent history</h2>` **before**
  INTK-046 touched the page. The name is pre-existing `dev`, not a lane invention.
- `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs:477` asserts
  `Assert.Contains("Permanent history", finalHtml, StringComparison.Ordinal)`.
  That file is byte-identical to `origin/dev`
  (`git diff origin/dev HEAD -- tests/` is empty) and is not in INTK-046's
  "Owns" list.
- Renaming the heading to the contract's "Notes" was tried and measured, not
  argued: build green, then
  `dotnet test ./Pegasus.slnx -c Release --no-build --filter "FullyQualifiedName~QdosTriageIntegrationTests"`
  → `Failed: 1, Passed: 8, Total: 9`, failing at
  `QdosTriageIntegrationTests.cs:line 477`, `Not found: "Permanent history"`.
  The experiment was reverted.
- `docs/frd/frd-03-triage.md` (one of INTK-046's own `refs`) owns the term
  ("...remain in permanent history"). Triage has no note entity; every entry is a
  retained business event.

§1.5's **entry shape** (Date/Time/ID + text) is shipped as specified. Only the
panel's name diverges.

**Decision needed:** either amend §1.5 to name the panel "Permanent history", or
rule that the panel is renamed to "Notes" — in which case this ticket also owns
the matching change to `QdosTriageIntegrationTests.cs:477`, since the rename and
the assertion must move in one diff.

## 2. D7's second clause bans a disabled control that two merged pages already use

D7 reads: *"Uncomposed integrations (Experian, Glass's, Audatex, Cazana) render
disabled as drawn; a disabled control is permitted only for a named, ticketed
integration seam."*

Read literally, the second clause forbids **every** disabled control that is not
an integration seam. Three merged facts contradict that reading:

- `src/Pegasus.Web/Pages/Cases/Details.cshtml:269` — "Open Assessment" gated with
  `data-condition="@(Model.CanOpenAssessment ? null : "Available after the current Review export")"`.
  A state gate, not an integration seam. Merged.
- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:765` — "Import estimate"
  gated with `data-condition="Only an Engineer can import an estimate"`.
  A role gate, not an integration seam. Merged.
- `src/Pegasus.Web/wwwroot/css/site.css:1893-1911` defines `.gated` /
  `content: attr(data-condition)` as a general design-system rule with its own
  forced-colours case at 1961 — not an integration-seam-specific rule.
- `QdosTriageIntegrationTests.cs:216-221` (pre-existing `dev`) asserts
  `Assert.Contains("Available once a finding is recorded", detailHtml)` under the
  comment *"Completion keeps its place with its condition named, rather than
  disappearing until it happens to work."*

The Rules bullet that cites D7 ("Every drawn control maps to a named handler or
an approved disabled seam. Never render an inert control") is satisfied by all of
these — each posts a real handler. The D7 table row's stricter clause is not.

**Decision needed:** either narrow D7's second clause to its subject (uncomposed
integrations), or rule the merged `.gated` state/permission-gate convention out —
which would be a multi-lane change touching `Pages/Cases/Details.cshtml`,
`Pages/Cases/Assessment/Index.cshtml`, `Pages/Triage/Details.cshtml` and the
`dev` assertions that pin them.

## Verification

- [ ] §1.5's panel name and the shipped heading agree.
- [ ] D7's wording and the merged `.gated` convention agree.
- [ ] `QdosTriageIntegrationTests` green after whichever way each is settled.
