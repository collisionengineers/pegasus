# Plan — MAIL-007

Branch `task/mail-007-provider-footer` from origin/dev (post-#498, so the
paragraph rendering exists), worktree `../pegasus-worktrees/mail-007`. PR to
dev after [[DOCS-006]]'s PR merges (serial merges).

1. `StaffForwardBodyCleaner.TrimProviderFooter`: split lines; find the
   earliest line matching a footer marker (`[https?://…`/`[cid:…` placeholder
   lines; `<tel:`/`<mailto:`/`<http` decorated lines; "You are dealing
   with…"; "This e-mail/email and any attachments…"/"…is confidential…";
   "The registered office…"; "Proud members of…"; standalone "Disclaimer";
   "confidential and intended solely/only"). Cut from that line; return the
   original text when no marker matches or no non-empty line would remain.
2. Call it from `MailBodyPresentation.Present` (after the header split) and
   from the store's excerpt derivation, so the page and the preview line
   agree. Retained/search text untouched — classification unaffected by
   construction (recorded in research).
3. Facts from the measured corpus shapes; run the mail + cleaner + intake
   suites; Release build 0/0.
4. Simplification pass over the diff before the PR.
