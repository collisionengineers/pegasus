# UI Queues — task plan

Branch `task/ui-queues`. Pages 3 and 11.

## The screen

"Triage queue" spent a reserved business term on a page that is mostly not
about Triage-type work. It is **Queues**: the work waiting before a case
reaches an Engineer.

Four tabs with counts: **Not ready · Review · Held · Triage**. The first three
are Case stages; Triage is a separate pre-case entity with its own lifecycle,
which is exactly why it keeps a tab rather than being folded in as a stage.
"Needs sorting" is deliberately absent — it means unmatched e-mail, and it
lives in the Inbox.

The five triage-record states become sub-states of the Triage tab. At page
level they read as case stages, which they are not.

The Dashboard's Active-cases tiles now open these tabs one-to-one.

## The triage record (page 11)

One container: header band with registration, origin, opened date and state;
an action bar; the body. **Complete keeps its place, disabled, with its
condition named on the control** — "Available once a finding is recorded".
Removing it would say completion is impossible here, which is false.

The record's own identifiers are gone from the page: receipt GUIDs, the
evaluation revision, the source hash, and the version integers in history.
None of them is something an operator can act on.

## Verification

- Integration 399 passed / 0 failed
- The triage test now asserts the container, the absence of each internal
  identifier, and the stated condition on the disabled action
