# Research

## Verified premises

- The canonical MIME reader currently defines an inline image as any image whose disposition is inline **or** whose Content-ID is nonblank.
- That test ignores the stronger explicit `Content-Disposition: attachment` signal, so an attached Content-ID image is omitted from `AttachmentRecords`.
- `LocalEmailDisplayReader` retains the explicit attachment, so every later ordinal can shift.
- The existing occurrence test already compares display and canonical attachment sequences for nameless and attached-text parts.

## Assumptions

- None. The parser condition and both occurrence domains were checked on the shared branch.
