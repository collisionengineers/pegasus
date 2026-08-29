## Checklist — PLAT-026

- [ ] Mail settings area rendered on the `admin-layout` shell via `_AdminNav`
      (matches `docs/design/README.md` and EPIC-011 context.md §1.12).
- [ ] Approved mailboxes table: Mailbox, Scope, Last update, State, Activated,
      Subscription, Review folders/Refresh — all present, all real handlers.
- [ ] Mail categories table: Category, State, Review + Add category — all
      present, all real handlers.
- [ ] Activated and Subscription columns' data sources unchanged
      (MAIL-017/018/020/021 behaviour preserved).
- [ ] All literal copy centralized in `OperatorLabels.MailSettings`; no label
      duplicated elsewhere.
- [ ] `OperatorLabels.cs` edited append-only; no existing member reordered.
- [ ] No explanatory copy introduced (labels/values/controls only).
- [ ] Every control maps to a real handler; no inert control.
- [ ] `MailCategories` redirect route left intact; reported to UIIMP-009 as
      the surface to delete in wave 5.
- [ ] Web tests updated to assert new markup/handlers; no assertion weakened,
      skipped, deleted or inverted.
- [ ] Non-administrator access denial still asserted.
- [ ] `catalogue.json` structural entries corrected if stale.
- [ ] `dotnet build` run by this session (not just codex), real exit code
      recorded.
- [ ] Focused test filter for both test classes run by this session, real
      counts recorded.
- [ ] Only PLAT-026's file set touched; anything else reverted.
- [ ] Simplification pass run over the branch diff; findings dispositioned
      under a dated heading.
