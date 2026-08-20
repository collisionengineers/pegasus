## Final independent re-review — 2026-08-20

**Needs changes; blocker retained.** Root projection admission is now excluded, but body admission still searches raw retained `BodyPlainText` while detail shows `StaffForwardBodyCleaner` output. Removed wrapper/cid text can still return a row labeled Message body with no visible match. Make the displayed normalized body the one search owner and prove the negative case.
