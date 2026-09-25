# v30 discussion log

## 24 September 2026 · Initial request

Operator: “run $razor-html-mockup-creation and create 5 different new designs
for this flow.” Five offline options were built around the live Upload,
processing and destination states.

## 24 September 2026 · Copy correction

Operator: “too much UI narration”. The design copy was cut to short state,
field, and action labels. Repeated descriptions below the page and card
headings were removed. This raised sign-off items B and C in the notes.

## 24 September 2026 · Shell correction

Operator: “it also completely doesnt resemble the actual pegasus”. The
hand-drawn shell was replaced with the real v28 captured Pegasus shell and
the current `origin/dev` CSS, font files, logo, rail and utility bar. All
five alternatives now change only the Upload content. This raised sign-off
item A and prompted a new screenshot set.

## 24 September 2026 — professional polish

Operator asked to spruce up each design. Refined all five using the live Pegasus
shell and tokens, with restrained copy, consistent spacing, file icons, compact
status badges, and a clearer primary action. Updated the screenshots and reran
the offline interaction check. No implementation option has been approved.

## 24 September 2026 — Case proposal is primary

Operator: “fundamentally the page itself is flawed against our process” and
“It should propose a case if that is possible but this is shown akin to more
of a secondary option”.

All five options now show the proposed Case first, with exact identifying
facts and confirmation. Added no-match, multiple-match and attached presets.
Removed manual image registration: FRD-18 specifies automatic registration
and candidates first. This hierarchy is settled; layout selection stays open.

## 25 September 2026 — five surfaces, one walkthrough

Operator: "We are looking to add polish and improvement to: 1. login
screen 2. Inbox page 3. Work centre page 4. administration pages, e.g.
editing staff account 5. The already existing untracked upload page/flow.
This does not require a full playwright run/screenshot set, just prepare
the mockups for examination first. Create a combined version that I can
walk through and view each page for. Ensure the latest changes from the
currently active dev → main PR are factored in."

**What was explored.** `origin/dev` `32dabfc59` (Release 64 plus PRs 852,
854 and 855): `_Layout.cshtml` and `_LayoutAuth.cshtml`, `SignIn`,
`PasswordChange` (PR 828), `AccessDenied`, `Mail/Index`, `_WorkCentreBody`
(Triages metric, the one Refresh partial from PR 838),
`Administration/Accounts/Index`, `_AdminNav`, `OperatorLabels`, the design
authority and the UI guardrails. The operator's two screenshots (the
sign-in card and the `alex` settings dialog) are the baseline references.

**What changed.**

- The round moved from `v29_planning` to `v30_planning`: `dev` had shipped
  its own v29 round (Case referencing) while this one was in progress, and
  the two folders collided on merge. The Upload files are renamed `_v30`.
- The Upload mockups' frame moved from the v28 capture (which still drew
  the working-set strip removed on 24 September) to a shell rebuilt from
  the live `_Layout.cshtml`.
- Four new surface files, each with a Baseline layer (the live page rebuilt
  with a fixture) and a Proposal layer, and a walkthrough frame over all
  nine files.
- Items H to Z raised; nothing decided. See v30-notes.md Part 2.

**Not done, by instruction.** No screenshot set; `shoot.ps1` is ready for
it. Twelve verification captures were taken to check the build.
