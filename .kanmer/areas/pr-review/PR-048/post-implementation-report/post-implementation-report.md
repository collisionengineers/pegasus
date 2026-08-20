# Post-implementation report

Resolved in `6b7c62a4` on PR #490. Mail now prepares the existing Case lease before confirmation and the exact final link/unlink POST calls the canonical Core command without fresh Web state checks. SQL/Web proof repeats successful link and unlink submissions with one history entry each, then proves a changed reason under the same key conflicts. No Core, EF, schema or generic command owner changed.
