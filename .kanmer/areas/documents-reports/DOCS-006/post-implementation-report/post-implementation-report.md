# Post-implementation report — DOCS-006

Delivered as planned on `task/docs-006-instruction-images-evidence` (PR #499).
Deviations and finds:

- The corpus end-to-end fact surfaced that a real email's processing advances
  the receipt version before acceptance (synthetic fixtures do not) — the
  test helper now accepts the current stored version; no product change.
- `_ImageGallery` was generalized to a `GalleryImage(Href, FileName)` view
  model on gaining its second concrete caller — the image-intake gallery and
  the new instruction-photograph gallery share one partial. (Razor note: the
  projections live in code blocks; expressions with nested quotes inside a
  `model` attribute mis-parse.)
- Selection threshold recorded as measured: letterhead art ≤ 28.7 KB
  (repeats by identical hash), photographs ≥ 60.2 KB; floor 40 KB, one
  constant in `InstructionEvidenceImages`.
- All body verification items green: photos beside the source in custody and
  on the Evidence tab; logos/inline excluded; replay idempotent; suites
  47/47 + Core 870/870; build 0/0.
- Live proof deferred to the next release (QDOS26007-shape email → Evidence
  tab + Box files on the deployed estate).

Self-reviewed; subagents barred by operator directive (deviation noted).
