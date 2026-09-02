# Open questions — ENG-030

None. The governing documents already settle every decision this removal
needs: EPIC-011 `context.md` D7 (narrowed 2026-09-01) and D21 name the exact
two controls, the file this ticket owns, and what stays (Experian/Cazana
seams, manual valuation records, Glass's/Audatex file import). The pack's
`decisions/2026-09-01-work-pack.md` and `decisions-and-constraints.md`
confirm the same. No file outside `Index.cshtml`/`Index.cshtml.cs` needs to
change for this ticket's own scope (see `files` — the capability/boundary
docs update is already covered by open PR #643, DELIV-040), and no test
asserts on the two controls being removed.

## Parked (explicitly deferred)

- [ ] Whether a future direct Glass's or Audatex service integration is
      dropped permanently or returns as a separately authorized capability
      — explicitly recorded as **not decided by this pack**
      (`decisions-and-constraints.md`, "Explicitly not decided by this
      pack"). Safe to defer: it does not affect this ticket's removal, only
      a hypothetical future re-introduction ticket. Reopens only if a future
      ticket proposes restoring a direct-service launch control.
