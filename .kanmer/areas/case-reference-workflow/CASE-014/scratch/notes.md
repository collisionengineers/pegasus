## 2026-08-22 — what is left, exactly

Deployed in release 18 (`1f3be493`) and carried to release 20 (`05fe7a7f`).
Not yet exercised, and the production data says so plainly:

```
Reference   AuditReference   Created
QDOS26010   a.QDOS26010      2026-08-22T02:01:04Z
QDOS26009   a.QDOS26009      2026-08-21T23:00:16Z
```

Both carry **two** identities — the pre-fix shape. Both were created before this
fix reached the estate (QDOS26010 at 02:01Z, release 18 deployed at roughly
03:2xZ), so this is evidence the fix has not run yet, not evidence it failed.
After the fix a case's own `Reference` carries the `a.` / `ap.` prefix and
`AuditReference` is null.

**Single gate:** one instruction through the live pipeline. Nothing else is
outstanding — code shipped, tests green, checklist 7/8 with only live
observation unticked.
