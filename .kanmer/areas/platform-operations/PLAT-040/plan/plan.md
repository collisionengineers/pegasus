# Plan

Reuses [[PLAT-017]]'s procedure, re-resolved against today's schema.

1. **Inventory (read-only, done):** 99 tables, 62 non-empty; `transient-intake`
   77 blobs; all four queues verified empty; `authentication-ring` and
   `box-links` confirmed out of reach of this identity.
2. **Validate the preserve list before deleting anything.** The script fails
   closed if any of the 31 preserved names is no longer in the schema — a
   renamed table must be re-read, not silently wiped.
3. **SQL wipe:** `ALTER TABLE … NOCHECK CONSTRAINT ALL` across the wiped set,
   `DELETE` each, then `WITH CHECK CHECK CONSTRAINT ALL` to re-validate. The
   wiped set is resolved once into a table variable so the three statements and
   the verification all speak about the same tables. Foreign keys are disabled
   for the delete, so delete order cannot matter and a dangling reference
   cannot survive.
4. **Blob wipe:** `az storage blob delete-batch` on `transient-intake`.
5. **Verify in the same batch as the delete**, plus blob and queue counts and a
   smoke run afterwards.

## Timing — corrected during execution

Planned to run **after** the release-26 deploy and smoke, so the environment the
operator next tests is both new code and clean data.

That ordering has a cost I did not see when writing it: the wipe destroys
`ap.QDOS26012`, the only case that could demonstrate [[DOCS-010]] and
[[PLAT-039]] on real data. I checked whether to verify those first — and could
not, because the browser session had signed out and entering a password is not
something I may do.

On reflection the ordering is right anyway, and would have been the better
choice even with a live session: after the wipe, **one forwarded QDOS triage
email proves all four fixes at once** on a case created entirely by the new
code — route ([[MAIL-011]]), classification ([[MAIL-012]]), gallery
([[DOCS-010]]) and export ([[PLAT-039]]). Verifying against a case created by
the *old* code would have been weaker evidence, not stronger.
