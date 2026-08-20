## Independent re-review — 2026-08-20

**Needs changes; blocker retained.** The nullable ordinal exists, but exact cross-reader identity is not proved: retained display attachments omit nameless parts while the canonical reader may infer a name and advance its descriptor ordinal, shifting later occurrences. Retained message detail also never renders `IsSearchable`, so FRD-08's per-attachment disclosure and this ticket's PIR claim are unmet. Keep PR-018 blocking TICK-053 and re-review after exact mapping, rendered disclosure, and focused persistence/Web evidence land.
