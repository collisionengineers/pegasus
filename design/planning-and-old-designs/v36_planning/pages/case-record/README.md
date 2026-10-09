# Case record

Temporary review artifact; see the [round README](../../README.md).

- **Mockup route:** `current/pegasus_case_walk_v36.html` with `page=case` (the default); `state=` picks the captured Case, `mode=read|edit`, `layout=scroll|tabs`, `section=` lands on a section.
- **Live source:** `src/Pegasus.Web/Pages/Cases/Details.cshtml` and `Pages/Cases/Shared/_Case*.cshtml`; styles `wwwroot/css/site.css` and `case-workspace.css`; behaviour `wwwroot/js/case-workspace.js` and `site.js`.
- [Dialogs](dialogs/README.md) · [States](states/README.md) · [Panels](panels/README.md)
- [How it works today](how-it-works.md) · [How it should work](how-it-should-work.md)

## Screenshots

Listed in [`current/verification.json`](../../current/verification.json) and cited by number in the [notes](../../current/v36-notes.md). Each state is shot today and as the proposal, read and (where the frame exists) edit, at 1580, 1440 and 760; each section of the With Engineer Case in both modes; the operator's variants; the widgets; the dialogs; the viewer.

## Notes

- The page is drawn from frames the running synthetic host rendered on 9 October 2026 with its scripts run, so no-script fallbacks are absent (the v35 frame showed them).
- The fixtures are synthetic: the Case QDOS31001 of the visual host, two Cases created for the round (QDOS31003 Inspection + Audit with its Audit; QDOS31004, an Inspection flipped to a standalone Audit by SQL), Review, Held and a colleague's lease set by SQL on the host's database.
