# Planning folder layout

`design/planning-and-old-designs/vNN_planning/` holds one round. Model:
[v26_planning](../../../../design/planning-and-old-designs/v26_planning/README.md).

```text
vNN_planning/
  README.md                 what current/ and pages/ are; temporary marker
  current/
    README.md               what it is, files table, status
    <reference>.html        the operator's reference file, when supplied
    pegasus_case_workspace_vNN.html
    pegasus_shell_vNN.html
    vNN-selfcheck.html, vNN-shell-selfcheck.html
    vNN-notes.md
    discussion-log.md
    vNN-shots/
  pages/
    README.md               tree of every page with its how-it-works links
    <page>/                 child pages nest under the page that reaches them
      README.md
      dialogs/README.md     one subfolder per dialog as planning starts
      states/README.md
      panels/README.md
      how-it-works.md
      how-it-should-work.md
```

## `current/README.md`

What this is (a temporary design review artifact created at operator request,
not application code, not design authority, not implementation evidence, with
the removal condition), how to open it, the mockup strip disclaimer, a files
table, and a Status section naming which decisions await sign-off.

## `vNN-notes.md`

1. What changed from the previous version and why: a table of
   previous problem → change → screenshot numbers.
2. Live rules the mockup mirrors, with the source symbol for each
   (`CanLaunchGlass`, `GlassLabels.StateLabel`, `PendingEstimateSources`).
3. Frame rules as numbers.
4. Decisions taken and their authority (design README, FRD section, operator
   issue number).
5. Deliberate departures from live, with reasons.
6. The lettered sign-off list; settled items keep their letter with the date
   and outcome in italics.
7. Self-check result with date and count.
8. Known limits.
9. Dated sections appended per feedback round or approval, never rewritten
   history.

## `discussion-log.md`

Chronological. For each round: the operator's words quoted, what was explored,
what changed, and which sign-off items it raised or closed. It records how the
mockup came to be and is not design authority.

## `pages/<page>/README.md`

Mockup route, live source, the dialogs / states / panels links, the two
how-it-works links, a Screenshots list and a Notes section. Template:
[assets/page-readme-template.txt](../assets/page-readme-template.txt).

## `how-it-works.md`

"Read from the live source on <date>." Then:

- what the page does not show (the gaps a person would not guess);
- a governing documentation table: document → what it settles for this page;
- a source table by layer: Web, Core, Infrastructure → file → what it owns;
- the behaviours, one subsection each, with the rule and its origin;
- "Things the FRD does not settle", for the next FRD.

## `how-it-should-work.md`

"Decided with the operator on <date>." Then numbered rules D1, D2 …, each one
sentence the FRD can carry. A rule lives in the folder of the page it changes
and is only referenced from other pages. Open questions are lines beginning
"Open:". For a dialog or action: "The action", "The dialog", a "Where this
lands" table (page → entry), "Documentation impact when the FRD is written",
and a closing "Decided <date>" section once the operator settles the open
lines.
