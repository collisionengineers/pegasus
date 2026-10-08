# v35 discussion log

Records how the mockup came to be; not design authority.

## 8 October 2026: the brief

The operator, with a screenshot of the aside on a Review Case (Figures; Next action with "Assign Engineer" and an Assign Engineer button; an amber "Report not ready" panel of blocker cards):

> the right hand rail looks odd / needs better organization
>
> It shows one loose button under "next action" and then a yellow warningstyle menu for what the report needs.
>
> This just needs better organization/display
>
> create 3 mockups of potential ideas first.

Explored: the live `_CaseAside.cshtml`, `CaseNextAction`, the blocker sources in Core, FRD-13's "Blockers are specific", FRD-16's aside rules and issue 899's tall-list ruling (28 September 2026). Built today's aside and three proposals over the captured Case page: A groups the blockers by section, B shows one line per blocker that opens for detail, and C moves the blockers into their own card. Raised items A to I ([notes](v35-notes.md), section 6).

## 8 October 2026: missing images, Case details, original report

The operator:

> What would it look like if we are missing images, claimant or Case details, the original report, that kinda thing?

Added the Not ready state for a standalone Audit with its original report and images missing and 22 report blockers. The live aside names only the first Case requirement, and links "Original report missing" to Case details although it is cleared on Files. Added a strip switch that lists every Case requirement in A–C. Raised items J and K ([notes](v35-notes.md), section 9).

## 8 October 2026: six designs for the worst case

The operator:

> redo the mockups with this in mind. go for 6 mockups now

Rebuilt the mockup around the missing-everything Case, which it now opens on. A, B and C became designs 1, 2 and 3, and every design now lists every Case requirement (J) and sends Original report missing to Files (K). Added 4 (one row per section, naming what it is missing), 5 (the first thing to do in full, the rest one line each) and 6 (as 4, following the page, with marks on the section row). Raised items L, M and N ([notes](v35-notes.md), section 10).

## 8 October 2026: Next action is one thing

The operator:

> It doesn't really make sense if it's "next action" and then there's like 50 things
>
> try some more designs

Built designs 7 to 12, in which Next action holds exactly one step and the rest lives elsewhere: a dialog (7), a closed card (8), the sections themselves with marked fields (9), the Report section (10), a checklist of sections (11), and a bar across the page (12). Raised items O to U ([notes](v35-notes.md), section 11).
