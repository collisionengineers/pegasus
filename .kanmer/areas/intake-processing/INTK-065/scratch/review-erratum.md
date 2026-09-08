## Independent-review census erratum — 2026-09-08

This note preserves `scratch/review.md` as the historical pre-merge attestation
and corrects only its sentence “All 220 referenced ids resolve.” No product
source, generated package, attestation, proof, or prior command output is
rewritten.

### Provenance of 220

The independent review's read-only PowerShell census at approximately
`2026-09-08T14:38:35Z` built a case-sensitive HashSet from the `id` fields
of exactly three top-level collections:

```powershell
foreach ($group in @(
    $json.sourceSnapshots,
    $json.evaluationSummaries,
    $json.evidenceItems))
{
    foreach ($item in $group)
    {
        $null = $ids.Add([string]$item.id)
    }
}
```

That set produced the retained output `Resolvable ID count: 220`. It is the
declared resolver-target universe, not an occurrence count and not the count of
distinct strings appearing in `evidenceRefs`.

The same command separately traversed every nested `evidenceRefs` array. For
each non-`sha256:` reference it removed any `#fragment` and required the
base ID to exist in that declared set. Its retained result was
`Unresolved evidence refs: 0`. That zero-unresolved result, rather than the
number 220, is the resolution evidence used by the acceptance decision.

### Exact-merge read-only census

A fresh read-only census of the tracked JSON at integration SHA
`d76de2534ec6651c1a434a55f76593b7b140bf1c` found:

- declared resolver-target IDs across the three collections: **220**;
- all nested `evidenceRefs` occurrences: **1,538**;
- distinct raw `evidenceRefs` strings: **376**;
- `sha256:` occurrences: **182**;
- non-`sha256:` occurrences: **1,356**;
- distinct non-`sha256:` base IDs after fragment removal: **143**;
- unresolved non-`sha256:` occurrences: **0**.

Correct reading: the package contains 1,538 nested reference occurrences
(376 distinct raw strings), and every non-hash reference resolves against the
220-ID declared target universe. The historical review's use of “220
referenced ids” conflated those two censuses.

This count correction does not change the scoped acceptance requirement, which
was resolution rather than a prescribed reference count. It does not convert
the retained exact-merge verifier-harness failure into PASS or authorize a
rerun; verification retains its own recorded stop and requires its own grant.
