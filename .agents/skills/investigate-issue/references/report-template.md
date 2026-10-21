# Investigation comment

Post one comment on the issue with `gh issue comment <N> --body-file <file>`.
Fill every section; write "None" rather than deleting a heading, so a reader
can tell an empty section from a forgotten one. Keep it readable by the
operator: plain sentences, file paths only where the reader must go there.

```markdown
## Investigation at `<tip sha>` (<date>)

**Verdict.** One paragraph: what is actually happening, whether the issue's
description still matches the code at the tip, and the recommended approach
in one sentence.

### Current behaviour

Trace the path the issue describes, as it is at the tip: entry point → policy
owner → adapter/persistence → what the user sees. Say where the issue's own
file:line claims still hold and where they have moved (name the PR that moved
them). Separate **verified** facts (with the command or file read) from
**assumed** ones.

### Governing requirement

The FRD/ADR/capability/glossary clause that owns the behaviour, quoted or
linked. Say whether it traces to supplied evidence or reads as an earlier
invention; an invented rule is a candidate for deletion, not a constraint to
design around. "None found" is a valid and important answer.

### Blast radius

| Code | Role | Callers and consumers |
| --- | --- | --- |
| `src/.../Owner.cs` | the one policy owner | Web page X, Worker function Y, MCP tool Z |
| `tests/.../OwnerTests.cs` | owning test class | — |
| migrations / config / fixtures / docs | what else must move | — |

Include data: tables, columns, stored enums, migrations, retained records that
already exist in production and what happens to them.

### Approaches

**A. <name>** — what changes, in which owner. Footprint: ~N files, ~N lines.
Migration: none / additive / destructive. UI: none / Web change under the
repo's UI guardrails. Risks: … Leaves out: …

**B. <name>** — same shape.

(Add C if a genuinely different shape exists; do not pad.)

### Recommendation

Which approach and why, in two or three sentences. Prefer the smallest change
that fully resolves the issue and keeps one policy owner. Name what the
recommendation deliberately does not do.

### Decisions for the operator

Numbered. Only things that are genuinely the operator's call: wording of
user-facing copy, product behaviour the requirement does not settle, whether
an untraced rule is kept or dropped. Write "None" if the path is clear.

### Test plan

- Existing owning classes: `…Tests` (which CI lane runs them).
- New test(s) that prove the fix, by name and what each asserts.
- What a running app or the live estate alone can prove (and so stays out of CI).

### Not covered

What this investigation did not read or could not verify, and why.

---
Investigated by Claude Code against `origin/<base>` @ `<sha>`.
```
