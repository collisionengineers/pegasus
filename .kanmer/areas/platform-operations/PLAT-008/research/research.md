## Asset search (2026-08-20)

Searched for the four supplied mark asset files (`activity`, `brand`, `calendar`, `casefolder`) referenced by `docs/design/README.md` ("supplied and not yet placed", lines 124-125) and by this ticket's Approach.

Checked (read-only):
- `src/Pegasus.Web/wwwroot/images/marks/` — holds only the ten already-placed marks + `pegasus-lockup.png`. None of the four target files present.
- `git ls-files | grep -i -E "activity|calendar|casefolder|brand"` — no image assets match; only unrelated source files (`AutomationActivity.cs`, `LondonCalendar.cs`) and the existing `docs/design/brand/` (Collision Engineers logo/signatures, not the "brand" *mark*).
- Filesystem-wide `find` across the whole repo (excluding `node_modules`, `.worktrees`, `bin`, `obj`) for `*activity*`, `*calendar*`, `*casefolder*`, `*brand*` — no matching image files anywhere in the tree.
- `git log --all --diff-filter=A --name-only` across all branches/history for files matching those names — no mark image files were ever added and later removed; the only hits are unrelated code/docs paths.
- Ticket's own assets folder on the board worktree: `C:\Users\Alex\Documents\GitHub\pegasus\.worktrees\kanmer\.kanmer\areas\platform-operations\PLAT-008\assets\` — **does not exist** (only `PLAT-008.md` is present in the ticket folder).
- `docs/design/README.md` §"Pegasus marks source-to-runtime mapping" (lines 130-134) confirms the upstream source is an external tool: "Claude Design project `710bb42f`, `assets/icons/`" — i.e. the ten placed marks' bytes were pulled from that external Claude Design project during PLAT-001, and the doc itself says the four remaining marks "belong in the register below once their bytes are in the tree" (line 127-129), i.e. explicitly acknowledging the bytes are not yet in this repository.
- PLAT-001 (archived, superseded-by note on this ticket) outcome section lists as follow-up #5: "Four unplaced marks (`activity`, `brand`, `calendar`, `casefolder`) need surfaces or a decision to retire them" — consistent with the bytes never having been copied into the repo during that ticket.

**Conclusion:** the four marks are recorded as supplied (in `docs/design/README.md` and this ticket) but no asset files exist anywhere in the repository, in its git history, or in the ticket's own assets folder on the board. The external Claude Design project (`710bb42f`) that supplied the original ten marks is not reachable from this agent's toolset (no credentials/connection to that external system). Per the ticket's own Approach ("do not redraw, regenerate, recolour, or substitute them") and the lane instruction not to fabricate marks, this ticket cannot proceed to placement without the operator supplying the actual source files.
