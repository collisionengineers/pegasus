# Checklist — TICK-053 / MAIL-11

- [x] Extend the existing Core retained-mail search/match contract and derive one canonical receipt search-document projection from the existing intake-reader result.
- [x] Persist/replace that projection atomically with receipts and add the focused migration/model snapshot.
- [x] Apply mailbox/folder/body/attachment-name/attachment-content search before SQL count/paging and project exact match/searchability evidence.
- [x] Add the bounded approved Deleted Items Core port/use case and GET-only Graph adapter using the existing intake reader, with explicit unavailable/truncated states.
- [x] Wire the authenticated `/Inbox` search and retained detail-return context with accessible pagination and honest empty/error/truncation UI.
- [x] Add focused Core, persistence/migration, fake-Graph and Web acceptance tests without external writes or fabricated production data.
- [x] Update capabilities/current-architecture only to the exact local evidence tier.
- [x] Run locked restore, Release build, focused tests and the relevant full suite.
- [x] Run the four-lens simplification pass and append dated findings/dispositions to the plan.
- [x] Write the post-implementation report, commit/push the branch, open the dev-targeting PR, record traceability and move TICK-053 to Review.

## Parked post-deployment acceptance

- [x] After deployment, run the already-approved authenticated read-only production browse/search/thread journey; Deleted Items stays within the existing approved scope, with no mutation or historical reconstruction.
