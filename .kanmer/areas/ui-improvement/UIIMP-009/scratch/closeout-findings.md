## 2026-08-29 — findings handed to this lane by the closeout

Two independent passes surfaced removal candidates that belong here. **Each is a
starting point, not a proof — verify before deleting anything.**

### From the board reconciliation

**CASE-026's `kind` / Vehicle-images section is reachable only by a hand-typed
URL.** No rendered control routes to it. It looks orphaned by decision D1, which
restructured the search and queue surfaces. Confirm against `context.md`'s D1 and
FRD-12 before removing: if D1 genuinely dropped it, this is a removal; if it was
merely never wired, it is a wiring job for the surface's owner and **not** yours.

### From the INTK-047 lane, which found them while porting the upload pages

- `.upload-attach`, `.case-search-list`, `.upload-thumb`, `.upload-outcome-list`
  — legacy-block CSS rules whose only remaining callers are the upload pages,
  with **no new-vocabulary equivalent** and legacy design tokens inside.
  **Promote these into the new vocabulary; do not delete them.** Deleting the
  legacy block wholesale would strip live styling from surfaces that have no
  replacement class to fall back on.
- `.accepted-list` (`site.css:545`) — INTK-047 removed its last caller, so this
  one is a genuine delete.

INTK-047 also reported that `docs/design/README.md`'s component map omits the
first three classes, which is a [[UIIMP-006]] gap rather than yours.

### A general guard worth adding while you are in the catalogue

[[UIIMP-008]] shipped two controls rendering `href=""` because `asp-page` was
given a route template (`/Operations`) instead of a page name
(`/Operations/Index`). The committed snapshot recorded it verbatim at
`docs/design/test-ui/pages/received-details--default.html:78` and **no gate
caught it** — a dead link renders perfectly valid HTML.

An assertion that no committed snapshot contains `href=""` would catch this whole
class for the cost of one line. It belongs with the catalogue tooling
([[UIIMP-005]] / [[UIIMP-010]]) rather than being invented a second time here —
coordinate rather than duplicating it.
