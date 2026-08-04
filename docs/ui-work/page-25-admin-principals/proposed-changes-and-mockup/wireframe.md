# Wireframe — Principals

## Main state (two organizations, one with a replacement chain)

```
+--------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS  Pegasus   Dashboard Inbox Upload Queues Cases [Administration]  alex   |
+--------------------------------------------------------------------------------------------+
| Administration / Principals                                                                |
|                                                                                            |
| H1  Principals                                              [ + Create principal ]         |
|                                                                                            |
| (i) status card - post-action confirmation, only when present                              |
|                                                                                            |
| ORGANISATION ONE                                                    Create principal ->    |
| +----------------------------------------------------------------------------------------+ |
| | CODE    STATUS      INSPECTION MODE          ALLOCATED CASES  REPLACEMENT     ACTIONS  | |
| |----------------------------------------------------------------------------------------| |
| | QDOS    (Active)    Physical address                     12                    Replace | |
| | QDOSB   (Active)    Image Based Assessment                4   Replaces QDOSA   Replace | |
| | QDOSA   (Disabled)  Physical address                     31   Replaced by              | |
| |                                                               QDOSB                    | |
| +----------------------------------------------------------------------------------------+ |
|  Replacing a principal disables it and creates a linked successor. The code, its cases     |
|  and its references never change.                                                          |
|                                                                                            |
| ORGANISATION TWO                                                                           |
| +----------------------------------------------------------------------------------------+ |
| |  No principals yet.                                                                    | |
| +----------------------------------------------------------------------------------------+ |
|   (no create link - Organisation Two is not a Work Provider)                               |
|                                                                                            |
|  (organization pager only when >1 page:  < Previous   Page 2   Next > )                    |
+--------------------------------------------------------------------------------------------+
```

## Alternate state (capped principal load, per-organization pager)

```
| ORGANISATION ONE                                                    Create principal ->    |
| +----------------------------------------------------------------------------------------+ |
| | CODE    STATUS      INSPECTION MODE          ALLOCATED CASES  REPLACEMENT     ACTIONS  | |
| | ...100 rows...                                                                         | |
| |----------------------------------------------------------------------------------------| |
| | Showing the first 100 principals              < Previous    Page 1    Next >           | |
| +----------------------------------------------------------------------------------------+ |
```

## Empty page state

```
| H1  Principals                                              [ + Create principal ]         |
| +----------------------------------------------------------------------------------------+ |
| |  No organizations yet.  Create one in Organizations ->                                 | |
| +----------------------------------------------------------------------------------------+ |
```

## Legend

| Symbol | Meaning |
|---|---|
| `ORGANISATION ONE` | Uppercase section label, matching pages 23 and 24; replaces the full-weight H2 |
| `Create principal ->` | Per-organization link in the section header row; Work Provider organizations only |
| `[ + Create principal ]` | Page primary red action; upload icon removed |
| `(Active)` / `(Disabled)` | Status chip — text plus tone, never colour alone |
| `REPLACEMENT` | Single column replacing PREDECESSOR and SUCCESSOR; shows a **code**, never an identifier; empty when no relationship exists |
| `Replaces QDOSA` | This principal is the successor; the code links to that row |
| `Replaced by QDOSB` | This principal was replaced; the code links to that row |
| `Replace` | Action; active principals with no successor only. Empty cell otherwise — no "No replacement action" text |
| Sentence under table | The only surviving fragment of the deleted lede, placed at the point of decision |

Removed from the current screen: the SEQUENCE LINEAGE column and its GUID, the PREDECESSOR and
SUCCESSOR columns, the "None" cells, the page lede, the "No replacement action" filler, the
visible caption, and the link-less "Page 1" pager. Kept unchanged: the inspection-mode values
"Physical address" and "Image Based Assessment".
