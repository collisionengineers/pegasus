# Proof — TICK-204

## Verified target

- Branch: `dev` in the main repository checkout.
- Pull request: https://github.com/collisionengineers/pegasus/pull/412
- PR state: merged at `2026-08-19T09:17:00Z`.
- Exact merge commit: `314a9b266560446d25afe4648148181fb27779b8`.
- Included ticket commits:
  - `545a287d50bc9ab223db632e4c1905e575f1121e`
  - `8124ae2abf0ccbe24f57b52703c4dc48e6e6719c`
- `git merge-base --is-ancestor` returned success for both ticket commits against merged `dev`.
- Verification used merged `dev`; no `main` checkout, promotion, deployment, or cloud write occurred.

## Evidence

### Exact merged diff

Command:

```powershell
git diff --check 314a9b266560446d25afe4648148181fb27779b8^1 314a9b266560446d25afe4648148181fb27779b8
git diff --stat 314a9b266560446d25afe4648148181fb27779b8^1 314a9b266560446d25afe4648148181fb27779b8
git diff --name-only 314a9b266560446d25afe4648148181fb27779b8^1 314a9b266560446d25afe4648148181fb27779b8
```

Result:

- `git diff --check` produced no errors.
- Exactly one file changed: `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.
- Diff size: 33 insertions, no deletions relative to the merge commit's first parent.

### Contract behavior

Command:

```powershell
rg -n "RPT-02|total_loss|repairable|cash_in_lieu|contract_repair|distinct fourth outcome|Core-computed VAT-inclusive repair total|accepted raw cost components|fails closed|not a second policy owner|immutable artifact/version identity|sentDateTime" docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
```

Observed on merged `dev`:

- One closed RPT-02 vocabulary names `total_loss`, `repairable`, `cash_in_lieu`, and `contract_repair`.
- Contract repair is explicitly a distinct fourth outcome.
- Each outcome has its own title, badge, headline figures, and settlement meaning.
- Contract repair uses the Core-computed VAT-inclusive repair total as the agreed cap.
- Readiness uses accepted raw cost components rather than a separate capped-amount input.
- Missing, unknown, conflicting, or incomplete outcome data fails closed.
- Supplied renderer material is evidence, not a second policy owner.
- Existing immutable artifact/version/hash, correction/addendum, and authoritative Outlook `sentDateTime` rules remain present.

Negative check:

```powershell
rg -n "accepted capped amount|accepted VAT-inclusive contract-repair amount" docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
```

Result: no matches; the review defect recorded by PR-003 is absent from merged `dev`.

### Pull-request checks

Command:

```powershell
gh pr checks 412
```

Result:

- `changes`: pass
- `documentation`: pass
- `reference-data`: pass
- Browser, infrastructure, SQL integration, SQL coverage, and unit jobs were skipped by the repository change classifier for this one-file documentation change.

## Qualification

This proof establishes the merged documentation contract on `dev`. It does not claim renderer implementation, application caller activation, Azure deployment, live verification, or `dev` to `main` promotion. The main checkout's pre-existing local `.codex/config.toml` modification was preserved and was not part of this ticket.
