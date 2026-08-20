# Post-implementation report — PR-041

## Outcome

Successful moves remain excluded from ordinary Inbox browse but are findable through the existing non-empty retained search. The canonical query projects latest successful logical folder, preserves mailbox filtering/count/paging, and keeps the immutable arrival row unchanged. Web copy explains the wider current-folder search scope.

## Verification

- Persistence happy/reclassification cases prove Inbox exclusion, search inclusion exactly once, current logical folder, mailbox filtering, total count, page-two empty and immutable arrival folder.
- `AuthenticatedStaffConfirmsTheServerDerivedFolderWithoutPostingTransportIdentity` passed with the final authenticated `/Inbox?search=estimate` assertion and visible current-folder search notice.
- The broader retained-mail slice passed all 87 unaffected cases; its only initial failure was the now-corrected old copy expectation, rerun separately on the final binary.
- No destination tabs, second search store or duplicate category policy was added.

## Simplicity

MAIL-11’s retained search remains the only search owner.
