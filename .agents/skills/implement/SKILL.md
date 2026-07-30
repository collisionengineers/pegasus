---
name: implement
description: "Implement a piece of work based on a spec or set of tickets."
disable-model-invocation: true
---

Implement the work described by the user in the activated spec or tickets.

Before changing files, recover and continue the material change's existing issue/change record, branch, and pull request. If any identity is missing for material work, establish it through the repository workflow before implementation; do not create a parallel work identity.

Before any destructive or external mutation:

1. Enumerate the exact targets and intended mutations.
2. Run the available read-only rehearsal or preview against those targets.
3. Capture the current target identity and baseline needed to detect drift.
4. Prove the recovery path covers every target and preserves required data.
5. State the expected post-operation state and stop on any preview, identity, baseline, recovery, or result mismatch.
6. Obtain the repository-required explicit approval for those exact targets.

Use /tdd where possible, at pre-agreed seams.

Run typechecking regularly, single test files regularly, and the full test suite once at the end.

Once done, use /code-review to review the complete fixed-point-to-working-tree change, including committed, staged, unstaged, and untracked files that belong to the work.

Commit only the scoped work to the current branch, preserving unrelated changes and staging.
