# Create Case

- **Parent:** [Cases](../index/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Create.cshtml`, `src/Pegasus.Web/Pages/Cases/Create.cshtml.cs`, `src/Pegasus.Web/Pages/Shared/_InstructionDraftFields.cshtml`
- [**How it works**](how-it-works.md)

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Create case, manual | `/Cases/Create` | [frame](../../../current/pegasus_cases_index_v28.html#create-case) · [page](../../../current/states/create-case.html) | [1580](../../../current/v28-shots/s15-create-case-1580.png) · [1440](../../../current/v28-shots/s15-create-case-1440.png) · [760](../../../current/v28-shots/s15-create-case-760.png) |
| Create case from a received instruction | `/Cases/Create?receiptId=74e58afe-48b3-4fa7-b5c8-b9d9e2b065d1` | [frame](../../../current/pegasus_cases_index_v28.html#create-case-from-receipt) · [page](../../../current/states/create-case-from-receipt.html) | [1580](../../../current/v28-shots/s16-create-case-from-receipt-1580.png) · [1440](../../../current/v28-shots/s16-create-case-from-receipt-1440.png) · [760](../../../current/v28-shots/s16-create-case-from-receipt-760.png) |

## Not captured

- Validation errors after a refused submit, and the refusal mode ("This item cannot become a case").
- The address-resolution choice between a found and an entered inspection address.
