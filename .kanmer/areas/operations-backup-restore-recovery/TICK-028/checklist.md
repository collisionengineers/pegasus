# Recovery capability checklist

## Migrated validation — [[TICK-190]]

- [ ] Back up the template database against the supported external SQL Server boundary.
- [ ] Restore into a new database and verify the restored schema and data.
- [ ] Record the achieved recovery evidence against the 15-minute RPO and four-hour RTO objectives.

## Migrated validation — [[TICK-191]]

- [ ] Observe reclamation behavior for abandoned LocalDB databases and backup files.
- [ ] Record retained, reclaimed, and failure outcomes without destructive cleanup outside an approved target.
