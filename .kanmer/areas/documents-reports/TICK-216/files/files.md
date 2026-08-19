# Files — wording and signature activation

## Change files

| Path/module | Expected change | Risk |
| --- | --- | --- |
| `docs/open-decisions.md` | Resolve or narrow the report-wording decision after operator confirmation | Protected business meaning |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Specify accepted assessment wording/signature fail-closed behavior | Must not invent qualifications |
| `docs/capabilities.md` | Reflect activation/acceptance evidence for RPT-02 | Schedule vs delivery claim |
| `reference/rendererref1/**` | Read-only supplied evidence; never modify | Evidence integrity |
| `docs/design/brand/signatures/**` | Governed source assets used only if authorised | Personal signature misuse |
| `src/Pegasus.Core/**` | Own engineer/signature authorization and required-field policy | One policy owner |
| `src/Pegasus.Infrastructure/**` | Map authorised signature key/assets into renderer | Must fail closed |
| `tests/**` | Prove authorised selection and rejection of missing/unknown/mismatched signatures | Security and professional attribution |

## Context files

| Path | Why read it |
| --- | --- |
| `reference/rendererref1/DESIGN_SPEC.md` | Supplied assessment wording and outcome baseline |
| `reference/rendererref1/report_data_schema.json` | Required fields and allowed signature keys |
| `docs/open-decisions.md` | Existing unresolved wording authority |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Report approval/finality and signature status |
| `workspaces/report-renderer/NOTICE.md` | Authorised-use restriction |
| `EPIC-004/context.md` | rendererref1 is evidence, not a second policy owner |

## Out of scope

- Editing supplied evidence.
- Inventing missing qualifications, salvage wording, or signature authorization.
- Treating generation as report issue or external sending.
