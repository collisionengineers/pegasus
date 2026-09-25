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
| E · gallery | [E](v29-shots/e-decision-1580.png) | [E](v29-shots/e-processing-1580.png) | Helps only if real thumbnail content adds recognition; JPEG placeholders cannot prove that. |

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

Layout items remain **open**. Item E is rejected and replaced below.

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

**E. Rejected — 24 September 2026.** Manual Vehicle images registration was
the wrong primary flow. FRD-18 registers usable image identity automatically.
Matching Cases must be offered first, with explicit selection and confirmation.

**F.** Confirm whether real image thumbnails merit the E contact sheet, or
drop E after reviewing the other four. The JPEG tiles are placeholders.

**G.** Confirm applying the selected presentation to the single-file status
page as well, or keep its existing distinct layout.

## Verification and limits

The 24 September 2026 self-check returned `RESULT {"fail":[],"okCount":705}`
after rendering every option and state. `shoot.ps1` generated 195 captures:
five options, thirteen states, and three widths. There is no server, file upload,
Case search, Worker, saved
receipt, or live confirmation behind these mockups. File names, Case/PO and
Image reference are synthetic. E has placeholders rather than real thumbnails.
Shell navigation and account controls are visual context in this offline file;
the upload controls and state presets are the active review surface.
Stage 2 has not started.

## Refinement — 24 September 2026

All five options retain the actual Pegasus shell, typography and colour tokens.
The second pass increases panel spacing, aligns fields and file metadata, uses
consistent image icons and quieter status badges, and makes Upload more files
a secondary action. A keeps the split workspace; B refines the step indicator;
C keeps per-file failures visible; D aligns the decision above a dense ledger;
E uses evenly spaced JPEG tiles instead of numbered boxes.

The duplicate Upload eyebrow and repeated discard explanation are removed.
The retention consequence appears once in the confirmation. Mobile badges no
longer stretch across a row. Uploading does not claim a fabricated stored count.
This remains a visual proposal; the sign-off items above remain open.

## Process correction — 24 September 2026

Operator: “It should propose a case if that is possible but this is shown akin
to more of a secondary option.” This settles the hierarchy across all five
options. The prior form-first interpretation was incorrect.

- A viable proposed Case is visible first, with Case/PO, registration, claimant
  and stage. Review and add leads to explicit confirmation; nothing auto-links.
- Multiple viable Cases appear together, with none selected by default.
- No match opens Case lookup without inventing a proposal.
- Automatic Vehicle images registration is secondary context for these image
  fixtures. The manual registration and Reason form are removed.
- Confirmation reports the selected Case. Discard and Leave undecided remain
  secondary.

The fixtures remain images. Eligible non-image material uses the existing
extracted new-Case proposal screen under FRD-18; this pass does not invent
an image-to-new-Case form. Verification: 705 offline checks passed.
