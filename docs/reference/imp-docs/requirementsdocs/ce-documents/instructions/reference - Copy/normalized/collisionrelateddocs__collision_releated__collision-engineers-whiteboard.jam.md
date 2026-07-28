# Normalized Companion: Collision Engineers Whiteboard.jam

- Raw source: `docs/reference/raw/collisionrelateddocs/collision_releated/Collision Engineers Whiteboard.jam`
- SHA256: `710a56041e42fb4755838bd90944b2f69d1c8ba819bea0fd030f21f1e5c2fa31`
- Extraction method: local `.jam` ZIP image export plus Figma `get_figjam` inspection
- Extraction confidence: reviewed-derivative
- Figma board: https://www.figma.com/board/klGsJGOEG41oP459EbH0ea/Collision-Engineers-Whiteboard?t=1uUOSUp9VtqVuFtN-6
- Figma file key: `klGsJGOEG41oP459EbH0ea`
- Export folder: `docs/reference/data/jam_exports/collisionrelateddocs__collision_releated__collision-engineers-whiteboard.jam/`
- Metadata JSON: `docs/reference/data/jam_exports/collisionrelateddocs__collision_releated__collision-engineers-whiteboard.jam/jam_meta.json`
- Image index: `docs/reference/data/jam_exports/collisionrelateddocs__collision_releated__collision-engineers-whiteboard.jam/image_index.csv`
- Figma inspection summary: `docs/reference/data/jam_exports/collisionrelateddocs__collision_releated__collision-engineers-whiteboard.jam/figma_inspection.md`
- Contact sheets: `docs/reference/data/jam_exports/collisionrelateddocs__collision_releated__collision-engineers-whiteboard.jam/contact_sheets/whiteboard-images-01.jpg` through `whiteboard-images-08.jpg`
- Thumbnail derivative: `docs/reference/data/jam_exports/collisionrelateddocs__collision_releated__collision-engineers-whiteboard.jam/images/thumbnail.png`

This companion is a derivative working copy. The raw source remains the source of truth and must stay unchanged.

## Archive Contents

The `.jam` archive contains:

| Entry | Notes |
| --- | --- |
| `canvas.fig` | Binary FigJam canvas data. Not treated as reliable text source directly. |
| `thumbnail.png` | Exported as the derivative thumbnail and cross-listed in `image_index.csv`. |
| `meta.json` | Exported to `jam_meta.json`; original export timestamp is `2026-05-22T08:29:55.387Z`. |
| `images/` | 44 embedded image assets exported as `frame-001.png` through `frame-044.png`. |

The export bundle reviewed for this companion consists of 56 derivative files total: 44 embedded image frames, 1 thumbnail, 1 metadata JSON, 1 image index CSV, 1 Figma inspection summary, and 8 contact sheets.

## Figma Inspection Additions

The connected Figma board was inspected successfully after the board URL was supplied. Figma inspection status is `completed` in `jam_meta.json`, and the node tree confirms and extends the local image review:

- Admin intake splits email into instruction, vehicle images, or query.
- WhatsApp is also a first-class source for images and queries.
- Outlook labels are applied based on instruction type and work provider.
- If instruction and images are both present, the case goes to Table 2, `Ready to Set Up`.
- If anything is missing, the case goes to Table 1, `Not Ready Yet`.
- For some providers CE waits for the garage to send images and repair estimate; for others CE messages the garage on WhatsApp.
- Missing information is chased for up to 4 weeks, then CE closes the file, informs the work provider, and backs up the information to Box.com in case the file is reopened.
- Job setup follows the `EVA Setup Guide` and per-work-provider SOP exceptions.
- Engineers complete the inspection in the EVA integration of Glass's Repair Estimate or in standalone Audatex.
- Inspection reports are backed up to Box.com and delivered by Outlook and/or WhatsApp depending on work provider rules.
- The website form flow uses Stripe checkout. On successful checkout, CE updates spreadsheet paid status and Box URL, creates a Box folder with `Invoice.pdf`, `Summary.txt`, and attachments, sends confirmation email with invoice, and sends CE a new repair estimate request email with form data and Box link.
- Pending-payment jobs are checked every 5 minutes; if payment is pending, no chaser has been sent, and the submission is at least 15 minutes old, a chaser email is sent and the chaser flag is set.

## Embedded Image Evidence Summary

The exported frames include evidence for:

- website repair-estimate spreadsheet and portal form;
- Stripe/payment confirmation and invoice email;
- Box `Repair Estimate Requests` folders with uploaded images, `Invoice.pdf`, and `Summary.txt`;
- Outlook categories and attachment handling;
- WhatsApp image, valuation, query, and report-delivery flows;
- job sheet `NOT READY YET` and `READY TO SET UP` sections;
- local network folder creation from the spreadsheet;
- provider fee-note/report-routing exceptions;
- SOP guide folder references;
- EVA setup guide screenshots;
- `Closed Files` lifecycle evidence.

See `image_index.csv` for per-image dimensions, hashes, archive entries, categories, and descriptions. The contact sheets provide quick visual QA across all 44 embedded images, while `thumbnail.png` preserves the whole-board layout for orientation.

## Concise Evidence Summary

- Current intake/status evidence is multi-channel: Outlook, website portal/payment status, WhatsApp, Box, and local network folders all appear in the reviewed derivatives.
- The website repair-estimate workflow is current operational evidence, not just a future idea, but portal/payment automation remains outside parser MVP scope.
- Job-sheet operations are explicit in the evidence: `NOT READY YET`, `READY TO SET UP`, `Create Folder`, local network links, `Move Up`, and `Move Down`.
- Provider-routing evidence covers fee-note/report-delivery exceptions and query ownership, including `QDOS`, `QCL`, `AX`, `FW`, `OAK`, and `ALS`.

## Documentation Implications

- Current CCC docs should distinguish the existing repair-estimate website portal from future external portal/API expansion.
- Work item source metadata should support Outlook, WhatsApp, website portal, Box link, payment state, local-network folder link, and source labels.
- Provider/principal config should cover delivery channel, query handling, fee-note routing, report routing, garage-figures rules, and SOP exceptions.
- Operations docs should include the 4-week missing-information closure rule and Box backup on closure.
- Parser MVP should remain deterministic parser-first; payment automation and live portal integration belong in later phases unless separately approved.
