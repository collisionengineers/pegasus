# Post-implementation report — ENG-016

## Outcome

Export is the single current send-to-Engineer action. A reviewed case produces the deterministic JSON/image package for staff to import into EVA. The planned successors are the EVA API when usable and direct estimating-system integrations that replace EVA; AI-generated estimates may remain in Pegasus for Engineer review and report generation.

## Implementation

- `Review` is the sole business readiness gate. `CaseLifecycleRules.ValidateReviewReadiness` now rejects incomplete instructions or images even when staff-review flags are true.
- Export enforces `Review` server-side. It adds no duplicate Case custody, Audit custody, or accepted-only evidence policy.
- `MapForOperatorExport` remains the one mapping: suggested values are retained; VAT and Mileage are optional; Mileage requires Unit when present; blank Inspection Date defaults to the export date.
- Saving case data retains the existing completeness invalidation and `Not ready` transition, and now tells the operator that completeness must be confirmed again.
- Every successful export writes structured, replay-safe `ActionHistory`; the first also writes the once-per-Case `EvaFirstHandoffProxies` dashboard fact.
- The POST carries antiforgery and an operation key. The response carries `Content-Digest` for the archive SHA-256.
- The duplicate hand-off routes, UI, MCP tools, Core ports and persistence tables remain deleted.
- The migration is a direct pre-cutover removal under ADR-0030. This unreleased product has no rollback/legacy compatibility path; recovery is roll-forward.

## Action-history record

Event `eva_bundle_exported` is stored against the Case for every distinct successful export. Its structured result includes the case version, mapping key/version/evidence, exported fields and provenance, bundle and JSON hashes, and each image occurrence/document/version/hash/source identity. Reusing an operation key is accepted only for an exact replay.

## Simplification pass

- Reused the existing lifecycle/completeness owner, EVA mapper, image selector, action-history helper and first-export proxy.
- Added no new service, wrapper, compatibility layer or second readiness list.
- Removed an unnecessary `Cases` query because `CaseDataProjection.Identity` already carries the canonical reference.
- Updated FRD-07 for business behaviour and current architecture for implementation facts.
- All findings applied; none deferred.

## Verification

| Check | Result |
| --- | --- |
| Locked restore | Passed |
| Release build | Passed, 0 warnings / 0 errors |
| Full Core | 975 passed |
| Full Architecture | 99 passed |
| Full Integration | 945 passed, 2 declared skips |
| Final-build focused Core | 40 passed |
| Final-build focused Integration | 2 passed |
| `git diff --check` | Passed |
| Commit | `cf28b8b0` |
| PR | #539 |

No deploy or release was performed, so `docs/operations.md` was not changed.
