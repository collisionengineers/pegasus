# Proof — INTK-015

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #447 + census fix), production smoke passed 2026-08-20.

- Deployed: migration `20260820034652_ImageIntakeSubmissionGroup` applied to production (efbundle transcript, head readback); worker `PendingWorkDispatchSchedule` now `*/15 * * * * *` (provision preview + bicep at the cut) — the minute-quantised idle gap is gone.
- Verification lane at the cut: single `TryRegisterGroupAsync` per group with operation key `image-intake-register:group:{id:N}`, `SubmissionGroupId` filtered unique index, member receipts flipped in the registration transaction, group-level confirmation row on UploadGroupStatus.
- The one-group→one-record behaviour and prompt dispatch prove end-to-end on the operator's next real multi-image upload (registration-time feature; no production test upload was made). The historical AU17SEO-01..07 fan-out is data predating the fix — consolidation direction is with the operator (staff closure with reason on the surplus records).
- Full transcript: DELIV-013 scratch.
