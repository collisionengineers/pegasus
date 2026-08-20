# Checklist — MAIL-14 verification (backfill)

- [x] Capability text compared clause-by-clause against `PollSentEvidence.cs` (detection, retained identities, exactness, fail-unconfirmed)
- [x] File presence confirmed on origin/main (2325ed4a ancestor path)
- [x] Worker caller confirmed enabled in production (`SentEvidencePollFunction`)
- [x] Live poll state read back (LastCompletedAtUtc 2026-08-20 05:39:15Z, no failure code)
- [x] Mailbox approval state read back (`AllowSentEvidence = True`, Approved)
- [x] Residuals recorded (no live detection yet; MAIL-003 on dev only; approval not in runbook)
