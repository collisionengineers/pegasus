Read from the live source on 18 September 2026.

## What this page does not show

- No inspection-address resolution, provenance icons, "Change a value"
  disclosure or refusal state — those all belong to the received-file-seeded
  variant of this same route, which this capture does not cover (see the
  folder's `README.md`).
- No live validation summary content — the manual form's `_ErrorSummary`
  partial is present but empty until a submit actually fails; this static
  capture shows the form's clean initial state only.
- No confirmation of what happens next (which queue the new Case lands in,
  or its allocated reference) — the live handler redirects straight to the
  Case's own record with a status message; there is no intermediate "Case
  created" screen to capture here.

## Source table

| File | What it owns |
| --- | --- |
| `Pages/Cases/Create.cshtml` | Branches on `Model.IsManual`; the manual branch (the only one captured) renders the Case panel and the Vehicle and inspection panel as one form. |
| `Pages/Cases/Create.cshtml.cs` (`CreateModel`) | `OnGetAsync` sets `IsManual = true` only when `receiptId == Guid.Empty` (the "Add" / Ctrl+N path); `OnPostCreateManualAsync` validates and calls `ICreateManualCase`. |
| `Core.Cases.CasePrincipalCode` | The principal code's maximum length, enforced both by the input's `maxlength` and a server-side check. |

## Behaviours found

### Manual creation is a genuinely distinct code path, not a mode of the seeded form

`OnGetAsync` (`Create.cshtml.cs:229-246`) short-circuits entirely for
`receiptId == Guid.Empty`: it sets `IsManual`, generates the operation id,
loads Claim Source choices and returns — none of the receipt read, address
resolution or refusal logic further down in the method ever runs. The two
halves of `Create.cshtml` (`@if (Model.IsManual) { ... } else { ... }`) are
consequently two unrelated screens sharing one route and one "Create case"
button style, not one screen with an optional section.

### Case type is the one field silently narrower for manual creation

The manual form's `<select asp-for="CaseType">` only ever offers Inspection
and Inspection and Audit (`Create.cshtml:76-79`); the seeded form's
equivalent select can additionally offer Audit, but only when
`Model.IsRetainedClassifiedAudit` is true. `OnPostCreateManualAsync`
enforces this server-side too:
`ValidateAuditCannotBeManuallyCreated` rejects `CaseType.Audit` outright
(`Create.cshtml.cs:724-730`) — a standalone Audit Case cannot be created
through this page's manual path at all, consistent with
[`CONTEXT.md`](../../../../../../CONTEXT.md)'s definition of Audit as
either created with or without an original report, but always instructed
work, never a hand-typed manual record.

### The principal code is normalised, not merely validated

Before anything else, `PrincipalCode` is trimmed and upper-cased
(`Create.cshtml.cs:499`) — what the operator types and what gets checked
against `CasePrincipalCode.MaximumLength` are not the same string; the form
never reflects the normalised value back before submit.

### One button commits the whole Case in one write

Manual creation is a single `ICreateManualCase.ExecuteAsync` call
(`Create.cshtml.cs:577-581`) carrying every posted field as one
`CaseEditableData`/`InstructionDraft` pair — there is no separate save-draft
step and no partial-completion path the way the seeded variant's
three-write sequence (draft, address, acceptance) has.

## Things the FRD does not settle

- Nothing in `Create.cshtml.cs` states why the manual path silently omits
  the Audit case type from its own `<select>` rather than showing it disabled
  or explaining the restriction — a person who expects to create a
  standalone Audit Case by hand has no on-screen indication that the option
  does not exist here, only that it is simply not offered.
