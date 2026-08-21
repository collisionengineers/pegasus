# Research — MAIL-007

Footer shapes measured across 261 real plain-text bodies (local corpus,
read-only) plus the four live retained messages:

- **183/261 bodies carry at least one unambiguous footer marker**; in 174 of
  them real message content precedes the first marker; in **9 the body is
  footer-only** (signature-only instruction emails — the live QDOS shape) —
  trimming those to empty would hide the only text there is, so the rule
  fails open when nothing would remain.
- Marker frequencies: decorated links `<http…>` 137, disclaimer "This
  email/e-mail and any attachments…" 122, `<mailto:…>` 105, `[cid:…]` 101,
  "The registered office…" 85, "Proud members of" 84, `[https://…]` image
  placeholders 74, `<tel:…>` 69, "You are dealing with…" 53, standalone
  "Disclaimer" 6, "confidential and intended solely/only" 9.
- Bodies with **no** marker end in ordinary sign-offs ("Yours faithfully",
  "Kind regards, / Claims Team") or quoted originals — nothing to trim;
  fail-open leaves them whole. Two residual footer shapes deliberately left
  alone: plain address blocks with no marker line (a repairer's letterhead
  address) and a solicitor's "regulated and authorised by…" line — both rare,
  both safer shown than guessed at.

Design decisions this grounds:

1. **Display-side only.** The trim is a new `StaffForwardBodyCleaner`
   function called by the message-page rendering and the list excerpt — the
   retained body, the search text, and everything classification reads are
   untouched, so classification outcomes cannot change (the ticket's
   predicate check holds by construction).
2. **Boundary = earliest marker line**, cutting from it to the end. Markers
   are the measured set above. "Yours faithfully / Neil Duncombe" precedes
   every marker in the letter shapes, so the sign-off survives; a repeated
   name/job-title pair at the very top of a signature block may survive too —
   accepted residue, safer than a name heuristic.
3. **Fail open:** no marker → unchanged; trimming would leave no non-empty
   line → unchanged (the signature-only bodies).
4. The forwarded-header regex coupling to the reader is untouched.
