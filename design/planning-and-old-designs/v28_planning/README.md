# v28 planning

A temporary design review artifact under the [documentation index](../../../docs/index.md)
carve-out, created at operator request on 18 September 2026. Not application
code, not design authority, not implementation evidence. Remove or retain it
by operator instruction in the final Stage 2 PR, if a Stage 2 round is ever
run from this capture.

**Purpose.** A faithful baseline of the whole application shell as it runs on
`origin/dev` (commit `904903fd1`) on 18 September 2026, and, from the same
day, the operator's proposals drawn over it as a switchable layer. The
baseline is never edited; the proposals are compared against it.

**Rebuilt on 18 September 2026.** The first build of this round was
transcribed by hand from the Razor source and was rejected on review: it
invented vocabulary, showed edit-only controls in read mode, dropped page
wrappers and was stale against Administration. The rebuild transcribes
nothing. Every state is the running application's own server-rendered HTML,
saved with the live CSS and JS, and checked element for element against the
running application. See [`current/v28-notes.md`](current/v28-notes.md).

- [`pegasus_case_dashboard_2026-09-15.html`](pegasus_case_dashboard_2026-09-15.html):
  the operator's reference file. The Case record proposals take their ideas
  from it; none of its markup or styling is used. This copy (3,201 lines) is a
  later build than the one v27's inventory read (2,705 lines); the fifth pass
  of 20 September inventoried the difference.
- [`current/working-log.md`](current/working-log.md): the running record of
  changes and the choices made.
- [`current/`](current/README.md): the nine family files, the captured state
  pages, their assets, the self-check, screenshots and notes.
- [`pages/`](pages/README.md): one folder per page, each listing its captured
  states and what could not be captured, with `how-it-works.md` read from the
  live source.

The previous round is [`../v27_planning/`](../v27_planning/README.md), a
proposal round for the Case record scoped narrower than this one.
