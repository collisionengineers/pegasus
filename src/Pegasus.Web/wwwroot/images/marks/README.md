# Pegasus marks

Fourteen commissioned raster marks, supplied by the operator with the Claude
Design project. Approved and recorded in
[the design authority](../../../../../docs/design/README.md#the-pegasus-marks).

They are not a second icon system. A Lucide glyph from the checksummed sprite
names an action or a state inside a row; a mark names a whole surface, at a size
a 16px line glyph cannot hold. Every mark is decorative — `aria-hidden`, empty
`alt`, always beside text that already says the same thing.

## Expected files

| File | Used by |
| --- | --- |
| `pegasus-lockup.png` | the rail brand, the forced password-change card |
| `accounts.png` | Administration → Staff accounts |
| `roles.png` | Administration → Staff roles |
| `access.png` | Administration → Access review |
| `organisations.png` | Administration → Organisations |
| `principals.png` | Administration → Principals |
| `configuration.png` | Administration → Workflow configuration |
| `mailboxes.png` | Administration → Approved mailboxes; the Inbox empty state |
| `automation.png` | Administration → Automation |
| `checkmark.png` | the Queues empty states |
| `activity.png` | supplied, not yet placed |
| `brand.png` | supplied, not yet placed |
| `calendar.png` | supplied, not yet placed |
| `casefolder.png` | supplied, not yet placed |

## Why they are not in the repository yet

They could not be retrieved through the Claude Design MCP: `get_file` is capped
at 256 KiB and each source PNG is larger, so every download came back truncated
and would have committed a corrupt image. They need copying in from the source
files directly.

The markup, the stylesheet and the design authority are all in place and expect
these exact filenames, so dropping the files here is the only remaining step.
Until then each `<img>` renders as its empty `alt` — nothing breaks, because the
marks are decorative and the text beside them carries the meaning.
