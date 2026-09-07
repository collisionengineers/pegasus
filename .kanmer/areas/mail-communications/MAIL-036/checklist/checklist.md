# Checklist — MAIL-036

- [x] Record a wipe cutoff atomically with SQL deletion and require a stopped Worker.
- [x] Preserve cutoff through poll refresh and reject old notified MIME.
- [x] Process only the exact notified message; release lease without a recovery scan/cursor advance.
- [x] Prove SQL missing/existing state, scope refresh and Graph old/new reset paths; update workflow docs.

Focused evidence: 72 initial integration cases, 27 notification Core cases and 3 updated SQL cases passed; no live wipe.
