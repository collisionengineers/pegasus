2026-08-27 — Test UI snapshot regeneration on task/mail-018-mailbox-subscription-health (`pwsh ./scripts/Update-TestUiSnapshots.ps1`, exit 0; capture 265 passed / 11 skipped; update 1 passed). Controller decision: commit only `docs/design/test-ui/pages/administration-mailboxes--default.html` with the brand-mark image line kept at the committed relative path; the 49 other regenerated files were discarded with `git checkout --` (list below). `-Verify` was not run. Follow-up ticket to be filed by the controller.

Three unrelated causes characterised (none from MAIL-018's diff):

1. Every page (2-line diff each): the brand mark `<img class="mark" src="../../../../src/Pegasus.Web/wwwroot/images/marks/pegasus-lockup.png">` is regenerated as an inlined `data:image/png;base64,...` URI. The generator on dev (`tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs:280`, commit 44d16f46 "Embed captured evidence images in Test UI") inlines captured assets, so dev's committed snapshots are stale against dev's own generator and would fail `-Verify` today regardless of this ticket. `administration--default.html` additionally inlines its 8 admin-card icons.
2. `case-create--default.html` (54 lines): a different capture candidate was selected — committed snapshot has the hand-keyed form pre-filled ("Hand Keyed Claimant", "HK-2031-001", checked checkboxes); regenerated one is empty ("Not recorded", validation summary valid). Capture-selection nondeterminism.
3. `upload-group-status--processing.html` (16 lines) and others: `OperationId` hidden-field nonce differs per capture.

Also: git reports `LF will be replaced by CRLF` for all generated files on this Windows checkout (generator writes LF; committed files are LF). The committed Mailboxes file was verified LF-only before commit.

Discarded (49):
docs/design/test-ui/index.html
docs/design/test-ui/pages/access-denied--default.html
docs/design/test-ui/pages/administration--default.html
docs/design/test-ui/pages/administration-access--default.html
docs/design/test-ui/pages/administration-account-edit--default.html
docs/design/test-ui/pages/administration-accounts--default.html
docs/design/test-ui/pages/administration-accounts--empty.html
docs/design/test-ui/pages/administration-automation--default.html
docs/design/test-ui/pages/administration-automation-activity--default.html
docs/design/test-ui/pages/administration-configuration--default.html
docs/design/test-ui/pages/administration-mail-categories--default.html
docs/design/test-ui/pages/administration-organization-edit--default.html
docs/design/test-ui/pages/administration-organizations--default.html
docs/design/test-ui/pages/administration-principal-create--default.html
docs/design/test-ui/pages/administration-principal-replace--default.html
docs/design/test-ui/pages/administration-principals--default.html
docs/design/test-ui/pages/administration-roles--default.html
docs/design/test-ui/pages/case-assessment--default.html
docs/design/test-ui/pages/case-create--default.html
docs/design/test-ui/pages/case-details--conflict.html
docs/design/test-ui/pages/case-details--default.html
docs/design/test-ui/pages/case-details--unavailable.html
docs/design/test-ui/pages/cases--default.html
docs/design/test-ui/pages/cases--empty.html
docs/design/test-ui/pages/cases--unavailable.html
docs/design/test-ui/pages/connector-authorize--default.html
docs/design/test-ui/pages/dashboard--default.html
docs/design/test-ui/pages/error--default.html
docs/design/test-ui/pages/inbox--default.html
docs/design/test-ui/pages/inbox--empty.html
docs/design/test-ui/pages/inbox--unavailable.html
docs/design/test-ui/pages/inbox-message--default.html
docs/design/test-ui/pages/operations--default.html
docs/design/test-ui/pages/operations--empty.html
docs/design/test-ui/pages/password-change--default.html
docs/design/test-ui/pages/queues--default.html
docs/design/test-ui/pages/queues--empty.html
docs/design/test-ui/pages/received-details--default.html
docs/design/test-ui/pages/sign-in--default.html
docs/design/test-ui/pages/sign-in--signed-out.html
docs/design/test-ui/pages/sign-in--validation.html
docs/design/test-ui/pages/status-code--default.html
docs/design/test-ui/pages/triage-details--default.html
docs/design/test-ui/pages/unidentified-details--default.html
docs/design/test-ui/pages/upload--default.html
docs/design/test-ui/pages/upload--validation.html
docs/design/test-ui/pages/upload-group-status--default.html
docs/design/test-ui/pages/upload-group-status--needs-decision.html
docs/design/test-ui/pages/upload-group-status--processing.html
docs/design/test-ui/pages/upload-request--default.html
docs/design/test-ui/pages/upload-request--validation.html
docs/design/test-ui/pages/upload-status--default.html
docs/design/test-ui/pages/upload-status--needs-decision.html
docs/design/test-ui/pages/upload-status--processing.html
docs/design/test-ui/pages/vehicle-images--default.html
docs/design/test-ui/pages/vehicle-images--empty.html
docs/design/test-ui/pages/vehicle-images-details--default.html
