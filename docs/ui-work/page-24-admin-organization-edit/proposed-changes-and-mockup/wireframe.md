# Wireframe — Organization

One container: header, action bar, tabs (ui-standards §4 rule 13). The roles form stops being a
permanently open form in the page body — the Roles tab states what is true, and editing happens
in a dialog off the action bar.

## Main state (Work Provider organization with active principals)

```
+--------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS  Pegasus   Dashboard Inbox Upload Queues Cases [Administration]  alex   |
+--------------------------------------------------------------------------------------------+
| Administration / Organizations / Organisation One                                          |
| ###############################  CONTAINER  ############################################   |
| # Organisation One  (Work Provider)  3 principals · 47 allocated cases                 #   |
| #                                                    <Back to Organizations>           #   |
| #----------------------------------------------------------------------------------------# |
| # [ Edit roles ]  [ Create principal ]  [ More v ]                                     #   | <- sticky
| #----------------------------------------------------------------------------------------# |
| # (i) The organization roles were updated.                                             #   |
| #----------------------------------------------------------------------------------------# |
| # | Roles* | Principals (3) |                                                          #   |
| #----------------------------------------------------------------------------------------# |
| #  Work Provider · locked      ~Cannot be removed while this organization has an       #   |
| #                              ~active principal.~                                     #   |
| #  Instruction Intermediary    ~Not held.~                                             #   |
| ##########################################################################################  |
+--------------------------------------------------------------------------------------------+
```

Principals tab:

```
| #  CODE     STATUS      INSPECTION MODE            ALLOCATED CASES   ACTIONS           #   |
| #  QDOS     (Active)    Physical address                        12   Replace           #   |
| #  QDOSB    (Active)    Image Based Assessment                   4   Replace           #   |
| #  QDOSA    (Disabled)  Physical address                        31                     #   |
| #  Showing the first 100 principals              < Previous   Page 1   Next >          #   |
```

Empty variant of the Principals tab: one line — `No principals yet.  Create principal ->`

## Edit roles dialog

```
        +------------------------------------------------------------+
        |  Edit organization roles                             ( x ) |
        |------------------------------------------------------------|
        |  [x] Work Provider   (locked)                              |
        |      ! Work Provider cannot be removed while this          |
        |        organization has an active principal.               |
        |  [ ] Instruction Intermediary                              |
        |                                                            |
        |  Reason for change  (required)                             |
        |  [______________________________________________]          |
        |  ~Recorded against this change in the administration       |
        |  ~record.~                              0/500 characters   |
        |                        ( Cancel )   (( Update roles ))     |
        +------------------------------------------------------------+
```

`Update roles` is disabled until the selection changes. Deselecting both roles keeps it disabled
and shows "Select at least one organization role." inline rather than after a round trip.

## Legend

| Symbol | Meaning |
|---|---|
| `#` border | The record container — one shell around header, action bar and tabs |
| `(Work Provider)` | Role chip in the header; replaces the removed "Version 0" |
| `(i)` | Status card, rendered only after an action, inside the container under the bar |
| `[x] ... (locked)` | Checked and disabled; only when the organization has an active principal |
| `!` | Inline consequence sentence, bound to the checkbox with `aria-describedby` |
| `(Active)` / `(Disabled)` | Status chip — text plus tone, never colour alone |
| `Replace` | Row link → Replace principal (page 27); absent when the principal is disabled or already replaced |
| `0/500 characters` | Live counter against `OrganizationAdministrationPolicy.MaximumReasonLength` |
| `< Previous / Next >` | Pager; rendered only when more than one page of principals exists |

Notes: no eyebrow, no back link in the body, no lede, no visible caption, no version integer, and
no "projection" copy anywhere. The Principals tab carries the same Replace action as page 25.
