# Automation — wireframe

Proposed layout at 1280px+. One panel: the registration's facts, the one control that
changes them, and one link out. Both states below are **enabled** states — when the feature
gate is off, neither this page nor its Administration card exists (standards §4.9), so
there is no third wireframe to draw.

## Main state — automation is on

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues Cases Administration* |
|                                                    alex · Change password · Sign out|
+------------------------------------------------------------------------------------+
|  Administration / Automation                                                        |
|                                                                                     |
|  Automation                                                                         |
|                                                                                     |
|  +-------------------------------------------------------------------+             |
|  | Name                 Pegasus Automation                            |             |
|  |-------------------------------------------------------------------|             |
|  | Status               [On]                                          |             |
|  |-------------------------------------------------------------------|             |
|  | Can use              Cases                                         |             |
|  |                      ~ Find and open cases, and record case edits  |             |
|  |                      Evidence                                      |             |
|  |                      ~ Add, download and export case documents     |             |
|  |                      Inbox                                         |             |
|  |                      ~ Read received items and submit uploads      |             |
|  |-------------------------------------------------------------------|             |
|  | Client identifier    pegasus-automation                            |             |
|  |===================================================================|             |
|  | Reason                                                             |             |
|  | [                                                    ]             |             |
|  | ~ Recorded permanently with the change.                            |             |
|  |-------------------------------------------------------------------|             |
|  | Turning automation off stops it within seconds. Your name and      |             |
|  | reason are recorded permanently.                                   |             |
|  | [ Turn off automation ]                                            |             |
|  +-------------------------------------------------------------------+             |
|  |  View automation activity  ->                                      |             |
|  +-------------------------------------------------------------------+             |
+------------------------------------------------------------------------------------+
```

## Alternate state — automation is off

Same panel; the status chip, the consequence sentence and the button are the only
differences. The permitted areas still show — they are what automation *would* be able to
use — and the link to activity still shows, because the record outlives the switch.

```
|  +-------------------------------------------------------------------+             |
|  | Name                 Pegasus Automation                            |             |
|  | Status               [Off]                                         |             |
|  | Can use              Cases · Evidence · Inbox (as above)          |             |
|  | Client identifier    pegasus-automation                            |             |
|  |===================================================================|             |
|  | Reason                                                             |             |
|  | [                                                    ]             |             |
|  | ~ Recorded permanently with the change.                            |             |
|  |-------------------------------------------------------------------|             |
|  | Turning automation on lets it act on cases and documents within    |             |
|  | seconds. Your name and reason are recorded permanently.            |             |
|  | [ Turn on automation ]                                             |             |
|  +-------------------------------------------------------------------+             |
|  |  View automation activity  ->                                      |             |
|  +-------------------------------------------------------------------+             |
```

## Confirmation after a change

```
|  Administration / Automation                                                        |
|  +-------------------------------------------------------------------+             |
|  | (i) Automation is off.                                             |             |
|  +-------------------------------------------------------------------+             |
|  Automation                                                                         |
|  (panel follows, Status [Off])                                                      |
```

## Legend

- `*` — active nav item (red underline).
- `Administration / Automation` — breadcrumb; replaces the eyebrow and the "Back to
  Administration" link.
- `[On]` — green-bordered status chip; `[Off]` — muted chip. Label always present, never
  colour-only.
- `~` — muted hint or one-line job description under the area it belongs to.
- `===` — hairline separating the read-only facts from the one control that changes them.
- `[ Turn off automation ]` — primary (red) action. The consequence sentence sits **above**
  it, not below, and changes with the current status.
- `->` — link row at the foot of the panel; replaces the whole second "Activity" panel.
- `(i)` — status card after a successful change; states the resulting fact.
- Scope values are labelled areas with descriptions — the raw comma join of machine scope
  strings never renders.
