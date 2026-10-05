# v31 discussion log

This log records how the round came about. It is not design authority.

## Round 1 — 5 October 2026

**Operator's brief (verbatim, numbered as given):**

> Plan a triage page redesign/rework:
> 1. Needs correspondence tab under files as regular case page does
> 2. [the Chaser correspondence panel] this whole section makes zero sense. Correspondence should be done through a composer window same as the inbox page.
> 3. Case needs an actions [menu] as per regular case pages. Can move Cancel to here.
> 4. Determinations section can move to a dialogue box too.
> 5. [Assignee · Assign] assign button can also move to this actions dropdown
> 6. Link case can also move to this
> 7. "Open Message" can be removed
>
> Main focus should be vehicle images on triage cases.
>
> Plan 3 seperate mockup designs for a triage case page redesign and submit to me for selection, then proceed to implement the winner.

**What was explored**
- The live page on `origin/dev` 81b571c36, recorded in [how-it-works](../pages/triage-case/how-it-works.md).
- The regular Case page's ribbon Actions menu (`_CaseRibbon.cshtml`).
- The Files tabs and Correspondence tab (`_CaseFiles.cshtml`, `_CaseCorrespondence.cshtml`).
- The Inbox composer overlay (`_ComposeForm.cshtml`, `mail-compose.js`).

**What was built.** Three designs share every numbered request and differ in how the images lead:
- A · Image stage: one large lead image with a filmstrip.
- B · Inspection split: a persistent viewer beside a status aside.
- C · Contact sheet: tabs with a large-thumbnail grid.

**Items raised.** A–F in [v31-notes.md](v31-notes.md#5-lettered-sign-off-items).

## Round 2 — 5 October 2026

**Operator's words (verbatim):**

> They do not need: 1. Crop 2. Tag. These are for real cases. Triage doesnt need these options.
> "Await Information" - cant even see what this is supposed to do/be for. Likely just a removal candidate.
> Change "Determination" to "Record Finding"
> "Record Finding" dialogue window should have: Tickbox for complete triage, Tickbox for reply with finding
> Prefer option C for the design
> Make the changes to option C, finalize it, and implement and submit on a PR

**Changes made**
- Crop, Tag, tag chips and the Cropped badge are removed from every design's images and viewer.
- Await information is removed from the Actions menu.
- "Determinations" becomes **Record finding** (menu item, dialog title and submit). The dialog gains **Complete Triage** and **Reply with finding** tickboxes. Ticking Reply with finding ticks Complete Triage, and unticking Complete Triage clears it. Reply with finding is offered only where a reply can be sent.
- The page-wide term "Reply with outcome" becomes **Reply with finding**.
- C is final and was implemented in the Stage 2 PR from this branch. The self-check gave 1783/0.

**Items closed:** A (C), D (C's placement: response evidence in the Correspondence tab). Items B, C, E and F were not answered and were implemented as proposed; see [v31-notes.md](v31-notes.md#8-decided-5-october-2026).
