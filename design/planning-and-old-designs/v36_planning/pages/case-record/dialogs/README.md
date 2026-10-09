# Case record dialogs

Temporary review artifact; see the [page README](../README.md).

The walk opened every dialog the Actions menu offers in each captured state. None is changed by this round; they are listed so the mockup's `dialog=` preset and the self-check cover them.

| Dialog | Opens from | Captured in |
| --- | --- | --- |
| Place on Hold (`case-hold-dialog`) | Actions | With Engineer, Review, Not ready, Inspection + Audit |
| Correct principal (`case-correct-principal-dialog`) | Actions | every open state |
| Close case (`case-close-dialog`) | Actions | every open state |
| Return to Review (`case-return-review-dialog`) | Actions | With Engineer |
| Release Hold (`case-release-hold-dialog`) | Actions | Held |
| Assign Engineer (`case-handoff-dialog`) | Actions and Next action | Review |
| Create audit (`case-create-audit-dialog`) | Actions | Inspection + Audit |

Seen on the walk and recorded in the notes as a behaviour finding: an item that opens a dialog leaves the Actions menu open behind the backdrop.
