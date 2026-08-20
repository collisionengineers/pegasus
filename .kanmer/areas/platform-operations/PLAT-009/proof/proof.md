# Proof — PLAT-009

## Merge

PR #430, merge commit `77f34af29bd9f4553f57a9ce1f42033ce04cfe02` on `dev`/`main`.

## Deployment

Shipped in **release 13** (`2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`,
deployed 2026-08-20 ~01:10–01:20Z). `77f34af2` is a verified ancestor of
`2325ed4a`. See [[DELIV-012]] proof (Appendix — Release 13) for the
deployment readbacks.

## Production evidence

Browser-verified on production per [[DELIV-012]] proof: `/Administration/Mailboxes`
now renders a compact table (both route scopes ticked, Sent folder bound) —
the operator's first verbatim complaint ("giant box") fixed. This matches
the ticket's own verification evidence (screenshots at 1920/1366, 37/37
Browser/AccessibilityTests including a 0-violation axe scan of the route).

## Qualification

None — the ticket's own checklist is fully checked and the production
screenshot confirms the same restructured table/panel layout.
