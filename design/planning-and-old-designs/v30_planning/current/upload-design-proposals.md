# Five new Upload designs

25 September 2026 · temporary Stage 1 review artifacts.

Open the [visual comparison](pegasus_upload_designs_v30.html). Each design is
self-contained and works offline. The earlier five alternatives remain beside
these files. No application Upload code is changed by this round.

## The five directions

| Design | Main idea | Best use | Tradeoff |
| --- | --- | --- | --- |
| [A — Review desk](pegasus_upload_a_new_v30.html) | One work surface: the Case decision on the left, the complete file roster on the right. | Everyday office uploads. Recommended. | Open a preview to inspect an image closely. |
| [B — Guided review](pegasus_upload_b_new_v30.html) | A centred review sheet with Choose files → Review → Complete and a folding file summary. | Less frequent uploaders; a calm, directed flow. | Opening the complete roster takes one disclosure. |
| [C — Contact sheet](pegasus_upload_c_new_v30.html) | A photographic contact sheet beside a steady Case panel. | Recognising an image set quickly. | Eleven images require more vertical space. |
| [D — Compact ledger](pegasus_upload_d_new_v30.html) | A horizontal Case decision above aligned, compact file rows. | Repeated batch review and file comparison. | Small previews; more horizontal scanning in the decision area. |
| [E — Inspection studio](pegasus_upload_e_new_v30.html) | A large selected image, a file selection rail and the Case decision. | Inspecting individual images before association. | Uses more space; the narrow layout stacks inspection below the decision. |

All five use the same fixture and behaviour model. The differences are genuine
compositions, rather than alternate colour themes. A keeps the complete roster
visible while giving the Case decision a clear home. C is the strongest visual
option for photographic work. D gives a large upload the most compact treatment.

## What improves on the earlier five

The earlier [A](pegasus_upload_a_v30.html), [B](pegasus_upload_b_v30.html),
[C](pegasus_upload_c_v30.html), [D](pegasus_upload_d_v30.html) and
[E](pegasus_upload_e_v30.html) are the comparison baseline. Their content and
interactions were inspected in `lib/upload.mjs`, alongside the existing captures.

| Earlier treatment | New treatment | Practical benefit |
| --- | --- | --- |
| Repeated green Ready badges and long file names carry much of the visual weight. | Quiet, compact ready outcomes; colour reserved for running, failed or confirmed states. | Problems and decisions stand out. |
| Gallery tiles use JPEG placeholders. | Embedded synthetic photographs, selectable file previews and Previous / Next. | Reviewers can judge the actual visual balance and the inspection interaction. |
| Candidate identity shows a reference, registration, claimant and stage. | Principal and Case/PO are included alongside those facts. | The destination can be distinguished before confirmation. |
| Many actions navigate between canned query states; file selection is largely illustrative. | Real local selection, removal, validation and preview; a separate sample upload journey. | The selection interaction can be exercised without falsely claiming local files were processed. |
| Typed lookup demonstrates one hard-coded reference. | Search fixture Cases by reference, PO, registration, claimant or Principal, including Complete and Triage. | Search, no match and search failure can be reviewed separately. |
| File ledgers, cards and secondary registration compete as separate blocks. | Each composition gives the Case decision one home; Image intake is a subordinate record link. | The next decision is clearer. |
| Narrow states mainly resize the desktop arrangement. | Decision first on narrow screens, with every file still accessible; dialogs fit the viewport. | Essential actions remain reachable. |

The improvement claim is about these concrete changes. Preference among the
compositions remains the operator's decision. Browser evidence cannot establish
that a design is flawless or that the application implements it.

## Behaviour shared by all five

1. Choose or drop supported files. Clear or remove individual selections before
   upload. Validation covers unsupported extensions, empty files, 20 files,
   100 MiB per file and 200 MiB in total. Limits use exact binary units.
2. Use **Load sample files** in Mockup controls to run the complete simulated
   journey. All selected files enter Uploading together. Stored/processing
   follows; no invented per-file storage percentages or staggered success ticks.
3. Possible Cases are not preselected, even when only one matches. Select a Case,
   then **Review and add to Case**. The focused confirmation repeats the exact
   reference, PO, registration, claimant, Principal, stage and file count.
4. Cancel or Escape returns to the same choice. Confirm preserves the chosen
   destination, including a second candidate or a typed search result. A conflict
   adds nothing and requires refresh before a new choice.
5. Image intake registration is automatic and secondary. **BH17RZV-01** links to
   the existing Image intake record. There is no manual registration action or
   registration reason form. This preserves the decision of 24 September.
6. An unreadable member is marked on its row and disclosed before confirmation.
   Its original remains part of the upload. Readable members can still support
   the group decision. An all-unreadable upload links to Unidentified and offers
   no invented Case candidate. An incomplete upload offers refresh, not addition.
7. A document fixture includes the existing onward **Review new Case proposal**
   route. It does not allocate a new Case inside Upload. A duplicate fixture
   reports the existing destination and does not offer another association.
8. **Leave undecided** retains the upload and returns to selection. **Discard
   upload** is separated from the primary flow and uses a focused confirmation
   with the existing acknowledgement checkbox. Source and processing records
   remain retained. No reason field is added.
