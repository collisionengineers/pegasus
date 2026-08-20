# Proof — INTK-014

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #462), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Deployed: migration `20260820055900_ImageCaseCustody` applied to production (efbundle transcript; head readback); grant carried; census 58/58.
- Verification lane at the cut: `create_image_case_custody` enqueued in the registration transaction (durable outbox), `merge_image_case_custody` + `image_custody` CaseHistory on merge; `BoxCaseCustody` folds content into the paired case's `Evidence/Images` and removes the emptied folder fail-closed (non-recursive, refuses non-file children); `LocalCaseCustody` parity for offline; worker dispatch chain confirmed (`PendingWorkDispatchFunction` → `external-work` → `EfQueuedCustodyProcessor`).
- Production Box folders appear on the next image-initiated registration (registration-time feature; pre-existing AU17SEO records predate it by design). No production test upload was made.
- Full transcript: DELIV-013 scratch.
