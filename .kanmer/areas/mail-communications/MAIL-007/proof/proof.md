# Proof — MAIL-007 (verified on deployed release 16, 2026-08-21)

Type: visual. Deployment evidence bundle: [[DELIV-015]] proof. Verified on the production UI over real retained mail.

- Message page (EREF6): the displayed body is exactly the sender's sign-off — "Julie Fleming / Existing Claims Handler" — with the Collision Engineers contact/LinkedIn/registered-office/disclaimer footer and `[https://…]`/`[cid:…]` placeholder lines gone (`StaffForwardBodyCleaner.TrimProviderFooter` at the one display seam).
- Inbox excerpts derive from the cleaned body: sender's words lead every row; the EREF24-shape inline-letter email legitimately excerpts the letter text (its true body), and signature-only bodies legitimately show the signature — fail-open behaviour confirmed (no over-trimming to empty).
- The three cleaner facts (trim keeps the sign-off, signature-only bodies stay whole, markerless bodies stay whole) shipped green with the merge (#500) and guard the fail-open contract.
