# Operator authority

These documents are Collision Engineers' authoritative business requirements,
processes, operating knowledge, product requirements, and practices. They are
organized by concern so agents can find the relevant authority without treating
the folder as a single undifferentiated source.

Current explicit user direction authorizes Azure Workflow to maintain and
organize repository documentation, including this folder. Changes must preserve
every material business statement, remain reviewable in Git history, and stop
for user resolution if two authoritative statements materially conflict. Code,
references, plans, and predecessor behavior do not override this authority.

## Contents

### Business process

- [Case lifecycle](business-process/case-lifecycle.md)
- [Intake and work instructions](business-process/intake-and-work-instructions.md)
- [Case types and internal references](business-process/case-types-and-references.md)
- [Inspection address](business-process/inspection-address.md)
- [Reserved terms](business-process/reserved-terms.md)

### Product requirements

- [Required capabilities](product-requirements/required-capabilities.md)
- [Engineering and interface constraints](product-requirements/engineering-constraints.md)

### Systems and integrations

- [Current systems map](systems-and-integrations/README.md)

## Onboarding source map

The 2026-07-27 Azure Workflow onboarding consolidated the original fragments as
follows. This map preserves provenance and makes removals auditable.

| Original source | Canonical destination |
| --- | --- |
| `collision-engineers-process/process-overview.md` | `business-process/case-lifecycle.md` |
| `collision-engineers-process/initial-case-intake/*` | `business-process/intake-and-work-instructions.md` |
| `collision-engineers-process/case-guide/*` | `business-process/case-types-and-references.md` |
| `collision-engineers-process/inspection-address/inspection-address-overview.md` | `business-process/inspection-address.md` |
| `reserved-terms.md` | `business-process/reserved-terms.md` |
| `development-notes/required-features-overview.md` | `product-requirements/required-capabilities.md` |
| `development-notes/rules-to-follow.md` and `dev-tools.md` | `product-requirements/engineering-constraints.md` |
| `systems-used/*` | `systems-and-integrations/*` |
| empty `development-notes/Untitled.md` | removed; contained no statement |
