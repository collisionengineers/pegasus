# EVA handoff

- **Mockup route:** `cr-view="evasend"` inside `pegasus_case_record_v28.html`
  (strip control "View") in [`../../../../current/`](../../../../current/README.md);
  also the `eva-handoff-dialog` inside the default `cr-view="record"` view.
- **Live source:** `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml` (+ `.cshtml.cs`),
  `Shared/_EvaHandoff.cshtml`
- Parent: [**Case record**](../README.md)
- [**How it works**](how-it-works.md)

The Case record's Actions-menu "Send to EVA" control is both a real link to
the standalone `/Cases/{id}/Eva/Send` page (the no-script destination) and
the trigger that enhances it into `eva-handoff-dialog` — the same
`_EvaHandoff.cshtml` content either way. It is a child of the Case record
rather than a section of it, so it gets its own subfolder.

## Screenshots

- [s22-case-record-evasend-1580.png](../../../../current/v28-shots/s22-case-record-evasend-1580.png) · [1440](../../../../current/v28-shots/s22-case-record-evasend-1440.png) · [760](../../../../current/v28-shots/s22-case-record-evasend-760.png)

## Notes

- The standalone page adds a page header ("Case workspace · {reference}" /
  "EVA handoff", with a Back-to-Case link) and a Last attempt/outcome or
  Sent-to-EVA summary the dialog does not show; both share the Sign-off
  Engineer selector and the Export ZIP / Send via API buttons.
- `CanRetryAutomaticFailure` and the automatic-failure notice are fixture
  facts only in this capture (the dialog/page always show a recorded prior
  submission); the live conditional notice text is present in source but
  not toggled by a separate strip control.
