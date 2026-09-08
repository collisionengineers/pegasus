Root exact-merge setup: PR706 confirmed MERGED at d76de2534ec6651c1a434a55f76593b7b140bf1c. Current declared pr.yml/verify/push lookup exits1 HTTP404 absent workflow; obligations missing under ordinary local fallback, not CI PASS. Reconciliation produced no recommendation and records unavailable required-check/reachability facts; no recovery mutation applied. After receipt lookup, created clean exactdetached .worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c; HEAD/exact/detached checks confirmed. Lease renewed revision6 verifying. No postmerge verification command started: solehost currentlyD56. Root additionally found erroneous command spellings/frozenSHA in premerge scratch/report; actualexecution receipts are being audited and corrected explicitly, not silently overwritten or rerun. No proof/Done claim yet.

## Exact-merge fallback attempt — stopped non-PASS — 2026-09-08

Authorized detached integration target: `d76de2534ec6651c1a434a55f76593b7b140bf1c`; merge parent: `a1f0bfe260ea05df531df6e0ca3109141e7697da`. This attempt did not regenerate or write the tracked package and does not claim full-original regeneration.

### Preflight

- `2026-09-08T15:37:32.0775140Z`: `git rev-parse HEAD`, `git rev-parse HEAD^`, `git symbolic-ref --short -q HEAD`, `git status --porcelain=v1 --untracked-files=all`, `git rev-parse --git-common-dir`, and scoped process census — exit 0; exact HEAD and parent above, detached, clean, common Git directory `C:/Users/Alex/Documents/GitHub/pegasus/.git`. The census found three idle MSBuild node-reuse `dotnet` processes (PIDs 7460, 18340, 30128; each command line was `dotnet.exe ...MSBuild.dll /noautoresponse /nologo /nodemode:1 /nodeReuse:true /low:false`), not an active test invocation.
- `2026-09-08T15:38:01.4276462Z`: `dotnet build-server shutdown` — exit 0; MSBuild and compiler servers shut down, and the scoped verification-process census was empty.

### Commands completed before the stop

1. `dotnet restore ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --locked-mode`
   - attempted_at: `2026-09-08T15:38:10.9274326Z`
   - exit_code: **0**
2. `dotnet build ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-restore`
   - attempted_at: `2026-09-08T15:38:20.2923516Z`
   - exit_code: **0**
   - output: build succeeded; 0 warnings, 0 errors.
3. `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PrincipalIdentificationCorpusTests" --logger "trx;LogFileName=intk-065-exactmerge-d76de253-20260908-1538.trx" --results-directory ./artifacts/verification`
   - attempted_at: `2026-09-08T15:38:57.6543208Z`
   - exit_code: **0**
   - result: 7 passed, 0 failed, 0 skipped.
   - retained TRX: `artifacts/verification/intk-065-exactmerge-d76de253-20260908-1538.trx`
   - retained TRX SHA-256: `83AEA0E4A0DB98D6C1174A10FA48201C03FBBF16F7AB0CEEAF07E8FB083CEA6E`.
4. `python -m unittest discover -s scripts/reference_data/tests -p test_build_principal_identification_corpus.py`
   - attempted_at: `2026-09-08T15:39:09.7589368Z`
   - exit_code: **0**
   - result: 2 tests ran; OK.
5. `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1`
   - attempted_at: `2026-09-08T15:39:18.3555778Z`
   - exit_code: **0**
   - result: all relative Markdown links resolve; 140 files checked.
6. `pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1`
   - attempted_at: `2026-09-08T15:39:29.1358160Z`
   - exit_code: **0**
   - result: Markdown placement regression tests passed.
7. `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base a1f0bfe260ea05df531df6e0ca3109141e7697da -Head d76de2534ec6651c1a434a55f76593b7b140bf1c`
   - attempted_at: `2026-09-08T15:39:48.2645880Z`
   - exit_code: **0**
   - result: placement passed for the actual merge-parent range.

### Stop event

