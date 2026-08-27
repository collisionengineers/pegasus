# Plan — PLAT-045

Runs after release 34 ([[DELIV-027]]) has smoked. Reuses the PLAT-040 batch
shape: one Entra-token `SqlConnection` (`az account get-access-token
--resource https://database.windows.net/`), one T-SQL batch.

1. **Inventory (read-only).** `sys.tables` count; preserve list found/missing
   (must be all found); tables-to-wipe list and row totals. Assert the
   preserve list resolves to PLAT-040's 31 plus every `ApprovedMailbox*`
   table; stop if any listed table is missing.
2. **Blobs/queues before.** `az storage blob list` on
   `pegcustody252ow37gij/transient-intake` (count); `az storage message peek`
   / queue approximate count on the four `pegtrans252ow37gij` queues.
3. **Wipe batch.** `ALTER TABLE … NOCHECK CONSTRAINT ALL` on the wipe set →
   `DELETE` each → `ALTER TABLE … WITH CHECK CHECK CONSTRAINT ALL` → verify
   every wiped table reads 0 and `CaseSequences.LastAllocatedSequence`,
   `ImageIntakeSequences`, `UnidentifiedSequences` are unchanged.
4. **Blobs.** `az storage blob delete-batch --account-name pegcustody252ow37gij
   --source transient-intake --auth-mode login`; re-list → 0.
5. **Smoke.** `Invoke-ProductionSmoke.ps1` against release 34's SHA; browse
   sign-in, Inbox, Queues, Cases empty.
6. **Proof** with before/after tables in this ticket; the counts also go into
   the release-34 paragraph in `docs/operations.md` under DELIV-027.

Not touched: Outlook, Box, `authentication-ring`, `box-links`,
`pegtrans252ow37gij` containers, poll cursors, sequences.

## Simplification pass

n/a — operations, no code.
