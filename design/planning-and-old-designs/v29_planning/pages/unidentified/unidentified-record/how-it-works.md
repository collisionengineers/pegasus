# Unidentified record — Open the Triage — how it works today

Read from the live source on 23 September 2026 (origin/dev 446c3ce2f).

The Unidentified record is `/Unidentified/{id}`. This covers its **Open the
Triage** action: when it is offered, what it asks for and what it creates.
Paths are under `src/` unless stated.

## What this page does not show

- No Principal field. Open the Triage asks only for a registration; the
  Triage's Principal comes from the receipt.
- No reason field, and no version or edit-scope check on the form.
- No way to make other material a Triage. The action appears only when the
  material already carries one accepted Triage match.
- No jump to the new Triage. After opening it the page returns to the
  Unidentified record; the Triage link then sits in the Resolution panel.
- No Case/PO. A Triage opened here gets a `T-` reference.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-15](../../../../../../docs/frd/frd-15-work-centre-queues-and-search.md) | The Unidentified record's actions; "Where Core allows it the record also offers **Open the Triage**"; no "Resolution" or "Resolve material" wording in the UI. |
| [FRD-02](../../../../../../docs/frd/frd-02-intake-and-source-identity.md) | A classified Triage request with no registration is held in Unidentified until one is known; opening the Triage resolves that item. |
| [FRD-03](../../../../../../docs/frd/frd-03-triage.md) | No registration means no Triage; a known registration opens it as Open; the `T-` reference; staff may classify retained material as Triage, recording source, route evidence, actor, time, reason and policy version; automatic association with one Case. |
| [CONTEXT.md](../../../../../../CONTEXT.md) | Unidentified is distinct from Triage; Triage allocates no Case/PO. |

## Source

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pegasus.Web/Pages/Unidentified/Details.cshtml` | The Resolve panel's **Open the Triage** link, the `unidentified-triage-dialog`, and the Resolution panel's link to an existing Triage. |
| Web | `Pegasus.Web/Pages/Unidentified/Details.cshtml.cs` | `CanOpenTriage`, `OnPostOpenTriageAsync`, `ExecuteAsync`, `SynchronizeAsync`, `TriageDialog` (`"triage"`). |
| Core | `Pegasus.Core/Intake/Unidentified/UnidentifiedItemContext.cs` | `GetUnidentifiedItemContext`: `CanOpenTriage`, `Triage`, `SuggestedRegistration`. |
| Core | `Pegasus.Core/Triage/TriageLifecycle.cs` | `CreateTriageFromIntake` (validation, then `TriageCasePairing`), `TriageLifecycleRules.ValidateCreate`. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfTriageStore.cs` | Creates the Triage, allocates the reference, records the established Principal. |

## Behaviours

### When Open the Triage is offered

`GetUnidentifiedItemContext` sets `CanOpenTriage` when all hold:

- the item's origin is a receipt, and the receipt is found;
- the item is Open;
- no Triage exists for that receipt (`ITriageQueries.GetByOriginReceiptAsync`
  returns none);
- the receipt's decision is `NeedsSorting`;
- exactly one of the receipt's evidence items has the finding
  `AcceptedTriageMatch`.

When true, the open item's **Resolve** panel shows **Open the Triage** (icon
`clipboard-list`) beside Link to Case, Create case, Register images and Close
with reason. It links to `?dialog=triage` and opens the dialog in place with
script.

### What it asks for

The dialog "Open the Triage" has one required field, **Vehicle
registration** (mono, at most 20 characters). It is prefilled with
`SuggestedRegistration`: the registration of the pending suggested readings
when exactly one distinct value agrees. It asks for nothing else: no
Principal, no reason. The form posts `vehicleRegistration` and an operation
key.

### What happens on submit

`OnPostOpenTriageAsync` rechecks the casework right and `CanOpenTriage`
("A Triage cannot be opened from this item."), takes the single accepted
Triage match, and resolves the receipt's origin ("The material has no
completed evaluation to open a Triage from."). It then calls
`ICreateTriageFromIntake` with the origin, the registration normalised by
`ImageIntakeLifecycleRules.NormalizeRegistrationInput`, the accepted match
as evidence, the staff actor, and the key `triage-from-staff:{operationKey}`.

`CreateTriageFromIntake` validates the origin, the registration, the match
evidence and the actor, creates the Triage through the store, and then runs
`TriageCasePairing.PairTriageAsync`, which may link one Case automatically.
The store allocates the next `T-` reference. It records a Principal only when
the receipt's instruction draft suggests a code that resolves to exactly one
active Principal; otherwise the Triage reads "Not known"
(`ResolveEstablishedPrincipalAsync`).

The handler then calls `SynchronizeAsync`, which reads the receipt again and
settles the Unidentified item against its new destination; a failure there
is logged, not shown. The page returns to the Unidentified record with "The
Triage was opened." On failure it returns with the dialog open and either the
argument's message or "The action was not applied because the item changed
or the action is not permitted. Reload and try again."

### Afterwards

Once the item is no longer open, the page shows a **Resolution** panel with
an Outcome fact (`ClosedOutcome`, else `Item.ResolutionTargetReference`, else
"Resolved"), **Open the Triage** linking to `/Triage/{id}` when the context
has a Triage, and **Reopen**.

## Things the FRD does not settle

- Whether opening a Triage here should record a reason. FRD-03's staff
  classification records one; this action asks for none.
- Whether staff should set the Principal while opening the Triage, or only
  afterwards on the Triage record.
- Whether the page should go to the new Triage after opening it.
- The panel headings. FRD-15 says there is no "Resolution" wording; the
  page's panels are headed **Resolve** and **Resolution**.
