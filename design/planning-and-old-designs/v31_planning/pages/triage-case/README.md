# Triage Case

`/Cases/{id}` when the Case is a Triage Case. `Pages/Cases/Details.cshtml` renders `Cases/Shared/_TriageCase.cshtml`. FRD-15 "The Triage Case page" owns the page and FRD-03 owns the chaser.

- [How it works today](how-it-works.md), read from `origin/dev` 81b571c36.
- [How it should work](how-it-should-work.md), pending sign-off.

## Shots (`../../current/v31-triage-shots/`)

| Shots | What |
| --- | --- |
| 01–27 | Each of the three designs in Open, Finding recorded, Completed, Cancelled, Upload, Blocked, Response, No images and Twenty images, at 1580, 1440 and 760 |
| 28–30 | Actions menu |
| 31–33 | Composer (Send chaser) |
| 34–36 | Determinations dialog |
| 37–39 | Message dialog from the Correspondence tab |
| 40–42 | Image viewer |
| full-a/b/c | Whole page, Finding recorded |

## Dialogs

The proposals include these dialogs:
- Assign
- Determinations (new: the old inline form)
- Record correction
- Link case
- Unlink case
- Cancel Triage
- Reopen
- the composer (new: replaces the inline chaser form)
- the message dialog (new on this page)
- the image viewer
