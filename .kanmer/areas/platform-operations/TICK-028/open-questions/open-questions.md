# Open questions

## Parked (explicitly deferred)

- [ ] Approve a one-off point-in-time restore drill into a new database db name pegasus-restore-drill-<date> (Azure write) to evidence RTO/RPO end to end

Reason parked: this is the residual half of OPS-09 not covered by this pass —
the read-only posture check (retention, redundancy, earliest restore point,
SKU/size) and the documented restore procedure are complete, but creating an
actual restored database is an Azure write under
[the live-operation approval matrix](../../../../docs/runbook.md#live-operation-approval-matrix)
("Deploy, restore, fail over, or retire" row) and is explicitly out of scope
for a lane (no Azure writes permitted). It needs operator exact-target
approval (subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, resource group
`rg-pegasus-prod`, server `pegasus-prod-sql-252ow37gij`, new database named
`pegasus-restore-drill-<date>`) before it can run.
