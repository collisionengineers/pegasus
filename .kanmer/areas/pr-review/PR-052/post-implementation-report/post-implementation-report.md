# Post-implementation report

## Outcome

A recoverable IReleaseCaseEditLease failure no longer gets swallowed while recovery authority is discarded. The page surfaces a same-confirmation retry and retains the exact protected message/receipt/action/Case/version/token/key authority. The retry repeats the definitive no-write refusal, releases through the existing port, then clears authority. Caller cancellation and uncertain association outcomes retain their established same-confirmation behavior.

## Files

- src/Pegasus.Web/Pages/Mail/Message.cshtml.cs — retains authority on transient release failure, distinguishes the recovery message and clears only after confirmed/definitive resolution.
- src/Pegasus.Web/Pages/Mail/Message.cshtml — re-renders only the exact recovery confirmation.
- tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs — fail-once real-release decorator and authenticated retry/no-write/immediate-reacquisition proof.

## Verification

- Exact focused authority/recovery/replay tests: 3/3 passed.
- Full MailWorkspaceWebTests: 35/35 passed.
- Locked restore and Release solution build: passed, 0 warnings/errors.
- git diff --check: passed (line-ending notices only).
- Commit: 563bb2ec; PR #490 targets dev.

No background worker, schema, new store/framework or external write.
