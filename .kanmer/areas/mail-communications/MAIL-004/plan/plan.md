# Plan — MAIL-004

## Approach and estimate

Add one focused Core catalogue contract/use case, one EF entity/store/table, and one dedicated Administrator page. MAIL-13 consumes the Active-only resolver by internal id; MAIL-004 performs no Graph call. Reuse approved-mailbox authorization, serializable replay/history, migration/runtime-grant, admin navigation/form, and test conventions. Estimated 14–18 files plus generated migration metadata, roughly 500–800 handwritten lines.

## Steps

1. Add the global `ApprovedOutlookCategory` vocabulary (internal id, trimmed display name, Active/Disabled, version), Administrator-only list/update commands, and Active-only resolver. Reject duplicates case-insensitively; disable rather than delete; preserve expected-version, reason and operation-key semantics.
2. Persist one normalized table through the existing administration EF boundary, ActionHistory convention and Web-only `SELECT/INSERT/UPDATE` grants with DELETE denied. Register the store; add no Graph id/color or per-mailbox join.
3. Add `/Administration/MailCategories` and one Administration index link. Forms post internal id only, and display only name/state; no Graph sync, arbitrary category, color/id, search/linking, or generic rules UI.
4. Add focused Core, relational and Web tests proving authorization, validation/duplicates, Active-only reload for MAIL-13, replay/version/history, disable-not-delete, and identifier-free UI.
5. Run locked restore/build, focused tests and proportional shared migration/admin tests. Run four lenses (reuse, simplification, efficiency, altitude), apply safe findings and record dispositions.
6. Commit/push, open a PR to `dev`, write PIR/traceability, and move to Review without reviewing or merging.

## Governing docs

FRD-08 owns MAIL-13/category behavior and FRD-12 owns Administrator UX. Existing requirements already support a configured approved set; update evidence only if implementation exposes a contradiction. No ADR/new boundary/deployment.

## Exclusions

No Graph master-category read/write/sync, ids/colors, message mutation, search/filter/index, Case association, Automation tool, generic settings framework, permission expansion, deployment, or live write.

## Simplification pass

To be recorded after implementation.

## Simplification pass — 2026-08-20

- **Reuse:** reused the existing StaffAuthorization management-right pattern, serializable ActionHistory/replay shape, administration page/index/form conventions, EF administration model, runtime-grant matrix, route inventory and browser accessibility lane.
- **Simplification:** kept one global two-state catalogue and one narrow management store; separated the Active-only resolver port so MAIL-13 cannot receive list/update authority. No generic settings/rules framework or mailbox-category join.
- **Efficiency:** list/order and exact-id Active lookup are direct indexed EF queries; duplicate enforcement uses one normalized unique index. Catalogue size is operator-bounded and no Graph polling/sync exists.
- **Altitude:** Core owns validation/authorization/Active reload; Infrastructure owns EF/history; Web owns thin Administrator forms. No retained-mail, search, association, Graph, Automation or message-action code changed.

Applied finding: added the new route to the existing canonical authorization, antiforgery and browser axe inventories rather than adding parallel page-only accessibility machinery. No unapplied findings.
