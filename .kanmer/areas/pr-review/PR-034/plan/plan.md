# Plan

Estimated diff: about 3 production lines and 15–25 focused test lines.

1. Reuse MimeKit's existing attachment disposition and change only inline-image precedence: an explicit attachment remains an attachment even with Content-ID.
2. Extend the existing occurrence-order integration test so display and canonical ordinals remain identical through the attached image and following attachment.
3. Run the focused retained-mail occurrence test, Release build, and diff check.
4. Record four-lens dispositions and PIR.

No new helper or abstraction is warranted for one predicate.
