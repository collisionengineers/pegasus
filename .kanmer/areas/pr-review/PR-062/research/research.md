# Research

ADR-0032 intentionally replaces only ADR-0002's polling/timer-first trigger clauses. Repository precedent ADR-0030 leaves `supersedes` and the older ADR's `superseded_by` empty for partial clause replacement, while status/body/index prose records the limited relationship. The current PR's reciprocal relationship arrays imply whole-record supersession and conflict with both ADRs remaining accepted.

No runtime, FRD, capability, or deployment change is required.
