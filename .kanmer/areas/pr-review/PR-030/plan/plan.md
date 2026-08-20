# Plan

Estimated diff: two existing files, under 70 lines.

1. Add a folder-scoped MIME method to the existing Graph client and use it only for selected Deleted candidates.
2. Prove unchanged content uses the exact resolved folder path and a concurrent move fails to the existing unavailable state.
3. Run focused Graph checks and four-lens/PIR updates.

## Governing docs

FRD-08's exact approved mailbox/folder scope remains true at content-read time, with GET-only access and unchanged bounds.
