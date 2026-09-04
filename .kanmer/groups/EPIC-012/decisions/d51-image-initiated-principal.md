# D51 — optional principal on image-initiated records (operator, 2026-09-04)

Binding alongside the D29–D50 table in `context.md`; it is filed here so the
table need not be rewritten mid-run, and `context.md` points to it.

An image-initiated record carries an **optional principal**. It will often
not be known when image material is received; the field is **displayed
either way** on the Awaiting instruction row and quick view (`/Cases`,
[[CASE-042]]) and on the image-initiated detail page, showing the exact label
`Not known` when none is recorded — an operator-directed exception to the
absent-not-drawn rule, for this field only.

A principal is recorded only when it is genuinely known: staff set it on the
image-initiated detail page from the active principals list, or an intake
route that is principal-authenticated records it at registration (if such a
route exists today; none is built for this). It is never inferred from a
sender address or a registration match, and association with a Case does not
rewrite the record's own value. Storage is one nullable `PrincipalId` on
`ImageIntakeEntity` with its migration, grants and bootstrap census in one
diff. Owner: [[CASE-045]], which merges after CASE-032 and CASE-042.

Controller resolution derived from the operator's words ("there should be the
possibility that we know which principal, but we won't always know … if not
there will be a Not known option. We need to display this"): the two writers
above. The operator may veto either writer in CASE-045's review.
