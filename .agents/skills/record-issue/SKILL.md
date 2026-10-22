---
name: record-issue
description: Record a problem, task or decision as a short templated GitHub issue, quickly, so it can be investigated later - what happens, what was expected, where in the code (as far as a quick look goes), done-when, and a type label. It checks for an existing open issue first and files one issue. Use it whenever the user says to raise, file, log, record, note, open or capture an issue, bug, ticket or "something to look at later", describes a defect or an odd behaviour they noticed mid-session, says "make an issue for that", "park this", "add it to the backlog", or asks to split something out of the current work, even if they never say "issue". Not for investigating (investigate-issue), fixing (resolve-issue), or editing an existing issue.
---

# Record an issue

Capture what was seen, well enough that someone can pick it up cold, in a
few minutes. The depth belongs to `investigate-issue` later; this skill's
job is to not lose the observation and to write it so the investigation has
something to resolve. Most of the value is in naming the code and the
observable outcome, not in diagnosing the cause.

Inputs: the user's description, plus whatever the current conversation
already holds (a failing test, a PR under review, a page that misbehaved).
Optionally the repo (`owner/name`) and labels.

## Authority

This repository's AGENTS.md governs every write; invoking this skill authorises none.

Invoking this skill authorises a duplicate search and creating **one**
issue in the repository (the write is implied by the invocation; do not ask
again). It does not authorise editing or closing other issues, assigning,
milestones, or any branch or file change. Echo the title before filing; the
one mistake this skill can make is filing the wrong thing in the wrong repo.

Repo instructions win: read CLAUDE.md/AGENTS.md for glossary terms (use the
reserved vocabulary as it is defined, not a loose synonym) and any issue
conventions.

## Procedure

### 1. Pull the facts out of the conversation first

Most of what the issue needs is already in the session: the Case reference,
the page, the exact text, the test name, the PR number. Write those down
before asking anything. Ask the user only for what is missing and matters:
what was expected, if that is not obvious.

### 2. One quick look at the code

Spend a few minutes, not an investigation: `git grep -n -w` for the label,
message or type the user mentioned, so the issue can name the owning file
and type in backticks. Stop as soon as there is one good anchor. If nothing
turns up quickly, write "Not looked" in that section; a wrong path is worse
than none.

### 3. Check it is not already open

```bash
gh issue list --state open --search "<two or three distinctive words>" --json number,title,url
```

If a match looks like the same thing, show it and ask whether to add a
comment there instead of filing again. A near-miss (same area, different
symptom) is a new issue that cites the other by number.

### 4. Draft from the template

Read `references/issue-template.md` and fill it. Keep it short. Name the
observable "done when" rather than a fix. Pick one `type:*` label the repo
already uses (list them with `gh label list` if unsure).

### 5. File it and report

```bash
gh issue create --title "<title>" --body-file <file> --label "type:bug"
```

Report the issue URL and the one-line title. If the issue was split out of
work in progress (a PR, a plan), add the issue number where that work is
recorded so it is not lost: a line in the PR body's "Left out" section, or
the plan.

## Examples

**Input:** "the valuation card still says press Use this value again but we
removed that button in 1099"
**Title:** `Valuation refusal still says "Press Use this value again" after the button was removed (#1099)`
**Label:** `type:bug`. Body quotes the exact sentence, names the Razor page
and the label constant found by grep, and "Done when: the refusal names the
action that now exists".

**Input:** "we never decided whether Completed counts cases or events, park
that"
**Title:** `Work Centre Activity: should Completed count Cases or completion events?`
**Label:** `type:decision`. Body states the current behaviour (events), the
two readings, and "Done when: FRD-15 says which and the query matches".

## What good looks like

- A stranger can tell what was seen and what should have happened.
- Backticked paths and types resolve at the tip.
- One issue, one type label, no duplicate of an open one.
- Under ten minutes from "record that" to the URL.
