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

**Items raised.** A–F in [v31-notes.md](v31-notes.md#lettered-sign-off-items).
