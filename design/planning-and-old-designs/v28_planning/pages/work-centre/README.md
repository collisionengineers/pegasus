# Work Centre

- **Live source:** `src/Pegasus.Web/Pages/Index.cshtml`, `src/Pegasus.Web/Pages/_WorkCentreBody.cshtml`, `src/Pegasus.Web/Pages/Shared/_Layout.cshtml`, `src/Pegasus.Web/Pages/Shared/_ShellDialogs.cshtml`
- [**How it works**](how-it-works.md)

The shell (rail, utility bar, open-records strip, account and notifications dialogs) is part of every captured page; its own presets are listed here because the Work Centre is where it is first met.

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Office-wide work | `/` | [frame](../../current/pegasus_work_centre_v28.html#work-centre) · [page](../../current/states/work-centre.html) | [1580](../../current/v28-shots/s01-work-centre-1580.png) · [1440](../../current/v28-shots/s01-work-centre-1440.png) · [760](../../current/v28-shots/s01-work-centre-760.png) |
| Mine | `/?scope=mine` | [frame](../../current/pegasus_work_centre_v28.html#work-centre-mine) · [page](../../current/states/work-centre-mine.html) | [1580](../../current/v28-shots/s02-work-centre-mine-1580.png) · [1440](../../current/v28-shots/s02-work-centre-mine-1440.png) · [760](../../current/v28-shots/s02-work-centre-mine-760.png) |
| Triage item selected | `/?scope=office&selected=78da3cb3-01fb-4d8c-801c-85f93855b44f` | [frame](../../current/pegasus_work_centre_v28.html#work-centre-selected-triage) · [page](../../current/states/work-centre-selected-triage.html) | [1580](../../current/v28-shots/s03-work-centre-selected-triage-1580.png) · [1440](../../current/v28-shots/s03-work-centre-selected-triage-1440.png) · [760](../../current/v28-shots/s03-work-centre-selected-triage-760.png) |
| Unidentified item selected | `/?scope=office&selected=fe35ab69-545c-4047-8541-c92e645274e7` | [frame](../../current/pegasus_work_centre_v28.html#work-centre-selected-unidentified) · [page](../../current/states/work-centre-selected-unidentified.html) | [1580](../../current/v28-shots/s04-work-centre-selected-unidentified-1580.png) · [1440](../../current/v28-shots/s04-work-centre-selected-unidentified-1440.png) · [760](../../current/v28-shots/s04-work-centre-selected-unidentified-760.png) |
| Filtered to Unidentified | `/?scope=office&kind=unidentified` | [frame](../../current/pegasus_work_centre_v28.html#work-centre-kind-unidentified) · [page](../../current/states/work-centre-kind-unidentified.html) | [1580](../../current/v28-shots/s05-work-centre-kind-unidentified-1580.png) · [1440](../../current/v28-shots/s05-work-centre-kind-unidentified-1440.png) · [760](../../current/v28-shots/s05-work-centre-kind-unidentified-760.png) |
| Filtered to Review (nothing) | `/?scope=office&kind=review` | [frame](../../current/pegasus_work_centre_v28.html#work-centre-kind-review) · [page](../../current/states/work-centre-kind-review.html) | [1580](../../current/v28-shots/s06-work-centre-kind-review-1580.png) · [1440](../../current/v28-shots/s06-work-centre-kind-review-1440.png) · [760](../../current/v28-shots/s06-work-centre-kind-review-760.png) |
| Notifications route (/Notifications redirects here) | `/?notifications=1` | [frame](../../current/pegasus_work_centre_v28.html#work-centre-notifications) · [page](../../current/states/work-centre-notifications.html) | [1580](../../current/v28-shots/s07-work-centre-notifications-1580.png) · [1440](../../current/v28-shots/s07-work-centre-notifications-1440.png) · [760](../../current/v28-shots/s07-work-centre-notifications-760.png) |
| Account dialog (on "Office-wide work") | `/` | [frame](../../current/pegasus_work_centre_v28.html#work-centre?dialog=account-dialog) | [1580](../../current/v28-shots/s74-shell-account-dialog-1580.png) · [1440](../../current/v28-shots/s74-shell-account-dialog-1440.png) · [760](../../current/v28-shots/s74-shell-account-dialog-760.png) |
| Notifications dialog (on "Office-wide work") | `/` | [frame](../../current/pegasus_work_centre_v28.html#work-centre?dialog=notifications-dialog) | [1580](../../current/v28-shots/s75-shell-notifications-dialog-1580.png) · [1440](../../current/v28-shots/s75-shell-notifications-dialog-1440.png) · [760](../../current/v28-shots/s75-shell-notifications-dialog-760.png) |
| Rail collapsed (on "Office-wide work") | `/` | [frame](../../current/pegasus_work_centre_v28.html#work-centre?click=%5Bdata-rail-toggle%5D) | [1580](../../current/v28-shots/s76-shell-rail-collapsed-1580.png) · [1440](../../current/v28-shots/s76-shell-rail-collapsed-1440.png) · [760](../../current/v28-shots/s76-shell-rail-collapsed-760.png) |

## Not captured

- Partial, Stale and Unavailable freshness. The local fixture cannot force a failed or partial read.
- A selected Case item with the Assign Engineer dialog, a populated AI jobs table and a populated New cases list. The fixture holds one Case, already assigned.
- A populated notifications list. The fixture user has no notifications.
- The command palette. It is opened from the keyboard and lists live search results.
