# Open questions — CASE-045

## Open

- [ ] **Which recorded relationship supplies the principal on an
      *unassociated* image-initiated case?** CASE-045's ticket body says
      "reuse the canonical principal relationship and omit the value when
      none is recorded", but no such relationship exists for the records the
      Awaiting instruction queue shows. Verified read-only in
      `.worktrees/research` at `origin/dev` 80f0ca26 on 2026-09-04 (Claude
      Opus, confirming the same finding from the gpt-5.6-terra research run):

      - `ImageIntakeSummary`
        (`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs:100-109`)
        carries Id, OriginReceiptId, ImageIntakeReference,
        NormalizedVehicleRegistration, AssociatedCaseId/Reference,
        RegisteredAtUtc, State, ClosureReason — no principal.
      - `grep -rn "Principal" src/Pegasus.Core/ImageIntake/` returns **no
        hits**; `ImageIntakeEntity`
        (`src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs:8-48`)
        has no principal column either.
      - The only canonical path is
        `ImageIntake.OriginReceiptId` → active association → `Case` →
        `Case.PrincipalId` → `Principal.Code`. That path exists **only once
        the image record has joined a Case**, and D38's Awaiting instruction
        queue is by definition the *unmerged* image-initiated records
        (`IImageIntakeQueries.ListAsync(associated: false, …)`,
        `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:372-414`).

      So the answer decides the whole shape of the ticket. Options:

      1. **No new storage.** The principal is shown only where an
         association already exists — i.e. on the standalone
         `Pages/ImageIntake/Details` page for an associated record, projected
         through the existing Case → Principal join. The Awaiting queue row
         and quick view then never show a principal, because a record in
         that queue has none recorded. This needs no migration.
      2. **A stored principal on the image record.** Showing a principal on
         an *unassociated* Awaiting row requires a new nullable
         `PrincipalId` on `ImageIntakeEntity`, plus a migration, its grants
         and a bootstrap census in the same diff — and a separate answer to
         *who writes it*, since no intake path records one today and the
         ticket forbids inferring one.

      Option 2 adds a stored field, which the controller instruction for this
      run requires be raised as an operator question rather than added
      silently. Planning cannot proceed until the operator picks 1 or 2.

      Candidate-matching by registration on
      `Pages/ImageIntake/Details.cshtml.cs:26-45` must **not** be repurposed
      as an answer: the ticket body forbids inference or fabrication.

## Parked (explicitly deferred)
