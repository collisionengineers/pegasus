# Proof — INTK-016

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #465), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: post-upload confirmation offers the decision set — automatic image-case registration reported plainly, Cancel navigation, and "Add to an existing case" with an accessible autocomplete combobox (`role=combobox`, keyboard + no-script fallback) over `ISearchCases`; replay-keyed attach (`upload-attach:{receiptId:N}:{caseId:N}`) through the existing leased link path. 13/13 checklist; UploadConfirmationWebTests green in the pre-cut focused run.
- Live: Upload page renders on release 14; the confirmation flow exercises on the operator's next real upload (no production test upload was made).
- Full transcript: DELIV-013 scratch.
