# Plan — PR-041

## Approach

1. In the canonical retained query, exclude successful moves only for non-search Inbox browsing.
2. Project the latest successful `MailLogicalFolderType` into the summary as current-location evidence.
3. Clarify existing search scope and treat matching moved detail as inside that search result.
4. Add SQL/Web evidence for inclusion exactly once, paging, mailbox filtering and preserved arrival folder.

## Governing docs

FRD-08 permits findability through search. This plan reuses the existing MAIL-11 retained search and logical-folder enum, avoiding new policy ownership.

## Risks

Deleted search remains its separately bounded Graph route; this change affects retained Inbox search only.