8. `python -B ./artifacts/intk-065-exactmerge-verify.py`
   - attempted_at: `2026-09-08T15:42:38.5240248Z`
   - exit_code: **1**
   - retained output: `AssertionError: Expected 220 evidence references, found 1538`.
   - disposition: **verifier-harness interpretation failure; exact-merge result remains non-PASS**. The ignored one-off harness counted every nested `evidenceRefs` occurrence, whereas the independent review states “all 220 referenced ids resolve.” It reached this assertion after generator syntax, canonical-byte/hash, seven historical-object equality versus the merge parent, and five-current-snapshot checks, but no partial success is promoted to PASS. The harness was removed after the attempt. Under the explicit stop-on-first-failure/no-retry rule, it was not corrected or rerun; a fresh root grant is required for any harness-only correction.

Read-only post-stop census at `2026-09-08T15:43:13.6885691Z`: HEAD remained exact; tracked/untracked status remained clean; no scoped verification process remained; tracked package SHA-256 remained `494E0A0F42CED164AAB97CD50EBB497C1479C09EAF9F0A4DB177949BBDC7C251`. No source fix, new framework, regeneration, full rail, proof, Done movement, or ticket-worktree cleanup occurred.

## Exact-merge structural correction — PASS — 2026-09-08

This is the root-inspected harness-only correction to the earlier exact-merge attempt's retained exit 1. The original `2026-09-08T15:42:38.5240248Z` failure remains authoritative attempt history: it incorrectly asserted that every nested `evidenceRefs` occurrence count must equal 220 and observed 1,538. Root established that 220 instead describes the resolver-target universe (37 source snapshots + 1 evaluation summary + 182 evidence items), read the complete corrected harness, confirmed its semantics, and explicitly authorized one execution only.

Preflight at `2026-09-08T16:07:00.5235017Z` confirmed exact detached HEAD `d76de2534ec6651c1a434a55f76593b7b140bf1c`, clean Git status, no scoped verification process, and staged ignored harness SHA-256 `11973FB514CF477D473ECD6E7E8E1571B77510277511CB1A10E30C5C4589681A`.

### Exact command and result

`python -B ./artifacts/intk-065-exactmerge-verify.py`
- attempted_at: `2026-09-08T16:07:09.1887499Z`
- exit_code: **0**
- exact output:
  - `GENERATOR_SYNTAX=PASS`
  - `CANONICAL_BYTES=PASS`
  - `PACKAGE_SHA256=494e0a0f42ced164aab97cd50ebb497c1479c09eaf9f0a4db177949bbdc7c251`
  - `CURRENT_POLICY_SNAPSHOTS_VALID=5`
  - `RESOLVER_TARGETS_UNIQUE=220`
  - `NESTED_EVIDENCE_REF_LISTS=1376`
  - `NESTED_EVIDENCE_REF_OCCURRENCES=1538`
  - `HASH_EVIDENCE_REF_OCCURRENCES=182`
  - `NONHASH_EVIDENCE_REF_OCCURRENCES=1356`
  - `ALL_NESTED_EVIDENCE_REFS_RESOLVE=PASS`
  - `HISTORICAL_OBJECTS_UNCHANGED_VS_MERGE_PARENT=7`
  - `APPROVED_EVIDENCE_REF_REPLACEMENTS=26`
  - `ALLOWED_JSON_DELTA=PASS`

Postcheck at `2026-09-08T16:07:46.5724774Z` retained exact HEAD, clean Git status, no scoped process, the same harness hash, and tracked package SHA-256 `494E0A0F42CED164AAB97CD50EBB497C1479C09EAF9F0A4DB177949BBDC7C251`.

Disposition: **PASS** for the previously missing exact-merge structural obligation. Combined with the earlier exact-SHA restore/build, focused Core 7/7, Python unit 2/2, documentation-link 140-file PASS, Markdown regression PASS, and actual merge-parent placement PASS, the scoped exact-merge fallback is complete. The earlier harness failure is preserved; no package regeneration/write, other command rerun, source mutation, proof, Done movement, cleanup, or ENG check occurred.

### Retained corrected ignored harness

