## 2026-08-29 — `site.js` has been edited by two other lanes; read before you start

`src/Pegasus.Web/wwwroot/js/site.js` is PLAT-029's file by `waves.md`, and
PLAT-029 is merged. Two lanes have since edited it with the orchestrator's
knowledge, both under D19 case 2 ("fix it anyway when it is a small change in a
file no in-flight lane owns — but say so loudly"):

- **[[INTK-047]]** (PR #627) rewrote the `[data-dropzone]` block at
  `site.js:175-345`: `.file-row` vocabulary, an indeterminate `<progress>` built
  at `:236-240` and revealed at `:331`, a `reset` handler at `:309-313`, and a
  form-scoped readout at `:177-182`. Without it the `<progress>` capability the
  ticket names has **no caller at all**, so the edit was load-bearing rather than
  incidental. The lane verified no branch or worktree claimed `site.js` at the
  time, including this ticket — TICK-223 had neither.

**What this means for TICK-223.** Your scope — "dialog triggers must keep a
static link target" — lands in the same file. Rebase onto current `dev` and read
the dropzone block before editing; do not assume the file matches what the
ticket was written against.

## The shape to match is now set, twice

[[PLAT-027]]'s remediation adopted the static-target-plus-enhancement pattern for
its Disable and Review controls: a real link to a confirm route that serves a
working POST form, with `data-dialog-open` retained purely as the JavaScript
enhancement. [[ENG-028]] has been told to match the same shape for the Send to
Claude dialog.

TICK-223 is the ticket that *records* this rule, so the convention must end up
described here and implemented identically in all three places. If the three
diverge, this ticket is the one that reconciles them — do not let a fourth
variant appear.

## One deferred finding belongs to you

PLAT-027's review raised **Save dirty-state behaviour** on the Staff accounts
page and it was deferred here, because the behaviour belongs in `site.js` which
that lane was barred from touching. Pick it up with the rest of the file's work.
