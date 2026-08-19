# Open questions — MAIL-13

- [ ] Confirm the mutation scope and especially deletion semantics. Recommendation: implement read/unread, approved Outlook categories and flags with explicit confirmation/history, but defer delete until the operator specifies soft-delete/recovery, authorization and retention behaviour; never hard-delete through Pegasus.

## Parked (explicitly deferred)

- [ ] Real Outlook/Graph/cloud activation and live verification — requires explicit approval for exact targets and operations.
