---
ticket: PLAT-045
executed: 2026-08-27T09:34Z
release: 34 (source 1ec65dc894f121f4bb5b31ae82c818a401d08beb)
proof_type: command-log
---

# Proof — PLAT-045, executed 2026-08-27 after release 34 smoked

Tier: **production**. Script `artifacts/plat-045-wipe/Invoke-Plat045Wipe.ps1`
(ignored path; output retained in `wipe-output.txt` beside it). Entra token
via `az account get-access-token`, one `XACT_ABORT` transaction:
NOCHECK → DELETE → `WITH CHECK CHECK CONSTRAINT ALL` → verify.

## SQL — `pegasus` on `pegasus-prod-sql-252ow37gij`

| | Before | After |
| --- | ---: | ---: |
| Tables total | 97 | 97 |
| Preserve list found | 31/31 | — |
| Preserved effective (31 + `ApprovedMailboxSubscriptions`) | 32 | 32 |
| Tables wiped | 65 | 65 |
| Rows in wiped tables | **623** | **0** |
| Rows in preserved tables | — | 302 |
| `CaseSequences.LastAllocatedSequence` | 23 | **23** |
| `ImageIntakeSequences` / `UnidentifiedSequences` rows | 6 / 1 | 6 / 1 |

Rows affected reported by the batch: 623. `WITH CHECK` re-enable passed —
no preserved table referenced a deleted row. Next case is **QDOS26024**; no
reference is reused. Poll cursors (`ApprovedInboxPollStates`) and Graph
subscriptions preserved, so nothing in the mailbox is re-ingested.

## Blob storage / queues

- `pegcustody252ow37gij/transient-intake`: 0 blobs before, 0 after (the
  release-33 intake path had already retained nothing there).
- `authentication-ring`, `box-links`, all `pegtrans252ow37gij` containers:
  untouched.
- Queues `intake-work`, `intake-work-poison`, `external-work`,
  `external-work-poison`: 0 messages before and after.

## The estate still works

`Invoke-ProductionSmoke.ps1` after the wipe: "Production Worker activation
smoke passed (approved-live-worker). Production smoke passed." against
release 34 (`1ec65dc8`, `0.1.0-alpha.1`).

## Out of scope, as planned

Outlook and Box untouched.

Verdict: **PASS**.
