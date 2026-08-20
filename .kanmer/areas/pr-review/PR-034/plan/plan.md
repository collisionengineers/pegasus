# Plan

Estimated diff: about 3 production lines and 15–25 focused test lines.

1. Reuse MimeKit's existing attachment disposition and change only inline-image precedence: an explicit attachment remains an attachment even with Content-ID.
2. Extend the existing occurrence-order integration test so display and canonical ordinals remain identical through the attached image and following attachment.
3. Run the focused retained-mail occurrence test, Release build, and diff check.
4. Record four-lens dispositions and PIR.

No new helper or abstraction is warranted for one predicate.

## Simplification pass — 2026-08-20

- **Reuse:** Reused MimeKit's existing `IsAttachment` signal and the existing canonical/display occurrence test.
- **Simplification:** Explicit attachment precedence is a two-condition predicate change, not a new attachment classifier or identity map.
- **Efficiency:** Parsing, projection, persistence, and attachment enumeration remain single-pass and unchanged.
- **Altitude:** MIME classification stays in the canonical Infrastructure reader; Core projection/store/UI remain untouched. No unapplied findings.
