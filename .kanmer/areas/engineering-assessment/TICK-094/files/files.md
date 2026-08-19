# Files — Engineer-owned report decisions

| Path/module | Expected change | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Assessment/AssessmentVocabulary.cs` / contracts | Typed approved report vocabulary | Migration of existing keys |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Outcome-specific confirmation/readiness | Conditional complexity |
| `src/Pegasus.Core/Reports/**` | Calculation policy and immutable accepted snapshot | Financial correctness |
| `src/Pegasus.Infrastructure/Persistence/**Assessment**` | Persist typed/versioned accepted decisions | Data migration |
| `src/Pegasus.Web/Pages/Cases/Assessment/**` | Engineer controls/validation/status | UX |
| Core/integration/render tests | Four variants, VAT, settlement, authorization, stale version | High consequence |

## Context files

| Path | Why |
| --- | --- |
| `reference/rendererref1/report_data_schema.json` | Allowed values/raw inputs |
| `reference/rendererref1/DESIGN_SPEC.md` | Calculation/narrative rules |
| `docs/adr/0021-*.md` | Automation vs Engineer authority |
| `src/Pegasus.Core/Assessment/**` | Existing confirmation owner |

## Out of scope

- AI confirmation or report approval.
- Audit/diminution/addendum outcomes.
