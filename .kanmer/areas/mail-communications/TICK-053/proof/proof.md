# Proof — TICK-053 / MAIL-11 (deployed and live-verified at release 16, 2026-08-21)

Type: visual. Deployment bundle: [[DELIV-015]] proof. Deployed to production at `4111ad29`. Live verification on the production workspace:

- **Retained search**: query "instruction" filtered before paging across body, attachment filenames, and readable attachment content — results identified **where each match occurred** ("Matched in: Attachment content: Bodyshopreport555017-V1.pdf (attachment 2)", "Matched in: Message body"), the PR-024 no-invisible-match rule live.
- **Scopes**: Inbox / Sent / Deleted items render as explicit scopes. The **Deleted Items** scope returned its honest unavailable state live ("Deleted Items search is unavailable. Retained Inbox mail remains available.") — the bounded GET-only read maps its failure classes to unavailable without hiding the scope or inventing results (PR-016/020/031/033/037 behaviour).
- Message detail, retained-scope thread, and attachment views render live (EREF6 walkthrough).

The bounded (100-newest) Deleted Items read against a live folder returning results is exercisable when the approved mailbox's Deleted Items access is available; the fail-closed path is the live-verified behaviour today.
