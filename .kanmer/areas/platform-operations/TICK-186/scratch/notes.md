## 2026-08-20 — command log (for proof.md)

```
$ find corpus -maxdepth 1 -type f -iname "*.eml" | wc -l
256

$ find corpus -maxdepth 1 -type f ! -iname "*.eml" -printf '%f\t%s\n'
test_explode_eml.cpython-314-pytest-9.0.3.pyc	26182
test_explode_eml.py	5654

$ python3 artifacts/evaluation/extraction-cohort/20260820/_build_manifest.py
total=256 cohort=204 holdout=52

# reproducibility check: copy manifest, rerun, diff
$ cp artifacts/evaluation/extraction-cohort/20260820/manifest.csv /tmp/manifest_before.csv
$ python3 artifacts/evaluation/extraction-cohort/20260820/_build_manifest.py
total=256 cohort=204 holdout=52
$ diff /tmp/manifest_before.csv artifacts/evaluation/extraction-cohort/20260820/manifest.csv && echo "IDENTICAL - reproducible"
IDENTICAL - reproducible

# csv-aware validation (avoids naive comma-split errors from commas inside filenames)
$ python3 -c "
import csv
from collections import Counter
with open('artifacts/evaluation/extraction-cohort/20260820/manifest.csv', newline='', encoding='utf-8') as f:
    rows = list(csv.DictReader(f))
print('total rows:', len(rows))
print('split counts:', dict(Counter(r['split'] for r in rows)))
names = Counter(r['filename'] for r in rows)
print('duplicate filenames:', {k:v for k,v in names.items() if v>1})
hashes = Counter(r['sha256'] for r in rows)
print('duplicate sha256 groups:', len({k:v for k,v in hashes.items() if v>1}))
"
total rows: 256
split counts: {'cohort': 204, 'holdout': 52}
duplicate filenames: {}
duplicate sha256 groups: 4

# confirmed none of the 4 duplicate-content groups straddles the split boundary
# (hash_sort_index pairs: [72,73] cohort; [193,194] cohort; [243,244] holdout;
#  [249,250,251] holdout — cut is at index 203/204, none of these touch it)
```

Artifacts produced (local-only, gitignored, no repo diff):
- artifacts/evaluation/extraction-cohort/20260820/manifest.csv
- artifacts/evaluation/extraction-cohort/20260820/_build_manifest.py
- artifacts/evaluation/extraction-cohort/20260820/README.md
