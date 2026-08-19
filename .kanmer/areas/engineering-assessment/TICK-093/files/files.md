# Files — canonical repair specification

| Path/module | Expected change | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Evolve estimate lines/spec identity/version/provenance | Compatibility |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Validate accepted spec/category/source/readiness | Policy breadth |
| `src/Pegasus.Core/Assessment/AssessmentOperations.cs` | Versioned acceptance/correction operations | Concurrency |
| `src/Pegasus.Infrastructure/Persistence/**Assessment**` | Persist versions, source, supersession, line categories | Migration/data conversion |
| `src/Pegasus.Core/Reports/**` | Read accepted spec snapshot only | Duplication |
| Assessment/report tests | Prove routes, confirmation, versions, three sections, totals binding | Fixture size |
| `docs/frd/frd-06-*.md`, FRD-11 | Exact behavior | Authority alignment |

## Context files

| Path | Why |
| --- | --- |
| `reference/rendererref1/DESIGN_SPEC.md` | Three report list sections and names-only rule |
| `reference/rendererref1/report_data_schema.json` | Approved repair-spec payload shape |
| `TICK-205 research` | Audit dual-version exception |
| `src/Pegasus.Core/Assessment/**` | Existing estimate/confirmation owner |

## Out of scope

- Implementing Glass's/Audatex/AI extraction routes themselves.
- Audit rendering/template.
