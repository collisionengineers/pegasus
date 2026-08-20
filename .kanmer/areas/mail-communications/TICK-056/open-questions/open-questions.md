# Open questions — TICK-056 / UI-10

No unresolved operator or product question remains for research. FRD-08 and the current programme settle the reduced scope:

- [[TICK-053]] and [[TICK-057]] are the only direct structural blockers. UI-10 consumes their merged query/filter/detail-return shape.
- [[TICK-064]] remains an earlier programme/coordination predecessor and a prerequisite for folder recommendation/move, but not a direct read-workspace/preview dependency.
- Separately owned recommendation, association, move, Outlook-state and advisory controls appear only after their Core capabilities land. Their absence is honest progressive availability, not a reason for placeholders or a UI-10 blocker.
- [[TICK-088]] is Later/0.5.0 and does not block Next/0.3.0 assembly.
- The quick preview uses the existing external CSP-safe `site.js` and existing retained-mail detail use case unless the merged prerequisites already provide a smaller complete preview projection. Choosing between those two existing seams after rebase is an implementation decision, not missing product intent.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — The 2026-08-19 operator answer remains binding: after deployment, run the authenticated production browser journey through the default and refined workspace, preview, detail and available controls. Read-only behavior may be exercised directly. Execute no mutation unless the owning MAIL capability separately records exact-target approval; UI-10 grants no additional write authority.
- [x] **Must every EPIC-006 action land before UI-10?** — No. MAIL-11 and UI-14 must land because they define the assembled navigation/read shape. Other controls remain independently deliverable and progressively visible from exact message detail; MAIL-12 is explicitly later.
- [x] **Does quick preview require a new CSP decision or client framework?** — No. Current `origin/dev` loads the same-origin external `wwwroot/js/site.js`; extend that progressive-enhancement convention and keep the detail link as the no-script fallback.
