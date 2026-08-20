# INTK-014 checklist

- [ ] Core: new work kinds routed; `ICaseCustody` image-case operations with fail-closed defaults
- [ ] Migration: nullable `ExternalWorkItems.CaseId` + `ImageIntakeId`; ImageIntakes custody columns; grants test green
- [ ] Registration enqueues `create_image_case_custody` in-transaction (replay-safe)
- [ ] Merge transition enqueues `merge_image_case_custody` in-transaction (replay-safe, carries CaseId)
- [ ] Processor handles both kinds; completion stamps custody columns under lease
- [ ] BoxCaseCustody: image asset retention + merge move + fenced delete of emptied folder
- [ ] LocalCaseCustody: same operations for local testing
- [ ] Failure paths: re-arm with backoff on dependency failure, terminal fail recorded as `CustodyState='failed'`, images never lost
- [ ] Tests: Box adapter (StatefulBox move/delete), integration end-to-end with LocalCaseCustody, failure/replay coverage
- [ ] Build zero warnings; focused test filters green; migration scripts green
- [ ] Simplification pass recorded in plan; post-implementation report; PR to dev; ticket → review
