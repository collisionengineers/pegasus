# Plan — ENG-009: Initiate Cazana valuation from the case workbench

## Approach

Add one Engineer-only form to the existing assessment valuation section. Its POST invokes the case-centred Core command supplied by ENG-008 and returns the existing concise status/error presentation. The handler passes only case identity, actor, and operation key; it neither reads nor submits VRM, mileage, incident date, or provider credentials. This reuses the assessment page's established command-boundary, antiforgery, and error conventions rather than adding a browser client or a second valuation owner.

## Governing docs

- **New FRD:** `docs/frd/frd-13-cazana-valuation.md` is required and is owned by [[PLAT-022]]. It will define the exact Cazana request, source evidence, failure/recovery, Engineer authority, and activation criteria for EXT-07/10/13.
- **FRD-06:** ENG-009 preserves source-labelled vehicle and valuation evidence, never makes a provider result an Engineer-selected value, and relies on [[INTK-026]] for canonical-mile case facts.
- **FRD-07:** The UI calls only the application path; ENG-009 adds no external client, credential, or activation claim.
- **FRD-12 / UI-15:** Reuse the progressive workbench's valuation section with one compact labelled action and no explanatory copy.

## Steps

1. Wait for [[PLAT-022]], [[INTK-026]], and ENG-008 to complete: FRD-13 must be linked and ENG-008 must expose a case-ID Core command with explicit outcomes.
2. Add the Engineer-only valuation-section form in `Index.cshtml`, with the existing antiforgery and operation-key pattern. It contains no VRM, VIN, mileage, date, condition, confidence, or stocking-depreciation control.
3. Inject and call ENG-008's command from a POST handler in `Index.cshtml.cs`; preserve existing case lookup, role enforcement, operation validation, outcome-to-status/error, and redirect behaviour.
4. Add focused Web integration tests using a fake Core command. Assert command identity forwarding, no action for unauthorised staff, no direct HTTP client, and compact success/refusal/error rendering.
5. Run the focused tests and repository Release build/test commands. Perform the required simplification pass before review.

## Verification

- Focused Web tests prove the action exists only for Engineers and posts only case/action identity.
- ENG-008's tests prove its Cazana request uses the case incident date rather than today and contains only VRM, canonical mileage, and date.
- `dotnet restore`, `dotnet build --configuration Release`, and focused/full `dotnet test` provide the review evidence.

## Risks / open questions

- ENG-008 has no implementation or public command today. Do not create its Core contract from this ticket; wait for its owning ticket.
- Cazana live access is prohibited until PLAT-022 has exact operator consent and recorded activation evidence.
- INTK-026 must land before ENG-008 so provider mileage is canonical miles and retains conversion provenance.
