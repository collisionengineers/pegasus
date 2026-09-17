# Pegasus UI agent-guardrail pack

This pack is intended to make the current v26 visual/layout contract the default for any coding
agent working in Pegasus Web.

## Recommended repository changes

Add:

- `.agents/skills/pegasus-ui-guardrails/SKILL.md`
- `.agents/skills/pegasus-ui-guardrails/references/case-workspace.md`
- `src/Pegasus.Web/AGENTS.md`
- `src/Pegasus.Web/CLAUDE.md`
- `src/Pegasus.Web/Pages/Cases/AGENTS.md`
- `src/Pegasus.Web/Pages/Cases/CLAUDE.md`

The paired local files deliberately target both AGENTS.md-aware harnesses and Claude Code. The
substantive rules live once in the skill to avoid duplicated policy drifting.

## Required placement-gate change

Pegasus currently rejects new Markdown outside approved documentation/integration roots. Do not
weaken that gate generally.

Extend `Test-AllowedMarkdownPath` in `scripts/Test-MarkdownPlacement.ps1` with a narrow exception
for only these local instruction files, for example:

```powershell
if ($normalized -in @(
    'src/Pegasus.Web/AGENTS.md',
    'src/Pegasus.Web/CLAUDE.md',
    'src/Pegasus.Web/Pages/Cases/AGENTS.md',
    'src/Pegasus.Web/Pages/Cases/CLAUDE.md'
)) {
    return $true
}
```

Keep the existing regex for every other Markdown file.

## Root routing addition

Add one short rule to both root `AGENTS.md` and root `CLAUDE.md`:

> Any change under `src/Pegasus.Web` must follow
> `.agents/skills/pegasus-ui-guardrails/SKILL.md` and any nearer local
> `AGENTS.md`/`CLAUDE.md`. A Web feature or bug fix is not permission to redesign the UI.

This makes the guardrail visible even to an agent/harness that does not automatically load nested
instruction files.

## CI / mechanical guardrails worth adding

Text instructions are not sufficient for strong enforcement. Add a low-noise UI-design check that
fails changed Razor/UI code for:

- inline `<style>` blocks;
- inline `style=` attributes;
- new external font/CSS/icon/script CDNs;
- known retired operator copy such as `Send to Claude`;
- reintroduction of `Add valuation` in the Case valuation surface;
- accidental page-local use of raw product colours where an approved token exists.

Keep mechanical checks narrow. Do not attempt to lint "good design" with brittle regexes.

More valuable than broad CSS linting is visual-contract evidence:

1. Keep the existing shell/Case structural Web tests.
2. Add Playwright screenshot baselines for the shared shell and Case frame at:
   - 1580×1000;
   - 1440×900 (or the current smaller-desktop reference);
   - 760px for shared-layout changes.
3. Treat baseline updates as design changes: they require explicit design authorization, not merely a
   failing snapshot.
4. Add targeted regression tests for decisions that already regressed in live review:
   - Damage does not render the Case image strip;
   - Estimate does not render the redundant Engineer's-Value lock pill;
   - guide valuation has no `Add valuation` route;
   - each guide source has one source-card route;
   - toolbar selects are not globally forced full width;
   - Cancel does not open the dirty-edit confirmation.

## Why this is stricter than the existing Razor skills

The current Razor design skill intentionally says it supplies judgment rather than a house style.
That is appropriate generically, but Pegasus now *has* an operator-approved house style and a
mature design authority. The additional mandatory skill converts those project-specific decisions
into default constraints while still allowing explicit operator-directed redesigns.
