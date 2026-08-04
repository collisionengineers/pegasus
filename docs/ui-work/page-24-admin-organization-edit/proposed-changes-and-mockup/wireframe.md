# Wireframe — Organization edit

## Main state (Work Provider organization with active principals)

```
+--------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS  Pegasus   Dashboard Inbox Upload Queues Cases [Administration]  alex   |
+--------------------------------------------------------------------------------------------+
| Administration / Organizations / Organisation One                                          |
|                                                                                            |
| H1  Organisation One                              (Work Provider)  (Instruction Interm.)   |
|                                                                                            |
| (i) The organization roles were updated.                                                   |
|                                                                                            |
| ORGANIZATION ROLES                                                                         |
| +----------------------------------------------------------+                              |
| | [x] Work Provider            (locked)                    |                              |
| |     ! Work Provider cannot be removed while this          |                              |
| |       organization has an active principal.               |                              |
| | [ ] Instruction Intermediary                              |                              |
| |                                                           |                              |
| |  Reason for change  (required)                            |                              |
| |  +-----------------------------------------------------+  |                              |
| |  |                                                     |  |                              |
| |  |                                                     |  |                              |
| |  +-----------------------------------------------------+  |                              |
| |  Recorded against this change in the administration       |                              |
| |  record.                                    0/500 chars   |                              |
| |                                                           |                              |
| |  [ Update roles ]   <- disabled until the selection changes|                             |
| +----------------------------------------------------------+                              |
|                                                                                            |
| PRINCIPALS                                                                                 |
| +----------------------------------------------------------------------------------------+ |
| | CODE     STATUS      INSPECTION MODE            ALLOCATED CASES   ACTIONS              | |
| |----------------------------------------------------------------------------------------| |
| | QDOS     (Active)    Physical address                        12   Replace              | |
| | QDOSB    (Active)    Image Based Assessment                   4   Replace              | |
| | QDOSA    (Disabled)  Physical address                        31                        | |
| +----------------------------------------------------------------------------------------+ |
+--------------------------------------------------------------------------------------------+
```

## Alternate state (no principals, and the capped/paged variant)

```
| PRINCIPALS                                                                                 |
| +----------------------------------------------------------------------------------------+ |
| |  No principals yet.   Create principal ->                                              | |
| +----------------------------------------------------------------------------------------+ |
|                                                                                            |
|  ...or, when the load is capped:                                                           |
|                                                                                            |
| +----------------------------------------------------------------------------------------+ |
| | CODE     STATUS      INSPECTION MODE            ALLOCATED CASES   ACTIONS              | |
| | ...100 rows...                                                                         | |
| |----------------------------------------------------------------------------------------| |
| | Showing the first 100 principals          < Previous    Page 1    Next >               | |
| +----------------------------------------------------------------------------------------+ |
```

## Legend

| Symbol | Meaning |
|---|---|
| `(Work Provider)` | Role chip in the heading actions slot; replaces the removed "Version 0" |
| `(i)` | Status card, rendered only after an action, directly above the roles card |
| `[x] ... (locked)` | Checked and disabled; only when the organization has an active principal |
| `!` | Inline consequence sentence, bound to the checkbox with `aria-describedby` |
| `(Active)` / `(Disabled)` | Status chip — text plus tone, never colour alone |
| `Replace` | Row link → Replace principal (page 27); absent when the principal is disabled or already replaced |
| `0/500 chars` | Live counter against `OrganizationAdministrationPolicy.MaximumReasonLength` |
| `Create principal ->` | Shown in the empty state for Work Provider organizations only |
| `< Previous / Next >` | Pager; rendered only when more than one page of principals exists |

Notes: no eyebrow, no back link, no lede, no visible caption, no version integer, and no
"projection" copy anywhere. The roles form leads because it is the page's only mutation; the
principals table sits full width beneath it and now carries the same Replace action as page 25.
Deselecting both roles disables the submit and shows "Select at least one organization role."
inline rather than after a round trip.
