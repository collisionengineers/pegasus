# Research

## Question

What is the smallest correction for the broken relative link inherited by PR #560?

## Findings

- `.grok/skills/kanmer-setup/SKILL.md` links from `.grok/skills/kanmer-setup/` to `../../../../docs/manual/greenfield.md`; that target does not exist in `origin/dev`.
- Commit `9061c4c6` introduced the link. No tracked `greenfield.md` or equivalent planning-depth guide exists in this repository.
- `scripts/Test-DocumentationLinks.ps1` scans tracked Markdown outside its excluded trees, including `.grok`, and therefore correctly fails.
- PR #560 does not modify the broken skill file; it inherits the failure from its `dev` base.

## Implication

Remove the unsupported external-reference sentence. The numbered greenfield workflow immediately below remains complete, so no replacement document, compatibility path, or documentation tree is required.
