# Create case — how it works today

Read from the live source on 23 September 2026 (origin/dev 446c3ce2f).

`/Cases/Create` has two forms. Without a `receiptId` it is the manual form
(staff type the facts). With `?receiptId=` it is the from-receipt form over
received material. Paths are under `src/` unless stated.

## What this page does not show

- No Audit option on the manual form, and none on the from-receipt form
  unless the received mail was classified as an Audit.
- No Triage option. Nothing on this page opens a Triage.
- No Principal list. The Principal is typed as a code.
- No required marker on the from-receipt form's "Confirmed principal code",
  though the server requires it.
- No confirmation on the new Case. The page sets
  `TempData["CaseDetailsStatus"]`; the Case record reads
  `TempData["CaseStatus"]`, and nothing reads `CaseDetailsStatus`.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-12](../../../../../../docs/frd/frd-12-operator-experience.md) | Create Case opens direct staff creation with the identity-critical facts and no invented receipt; opened from an Unidentified item or an upload decision, the same form uses that receipt and the normal allocation path; a file that cannot become a Case is refused with Open message or Open file. |
| [FRD-01](../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md) | A reference is allocated once Principal and Case type are settled; missing business detail keeps the Case Not ready; the three Case types; a standalone Audit on manual upload needs a staff member to accept the proposal. |
| [FRD-02](../../../../../../docs/frd/frd-02-intake-and-source-identity.md) | The ways intake starts, including staff acceptance of a proposal. |
| [FRD-09](../../../../../../docs/frd/frd-09-provider-and-intermediary-routes.md) | Provider API Case types `inspection`, `audit`, `auditreport` (Inspection + Audit) and `triage`. |
| [FRD-22](../../../../../../docs/frd/frd-22-pre-case-gates-matching-and-association.md) | The gates before a Case/PO: an unambiguous Case type and Principal. |
| [CONTEXT.md](../../../../../../CONTEXT.md) | Case, Case/PO, Audit, Inspection + Audit, Triage. |

