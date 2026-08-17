# Plan

## Target areas

- mail-communications (MAIL, #16a085)
- automation-integrations (AUTO, #6c5ce7)
- documents-reports (DOCS, #2980b9)
- engineering-assessment (ENG, #27ae60)
- intake-processing (INTAKE, #e67e22)
- platform-operations (PLAT, #0984e3)
- delivery-repository (DELIV, #6366f1)
- case-reference-workflow (CASE, #c0392b)
- kanmer-meta (KANMER, #5b8cff; retained column)

## Expected working-set counts

- automation-integrations: 27 active / 29 archived / 56 total
- case-reference-workflow: 6 active / 8 archived / 14 total
- delivery-repository: 9 active / 7 archived / 16 total
- documents-reports: 24 active / 4 archived / 28 total
- engineering-assessment: 19 active / 2 archived / 21 total
- intake-processing: 17 active / 12 archived / 29 total
- kanmer-meta: 6 active / 0 archived / 6 total
- mail-communications: 28 active / 12 archived / 40 total
- platform-operations: 12 active / 24 archived / 36 total

The total is 246: 245 pre-existing tickets plus KANMER-004.

## Migration sequence

1. Create the eight new areas and normalize Kanmer Meta.
2. Create six groups and write their binding context.
3. Add exact group memberships using fresh reads, preserving existing groups.
4. Migrate mixed-area exceptions individually with optimistic concurrency.
5. Remove every obsolete area with migrate_to for the remaining direct mappings.
6. Reorder the nine areas, then verify counts, invariants, rosters and idempotency.
7. Record independent review, proof and closeout.

## Exact group rosters

### simplification (17)

SIMPLI-001, SIMPLI-002, SIMPLI-003, SIMPLI-004, SIMPLI-005, SIMPLI-006, SIMPLI-007, SIMPLI-008, SIMPLI-009, SIMPLI-010, SIMPLI-011, SIMPLI-012, SIMPLI-013, SIMPLI-014, SIMPLI-015, TICK-220, TICK-221

### ui (38)

TICK-009, TICK-010, TICK-044, TICK-047, TICK-048, TICK-049, TICK-050, TICK-051, TICK-052, TICK-053, TICK-054, TICK-056, TICK-057, TICK-064, TICK-076, TICK-092, TICK-093, TICK-094, TICK-095, TICK-105, TICK-106, TICK-107, TICK-118, TICK-128, TICK-130, TICK-136, TICK-137, TICK-170, TICK-172, TICK-173, TICK-174, TICK-178, TICK-179, TICK-181, TICK-184, TICK-185, UICASE-001, UIOPER-001

### renderer (19)

SIMPLI-013, SIMPLI-014, SIMPLI-015, TICK-203, TICK-204, TICK-205, TICK-206, TICK-207, TICK-208, TICK-209, TICK-210, TICK-211, TICK-212, TICK-213, TICK-214, TICK-215, TICK-216, TICK-220, TICK-221

### ai (48)

SIMPLI-001, SIMPLI-012, TICK-023, TICK-024, TICK-025, TICK-026, TICK-027, TICK-062, TICK-063, TICK-070, TICK-071, TICK-072, TICK-073, TICK-074, TICK-087, TICK-101, TICK-102, TICK-103, TICK-104, TICK-144, TICK-145, TICK-146, TICK-147, TICK-148, TICK-149, TICK-150, TICK-151, TICK-152, TICK-153, TICK-154, TICK-155, TICK-156, TICK-157, TICK-158, TICK-159, TICK-160, TICK-161, TICK-162, TICK-163, TICK-164, TICK-165, TICK-166, TICK-167, TICK-168, TICK-169, TICK-171, TICK-176, TICK-192

### external (23)

TICK-015, TICK-016, TICK-020, TICK-021, TICK-022, TICK-058, TICK-059, TICK-060, TICK-061, TICK-069, TICK-077, TICK-078, TICK-079, TICK-080, TICK-082, TICK-083, TICK-084, TICK-085, TICK-086, TICK-089, TICK-090, TICK-091, TICK-119

### qdos (61)

SIMPLI-003, SIMPLI-007, SIMPLI-008, SIMPLI-009, SIMPLI-010, TICK-001, TICK-002, TICK-003, TICK-004, TICK-005, TICK-006, TICK-007, TICK-008, TICK-009, TICK-010, TICK-011, TICK-012, TICK-013, TICK-014, TICK-015, TICK-016, TICK-017, TICK-018, TICK-019, TICK-020, TICK-021, TICK-022, TICK-023, TICK-024, TICK-025, TICK-026, TICK-027, TICK-028, TICK-029, TICK-030, TICK-031, TICK-032, TICK-033, TICK-042, TICK-065, TICK-102, TICK-108, TICK-109, TICK-110, TICK-111, TICK-112, TICK-113, TICK-114, TICK-115, TICK-116, TICK-117, TICK-118, TICK-119, TICK-120, TICK-121, TICK-122, TICK-123, TICK-124, TICK-125, TICK-218, TICK-219

## Roster rules

- Simplification: every ticket originally in simplify.
- Cross-domain UI: every ticket originally in the four UI areas.
- Renderer/extractor: SIMPLI-013..015, TICK-203..216 and TICK-220..221.
- AI/Automation Actor: original Automation AI area plus SIMPLI-001 and SIMPLI-012.
- External integrations: original provider API, DVLA/DVSA/WhatsApp/guided capture and EVA handoff areas.
- QDOS alpha: non-source-now tickets carrying now, plus the explicitly identified simplification/cutover/acceptance records listed above.

## Acceptance

Exactly nine areas; exact counts above; six group derived rosters equal these lists; existing group membership unchanged; all non-area invariants preserved; zero unmapped tickets; second classifier pass produces zero area or membership patches.
