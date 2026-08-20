# Plan

Estimated diff: two existing files, under 70 lines.

1. Add a folder-scoped MIME method to the existing Graph client and use it only for selected Deleted candidates.
2. Prove unchanged content uses the exact resolved folder path and a concurrent move fails to the existing unavailable state.
3. Run focused Graph checks and four-lens/PIR updates.

## Governing docs

FRD-08's exact approved mailbox/folder scope remains true at content-read time, with GET-only access and unchanged bounds.

## Simplification pass — 2026-08-20

- Reuse: the existing Graph client, resolved folder identity, failure mapping, and common MIME response reader are reused.
- Simplification: one folder-scoped URI builder call; no recheck service, retry, or new state.
- Efficiency: MIME count/global bound is unchanged and concurrent moves fail on the same GET.
- Altitude: exact external scope stays at the Infrastructure adapter boundary.