## Source

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pegasus.Web/Pages/Cases/Create.cshtml` | Both forms: fields, Case type options, the refusal panel, the address panel. |
| Web | `Pegasus.Web/Pages/Cases/Create.cshtml.cs` | `OnGetAsync`, `OnPostCreateAsync`, `OnPostCreateManualAsync`, `ValidateAndBuildDraft`, `ValidateAddressChoice`, `ValidateAuditCannotBeManuallyCreated`, `IsRetainedClassifiedAudit`, `DescribeRefusal`. |
| Core | `Pegasus.Core/Cases/ManualCaseCreation.cs` | `CreateManualCase`: casework right, Staff actor, identity-critical fields, no Audit. |
| Core | `Pegasus.Core/Intake/InstructionDraftCompleteness.cs` | `MissingIdentityCriticalFieldNames`: Claimant name, Claim number, Vehicle registration. |
| Core | `Pegasus.Core/Cases/CaseContracts.cs` | `CaseType`, `CasePrincipalCode.MaximumLength` (20). |

## Behaviours

### Where it is opened

| From | Link | Form |
| --- | --- | --- |
| Work Centre header | **Create Case** (`_WorkCentreBody.cshtml`) | Manual |
| Utility bar | **New case** (`Shared/_Layout.cshtml`) | Manual |
| Ctrl N | `site.js` goes to `/Cases/Create` | Manual |
| Cases list, Awaiting instruction quick detail | **Create Case** | Manual |
| Unidentified record | **Create case** with `receiptId` | From receipt |
| Upload outcomes | "Create a new case" or "Create a case" with `receiptId` (`Presentation/UploadOutcome.cs`) | From receipt |

Search's **Create Case** goes to `/Upload`, not here. Its source comment says
`/Cases/Create` "returns NotFound without" a receipt; `OnGetAsync` serves the
manual form when `receiptId` is empty.

### The manual form

Panels **Case** and **Vehicle and inspection**:

| Field | Required on the form |
| --- | --- |
| Principal code (max 20) | Yes |
| Claim Source ("Not recorded" or an active Claim Source) | No |
| Case type: **Inspection** (default) or **Inspection and Audit** | — |
| Claimant | Yes |
| Claim number | Yes |
| Registration | Yes |
| Instruction date | No |
| Make, Model, Mileage, Mileage unit, Incident date, Inspection date, Inspection address, Accident circumstances | No |

`OnPostCreateManualAsync` checks, collecting every error:

- a valid operation id, else "This request is no longer valid. Reload the
  page and try again.";
- the Principal code, upper-cased: "Enter the principal code." or "The
  principal code must be 20 characters or fewer.";
- the Case type defined and not Audit, else "Choose a valid case type.";
- the Claim Source one of the active records, else "Select an active Claim
  Source from the directory.";
- each missing identity-critical field: "{Claimant name | Claim number |
  Vehicle registration} is needed before a case can be created.";
- `CaseDataPolicy.Normalize`'s own messages.

The mileage unit is kept only with a mileage. The inspection deadline is set
to the inspection date, and the inspection mode is inferred from the address.
`CreateManualCase` then repeats the Staff and casework checks, refuses
missing identity fields ("A manual case needs …") and refuses Audit ("The
case type is invalid."). When no active Principal has the code
(`PrincipalUnavailableException`), the page reads "This principal is
unavailable." Success redirects to `/Cases/{id}`.

### The from-receipt form

`OnGetAsync` loads the receipt (404 when absent). If the receipt already has
a Case, it redirects to that Case. If the receipt's decision cannot become a
Case (anything but OCR required, Needs sorting or a decision
`IntakeDecisionPolicy.CanBecomeCase` accepts), the page shows "This item
cannot become a case" with the reason, **Open message** and **Open file**,
and no form.

Otherwise the page shows "Seeded from {file}" with **Open file** and **Open
message**, then:

- **Details**: Claimant, Claim number, Registration, Vehicle, Incident date
  and Instruction date as read, each with an Extracted tag where it came from
  the document, and a **Change a value** disclosure holding the editable
  fields.
- **Inspection address**: the Image Based Assessment notice for an
  image-based Principal; the settled address; a found address with the
  choice "Use the address found" or "Use this address instead"; or, when
  nothing was found, a required address.
- **Case**: Confirmed principal code (prefilled from the draft's suggestion)
  and Case type, then **Accept and create case** and Cancel.

The Case type select offers **Inspection** and **Inspection and Audit**, and
**Audit** only when `IsRetainedClassifiedAudit`: the receipt's mail
classification decided Case type Audit. It opens on the classification's
Case type, or Inspection.

`ValidateAndBuildDraft` checks the Principal code ("Enter the confirmed
principal code.", 20 characters), a defined Case type, the address choice,
Audit only for a classified Audit receipt ("Choose a valid case type."), and
the three identity-critical fields ("… is needed before this item can become
a case."). The post then corrects the draft (reason "Case created from
reviewed intake."), settles the address where needed, and calls
`AttemptStaffCreateAsync` with the Case type, Principal code, completeness
(instruction complete; images complete when the receipt has instruction
images), any standalone-Audit evidence kept for the receipt, and the
inspection date. Success redirects to the new Case.

### Case type words

The options read "Inspection", "Inspection and Audit" and "Audit". The Case
record's ribbon chip reads "Inspection + Audit" and its Overview reads
"Inspection and audit" (see [Case record](../case-record/how-it-works.md)).

## Things the FRD does not settle

- Whether staff may create a standalone Audit by hand. FRD-01 describes a
  manual upload proposal that staff accept; the page refuses Audit unless
  the mail classification already said Audit.
- Whether this page should be able to create a Triage. FRD-03 lets staff
  classify retained material as Triage; the only staff route in the source
  is the Unidentified record's Open the Triage.
- The Case type's wording on this form.
