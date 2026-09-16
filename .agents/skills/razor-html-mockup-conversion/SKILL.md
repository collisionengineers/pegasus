---
name: razor-html-mockup-conversion
description: Run Stage 2 for Pegasus. Convert an approved vNN HTML mockup and its page planning decisions into the live Razor Pages application, with FRD and ADR updates in the same delivery and side-by-side conformance evidence. Use when a mockup round has been approved and the implementation, conversion or Stage 2 is asked for; not for building or reviewing the mockup itself.
---

# Razor HTML mockup conversion

Stage 2 begins only after the operator approves the mockup round produced by
[razor-html-mockup-creation](../razor-html-mockup-creation/SKILL.md) and every
lettered sign-off item is settled. This skill owns what the implementation
takes from the round and how it closes it.

## Inputs

- The approved `pegasus_*_vNN.html` files and `vNN-shots/`.
- `vNN-notes.md`: its decisions with their authority, the sign-off list with
  each outcome, and the deliberate departures from live.
- Every `pages/**/how-it-should-work.md`: the numbered rules are the
  specification; the "Where this lands" tables say which pages change; the
  "Documentation impact" lines become FRD, ADR and CONTEXT.md edits in the same
  delivery, not later.
- The `how-it-works.md` source tables name the owning files per layer.

Plan the work from these, page by page, front end and back end together. When
a rule needs a Core change, Core owns it and Web consumes it.

## What is not product

- The mockup strip, query-string presets, `localStorage` variables and
  synthetic fixtures. Only the settled default of each strip variable ships;
  the alternatives are not feature flags.
- Demo returns, representative failure codes and generated images. The live
  path uses the canonical command, the gateway's codes and stored evidence.
- Departures the notes list as deliberate are decisions, not defects to
  "fix" back to the mockup or back to live.

## Policy stays in Core

The mockup mirrors Core shapes it read from `origin/dev`
(`ValuationCalculations`, `GlassLabels`, `PendingEstimateSources`,
`CanLaunchGlass`). Implement against those symbols; do not re-derive a rule from
the mockup's JavaScript. Where the mockup and Core disagree, the sign-off list
says which wins; if it does not, stop and ask.

## Mechanics

Use [razor-pages-ui-implementation](../razor-pages-ui-implementation/SKILL.md)
for the Razor choices. Reuse the live shell, tokens, partials and scripts the
mockup was built from; the mockup's CSS is a proposal for the same tokens, not
a second stylesheet. Keep the frame rules the notes state as numbers.

## Conformance evidence

- Screenshots of the routed page at 1580×1000, 1440×900 and 760×1000 in the
  same states the mockup shots show, compared side by side with the
  `vNN-shots/` file of the same number.
- The behaviours the notes table promised (edit in place, no page movement,
  availability labels, one geometry) proven on the routed page, not inferred
  from markup.
- Review with the Stage 2 lens in
  [razor-pages-ui-review](../razor-pages-ui-review/SKILL.md).

## Close-out

- The planning folder is temporary. In the final Stage 2 PR remove it, or
  retain it by operator instruction with a README that states it is
  historical reference and that current behaviour is owned by the PRDs, FRDs
  and ADRs, as v26's does.
- The FRD and ADR edits from "Documentation impact" land before or with the
  code, and the docs index routes to them.