9. Actual selected local files remain local. Uploading them opens an explicit
   offline limitation message. Existing Case, original file, Image intake,
   Unidentified and new Case proposal links open labelled destination previews;
   those application screens are outside this design round.

The sample has eleven long WhatsApp filenames and six generated photographic
views. Reused views are a layout fixture, not eleven independent pieces of
evidence. [Image provenance and exact prompt](assets/upload-designs/README.md).

## Source and coverage audit

Live source was read from `origin/dev` at **32dabfc59**, 25 September 2026.
The builder pins the shell CSS, fonts, marks and Lucide sprite to that commit,
so unrelated worktree implementation edits cannot silently alter these files.
The existing `shared.mjs` shell reconstruction preserves the 220 px rail,
48 px utility bar, approved mark and responsive navigation. Page styles are local.

Owners: [FRD-18](../../../../docs/frd/frd-18-manual-upload.md),
[FRD-12](../../../../docs/frd/frd-12-operator-experience.md),
[design authority](../../../../docs/design/README.md) and
[reserved vocabulary](../../../../CONTEXT.md).

| Live control or fact | Coverage in new proposals |
| --- | --- |
| Multiple selection, drop target, accepted types, limits, Upload, Clear, validation | Working local selection plus a clearly separate sample transfer. |
| File name, size, file outcome, open file and thumbnail | Complete roster in every design; B and E use disclosures; preview dialogs in all five. |
| Received time, refresh while processing | Received timestamp after storage; manual refresh and processing presets. |
| Upload another file | Consistent **New upload** action after processing; selection begins with no empty file panel. |
| Duplicate and already-associated outcome | Existing Case is shown; further association is absent. |
| Read failure, technical/upload failure, unavailable group member | Separate unreadable, upload-error and incomplete states with truthful outcomes. |
| Automatic Image intake identity and awaiting-instruction outcome | One subordinate record link after image processing. |
| Possible Cases and typed lookup | Complete distinguishing identity, unselected candidates, searchable fixture Cases/Triage in any stage. |
| Confirmation with exact Case, complete roster and reviewed versions | Exact identity/count in the modal. Live leases, versions, operation IDs and all-member validation remain implementation requirements, not simulated backend evidence. |
| Failed/stale confirmation | No success shown; refresh and a fresh explicit choice are required. |
| Editable new Case proposal from instruction material | Onward route preview in the document state; no new allocator. |
| Cancel/no association and group discard acknowledgement | Leave undecided; separate discard modal retaining the checkbox and custody consequence. |
| Single-file status page | Single-image and single-document presets reuse the same decision treatment. |
| Navigation, account, notifications and global search | Existing shell retained; out-of-scope destinations are labelled preview dialogs; rail collapse and Ctrl K work. |

The older page-shape prose in FRD-18 still describes a manual registration/reason
form. Its removal was already decided on 24 September in this planning round.
The new designs follow that decision and the current automatic-registration
requirements. Selecting these mockups does not waive the server's complete
roster, lease, stale-version, idempotency or custody obligations.

## Decisions for this round

These are proposals, not implementation approval. The original A–G items remain
in the historical notes; the following list describes the new five designs.
The new discard proposal retains the existing checkbox, unlike earlier item B.

| Item | Decision |
| --- | --- |
| UA | **Confirm A — Review desk, or choose B, C, D or E.** This chooses the desktop composition and its narrow reflow. |
| UB | **Confirm selectable file previews with Previous / Next, or retain direct original-file opening.** C adds the contact sheet; E adds the inspection rail; B folds the full roster. |
| UC | **Confirm explicit Case selection followed by the Review action and exact-target modal, or use a direct Review action on each candidate.** The proposed sequence costs one more explicit action than the earlier candidate button. The backend confirmation remains required either way. |
| UD | **Confirm Leave undecided and New upload, or retain the current Cancel and Upload another file labels.** Behaviour and retention are unchanged. |
| UE | **Confirm moving discard into the focused modal while retaining the acknowledgement checkbox, or keep the existing in-page disclosure.** The retained-source consequence and version checks remain. |

No new approval is requested for automatic Image intake registration, explicit
association, server limits or retained custody: these are existing requirements.
Stage 2 begins only after the operator settles the design and interaction choices.

## Review evidence

The [focused browser runner](check-upload-designs.py) exercises the offline
mockups, captures all 20 states for all five variants at 1580×1000, 1440×900 and
760×1000, and records [its result](v30-upload-new-shots/selfcheck-result.json).
It also exercises keyboard focus, Escape, exact destination, search outcomes,
conflict recovery, discard, selection, file validation, sample transfer and
preview navigation. Extra checks cover 390 px overflow and rail collapse.

The [capture index](v30-upload-new-shots/README.md) lists the numbered states.
These checks are offline design evidence only. They do not run an application
build or establish live upload, processing, extraction, search or persistence.

### Offline result — 25 September 2026

`RESULT {"fail":[],"okCount":2858}`. No console/runtime errors and no external
requests. Captured 300 state/size images plus five complete desktop pages.
Keyboard focus, Escape/focus return, exact second-Case identity, Complete/Triage
lookup, no-match/search failure, mixed outcomes, conflict/refresh, discard
acknowledgement, selection/removal, sample transfer, local-file validation and
preview navigation passed. This result concerns the HTML mockups only.
