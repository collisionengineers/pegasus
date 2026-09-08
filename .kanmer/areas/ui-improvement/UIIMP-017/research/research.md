# UIIMP-017 research

Read complete pegasus_pack/current/uiimp-005-snapshot-diagnosis.md and actual
Health Razor, ApprovedMailboxAdministrationWebTests and TestUiSnapshotTests
at accepted dev19e6f523bf6760cab39104b4dca3674b0ac8a512. Source behavior
matches the diagnosis: five metrics instants interpolate raw UTC/culture;
service rows already use OperatorLabels.OfficeTime. Existing design/README
one-clock rule requires London across all operator instants.

Health's default matcher accepts any Health heading without Access denied;
three existing successful cohorts are semantically different. Preserve the
existing populated fixed-clock mailbox scenario, selected by the exact
persisted graph_unavailable table cell, rather than suppressing timestamps.
Failed CI34132950893 at3da60bd0 records stale Health output but retains no exact
failing capture bytes; do not claim its precise field was diagnosed from CI.

Reuse actual route test ThePageShowsActivationAndSubscriptionHealthPerMailbox,
existing StateMatch predicate and OfficeTime formatter. No new helper/service,
dependency, fixture host, business data, runtime flag or global clock change.
Historical UIIMP005 foreign claim and priorPR588/609 evidence stay untouched.
