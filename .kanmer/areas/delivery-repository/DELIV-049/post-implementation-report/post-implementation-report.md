# Post-implementation report — DELIV-049

Base dev: 3da60bd0c270111d5168dc17246dc831882108ea.
Implementation: a84edccd49de692371fcd04423f9190e1b1cf9f9.

## Changes

Removed NOW.md and its exceptions in AGENTS.md, docs/index.md and the existing
Markdown placement guard. Replaced the expired three-stream Git procedure with
current remediation authority. Annotated only the superseded delivery bullet
in protected operator notes; material business statements remain unchanged.
No product, migration or runtime configuration changed.

## Validation

- git diff --check: exit 0.
- git grep -n NOW.md -- AGENTS.md docs scripts: exit 1, no matches (expected).
- pwsh -NoProfile -File scripts/Test-TestMarkdownPlacement.ps1: exit 0;
  Markdown placement regression tests passed.
- pwsh -NoProfile -File scripts/Test-MarkdownPlacement.ps1 -Base
  3da60bd0c270111d5168dc17246dc831882108ea -Head HEAD: exit 0.

The first grouped diagnostic invocation's individual exit was not retained;
no pass is claimed from it. After confirming it was inactive, the standalone
regression script above supplied the retained successful exit. No application
build or full integration suite was run for this documentation/guard edit.

## Simplification

The guard reuses its existing accepted-path matcher with one exception removed.
No new helper, configuration or dependency. Expired multi-stream procedure was
removed; historical source remains in Git and original owner tickets.

## Remaining and review

Cross-OS release implementation remains DELIV-048. Runtime current-state docs
must be refreshed with the final deployment. Independent review and exact-merge
proof are still required; this report is pre-merge evidence only.
