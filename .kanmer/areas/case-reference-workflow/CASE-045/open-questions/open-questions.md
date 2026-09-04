# Open questions — CASE-045

## Open

- [x] **Which recorded relationship supplies the principal on an
      *unassociated* image-initiated case?** Answered by the operator on
      2026-09-04 and recorded as **D51** in EPIC-012 `context.md`: there must
      be the *possibility* of knowing the principal for an image-initiated
      record, it will often not be known, and the field is **displayed
      either way** — the exact label `Not known` when none is recorded. So
      option 2 applies: a nullable `PrincipalId` on `ImageIntakeEntity`
      (migration, grants and bootstrap census in one diff), shown on the
      Awaiting instruction row, the quick view and `Pages/ImageIntake/Details`.
      Writers (controller resolution derived from the answer, operator may
      veto in review): (a) staff set it on the image-initiated detail page
      from the active principals list (default `Not known`); (b) an intake
      route that already knows the principal because it is
      principal-authenticated records it at registration — the research must
      say whether such a route exists today and no new route is built. Never
      inferred from a sender address or a registration match; association
      with a Case does not rewrite the record's own value.

      Original finding (2026-09-04, Claude Opus confirming the gpt-5.6-terra
      research): `ImageIntakeSummary`
      (`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs:100-109`) and
      `ImageIntakeEntity` (`ImageIntakeEntities.cs:8-48`) carry no principal;
      the only path is `OriginReceiptId` → association → `Case.PrincipalId`,
      which the Awaiting queue (`IImageIntakeQueries.ListAsync(associated:
      false)`, `Pages/Cases/Index.cshtml.cs:372-414`) by definition lacks.
      Candidate-matching by registration on
      `Pages/ImageIntake/Details.cshtml.cs:26-45` must not be repurposed as
      the source.

## Parked (explicitly deferred)
