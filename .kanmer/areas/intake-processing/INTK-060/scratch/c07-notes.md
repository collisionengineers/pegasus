## C07 slice, attempt 2 — progress (branch `c07-precase`)

**Item 1 (global Triage reference) — done.** Commits `c66b8580b` (production)
and `9a4aab92c` (tests).

- `TriageReferenceFormat.Format/TryParse` in `src/Pegasus.Core/Triage/TriageContracts.cs`
  (`T-` + sequence padded to five digits, expands past `T-99999`).
- `EfTriageStore.CreateAsync` allocates from the single seeded `TriageSequences`
  row inside its existing serializable transaction. The counter row is read with
  `WITH (UPDLOCK, HOLDLOCK)` (guarded by `Database.IsSqlServer()`, the house
  idiom from `EfIntakeReceiptStore.cs:388`) and is the **last** lock the
  transaction takes, so concurrent creations queue on it instead of deadlocking
  against each other's Triage range locks. This fixes the F-level defect where
  the store inserted `Sequence = 0` and violated `CK_Triage_Sequence`.
- Creation also records `TriageEntity.PrincipalId` from the originating
  instruction draft's `SuggestedPrincipalCode`, accepted only when it resolves
  to exactly one **active** principal; anything else stays null and renders
  `Not known`.
- `TriageSummary.Reference` is now the T reference; the provider claim number
  moved to a new `ClaimNumber` member. `TriageDetail` gained `PrincipalCode`,
  projected in the same round trip as the record.
- `/Triage/{id}` is titled by the reference and shows Triage reference,
  Registration and Principal. The `/Cases?tab=triage` row picks the T reference
  up through `TriageSummary.Reference` with no edit to the B-owned
  `Pages/Cases/Index.cshtml.cs`.

### ASSUMPTION 1 (C07 implementer, attempt 2) — `TriageSummary.Reference` stays nullable

Every persisted Triage now carries a reference, so the member should be
required. It is left `string?` because tightening it breaks two out-of-stream
in-memory fixtures — `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs:149`
and `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs:312` — which
pass `Reference: null` and are not in this slice's file list. Because: M5 keeps
the slice inside its files and the stop condition needs a green build.
Alternatives: edit both fixtures (outside scope), or keep the claim number in
`Reference` (contradicts the brief). Follow-up for those files' owner.

### ASSUMPTION 2 (C07 implementer, attempt 2) — one `Principal`/`Not known` label pair

PR 671's C17 named `OperatorLabels.ImageIntakePrincipal` /
`ImageIntakePrincipalNotKnown`. With Triage now showing a principal too, two
scoped constants would be the same two strings twice. They are consolidated to
`OperatorLabels.Principal` / `OperatorLabels.PrincipalNotKnown`, plus a new
`OperatorLabels.TriageReference`. Because: AGENTS.md "One list per concept"
binds over the PR's constant names, and the pair stays concept-scoped rather
than becoming a generic empty label. Alternatives: duplicate the pair per
surface, or alias one to the other.
