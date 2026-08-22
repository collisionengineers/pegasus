## Migration dry run against production — 2026-08-22

Ran the migration's exact predicate as a `SELECT` (read-only, permitted) before shipping it, to see what it will touch:

| Effect | Rows |
| --- | ---: |
| QDOS26011 photographs corrected `Instruction` → `Image` | 8 |
| QDOS26010 photographs corrected | 6 |
| Embedded photographs left alone (already `Image`) | 9 |
| PDF attachments left as `Instruction` | 3 |

QDOS26009 contributes nothing, which is right: its custody failed (DOCS-008) before any document row was written, so it has no image occurrence to correct.

The `:attachment:` / `:embedded:` operation-key split does the work it was chosen for — nine already-correct embedded photographs sit outside the predicate in both directions.
