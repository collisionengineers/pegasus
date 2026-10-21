# PR body

Title: conventional subject, under 72 characters, naming the behaviour, not
the issue number: `fix(work-centre): bound Sent to Engineer to this week's
events`. The issue number goes in the body.

If the repo has `.github/pull_request_template.md`, keep its headings and add
the ones below that are missing. Write "None" rather than deleting a section.
The PR body is the durable record: issues get closed, branches get deleted,
and the body is what a later reader finds.

```markdown
## Summary

What changed and why, in a few sentences a non-author can follow. Name the
capability or requirement ID the repo uses (FRD/ADR/capability) where one
applies, and the one policy owner that changed.

Closes #<N>

## Operator decisions applied

Each decision the investigation raised and the answer given, with the date
and where (issue comment, chat). "None" if the path was clear.

## How verified

- What CI proves on this head: the lanes and the owning test classes
  (`FooTests`, `BarWebTests`).
- New tests and what each asserts.
- What was checked by hand, if anything, and what remains unproved (a running
  app, the live estate, operator acceptance).

Local builds and tests that CI runs were not repeated, by repo policy.

## Simplification pass

Findings from the pass over this branch's diff: applied (with the commit),
deferred (to which issue), or not applicable (why). "None found" is fine.

## Left out

What the issue asked for or hinted at that this PR deliberately does not do,
and where it went (a new issue, a question to the operator, out of scope).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

`Closes #N` on its own line is what links the issue in GitHub's sidebar and
populates `closingIssuesReferences`, which `combine-prs` reads at landing to
close the issue with a comment. On a non-default base branch the keyword
does not close the issue by itself, and that is intended.

Use the attribution line your environment specifies.