Path at execution: `artifacts/intk-065-exactmerge-verify.py`
SHA-256: `11973FB514CF477D473ECD6E7E8E1571B77510277511CB1A10E30C5C4589681A`

```python
from __future__ import annotations

import ast
import copy
import hashlib
import importlib.util
import json
import re
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PACKAGE_RELATIVE_PATH = "reference/workproviders-and-repairers/principal-identification-corpus.v1.json"
PACKAGE_PATH = ROOT / PACKAGE_RELATIVE_PATH
GENERATOR_PATH = ROOT / "scripts/reference_data/build_principal_identification_corpus.py"
EXPECTED_PACKAGE_SHA256 = "494e0a0f42ced164aab97cd50ebb497c1479c09eaf9f0a4db177949bbdc7c251"
EXPECTED_SNAPSHOTS = {
    "principal-mail-route-v1": "src/Pegasus.Core/Intake/PrincipalMailRoutePolicy.cs",
    "principal-mail-classification-v1": "src/Pegasus.Core/Intake/Classification/PrincipalMailClassificationPolicy.cs",
    "principal-case-match-v1": "src/Pegasus.Core/Intake/CaseMatching/PrincipalCaseMatchPolicy.cs",
    "qdos-extraction-policy-v8": "src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs",
    "shared-mail-taxonomy": "src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs",
}
OLD_SNAPSHOT_IDS = {
    "qdos-route-policy-v4",
    "qdos-runtime-policy-v5",
    "qdos-case-match-policy-v1",
    "qdos-extraction-policy-v7",
    "shared-mail-taxonomy",
}
REVERSE_REFERENCE_IDS = {
    "principal-mail-route-v1": "qdos-route-policy-v4",
    "principal-mail-classification-v1": "qdos-runtime-policy-v5",
    "principal-case-match-v1": "qdos-case-match-policy-v1",
    "qdos-extraction-policy-v8": "qdos-extraction-policy-v7",
}
HISTORICAL_KEYS = (
    "coverage",
    "evaluationSummaries",
    "evidenceItems",
    "historicalCrosswalks",
    "runtimeContract",
    "sharedTaxonomy",
    "supportingIdentities",
)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def reverse_evidence_refs(value: object) -> int:
    changed = 0
    if isinstance(value, dict):
        for key, child in value.items():
            if key == "evidenceRefs" and isinstance(child, list):
                for index, reference in enumerate(child):
                    if reference in REVERSE_REFERENCE_IDS:
                        child[index] = REVERSE_REFERENCE_IDS[reference]
                        changed += 1
            else:
                changed += reverse_evidence_refs(child)
    elif isinstance(value, list):
        for child in value:
            changed += reverse_evidence_refs(child)
    return changed


def collect_evidence_ref_lists(value: object, path: str = "$") -> list[tuple[str, list[str]]]:
    found: list[tuple[str, list[str]]] = []
    if isinstance(value, dict):
        for key, child in value.items():
            child_path = f"{path}.{key}"
            if key == "evidenceRefs":
                require(isinstance(child, list), f"evidenceRefs is not a list at {child_path}")
                require(all(isinstance(item, str) for item in child), f"Non-string evidenceRef at {child_path}")
                found.append((child_path, child))
            else:
                found.extend(collect_evidence_ref_lists(child, child_path))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            found.extend(collect_evidence_ref_lists(child, f"{path}[{index}]"))
    return found


source = GENERATOR_PATH.read_text(encoding="utf-8")
ast.parse(source, filename=str(GENERATOR_PATH))
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("intk065_exactmerge_generator", GENERATOR_PATH)
require(spec is not None and spec.loader is not None, "Generator module could not be loaded")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

current_bytes = PACKAGE_PATH.read_bytes()
actual_sha256 = hashlib.sha256(current_bytes).hexdigest()
require(actual_sha256 == EXPECTED_PACKAGE_SHA256, "Final package SHA-256 drifted")
current = json.loads(current_bytes)
require(module.canonical_json_bytes(current) == current_bytes, "Tracked package bytes are not canonical generator bytes")

parent_bytes = subprocess.check_output(["git", "show", f"HEAD^:{PACKAGE_RELATIVE_PATH}"], cwd=ROOT)
parent = json.loads(parent_bytes)
require(current.keys() == parent.keys(), "Top-level package keys changed")
for key in HISTORICAL_KEYS:
    require(current[key] == parent[key], f"Historical object differs from merge parent: {key}")

policy_snapshots = [
    item
    for item in current["sourceSnapshots"]
    if item["relativePath"].startswith("src/Pegasus.Core/Intake/")
]
require(len(policy_snapshots) == 5, f"Expected exactly 5 current policy snapshots, found {len(policy_snapshots)}")
current_snapshots = {item["id"]: item for item in policy_snapshots}
require(set(current_snapshots) == set(EXPECTED_SNAPSHOTS), "Current policy snapshot IDs drifted")
for source_id, relative_path in EXPECTED_SNAPSHOTS.items():
    expected = module.snapshot(
        source_id,
        "pegasus",
        ROOT,
        relative_path,
        "source-code",
        hash_mode="normalized-lf",
    )
    require(current_snapshots[source_id] == expected, f"Current snapshot metadata drifted: {source_id}")

resolver_items = current["sourceSnapshots"] + current["evaluationSummaries"] + current["evidenceItems"]
resolver_ids = [item["id"] for item in resolver_items]
require(len(resolver_ids) == len(set(resolver_ids)), "Duplicate resolver target ID")
resolver_id_set = set(resolver_ids)
ref_lists = collect_evidence_ref_lists(current)
reference_count = sum(len(refs) for _, refs in ref_lists)
hash_reference_count = 0
nonhash_reference_count = 0
for path, refs in ref_lists:
    for reference in refs:
        if reference.startswith("sha256:"):
            require(re.fullmatch(r"sha256:[0-9a-f]{64}", reference) is not None, f"Invalid hash evidenceRef at {path}: {reference}")
            hash_reference_count += 1
        else:
            require(reference.split("#", 1)[0] in resolver_id_set, f"Unresolved evidenceRef at {path}: {reference}")
            nonhash_reference_count += 1

parent_remaining = [item for item in parent["sourceSnapshots"] if item["id"] not in OLD_SNAPSHOT_IDS]
current_remaining = [item for item in current["sourceSnapshots"] if item["id"] not in EXPECTED_SNAPSHOTS]
require(current_remaining == parent_remaining, "A non-current source snapshot changed")
normalized = copy.deepcopy(current)
normalized["purpose"] = parent["purpose"]
normalized["sourceSnapshots"] = parent["sourceSnapshots"]
reference_change_count = reverse_evidence_refs(normalized)
require(reference_change_count == 26, f"Expected 26 approved evidence-reference replacements, found {reference_change_count}")
require(normalized == parent, "JSON delta exceeds approved purpose, current snapshots, and evidenceRefs")

print("GENERATOR_SYNTAX=PASS")
print("CANONICAL_BYTES=PASS")
print(f"PACKAGE_SHA256={actual_sha256}")
print(f"CURRENT_POLICY_SNAPSHOTS_VALID={len(policy_snapshots)}")
print(f"RESOLVER_TARGETS_UNIQUE={len(resolver_ids)}")
print(f"NESTED_EVIDENCE_REF_LISTS={len(ref_lists)}")
print(f"NESTED_EVIDENCE_REF_OCCURRENCES={reference_count}")
print(f"HASH_EVIDENCE_REF_OCCURRENCES={hash_reference_count}")
print(f"NONHASH_EVIDENCE_REF_OCCURRENCES={nonhash_reference_count}")
print("ALL_NESTED_EVIDENCE_REFS_RESOLVE=PASS")
print(f"HISTORICAL_OBJECTS_UNCHANGED_VS_MERGE_PARENT={len(HISTORICAL_KEYS)}")
print(f"APPROVED_EVIDENCE_REF_REPLACEMENTS={reference_change_count}")
print("ALLOWED_JSON_DELTA=PASS")
```
