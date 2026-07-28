# Case-system connectors

This imported connector proposal has no Pegasus caller. A future adapter may translate vendor
payloads only through an accepted `Pegasus.Core` port; it cannot own case contracts or make a vendor
schema the domain model. Live read or write-back requires separate exact-target approval.
