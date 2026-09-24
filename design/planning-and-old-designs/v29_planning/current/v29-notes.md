# v29 Upload options and sign-off

Temporary Stage 1 review artifact for [issue #830](https://github.com/collisionengineers/pegasus/issues/830),
24 September 2026. These offline captures show proposed presentation with
synthetic files. They are not application, deployment, or acceptance evidence.

## Five alternatives

| Option | Decision view | Processing view | Tradeoff |
| --- | --- | --- | --- |
| A · split | [A](v29-shots/a-decision-1580.png) | [A](v29-shots/a-processing-1580.png) | Files and action both visible on desktop; decision goes first at 760 px. |
| B · guided | [B](v29-shots/b-decision-1580.png) | [B](v29-shots/b-processing-1580.png) | Clear sequence; uses more vertical space. |
| C · destination first | [C](v29-shots/c-decision-1580.png) | [C](v29-shots/c-processing-1580.png) | Fastest access to action; compact file index needs careful failure visibility. |
| D · operations table | [D](v29-shots/d-decision-1580.png) | [D](v29-shots/d-processing-1580.png) | Best comparison for mixed results; wide action area can feel sparse. |
| E · gallery | [E](v29-shots/e-decision-1580.png) | [E](v29-shots/e-processing-1580.png) | Helps only if real thumbnail content adds recognition; numbered placeholders cannot prove that. |

The [page README](../pages/upload/README.md) links all three widths. The
query strip exposes the remaining states without changing business data.

## Source and frame rules

The shell is taken from the v28 server capture of `/Upload/Group/{id}`. Current
`origin/dev` `site.css` and Inter font files are inlined into each HTML file.
The rail is 220 px on desktop, the utility bar 48 px, body type 13.5 px,
controls 36 px, dense file rows about 40 px, and content is capped at 1580 px.
At 760 px the real shell reflows and the active decision precedes the file
list. The footer strip is mockup control only.

FRD-18 owns the 100 MiB per-file, 20-file and 200 MiB submission limits, the
single submission decision, member outcomes and explicit destination review.
The options do not implement a second custody or policy path. A live single
file still has its own status route; `?state=single` shows the proposed visual
result only.

## Deliberate departures from live

- The legalistic discard checkbox and repeated prose are replaced in the
  proposals by one sentence and a danger action. This changes FRD-18's
  page-shape clause and needs operator sign-off.
- The open decision is put before the file list at 760 px; option C also puts
  it first on desktop. Current P11 says files first and wide, so placement
  needs operator sign-off.
- “vehicle-image case” becomes Vehicle images and Image reference in the
  proposals, per `CONTEXT.md`. The current code and FRD still use the older
  words in places.
- Thumbnail boxes during processing are removed. E explores a contact sheet
  after processing but has no actual images in this artifact.

## Sign-off list

Every item is **open**. Confirm one option, or describe a combination.

**A.** Confirm A, B, C, D or E as the Upload layout, or identify elements to
combine. A is the current recommendation because the compact file ledger
and decision stay visible together on desktop.

**B.** Confirm removal of the discard checkbox and the repeated consequence
copy, or specify the exact confirmation required. FRD-18 currently mandates
the checkbox.

**C.** Confirm the short operator labels “Vehicle images”, “Image reference”,
“Leave undecided” and “Upload more files”, or give replacement words. These
change current visible copy.

**D.** Confirm placing the decision above the file list at 760 px, and in C
on desktop, or retain the current files-first P11 placement.

**E.** Confirm that Register Vehicle images retains both Vehicle registration
and a required Reason, or describe the accepted image registration action.

**F.** Confirm whether real image thumbnails merit the E contact sheet, or
drop E after reviewing the other four. The numbered boxes are placeholders.

**G.** Confirm applying the selected presentation to the single-file status
page as well, or keep its existing distinct layout.

## Verification and limits

The 24 September 2026 self-check returned `RESULT {"fail":[],"okCount":440}`
after rendering every option and state. `shoot.ps1` generated 150 captures:
five options, ten states, and three widths. There is no server, file upload,
Case search, Worker, saved
receipt, or live confirmation behind these mockups. File names, Case/PO and
Image reference are synthetic. E has placeholders rather than real thumbnails.
Shell navigation and account controls are visual context in this offline file;
the upload controls and state presets are the active review surface.
Stage 2 has not started.
