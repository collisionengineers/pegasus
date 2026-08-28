## Prior art from CASE-012's superseded parallel branch — 2026-08-28

`origin/task/case-012-case-workspace-parallel` (head 866fe459, never merged,
**must not be merged** — see CASE-012's scratch for the ruling) contains two
new partials that are lane E2 scope, so CASE-012 round 3 deliberately left
them out of its salvage rather than absorbing this ticket's work:

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml` (+85 lines) —
  vehicle facts plus the lookup and suggestion forms, with the disabled
  "Look up vehicle" / "Check vehicle history" buttons removed.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml` (+110 lines) —
  custody panel, `_CaseDocuments`, and the two image galleries.

Read them with `git show origin/task/case-012-case-workspace-parallel:<path>`;
do not merge or cherry-pick the branch. They were written against a dev that
is now 47 commits behind and against a `Details.cshtml` that was superseded by
PR #599, so treat them as a sketch of the section bodies, not as a patch.

Two notes from that run that bear on this ticket:

- Valuations is accepted as `?section=valuations` but has no content, so the
  section nav row exists without a body. Whatever CASE-027 adds must not leave
  an inert control.
- `.workflow-stepper` is a 5-column grid in `site.css` for four D3 stages;
  wave 5 (site.css owner) should make it `repeat(4, …)`.
