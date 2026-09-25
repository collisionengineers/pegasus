# Proposed Upload behavior

Stage 1 proposals, **not yet decided**. The operator's selection and lettered
sign-off in [v29 notes](../../current/v30-notes.md) will determine the rules
that FRD-18 and the design authority receive during Stage 2.

1. Show one concise submission result or decision and keep every member's
   meaningful file state visible.
2. Use the selected option's layout without changing the actual Pegasus
   shell, custody, route, target confirmation, or version checks.
3. Keep file rows stable while upload and processing status changes. Name
   failures and unreadable files on their rows.
4. Show viable Cases first with Case/PO, registration, claimant and stage.
   Review the exact target before confirmation. Multiple matches remain
   unselected; no match exposes Case lookup.
5. Show a registered Image reference once and link to its Vehicle images
   record. A single-file result uses the same outcome vocabulary.
6. Leave without deciding returns to Upload without changing retained
   material. Discard states its consequence once and requires a deliberate
   danger action.
7. Preserve the exact Case/PO, registration, claimant/stage information and
   confirmation before linking to an existing Case.

Open: choose A, B, C, D, E, or a combination; settle the sign-off list before
any of these are made authoritative. The gallery's thumbnail value cannot be
judged from JPEG placeholders.

## Decided — 24 September 2026

Case proposals own the primary decision when available. Vehicle images
registration is automatic for usable identity and is secondary context.
Manual Register Vehicle images and Reason are removed.

## New five-design round — 25 September 2026

[Compare five new designs](../../current/pegasus_upload_designs_v30.html).
The [new proposal document](../../current/upload-design-proposals.md) owns their
behaviour and open decisions **UA–UE**. A — Review desk is recommended.

All five use an explicit unselected Case choice, complete destination identity,
exact-target confirmation, per-file outcomes, retained unreadable originals,
a separate incomplete/conflict state, and subordinate automatic Image intake
registration. The discarded state keeps the source/processing retention fact.
Actual local files can be selected and previewed but are not uploaded by these
HTML artifacts. The complete journey uses synthetic sample files.

The new placement of controls, preview dialogs, selection sequence and labels
remain proposals. The discard checkbox is retained in this round; the earlier
suggestion to remove it is not part of these five designs. No Stage 2 decision
has been inferred from the request for alternatives.


## Decided — 25 September 2026: E, Inspection studio

Operator: "pegasus_upload_e_new_v30.html i select this option for our
upload/processing view. Implement this exactly, including all functionality."

1. `/Upload` is the picker beside the selected files: drop or choose files
   in several steps into one upload, remove one, Clear, the three limits
   stated and checked before posting; **Upload N files** posts once and every
   row reads Uploading together.
2. `/Upload/Group/{id}` and `/Upload/Status/{id}` render one review: the
   inspector (large file, caption with **Open**, filmstrip, folded file names
   and outcomes) on the left and the one decision on the right.
3. The decision: pending with progress and Refresh; candidates as radio
   cards, none selected; **Review and add to Case** opens the exact-target
   dialog; **Find another Case** searches every viable Case and Triage Case
   (a failed search says so); **Review new Case proposal** for instruction
   material; the automatic Image intake as a record link; **Leave undecided**
   and **Discard upload** (focused dialog, acknowledgement kept).
4. Added to Case names the confirmed destination with **Open <reference>**;
   Upload discarded states the material is retained; unreadable files are
   marked on their rows and in the filmstrip; a wholly unreadable upload
   opens its Unidentified item.
5. A browser without script keeps every path: the native input, the GET
   search, the radio form whose post renders the dialog server-side, the
   anchors behind every preview.

UA–UE settled as E; A–G superseded. FRD-18 owns the behaviour.
