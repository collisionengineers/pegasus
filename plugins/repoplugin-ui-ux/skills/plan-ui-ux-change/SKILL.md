---
name: plan-ui-ux-change
description: Plan an operator-facing repository UI or UX change from requirements inventory through reviewed wireframe directions, approved visual concepts, and an implementation handoff. Use for new or changed screens, flows, forms, dashboards, responsive behavior, accessibility, or visual interaction design.
---

# Plan UI and UX Change

Resolve one shared task with `$repoplugin-task-contracts:resolve-repository-task`. Write ordinary Markdown beneath `.repoplugin/tasks/<task-id>/ui-ux/` using `$repoplugin-task-contracts:persist-repository-task-artifact`; never infer a task from chat or select a latest task. Use `$repoplugin-ui-ux:apply-collision-engineers-ui-style` only for the contained internal-app visual treatment.

## Required route

1. Inspect live repository authorities and the real caller. Create `inventory.md` covering every required feature, role/permission, business policy, state/transition, validation, error, empty, loading, and responsive path. Record assumptions, conflicts, and unknowns in `open-questions.md`; the user resolves material conflicts.
2. Create `ui-spec.md`: information hierarchy, components, behavior, keyboard and screen-reader support, focus/error handling, responsive rules, and acceptance evidence. It must trace every item back to the inventory.
3. Create two or three materially different directions under `wireframes/` as lightweight HTML/CSS or diagrams. Each direction must make its layout, interaction, trade-offs, and unsupported assumptions clear; do not wire production behavior.
4. Assign an independent reviewer to compare every direction with `inventory.md` and `ui-spec.md`. Persist `review.md`, correct gaps, and repeat only where the review finds a material mismatch.
5. Obtain explicit user approval of one direction before generating any visual concept. The approval records the chosen direction and any change request in `decision.md`.
6. After approval, use the available `$imagegen` skill with only the approved description and approved non-sensitive references. Do not send operator material, credentials, corpus data, customer material, or unapproved assets.
7. Manually review the generated concept against the approved UI spec: text, policy/state depiction, accessibility cues, and responsive intent. Iterate only within the approved direction; record `visual-review.md`.
8. Create `implementation-handoff.md` with selected artifacts, caller and policy owner, files/components likely affected, accessibility and responsive acceptance checks, visual-concept limitations, and evidence to collect.

Generated images are concepts only: they do not set requirements, implement behavior, or prove acceptance. Requirements and wireframes may be reported as an intermediate result, but missing approval, unavailable image generation, or an incomplete manual review blocks the final implementation handoff and route completion. A changed requirement creates an ordinary `changes/RC-NNN.md`, identifies affected artifacts, and reopens only that scope.

## Boundaries

- Keep planning artifacts in the shared task folder; do not modify production UI while planning.
- Use bounded parallel exploration only for independent read-only questions, with distinct artifact targets. Keep one accountable author and one independent reviewer.
- Read live authorities instead of copying repository rules into this skill. Escalate contradictions, irreversible choices, or unresolved policy to the user.
- If implementation follows, its owner must use the Codex harness `update_plan` tool before editing or delegating; Markdown checklists are not a substitute.
