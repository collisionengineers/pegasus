# v36 discussion log

Chronological record of how the round came to be. It is not design authority. See the [notes](v36-notes.md) for what stands.

## 9 October 2026: the brief

The operator asked:

> Perform a full walk of the case page, including edit mode and non-edit mode. Nitpick the layout, UI, display, and UX. This is not a redesign, it is a sharpening, adjustment, and improvement focus. Go very thorough and in depth on this. Create a mockup using /razor-html-mockup-creation with a widget to toggle between today and your proposal, and include any other relevant states or widgets that will display differences.

While the round was being planned the operator added three remarks, each with a screenshot:

> get valuation button also looks weird where its currently placed

(the Glass's guide card in the Valuation section, edit mode: the Get valuation button at the right of the card head above a column of right-aligned narrow boxes)

> the "not recorded" looks weird with the radio buttons - any alternatives?

(the Decisions strip: Outcome and Roadworthiness drawn as segmented radios with "Not recorded" as the selected segment)

> the following buttons also require busy states similar to the rest of application

(the dark arrow link buttons Assign Engineer and Open Triage on the Work Centre rows, and Open full Case on the Cases list's Quick detail)

> the repair spec columns arent aligned very well. Also "Unit £" looks visually poor

(the Repair Spec line grid: a Glass's-imported line whose figures and headers do not share an alignment, Hours clipped at "0.700(", and the "Unit £" header)

> on the right hand case rail: "Import an estimate, bring one back from Glass's or add a new repair spec on the Repair Spec section; Use repair spec switches to an existing one." remove this text - UI narration / unnapproved copy

(the blocker's "how" sentence in Next action and the Report not ready card; finding f59, item AB)

Asked how to treat findings that are behaviours a static mockup cannot show, the operator chose: list them in the notes with a Stage 2 scope letter, and draw the ones with a visible state as strip toggles in the mockup.

### What was explored

- The live Case page sources on `origin/dev` 37c4b96f5 (`Details.cshtml`, every `_Case*.cshtml` partial, `case-workspace.css`, `case-workspace.js`, `site.css`, `site.js`), the design authority, the Case-workspace guardrails and FRD-16.
- The v33, v34 and v35 rounds' captured frames, which share one capture from 6be875542 and predate the v34 Valuation cards, the v35 aside and the ribbon's Registration cell. The v35 frame also drops the page's scripts, so no-script fallbacks show (the Repair Spec head's raw file input). This round captures the page afresh from the running synthetic host with its scripts, in read and edit.

### Decisions in the brief

- Sharpening only: the v26 frame, section order, aside model, read/edit shared geometry and the Actions menu stay.
- One mockup with a Today / Proposal toggle, per-finding switches, and difference widgets.
- Findings in three tiers: alignment inside the contract (listed), changes to something the operator placed or an FRD rule (lettered), behaviours (notes table with a scope letter, plus visual stand-ins).
