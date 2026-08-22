# Proof

**Shipped:** PR #506, commit `43488ea9` · **Deployed:** Release 18, `1f3be493`, smoke-asserted source SHA.

## The database question, settled with production data

The operator asked whether these were *duplicate database fields*. Read directly from
production:

```sql
SELECT DISTINCT FieldName FROM CaseDataFields;
```

Twelve names in use; nineteen declared in `CaseDataFieldNames` and enforced by the
`CK_CaseDataFields_FieldName` check constraint. **There is no `odometer` field and no
second mileage field** — one `vehicle_mileage` with `vehicle_mileage_unit` beside it. A
duplicate cannot be introduced by accident, because the constraint would reject it.

QDOS26010 shows exactly one of each:

```
vehicle_mileage       fact  132389
vehicle_mileage_unit  fact  miles
```

## What was wrong, and is now fixed

One value under two names in the UI. `_CaseSummary.cshtml` said **Odometer** where
`_CaseWorkflow.cshtml` said **Mileage**; the summary now says Mileage. At the deployed
revision the only remaining "Odometer" under `Pages/**` is the assessment suggestion label,
which is the engineer's recorded reading — a different fact, Core-owned, deliberately
untouched.

## Judgement on the record

The sweep also found `Make`/`Vehicle make` and `Model`/`Vehicle model` differing between the
same two panels. **Left alone**: those read as obviously the same field, whereas "Odometer"
and "Mileage" read as two different numbers. A reviewer may disagree; the finding is
recorded rather than quietly dropped.

## Evidence tier

The schema half is **observed in production**. The label half is **deployed-code** — the
authenticated case page has not been viewed, because that needs a sign-in I must not
perform.
